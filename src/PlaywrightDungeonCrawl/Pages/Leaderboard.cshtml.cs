using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Pages;

public class LeaderboardModel(IPlayerRecords records, IProgressStore progress, DungeonCatalog catalog, IConfiguration config) : PageModel
{
    public IReadOnlyList<(int Rank, Standing Standing)> Rows { get; private set; } = [];

    public bool AccountsOn => Accounts.IsEnabled(config);

    public int FloorCount => catalog.Floors.Count;

    /// <summary>The signed-in player's own verified standing, listed or not.</summary>
    public Standing? Mine { get; private set; }

    public bool MineIsListed { get; private set; }

    public async Task OnGetAsync()
    {
        Rows = await Leaderboard.BuildAsync(records, progress, catalog, HttpContext.RequestAborted);
        if (PlayerId is { } playerId)
        {
            var record = await records.GetAsync(playerId, HttpContext.RequestAborted)
                ?? PlayerRecord.Empty(playerId, User.Identity!.Name ?? "");
            Mine = Leaderboard.Score(record, await progress.GetAsync(playerId, HttpContext.RequestAborted), catalog);
            MineIsListed = record.OnLeaderboard;
        }
    }

    /// <summary>The opt-in switch: nobody appears on the board unless they choose to.</summary>
    public async Task<IActionResult> OnPostListingAsync(bool show)
    {
        if (PlayerId is not { } playerId)
        {
            return Challenge();
        }
        await records.SetOnLeaderboardAsync(playerId, User.Identity!.Name ?? "", show, HttpContext.RequestAborted);
        return RedirectToPage();
    }

    private string? PlayerId => User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}
