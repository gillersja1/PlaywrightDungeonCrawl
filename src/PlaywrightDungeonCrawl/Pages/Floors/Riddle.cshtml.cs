using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Pages.Floors;

public class RiddleModel(DungeonCatalog catalog) : PageModel
{
    public Floor Floor { get; private set; } = null!;

    [BindProperty]
    public int?[] Answers { get; set; } = [];

    /// <summary>The language the riddles were shown in, so they're graded in the same one.</summary>
    [BindProperty]
    public string? Lang { get; set; }

    public CodeLanguage Language { get; private set; }

    public QuizQuestion[] Riddles => Floor.RiddlesIn(Language);

    public bool Graded { get; private set; }
    public int CorrectCount { get; private set; }
    public bool Passed => Graded && CorrectCount == Riddles.Length;

    public IActionResult OnGet(string slug) => Load(slug) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync(string slug)
    {
        if (!Load(slug))
        {
            return NotFound();
        }
        Language = Floor.For(Languages.Parse(Lang) ?? Languages.Current(HttpContext));
        Graded = true;
        CorrectCount = Grade(Riddles, Answers);
        if (Passed)
        {
            await Leaderboard.RecordIfSignedInAsync(HttpContext, Achievement.Riddle, Floor.Slug);
        }
        return Page();
    }

    public static int Grade(IReadOnlyList<QuizQuestion> riddles, IReadOnlyList<int?> answers) =>
        riddles.Where((riddle, i) => i < answers.Count && answers[i] == riddle.CorrectIndex).Count();

    private bool Load(string slug)
    {
        if (catalog.Find(slug) is not { } floor)
        {
            return false;
        }
        Floor = floor;
        Language = floor.For(Languages.Current(HttpContext));
        return true;
    }
}
