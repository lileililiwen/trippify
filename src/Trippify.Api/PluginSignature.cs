using System.Security.Cryptography;
using System.Text;

namespace Trippify.Api;

public static class PluginSignature
{
    public const string DefaultSecret = "trippify-dev-shared-hmac-secret";
    public const string HeaderPrefix = "sha256=";

    public static string Sign(string manifest, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest));
        return HeaderPrefix + Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool Verify(string manifest, string signature, string secret)
    {
        if (string.IsNullOrEmpty(signature)) return false;
        var expected = Sign(manifest, secret);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }
}
