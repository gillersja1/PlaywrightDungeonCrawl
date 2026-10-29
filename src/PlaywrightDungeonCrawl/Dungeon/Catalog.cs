namespace PlaywrightDungeonCrawl.Dungeon;

/// <param name="Hint">Shown after a wrong answer: points the way without giving the answer.</param>
/// <param name="Explanation">Shown once the question is answered correctly.</param>
public sealed record QuizQuestion(string Prompt, string[] Options, int CorrectIndex, string Explanation, string Hint);

public sealed record Floor(int Number, string Slug, string Title, string Skill, string Tagline, Room Room, QuizQuestion[] Riddles);

/// <summary>
/// XP for each kind of progress, and the rank titles they unlock. The
/// browser keeps the learner's progress; this is the single source of truth
/// for what it's worth.
/// </summary>
public static class Rewards
{
    public const int Scroll = 10;
    public const int Riddle = 25;
    public const int Treasure = 50;

    /// <summary>XP taken off your score for opening a riddle hint.</summary>
    public const int HintCost = 5;

    /// <summary>XP taken off for each room nudge, in order: help gets dearer the more you take.</summary>
    public static readonly int[] NudgeCosts = { 5, 10, 15 };

    /// <summary>Help is free on floor 1: nobody should lose XP for learning the setup.</summary>
    public static int HelpCost(int floorNumber, int cost) => floorNumber == 1 ? 0 : cost;

    public static int NudgeCost(int floorNumber, int nudgeIndex) =>
        HelpCost(floorNumber, NudgeCosts[Math.Min(nudgeIndex, NudgeCosts.Length - 1)]);

    /// <summary>Ranks unlock at a share of the XP available, so they scale as floors are added.</summary>
    public static readonly (double Share, string Title)[] Ranks =
    {
        (0, "Novice Delver"),
        (0.15, "Torchbearer"),
        (0.4, "Rune Reader"),
        (0.7, "Vault Breaker"),
        (1.0, "Dungeon Master"),
    };

    public static IEnumerable<(int MinXp, string Title)> RanksFor(int maxXp) =>
        Ranks.Select(r => ((int)Math.Round(r.Share * maxXp), r.Title));
}

public sealed class DungeonCatalog
{
    public IReadOnlyList<Floor> Floors { get; }

    public DungeonCatalog(IEnumerable<Room> rooms)
    {
        var bySlug = rooms.ToDictionary(r => r.Slug);
        Floors = new[]
        {
            new Floor(1, "setup", "The Threshold", "Setting up",
                "Before you can crawl, you need a crawler: Visual Studio, the starter kit, and your first test run.",
                bySlug["welcome-mat"],
                new[]
                {
                    new QuizQuestion(
                        "You click Run All in Test Explorer and a browser opens by itself. What opened it?",
                        new[]
                        {
                            "Visual Studio's built-in browser preview",
                            "Your test: classes based on PageTest launch a fresh browser for every test",
                            "Windows, because the test touched a website",
                            "The dungeon website",
                        },
                        1,
                        "PageTest (from Microsoft.Playwright.NUnit) starts a browser, gives each test a new page, and closes it afterwards.",
                        "Look at the class your tests inherit from. What does its name suggest it gives you?"),
                    new QuizQuestion(
                        "Where does the starter kit keep the dungeon's address?",
                        new[]
                        {
                            "Hard-coded in every test",
                            "In Windows environment settings",
                            "In dungeon.runsettings, as the DungeonUrl parameter",
                            "In the browser's bookmarks",
                        },
                        2,
                        "One setting for the whole project, so pointing every test at a different copy of the dungeon is a one-line change.",
                        "It's a file in the starter kit you opened during setup, with \"settings\" in its name."),
                    new QuizQuestion(
                        "Your test printed a flag. Where do you find it in Visual Studio?",
                        new[]
                        {
                            "In the Error List window",
                            "In Test Explorer: select the test, then open its additional output in the details pane",
                            "In the browser's console",
                            "It's emailed to you",
                        },
                        1,
                        "Anything written with TestContext.Out.WriteLine is stored with the test result and shown in its details.",
                        "Output isn't an error, and it isn't in the browser. It's saved with the test result itself."),
                }),
            new Floor(2, "locators", "The Entrance", "Locators",
                "Every door looks alike. Learn to find exactly the one you mean.",
                bySlug["hall-of-doors"],
                new[]
                {
                    new QuizQuestion(
                        "Which locator should you reach for first?",
                        new[]
                        {
                            "Page.Locator(\"xpath=//div[3]/button\")",
                            "Page.Locator(\".btn-primary\")",
                            "Page.GetByRole(AriaRole.Button, new() { Name = \"Enter\" })",
                            "Page.QuerySelectorAsync(\"button\")",
                        },
                        2,
                        "Role locators find elements the way users and screen readers do, so they survive markup and styling changes.",
                        "Which option describes the element the way a user or a screen reader would, rather than how the page is built?"),
                    new QuizQuestion(
                        "A locator matches two buttons and you call ClickAsync(). What happens?",
                        new[]
                        {
                            "It clicks the first match",
                            "It clicks both",
                            "It throws a strict mode violation",
                            "It waits until only one matches",
                        },
                        2,
                        "Locators are strict: an action on a locator that matches more than one element fails instead of guessing.",
                        "Playwright locators are strict. What would a strict tool do when it can't tell which one you meant?"),
                    new QuizQuestion(
                        "Two buttons are named \"Ember Gate\", and one is disabled. How do you target the enabled one?",
                        new[]
                        {
                            "Page.GetByRole(AriaRole.Button, new() { Name = \"Ember Gate\", Disabled = false })",
                            "Page.GetByText(\"Ember Gate\").Last",
                            "Page.GetByRole(AriaRole.Button).Nth(1)",
                            "Page.Locator(\"button\").First",
                        },
                        0,
                        "Filter on the state you care about. Position-based picks like First, Last and Nth break as soon as the order changes.",
                        "Avoid answers that depend on order. Which one describes the state of the door you want?"),
                }),
            new Floor(3, "waiting", "The Torchlit Stair", "Auto-waiting",
                "Things appear when they're ready, not when you are. Learn to wait for state, not time.",
                bySlug["flickering-torch"],
                new[]
                {
                    new QuizQuestion(
                        "Why avoid await Task.Delay(3000) before checking an element?",
                        new[]
                        {
                            "It isn't allowed in NUnit",
                            "It's too slow when the element is fast and too short when it's slow",
                            "It disables Playwright's tracing",
                            "It blocks other tests from running",
                        },
                        1,
                        "A fixed sleep is both wasted time and a source of flakiness. Wait for the condition itself.",
                        "Picture a page that loads in 1 second one day and 4 seconds the next."),
                    new QuizQuestion(
                        "What does await Expect(locator).ToBeVisibleAsync() do if the element isn't visible yet?",
                        new[]
                        {
                            "Fails immediately",
                            "Retries until it's visible or the timeout passes",
                            "Scrolls the page until it appears",
                            "Returns false",
                        },
                        1,
                        "Web-first assertions retry until they pass or time out (5 seconds by default).",
                        "These assertions are called web-first because they behave like a patient user."),
                    new QuizQuestion(
                        "An element shows for half a second, then is removed. How do you read its text?",
                        new[]
                        {
                            "Take screenshots in a loop",
                            "await Task.Delay, then call TextContentAsync",
                            "Call WaitForAsync() on the locator, then TextContentAsync() straight away",
                            "Reload the page until it stays",
                        },
                        2,
                        "WaitForAsync returns the moment the element is attached and visible, leaving time to read it.",
                        "You need something that returns the moment the element shows up, not after a guess."),
                }),
            new Floor(4, "forms", "The Scribe's Hall", "Forms and input",
                "Text boxes, dropdowns, radio buttons, checkboxes and dates. Fill them like a user would.",
                bySlug["scribes-ledger"],
                new[]
                {
                    new QuizQuestion(
                        "How do you fill the text box labelled \"Adventurer name\"?",
                        new[]
                        {
                            "Page.GetByLabel(\"Adventurer name\").FillAsync(name)",
                            "Page.Locator(\"input\").First.TypeAsync(name)",
                            "Page.Keyboard.TypeAsync(name)",
                            "Page.EvalOnSelectorAsync(\"input\", \"e => e.value = name\")",
                        },
                        0,
                        "GetByLabel uses the label a user reads, and FillAsync clears the field and fires the right input events.",
                        "How would a user find this text box? By the words written next to it."),
                    new QuizQuestion(
                        "How do you pick \"Cartographers\" from a <select>?",
                        new[]
                        {
                            "ClickAsync() on the select, then ClickAsync() on the option",
                            "SelectOptionAsync(\"Cartographers\")",
                            "FillAsync(\"Cartographers\")",
                            "PressAsync(\"C\")",
                        },
                        1,
                        "SelectOptionAsync selects by value or label in one step, without opening the native dropdown.",
                        "A <select> is a list of options. Which method's name says \"select an option\"?"),
                    new QuizQuestion(
                        "Why prefer CheckAsync() over ClickAsync() for a checkbox?",
                        new[]
                        {
                            "It's faster",
                            "ClickAsync() doesn't work on checkboxes",
                            "CheckAsync() makes sure it ends up checked, while a click toggles whatever state it's in",
                            "CheckAsync() skips validation",
                        },
                        2,
                        "CheckAsync is idempotent and verifies the result, so it can't accidentally uncheck a box.",
                        "What happens if you click a checkbox that's already ticked?"),
                }),
            new Floor(5, "frames", "The Hall of Mirrors", "Frames, shadow DOM and dialogs",
                "Content hidden inside other documents, components and pop-ups.",
                bySlug["mirror-chamber"],
                new[]
                {
                    new QuizQuestion(
                        "How do you click a button inside <iframe id=\"mirror\">?",
                        new[]
                        {
                            "Page.GetByRole(AriaRole.Button).ClickAsync() finds it automatically",
                            "Page.FrameLocator(\"#mirror\").GetByRole(AriaRole.Button).ClickAsync()",
                            "Switch the driver to the frame first",
                            "It can't be done in Playwright",
                        },
                        1,
                        "Page-level locators don't search inside frames; FrameLocator scopes a locator to the frame's document.",
                        "An iframe is a separate document. You have to tell Playwright to look inside that frame."),
                    new QuizQuestion(
                        "The button is inside an open shadow root. What do you need to do?",
                        new[]
                        {
                            "Use a special >>> selector",
                            "Run JavaScript to read shadowRoot",
                            "Nothing: Playwright's locators pierce open shadow roots",
                            "Switch to the shadow root like a frame",
                        },
                        2,
                        "CSS, text and role locators look inside open shadow roots by default (XPath doesn't).",
                        "Playwright was built for modern web components. Do you think it needs help with open shadow roots?"),
                    new QuizQuestion(
                        "A click opens a confirm() dialog and your test has no dialog handler. What happens?",
                        new[]
                        {
                            "The test hangs forever",
                            "Playwright dismisses it, so confirm() returns false",
                            "Playwright accepts it",
                            "The test fails with a dialog error",
                        },
                        1,
                        "Dialogs are dismissed by default. To accept, register Page.Dialog += (_, d) => d.AcceptAsync() before the click.",
                        "Nobody is there to click OK, so Playwright picks the safe option for you."),
                }),
            new Floor(6, "network", "The Deep Vault", "Network",
                "Some secrets are never shown on screen. Learn to listen to the wire.",
                bySlug["whispering-vault"],
                new[]
                {
                    new QuizQuestion(
                        "How do you capture a response that the page requests while it loads?",
                        new[]
                        {
                            "GotoAsync(url), then WaitForResponseAsync(...)",
                            "RunAndWaitForResponseAsync(() => Page.GotoAsync(url), r => r.Url.Contains(\"/reveal\"))",
                            "Read it from Page.ContentAsync()",
                            "Use Page.Context.Cookies",
                        },
                        1,
                        "Start listening before the action that triggers the request; otherwise it can finish before you begin waiting.",
                        "If you navigate first and start listening second, could the response already be gone?"),
                    new QuizQuestion(
                        "What is Page.RouteAsync() for?",
                        new[]
                        {
                            "Navigating between pages",
                            "Intercepting requests to mock, modify or block them",
                            "Defining ASP.NET routes",
                            "Recording a HAR file",
                        },
                        1,
                        "Routing lets a test control the network: return fake data, simulate errors, or block third parties.",
                        "Route here means the path a request takes, and stepping into it."),
                    new QuizQuestion(
                        "A response body is JSON. How do you read a field from it?",
                        new[]
                        {
                            "var json = await response.JsonAsync(); json?.GetProperty(\"field\")",
                            "response.Headers[\"field\"]",
                            "await response.TextAsync() always returns a parsed object",
                            "response.Status",
                        },
                        0,
                        "JsonAsync() parses the body into a JsonElement you can navigate with GetProperty.",
                        "The response object has a method that parses the body for you."),
                }),
        };
    }

    /// <summary>The deeper floors, shown on the map as sealed until they're built.</summary>
    public IReadOnlyList<(int Number, string Skill)> SealedFloors { get; } = new[]
    {
        (7, "Tabs, pop-ups and navigation"),
        (8, "Mouse and keyboard"),
        (9, "File upload and download"),
        (10, "Login state and browser contexts"),
        (11, "API testing"),
        (12, "Mocking responses"),
        (13, "Page Object Model"),
        (14, "Fixtures, setup and parallel tests"),
        (15, "Tracing and debugging"),
        (16, "Screenshots and visual comparison"),
        (17, "Emulation and the clock"),
        (18, "The final boss: a flaky-test hunt"),
    };

    public Floor? Find(string slug) => Floors.FirstOrDefault(f => f.Slug == slug);

    public Floor? FindByRoom(string roomSlug) => Floors.FirstOrDefault(f => f.Room.Slug == roomSlug);

    public static int MaxXp(int floorCount) => floorCount * (Rewards.Scroll + Rewards.Riddle + Rewards.Treasure);
}
