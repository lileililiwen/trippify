using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Trippify.Api;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

public sealed class VerifiedEvidenceAttachmentApiTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    private const string Password = "Strong!Pass123";
    private const string AdminRole = "Administrator";

    [Fact]
    public async Task Anonymous_cannot_stage_or_upload_attachments()
    {
        using var anonymous = factory.CreateClient();
        var stage = await anonymous.PostAsJsonAsync("/api/v1/evidence/attachments", new
        {
            fileName = "evidence.jpg",
            contentType = "image/jpeg",
            sizeBytes = 2048,
            sha256 = new string('0', 64),
        });
        Assert.Equal(HttpStatusCode.Unauthorized, stage.StatusCode);
        var list = await anonymous.GetAsync("/api/v1/evidence/attachments");
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
    }

    [Fact]
    public async Task Eligible_traveler_uploads_signature_matched_attachment_and_links_to_evidence()
    {
        var creator = await CreateUser("vt-att-creator@example.com", creator: true);
        var buyer = await CreateUser("vt-att-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));

        var bytes = MakeJpegBytes(2048);
        var sha = Hex(SHA256.HashData(bytes));
        var attachmentId = await Stage(buyerClient, "evidence.jpg", "image/jpeg", bytes.LongLength, sha);
        var upload = await UploadContent(buyerClient, attachmentId, bytes);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var scanPayload = (await ReadJson(upload)).GetProperty("state").GetString();
        Assert.Equal("Scanning", scanPayload);

        await WaitUntilReady(buyerClient, attachmentId);

        var submit = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence", new
        {
            kind = "PhotoNote",
            body = new string('h', 60),
            attachmentIds = new[] { attachmentId },
        });
        submit.EnsureSuccessStatusCode();
        var evidenceId = (await ReadJson(submit)).GetProperty("id").GetGuid();

        var list = await buyerClient.GetAsync($"/api/v1/evidence/{evidenceId}/attachments");
        list.EnsureSuccessStatusCode();
        var rows = (await ReadJson(list)).EnumerateArray().ToArray();
        Assert.Single(rows);
        Assert.Equal(attachmentId.ToString(), rows[0].GetProperty("id").GetString());

        var download = await buyerClient.GetAsync($"/api/v1/evidence/attachments/{attachmentId}/download");
        download.EnsureSuccessStatusCode();
        var downloadJson = await ReadJson(download);
        Assert.True(downloadJson.TryGetProperty("url", out _));
        Assert.True(downloadJson.TryGetProperty("expiresAt", out _));
    }

    [Fact]
    public async Task Disguised_or_oversized_or_unsafe_files_are_rejected()
    {
        var creator = await CreateUser("vt-attv-creator@example.com", creator: true);
        var buyer = await CreateUser("vt-attv-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));

        // Disguised: claims jpeg but the bytes start with PNG magic.
        var disguisedBytes = new byte[2048];
        Array.Copy(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, disguisedBytes, 8);
        for (var i = 8; i < disguisedBytes.Length; i++) disguisedBytes[i] = 0xAA;
        var disguisedSha = Hex(SHA256.HashData(disguisedBytes));
        var disguisedId = await Stage(buyerClient, "fake.jpg", "image/jpeg", disguisedBytes.LongLength, disguisedSha);
        var disguisedUpload = await UploadContent(buyerClient, disguisedId, disguisedBytes);
        Assert.Equal(HttpStatusCode.BadRequest, disguisedUpload.StatusCode);
        await WithDb(async db => Assert.Equal(EvidenceAttachmentState.Rejected, (await db.EvidenceAttachments.SingleAsync(x => x.Id == disguisedId)).State));

        // Unsafe: a script-injected payload using the PDF signature to bypass head check.
        var unsafePrefix = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        var unsafePayload = Encoding.ASCII.GetBytes("<script>alert(1)</script>");
        var unsafeBytes = new byte[unsafePrefix.Length + unsafePayload.Length];
        Array.Copy(unsafePrefix, 0, unsafeBytes, 0, unsafePrefix.Length);
        Array.Copy(unsafePayload, 0, unsafeBytes, unsafePrefix.Length, unsafePayload.Length);
        var unsafeSha = Hex(SHA256.HashData(unsafeBytes));
        var unsafeId = await Stage(buyerClient, "unsafe.pdf", "application/pdf", unsafeBytes.LongLength, unsafeSha);
        var unsafeUpload = await UploadContent(buyerClient, unsafeId, unsafeBytes);
        Assert.Equal(HttpStatusCode.BadRequest, unsafeUpload.StatusCode);
        await WithDb(async db => Assert.Equal(EvidenceAttachmentState.Rejected, (await db.EvidenceAttachments.SingleAsync(x => x.Id == unsafeId)).State));

        // Oversized stage (> 5 MB declared).
        var oversized = await buyerClient.PostAsJsonAsync("/api/v1/evidence/attachments", new
        {
            fileName = "huge.jpg",
            contentType = "image/jpeg",
            sizeBytes = 6_000_000,
            sha256 = new string('a', 64),
        });
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);

        // Mismatched checksum after upload.
        var mismatchBytes = MakeJpegBytes(1024, 3);
        var mismatchSha = Hex(SHA256.HashData(mismatchBytes));
        var mismatchId = await Stage(buyerClient, "mismatch.jpg", "image/jpeg", mismatchBytes.LongLength, mismatchSha);
        var wrongBytes = MakeJpegBytes(1024, 7);
        var mismatchUpload = await UploadContent(buyerClient, mismatchId, wrongBytes);
        Assert.Equal(HttpStatusCode.BadRequest, mismatchUpload.StatusCode);

        // Submitting rejected attachments is rejected.
        var (rejectedReadyId, _) = await StageAndReady(buyerClient, "ok.jpg", "image/jpeg", MakeJpegBytes(1024));
        var submit = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence", new
        {
            kind = "PhotoNote",
            body = new string('g', 60),
            attachmentIds = new[] { rejectedReadyId, disguisedId },
        });
        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
    }

    [Fact]
    public async Task Unrelated_buyer_and_creator_cannot_access_attachments_or_downloads()
    {
        var creator = await CreateUser("vt-acc-creator@example.com", creator: true);
        var buyer = await CreateUser("vt-acc-buyer@example.com");
        var unrelated = await CreateUser("vt-acc-stranger@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var body = new string('b', 60);
        var submitted = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence", new { kind = "TripJournal", body });
        if (!submitted.IsSuccessStatusCode)
        {
            var resp = await submitted.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"submit failed {submitted.StatusCode}: {resp}");
        }
        var evidenceId = (await ReadJson(submitted)).GetProperty("id").GetGuid();
        var (attachmentId, _) = await StageAndReady(buyerClient, "evidence.jpg", "image/jpeg", MakeJpegBytes(1024));
        var attach = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence/{evidenceId}/attachments", new { attachmentIds = new[] { attachmentId } });
        if (!attach.IsSuccessStatusCode)
        {
            var resp = await attach.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"attach failed {attach.StatusCode}: {resp}");
        }

        using var unrelatedClient = factory.CreateClient();
        unrelatedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(unrelatedClient, unrelated.Email!));
        Assert.Equal(HttpStatusCode.NotFound, (await unrelatedClient.GetAsync($"/api/v1/evidence/{evidenceId}/attachments")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await unrelatedClient.GetAsync($"/api/v1/evidence/attachments/{attachmentId}/download")).StatusCode);

        using var creatorReviewer = factory.CreateClient();
        creatorReviewer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorReviewer, creator.Email!));
        Assert.Equal(HttpStatusCode.Forbidden, (await creatorReviewer.GetAsync("/api/v1/admin/evidence/{evidenceId}/attachments".Replace("{evidenceId}", evidenceId.ToString()))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await creatorReviewer.GetAsync("/api/v1/admin/evidence/attachments/{attachmentId}/download".Replace("{attachmentId}", attachmentId.ToString()))).StatusCode);
    }

    [Fact]
    public async Task Administrator_can_review_and_download_attachments_but_owner_cannot()
    {
        var creator = await CreateUser("vt-ada-creator@example.com", creator: true);
        var buyer = await CreateUser("vt-ada-buyer@example.com");
        var admin = await CreateUser("vt-ada-admin@example.com", admin: true);
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var body = new string('a', 60);
        var submitted = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence", new { kind = "Receipt", body });
        submitted.EnsureSuccessStatusCode();
        var evidenceId = (await ReadJson(submitted)).GetProperty("id").GetGuid();
        var (attachmentId, _) = await StageAndReady(buyerClient, "evidence.jpg", "image/jpeg", MakeJpegBytes(1024));
        (await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence/{evidenceId}/attachments", new { attachmentIds = new[] { attachmentId } })).EnsureSuccessStatusCode();

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));
        var list = await adminClient.GetAsync($"/api/v1/admin/evidence/{evidenceId}/attachments");
        list.EnsureSuccessStatusCode();
        var listJson = await ReadJson(list);
        Assert.Single(listJson.EnumerateArray());
        var download = await adminClient.GetAsync($"/api/v1/admin/evidence/attachments/{attachmentId}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);

        using var anonClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync($"/api/v1/admin/evidence/{evidenceId}/attachments")).StatusCode);

        // Owner (creator) cannot self-review.
        Assert.Equal(HttpStatusCode.Forbidden, (await creatorClient.PostAsJsonAsync($"/api/v1/admin/evidence/{evidenceId}/review", new { decision = "Approved", reason = "self" })).StatusCode);
    }

    [Fact]
    public async Task Duplicate_files_and_expired_staging_are_rejected()
    {
        var creator = await CreateUser("vt-dup-creator@example.com", creator: true);
        var buyer = await CreateUser("vt-dup-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));

        var bytes = MakeJpegBytes(2048);
        var sha = Hex(SHA256.HashData(bytes));
        await Stage(buyerClient, "first.jpg", "image/jpeg", bytes.LongLength, sha);
        await WithDb(async db =>
        {
            var existing = await db.EvidenceAttachments.SingleAsync(x => x.Sha256 == sha && x.OwnerUserId == buyer.Id);
            existing.State = EvidenceAttachmentState.Ready;
            await db.SaveChangesAsync();
        });
        var dup = await buyerClient.PostAsJsonAsync("/api/v1/evidence/attachments", new { fileName = "second.jpg", contentType = "image/jpeg", sizeBytes = bytes.LongLength, sha256 = sha });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        // Expired staging: force an attachment to be past its expiry and upload content.
        var expiredId = await Stage(buyerClient, "expired.jpg", "image/jpeg", bytes.LongLength, Hex(SHA256.HashData(MakeJpegBytes(1024))));
        await WithDb(async db =>
        {
            var row = await db.EvidenceAttachments.SingleAsync(x => x.Id == expiredId);
            row.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });
        var expiredUpload = await UploadContent(buyerClient, expiredId, bytes);
        Assert.Equal(HttpStatusCode.Conflict, expiredUpload.StatusCode);
    }

    [Fact]
    public async Task Evidence_retention_removes_attachment_bytes_and_does_not_corrupt_badge_counts()
    {
        var creator = await CreateUser("vt-ret-creator@example.com", creator: true);
        var buyer = await CreateUser("vt-ret-buyer@example.com");
        var admin = await CreateUser("vt-ret-admin@example.com", admin: true);
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var body = new string('z', 60);
        var submitted = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence", new { kind = "BookingConfirmation", body });
        submitted.EnsureSuccessStatusCode();
        var evidenceId = (await ReadJson(submitted)).GetProperty("id").GetGuid();
        var (attachmentId, _) = await StageAndReady(buyerClient, "receipt.jpg", "image/jpeg", MakeJpegBytes(2048));
        (await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence/{evidenceId}/attachments", new { attachmentIds = new[] { attachmentId } })).EnsureSuccessStatusCode();

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));
        (await adminClient.PostAsJsonAsync($"/api/v1/admin/evidence/{evidenceId}/review", new { decision = "Approved", reason = "ok" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.DeleteAsync($"/api/v1/admin/evidence/{evidenceId}")).StatusCode);

        await WithDb(async db =>
        {
            var attachment = await db.EvidenceAttachments.AsNoTracking().SingleAsync(x => x.Id == attachmentId);
            Assert.NotNull(attachment.DeletedAt);
            Assert.Equal(EvidenceAttachmentState.Deleted, attachment.State);
            var badge = await db.VerifiedGuideBadges.AsNoTracking().SingleAsync(x => x.GuideId == guideId);
            Assert.Equal(0, badge.ApprovedEvidenceCount);
            Assert.NotNull(badge.RevokedAt);
        });

        var badgeAfter = await ReadJson(await adminClient.GetAsync($"/api/v1/guides/{guideId}/evidence/badge"));
        Assert.False(badgeAfter.GetProperty("verified").GetBoolean());
    }

    [Fact]
    public async Task Public_badge_response_does_not_leak_attachment_identifiers()
    {
        var creator = await CreateUser("vt-leak-creator@example.com", creator: true);
        var buyer = await CreateUser("vt-leak-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var body = new string('l', 60);
        var submitted = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence", new { kind = "TripJournal", body });
        submitted.EnsureSuccessStatusCode();
        var evidenceId = (await ReadJson(submitted)).GetProperty("id").GetGuid();
        var (attachmentId, _) = await StageAndReady(buyerClient, "evidence.jpg", "image/jpeg", MakeJpegBytes(1024));
        (await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence/{evidenceId}/attachments", new { attachmentIds = new[] { attachmentId } })).EnsureSuccessStatusCode();

        using var anon = factory.CreateClient();
        var payload = await anon.GetStringAsync($"/api/v1/guides/{guideId}/evidence/badge");
        Assert.DoesNotContain("attachment", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(attachmentId.ToString(), payload, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] MakeJpegBytes(int length, int salt = 0)
    {
        var bytes = new byte[length];
        Array.Copy(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, bytes, 4);
        for (var i = 4; i < bytes.Length; i++) bytes[i] = (byte)((i + salt) % 251);
        return bytes;
    }

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    private async Task<Guid> Stage(HttpClient client, string fileName, string contentType, long sizeBytes, string sha256)
    {
        var stage = await client.PostAsJsonAsync("/api/v1/evidence/attachments", new { fileName, contentType, sizeBytes, sha256 });
        stage.EnsureSuccessStatusCode();
        return (await ReadJson(stage)).GetProperty("attachmentId").GetGuid();
    }

    private static async Task<HttpResponseMessage> UploadContent(HttpClient client, Guid attachmentId, byte[] bytes)
    {
        var token = client.DefaultRequestHeaders.Authorization?.Parameter;
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/evidence/attachments/{attachmentId}/content");
        if (!string.IsNullOrEmpty(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return await client.SendAsync(request);
    }

private async Task WaitUntilReady(HttpClient client, Guid attachmentId)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<BackgroundJobProcessor>().ProcessBatchAsync("test-worker", 20, default);
            var list = await client.GetAsync("/api/v1/evidence/attachments");
            list.EnsureSuccessStatusCode();
            foreach (var entry in (await ReadJson(list)).EnumerateArray())
            {
                if (entry.GetProperty("id").GetGuid() == attachmentId)
                {
                    var state = entry.GetProperty("state").GetString();
                    if (state == "Ready") return;
                    if (state == "Rejected")
                    {
                        throw new InvalidOperationException($"attachment rejected: {entry.GetProperty("scanFailureCode").GetString()}");
                    }
                }
            }
            await Task.Delay(50);
        }
    }

    private async Task<(Guid id, string state)> StageAndReady(HttpClient client, string fileName, string contentType, byte[] bytes)
    {
        var sha = Hex(SHA256.HashData(bytes));
        var id = await Stage(client, fileName, contentType, bytes.LongLength, sha);
        var upload = await UploadContent(client, id, bytes);
        upload.EnsureSuccessStatusCode();
        await WaitUntilReady(client, id);
        return (id, "Ready");
    }

    private async Task<Guid> PublishFreeGuide(HttpClient creatorClient)
    {
        var created = await creatorClient.PostAsJsonAsync("/api/v1/guides", new
        {
            title = "Verified trip evidence guide",
            subtitle = "Sub",
            summary = "Summary",
            coverUrl = (string?)null,
            countryCode = "JP",
            cities = new[] { "Osaka" },
            tags = Array.Empty<string>(),
            tripDays = 1,
        });
        created.EnsureSuccessStatusCode();
        var json = await ReadJson(created);
        var id = json.GetProperty("id").GetGuid();
        var structure = new
        {
            concurrencyToken = json.GetProperty("concurrencyToken").GetGuid(),
            days = new[] { new { title = "Day", notes = "", nodes = new[] { new { type = "Attraction", name = "Spot", address = "", latitude = 34.0, longitude = 135.0, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" } } } },
            sections = Array.Empty<object>(),
        };
        (await creatorClient.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        var token = (await ReadJson(await creatorClient.GetAsync($"/api/v1/guides/{id}"))).GetProperty("concurrencyToken").GetGuid();
        (await creatorClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = token, pricing = (object?)null })).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<string> Login(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        return (await ReadJson(response)).GetProperty("accessToken").GetString()!;
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private async Task<AppUser> CreateUser(string email, bool creator = false, bool admin = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
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

    private async Task WithDb(Func<AppDbContext, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}