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

    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (PlayerId is null)
        {
            return Page();
        }
        await LoadAsync();
        Name = Existing?.Name ?? User.FindFirstValue("urn:github:name") ?? User.Identity!.Name;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (PlayerId is not { } playerId)
        {
            return Challenge();
        }
        await LoadAsync();
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

        var issued = await records.IssueCertificateAsync(new Certificate(
            Existing?.Id ?? CertificateRules.NewId(), playerId, User.Identity!.Name ?? "", name,
            Mine!.FloorsCleared, Mine.Score, Existing?.IssuedAt ?? clock.GetUtcNow()), HttpContext.RequestAborted);
        logger.LogInformation("Certificate {CertificateId} {Action}", issued.Id, Existing is null ? "issued" : "renamed");
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
