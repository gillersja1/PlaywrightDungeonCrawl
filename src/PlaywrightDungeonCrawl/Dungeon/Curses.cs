using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace PlaywrightDungeonCrawl.Dungeon;

/// <summary>
/// A Cursed Run: a room you've cleared, replayed with a twist the room itself enforces. Breaking a curse earns
/// a badge, never XP, so the score, rank and certificates stay about the floors.
/// </summary>
/// <param name="Lesson">The Playwright habit the curse rewards, shown before you start.</param>
public sealed record Curse(string Code, string Name, string Description, string Lesson)
{
    /// <summary>The verified achievement for breaking this curse in <paramref name="roomSlug"/>.</summary>
    public string AchievementSlug(string roomSlug) => $"{roomSlug}:{Code}";
}

public static class Curses
{
    public const string AchievementKind = "curse";

    public static readonly Curse MissingIds = new("missing-ids", "Curse of the Missing IDs",
        "Every data-testid in the room is gone.",
        "Find things the way a user does: by role, label and text. Chain and filter locators to narrow down.");

    public static readonly Curse Haste = new("haste", "Curse of Haste",
        "Half the usual time, timed on the server.",
        "No fixed sleeps: wait for exactly what you need, and nothing longer.");

    public static readonly Curse ShiftingWalls = new("shifting-walls", "Curse of Shifting Walls",
        "Things move, labels change their CASE, and decoys appear beside the real controls. Touch a decoy and the trap springs.",
        "Resilient locators: match whole names (Exact, or an anchored regex), ignore case where it doesn't matter, never rely on order.");

    public static readonly Curse Flawless = new("flawless", "Curse of the Flawless",
        "One try a day, and only if you've taken no help on this floor. Miss and it's gone until tomorrow.",
        "Get it right first time: write it against the normal room, then run it once for keeps.");

    public static readonly IReadOnlyList<Curse> All = [MissingIds, Haste, ShiftingWalls, Flawless];

    /// <summary>Rooms with test ids to take away. Elsewhere the Missing IDs curse would change nothing.</summary>
    private static readonly HashSet<string> RoomsWithTestIds = ["hall-of-doors", "portal-bazaar", "tinkerers-workbench", "wardens-gauntlet"];

    public static Curse? Find(string? code) => All.FirstOrDefault(c => c.Code == code);

    /// <summary>The curses a room offers. The setup room has no timer to halve and no tricks to shift: it offers none.</summary>
    public static IReadOnlyList<Curse> For(Room room) => room.TimeLimit >= TimeSpan.FromMinutes(5)
        ? []
        : All.Where(c => c != MissingIds || RoomsWithTestIds.Contains(room.Slug)).ToList();

    public static bool Offers(Room room, Curse curse) => For(room).Contains(curse);

    /// <summary>How long a cursed attempt has: Haste halves it.</summary>
    public static TimeSpan TimeLimit(Room room, Curse? curse) => curse == Haste ? room.TimeLimit / 2 : room.TimeLimit;

    /// <summary>Flawless needs a clean floor: no hints read and no nudges taken there (help keys look like "hint:locators:0").</summary>
    public static bool TookHelp(PlayerProgress progress, Floor floor) => progress.Help.Keys.Any(key =>
        key.Split(':') is [_, var where, ..] && (where == floor.Slug || where == floor.Room.Slug));

    /// <summary>Marks the day's one Flawless try as used, per room and per UTC day.</summary>
    public static string FlawlessTicket(string roomSlug, DateTimeOffset now) => $"{roomSlug}:{now.UtcDateTime:yyyy-MM-dd}";

    public const string FlawlessTicketKind = "flawless-ticket";
}

/// <summary>
/// A run link: <c>/rooms/{room}?run={Token}</c>. The test that opens it plays for <see cref="PlayerId"/>, so
/// breaking the curse is recorded on their account even though the test's browser isn't signed in.
/// </summary>
public sealed record CursedRun(string Token, string PlayerId, string Login, string RoomSlug, Curse Curse, DateTimeOffset ExpiresAt);

/// <summary>Run links last <see cref="Lifetime"/>. A Flawless link works for one attempt; the others until the curse is broken.</summary>
public sealed class CursedRunStore(IMemoryCache cache, TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    public CursedRun Issue(string playerId, string login, string roomSlug, Curse curse)
    {
        // Short enough to paste into a test, long enough that nobody guesses one.
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var run = new CursedRun(token, playerId, login, roomSlug, curse, clock.GetUtcNow() + Lifetime);
        cache.Set(Key(token), run, run.ExpiresAt);
        return run;
    }

    /// <summary>The run behind a link, if it's still good and belongs to this room. A Flawless run is used up by being found.</summary>
    public CursedRun? Take(string? token, string roomSlug)
    {
        if (string.IsNullOrEmpty(token) || !cache.TryGetValue(Key(token), out CursedRun? run) || run!.RoomSlug != roomSlug
            || clock.GetUtcNow() > run.ExpiresAt)
        {
            return null;
        }
        if (run.Curse == Curses.Flawless)
        {
            cache.Remove(Key(token));
        }
        return run;
    }

    /// <summary>Once the curse is broken the link has done its job.</summary>
    public void Finish(CursedRun run) => cache.Remove(Key(run.Token));

    private static string Key(string token) => $"cursed-run:{token}";
}
