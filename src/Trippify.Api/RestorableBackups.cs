using System.Security.Cryptography;
using System.Text;

namespace Trippify.Api;

public sealed class RestorableBackupService(IConfiguration configuration)
{
    private readonly string _root = configuration["SelfHosted:BackupDirectory"]
        ?? Path.Combine(AppContext.BaseDirectory, "backups");

    public async Task<(string path, string checksum, bool encrypted)> WriteAsync(
        Guid id, string payload, string? operatorKey, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_root);
        var bytes = Encoding.UTF8.GetBytes(payload);
        var encrypted = !string.IsNullOrWhiteSpace(operatorKey);
        if (encrypted)
        {
            var key = SHA256.HashData(Encoding.UTF8.GetBytes(operatorKey!));
            var nonce = RandomNumberGenerator.GetBytes(12);
            var cipher = new byte[bytes.Length];
            var tag = new byte[16];
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, bytes, cipher, tag);
            bytes = [.. nonce, .. tag, .. cipher];
        }
        var path = Path.Combine(_root, $"{id:N}.trippify-backup");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return (path, Convert.ToHexString(SHA256.HashData(bytes)), encrypted);
    }

    public async Task<string> ReadAndValidateAsync(Trippify.Infrastructure.BackupSnapshot snapshot, string? operatorKey, CancellationToken cancellationToken)
    {
        if (!snapshot.Restorable || snapshot.ArtifactPath is null || snapshot.SchemaVersion != 2)
            throw new InvalidOperationException("This snapshot is not restorable.");
        if (!File.Exists(snapshot.ArtifactPath)) throw new FileNotFoundException("Backup artifact is missing.");
        var bytes = await File.ReadAllBytesAsync(snapshot.ArtifactPath, cancellationToken);
        var checksum = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.Equals(checksum, snapshot.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Backup checksum validation failed.");
        if (snapshot.Encrypted)
        {
            if (string.IsNullOrWhiteSpace(operatorKey)) throw new UnauthorizedAccessException("An operator key is required.");
            if (bytes.Length < 28) throw new InvalidDataException("Backup envelope is invalid.");
            var key = SHA256.HashData(Encoding.UTF8.GetBytes(operatorKey!));
            var nonce = bytes[..12]; var tag = bytes[12..28]; var cipher = bytes[28..]; var plain = new byte[cipher.Length];
            using var aes = new AesGcm(key, 16);
            try { aes.Decrypt(nonce, cipher, tag, plain); } catch (CryptographicException ex) { throw new UnauthorizedAccessException("Backup key validation failed.", ex); }
            return Encoding.UTF8.GetString(plain);
        }
        return Encoding.UTF8.GetString(bytes);
    }
}
