namespace PlaywrightDungeonCrawl.Dungeon;

/// <summary>
/// Lines spoken by the Dungeon AI, the dungeon's announcer: polite on the
/// surface, judgmental underneath, quietly rooting for you. Lines that react
/// to progress live in wwwroot/js/dungeon-ai.js.
/// </summary>
public static class DungeonAi
{
    private static readonly Dictionary<string, string> FloorIntros = new()
    {
        ["setup"] = "Welcome, crawler. Before the dungeon lets you in, it requires paperwork, a browser, and proof that you are not entirely human. Two out of three is a fail.",
        ["locators"] = "Forty doors. One correct answer. The Dungeon AI has been told it is unsporting to laugh at crawlers who pick the wrong one. It is working on that.",
        ["waiting"] = "It is dark. Things appear when they please, not when you would like. Impatience has ended more crawls than spiders. Spiders are a close second.",
        ["forms"] = "Welcome to the only floor where the monsters are forms. Every field must be correct. The clerks can smell a typo.",
        ["frames"] = "Everything on this floor is inside something else. Try not to get lost in your own reflection. The last crawler who did is still in there, waving.",
        ["network"] = "The vaults never write anything down. If you want their secrets, you will have to listen to the wire. Please don't hum.",
        ["gauntlet"] = "The Warden has watched every crawler who came down those stairs. It has notes. Five trials, one breath, no do-overs. The Dungeon AI has reserved a seat in the front row.",
    };

    public static string FloorIntro(string floorSlug) => FloorIntros.GetValueOrDefault(floorSlug, "");

    /// <summary>The Dungeon AI's endorsement, printed on every certificate.</summary>
    public const string CertificateSignOff = "Adequate. Genuinely, unsettlingly adequate.";

    /// <summary>Said under the sealed floors on the map.</summary>
    public const string SealedFloors =
        "Floors eight to nineteen are under construction. The Dungeon AI apologizes for the inconvenience. It does not apologize for what's down there.";
}
