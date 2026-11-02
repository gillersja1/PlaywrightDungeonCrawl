namespace PlaywrightDungeonCrawl.Dungeon;

/// <summary>A dungeon resident on the leaderboard: always labeled as an NPC, never passed off as a player.</summary>
/// <param name="Floors">Floors cleared, capped at however many floors are open.</param>
/// <param name="HelpTaken">XP spent on hints and nudges, so their scores aren't all perfect.</param>
public sealed record Rival(string Name, string Icon, string Quip, int Floors, int HelpTaken);

/// <summary>
/// Fixed rivals that give new players someone to beat. Their standings are made up and labeled that way;
/// they never touch verified records or certificates.
/// </summary>
public static class Rivals
{
    public static readonly IReadOnlyList<Rival> All =
    [
        new("The Warden", "🗝️", "Guards floor 7. Has never got past it.", 6, 40),
        new("Skeleton Intern", "💀", "Unpaid. Mostly bones. Very keen.", 4, 25),
        new("The Quartermaster", "📦", "Should be stocking the loot shop.", 2, 0),
        new("Rattlebones the Flaky", "🦴", "Passes on the first run. Fails on the rerun.", 1, 15),
        new("The Dungeon AI", "🤖", "Built the place. Refuses to be tested.", 0, 0),
    ];

    public static IEnumerable<Standing> StandingsFor(DungeonCatalog catalog) => All.Select(r =>
    {
        var floors = Math.Min(r.Floors, catalog.Floors.Count);
        var score = Math.Max(0, floors * (Rewards.Scroll + Rewards.Riddle + Rewards.Treasure) - r.HelpTaken);
        return new Standing(r.Name, floors, score, null) { Npc = r };
    });
}
