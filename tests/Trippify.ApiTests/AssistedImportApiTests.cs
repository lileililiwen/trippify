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

public sealed class AssistedImportApiTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    private const string Password = "Strong!Pass123";

    [Fact]
    public async Task Text_import_produces_pending_draft_with_provenance_and_does_not_publish()
    {
        var user = await CreateUser("ai-importer@example.com");
        using var userClient = factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(userClient, user.Email!));

        var response = await userClient.PostAsJsonAsync("/api/v1/me/imports/text", new { sourceText = new string('a', 60) });
        response.EnsureSuccessStatusCode();
        var job = await Json(response);
        Assert.Equal("Completed", job.GetProperty("status").GetString());
        Assert.Equal("Text", job.GetProperty("kind").GetString());

        var jobId = job.GetProperty("id").GetGuid();
        var detail = await Json(userClient, $"/api/v1/me/imports/{jobId}");
        var draft = detail.GetProperty("draft");
        Assert.NotEqual(default, draft.GetProperty("id").GetGuid());
        Assert.Equal("PendingReview", draft.GetProperty("status").GetString());
        Assert.False(string.IsNullOrEmpty(draft.GetProperty("suggestedTitle").GetString()));
        Assert.False(string.IsNullOrEmpty(draft.GetProperty("provenanceJson").GetString()));
    }

    [Fact]
    public async Task Object_import_respects_kind_and_creates_draft_too()
    {
        var user = await CreateUser("ai-obj@example.com");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, user.Email!));

        var response = await client.PostAsJsonAsync("/api/v1/me/imports/object", new { objectKey = "uploads/sunset.png", kind = "Photo" });
        response.EnsureSuccessStatusCode();
        var detail = await Json(client, $"/api/v1/me/imports/{(await Json(response)).GetProperty("id").GetGuid()}");
        Assert.Equal("Photo", detail.GetProperty("job").GetProperty("kind").GetString());
        Assert.Equal("Completed", detail.GetProperty("job").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Translation_is_linked_to_source_draft_and_can_be_replaced()
    {
        var user = await CreateUser("ai-trans@example.com");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, user.Email!));

        var submitted = await client.PostAsJsonAsync("/api/v1/me/imports/text", new { sourceText = new string('b', 80) });
        submitted.EnsureSuccessStatusCode();
        var draftId = (await Json(await client.GetAsync($"/api/v1/me/imports/{(await Json(submitted)).GetProperty("id").GetGuid()}"))).GetProperty("draft").GetProperty("id").GetGuid();

        var first = await client.PostAsJsonAsync("/api/v1/me/translations", new { sourceDraftId = draftId, locale = "es", body = "Translated draft" });
        first.EnsureSuccessStatusCode();
        var second = await client.PostAsJsonAsync("/api/v1/me/translations", new { sourceDraftId = draftId, locale = "es", body = "Translated again" });
        second.EnsureSuccessStatusCode();

        await WithDb(async db =>
        {
            Assert.Single(await db.Translations.AsNoTracking().Where(x => x.SourceDraftId == draftId).ToListAsync());
            var updated = await db.Translations.AsNoTracking().SingleAsync(x => x.SourceDraftId == draftId && x.Locale == "es");
            Assert.Equal("Translated again", updated.Body);
            Assert.Equal(TranslationStatus.Outdated, updated.Status);
        });
    }

    [Fact]
    public async Task Quota_exceeded_rejects_with_403_after_threshold()
    {
        var user = await CreateUser("ai-quota@example.com");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, user.Email!));

        for (var i = 0; i < 5; i++)
        {
            var r = await client.PostAsJsonAsync("/api/v1/me/imports/text", new { sourceText = new string((char)('c' + i), 60) });
            r.EnsureSuccessStatusCode();
        }
        var blocked = await client.PostAsJsonAsync("/api/v1/me/imports/text", new { sourceText = new string('z', 60) });
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
    }

    [Fact]
    public async Task Approve_rejects_drafts_owned_by_others()
    {
        var owner = await CreateUser("ai-owner@example.com");
        var stranger = await CreateUser("ai-stranger@example.com");
        using var ownerClient = factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(ownerClient, owner.Email!));
        using var strangerClient = factory.CreateClient();
        strangerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(strangerClient, stranger.Email!));

        var submitted = await ownerClient.PostAsJsonAsync("/api/v1/me/imports/text", new { sourceText = new string('d', 60) });
        submitted.EnsureSuccessStatusCode();
        var detail = await Json(await ownerClient.GetAsync($"/api/v1/me/imports/{(await Json(submitted)).GetProperty("id").GetGuid()}"));
        var draftId = detail.GetProperty("draft").GetProperty("id").GetGuid();

        var response = await strangerClient.PostAsJsonAsync($"/api/v1/me/drafts/{draftId}/approve", new { guideId = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Replay_process_on_completed_job_is_idempotent()
    {
        var user = await CreateUser("ai-replay@example.com");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, user.Email!));

        var submitted = await client.PostAsJsonAsync("/api/v1/me/imports/text", new { sourceText = new string('e', 60) });
        submitted.EnsureSuccessStatusCode();
        var jobId = (await Json(submitted)).GetProperty("id").GetGuid();

        var first = await Json(await client.PostAsync($"/api/v1/me/imports/{jobId}/process", null));
        var second = await Json(await client.PostAsync($"/api/v1/me/imports/{jobId}/process", null));
        Assert.Equal(first.GetProperty("completedAt").GetDateTimeOffset(), second.GetProperty("completedAt").GetDateTimeOffset());
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

    private async Task<JsonElement> Json(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private Task<AppUser> CreateUser(string email, bool creator = false) => CreateUser(factory.Services, email, creator);
    private static async Task<AppUser> CreateUser(IServiceProvider services, string email, bool creator = false)
    {
        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
        Assert.True((await manager.CreateAsync(user, Password)).Succeeded);
        if (creator) db.CreatorProfiles.Add(new CreatorProfile { UserId = user.Id, Slug = user.Id.ToString("N"), Status = CreatorStatus.Active });
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
