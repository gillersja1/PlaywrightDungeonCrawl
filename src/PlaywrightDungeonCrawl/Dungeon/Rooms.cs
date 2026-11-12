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

/// <summary>
/// Floor 13, the second boss: one scenario where each skill from floors 8 to 12 unlocks the next. The Chancellor's
/// password is only filed with the Registry API. The Clerk and the Chancellor wear a cookie of the same name, so they
/// need two contexts. The Chancellor's chambers open the strongroom in a new tab. The seal is dragged onto the charter,
/// and the sealed charter is a download that the Clerk files back as an upload. The server checks every link of the
/// chain, so no step can be skipped.
/// </summary>
public sealed class ChancellorsSeal : Room
{
    /// <summary>Who's signed in and what's been sealed. Kept out of /reveal by a reveal time no attempt lives to see.</summary>
    public sealed class Chancellery(string petition, string clerkPassword, string chancellorPassword, string charterName)
    {
        public object Gate { get; } = new();
        public string Petition { get; } = petition;
        public string ClerkPassword { get; } = clerkPassword;
        public string ChancellorPassword { get; } = chancellorPassword;
        /// <summary>The name the Clerk must file the charter under, written only inside the charter.</summary>
        public string CharterName { get; } = charterName;
        public System.Collections.Concurrent.ConcurrentDictionary<Guid, string> Petitions { get; } = new();
        public string? ClerkSeal { get; set; }
        public string? ChancellorSeal { get; set; }
        /// <summary>Minted when the charter is sealed, for the Clerk and Chancellor signed in at that moment.</summary>
        public string? CharterSeal { get; set; }
        public string? SealedForClerk { get; set; }
        public string? SealedByChancellor { get; set; }
    }

    public const string Cookie = "seal";
    public const string PetitionHeader = "X-Petition";
    public const string Office = "Chancellor";

    public override string[] Nudges { get; } =
    {
        "One chain, five links, in order: the Registry API, two people signed in at once, a strongroom that opens in a new tab, a seal that has to be dragged, and a charter that goes out as a download and comes back as an upload.",
        "The Chancellor's password is only in the Registry: POST a petition with the X-Petition header and follow the Location. The Clerk and the Chancellor both wear a cookie called seal, so the chambers need a second context. In there, catch the strongroom tab as it opens.",
        "var reply = await Page.APIRequest.PostAsync(registryUrl, new() { DataObject = new { office = \"Chancellor\" }, Headers = ... }); GetAsync(reply.Headers[\"location\"]) for the password. Sign the Clerk in on Page; open the chambers in Browser.NewContextAsync(). var strongroom = await chambers.RunAndWaitForPopupAsync(...); seal.DragToAsync(charter); RunAndWaitForDownloadAsync on Collect the charter; read the name inside, then SetInputFilesAsync with a FilePayload on the Clerk's Page.",
    };

    public override string[] TypeScriptNudges { get; } =
    {
        "One chain, five links, in order: the Registry API, two people signed in at once, a strongroom that opens in a new tab, a seal that has to be dragged, and a charter that goes out as a download and comes back as an upload.",
        "The Chancellor's password is only in the Registry: POST a petition with the X-Petition header and follow the Location. The Clerk and the Chancellor both wear a cookie called seal, so the chambers need a second context. In there, catch the strongroom tab as it opens.",
        "const reply = await page.request.post(registryUrl, { data: { office: 'Chancellor' }, headers: { 'X-Petition': petition } }); page.request.get(reply.headers()['location'], ...) for the password. Sign the Clerk in on page; open the chambers in browser.newContext(). Start chambers.waitForEvent('popup') before the click; seal.dragTo(charter); start waitForEvent('download') before Collect the charter; read the name inside, then setInputFiles({ name, mimeType, buffer }) on the Clerk's page.",
    };

    public override string Slug => "chancellors-seal";
    public override string Name => "The Chancellor's Seal";
    public override string Objective =>
        "The Journeyman's charter needs the Chancellor's seal, and the Chancellor trusts nobody. Get the password from " +
        "the Registry, keep the Clerk and the Chancellor signed in at the same time, seal the charter in the strongroom, " +
        "and have the Clerk file it under the name written inside.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(30);

    public override RoomPuzzle Generate(Random rng)
    {
        var chancellery = new Chancellery(Rune(rng, 10).ToLowerInvariant(), Rune(rng, 8).ToLowerInvariant(),
            Rune(rng, 8).ToLowerInvariant(), $"charter-{Rune(rng, 4).ToLowerInvariant()}.txt");
        return new RoomPuzzle(new { petition = chancellery.Petition, clerkPassword = chancellery.ClerkPassword }, Rune(rng, 8),
            Reveal: chancellery, RevealAfter: TimeSpan.FromDays(1));
    }

    /// <summary>The sealed charter, as the strongroom hands it over.</summary>
    public static string CharterText(Chancellery c) =>
        "THE CHANCELLERY: CHARTER OF THE JOURNEYMAN\n\n" +
        $"Seal: {c.CharterSeal}\n\n" +
        $"The Clerk on duty must file this charter under the name {c.CharterName}.\n" +
        "A charter filed under any other name is kindling.\n";

    /// <summary>The Clerk files the charter as its name, a newline, then its contents. Only the seal line has to survive.</summary>
    public static bool IsFiled(Chancellery c, string filing)
    {
        var parts = filing.Split('\n', 2);
        return c.CharterSeal is { } seal && parts.Length == 2 && parts[0].Trim() == c.CharterName
            && parts[1].Contains(seal, StringComparison.Ordinal);
    }
}

/// <summary>
/// Floor 14, mocking responses: the stage fetches its act and a heckler as the page loads. The real act keeps the
/// curtain down, so the test has to fetch it and hand the page a changed copy, and the heckler has to be stopped
/// before he says a word. The word on the real act is the answer, so a fake made from nothing won't do.
/// </summary>
public sealed class IllusionistsStage : Room
{
    public override string[] Nudges { get; } =
    {
        "The page asks the server for two things as it loads: the act and a heckler. Watch what comes back, and change it before the page sees it.",
        "Set your routes up before you open the page. Stop the heckler's request, and let the act's request through but change the reply: the curtain has to be up.",
        "await Page.RouteAsync(\"**/heckler\", r => r.AbortAsync()); await Page.RouteAsync(\"**/act\", async r => { var real = await r.FetchAsync(); var act = await real.JsonAsync(); ... await r.FulfillAsync(new() { Response = real, Json = new { curtain = \"up\", word = <the real word> } }); }); then GotoAsync.",
    };

    public override string[] TypeScriptNudges { get; } =
    {
        "The page asks the server for two things as it loads: the act and a heckler. Watch what comes back, and change it before the page sees it.",
        "Set your routes up before you open the page. Stop the heckler's request, and let the act's request through but change the reply: the curtain has to be up.",
        "await page.route('**/heckler', r => r.abort()); await page.route('**/act', async r => { const response = await r.fetch(); const act = await response.json(); await r.fulfill({ response, json: { ...act, curtain: 'up' } }); }); then goto.",
    };

    public override string Slug => "illusionists-stage";
    public override string Name => "The Illusionist's Stage";
    public override string Objective =>
        "The illusionist won't perform with the curtain down, and the act the server sends always has it down. " +
        "A heckler in the back row ruins every show. Change the act on its way to the page, and silence the heckler " +
        "before he opens his mouth.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(10);

    public override RoomPuzzle Generate(Random rng) => new(new { }, Rune(rng, 8));
}

/// <summary>
/// Floor 15, the Page Object Model: four identical blueprint panels, shuffled, each to be filled in from the work
/// order. Written out longhand it's sixteen near-identical blocks; written as one component object, it's a loop.
/// </summary>
public sealed class DraftingTable : Room
{
    private static readonly string[] Names = { "Tower", "Bridge", "Archway", "Vault", "Spire", "Cellar", "Gatehouse" };
    private static readonly string[] Materials = { "Oak", "Granite", "Iron", "Marble" };

    public override string[] Nudges { get; } =
    {
        "Four panels, all the same shape. Write the steps for one panel once, and point it at each panel in turn.",
        "Make a small class that takes the panel's Locator (its region, found by name) and has one method that fills in the material, the length and the corners and stamps it.",
        "class Blueprint(ILocator root) { public async Task DraftAsync(string material, int length, bool square) { await root.GetByLabel(\"Material\").SelectOptionAsync(material); ... } } then new Blueprint(Page.GetByRole(AriaRole.Region, new() { Name = \"Blueprint: \" + name })) for every row of the work order.",
    };

    public override string[] TypeScriptNudges { get; } =
    {
        "Four panels, all the same shape. Write the steps for one panel once, and point it at each panel in turn.",
        "Make a small class that takes the panel's Locator (its region, found by name) and has one method that fills in the material, the length and the corners and stamps it.",
        "class Blueprint { constructor(private root: Locator) {} async draft(material: string, length: number, square: boolean) { await this.root.getByLabel('Material').selectOption(material); ... } } then new Blueprint(page.getByRole('region', { name: 'Blueprint: ' + name })) for every row of the work order.",
    };

    public override string Slug => "drafting-table";
    public override string Name => "The Drafting Table";
    public override string Objective =>
        "The Architect has left a work order and four blank blueprints, in no particular order. Fill in every " +
        "blueprint exactly as the work order says, and stamp each one. A wrong measurement brings the ceiling down.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(10);

    public override RoomPuzzle Generate(Random rng)
    {
        var blueprints = Names.OrderBy(_ => rng.Next()).Take(4)
            .Select(name => new { name, material = Materials[rng.Next(Materials.Length)], length = rng.Next(2, 41), square = rng.Next(2) == 0 })
            .ToArray();
        var answer = System.Text.Json.JsonSerializer.Serialize(blueprints.OrderBy(b => b.name, StringComparer.Ordinal));
        return new RoomPuzzle(new { blueprints, materials = Materials }, answer);
    }
}

/// <summary>
/// Floor 16, fixtures, setup and parallel tests: a hydra with five heads. One setup call starts the hunt. Each head
/// needs its own crawler in its own fresh browser context (a hand already at the pit can't face a second head), and
/// the heads only rear once all five crawlers are there together. If they don't gather in time, or don't all cut
/// within a few seconds, the heads grow back. Only tests running side by side get there.
/// </summary>
public sealed class SeveringPit : Room
{
    public const int Heads = 5;
    /// <summary>How long after the first cut the last head must fall.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(3);
    /// <summary>How long after the first crawler arrives the other four have to turn up.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);
    public const string ScarCookie = "pit-scar";

    public enum HuntState { Gathering, Rearing, Slain, Regrown }

    /// <summary>One hunt, shared by the five crawlers who answer it. Started by the setup call, kept in the cache.</summary>
    public sealed class Hunt
    {
        public Guid Id { get; } = Guid.NewGuid();
        public object Gate { get; } = new();
        public Dictionary<int, (Guid Attempt, DateTimeOffset At)> Faced { get; } = new();
        public Dictionary<int, (Guid Attempt, DateTimeOffset At)> Cuts { get; } = new();

        public HuntState StateAt(DateTimeOffset now)
        {
            lock (Gate)
            {
                if (Cuts.Count > 0)
                {
                    var first = Cuts.Values.Min(c => c.At);
                    if (Cuts.Count == Heads && Cuts.Values.Max(c => c.At) - first <= Window) return HuntState.Slain;
                    if (now - first > Window) return HuntState.Regrown;
                }
                if (Faced.Count == Heads) return HuntState.Rearing;
                return Faced.Count > 0 && now - Faced.Values.Min(f => f.At) > Patience ? HuntState.Regrown : HuntState.Gathering;
            }
        }

        public bool WasCutBy(Guid attempt)
        {
            lock (Gate) return Cuts.Values.Any(c => c.Attempt == attempt);
        }
    }

    public override string[] Nudges { get; } =
    {
        "One crawler can't do this: the heads only rear when all five crawlers are at the pit together, each with clean hands. Your tests need to share one hunt, and run side by side.",
        "Start the hunt once, before the tests, in a [OneTimeSetUp]. Then write one test per head (a [TestCase] for each number) and let NUnit run them in parallel: [Parallelizable(ParallelScope.All)], one instance per test case, and at least five workers.",
        "[Parallelizable(ParallelScope.All)] [FixtureLifeCycle(LifeCycle.InstancePerTestCase)] on the class, [assembly: LevelOfParallelism(5)], a static [OneTimeSetUp] that POSTs /api/rooms/severing-pit/hunts and keeps its huntId, and [TestCase(1)]...[TestCase(5)] Cut(int head): EnterAsync($\"/rooms/severing-pit?hunt={huntId}&head={head}\"), click Cut, TakeTreasureAsync().",
    };

    public override string[] TypeScriptNudges { get; } =
    {
        "One crawler can't do this: the heads only rear when all five crawlers are at the pit together, each with clean hands. Your tests need to share one hunt, and run side by side.",
        "Workers are separate processes, so a beforeAll would start five hunts. Start one hunt in a globalSetup and hand its id to the workers in an environment variable. Then run five tests in parallel, with five workers.",
        "A pit.config.ts that builds on the kit's config with globalSetup: './pit-setup.ts', workers: 5, fullyParallel: true and testMatch: '**/*.pit.ts'. The setup POSTs /api/rooms/severing-pit/hunts and sets process.env.HUNT_ID. Then for head 1 to 5: test(`head ${head}`, ...) goes to /rooms/severing-pit?hunt=...&head=..., clicks Cut and takes the treasure. Run it with npx playwright test -c pit.config.ts.",
    };

    public override string Slug => "severing-pit";
    public override string Name => "The Severing Pit";
    public override string Objective =>
        "The hydra has five heads. They only rear when a crawler stands at every one, and they only stay down if all " +
        "five fall within three seconds. Start one hunt, then send a crawler for each head, each with clean hands, all at once.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(20);

    public override RoomPuzzle Generate(Random rng) =>
        new(new
        {
            heads = Heads,
            windowMs = (int)Window.TotalMilliseconds,
            patienceMs = (int)Patience.TotalMilliseconds,
            rearMs = 300 + rng.Next(600),
            openMs = 1500,
        }, Rune(rng, 8));
}

/// <summary>
/// Floor 17, screenshots and visual comparison: six portraits, painted on the server. A few seconds in, the forger
/// rehangs all six, and one comes back with a tiny patch off color. Nothing in the page or its data says which: the
/// test has to screenshot each portrait before and after and compare. A candle flickers beside them, so whole-page
/// screenshots never match twice.
/// </summary>
public sealed class CuratorsEye : Room
{
    private static readonly string[] Sitters =
    {
        "Lady Wren", "Sir Ambrose", "The Widow Hale", "Brother Cormac", "Duchess Ines", "Captain Rook",
        "Old Tamsin", "Lord Percival", "Mistress Juno", "The Young Earl",
    };

    public const int Count = 6;

    /// <summary>What the forger did. Kept out of /reveal by a reveal time no attempt lives to see.</summary>
    public sealed record Gallery(string[] Names, int[] Seeds, int Forged, Portraits.Flaw Flaw, int StrikeMs);

    public override string[] Nudges { get; } =
    {
        "Your eyes won't find it: the forgery is a few pixels, a shade off. Something that compares images exactly will.",
        "Screenshot each portrait on its own (a locator's screenshot, not the page's: the candle never holds still), before the forger comes and after the room says he's gone. The one whose screenshot changed is the forgery.",
        "var portraits = Page.GetByRole(AriaRole.Figure); for each one: before[i] = await portraits.Nth(i).ScreenshotAsync(); wait for the status to say the forger has gone, take the after shots the same way, and accuse the figure where !before[i].SequenceEqual(after[i]).",
    };

    public override string[] TypeScriptNudges { get; } =
    {
        "Your eyes won't find it: the forgery is a few pixels, a shade off. Something that compares images exactly will.",
        "Screenshot each portrait on its own (a locator's screenshot, not the page's: the candle never holds still), before the forger comes and after the room says he's gone. The one whose screenshot changed is the forgery.",
        "const portraits = page.getByRole('figure'); before[i] = await portraits.nth(i).screenshot(); wait for the status to say the forger has gone, take the after shots the same way, and accuse the figure where !before[i].equals(after[i]).",
    };

    public override string Slug => "curators-eye";
    public override string Name => "The Curator's Eye";
    public override string Objective =>
        "Six portraits hang in the gallery. Tonight a forger will swap one for a copy so good no eye can tell. " +
        "Find it anyway, and accuse it before the curator does.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(15);

    public override RoomPuzzle Generate(Random rng)
    {
        var names = Sitters.OrderBy(_ => rng.Next()).Take(Count).ToArray();
        var seeds = Enumerable.Range(0, Count).Select(_ => rng.Next()).ToArray();
        var forged = rng.Next(Count);
        var flaw = new Portraits.Flaw(10 + rng.Next(Portraits.Width - 24), 10 + rng.Next(Portraits.Height - 24));
        var strike = 2000 + rng.Next(1000);
        return new RoomPuzzle(new { sitters = names, strikeMs = strike }, names[forged],
            Reveal: new Gallery(names, seeds, forged, flaw, strike), RevealAfter: TimeSpan.FromDays(1));
    }
}

/// <summary>
/// Floor 18, emulation and the clock: the Belfry's door opens for one traveler a night. The door names a city; the
/// test has to arrive as that traveler (a phone, the city's time zone and language, standing in the city) and be
/// there when the bell strikes midnight, local time, which is hours away unless the test takes charge of the clock.
/// The page reads the browser and the server checks every reading before the bell gives up its word.
/// </summary>
public sealed class Belfry : Room
{
    public sealed record Traveler(string City, string TimeZone, string Locale, string Language, double Latitude, double Longitude);

    public static readonly IReadOnlyList<Traveler> Travelers =
    [
        new("Tokyo", "Asia/Tokyo", "ja-JP", "Japanese", 35.6762, 139.6503),
        new("Reykjavik", "Atlantic/Reykjavik", "is-IS", "Icelandic", 64.1466, -21.9426),
        new("Buenos Aires", "America/Argentina/Buenos_Aires", "es-AR", "Spanish", -34.6037, -58.3816),
        new("Nairobi", "Africa/Nairobi", "sw-KE", "Swahili", -1.2921, 36.8219),
        new("Auckland", "Pacific/Auckland", "en-NZ", "English", -36.8485, 174.7633),
        new("Cairo", "Africa/Cairo", "ar-EG", "Arabic", 30.0444, 31.2357),
        new("Lisbon", "Europe/Lisbon", "pt-PT", "Portuguese", 38.7223, -9.1393),
    ];

    /// <summary>How far from the city center still counts as being there.</summary>
    public const double ReachKm = 30;

    /// <summary>What the page read from the browser when the bell struck.</summary>
    public sealed record Readings(string? TimeZone, string? Language, double? Latitude, double? Longitude,
        bool Touch, int Width, long Epoch);

    public override string[] Nudges { get; } =
    {
        "You can't walk to Tokyo by midnight. Playwright can: a new browser context can pretend to be a phone, in another time zone, speaking another language, standing somewhere else. And it can move time.",
        "Read tonight's traveler off the door with the ordinary Page first. Then make a new context from a phone in Playwright.Devices, plus TimezoneId, Locale, Geolocation and the geolocation permission. Before opening the room there, install its clock a little before midnight in the traveler's time zone, then fast-forward past it.",
        "var context = await Browser.NewContextAsync(new(Playwright.Devices[\"iPhone 13\"]) { TimezoneId = tz, Locale = locale, Geolocation = new() { Latitude = lat, Longitude = lng }, Permissions = new[] { \"geolocation\" } }); var page = await context.NewPageAsync(); await page.Clock.InstallAsync(new() { TimeString = \"2026-01-01T23:59:00+09:00\" }); open the room, then await page.Clock.FastForwardAsync(\"02:00\").",
    };

    public override string[] TypeScriptNudges { get; } =
    {
        "You can't walk to Tokyo by midnight. Playwright can: a new browser context can pretend to be a phone, in another time zone, speaking another language, standing somewhere else. And it can move time.",
        "Read tonight's traveler off the door with the ordinary page first. Then make a new context from a phone in devices, plus timezoneId, locale, geolocation and the geolocation permission. Before opening the room there, install its clock a little before midnight in the traveler's time zone, then fast-forward past it.",
        "const context = await browser.newContext({ ...devices['iPhone 13'], timezoneId, locale, geolocation: { latitude, longitude }, permissions: ['geolocation'] }); const page = await context.newPage(); await page.clock.install({ time: new Date('2026-01-01T23:59:00+09:00') }); open the room, then await page.clock.fastForward('02:00').",
    };

    public override string Slug => "belfry";
    public override string Name => "The Belfry";
    public override string Objective =>
        "The Belfry's door opens for one traveler a night, when the bell strikes midnight in the traveler's own city. " +
        "Arrive as tonight's traveler, and don't keep the bell waiting.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(20);

    /// <summary>One traveler a night (a UTC day), so a test can read the door first and then arrive as them.</summary>
    public static Traveler Tonight(DateTimeOffset now) => Travelers[DateOnly.FromDateTime(now.UtcDateTime).DayNumber % Travelers.Count];

    public override RoomPuzzle Generate(Random rng)
    {
        var t = Tonight(DateTimeOffset.UtcNow);
        var word = Rune(rng, 8);
        return new RoomPuzzle(new
        {
            city = t.City, timeZone = t.TimeZone, locale = t.Locale, language = t.Language,
            latitude = t.Latitude, longitude = t.Longitude, reachKm = ReachKm,
        }, word)
        {
            Trials = [new Trial(json => Check(t, json).Count == 0, new { word })],
        };
    }

    /// <summary>What's wrong with the readings, in the doorkeeper's words. Empty means the traveler may pass.</summary>
    public static List<string> Check(Traveler t, string json)
    {
        Readings? r;
        try
        {
            r = System.Text.Json.JsonSerializer.Deserialize<Readings>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        }
        catch (System.Text.Json.JsonException)
        {
            return ["The doorkeeper can't read your papers."];
        }
        if (r is null) return ["The doorkeeper can't read your papers."];

        var problems = new List<string>();
        if (!string.Equals(r.TimeZone, t.TimeZone, StringComparison.Ordinal))
            problems.Add($"Your clock keeps {r.TimeZone ?? "no"} time, not {t.City} time.");
        if (!string.Equals(r.Language, t.Locale, StringComparison.OrdinalIgnoreCase))
            problems.Add($"You speak {r.Language ?? "nothing"}, not {t.Locale}.");
        if (r.Latitude is not { } lat || r.Longitude is not { } lng || DistanceKm(lat, lng, t.Latitude, t.Longitude) > ReachKm)
            problems.Add($"You aren't standing in {t.City}.");
        if (!r.Touch || r.Width > 500)
            problems.Add("You aren't on a phone.");
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(r.Epoch), TimeZoneInfo.FindSystemTimeZoneById(t.TimeZone));
        // The bell keeps tolling for a few minutes after midnight.
        if (local.Hour != 0 || local.Minute >= 5)
            problems.Add($"It's {local:HH:mm} in {t.City}, not midnight.");
        return problems;
    }

    private static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        static double Rad(double d) => d * Math.PI / 180;
        var a = Math.Pow(Math.Sin(Rad(lat2 - lat1) / 2), 2) +
                Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(Rad(lng2 - lng1) / 2), 2);
        return 6371 * 2 * Math.Asin(Math.Sqrt(a));
    }
}

/// <summary>
/// Floor 19, the final boss: The Flake, a monster made of every reason tests flake. Three rounds in one breath. Each
/// round its Strike appears late, behind a mist that eats clicks for a while; struck, it flickers (its text is
/// re-rendered again and again), shows a feint, and only then its true name; and the box for the name is rebuilt the
/// moment the name appears. Fixed sleeps are too short one round and too slow over three; reading too early gets the
/// feint; an element handle goes stale. Auto-waiting, retrying assertions and locators win.
/// </summary>
public sealed class TheFlake : Room
{
    public const int Rounds = 3;

    /// <summary>One round's timings (milliseconds) and names. Only the true name banishes it.</summary>
    public sealed record Round(int AppearMs, int MistMs, int FlickerMs, int FeintMs, string Feint, string TrueName);

    public override string[] Nudges { get; } =
    {
        "Everything here happens late, and by a different amount each round. Don't guess how long to wait: let Playwright wait for exactly what you need.",
        "Clicks wait for the Strike to exist, be enabled and not be covered. For the name, watch the Flake's voice with an assertion that retries until the text says 'True name: ...', then read it. Find the name box fresh each time with a locator, never hold on to an element.",
        "Three times: click Strike; await Expect(Page.GetByLabel(\"The Flake's voice\")).ToHaveTextAsync(new Regex(\"^True name: \")); read the name after the colon; FillAsync it into GetByLabel(\"The true name\"); click Banish. No WaitForTimeoutAsync anywhere.",
    };

    public override string[] TypeScriptNudges { get; } =
    {
        "Everything here happens late, and by a different amount each round. Don't guess how long to wait: let Playwright wait for exactly what you need.",
        "Clicks wait for the Strike to exist, be enabled and not be covered. For the name, watch the Flake's voice with an assertion that retries until the text says 'True name: ...', then read it. Find the name box fresh each time with a locator, never hold on to an element.",
        "Three times: click Strike; await expect(page.getByLabel(\"The Flake's voice\")).toHaveText(/^True name: /); read the name after the colon; fill it into getByLabel('The true name'); click Banish. No waitForTimeout anywhere.",
    };

    public override string Slug => "the-flake";
    public override string Name => "The Flake";
    public override string Objective =>
        "The Flake passes when it feels like it and fails when you need it most. Banish it three times running: strike " +
        "it, learn its true name, and speak it. It will feint, flicker and hide. It always does.";
    public override TimeSpan TimeLimit => TimeSpan.FromSeconds(24);

    public override RoomPuzzle Generate(Random rng)
    {
        var rounds = Enumerable.Range(0, Rounds).Select(_ =>
        {
            var name = Rune(rng, 6);
            string feint;
            do feint = Rune(rng, 6); while (feint == name);
            return new Round(200 + rng.Next(700), 200 + rng.Next(700), 300 + rng.Next(400), 300 + rng.Next(500), feint, name);
        }).ToArray();

        // Each banished round hands over the next; the last name is the room's answer.
        return new RoomPuzzle(new { rounds = Rounds, first = rounds[0] }, rounds[^1].TrueName)
        {
            Trials = Enumerable.Range(0, Rounds - 1)
                .Select(i => new Trial(answer => answer == rounds[i].TrueName, new { next = rounds[i + 1] }))
                .ToList(),
        };
    }
}
