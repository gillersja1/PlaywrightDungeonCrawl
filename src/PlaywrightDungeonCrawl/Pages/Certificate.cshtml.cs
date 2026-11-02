using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Pages;

/// <summary>The certificate path: where a signed-in player claims (or reprints) each tier's certificate.</summary>
public class CertificateModel(IPlayerRecords records, IProgressStore progress, DungeonCatalog catalog, TimeProvider clock,
    ILogger<CertificateModel> logger) : PageModel
{
    public enum TierState
    {
        /// <summary>Already issued: it can be reprinted.</summary>
        Earned,
        /// <summary>Every floor it covers is verified: ready to claim.</summary>
        Ready,
        /// <summary>Its floors are open but not all cleared yet.</summary>
        Locked,
        /// <summary>Some of its floors aren't built yet.</summary>
        Sealed,
    }

    public Standing? Mine { get; private set; }
    public IReadOnlyDictionary<CertificateTier, Certificate> Earned { get; private set; } = new Dictionary<CertificateTier, Certificate>();
    public int FloorCount => catalog.Floors.Count;

    [BindProperty]
    public string? Name { get; set; }

    /// <summary>The language printed on the certificate: "ts" or "csharp".</summary>
    [BindProperty]
    public string? Lang { get; set; }

    /// <summary>The tier being claimed or reprinted: "apprentice", "journeyman" or "master".</summary>
    [BindProperty]
    public string? Tier { get; set; }

    public CodeLanguage Language { get; private set; }

    public string? Error { get; private set; }

    /// <summary>The tier the error belongs to, so it shows next to the right form.</summary>
    public CertificateTier? ErrorTier { get; private set; }

    public TierState StateOf(TierRules tier) =>
        Earned.ContainsKey(tier.Tier) ? TierState.Earned
        : !CertificateRules.IsOpen(tier, catalog) ? TierState.Sealed
        : Mine is not null && CertificateRules.IsEligible(Mine, tier, catalog) ? TierState.Ready
        : TierState.Locked;

    public async Task<IActionResult> OnGetAsync()
    {
        if (PlayerId is null)
        {
            return Page();
        }
        await LoadAsync();
        Name = Earned.Values.FirstOrDefault()?.Name ?? User.FindFirstValue("urn:github:name") ?? User.Identity!.Name;
        Language = Earned.Values.FirstOrDefault()?.Language ?? Languages.Current(HttpContext);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (PlayerId is not { } playerId)
        {
            return Challenge();
        }
        await LoadAsync();
        var tier = CertificateRules.For(CertificateRules.ParseTier(Tier) ?? CertificateTier.Apprentice);
        Earned.TryGetValue(tier.Tier, out var existing);
        Language = Languages.Parse(Lang) ?? existing?.Language ?? Languages.Current(HttpContext);
        ErrorTier = tier.Tier;

        var state = StateOf(tier);
        if (state == TierState.Sealed)
        {
            Error = $"The {tier.Name} certificate opens once floors 1 to {tier.Floors} are built.";
            return Page();
        }
        if (state == TierState.Locked)
        {
            Error = $"The {tier.Name} certificate needs floors 1 to {tier.Floors} verified as cleared.";
            return Page();
        }
        if (CertificateRules.CleanName(Name) is not { } name)
        {
            Error = $"Enter the name to print, up to {CertificateRules.MaxNameLength} characters.";
            return Page();
        }

        var issuedAt = existing?.IssuedAt ?? clock.GetUtcNow();
        var upgrade = existing is not null && CertificateRules.IsLegacyId(existing.Id);
        var id = existing is null || upgrade ? CertificateRules.NewId(issuedAt) : existing.Id;
        // A reprint keeps the floors and score it was issued with; a new tier records them as they are now.
        var issued = await records.IssueCertificateAsync(new Certificate(
            id, playerId, User.Identity!.Name ?? "", name, existing?.Floors ?? tier.Floors, existing?.Score ?? Mine!.Score, issuedAt,
            upgrade ? existing!.Id : existing?.PreviousId, Language, tier.Tier), HttpContext.RequestAborted);
        logger.LogInformation("Certificate {CertificateId} ({Tier}) {Action}", issued.Id, tier.Name,
            existing is null ? "issued" : upgrade ? "upgraded" : "renamed");
        return Redirect($"/certificates/{issued.Id}");
    }

    private async Task LoadAsync()
    {
        var playerId = PlayerId!;
        var record = await records.GetAsync(playerId, HttpContext.RequestAborted) ?? PlayerRecord.Empty(playerId, User.Identity!.Name ?? "");
        Mine = Leaderboard.Score(record, await progress.GetAsync(playerId, HttpContext.RequestAborted), catalog);
        Earned = (await records.CertificatesForAsync(playerId, HttpContext.RequestAborted)).ToDictionary(c => c.Tier);
    }

    private string? PlayerId => User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}
