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

    /// <summary>The nudges in TypeScript, once this room has been written for it.</summary>
    public virtual string[]? TypeScriptNudges => null;

    public string[] NudgesIn(CodeLanguage language) =>
        language == CodeLanguage.TypeScript && TypeScriptNudges is { } ts ? ts : Nudges;

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

    public override string[] TypeScriptNudges { get; } =
    {
        "Clicking Knock yourself won't work: the door checks whether a test is driving the browser. You need to run a test from VS Code.",
        "Start from the training dummy test in step 6 of this floor's scroll. Copy it into a new file, tests/welcome-matt.spec.ts, then change where it goes, what it clicks and how it ends.",
        "Your test needs three lines: await dungeon.enter('/rooms/welcome-matt'), a click on page.getByRole('button', { name: 'Knock' }), then await dungeon.takeTreasure().",
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

    public override string[] TypeScriptNudges { get; } =
    {
        "The sign you need changes on every visit. Read it from the page first, then use it to find the door.",
        "Two doors carry that sign, so matching on the name alone is a strict mode violation. One of the two is disabled: filter on that.",
        "Read the sign with page.getByTestId('target-sign').textContent(), then click page.getByRole('button', { name: sign!, exact: true, disabled: false }).",
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
        "The rune is labeled \"Torch rune\": call WaitForAsync() then TextContentAsync() on Page.GetByLabel(\"Torch rune\"), fill the box labeled \"Speak the rune\", and click the \"Speak\" button.",
    };

    public override string[] TypeScriptNudges { get; } =
    {
        "The rune is only on screen for a moment, and you can't know when. Don't sleep: wait for it.",
        "Wait for the rune to appear with waitFor(), then read its text straight away, before it vanishes.",
        "The rune is labeled \"Torch rune\": call waitFor() then textContent() on page.getByLabel('Torch rune'), fill the box labeled \"Speak the rune\", and click the \"Speak\" button.",
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

    public override string[] TypeScriptNudges { get; } =
    {
        "Every field in the form has a label that matches a row in the ledger. Read the ledger, then fill each field by its label.",
        "Different inputs need different methods: fill() for text, numbers and dates, selectOption() for the dropdown, check() for the radio button and the oath.",
        "Dates go into date inputs as yyyy-MM-dd. The class is a radio button: page.getByRole('radio', { name: className }).check().",
    };

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

    public override string[] TypeScriptNudges { get; } =
    {
        "The mirror isn't on this page: it's inside an iframe, and page-level locators don't look inside frames.",
        "Use page.frameLocator(...) to search inside the frame. The button is also inside a shadow root, but Playwright looks inside those for you.",
        "Shattering asks \"are you sure?\" with a confirm dialog, which Playwright dismisses unless told otherwise. Register page.on('dialog', d => d.accept()) before you click.",
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

    public override string[] TypeScriptNudges { get; } =
    {
        "The combination never appears on screen. It travels over the network while the page loads.",
        "Capture the response the page requests as it loads. Start waiting before you navigate, or it'll be gone before you're ready.",
        "Start the wait, then navigate: const whisper = page.waitForResponse(r => r.url().includes('/reveal')); await dungeon.enter('/rooms/whispering-vault'); then read whispers.combination from await (await whisper).json().",
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
/// Floor 8: the price is only ever written on the far side of a portal, a link that opens a new tab.
/// That tab first shows a decoy toll, then moves on to the merchant's stall, where the real price is.
/// A test has to catch the new page, wait for it to arrive, read it, and pay back on the first page.
/// </summary>
public sealed class PortalBazaar : Room
{
    public override string[] Nudges { get; } =
    {
        "The price is never written in this room. Step through the portal: it opens in a new tab.",
        "A new tab is a new page, and Page still points at the old one. Catch the new one as it opens. Then don't trust the first thing you read there: the portal moves you on to the merchant's stall.",
        "var stall = await Page.RunAndWaitForPopupAsync(() => Page.GetByRole(AriaRole.Link, new() { Name = \"Step through the portal\" }).ClickAsync()); await stall.WaitForURLAsync(\"**/stall\"); read stall.GetByTestId(\"price\"), then pay on Page, not on stall.",
    };

    public override string[] TypeScriptNudges { get; } =
    {
        "The price is never written in this room. Step through the portal: it opens in a new tab.",
        "A new tab is a new page, and page still points at the old one. Catch the new one as it opens. Then don't trust the first thing you read there: the portal moves you on to the merchant's stall.",
        "Start waiting, then click: const opening = page.waitForEvent('popup'); await page.getByRole('link', { name: 'Step through the portal' }).click(); const stall = await opening; await stall.waitForURL('**/stall'); read stall.getByTestId('price'), then pay on page, not on stall.",
    };

    public override string Slug => "portal-bazaar";
    public override string Name => "The Merchant's Portal";
    public override string Objective =>
        "The merchant won't say his price in this room. He writes it on the far side of a portal. Step through, read it, come back and pay.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(8);

    public override RoomPuzzle Generate(Random rng)
    {
        var price = Rune(rng, 6);
        var toll = Rune(rng, 6);
        return new RoomPuzzle(new { }, price, Reveal: new { Price = price, Toll = toll });
    }
}

/// <summary>
/// Floor 9, mouse and keyboard: three jobs on a tinkerer's bench, each needing a different use of the mouse.
/// Bolts that only turn on a double-click, a mechanism whose options are on its right-click menu, and a furnace
/// dial with no keys or input box, set by dragging it to the right spot. The server checks each step before it
/// hands over what the next one needs: the menu's instruction, then the temperature.
/// </summary>
public sealed class TinkerersWorkbench : Room
{
    public const int Bolts = 4;
    public const int DialMax = 500;
    public const int DialStep = 10;

    public static readonly string[] Tasks =
    {
        "Oil the gears", "Wind the mainspring", "Tighten the escapement", "Polish the cogs", "Reset the pendulum",
    };

    public override string[] Nudges { get; } =
    {
        "Three jobs, in order: the bolts, the mechanism behind the cover, then the furnace dial. A plain click won't do any of them.",
        "The bolts want a double-click. The mechanism's options are on its right-click menu. The dial has no keys and no input box: press the mouse on it, move to the right spot, and let go.",
        "DblClickAsync() each \"Bolt 1\" to \"Bolt 4\". Right-click the mechanism with ClickAsync(new() { Button = MouseButton.Right }) and pick the menu item named in the note. For the dial, get its width from BoundingBoxAsync(); the temperature sits at width * temperature / 500 from its left edge. HoverAsync(new() { Position = ... }) at the left end, Page.Mouse.DownAsync(), HoverAsync at the temperature, Page.Mouse.UpAsync().",
    };

    public override string[] TypeScriptNudges { get; } =
    {
        "Three jobs, in order: the bolts, the mechanism behind the cover, then the furnace dial. A plain click won't do any of them.",
        "The bolts want a double-click. The mechanism's options are on its right-click menu. The dial has no keys and no input box: press the mouse on it, move to the right spot, and let go.",
        "dblclick() each \"Bolt 1\" to \"Bolt 4\". Right-click the mechanism with click({ button: 'right' }) and pick the menu item named in the note. For the dial, get its width from boundingBox(); the temperature sits at width * temperature / 500 from its left edge. hover({ position }) at the left end, page.mouse.down(), hover({ position }) at the temperature, page.mouse.up().",
    };

    public override string Slug => "tinkerers-workbench";
    public override string Name => "The Tinkerer's Workbench";
    public override string Objective =>
        "The foundry's furnace won't light until its clockwork is serviced. Loosen the cover's four bolts, do the job " +
        "scratched inside it, then set the furnace to the exact temperature and fire it. Every job takes a steady hand.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(10);

    public override RoomPuzzle Generate(Random rng)
    {
        var options = Tasks.OrderBy(_ => rng.Next()).ToArray();
        var task = options[rng.Next(options.Length)];
        // Well inside the dial, so neither end is the answer.
        var temperature = rng.Next(10, 46) * DialStep;

        return new RoomPuzzle(new { Bolts, DialMax, DialStep }, temperature.ToString())
        {
            Trials =
            [
                new(answer => answer.Trim() == "loosened", new { Note = task, Options = options }),
                new(answer => answer.Trim() == task, new { Temperature = temperature }),
            ],
        };
    }
}

/// <summary>
/// Floor 10, files: the counter hands over a sealed dispatch as a download, and the seal inside it only
/// counts once it comes back as an upload, in a file with the name the dispatch asks for. Nothing secret is
/// ever shown on the page: it's all in the files.
/// </summary>
public sealed class CouriersCounter : Room
{
    /// <summary>The dispatch file, served by the counter's download link.</summary>
    public sealed record Dispatch(string FileName, string Text);

    public override string[] Nudges { get; } =
    {
        "The seal isn't on the page. It's inside the dispatch the counter hands you, and the reply has to go back as a file.",
        "Wait for the download while you click the link, then read the file it saved. Hand the reply over with the file input; it doesn't have to exist on disk.",
        "var download = await Page.RunAndWaitForDownloadAsync(() => link.ClickAsync()); read File.ReadAllTextAsync(await download.PathAsync()). Then SetInputFilesAsync(new FilePayload { Name = \"<name from the dispatch>\", MimeType = \"text/plain\", Buffer = Encoding.UTF8.GetBytes(seal) }) and click Hand it over.",
    };

    public override string[] TypeScriptNudges { get; } =
    {
        "The seal isn't on the page. It's inside the dispatch the counter hands you, and the reply has to go back as a file.",
        "Wait for the download while you click the link, then read the file it saved. Hand the reply over with the file input; it doesn't have to exist on disk.",
        "Start page.waitForEvent('download') before the click, then fs.readFile(await download.path(), 'utf8'). Then setInputFiles({ name: '<name from the dispatch>', mimeType: 'text/plain', buffer: Buffer.from(seal) }) and click Hand it over.",
    };

    public override string Slug => "couriers-counter";
    public override string Name => "The Courier's Counter";
    public override string Objective =>
        "The Courier Guild trusts paper, not screens. Collect your sealed dispatch, read the seal inside it, and " +
        "hand back a reply with that seal in it, in a file with exactly the name the dispatch asks for.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(10);

    public override RoomPuzzle Generate(Random rng)
    {
        var seal = Rune(rng);
        var reply = $"receipt-{Rune(rng, 4).ToLowerInvariant()}.txt";
        var dispatch = new Dispatch($"dispatch-{Rune(rng, 4).ToLowerInvariant()}.txt",
            "THE COURIER GUILD: SEALED DISPATCH\n\n" +
            $"Seal: {seal}\n\n" +
            $"Write the seal in a file named {reply} and hand it over at the counter.\n" +
            "The Guild does not accept screenshots, photocopies or excuses.\n");
        return new RoomPuzzle(new { }, $"{reply}\n{seal}", Reveal: dispatch);
    }

    /// <summary>The page sends the uploaded file as its name, a newline, then its contents.</summary>
    public override bool IsCorrect(RoomPuzzle puzzle, string answer)
    {
        var expected = puzzle.Answer.Split('\n', 2);
        var given = answer.Split('\n', 2);
        return given.Length == 2 && given[0].Trim() == expected[0] && given[1].Trim() == expected[1];
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

    public override string[] TypeScriptNudges { get; } =
    {
        "Don't try to write it in one go. Get trial 1 passing, then add trial 2, and so on. Each trial shows a heading like \"Trial 2 of 5\": wait for it before you act.",
        "Everything you learned on floors 2 to 6 is in here, in order: a strict locator, a sigil that appears for a moment, a form, a mirror in a frame that asks \"are you sure?\", and a name that's only ever whispered over the network.",
        "Register the dialog handler before anything else. For the last trial, start waiting before the click that speaks the mirror's rune: const reply = page.waitForResponse(r => r.url().includes('/trials')); then click, then read unlocks.whispers.trueName from await (await reply).json().",
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

/// <summary>
/// Floor 11, login state and browser contexts: two guests, Fox and Owl, have to be at the ball at the same time.
/// Every guest wears the same cookie, "mask", so one browser context can only hold one guest: signing Owl in where
/// Fox is signed in takes Fox's mask off, and signing Fox in again starts Fox's evening over (Owl's curtsy was to the
/// old Fox). Two contexts, two masks, both at once.
/// </summary>
public sealed class MaskedBall : Room
{
    /// <summary>Who's at the ball right now. Kept out of /reveal by a reveal time no attempt lives to see.</summary>
    public sealed class Ball(string foxPassword, string owlPassword, string whisper)
    {
        public object Gate { get; } = new();
        public string FoxPassword { get; } = foxPassword;
        public string OwlPassword { get; } = owlPassword;
        public string Whisper { get; } = whisper;
        public string? FoxMask { get; set; }
        public string? OwlMask { get; set; }
        /// <summary>The Fox mask Owl curtsied to; a Fox who signs in again is a new Fox.</summary>
        public string? CurtsiedTo { get; set; }
    }

    public const string Cookie = "mask";

    public override string[] Nudges { get; } =
    {
        "Fox and Owl both wear a cookie called mask, and a browser context keeps one cookie of each name. Two guests at once means two contexts.",
        "Sign Fox in on the room page. Open the invitation in a second context, from Browser.NewContextAsync(), sign Owl in there and return the bow. Then come back to Fox's page.",
        "var owlContext = await Browser.NewContextAsync(); var owl = await owlContext.NewPageAsync(); await owl.GotoAsync(invitationUrl); sign in as Owl, click Return the bow, read the whisper, then type it into Fox's Unmask form on the original Page.",
    };

    public override string[] TypeScriptNudges { get; } =
    {
        "Fox and Owl both wear a cookie called mask, and a browser context keeps one cookie of each name. Two guests at once means two contexts.",
        "Sign Fox in on the room page. Open the invitation in a second context, from browser.newContext(), sign Owl in there and return the bow. Then come back to Fox's page.",
        "const owlContext = await browser.newContext(); const owl = await owlContext.newPage(); await owl.goto(invitationUrl); sign in as Owl, click Return the bow, read the whisper, then type it into Fox's Unmask form on the original page.",
    };

    public override string Slug => "masked-ball";
    public override string Name => "The Masked Ball";
    public override string Objective =>
        "Two guests are on the list, Fox and Owl, and the host only unmasks for Fox once Owl has returned Fox's bow. " +
        "Sign Fox in here, send Owl the invitation, sign Owl in there, and bring Owl's whisper back to Fox. " +
        "Both have to be at the ball at the same time.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(20);

    public override RoomPuzzle Generate(Random rng)
    {
        var ball = new Ball(Rune(rng, 8).ToLowerInvariant(), Rune(rng, 8).ToLowerInvariant(), Rune(rng));
        var briefing = new { guests = new[] { new { name = "Fox", password = ball.FoxPassword }, new { name = "Owl", password = ball.OwlPassword } } };
        return new RoomPuzzle(briefing, Rune(rng, 8), Reveal: ball, RevealAfter: TimeSpan.FromDays(1));
    }
}

/// <summary>
/// Floor 12, API testing: the Oracle has no buttons. Ask your question with a POST and the offering header, follow
/// the Location of the 201 it answers with, and read the prophecy from that resource. Only then speak it on the page.
/// </summary>
public sealed class OraclesWell : Room
{
    /// <summary>The questions asked so far. Kept out of /reveal by a reveal time no attempt lives to see.</summary>
    public sealed class Well(string offering)
    {
        public string Offering { get; } = offering;
        public System.Collections.Concurrent.ConcurrentDictionary<Guid, string> Questions { get; } = new();
    }

    public const string OfferingHeader = "X-Offering";

    public override string[] Nudges { get; } =
    {
        "The Oracle doesn't answer buttons. It answers HTTP requests, and the notice by the well says which ones.",
        "POST your question as JSON with the X-Offering header. The answer isn't in the reply body: look at the reply's status and its Location header.",
        "var reply = await Page.APIRequest.PostAsync(questionsUrl, new() { DataObject = new { question = \"Will I pass?\" }, Headers = new Dictionary<string, string> { [\"X-Offering\"] = offering } }); then GetAsync(reply.Headers[\"location\"]) with the same header and read the prophecy from its JSON.",
    };

    public override string[] TypeScriptNudges { get; } =
    {
        "The Oracle doesn't answer buttons. It answers HTTP requests, and the notice by the well says which ones.",
        "POST your question as JSON with the X-Offering header. The answer isn't in the reply body: look at the reply's status and its Location header.",
        "const reply = await page.request.post(questionsUrl, { data: { question: 'Will I pass?' }, headers: { 'X-Offering': offering } }); then page.request.get(reply.headers()['location']) with the same header and read the prophecy from its JSON.",
    };

    public override string Slug => "oracles-well";
    public override string Name => "The Oracle's Well";
    public override string Objective =>
        "The Oracle speaks only HTTP. Ask it a question through its API, with your offering in the X-Offering header, " +
        "follow it to where the answer lives, and speak the prophecy aloud on this page.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(10);

    public override RoomPuzzle Generate(Random rng)
    {
        var well = new Well(Rune(rng, 10).ToLowerInvariant());
        return new RoomPuzzle(new { offering = well.Offering }, Rune(rng, 8), Reveal: well, RevealAfter: TimeSpan.FromDays(1));
    }
}
