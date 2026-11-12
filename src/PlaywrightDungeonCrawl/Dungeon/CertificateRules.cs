using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PlaywrightDungeonCrawl.Dungeon;

/// <summary>The certificate path: each tier covers more of the dungeon, and each is its own certificate.</summary>
public enum CertificateTier { Apprentice, Journeyman, Ultimate }

/// <param name="Floors">Floors 1 to this many must be verified as cleared.</param>
/// <param name="Reach">How far the tier goes, for the certificate path.</param>
public sealed record TierRules(CertificateTier Tier, string Name, int Floors, string Reach)
{
    public string Code => Tier.ToString().ToLowerInvariant();
}

/// <summary>
/// A certificate for one tier of the path, with the name the player chose to print.
/// <paramref name="PreviousId"/> keeps an older ID's links working after an upgrade.
/// <paramref name="Language"/> is the one the player cleared the dungeon in, which the certificate names.
/// Certificates issued before tiers existed are Apprentice certificates.
/// <paramref name="UpdatedAt"/> is when an early certificate was brought up to its full tier; the issue date and ID never change.
/// </summary>
public sealed record Certificate(string Id, string PlayerId, string Login, string Name, int Floors, int Score, DateTimeOffset IssuedAt,
    string? PreviousId = null, CodeLanguage Language = CodeLanguage.CSharp, CertificateTier Tier = CertificateTier.Apprentice,
    DateTimeOffset? UpdatedAt = null);

public static partial class CertificateRules
{
    public const int MaxNameLength = 60;
    public const string Title = "Certificate of Survival";
    /// <summary>The course name LinkedIn lists, e.g. "Playwright Dungeon Crawl: Playwright for TypeScript (Apprentice)".</summary>
    public static string CourseName(CodeLanguage language, CertificateTier tier) =>
        $"Playwright Dungeon Crawl: Playwright for {(language == CodeLanguage.TypeScript ? "TypeScript" : ".NET")} ({For(tier).Name})";

    public static readonly IReadOnlyList<TierRules> Tiers =
    [
        new(CertificateTier.Apprentice, "Apprentice", 7, "from setup to the Warden's Keep"),
        new(CertificateTier.Journeyman, "Journeyman", 13, "through the Chancellery, the second boss"),
        new(CertificateTier.Ultimate, "Ultimate Dungeoneer", 19, "every floor, all the way down to The Flake"),
    ];

    public static TierRules For(CertificateTier tier) => Tiers.Single(t => t.Tier == tier);

    public static CertificateTier? ParseTier(string? code) =>
        Tiers.FirstOrDefault(t => t.Code.Equals(code, StringComparison.OrdinalIgnoreCase))?.Tier;
    public const string Issuer = "Playwright Dungeon Crawl";

    /// <summary>The Playwright Dungeon Crawl LinkedIn Page: using it shows its logo next to the certification.</summary>
    public const string LinkedInOrganizationId = "144076964";

    /// <summary>
    /// Issued before every floor of its tier was built (the first certificates covered floors 1 to 6, before floor 7
    /// existed). It stays valid, and says so; reprinting it once the rest are cleared brings it up to the full tier.
    /// </summary>
    public static bool IsEarlyIssue(Certificate certificate) => certificate.Floors < For(certificate.Tier).Floors;

    /// <summary>A tier opens once the dungeon has built every floor it covers.</summary>
    public static bool IsOpen(TierRules tier, DungeonCatalog catalog) => catalog.Floors.Count >= tier.Floors;

    /// <summary>Earned once every floor the tier covers, 1 to <see cref="TierRules.Floors"/>, is verified as cleared.</summary>
    public static bool IsEligible(Standing standing, TierRules tier, DungeonCatalog catalog) =>
        IsOpen(tier, catalog) && standing.ClearedThrough >= tier.Floors;

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
        $"&name={Uri.EscapeDataString(CourseName(certificate.Language, certificate.Tier))}" +
        $"&organizationId={LinkedInOrganizationId}" +
        $"&issueYear={certificate.IssuedAt.Year}&issueMonth={certificate.IssuedAt.Month}" +
        $"&certUrl={Uri.EscapeDataString(verifyUrl)}" +
        $"&certId={Uri.EscapeDataString(certificate.Id)}";
}
