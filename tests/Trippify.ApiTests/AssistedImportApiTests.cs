using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trippify.Application;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

public sealed class AssistedImportFactory : TrippifyFactory
{
    public FakeAiAssistant Fake { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAiAssistant>();
            services.AddSingleton<IAiAssistant>(Fake);
        });
    }
}

public sealed class FakeAiAssistant : IAiAssistant
{
    public Func<AiAssistRequest, AiAssistResult>? Responder { get; set; }
    public string ProviderName { get; set; } = "test";
    public string Model { get; set; } = "test-model";
    public string Schema { get; set; } = "v1";
    public int AttemptCount { get; set; } = 1;
    public bool DisabledMode { get; set; }
    AiAssistResult IAiAssistant.Disabled() => new(AiAssistStatus.Disabled, null, null, null, ProviderName, Model, Schema, 0, false, "disabled");
    public Task<AiAssistResult> AssistAsync(AiAssistRequest request, CancellationToken cancellation)
    {
        if (Responder is not null) return Task.FromResult(Responder(request));
        if (DisabledMode) return Task.FromResult(((IAiAssistant)this).Disabled());
        if (request.Kind == AiAssistKind.Translate)
            return Task.FromResult(new AiAssistResult(AiAssistStatus.Completed, null, null, $"[test:{request.TargetLocale}] {request.SourceText}", ProviderName, Model, Schema, AttemptCount, false, string.Empty));
        var nodes = request.SourceText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(8).ToList();
        if (nodes.Count == 0) nodes = new List<string> { request.SourceText[..Math.Min(200, request.SourceText.Length)] };
        return Task.FromResult(new AiAssistResult(AiAssistStatus.Completed, $"Imported — {nodes[0]}", nodes, null, ProviderName, Model, Schema, AttemptCount, false, string.Empty));
    }
}

public sealed class AssistedImportApiTests : IClassFixture<AssistedImportFactory>
{
    private const string Password = "Strong!Pass123";
    private readonly AssistedImportFactory factory;

    public AssistedImportApiTests(AssistedImportFactory factory)
    {
        this.factory = factory;
        ResetFake();
    }

    private void ResetFake()
    {
        factory.Fake.Responder = null;
        factory.Fake.DisabledMode = false;
        factory.Fake.ProviderName = "test";
        factory.Fake.Model = "test-model";
        factory.Fake.Schema = "v1";
        factory.Fake.AttemptCount = 1;
    }

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
            Assert.Equal("[test:es] Translated again", updated.Body);
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

    [Fact]
    public async Task Successful_draft_persists_provider_model_and_schema_provenance()
    {
        var user = await CreateUser("ai-prov@example.com");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, user.Email!));
        factory.Fake.ProviderName = "openai";
        factory.Fake.Model = "gpt-4o";
        factory.Fake.Schema = "v1";

        var submitted = await client.PostAsJsonAsync("/api/v1/me/imports/text", new { sourceText = new string('p', 60) });
        submitted.EnsureSuccessStatusCode();
        var job = await Json(submitted);
        Assert.Equal("openai", job.GetProperty("providerName").GetString());
        Assert.Equal("gpt-4o", job.GetProperty("modelName").GetString());
        Assert.Equal("v1", job.GetProperty("schemaVersion").GetString());

        var detail = await Json(await client.GetAsync($"/api/v1/me/imports/{job.GetProperty("id").GetGuid()}"));
        var draft = detail.GetProperty("draft");
        Assert.Equal("openai", draft.GetProperty("providerName").GetString());
        Assert.Equal("v1", draft.GetProperty("schemaVersion").GetString());
        Assert.Equal("PendingReview", draft.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Provider_invalid_output_marks_job_failed_and_persists_no_draft()
    {
        var user = await CreateUser("ai-invalid@example.com");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, user.Email!));
        ResetFake();
        factory.Fake.Responder = _ => new AiAssistResult(AiAssistStatus.InvalidOutput, null, null, null, "openai", "gpt-4o", "v1", 1, false, "schema-mismatch");

        var submitted = await client.PostAsJsonAsync("/api/v1/me/imports/text", new { sourceText = new string('x', 60) });
        submitted.EnsureSuccessStatusCode();
        var job = await Json(submitted);
        Assert.Equal("Failed", job.GetProperty("status").GetString());
        Assert.Equal("schema-mismatch", job.GetProperty("failureCode").GetString());

        var detail = await Json(await client.GetAsync($"/api/v1/me/imports/{job.GetProperty("id").GetGuid()}"));
        Assert.False(detail.TryGetProperty("draft", out var draftProp) && draftProp.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task Provider_unavailable_surfaces_retryable_failure_without_publishing()
    {
        var user = await CreateUser("ai-unav@example.com");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, user.Email!));
        ResetFake();
        factory.Fake.Responder = _ => new AiAssistResult(AiAssistStatus.ProviderUnavailable, null, null, null, "openai", "gpt-4o", "v1", 3, true, "provider-unavailable");

        var submitted = await client.PostAsJsonAsync("/api/v1/me/imports/text", new { sourceText = new string('u', 60) });
        submitted.EnsureSuccessStatusCode();
        var job = await Json(submitted);
        Assert.Equal("Failed", job.GetProperty("status").GetString());
        Assert.Equal("provider-unavailable", job.GetProperty("failureCode").GetString());
        Assert.Contains("unavailable", job.GetProperty("failureReason").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Disabled_provider_does_not_fabricate_a_draft()
    {
        var user = await CreateUser("ai-disabled@example.com");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, user.Email!));
        ResetFake();
        factory.Fake.DisabledMode = true;

        var submitted = await client.PostAsJsonAsync("/api/v1/me/imports/text", new { sourceText = new string('o', 60) });
        submitted.EnsureSuccessStatusCode();
        var job = await Json(submitted);
        Assert.Equal("Failed", job.GetProperty("status").GetString());
        Assert.Equal("disabled", job.GetProperty("failureCode").GetString());

        await WithDb(async db =>
        {
            Assert.Empty(await db.ImportDrafts.AsNoTracking().Where(x => x.UserId == user.Id).ToListAsync());
        });
    }

    [Fact]
    public async Task Translation_failure_returns_503_and_does_not_persist()
    {
        var user = await CreateUser("ai-transfail@example.com");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, user.Email!));
        ResetFake();
        var submitted = await client.PostAsJsonAsync("/api/v1/me/imports/text", new { sourceText = new string('t', 60) });
        submitted.EnsureSuccessStatusCode();
        var draftId = (await Json(await client.GetAsync($"/api/v1/me/imports/{(await Json(submitted)).GetProperty("id").GetGuid()}"))).GetProperty("draft").GetProperty("id").GetGuid();

        factory.Fake.Responder = _ => new AiAssistResult(AiAssistStatus.ProviderUnavailable, null, null, null, "openai", "gpt-4o", "v1", 1, true, "provider-unavailable");
        var response = await client.PostAsJsonAsync("/api/v1/me/translations", new { sourceDraftId = draftId, locale = "fr", body = "hello world this is a translation request" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await WithDb(async db =>
        {
            Assert.Empty(await db.Translations.AsNoTracking().Where(x => x.UserId == user.Id).ToListAsync());
        });
    }

    [Fact]
    public async Task HttpAiAssistant_contract_marks_invalid_output_and_never_echoes()
    {
        var options = new AiProviderOptions { Provider = "http", Endpoint = "https://provider.example.invalid", ApiKey = "test", Enabled = true, MaxInputChars = 200, MaxOutputChars = 200, MaxAttempts = 1, TimeoutMilliseconds = 500 };
        using var handler = new ScriptedHandler("not-json");
        using var http = new HttpClient(handler) { BaseAddress = new Uri(options.Endpoint!), Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds) };
        var assistant = new HttpAiAssistant(options, http);
        var request = new AiAssistRequest(AiAssistKind.Draft, new string('a', 250), null, null, "op", 200, 200);
        var result = await assistant.AssistAsync(request, default);
        Assert.Equal(AiAssistStatus.InvalidOutput, result.Status);
        Assert.Equal("input-too-long", result.FailureCode);
        Assert.NotEqual(request.SourceText, result.Title);
        Assert.Null(result.Body);
        Assert.Equal("http", result.ProviderName);
        Assert.False(result.Retryable);

        using var echoHandler = new ScriptedHandler("{ \"schemaVersion\": \"v1\", \"title\": \"" + new string('z', 50) + "\", \"nodes\": [\"a\",\"b\"] }");
        using var echoHttp = new HttpClient(echoHandler) { BaseAddress = new Uri("https://provider2.example.invalid") };
        var echoAssistant = new HttpAiAssistant(new AiProviderOptions { Provider = "http", Endpoint = "https://provider2.example.invalid", ApiKey = "k", Enabled = true, MaxInputChars = 20000, MaxOutputChars = 8000, TimeoutMilliseconds = 500, MaxAttempts = 1 }, echoHttp);
        var echoResult = await echoAssistant.AssistAsync(new AiAssistRequest(AiAssistKind.Draft, "abc", null, null, "op", 20000, 8000), default);
        Assert.Equal(AiAssistStatus.Completed, echoResult.Status);
        Assert.Equal(2, echoResult.Nodes!.Count);
    }

    [Fact]
    public async Task HttpAiAssistant_retries_on_transient_status_and_records_attempts()
    {
        var options = new AiProviderOptions { Provider = "http", Endpoint = "https://provider.example.invalid", ApiKey = "k", Enabled = true, MaxInputChars = 20000, MaxOutputChars = 8000, TimeoutMilliseconds = 5000, MaxAttempts = 3 };
        using var handler = new FlakyHandler(2, "{ \"schemaVersion\": \"v1\", \"title\": \"ok\", \"nodes\": [\"alpha\",\"beta\"] }");
        using var http = new HttpClient(handler) { BaseAddress = new Uri(options.Endpoint!), Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds) };
        var assistant = new HttpAiAssistant(options, http);
        var result = await assistant.AssistAsync(new AiAssistRequest(AiAssistKind.Draft, "go", null, null, "op", 20000, 8000), default);
        Assert.Equal(AiAssistStatus.Completed, result.Status);
        Assert.True(result.AttemptCount >= 2);
        Assert.Equal(3, handler.Served);
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

internal sealed class ScriptedHandler : HttpMessageHandler
{
    private readonly string _body;
    private readonly HttpStatusCode _status;
    public ScriptedHandler(string body, HttpStatusCode status = HttpStatusCode.OK) { _body = body; _status = status; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body) });
    }
}

internal sealed class FlakyHandler : HttpMessageHandler
{
    private readonly Queue<HttpStatusCode> _statuses;
    private readonly string _okBody;
    public int Served { get; private set; }
    public FlakyHandler(int failures, string okBody)
    {
        _statuses = new Queue<HttpStatusCode>();
        for (var i = 0; i < failures; i++) _statuses.Enqueue(HttpStatusCode.InternalServerError);
        _okBody = okBody;
    }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Served++;
        if (_statuses.Count > 0)
        {
            var status = _statuses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(status));
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_okBody) });
    }
}
