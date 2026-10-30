using System.Security.Cryptography;

namespace PlaywrightDungeonCrawl.Dungeon;

/// <summary>A certificate for clearing every open floor, with the name the player chose to print.</summary>
public sealed record Certificate(string Id, string PlayerId, string Login, string Name, int Floors, int Score, DateTimeOffset IssuedAt);

public static class CertificateRules
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

    /// <summary>Short, unguessable, and fine in a URL.</summary>
    public static string NewId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();

    /// <summary>LinkedIn's "Add to profile" form, filled in for this certificate.</summary>
    public static string LinkedInUrl(Certificate certificate, string verifyUrl) =>
        "https://www.linkedin.com/profile/add?startTask=CERTIFICATION_NAME" +
        $"&name={Uri.EscapeDataString(CourseName)}" +
        $"&organizationName={Uri.EscapeDataString(Issuer)}" +
        $"&issueYear={certificate.IssuedAt.Year}&issueMonth={certificate.IssuedAt.Month}" +
        $"&certUrl={Uri.EscapeDataString(verifyUrl)}" +
        $"&certId={Uri.EscapeDataString(certificate.Id)}";
}
