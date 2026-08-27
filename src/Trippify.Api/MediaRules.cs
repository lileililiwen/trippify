using System.Security.Cryptography;

namespace Trippify.Api;

public static class MediaRules
{
    public const long MaxBytes = 10_000_000;
    public const long MaxTotalBytesPerGuide = 50_000_000;
    public const int MaxAttachmentsPerGuide = 50;

    public static readonly IReadOnlyDictionary<string, MediaTypeRule> AllowedTypes = new Dictionary<string, MediaTypeRule>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = new MediaTypeRule("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF }, ".jpg"),
        ["image/png"] = new MediaTypeRule("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, ".png"),
        ["image/webp"] = new MediaTypeRule("image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46 }, ".webp", new byte[] { 0x57, 0x45, 0x42, 0x50 }),
        ["application/pdf"] = new MediaTypeRule("application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 }, ".pdf"),
    };

    public static readonly TimeSpan SignedReadLifetime = TimeSpan.FromMinutes(10);

    public static (bool Ok, string? FailureCode, string NormalizedContentType, string Extension) Validate(string? contentType, ReadOnlySpan<byte> head)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return (false, "missing-content-type", string.Empty, string.Empty);
        var normalized = contentType.Trim().ToLowerInvariant();
        if (!AllowedTypes.TryGetValue(normalized, out var rule)) return (false, "unsupported-content-type", normalized, string.Empty);
        if (rule.MagicHead.Length > head.Length) return (false, "truncated-file", normalized, rule.Extension);
        for (var i = 0; i < rule.MagicHead.Length; i++)
        {
            if (head[i] != rule.MagicHead[i]) return (false, "signature-mismatch", normalized, rule.Extension);
        }
        return (true, null, normalized, rule.Extension);
    }

    public static string ComputeSha256Hex(ReadOnlySpan<byte> body)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(body, hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed record MediaTypeRule(string ContentType, byte[] MagicHead, string Extension, byte[]? Trailer = null);
