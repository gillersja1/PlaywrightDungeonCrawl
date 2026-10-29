using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Pages.Rooms;

public class RoomModel(DungeonCatalog catalog, IWebHostEnvironment env, IConfiguration config) : PageModel
{
    public Floor Floor { get; private set; } = null!;

    /// <summary>A room is playable once its arena script exists.</summary>
    public bool IsBuilt { get; private set; }

    /// <summary>Rooms with a long limit (like the setup floor) show no countdown.</summary>
    public bool IsTimed => Floor.Room.TimeLimit < TimeSpan.FromMinutes(5);

    public string StarterRepoUrl => StarterKit.RepoUrl(config);

    /// <summary>e.g. "hall-of-doors" becomes "HallOfDoorsTests".</summary>
    public string TestClassName =>
        string.Concat(Floor.Room.Slug.Split('-').Select(w => char.ToUpperInvariant(w[0]) + w[1..])) + "Tests";

    public IActionResult OnGet(string slug)
    {
        if (catalog.FindByRoom(slug) is not { } floor)
        {
            return NotFound();
        }
        Floor = floor;
        IsBuilt = env.WebRootFileProvider.GetFileInfo($"js/rooms/{slug}.js").Exists;
        return Page();
    }
}
