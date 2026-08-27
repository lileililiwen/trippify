using System.Security.Cryptography;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class EvidenceAttachmentRules
{
    public static readonly TimeSpan StagingLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(10);
    public const int MaxAttachmentsPerEvidence = 5;
    public const int MaxAttachmentsPerUserPerDay = 20;
    public const long MaxBytes = 5_000_000;
    public const long MaxTotalBytesPerUser = 25_000_000;
    public const int DisallowedByteScanWindow = 4096;

    public static readonly IReadOnlyDictionary<string, AttachmentTypeRule> AllowedTypes = new Dictionary<string, AttachmentTypeRule>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = new AttachmentTypeRule("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF }, ".jpg"),
        ["image/png"] = new AttachmentTypeRule("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, ".png"),
        ["image/webp"] = new AttachmentTypeRule("image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46 }, ".webp", new byte[] { 0x57, 0x45, 0x42, 0x50 }),
        ["application/pdf"] = new AttachmentTypeRule("application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 }, ".pdf"),
    };

    public static (bool Ok, string? FailureCode, string NormalizedContentType) Validate(string? contentType, ReadOnlySpan<byte> head, ReadOnlySpan<byte> tail)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return (false, "missing-content-type", string.Empty);
        var normalized = contentType.Trim().ToLowerInvariant();
        if (!AllowedTypes.TryGetValue(normalized, out var rule)) return (false, "unsupported-content-type", normalized);
        if (rule.MagicHead.Length > head.Length) return (false, "truncated-file", normalized);
        for (var i = 0; i < rule.MagicHead.Length; i++)
        {
            if (head[i] != rule.MagicHead[i]) return (false, "signature-mismatch", normalized);
        }
        if (rule.Trailer is { } trailer)
        {
            if (trailer.Length > tail.Length) return (false, "truncated-file", normalized);
            for (var i = 0; i < trailer.Length; i++)
            {
                if (tail[^(trailer.Length - i)] != trailer[i]) return (false, "signature-mismatch", normalized);
            }
        }
        return (true, null, normalized);
    }

    public static bool IsUnsafe(ReadOnlySpan<byte> body)
    {
        foreach (var marker in UnsafeSignatures)
        {
            if (ContainsAt(body, marker)) return true;
        }
        return false;
    }

    private static readonly byte[][] UnsafeSignatures =
    {
        new byte[] { 0x4D, 0x5A }, // MZ (Windows executable)
        new byte[] { 0x7F, 0x45, 0x4C, 0x46 }, // ELF
        new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14 }, // ZIP with encryption header (heuristic)
        new byte[] { 0x3C, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74 }, // <script
        new byte[] { 0x73, 0x63, 0x72, 0x69, 0x70, 0x74 }, // script
    };

    private static bool ContainsAt(ReadOnlySpan<byte> haystack, byte[] needle)
    {
        if (needle.Length == 0) return true;
        var limit = Math.Min(haystack.Length, DisallowedByteScanWindow);
        for (var i = 0; i <= limit - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
    }

    public static string ComputeSha256Hex(ReadOnlySpan<byte> body)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(body, hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed record AttachmentTypeRule(string ContentType, byte[] MagicHead, string Extension, byte[]? Trailer = null);