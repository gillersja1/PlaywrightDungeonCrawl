using System.Text.Json;

namespace PlaywrightDungeonCrawl.Dungeon;

/// <summary>
/// A freshly generated puzzle. <see cref="Briefing"/> is sent to the browser;
/// <see cref="Answer"/> never leaves the server.
/// </summary>
public sealed record RoomPuzzle(object Briefing, string Answer, object? Reveal = null, TimeSpan RevealAfter = default)
{
    /// <summary>
    /// Steps a room makes you clear, in order, before its final answer counts.
    /// Each one is checked on the server, and only then hands over what the next one needs.
    /// </summary>
    public IReadOnlyList<Trial> Trials { get; init; } = [];
}

/// <summary>One step of a multi-step room. <see cref="Unlocks"/> is sent to the browser once it's cleared.</summary>
public sealed record Trial(Func<string, bool> Accepts, object Unlocks);

/// <summary>
/// A trap room. Each attempt gets a new random puzzle and a deadline that is
/// generous for a script and tight for a human.
/// </summary>
public abstract class Room
{
    public abstract string Slug { get; }
    public abstract string Name { get; }
    public abstract string Objective { get; }
    public abstract TimeSpan TimeLimit { get; }

    /// <summary>
    /// Progressively stronger hints: the idea, then the Playwright feature,
    /// then nearly the code.
    /// </summary>
    public abstract string[] Nudges { get; }

    /// <summary>An optional plaque on the room's wall, e.g. a dedication.</summary>
    public virtual string? Plaque => null;

    public abstract RoomPuzzle Generate(Random rng);

    public virtual bool IsCorrect(RoomPuzzle puzzle, string answer) =>
        string.Equals(puzzle.Answer, answer.Trim(), StringComparison.Ordinal);

    protected static string Rune(Random rng, int length = 6)
    {
        const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        return string.Create(length, rng, (span, r) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = alphabet[r.Next(alphabet.Length)];
            }
        });
    }
}

/// <summary>
/// Floor 1, setup: a door that only opens for an automated browser, so the
/// only way to the flag is to get a first Playwright test running.
/// </summary>
public sealed class WelcomeMatt : Room
{
    public override string[] Nudges { get; } =
    {
        "Clicking Knock yourself won't work: the door checks whether a test is driving the browser. You need to run a test from Visual Studio.",
        "Start from the training dummy test in step 6 of this floor's scroll. Copy it into a WelcomeMattTests class, then change where it goes, what it clicks and how it ends.",
        "Your test needs three lines: EnterAsync(\"/rooms/welcome-matt\"), a click on Page.GetByRole(AriaRole.Button, new() { Name = \"Knock\" }), then await TakeTreasureAsync().",
    };

    public override string Slug => "welcome-matt";
    public override string Name => "The Welcome Matt";
    public override string Objective =>
        "A heavy door with a brass knocker. Knock, and the door decides whether you're worthy. It has opinions about humans.";
    public override TimeSpan TimeLimit => TimeSpan.FromMinutes(10);

    public override string Plaque =>
        "The Welcome Matt is named in honor of Matt Dinniman, whose Dungeon Crawler Carl books turned a " +
        "dungeon into the best game show nobody would ever volunteer for, and inspired this one. If you enjoy " +
        "crawling here, go read them. This dungeon is a fan's tribute, not affiliated with him or his books.";

    // The page sends this only when navigator.webdriver is true, which is
    // the case in any browser Playwright launches.
    public const string AutomatonPassword = "automaton";

    public override RoomPuzzle Generate(Random rng) => new(new { }, AutomatonPassword);
}

/// <summary>Floor 2, locators: find the one open door among many lookalikes.</summary>
public sealed class HallOfDoors : Room
{
    public override string[] Nudges { get; } =
    {
        "The sign you need changes on every visit. Read it from the page first, then use it to find the door.",
        "Two doors carry that sign, so matching on the name alone is a strict mode violation. One of the two is disabled: filter on that.",
        "Read the sign with Page.GetByTestId(\"target-sign\").TextContentAsync(), then click Page.GetByRole(AriaRole.Button, new() { Name = sign, Exact = true, Disabled = false }).",
    };

    private static readonly string[] Names =
    {
        "Ember Gate", "Frost Door", "Iron Hatch", "Bone Arch", "Moss Portal", "Ash Door", "Gloom Gate",
        "Rust Hatch", "Echo Door", "Thorn Gate", "Salt Door", "Cinder Arch", "Raven Gate", "Mire Door",
        "Ivory Hatch", "Storm Gate", "Dusk Door", "Silt Arch", "Wyrm Gate", "Hollow Door", "Copper Hatch",
        "Veil Gate", "Grave Door", "Lantern Arch", "Quartz Gate", "Shade Door", "Bramble Hatch", "Tide Gate",
        "Flint Door", "Marrow Arch", "Spire Gate", "Ochre Door", "Rook Hatch", "Fen Gate", "Gilt Door",
        "Hearth Arch", "Pike Gate", "Wraith Door", "Amber Hatch", "Crypt Gate",
    };

    public override string Slug => "hall-of-doors";
    public override string Name => "The Hall of Doors";
    public override string Objective =>
        "Forty doors line the hall. Two carry the sign you're looking for, but one of them is sealed. Open the other.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(4);

    public override RoomPuzzle Generate(Random rng)
    {
        var labels = Names.OrderBy(_ => rng.Next()).ToList();
        var target = labels[0];
        // The target sign appears twice: once sealed (disabled), once open.
        labels[1] = target;

        var doors = labels
            .Select((label, i) => new { Label = label, Sealed = i == 1 })
            .OrderBy(_ => rng.Next())
            .Select(d => new Door($"door-{Rune(rng, 4).ToLowerInvariant()}", d.Label, d.Sealed))
            .ToList();
        var open = doors.Single(d => d.Label == target && !d.Sealed);
        return new RoomPuzzle(new { Target = target, Doors = doors }, open.Id);
    }

    public sealed record Door(string Id, string Label, bool Sealed);
}

/// <summary>Floor 3, waiting: a rune flickers into view for a split second.</summary>
public sealed class FlickeringTorch : Room
{
    public override string[] Nudges { get; } =
    {
        "The rune is only on screen for a moment, and you can't know when. Don't sleep: wait for it.",
        "Wait for the rune to appear with WaitForAsync(), then read its text straight away, before it vanishes.",
        "The rune is labelled \"Torch rune\": call WaitForAsync() then TextContentAsync() on Page.GetByLabel(\"Torch rune\"), fill the box labelled \"Speak the rune\", and click the \"Speak\" button.",
    };

    public override string Slug => "flickering-torch";
    public override string Name => "The Flickering Torch";
    public override string Objective =>
        "The torch will flare at a moment of its choosing and show a rune for less than a second. Read it, then speak it.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(10);

    public override RoomPuzzle Generate(Random rng)
    {
        var rune = Rune(rng);
        var delay = TimeSpan.FromMilliseconds(rng.Next(1500, 4000));
        return new RoomPuzzle(
            new { DelayMs = (int)delay.TotalMilliseconds, VisibleMs = 600 },
            rune,
            Reveal: new { Rune = rune },
            RevealAfter: delay);
    }
}

/// <summary>Floor 4, forms: copy a ledger into a form of mixed input types.</summary>
public sealed class ScribesLedger : Room
{
    public override string[] Nudges { get; } =
    {
        "Every field in the form has a label that matches a row in the ledger. Read the ledger, then fill each field by its label.",
        "Different inputs need different methods: FillAsync for text, numbers and dates, SelectOptionAsync for the dropdown, CheckAsync for the radio button and the oath.",
        "Dates go into date inputs as yyyy-MM-dd. The class is a radio button: Page.GetByRole(AriaRole.Radio, new() { Name = className }).CheckAsync().",
    };

    public static readonly string[] Guilds = { "Lanternwrights", "Cartographers", "Bone Menders", "Rune Smiths", "Gate Wardens" };
    public static readonly string[] Classes = { "Rogue", "Ranger", "Cleric", "Warden" };
    private static readonly string[] FirstNames = { "Ysolde", "Brannoc", "Merin", "Tavish", "Odalys", "Corwin", "Ilse", "Fenwick" };
    private static readonly string[] LastNames = { "Ashgrove", "Duskwater", "Thornby", "Kettleburn", "Morrow", "Vane" };

    public override string Slug => "scribes-ledger";
    public override string Name => "The Scribe's Ledger";
    public override string Objective =>
        "The scribe will only open the gate for an adventurer whose registration matches the ledger exactly. Fill in the form and sign the oath.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(6);

    public override RoomPuzzle Generate(Random rng)
    {
        var entry = new LedgerEntry(
            $"{FirstNames[rng.Next(FirstNames.Length)]} {LastNames[rng.Next(LastNames.Length)]}",
            Guilds[rng.Next(Guilds.Length)],
            Classes[rng.Next(Classes.Length)],
            rng.Next(2, 13),
            new DateOnly(2026, 1, 1).AddDays(rng.Next(0, 365)));

        var ledger = new[]
        {
            new { Label = "Adventurer name", Value = entry.Name },
            new { Label = "Guild", Value = entry.Guild },
            new { Label = "Class", Value = entry.Class },
            new { Label = "Torches carried", Value = entry.Torches.ToString() },
            new { Label = "Departure date", Value = entry.Departure.ToString("yyyy-MM-dd") },
        };
        return new RoomPuzzle(new { Ledger = ledger, Guilds, Classes }, JsonSerializer.Serialize(entry));
    }

    public override bool IsCorrect(RoomPuzzle puzzle, string answer)
    {
        try
        {
            var expected = JsonSerializer.Deserialize<LedgerEntry>(puzzle.Answer)!;
            var actual = JsonSerializer.Deserialize<LedgerSubmission>(answer, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return actual is not null
                && actual.Oath
                && string.Equals(actual.Name?.Trim(), expected.Name, StringComparison.Ordinal)
                && actual.Guild == expected.Guild
                && actual.Class == expected.Class
                && actual.Torches == expected.Torches
                && actual.Departure == expected.Departure;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public sealed record LedgerEntry(string Name, string Guild, string Class, int Torches, DateOnly Departure);

    public sealed record LedgerSubmission(string? Name, string? Guild, string? Class, int? Torches, DateOnly? Departure, bool Oath);
}

/// <summary>Floor 5, frames and dialogs: a button in a framed shadow root behind a confirm dialog.</summary>
public sealed class MirrorChamber : Room
{
    public override string[] Nudges { get; } =
    {
        "The mirror isn't on this page: it's inside an iframe, and page-level locators don't look inside frames.",
        "Use Page.FrameLocator(...) to search inside the frame. The button is also inside a shadow root, but Playwright looks inside those for you.",
        "Shattering asks \"are you sure?\" with a confirm dialog, which Playwright dismisses unless told otherwise. Register Page.Dialog += (_, d) => d.AcceptAsync(); before you click.",
    };

    public override string Slug => "mirror-chamber";
    public override string Name => "The Mirror Chamber";
    public override string Objective =>
        "The rune is sealed behind a mirror in the next room. Find the mirror, shatter it (it will ask if you're sure), and speak the rune you find.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(8);

    public override RoomPuzzle Generate(Random rng)
    {
        var rune = Rune(rng);
        return new RoomPuzzle(new { }, rune, Reveal: new { Rune = rune });
    }
}

/// <summary>Floor 6, network: the rune is only ever in an API response, never on screen.</summary>
public sealed class WhisperingVault : Room
{
    public override string[] Nudges { get; } =
    {
        "The combination never appears on screen. It travels over the network while the page loads.",
        "Capture the response the page requests as it loads. Start listening before you navigate, or it'll be gone before you're ready.",
        "Wrap the navigation: Page.RunAndWaitForResponseAsync(() => EnterAsync(\"/rooms/whispering-vault\"), r => r.Url.Contains(\"/reveal\")), then read whispers.combination from await response.JsonAsync().",
    };

    public override string Slug => "whispering-vault";
    public override string Name => "The Whispering Vault";
    public override string Objective =>
        "As you enter, the vault whispers its combination, but never writes it down. Listen closely, then speak it.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(5);

    public override RoomPuzzle Generate(Random rng)
    {
        var rune = Rune(rng, 8);
        var decoys = Enumerable.Range(0, 3).Select(_ => Rune(rng, 8)).ToArray();
        return new RoomPuzzle(
            new { },
            rune,
            Reveal: new { Vault = "sealed", Whispers = new { Combination = rune, Echoes = decoys } });
    }
}

/// <summary>
/// Floor 7, the first boss: five trials in one run, one for each skill so far.
/// The server checks every trial before it hands over what the next one needs,
/// so the only way through is in order, at script speed.
/// </summary>
public sealed class WardensGauntlet : Room
{
    public override string[] Nudges { get; } =
    {
        "Don't try to write it in one go. Get trial 1 passing, then add trial 2, and so on. Each trial shows a heading like \"Trial 2 of 5\": wait for it before you act.",
        "Everything you learned on floors 2 to 6 is in here, in order: a strict locator, a sigil that appears for a moment, a form, a mirror in a frame that asks \"are you sure?\", and a name that's only ever whispered over the network.",
        "Register the dialog handler before anything else. For the last trial, wrap the click that speaks the mirror's rune: Page.RunAndWaitForResponseAsync(() => speak.ClickAsync(), r => r.Url.Contains(\"/trials\")), then read unlocks.whispers.trueName from the JSON.",
    };

    private static readonly string[] Gates =
    {
        "Iron Gate", "Bone Gate", "Ash Gate", "Rust Gate", "Gloom Gate", "Thorn Gate", "Salt Gate", "Raven Gate",
        "Storm Gate", "Dusk Gate", "Wyrm Gate", "Veil Gate", "Grave Gate", "Quartz Gate", "Shade Gate", "Tide Gate",
        "Flint Gate", "Spire Gate", "Ochre Gate", "Rook Gate", "Fen Gate", "Gilt Gate", "Pike Gate", "Crypt Gate",
    };

    private static readonly string[] Titles =
    {
        "Vexmoor the Unblinking", "Ossric of the Long Watch", "Malgrave the Patient", "Thessaly Ninelocks",
        "Corvane the Keyless", "Ysmera of the Last Door",
    };

    public override string Slug => "wardens-gauntlet";
    public override string Name => "The Warden's Gauntlet";
    public override string Objective =>
        "The Warden guards the way down. Five trials, one after another, each built from a floor you've already beaten. " +
        "Clear them all, then speak the Warden's true name. Nobody has ever heard it said out loud.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(15);

    public override RoomPuzzle Generate(Random rng)
    {
        // Trial 1, locators: the gate's name appears twice, and one of the pair is sealed.
        var names = Gates.OrderBy(_ => rng.Next()).Take(16).ToList();
        var target = names[0];
        names[1] = target;
        var gates = names
            .Select((label, i) => new HallOfDoors.Door($"gate-{Rune(rng, 4).ToLowerInvariant()}", label, i == 1))
            .OrderBy(_ => rng.Next())
            .ToList();
        var open = gates.Single(g => g.Label == target && !g.Sealed).Id;

        // Trial 2, waiting: the sigil shows for a moment, after a delay nobody can predict.
        var sigil = Rune(rng);

        // Trial 3, forms: swear the oath exactly as the Warden reads it out.
        var oath = new OathEntry(
            Titles[rng.Next(Titles.Length)],
            ScribesLedger.Guilds[rng.Next(ScribesLedger.Guilds.Length)],
            rng.Next(2, 13));

        // Trial 4, frames: the rune behind the mirror. Trial 5, network: the true name, never shown.
        var mirrorRune = Rune(rng);
        var trueName = Rune(rng, 8);
        var echoes = Enumerable.Range(0, 3).Select(_ => Rune(rng, 8)).ToArray();

        return new RoomPuzzle(new { Target = target, Gates = gates }, trueName)
        {
            Trials =
            [
                new(answer => answer.Trim() == open,
                    new { DelayMs = rng.Next(1000, 2500), VisibleMs = 600, Sigil = sigil }),
                new(answer => answer.Trim() == sigil,
                    new { Oath = new[]
                    {
                        new { Label = "Warden's title", Value = oath.Title },
                        new { Label = "Guild", Value = oath.Guild },
                        new { Label = "Torches carried", Value = oath.Torches.ToString() },
                    }, ScribesLedger.Guilds }),
                new(answer => Swears(answer, oath),
                    new { MirrorRune = mirrorRune }),
                new(answer => answer.Trim() == mirrorRune,
                    new { Warden = "listening", Whispers = new { TrueName = trueName, Echoes = echoes } }),
            ],
        };
    }

    private static bool Swears(string answer, OathEntry expected)
    {
        try
        {
            var sworn = JsonSerializer.Deserialize<OathSubmission>(answer, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return sworn is not null
                && sworn.Sworn
                && string.Equals(sworn.Title?.Trim(), expected.Title, StringComparison.Ordinal)
                && sworn.Guild == expected.Guild
                && sworn.Torches == expected.Torches;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record OathEntry(string Title, string Guild, int Torches);

    public sealed record OathSubmission(string? Title, string? Guild, int? Torches, bool Sworn);
}
