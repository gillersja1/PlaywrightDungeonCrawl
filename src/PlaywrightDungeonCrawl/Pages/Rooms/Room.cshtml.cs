using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Pages.Rooms;

public class RoomModel(DungeonCatalog catalog, IWebHostEnvironment env, IConfiguration config,
    IPlayerRecords records, IProgressStore progress, CursedRunStore runs, TimeProvider clock) : PageModel
{
    public Floor Floor { get; private set; } = null!;

    /// <summary>A room is playable once its arena script exists.</summary>
    public bool IsBuilt { get; private set; }

    /// <summary>Rooms with a long limit (like the setup floor) show no countdown.</summary>
    public bool IsTimed => Floor.Room.TimeLimit < TimeSpan.FromMinutes(5);

    /// <summary>The language this room is taught in: TypeScript only once the floor has it.</summary>
    public CodeLanguage Language { get; private set; }

    public string[] Nudges => Floor.Room.NudgesIn(Language);

    public string StarterRepoUrl => StarterKit.RepoUrl(config, Language);

    /// <summary>e.g. "hall-of-doors" becomes "HallOfDoorsTests".</summary>
    public string TestClassName =>
        string.Concat(Floor.Room.Slug.Split('-').Select(w => char.ToUpperInvariant(w[0]) + w[1..])) + "Tests";

    public IReadOnlyList<Curse> CursesHere => Curses.For(Floor.Room);

    public bool AccountsOn => Accounts.IsEnabled(config);

    public bool SignedIn => PlayerId is not null;

    /// <summary>Cursed Runs open once this room's treasure is verified for the signed-in player.</summary>
    public bool RoomCleared { get; private set; }

    /// <summary>Codes of the curses the player has broken in this room.</summary>
    public IReadOnlySet<string> Broken { get; private set; } = new HashSet<string>();

    /// <summary>Why the Flawless curse can't be started right now, if it can't.</summary>
    public string? FlawlessBlocked { get; private set; }

    /// <summary>The run link just issued, shown with the code to open it.</summary>
    public CursedRun? Run { get; private set; }

    public string? RunError { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        if (!await LoadAsync(slug))
        {
            return NotFound();
        }
        return Page();
    }

    /// <summary>Issues a run link for one curse in this room.</summary>
    public async Task<IActionResult> OnPostStartRunAsync(string slug, string curse)
    {
        if (!await LoadAsync(slug))
        {
            return NotFound();
        }
        if (PlayerId is not { } playerId)
        {
            return Challenge();
        }
        var chosen = Curses.Find(curse);
        if (chosen is null || !Curses.Offers(Floor.Room, chosen))
        {
            RunError = "This room doesn't have that curse.";
        }
        else if (!RoomCleared)
        {
            RunError = "Clear this room while signed in first. Cursed runs are for rooms you've already beaten.";
        }
        else if (chosen == Curses.Flawless && FlawlessBlocked is { } why)
        {
            RunError = why;
        }
        else
        {
            if (chosen == Curses.Flawless)
            {
                await records.RecordAsync(playerId, User.Identity!.Name ?? "", Curses.FlawlessTicketKind,
                    Curses.FlawlessTicket(Floor.Room.Slug, clock.GetUtcNow()), HttpContext.RequestAborted);
                FlawlessBlocked = "You've used today's Flawless try in this room.";
            }
            Run = runs.Issue(playerId, User.Identity!.Name ?? "", Floor.Room.Slug, chosen);
        }
        return Page();
    }

    private async Task<bool> LoadAsync(string slug)
    {
        if (catalog.FindByRoom(slug) is not { } floor)
        {
            return false;
        }
        Floor = floor;
        Language = floor.For(Languages.Current(HttpContext));
        IsBuilt = env.WebRootFileProvider.GetFileInfo($"js/rooms/{slug}.js").Exists;

        if (PlayerId is { } playerId && CursesHere.Count > 0)
        {
            var record = await records.GetAsync(playerId, HttpContext.RequestAborted) ?? PlayerRecord.Empty(playerId, "");
            RoomCleared = record.Achievements.Any(a => a.Kind == Achievement.Treasure && a.Slug == slug);
            Broken = record.Achievements
                .Where(a => a.Kind == Curses.AchievementKind && a.Slug.StartsWith(slug + ":", StringComparison.Ordinal))
                .Select(a => a.Slug[(slug.Length + 1)..])
                .ToHashSet();
            var ticket = Curses.FlawlessTicket(slug, clock.GetUtcNow());
            if (record.Achievements.Any(a => a.Kind == Curses.FlawlessTicketKind && a.Slug == ticket))
            {
                FlawlessBlocked = "You've used today's Flawless try in this room. It comes back at midnight UTC.";
            }
            else if (Curses.TookHelp(await progress.GetAsync(playerId, HttpContext.RequestAborted), floor))
            {
                FlawlessBlocked = "You've taken help on this floor, so it can't be Flawless. Plenty of other curses to break.";
            }
        }
        return true;
    }

    private string? PlayerId => User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}
