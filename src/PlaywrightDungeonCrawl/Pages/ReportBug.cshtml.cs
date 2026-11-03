using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Pages;

/// <summary>Report a bug in the dungeon. Signed-in players only, so a confirmed find can be credited to someone.</summary>
public class ReportBugModel(IBugReports bugs, IConfiguration config, ILogger<ReportBugModel> log) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? From { get; set; }

    [BindProperty, Required(ErrorMessage = "Give the bug a short name."),
     StringLength(BugRules.MaxTitle, ErrorMessage = "Keep the name under 120 characters.")]
    public string? BugTitle { get; set; }

    [BindProperty, Required(ErrorMessage = "Say what happened, so it can be reproduced."),
     StringLength(BugRules.MaxDetails, ErrorMessage = "Keep it under 4,000 characters.")]
    public string? Details { get; set; }

    public bool AccountsOn => Accounts.IsEnabled(config);

    public bool SignedIn => PlayerId is not null;

    public bool Sent { get; private set; }

    public IReadOnlyList<BugReport> Mine { get; private set; } = [];

    public string ReturnPath => FeedbackRules.FromPage(From);

    public async Task OnGetAsync() => await LoadMineAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (PlayerId is not { } playerId)
        {
            return Challenge();
        }
        await LoadMineAsync();
        if (!ModelState.IsValid)
        {
            return Page();
        }
        if (Mine.Count(r => r.Status == BugStatus.Pending) >= BugRules.MaxPending)
        {
            ModelState.AddModelError("", $"You already have {BugRules.MaxPending} reports waiting for review. Give the Bugslayer's court a moment to catch up.");
            return Page();
        }

        var report = await bugs.SubmitAsync(playerId, User.Identity!.Name ?? "", BugTitle!.Trim(), Details!.Trim(), ReturnPath, HttpContext.RequestAborted);
        log.LogInformation("Bug report {Id} from {Login} on {Page}: {Title}", report.Id, report.Login, report.Page, report.Title);
        Sent = true;
        await LoadMineAsync();
        return Page();
    }

    private async Task LoadMineAsync()
    {
        if (PlayerId is { } playerId)
        {
            Mine = await bugs.ForPlayerAsync(playerId, HttpContext.RequestAborted);
        }
    }

    private string? PlayerId => User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}
