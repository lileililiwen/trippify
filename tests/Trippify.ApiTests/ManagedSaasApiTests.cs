using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Trippify.Application;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

public sealed class ManagedSaasApiTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    private const string Password = "Strong!Pass123";
    private const string AdminRole = "Administrator";

    [Fact]
    public async Task Administrator_can_create_tenant_and_user_gets_own_tenant_on_first_call()
    {
        var admin = await CreateUser("saas-admin@example.com", admin: true);
        var user = await CreateUser("saas-user@example.com");
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));
        var created = await adminClient.PostAsJsonAsync("/api/v1/admin/tenants", new { slug = "acme", displayName = "Acme", primaryDomain = (string?)null });
        created.EnsureSuccessStatusCode();
        var tenant = await Json(created);
        Assert.Equal("acme", tenant.GetProperty("slug").GetString());

        using var userClient = factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(userClient, user.Email!));
        var mine = await Json(userClient, "/api/v1/me/tenant");
        Assert.Equal("Default Tenant", mine.GetProperty("tenant").GetProperty("displayName").GetString());
        Assert.Equal("Free", mine.GetProperty("subscription").GetProperty("plan").GetString());
    }

    [Fact]
    public async Task Non_administrator_cannot_list_tenants_but_can_browse_own()
    {
        var admin = await CreateUser("saas-priv-admin@example.com", admin: true);
        var user = await CreateUser("saas-priv-user@example.com");
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));
        var r1 = await adminClient.PostAsJsonAsync("/api/v1/admin/tenants", new { slug = "ten1", displayName = "Ten1" });
        r1.EnsureSuccessStatusCode();
        var r2 = await adminClient.PostAsJsonAsync("/api/v1/admin/tenants", new { slug = "ten2", displayName = "Ten2" });
        r2.EnsureSuccessStatusCode();

        using var userClient = factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(userClient, user.Email!));
        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.GetAsync("/api/v1/admin/tenants")).StatusCode);
        var mine = await userClient.GetAsync("/api/v1/me/tenant");
        mine.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Admin_can_set_quota_on_user_tenant_and_user_sees_it()
    {
        var admin = await CreateUser("saas-q-admin@example.com", admin: true);
        var user = await CreateUser("saas-q-user@example.com");
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));
        using var userClient = factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(userClient, user.Email!));

        var mineInit = await userClient.GetAsync("/api/v1/me/tenant");
        mineInit.EnsureSuccessStatusCode();

        Guid userTenantId = Guid.Empty;
        await WithDb(async db =>
        {
            var member = await db.TenantMembers.AsNoTracking().SingleAsync(m => m.UserId == user.Id);
            userTenantId = member.TenantId;
        });

        var setResponse = await adminClient.PutAsJsonAsync($"/api/v1/admin/tenants/{userTenantId}/quotas/Guides", new { limit = 7 });
        setResponse.EnsureSuccessStatusCode();
        var setJson = await Json(setResponse);
        Assert.Equal(7, setJson.GetProperty("limit").GetInt32());

        var quotas = await Json(userClient, "/api/v1/me/tenant/quotas");
        var guidesQuota = quotas.GetProperty("items").EnumerateArray().First(q => q.GetProperty("metric").GetString() == "Guides");
        Assert.Equal(7, guidesQuota.GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task Subscription_update_to_same_plan_is_idempotent()
    {
        var admin = await CreateUser("saas-sub-admin@example.com", admin: true);
        var user = await CreateUser("saas-sub-user@example.com");
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));
        using var userClient = factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(userClient, user.Email!));

        (await userClient.GetAsync("/api/v1/me/tenant")).EnsureSuccessStatusCode();

        var first = await userClient.PostAsJsonAsync("/api/v1/me/tenant/subscription", new { plan = "Pro" });
        first.EnsureSuccessStatusCode();
        int before = 0;
        await WithDb(async db =>
        {
            var member = await db.TenantMembers.AsNoTracking().SingleAsync(m => m.UserId == user.Id);
            before = await db.TenantAuditEntries.AsNoTracking().CountAsync(e => e.TenantId == member.TenantId && e.Action == "subscription-updated");
        });

        var second = await userClient.PostAsJsonAsync("/api/v1/me/tenant/subscription", new { plan = "Pro" });
        second.EnsureSuccessStatusCode();
        await WithDb(async db =>
        {
            var member = await db.TenantMembers.AsNoTracking().SingleAsync(m => m.UserId == user.Id);
            var actual = await db.TenantAuditEntries.AsNoTracking().CountAsync(e => e.TenantId == member.TenantId && e.Action == "subscription-updated");
            Assert.Equal(before, actual);
        });
    }

    [Fact]
    public async Task Export_payload_includes_only_callers_purchases()
    {
        var user = await CreateUser("saas-export-user@example.com");
        var otherUser = await CreateUser("saas-export-other@example.com");
        using var userClient = factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(userClient, user.Email!));
        (await userClient.GetAsync("/api/v1/me/tenant")).EnsureSuccessStatusCode();

        await WithDb(async db =>
        {
            db.PurchaseEntitlements.Add(new PurchaseEntitlement { Id = Guid.NewGuid(), GuideId = Guid.NewGuid(), UserId = user.Id, OrderId = Guid.NewGuid(), GrantedAt = DateTimeOffset.UtcNow });
            db.PurchaseEntitlements.Add(new PurchaseEntitlement { Id = Guid.NewGuid(), GuideId = Guid.NewGuid(), UserId = otherUser.Id, OrderId = Guid.NewGuid(), GrantedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        });

        var exportResponse = await userClient.PostAsync("/api/v1/me/tenant/export", null);
        exportResponse.EnsureSuccessStatusCode();
        var exportBody = await exportResponse.Content.ReadAsStringAsync();
        var export = JsonDocument.Parse(exportBody).RootElement.Clone();
        Assert.Equal(user.Id, export.GetProperty("userId").GetGuid());
        Assert.Equal(1, export.GetProperty("purchases").GetArrayLength());
    }

    [Fact]
    public async Task Deletion_request_requires_reason_and_records_audit_entry()
    {
        var admin = await CreateUser("saas-del-admin@example.com", admin: true);
        var user = await CreateUser("saas-del-user@example.com");
        using var userClient = factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(userClient, user.Email!));
        (await userClient.GetAsync("/api/v1/me/tenant")).EnsureSuccessStatusCode();

        var missing = await userClient.PostAsJsonAsync("/api/v1/me/tenant/deletion", new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var response = await userClient.PostAsJsonAsync("/api/v1/me/tenant/deletion", new { reason = "no longer needed" });
        response.EnsureSuccessStatusCode();

        await WithDb(async db =>
        {
            var member = await db.TenantMembers.AsNoTracking().SingleAsync(m => m.UserId == user.Id);
            Assert.True(await db.TenantAuditEntries.AsNoTracking().AnyAsync(e => e.TenantId == member.TenantId && e.Action == "deletion-requested" && e.Reason == "no longer needed"));
        });
    }

    private async Task WithDb(Func<AppDbContext, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<JsonElement> Json(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private async Task<JsonElement> Json(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private Task<AppUser> CreateUser(string email, bool creator = false, bool admin = false) => CreateUser(factory.Services, email, creator, admin);
    private static async Task<AppUser> CreateUser(IServiceProvider services, string email, bool creator = false, bool admin = false)
    {
        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
        Assert.True((await manager.CreateAsync(user, Password)).Succeeded);
        if (creator) db.CreatorProfiles.Add(new CreatorProfile { UserId = user.Id, Slug = user.Id.ToString("N"), Status = CreatorStatus.Active });
        if (admin)
        {
            if (!await roles.RoleExistsAsync(AdminRole)) Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(AdminRole))).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, AdminRole)).Succeeded);
        }
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<string> Login(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("accessToken").GetString()!;
    }
}
