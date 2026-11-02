using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Pages;

/// <summary>Where a signed-in player claims (or renames) their certificate.</summary>
public class CertificateModel(IPlayerRecords records, IProgressStore progress, DungeonCatalog catalog, TimeProvider clock,
    ILogger<CertificateModel> logger) : PageModel
{
    public Standing? Mine { get; private set; }
    public Certificate? Existing { get; private set; }
    public bool Eligible { get; private set; }
    public int FloorCount => catalog.Floors.Count;

    [BindProperty]
    public string? Name { get; set; }

    /// <summary>The language printed on the certificate: "ts" or "csharp".</summary>
    [BindProperty]
    public string? Lang { get; set; }

    public CodeLanguage Language { get; private set; }

    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (PlayerId is null)
        {
            return Page();
        }
        await LoadAsync();
        Name = Existing?.Name ?? User.FindFirstValue("urn:github:name") ?? User.Identity!.Name;
        Language = Existing?.Language ?? Languages.Current(HttpContext);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (PlayerId is not { } playerId)
        {
            return Challenge();
        }
        await LoadAsync();
        Language = Languages.Parse(Lang) ?? Existing?.Language ?? Languages.Current(HttpContext);
        if (!Eligible)
        {
            Error = $"The certificate needs all {FloorCount} floors verified as cleared.";
            return Page();
        }
        if (CertificateRules.CleanName(Name) is not { } name)
        {
            Error = $"Enter the name to print, up to {CertificateRules.MaxNameLength} characters.";
            return Page();
        }

        var issuedAt = Existing?.IssuedAt ?? clock.GetUtcNow();
        var upgrade = Existing is not null && CertificateRules.IsLegacyId(Existing.Id);
        var id = Existing is null || upgrade ? CertificateRules.NewId(issuedAt) : Existing.Id;
        var issued = await records.IssueCertificateAsync(new Certificate(
            id, playerId, User.Identity!.Name ?? "", name, Mine!.FloorsCleared, Mine.Score, issuedAt,
            upgrade ? Existing!.Id : Existing?.PreviousId, Language), HttpContext.RequestAborted);
        logger.LogInformation("Certificate {CertificateId} {Action}", issued.Id,
            Existing is null ? "issued" : upgrade ? "upgraded" : "renamed");
        return Redirect($"/certificates/{issued.Id}");
    }

    private async Task LoadAsync()
    {
        var playerId = PlayerId!;
        var record = await records.GetAsync(playerId, HttpContext.RequestAborted) ?? PlayerRecord.Empty(playerId, User.Identity!.Name ?? "");
        Mine = Leaderboard.Score(record, await progress.GetAsync(playerId, HttpContext.RequestAborted), catalog);
        Existing = await records.CertificateForAsync(playerId, HttpContext.RequestAborted);
        Eligible = Existing is not null || CertificateRules.IsEligible(Mine, catalog);
    }

    private string? PlayerId => User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}
