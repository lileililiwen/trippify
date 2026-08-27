using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Trippify.Application;
using Trippify.Api;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

public sealed class PluginsApiTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    private const string Password = "Strong!Pass123";
    private const string AdminRole = "Administrator";

    [Fact]
    public async Task Administrator_can_register_plugin_with_valid_signature_and_it_appears_in_catalog()
    {
        var admin = await CreateUser("plug-admin@example.com", admin: true);
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));

        var manifest = "{\"id\":\"demo\",\"name\":\"Demo\",\"version\":\"1.0.0\",\"permissions\":[\"ReadGuides\"]}";
        var signature = PluginSignature.Sign(manifest, PluginSignature.DefaultSecret);
        var register = await adminClient.PostAsJsonAsync("/api/v1/admin/plugins", new
        {
            slug = "demo",
            displayName = "Demo Plugin",
            version = "1.0.0",
            publisher = "Trippify Test",
            manifest,
            signature,
        });
        register.EnsureSuccessStatusCode();
        var plugin = await Json(register);
        Assert.Equal("demo", plugin.GetProperty("slug").GetString());

        using var anon = factory.CreateClient();
        var list = await Json(anon, "/api/v1/plugins");
        Assert.True(list.GetProperty("total").GetInt32() >= 1);
        Assert.Equal("demo", list.GetProperty("items")[0].GetProperty("slug").GetString());
    }

    [Fact]
    public async Task Plugin_registration_with_invalid_signature_is_rejected_before_persistence()
    {
        var admin = await CreateUser("plug-sig-admin@example.com", admin: true);
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));

        var response = await adminClient.PostAsJsonAsync("/api/v1/admin/plugins", new
        {
            slug = "evil",
            displayName = "Evil",
            version = "1.0.0",
            publisher = "unknown",
            manifest = "{\"x\":1}",
            signature = "sha256=deadbeef",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await WithDb(async db =>
        {
            Assert.False(await db.Plugins.AsNoTracking().AnyAsync(x => x.Slug == "evil"));
        });
    }

    [Fact]
    public async Task Non_administrator_cannot_register_plugin_but_can_browse_catalog()
    {
        var admin = await CreateUser("plug-priv-admin@example.com", admin: true);
        var user = await CreateUser("plug-priv-user@example.com");
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));
        await RegisterApprovedPluginAsync(adminClient, "sample", "Sample");

        using var userClient = factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(userClient, user.Email!));
        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.PostAsJsonAsync("/api/v1/admin/plugins", new { slug = "nope", displayName = "n", version = "1.0.0", publisher = "p", manifest = "{}", signature = "sha256=x" })).StatusCode);

        using var anon = factory.CreateClient();
        var list = await Json(anon, "/api/v1/plugins");
        Assert.True(list.GetProperty("total").GetInt32() >= 1);
    }

    [Fact]
    public async Task User_can_install_enable_disable_and_uninstall_and_audit_is_recorded()
    {
        var admin = await CreateUser("plug-lc-admin@example.com", admin: true);
        var user = await CreateUser("plug-lc-user@example.com");
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));
        var pluginId = await RegisterApprovedPluginAsync(adminClient, "lifecycle", "Lifecycle");

        using var userClient = factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(userClient, user.Email!));
        var install = await userClient.PostAsJsonAsync($"/api/v1/me/plugins/{pluginId}/install", new { scopes = new[] { "ReadGuides" } });
        install.EnsureSuccessStatusCode();

        var enable = await userClient.PostAsync($"/api/v1/me/plugins/{pluginId}/enable", null);
        Assert.Equal(HttpStatusCode.NoContent, enable.StatusCode);
        var enableAgain = await userClient.PostAsync($"/api/v1/me/plugins/{pluginId}/enable", null);
        Assert.Equal(HttpStatusCode.NoContent, enableAgain.StatusCode);

        var disable = await userClient.PostAsync($"/api/v1/me/plugins/{pluginId}/disable", null);
        Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);

        var remove = await userClient.DeleteAsync($"/api/v1/me/plugins/{pluginId}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var listResponse = await userClient.GetAsync("/api/v1/me/plugins/installations");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        Assert.True(listResponse.IsSuccessStatusCode, $"status={listResponse.StatusCode} body={listBody}");
        var list = JsonDocument.Parse(listBody).RootElement.Clone();
        var installation = list.EnumerateArray().Single();
        Assert.Equal("Disabled", installation.GetProperty("lifecycle").GetString());
        Assert.NotEqual(default, installation.GetProperty("uninstalledAt").GetDateTimeOffset());

        await WithDb(async db =>
        {
            var audits = await db.PluginAuditEntries.AsNoTracking().Where(x => x.PluginId == pluginId).ToListAsync();
            Assert.Contains(audits, a => a.Action.StartsWith("installed"));
            Assert.Contains(audits, a => a.Action.StartsWith("enabled"));
            Assert.Contains(audits, a => a.Action.StartsWith("disabled"));
            Assert.Contains(audits, a => a.Action.StartsWith("uninstalled"));
        });
    }

    [Fact]
    public async Task Anonymous_cannot_list_my_installations_or_install_audit()
    {
        using var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/me/plugins/installations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/admin/plugins/audit")).StatusCode);
    }

    [Fact]
    public async Task Installing_without_scopes_is_rejected_and_does_not_create_installation()
    {
        var admin = await CreateUser("plug-noscope-admin@example.com", admin: true);
        var user = await CreateUser("plug-noscope-user@example.com");
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));
        var pluginId = await RegisterApprovedPluginAsync(adminClient, "noscope", "NoScope");

        using var userClient = factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(userClient, user.Email!));
        var result = await userClient.PostAsJsonAsync($"/api/v1/me/plugins/{pluginId}/install", new { scopes = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        await WithDb(async db => Assert.False(await db.PluginInstallations.AsNoTracking().AnyAsync(x => x.PluginId == pluginId)));
    }

    [Fact]
    public async Task Audit_responses_do_not_leak_manifest_secret()
    {
        var admin = await CreateUser("plug-audit@example.com", admin: true);
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));
        await RegisterApprovedPluginAsync(adminClient, "audit-test", "Audit Test");
        var audit = await (await adminClient.GetAsync("/api/v1/admin/plugins/audit")).Content.ReadAsStringAsync();
        var parsed = JsonDocument.Parse(audit).RootElement.Clone();
        foreach (var entry in parsed.GetProperty("items").EnumerateArray())
        {
            Assert.NotEqual("signature", entry.GetProperty("action").GetString(), StringComparer.OrdinalIgnoreCase);
            Assert.NotEqual("manifest", entry.GetProperty("action").GetString(), StringComparer.OrdinalIgnoreCase);
            Assert.NotEqual("manifest", entry.GetProperty("reason").GetString(), StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<Guid> RegisterApprovedPluginAsync(HttpClient adminClient, string slug, string displayName)
    {
        var manifest = $"{{\"slug\":\"{slug}\",\"permissions\":[\"ReadGuides\"]}}";
        var signature = PluginSignature.Sign(manifest, PluginSignature.DefaultSecret);
        var response = await adminClient.PostAsJsonAsync("/api/v1/admin/plugins", new
        {
            slug,
            displayName,
            version = "1.0.0",
            publisher = "Trippify Test",
            manifest,
            signature,
        });
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
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

    private async Task WithDb(Func<AppDbContext, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
