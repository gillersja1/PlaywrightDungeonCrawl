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
        ["portals"] = "Welcome to the bazaar, where every door leads somewhere else and nobody tells you where. Keep track of your tabs. The Dungeon AI keeps track of everything.",
        ["clockwork"] = "Welcome to the foundry. Everything here is gears, and the gears do not respond to polite single clicks. Twist, prod, drag. The Dungeon AI recommends keeping your fingers clear of the escapement.",
        ["couriers"] = "Welcome to the Courier Guild. Everything here travels on paper, in files, through slots in the wall. The Dungeon AI has never seen a courier read anything on a screen. It finds this charming.",
        ["masquerade"] = "Welcome to the Masquerade. Everyone here wears a mask, and nobody can wear two. The Dungeon AI has seen crawlers try. It was undignified.",
        ["oracle"] = "Welcome to the Oracle's Well. The Oracle has no face, no buttons and no patience for clicking. It speaks HTTP. The Dungeon AI finds it refreshingly honest about status codes.",
        ["chancellery"] = "Welcome to the Chancellery. Nothing happens here without a petition, two signatures, a seal and a file. The Chancellor has never once been in a hurry. Today you will be. The Dungeon AI has filed a petition to watch.",
        ["illusion"] = "Welcome to the Illusionist's Theater, where nothing on stage is what the server sent. The Dungeon AI approves. It has been editing what you see since floor one.",
        ["atelier"] = "Welcome to the Architect's Atelier. Every blueprint here is the same blueprint, drawn again. The Architect calls it design. The Dungeon AI calls it a page object.",
        ["hydra"] = "Welcome to the Hydra Pits. Cut one head and it grows back, which the hydra considers a business model. Down here you will need more than one of you. The Dungeon AI has always suspected there were several of you.",
        ["gallery"] = "Welcome to the Portrait Gallery. The sitters have been staring at that wall for three hundred years. Tonight one of them will be replaced, and nobody will notice. Except you, ideally. The Dungeon AI notices everything, but it isn't telling.",
        ["clocktower"] = "Welcome to the Clocktower of Elsewhere, where it is always midnight somewhere and never where you are. The Dungeon AI keeps every time zone at once. It finds this restful.",
        ["flake"] = "You found The Flake. Or it found you. It passed yesterday. It will pass tomorrow. Today it is waiting for you. The Dungeon AI has seen this fight many times and has never once been able to reproduce the ending.",
        ["gauntlet"] = "The Warden has watched every crawler who came down those stairs. It has notes. Five trials, one breath, no do-overs. The Dungeon AI has reserved a seat in the front row.",
    };

    public static string FloorIntro(string floorSlug) => FloorIntros.GetValueOrDefault(floorSlug, "");

    /// <summary>The Dungeon AI's endorsement, printed on every certificate.</summary>
    public const string CertificateSignOff = "Adequate. Genuinely, unsettlingly adequate.";

    /// <summary>Said under the sealed floors on the map.</summary>
    public const string SealedFloors =
        "Floors eighteen and nineteen are under construction. The Dungeon AI apologizes for the inconvenience. It does not apologize for what's down there.";

    /// <summary>Said under the map once every floor is dug.</summary>
    public const string AllFloorsDug =
        "That's every floor. Nineteen of them, all the way down to The Flake. The Dungeon AI has run out of stairs. It has not run out of opinions.";
}
