using Microsoft.AspNetCore.Mvc.RazorPages;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Pages;

/// <summary>Everyone who found a confirmed bug in the dungeon.</summary>
public class HallOfBugslayersModel(IBugReports bugs) : PageModel
{
    public IReadOnlyList<Bugslayer> Hall { get; private set; } = [];

    public async Task OnGetAsync() => Hall = await bugs.HallAsync(HttpContext.RequestAborted);
}
