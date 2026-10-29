using Microsoft.AspNetCore.Mvc.RazorPages;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Pages;

public class IndexModel(DungeonCatalog catalog) : PageModel
{
    public IReadOnlyList<Floor> Floors => catalog.Floors;

    public IReadOnlyList<(int Number, string Skill)> SealedFloors => catalog.SealedFloors;

    public int MaxXp => DungeonCatalog.MaxXp(catalog.Floors.Count);

    public (int Scroll, int Riddle, int Treasure) Rewards =>
        (Dungeon.Rewards.Scroll, Dungeon.Rewards.Riddle, Dungeon.Rewards.Treasure);

    public void OnGet()
    {
    }
}
