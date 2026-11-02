using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Pages.Floors;

public class LessonModel(DungeonCatalog catalog) : PageModel
{
    public Floor Floor { get; private set; } = null!;

    /// <summary>The language this floor is taught in: TypeScript only once it's been written.</summary>
    public CodeLanguage Language { get; private set; }

    public IActionResult OnGet(string slug)
    {
        if (catalog.Find(slug) is not { } floor)
        {
            return NotFound();
        }
        Floor = floor;
        Language = floor.For(Languages.Current(HttpContext));
        return Page();
    }
}
