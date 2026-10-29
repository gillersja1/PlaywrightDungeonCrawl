using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Pages.Floors;

public class LessonModel(DungeonCatalog catalog) : PageModel
{
    public Floor Floor { get; private set; } = null!;

    public IActionResult OnGet(string slug)
    {
        if (catalog.Find(slug) is not { } floor)
        {
            return NotFound();
        }
        Floor = floor;
        return Page();
    }
}
