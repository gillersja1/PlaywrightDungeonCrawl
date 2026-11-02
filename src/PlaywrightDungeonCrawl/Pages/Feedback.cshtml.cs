using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Pages;

public class FeedbackModel(IFeedbackStore store, IMemoryCache cache, TimeProvider clock, IConfiguration config, ILogger<FeedbackModel> log) : PageModel
{
    /// <summary>Submissions allowed per visitor address in <see cref="Window"/>; enough for people, not for spam.</summary>
    public const int PerWindow = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    [BindProperty(SupportsGet = true)]
    public string? From { get; set; }

    [BindProperty, Range(1, 5)]
    public int? Rating { get; set; }

    [BindProperty, Required(ErrorMessage = "Tell the dungeon something first."),
     StringLength(FeedbackRules.MaxMessage, ErrorMessage = "Keep it under 2,000 characters.")]
    public string? Message { get; set; }

    [BindProperty]
    public string? Email { get; set; }

    /// <summary>Hidden from people; bots that fill in every field give themselves away.</summary>
    [BindProperty]
    public string? Website { get; set; }

    public bool Sent { get; private set; }

    public string IssuesUrl => StarterKit.RepoUrl(config) + "/issues";

    public string ReturnPath => FeedbackRules.FromPage(From);

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        if (email is not null && !FeedbackRules.LooksLikeEmail(email))
        {
            ModelState.AddModelError(nameof(Email), "That doesn't look like an email address. Leave it blank if you'd rather not say.");
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }
        if (!string.IsNullOrEmpty(Website))
        {
            // Pretend it worked so the bot moves on.
            Sent = true;
            return Page();
        }
        if (!TakeSlot())
        {
            ModelState.AddModelError("", "That's a lot of feedback at once. Give it a few minutes and try again.");
            return Page();
        }

        var entry = new FeedbackEntry(Rating, Message!.Trim(), email, ReturnPath,
            User.Identity?.IsAuthenticated == true ? User.Identity.Name : null, clock.GetUtcNow());
        await store.AddAsync(entry, HttpContext.RequestAborted);
        // The email stays in the database, out of the logs.
        log.LogInformation("Feedback from {Page}, rating {Rating}: {Message}", entry.Page, entry.Rating, entry.Message);
        Sent = true;
        return Page();
    }

    private bool TakeSlot()
    {
        var key = $"feedback:{HttpContext.Connection.RemoteIpAddress}";
        var count = cache.GetOrCreate(key, e =>
        {
            e.AbsoluteExpirationRelativeToNow = Window;
            return new int[1];
        })!;
        lock (count)
        {
            if (count[0] >= PerWindow)
            {
                return false;
            }
            count[0]++;
            return true;
        }
    }
}
