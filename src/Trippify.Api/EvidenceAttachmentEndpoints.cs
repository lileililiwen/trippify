using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class EvidenceAttachmentEndpoints
{
    private static readonly Counter<long> AttachmentCommands = new Meter("Trippify.Verified").CreateCounter<long>("trippify.verified.attachments");

    public static void MapEvidenceAttachmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization();
        group.MapPost("/evidence/attachments", StageAttachment);
        group.MapPut("/evidence/attachments/{attachmentId:guid}/content", UploadAttachmentContent);
        group.MapGet("/evidence/attachments", ListOwnStagedAttachments);
    }

    private static async Task<IResult> StageAttachment(StageEvidenceAttachmentRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        if (string.IsNullOrWhiteSpace(request.FileName) || request.FileName.Length > 200)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["fileName"] = ["File name is required and must not exceed 200 characters."] });
        if (string.IsNullOrWhiteSpace(request.ContentType))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["contentType"] = ["Content type is required."] });
        if (request.SizeBytes is <= 0 or > EvidenceAttachmentRules.MaxBytes)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["sizeBytes"] = [$"File size must be between 1 byte and {EvidenceAttachmentRules.MaxBytes / 1_000_000} MB."] });
        var existingTypes = EvidenceAttachmentRules.AllowedTypes.Keys.ToArray();
        if (!existingTypes.Contains(request.ContentType.Trim(), StringComparer.OrdinalIgnoreCase))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["contentType"] = [$"Content type must be one of: {string.Join(", ", existingTypes)}."] });
        if (!IsHex64(request.Sha256))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["sha256"] = ["SHA-256 checksum must be 64 hexadecimal characters."] });
        if (await db.EvidenceAttachments.AnyAsync(x => x.Sha256 == request.Sha256.ToLowerInvariant() && x.OwnerUserId == user && x.DeletedAt == null))
            return Results.Conflict(new Dictionary<string, string[]> { ["sha256"] = ["This file has already been uploaded for the same evidence submission."] });
        var dailyCount = await db.EvidenceAttachments.CountAsync(x => x.OwnerUserId == user && x.CreatedAt >= clock.UtcNow.AddHours(-24) && x.DeletedAt == null);
        if (dailyCount >= EvidenceAttachmentRules.MaxAttachmentsPerUserPerDay)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["attachment"] = [$"You can upload at most {EvidenceAttachmentRules.MaxAttachmentsPerUserPerDay} attachments per 24 hours."] });
        var usedBytes = await db.EvidenceAttachments.Where(x => x.OwnerUserId == user && x.DeletedAt == null).SumAsync(x => (long?)x.SizeBytes) ?? 0;
        if (usedBytes + request.SizeBytes > EvidenceAttachmentRules.MaxTotalBytesPerUser)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["sizeBytes"] = ["Total attachment storage exceeds the per-user quota."] });

        var now = clock.UtcNow;
        var attachment = new EvidenceAttachment
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user,
            StorageKey = $"evidence/{user:N}/{Guid.NewGuid():N}",
            FileName = request.FileName.Trim(),
            ContentType = request.ContentType.Trim().ToLowerInvariant(),
            SizeBytes = request.SizeBytes,
            Sha256 = request.Sha256.ToLowerInvariant(),
            State = EvidenceAttachmentState.Staged,
            CreatedAt = now,
            ExpiresAt = now.Add(EvidenceAttachmentRules.StagingLifetime),
        };
        db.EvidenceAttachments.Add(attachment);
        await db.SaveChangesAsync();
        AttachmentCommands.Add(1, new KeyValuePair<string, object?>("operation", "attachment-staged"));
        return Results.Created($"/api/v1/evidence/attachments/{attachment.Id}", new StageEvidenceAttachmentResponse(attachment.Id, attachment.StorageKey, attachment.ExpiresAt));
    }

    private static async Task<IResult> UploadAttachmentContent(Guid attachmentId, HttpRequest request, ClaimsPrincipal principal, AppDbContext db, IObjectStorage storage, IClock clock, IBackgroundJobQueue queue)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var attachment = await db.EvidenceAttachments.SingleOrDefaultAsync(x => x.Id == attachmentId);
        if (attachment is null) return Results.NotFound();
        if (attachment.OwnerUserId != user) return Results.Forbid();
        if (attachment.State != EvidenceAttachmentState.Staged) return Results.Conflict(new Dictionary<string, string[]> { ["attachment"] = ["Only staged attachments accept content."] });
        if (attachment.ExpiresAt <= clock.UtcNow) return Results.Conflict(new Dictionary<string, string[]> { ["attachment"] = ["The staging window has expired; upload a new file."] });

        if (!string.IsNullOrEmpty(request.ContentType) && !request.ContentType.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["contentType"] = ["Upload content must be application/octet-stream."] });

        var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer);
        var bytes = buffer.ToArray();
        if (bytes.Length == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["content"] = ["Attachment content is empty."] });
        if (bytes.Length > attachment.SizeBytes + 1024)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["content"] = ["Attachment content exceeds the declared size."] });

        var headLen = Math.Min(16, bytes.Length);
        var tailLen = Math.Min(16, bytes.Length);
        var head = new byte[headLen];
        var tail = new byte[tailLen];
        Array.Copy(bytes, 0, head, 0, headLen);
        Array.Copy(bytes, bytes.Length - tailLen, tail, 0, tailLen);
        var (signatureOk, signatureFailure, normalizedType) = EvidenceAttachmentRules.Validate(attachment.ContentType, head, tail);
        if (!signatureOk)
        {
            attachment.State = EvidenceAttachmentState.Rejected;
            attachment.ScanFailureCode = signatureFailure;
            attachment.DeletedAt = clock.UtcNow;
            await db.SaveChangesAsync();
            try { await storage.DeleteAsync(attachment.StorageKey, default); } catch (Exception error) when (error is IOException or NotSupportedException) { /* best-effort */ }
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["content"] = [$"File content rejected: {signatureFailure}."] });
        }
        if (EvidenceAttachmentRules.IsUnsafe(bytes))
        {
            attachment.State = EvidenceAttachmentState.Rejected;
            attachment.ScanFailureCode = "unsafe-content";
            attachment.DeletedAt = clock.UtcNow;
            await db.SaveChangesAsync();
            try { await storage.DeleteAsync(attachment.StorageKey, default); } catch (Exception error) when (error is IOException or NotSupportedException) { /* best-effort */ }
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["content"] = ["File content rejected as unsafe."] });
        }

        var computed = EvidenceAttachmentRules.ComputeSha256Hex(bytes);
        if (!string.Equals(computed, attachment.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            attachment.State = EvidenceAttachmentState.Rejected;
            attachment.ScanFailureCode = "checksum-mismatch";
            attachment.DeletedAt = clock.UtcNow;
            await db.SaveChangesAsync();
            try { await storage.DeleteAsync(attachment.StorageKey, default); } catch (Exception error) when (error is IOException or NotSupportedException) { /* best-effort */ }
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["content"] = ["File content checksum does not match the staged declaration."] });
        }
        attachment.ContentType = normalizedType;
        try
        {
            await using var stream = new MemoryStream(bytes);
            await storage.PutAsync(attachment.StorageKey, stream, default);
        }
        catch (Exception error) when (error is IOException or NotSupportedException)
        {
            attachment.State = EvidenceAttachmentState.Rejected;
            attachment.ScanFailureCode = "storage-unavailable";
            attachment.DeletedAt = clock.UtcNow;
            await db.SaveChangesAsync();
            return Results.Problem("Attachment storage is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        attachment.State = EvidenceAttachmentState.Scanning;
        await db.SaveChangesAsync();
        await queue.EnqueueAsync(
            BackgroundJobTypes.EvidenceAttachmentScan,
            System.Text.Json.JsonSerializer.Serialize(new EvidenceAttachmentScanPayload(attachment.Id)),
            default,
            idempotencyKey: $"evidence-attachment-scan:{attachment.Id}",
            availableAt: clock.UtcNow);
        AttachmentCommands.Add(1, new KeyValuePair<string, object?>("operation", "attachment-content-uploaded"));
        return Results.Ok(new EvidenceAttachmentSummary(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.State.ToString(), attachment.CreatedAt, attachment.LinkedAt, attachment.ScanFailureCode));
    }

    private static async Task<IResult> ListOwnStagedAttachments(ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var rows = await db.EvidenceAttachments.AsNoTracking()
            .Where(x => x.OwnerUserId == user && x.DeletedAt == null && x.EvidenceId == null)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new EvidenceAttachmentSummary(x.Id, x.FileName, x.ContentType, x.SizeBytes, x.State.ToString(), x.CreatedAt, x.LinkedAt, x.ScanFailureCode))
            .ToListAsync();
        return Results.Ok(rows);
    }

    private static bool IsHex64(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 64) return false;
        foreach (var c in value)
        {
            if (!Uri.IsHexDigit(c)) return false;
        }
        return true;
    }
}