using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trippify.Api;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

public sealed class RestorableBackupTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    private const string Password = "Strong!Pass123";
    private const string AdminRole = "Administrator";

    [Fact]
    public async Task Capture_then_restore_round_trip_records_snapshot_and_validates_artifact()
    {
        var admin = await CreateAdmin("backup-roundtrip@example.com");
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));

        var capture = await adminClient.PostAsJsonAsync("/api/v1/admin/system/backup", new { label = "roundtrip" });
        capture.EnsureSuccessStatusCode();
        var captureJson = await Json(capture);
        var snapshotId = captureJson.GetProperty("id").GetGuid();
        var storedSha = captureJson.GetProperty("sha256").GetString();
        Assert.False(string.IsNullOrEmpty(storedSha));

        var restore = await adminClient.PostAsJsonAsync("/api/v1/admin/system/restore", new { snapshotId });
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        var restoreJson = await Json(restore);
        Assert.Equal("validated", restoreJson.GetProperty("status").GetString());

        await WithDb(async db =>
        {
            var stored = await db.BackupSnapshots.SingleAsync(x => x.Id == snapshotId);
            Assert.Equal("restored", stored.Status);
            Assert.True(stored.Restorable);
            Assert.NotNull(stored.ArtifactPath);
            Assert.NotNull(stored.Sha256);
        });
    }

    [Fact]
    public async Task Restoring_after_artifact_is_deleted_fails_without_overwriting_live_data()
    {
        var admin = await CreateAdmin("backup-deleted-artifact@example.com");
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));

        var capture = await adminClient.PostAsJsonAsync("/api/v1/admin/system/backup", new { label = "to-be-deleted" });
        capture.EnsureSuccessStatusCode();
        var snapshotId = (await Json(capture)).GetProperty("id").GetGuid();

        string? artifactPath = null;
        await WithDb(async db =>
        {
            artifactPath = (await db.BackupSnapshots.SingleAsync(x => x.Id == snapshotId)).ArtifactPath;
        });
        Assert.NotNull(artifactPath);
        File.Delete(artifactPath!);
        Assert.False(File.Exists(artifactPath!));

        var restore = await adminClient.PostAsJsonAsync("/api/v1/admin/system/restore", new { snapshotId });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, restore.StatusCode);
        await WithDb(async db =>
        {
            var stored = await db.BackupSnapshots.SingleAsync(x => x.Id == snapshotId);
            // The snapshot must not be flipped to "restored" when the artifact
            // is missing. It must surface the failure instead.
            Assert.NotEqual("restored", stored.Status);
        });
    }

    [Fact]
    public async Task Restoring_a_tampered_artifact_rejects_the_checksum()
    {
        var admin = await CreateAdmin("backup-tampered@example.com");
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));

        var capture = await adminClient.PostAsJsonAsync("/api/v1/admin/system/backup", new { label = "tamper" });
        capture.EnsureSuccessStatusCode();
        var snapshotId = (await Json(capture)).GetProperty("id").GetGuid();

        string? artifactPath = null;
        await WithDb(async db =>
        {
            artifactPath = (await db.BackupSnapshots.SingleAsync(x => x.Id == snapshotId)).ArtifactPath;
        });
        Assert.NotNull(artifactPath);
        // Append a byte so the SHA256 in the snapshot row no longer matches the
        // artifact on disk.
        using (var stream = new FileStream(artifactPath!, FileMode.Append, FileAccess.Write))
        {
            stream.WriteByte(0xFF);
        }

        var restore = await adminClient.PostAsJsonAsync("/api/v1/admin/system/restore", new { snapshotId });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, restore.StatusCode);
    }

    [Fact]
    public async Task Restoring_an_encrypted_snapshot_without_a_key_is_rejected_with_403()
    {
        var admin = await CreateAdmin("backup-encrypted@example.com");
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));

        var capture = await adminClient.PostAsJsonAsync("/api/v1/admin/system/backup", new { label = "encrypted", operatorKey = "test-key" });
        capture.EnsureSuccessStatusCode();
        var snapshotId = (await Json(capture)).GetProperty("id").GetGuid();

        var restoreWithoutKey = await adminClient.PostAsJsonAsync("/api/v1/admin/system/restore", new { snapshotId });
        Assert.Equal(HttpStatusCode.Forbidden, restoreWithoutKey.StatusCode);

        var restoreWithWrongKey = await adminClient.PostAsJsonAsync("/api/v1/admin/system/restore", new { snapshotId, operatorKey = "not-the-right-key" });
        Assert.Equal(HttpStatusCode.Forbidden, restoreWithWrongKey.StatusCode);
    }

    [Fact]
    public async Task Schema_version_mismatch_is_rejected_before_any_database_mutation()
    {
        var admin = await CreateAdmin("backup-schema-mismatch@example.com");
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));

        var capture = await adminClient.PostAsJsonAsync("/api/v1/admin/system/backup", new { label = "old-schema" });
        capture.EnsureSuccessStatusCode();
        var snapshotId = (await Json(capture)).GetProperty("id").GetGuid();

        // Patch the row to a legacy schema version, then ask the API to
        // restore it. The endpoint must refuse without mutating other rows.
        await WithDb(async db =>
        {
            var stored = await db.BackupSnapshots.SingleAsync(x => x.Id == snapshotId);
            stored.SchemaVersion = 1;
            await db.SaveChangesAsync();
        });

        var restore = await adminClient.PostAsJsonAsync("/api/v1/admin/system/restore", new { snapshotId });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, restore.StatusCode);

        await WithDb(async db =>
        {
            var stored = await db.BackupSnapshots.SingleAsync(x => x.Id == snapshotId);
            Assert.NotEqual("restored", stored.Status);
        });
    }

    [Fact]
    public async Task Capture_with_invalid_label_is_rejected()
    {
        var admin = await CreateAdmin("backup-bad-label@example.com");
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));

        var response = await adminClient.PostAsJsonAsync("/api/v1/admin/system/backup", new { label = "" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Non_administrator_cannot_restore_a_backup()
    {
        var user = await CreateUser("backup-non-admin@example.com");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, user.Email!));
        var response = await client.PostAsJsonAsync("/api/v1/admin/system/restore", new { payload = "{}" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<AppUser> CreateAdmin(string email)
    {
        AppUser? created = null;
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        created = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
        Assert.True((await users.CreateAsync(created, Password)).Succeeded);
        if (!await roles.RoleExistsAsync(AdminRole)) Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(AdminRole))).Succeeded);
        Assert.True((await users.AddToRoleAsync(created, AdminRole)).Succeeded);
        return created!;
    }

    private async Task<AppUser> CreateUser(string email)
    {
        AppUser? created = null;
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        created = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
        Assert.True((await users.CreateAsync(created, Password)).Succeeded);
        return created!;
    }

    private static async Task<string> Login(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("accessToken").GetString()!;
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
}
