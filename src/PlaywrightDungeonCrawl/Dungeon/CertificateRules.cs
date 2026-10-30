using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PlaywrightDungeonCrawl.Dungeon;

/// <summary>
/// A certificate for clearing every open floor, with the name the player chose to print.
/// <paramref name="PreviousId"/> keeps an older ID's links working after an upgrade.
/// </summary>
public sealed record Certificate(string Id, string PlayerId, string Login, string Name, int Floors, int Score, DateTimeOffset IssuedAt,
    string? PreviousId = null);

public static partial class CertificateRules
{
    public const int MaxNameLength = 60;
    public const string Title = "Certificate of Survival";
    public const string CourseName = "Playwright Dungeon Crawl: Playwright for .NET";
    public const string Issuer = "Playwright Dungeon Crawl";

    /// <summary>Earned once every floor the dungeon has open is verified as cleared.</summary>
    public static bool IsEligible(Standing standing, DungeonCatalog catalog) =>
        standing.FloorsCleared >= catalog.Floors.Count;

    /// <summary>The printed name, or null if it's empty, too long or has control characters.</summary>
    public static string? CleanName(string? name)
    {
        var trimmed = string.Join(' ', (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return trimmed.Length is > 0 and <= MaxNameLength && !trimmed.Any(char.IsControl) ? trimmed : null;
    }

    // Crockford's base 32: no I, L, O or U, so an ID read aloud or retyped can't be misread.
    private const string IdAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Like PDC-20261030-K7Q2M9XA: the issue date, then 40 random bits so IDs can't be guessed.</summary>
    public static string NewId(DateTimeOffset issuedAt) =>
        $"PDC-{issuedAt.UtcDateTime:yyyyMMdd}-{RandomNumberGenerator.GetString(IdAlphabet, 8)}";

    /// <summary>The first certificates had 16 hex digits; reprinting one moves it to the new format.</summary>
    public static bool IsLegacyId(string id) => LegacyId().IsMatch(id);

    [GeneratedRegex("^[0-9a-f]{16}$")]
    private static partial Regex LegacyId();

    /// <summary>LinkedIn's "Add to profile" form, filled in for this certificate.</summary>
    public static string LinkedInUrl(Certificate certificate, string verifyUrl) =>
        "https://www.linkedin.com/profile/add?startTask=CERTIFICATION_NAME" +
        $"&name={Uri.EscapeDataString(CourseName)}" +
        $"&organizationName={Uri.EscapeDataString(Issuer)}" +
        $"&issueYear={certificate.IssuedAt.Year}&issueMonth={certificate.IssuedAt.Month}" +
        $"&certUrl={Uri.EscapeDataString(verifyUrl)}" +
        $"&certId={Uri.EscapeDataString(certificate.Id)}";
}
