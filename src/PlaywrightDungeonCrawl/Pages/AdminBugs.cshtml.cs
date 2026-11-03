using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Pages;

/// <summary>
/// Where the dungeon's keeper reviews bug reports. Anyone not in Dungeon__Admins gets a plain 404,
/// so the page doesn't even admit to existing.
/// </summary>
public class AdminBugsModel(IBugReports bugs, IConfiguration config, ILogger<AdminBugsModel> log) : PageModel
{
    public IReadOnlyList<BugReport> Reports { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!BugRules.IsAdmin(User, config))
        {
            return NotFound();
        }
        Reports = await bugs.ListAsync(HttpContext.RequestAborted);
        return Page();
    }

    public async Task<IActionResult> OnPostReviewAsync(long id, string verdict)
    {
        if (!BugRules.IsAdmin(User, config))
        {
            return NotFound();
        }
        var status = verdict switch
        {
            "confirm" => BugStatus.Confirmed,
            "reject" => BugStatus.Rejected,
            "reopen" => BugStatus.Pending,
            _ => (BugStatus?)null,
        };
        if (status is null || !await bugs.ReviewAsync(id, status.Value, HttpContext.RequestAborted))
        {
            return BadRequest();
        }
        log.LogInformation("Bug report {Id} marked {Status} by {Login}", id, status, User.Identity?.Name);
        return RedirectToPage();
    }
}
