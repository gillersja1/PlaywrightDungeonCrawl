using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Pages.Floors;

public class RiddleModel(DungeonCatalog catalog) : PageModel
{
    public Floor Floor { get; private set; } = null!;

    [BindProperty]
    public int?[] Answers { get; set; } = [];

    public bool Graded { get; private set; }
    public int CorrectCount { get; private set; }
    public bool Passed => Graded && CorrectCount == Floor.Riddles.Length;

    public IActionResult OnGet(string slug) => Load(slug) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync(string slug)
    {
        if (!Load(slug))
        {
            return NotFound();
        }
        Graded = true;
        CorrectCount = Grade(Floor, Answers);
        if (Passed)
        {
            await Leaderboard.RecordIfSignedInAsync(HttpContext, Achievement.Riddle, Floor.Slug);
        }
        return Page();
    }

    public static int Grade(Floor floor, IReadOnlyList<int?> answers) =>
        floor.Riddles.Where((riddle, i) => i < answers.Count && answers[i] == riddle.CorrectIndex).Count();

    private bool Load(string slug)
    {
        if (catalog.Find(slug) is not { } floor)
        {
            return false;
        }
        Floor = floor;
        return true;
    }
}
