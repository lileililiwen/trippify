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

public sealed class SelfHostedApiTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    private const string Password = "Strong!Pass123";
    private const string AdminRole = "Administrator";

    [Fact]
    public async Task Public_system_info_endpoint_returns_version_only()
    {
        using var anon = factory.CreateClient();
        var response = await anon.GetAsync("/api/v1/system/info");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        Assert.False(payload.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.False(payload.Contains("Signature", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Non_administrator_cannot_trigger_upgrade_backup_or_feature_flag_writes()
    {
        var user = await CreateUser("sh-nonadmin@example.com");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, user.Email!));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/system/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/v1/admin/system/upgrade", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/admin/system/backup", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/v1/admin/feature-flags/discoverSearch", new { enabled = true, value = "{}" })).StatusCode);
    }

    [Fact]
    public async Task Administrator_can_run_upgrade_idempotently_and_upsert_feature_flags()
    {
        var admin = await CreateUser("sh-admin@example.com", admin: true);
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));

        var first = await adminClient.PostAsync("/api/v1/admin/system/upgrade", null);
        first.EnsureSuccessStatusCode();
        var second = await adminClient.PostAsync("/api/v1/admin/system/upgrade", null);
        second.EnsureSuccessStatusCode();

        var upsert = await adminClient.PutAsJsonAsync("/api/v1/admin/feature-flags/discoverSearch", new { enabled = true, value = "{\"level\":\"beta\"}" });
        upsert.EnsureSuccessStatusCode();
        var list = await adminClient.GetAsync("/api/v1/admin/feature-flags");
        list.EnsureSuccessStatusCode();
        var body = await list.Content.ReadAsStringAsync();
        Assert.Contains("discoverSearch", body);
    }

    [Fact]
    public async Task Backup_then_restore_records_snapshots_with_actor_and_label()
    {
        var admin = await CreateUser("sh-backup@example.com", admin: true);
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));

        var backup = await adminClient.PostAsJsonAsync("/api/v1/admin/system/backup", new { label = "smoke" });
        backup.EnsureSuccessStatusCode();
        var backupJson = await Json(backup);
        Assert.Equal("smoke", backupJson.GetProperty("label").GetString());

        var restore = await adminClient.PostAsJsonAsync("/api/v1/admin/system/restore", new { payload = "{\"hello\":\"world\"}" });
        restore.EnsureSuccessStatusCode();
        var restoreJson = await Json(restore);
        Assert.Equal("stored", restoreJson.GetProperty("status").GetString());

        await WithDb(async db =>
        {
            Assert.True(await db.BackupSnapshots.AsNoTracking().CountAsync() >= 2);
        });
    }

    private async Task WithDb(Func<AppDbContext, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private Task<AppUser> CreateUser(string email, bool admin = false) => CreateUser(factory.Services, email, admin);
    private static async Task<AppUser> CreateUser(IServiceProvider services, string email, bool admin = false)
    {
        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
        Assert.True((await manager.CreateAsync(user, Password)).Succeeded);
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
