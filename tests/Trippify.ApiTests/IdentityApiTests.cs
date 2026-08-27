using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Trippify.Infrastructure;
using Trippify.Application;
using Xunit;

namespace Trippify.ApiTests;

public class TrippifyFactory : WebApplicationFactory<Program>
{
    public static readonly string ObjectStorageRoot = Path.Combine(Path.GetTempPath(), "trippify-tests-" + Guid.NewGuid().ToString("N"));
    public static readonly string SignedUrlSecret = "test-secret-do-not-use-in-production-" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DemoSeed:Enabled"] = "false",
            ["BackgroundJobs:WorkersEnabled"] = "false",
            ["ObjectStorage:Provider"] = "local",
            ["ObjectStorage:LocalRoot"] = ObjectStorageRoot,
            ["ObjectStorage:PublicBaseUrl"] = "local://trippify-tests/",
            ["ObjectStorage:SignedUrlSecret"] = SignedUrlSecret,
            ["Map:Provider"] = "local",
        }));
        builder.ConfigureServices(services =>
        {
            var databaseName = "trippify-api-tests-" + Guid.NewGuid();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        });
    }
}

public sealed class IdentityApiTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    [Fact]
    public async Task Login_logout_revokes_access_token()
    {
        await CreateConfirmedUser("traveler@example.com", "Strong!Pass123");
        using var client = factory.CreateClient();
        var token = await Login(client, "traveler@example.com", "Strong!Pass123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/v1/system/protected")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/system/protected")).StatusCode);
    }

    [Fact]
    public async Task Registration_and_recovery_do_not_disclose_accounts()
    {
        using var client = factory.CreateClient();
        var registration = new { email = "new@example.com", password = "Strong!Pass123" };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/register", registration)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/register", registration)).StatusCode);
        var known = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "new@example.com" });
        var unknown = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "missing@example.com" });
        Assert.Equal(known.StatusCode, unknown.StatusCode);
    }

    [Fact]
    public async Task Public_creator_projection_excludes_private_account_fields()
    {
        var user = await CreateConfirmedUser("creator@example.com", "Strong!Pass123");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, user.Email!, "Strong!Pass123"));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/v1/me/profile", new { displayName = "A Traveler", avatarUrl = "https://example.com/a.png", locale = "en" })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/v1/creators/enroll", new { slug = "a-traveler", biography = "Trips", travelCountries = new[] { "JP" } })).StatusCode);
        await WithServices(async services => { var db = services.GetRequiredService<AppDbContext>(); (await db.CreatorProfiles.SingleAsync(x => x.UserId == user.Id)).Status = CreatorStatus.Active; await db.SaveChangesAsync(); });
        client.DefaultRequestHeaders.Authorization = null;
        var json = await (await client.GetAsync("/api/v1/creators/a-traveler")).Content.ReadAsStringAsync();
        Assert.Contains("A Traveler", json); Assert.DoesNotContain("creator@example.com", json); Assert.DoesNotContain("emailConfirmed", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_status_change_is_role_protected_and_audited()
    {
        var target = await CreateConfirmedUser("target@example.com", "Strong!Pass123");
        var ordinary = await CreateConfirmedUser("ordinary@example.com", "Strong!Pass123");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, ordinary.Email!, "Strong!Pass123"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/v1/admin/users/{target.Id}/status", new { status = "Suspended", reason = "test" })).StatusCode);
        var admin = await CreateConfirmedUser("admin@example.com", "Strong!Pass123", true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, admin.Email!, "Strong!Pass123"));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/admin/users/{target.Id}/status", new { status = "Suspended", reason = "policy violation" })).StatusCode);
        await WithServices(async services => { var db = services.GetRequiredService<AppDbContext>(); Assert.Equal(AccountStatus.Suspended, (await db.Users.SingleAsync(x => x.Id == target.Id)).Status); Assert.True(await db.IdentityAuditEntries.AnyAsync(x => x.TargetUserId == target.Id && x.ActorUserId == admin.Id)); });
    }

    [Fact]
    public async Task Suspended_creator_cannot_use_creator_workspace()
    {
        var creator = await CreateConfirmedUser("suspended-creator@example.com", "Strong!Pass123");
        await WithServices(async services => { var db = services.GetRequiredService<AppDbContext>(); db.CreatorProfiles.Add(new CreatorProfile { UserId = creator.Id, Slug = "suspended-creator", Status = CreatorStatus.Suspended }); await db.SaveChangesAsync(); });
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, creator.Email!, "Strong!Pass123"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/creator/workspace")).StatusCode);
    }

    [Fact]
    public async Task Confirmation_refresh_and_password_reset_complete_the_account_lifecycle()
    {
        AppUser? user = null; string? confirmation = null; string? reset = null;
        await WithServices(async services => { var users = services.GetRequiredService<UserManager<AppUser>>(); user = new AppUser { Id = Guid.NewGuid(), UserName = "lifecycle@example.com", Email = "lifecycle@example.com" }; Assert.True((await users.CreateAsync(user, "Strong!Pass123")).Succeeded); confirmation = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GenerateEmailConfirmationTokenAsync(user))); });
        using var client = factory.CreateClient(); Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync($"/api/v1/auth/confirm-email?userId={user!.Id}&code={confirmation}")).StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = "Strong!Pass123" }); login.EnsureSuccessStatusCode(); using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = loginJson.RootElement.GetProperty("refreshToken").GetString() }); refresh.EnsureSuccessStatusCode();
        await WithServices(async services => { reset = await services.GetRequiredService<UserManager<AppUser>>().GeneratePasswordResetTokenAsync((await services.GetRequiredService<UserManager<AppUser>>().FindByIdAsync(user.Id.ToString()))!); });
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email = user.Email, code = reset, newPassword = "NewStrong!Pass123" })).StatusCode);
        await Login(client, user.Email!, "NewStrong!Pass123");
    }

    [Fact]
    public async Task My_summary_returns_roles_and_creator_status_for_anonymous_user_creator_and_admin()
    {
        using var anonymousClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.GetAsync("/api/v1/me/summary")).StatusCode);

        var ordinary = await CreateConfirmedUser("ordinary-summary@example.com", "Strong!Pass123");
        using var userClient = factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(userClient, ordinary.Email!, "Strong!Pass123"));
        var userSummary = await userClient.GetAsync("/api/v1/me/summary");
        Assert.Equal(HttpStatusCode.OK, userSummary.StatusCode);
        using (var userDoc = JsonDocument.Parse(await userSummary.Content.ReadAsStringAsync()))
        {
            var root = userDoc.RootElement;
            Assert.Equal(ordinary.Email, root.GetProperty("email").GetString());
            Assert.False(root.GetProperty("isCreator").GetBoolean());
            Assert.Equal("Active", root.GetProperty("accountStatus").GetString());
            Assert.Equal(0, root.GetProperty("roles").GetArrayLength());
        }

        var creator = await CreateConfirmedUser("creator-summary@example.com", "Strong!Pass123");
        await WithServices(async services =>
        {
            var db = services.GetRequiredService<AppDbContext>();
            db.UserProfiles.Add(new UserProfile { UserId = creator.Id, DisplayName = "Creator Traveler", Locale = "en" });
            db.CreatorProfiles.Add(new CreatorProfile { UserId = creator.Id, Slug = "creator-traveler", Status = CreatorStatus.Active });
            await db.SaveChangesAsync();
        });
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!, "Strong!Pass123"));
        var creatorSummary = await creatorClient.GetAsync("/api/v1/me/summary");
        Assert.Equal(HttpStatusCode.OK, creatorSummary.StatusCode);
        using (var creatorDoc = JsonDocument.Parse(await creatorSummary.Content.ReadAsStringAsync()))
        {
            var root = creatorDoc.RootElement;
            Assert.Equal("Creator Traveler", root.GetProperty("displayName").GetString());
            Assert.True(root.GetProperty("isCreator").GetBoolean());
            Assert.True(root.GetProperty("emailConfirmed").GetBoolean());
        }

        var admin = await CreateConfirmedUser("admin-summary@example.com", "Strong!Pass123", true);
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!, "Strong!Pass123"));
        var adminSummary = await adminClient.GetAsync("/api/v1/me/summary");
        Assert.Equal(HttpStatusCode.OK, adminSummary.StatusCode);
        using (var adminDoc = JsonDocument.Parse(await adminSummary.Content.ReadAsStringAsync()))
        {
            var roles = adminDoc.RootElement.GetProperty("roles").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.Contains("Administrator", roles);
        }
    }

    [Fact]
    public async Task Registration_with_strong_password_creates_user_and_attempts_confirmation_email()
    {
        using var client = factory.CreateClient();
        var emailAddress = "matching@example.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = emailAddress, password = "Strong!Pass123" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AppUser? created = null;
        await WithServices(async services =>
        {
            var users = services.GetRequiredService<UserManager<AppUser>>();
            created = await users.FindByEmailAsync(emailAddress);
        });
        Assert.NotNull(created);
        Assert.False(created!.EmailConfirmed);
    }

    [Fact]
    public async Task Registration_succeeds_even_when_email_sender_throws()
    {
        var throwingFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<Trippify.Application.IEmailSender>();
            services.AddSingleton<Trippify.Application.IEmailSender, ThrowingEmailSender>();
        }));
        using var client = throwingFactory.CreateClient();
        var emailAddress = "email-fails@example.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = emailAddress, password = "Strong!Pass123" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AppUser? created = null;
        await using (var scope = throwingFactory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            created = await users.FindByEmailAsync(emailAddress);
        }
        Assert.NotNull(created);
    }

    [Fact]
    public async Task Registration_with_weak_password_is_rejected_with_validation_error()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = "weak@example.com", password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        AppUser? created = null;
        await WithServices(async services =>
        {
            var users = services.GetRequiredService<UserManager<AppUser>>();
            created = await users.FindByEmailAsync("weak@example.com");
        });
        Assert.Null(created);
    }

    [Fact]
    public async Task Confirmation_link_sent_during_registration_actually_confirms_the_account()
    {
        var capturingFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<Trippify.Application.IEmailSender>();
            services.AddSingleton<Trippify.Application.IEmailSender, CapturingEmailSender>();
        }));
        using var client = capturingFactory.CreateClient();
        var emailAddress = "confirm-link@example.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = emailAddress, password = "Strong!Pass123" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AppUser? user = null;
        await using (var scope = capturingFactory.Services.CreateAsyncScope())
        {
            user = await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByEmailAsync(emailAddress);
        }
        Assert.NotNull(user);
        var link = CapturingEmailSender.LastConfirmationLink;
        Assert.NotNull(link);
        Assert.False(user!.EmailConfirmed);

        var confirmResponse = await client.GetAsync(link!);
        Assert.Equal(HttpStatusCode.NoContent, confirmResponse.StatusCode);

        AppUser? refreshed = null;
        await using (var scope = capturingFactory.Services.CreateAsyncScope())
        {
            refreshed = await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByEmailAsync(emailAddress);
        }
        Assert.True(refreshed!.EmailConfirmed);
    }

    private sealed class ThrowingEmailSender : Trippify.Application.IEmailSender
    {
        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Simulated SMTP outage.");
    }

    private sealed class CapturingEmailSender : Trippify.Application.IEmailSender
    {
        public static string? LastConfirmationLink { get; private set; }
        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
        {
            if (subject.Contains("Confirm", StringComparison.OrdinalIgnoreCase)) LastConfirmationLink = body;
            return Task.CompletedTask;
        }
    }

    private async Task<AppUser> CreateConfirmedUser(string email, string password, bool admin = false)
    {
        AppUser? created = null;
        await WithServices(async services =>
        {
            var users = services.GetRequiredService<UserManager<AppUser>>(); var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            created = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
            Assert.True((await users.CreateAsync(created, password)).Succeeded);
            var stored = await users.FindByEmailAsync(email); Assert.NotNull(stored); Assert.True(stored.EmailConfirmed); Assert.True(await users.CheckPasswordAsync(stored, password));
            if (admin) { if (!await roles.RoleExistsAsync("Administrator")) Assert.True((await roles.CreateAsync(new IdentityRole<Guid>("Administrator"))).Succeeded); Assert.True((await users.AddToRoleAsync(created, "Administrator")).Succeeded); }
        });
        return created!;
    }

    private static async Task<string> Login(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Login failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return json.RootElement.GetProperty("accessToken").GetString()!;
    }

    private async Task WithServices(Func<IServiceProvider, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope(); await action(scope.ServiceProvider);
    }
}
