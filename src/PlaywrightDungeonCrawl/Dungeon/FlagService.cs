using System.Security.Cryptography;
using System.Text;

namespace PlaywrightDungeonCrawl.Dungeon;

/// <summary>
/// Issues and checks treasure flags. A flag is an HMAC of the room's slug,
/// so it can be verified without storing anything, and can't be guessed
/// without the server's secret.
/// </summary>
public sealed class FlagService
{
    private readonly byte[] _secret;

    public FlagService(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("A flag secret is required.", nameof(secret));
        }
        _secret = Encoding.UTF8.GetBytes(secret);
    }

    public string ForRoom(string roomSlug)
    {
        var hash = HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(roomSlug));
        return $"PDC{{{Convert.ToHexString(hash, 0, 6).ToLowerInvariant()}}}";
    }

    public bool IsValid(string roomSlug, string? flag)
    {
        if (string.IsNullOrWhiteSpace(flag))
        {
            return false;
        }
        var expected = Encoding.UTF8.GetBytes(ForRoom(roomSlug));
        var actual = Encoding.UTF8.GetBytes(flag.Trim());
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
