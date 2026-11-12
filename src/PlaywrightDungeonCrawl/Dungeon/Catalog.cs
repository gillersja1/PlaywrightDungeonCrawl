namespace PlaywrightDungeonCrawl.Dungeon;

/// <param name="Hint">Shown after a wrong answer: points the way without giving the answer.</param>
/// <param name="Explanation">Shown once the question is answered correctly.</param>
public sealed record QuizQuestion(string Prompt, string[] Options, int CorrectIndex, string Explanation, string Hint);

public sealed record Floor(int Number, string Slug, string Title, string Skill, string Tagline, Room Room, QuizQuestion[] Riddles)
{
    /// <summary>The riddles in TypeScript, once this floor has been written for it.</summary>
    public QuizQuestion[]? TypeScriptRiddles { get; init; }

    /// <summary>Whether this floor's scroll, riddles and room are taught in TypeScript yet.</summary>
    public bool HasTypeScript => TypeScriptRiddles is not null;

    public QuizQuestion[] RiddlesIn(CodeLanguage language) =>
        language == CodeLanguage.TypeScript && TypeScriptRiddles is { } ts ? ts : Riddles;
}

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
                bySlug["welcome-matt"],
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
                })
            {
                TypeScriptRiddles = new[]
                {
                    new QuizQuestion(
                        "You click the run button next to a test in VS Code and a browser opens by itself. What opened it?",
                        new[]
                        {
                            "VS Code's built-in browser preview",
                            "Your test: Playwright Test starts a browser and gives every test a fresh page",
                            "Windows, because the test touched a website",
                            "The dungeon website",
                        },
                        1,
                        "Playwright Test launches the browser, hands each test a new page (the page fixture), and closes it afterwards.",
                        "Look at what your test function receives: async ({ page, dungeon }) => ... Where did that page come from?"),
                    new QuizQuestion(
                        "Where does the starter kit keep the dungeon's address?",
                        new[]
                        {
                            "Hard-coded in every test",
                            "In Windows environment settings",
                            "In playwright.config.ts, as use.baseURL",
                            "In the browser's bookmarks",
                        },
                        2,
                        "One setting for the whole project. With a baseURL set, dungeon.enter('/rooms/...') and page.goto('/...') only need the path.",
                        "It's a file in the starter kit with \"config\" in its name."),
                    new QuizQuestion(
                        "Your test printed a flag. Where do you find it in VS Code?",
                        new[]
                        {
                            "In the Problems panel",
                            "In the Test Results panel, which shows what the test printed",
                            "In the browser's console",
                            "It's emailed to you",
                        },
                        1,
                        "Anything written with console.log in a test shows in the Test Results panel, or in the terminal if you ran npx playwright test.",
                        "Output isn't a problem, and it isn't in the browser. It's shown with the results of the run."),
                },
            },
            new Floor(2, "locators", "The Hall of Doors", "Locators",
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
                })
            {
                TypeScriptRiddles = new[]
                {
                    new QuizQuestion(
                        "Which locator should you reach for first?",
                        new[]
                        {
                            "page.locator('xpath=//div[3]/button')",
                            "page.locator('.btn-primary')",
                            "page.getByRole('button', { name: 'Enter' })",
                            "page.$('button')",
                        },
                        2,
                        "Role locators find elements the way users and screen readers do, so they survive markup and styling changes.",
                        "Which option describes the element the way a user or a screen reader would, rather than how the page is built?"),
                    new QuizQuestion(
                        "A locator matches two buttons and you call click(). What happens?",
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
                            "page.getByRole('button', { name: 'Ember Gate', disabled: false })",
                            "page.getByText('Ember Gate').last()",
                            "page.getByRole('button').nth(1)",
                            "page.locator('button').first()",
                        },
                        0,
                        "Filter on the state you care about. Position-based picks like first(), last() and nth() break as soon as the order changes.",
                        "Avoid answers that depend on order. Which one describes the state of the door you want?"),
                },
            },
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
                })
            {
                TypeScriptRiddles = new[]
                {
                    new QuizQuestion(
                        "Why avoid await page.waitForTimeout(3000) before checking an element?",
                        new[]
                        {
                            "It isn't allowed in Playwright Test",
                            "It's too slow when the element is fast and too short when it's slow",
                            "It disables tracing",
                            "It blocks other tests from running",
                        },
                        1,
                        "A fixed sleep is both wasted time and a source of flakiness. Wait for the condition itself.",
                        "Picture a page that loads in 1 second one day and 4 seconds the next."),
                    new QuizQuestion(
                        "What does await expect(locator).toBeVisible() do if the element isn't visible yet?",
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
                            "await page.waitForTimeout(...), then call textContent()",
                            "Call waitFor() on the locator, then textContent() straight away",
                            "Reload the page until it stays",
                        },
                        2,
                        "waitFor() returns the moment the element is attached and visible, leaving time to read it.",
                        "You need something that returns the moment the element shows up, not after a guess."),
                },
            },
            new Floor(4, "forms", "The Bureaucracy", "Forms and input",
                "Text boxes, dropdowns, radio buttons, checkboxes and dates. Fill them like a user would.",
                bySlug["scribes-ledger"],
                new[]
                {
                    new QuizQuestion(
                        "How do you fill the text box labeled \"Adventurer name\"?",
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
                })
            {
                TypeScriptRiddles = new[]
                {
                    new QuizQuestion(
                        "How do you fill the text box labeled \"Adventurer name\"?",
                        new[]
                        {
                            "page.getByLabel('Adventurer name').fill(name)",
                            "page.locator('input').first().pressSequentially(name)",
                            "page.keyboard.type(name)",
                            "page.$eval('input', e => e.value = name)",
                        },
                        0,
                        "getByLabel uses the label a user reads, and fill() clears the field and fires the right input events.",
                        "How would a user find this text box? By the words written next to it."),
                    new QuizQuestion(
                        "How do you pick \"Cartographers\" from a <select>?",
                        new[]
                        {
                            "click() on the select, then click() on the option",
                            "selectOption('Cartographers')",
                            "fill('Cartographers')",
                            "press('C')",
                        },
                        1,
                        "selectOption selects by value or label in one step, without opening the native dropdown.",
                        "A <select> is a list of options. Which method's name says \"select an option\"?"),
                    new QuizQuestion(
                        "Why prefer check() over click() for a checkbox?",
                        new[]
                        {
                            "It's faster",
                            "click() doesn't work on checkboxes",
                            "check() makes sure it ends up checked, while a click toggles whatever state it's in",
                            "check() skips validation",
                        },
                        2,
                        "check() is idempotent and verifies the result, so it can't accidentally uncheck a box.",
                        "What happens if you click a checkbox that's already ticked?"),
                },
            },
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
                })
            {
                TypeScriptRiddles = new[]
                {
                    new QuizQuestion(
                        "How do you click a button inside <iframe id=\"mirror\">?",
                        new[]
                        {
                            "page.getByRole('button').click() finds it automatically",
                            "page.frameLocator('#mirror').getByRole('button').click()",
                            "Switch the driver to the frame first",
                            "It can't be done in Playwright",
                        },
                        1,
                        "Page-level locators don't search inside frames; frameLocator scopes a locator to the frame's document.",
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
                        "Dialogs are dismissed by default. To accept, register page.on('dialog', d => d.accept()) before the click.",
                        "Nobody is there to click OK, so Playwright picks the safe option for you."),
                },
            },
            new Floor(6, "network", "The Whispering Vaults", "Network",
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
                })
            {
                TypeScriptRiddles = new[]
                {
                    new QuizQuestion(
                        "How do you capture a response that the page requests while it loads?",
                        new[]
                        {
                            "await page.goto(url), then await page.waitForResponse(...)",
                            "const r = page.waitForResponse(r => r.url().includes('/reveal')); await page.goto(url); await r;",
                            "Read it from await page.content()",
                            "Use page.context().cookies()",
                        },
                        1,
                        "Start waiting before the action that triggers the request; otherwise it can finish before you begin waiting.",
                        "If you navigate first and start listening second, could the response already be gone?"),
                    new QuizQuestion(
                        "What is page.route() for?",
                        new[]
                        {
                            "Navigating between pages",
                            "Intercepting requests to mock, modify or block them",
                            "Defining server routes",
                            "Recording a HAR file",
                        },
                        1,
                        "Routing lets a test control the network: return fake data, simulate errors, or block third parties.",
                        "Route here means the path a request takes, and stepping into it."),
                    new QuizQuestion(
                        "A response body is JSON. How do you read a field from it?",
                        new[]
                        {
                            "const json = await response.json(); json.field",
                            "response.headers()['field']",
                            "response.text() always returns a parsed object",
                            "response.status()",
                        },
                        0,
                        "json() parses the body into a plain object you can read with dot notation.",
                        "The response object has a method that parses the body for you."),
                },
            },
            new Floor(7, "gauntlet", "The Warden's Keep", "Boss: every skill at once",
                "The first boss. Five traps in a row, one for every floor above, and one test to beat them all.",
                bySlug["wardens-gauntlet"],
                new[]
                {
                    new QuizQuestion(
                        "Trial 4 asks \"are you sure?\" with a confirm dialog. Where does the line Page.Dialog += (_, d) => d.AcceptAsync(); go?",
                        new[]
                        {
                            "Right after the click that opens the dialog",
                            "Anywhere before that click; the top of the test is simplest",
                            "In a separate test that runs first",
                            "Nowhere: Playwright accepts dialogs by default",
                        },
                        1,
                        "A handler only catches dialogs that open after it's registered, so set it up before the action. At the top of the test, it can't be forgotten.",
                        "Floor 5 told you what Playwright does with a dialog nobody's listening for. When does the listening have to start?"),
                    new QuizQuestion(
                        "The true name is only in the response to the request your click sends. Which line catches it?",
                        new[]
                        {
                            "await speak.ClickAsync(); var r = await Page.WaitForResponseAsync(r => r.Url.Contains(\"/trials\"));",
                            "var r = await Page.RunAndWaitForResponseAsync(() => speak.ClickAsync(), r => r.Url.Contains(\"/trials\"));",
                            "await speak.ClickAsync(); var text = await Page.ContentAsync();",
                            "await Page.ReloadAsync(); var r = await Page.WaitForResponseAsync(\"**/trials\");",
                        },
                        1,
                        "Wrapping the click means you're already listening when the request goes out, however fast it comes back.",
                        "Floor 6's tip: start listening before the action, not after."),
                    new QuizQuestion(
                        "Your gauntlet test fails at trial 3, and the clock is too short to step through it in the Inspector. What's the best way to see what happened?",
                        new[]
                        {
                            "Add Task.Delay calls so you can watch",
                            "Record a trace and open it in the Trace Viewer",
                            "Turn on SlowMo",
                            "Run it again and hope",
                        },
                        1,
                        "A trace records every action, a DOM snapshot before and after it, the network and the console, so you can replay a full-speed run step by step.",
                        "You can't slow the room down. Can you record the run and watch it afterwards?"),
                })
            {
                TypeScriptRiddles = new[]
                {
                    new QuizQuestion(
                        "Trial 4 asks \"are you sure?\" with a confirm dialog. Where does page.on('dialog', d => d.accept()) go?",
                        new[]
                        {
                            "Right after the click that opens the dialog",
                            "Anywhere before that click; the top of the test is simplest",
                            "In a separate test that runs first",
                            "Nowhere: Playwright accepts dialogs by default",
                        },
                        1,
                        "A handler only catches dialogs that open after it's registered, so set it up before the action. At the top of the test, it can't be forgotten.",
                        "Floor 5 told you what Playwright does with a dialog nobody's listening for. When does the listening have to start?"),
                    new QuizQuestion(
                        "The true name is only in the response to the request your click sends. Which code catches it?",
                        new[]
                        {
                            "await speak.click(); const r = await page.waitForResponse(r => r.url().includes('/trials'));",
                            "const r = page.waitForResponse(r => r.url().includes('/trials')); await speak.click(); await r;",
                            "await speak.click(); const text = await page.content();",
                            "await page.reload(); await page.waitForResponse('**/trials');",
                        },
                        1,
                        "Starting the wait before the click means you're already listening when the request goes out, however fast it comes back.",
                        "Floor 6's tip: start listening before the action, not after."),
                    new QuizQuestion(
                        "Your gauntlet test fails at trial 3, and the clock is too short to step through it. What's the best way to see what happened?",
                        new[]
                        {
                            "Add page.waitForTimeout calls so you can watch",
                            "Open its trace (npx playwright show-report) and step through the recording",
                            "Turn on slowMo",
                            "Run it again and hope",
                        },
                        1,
                        "A trace records every action, a DOM snapshot before and after it, the network and the console, so you can replay a full-speed run step by step.",
                        "You can't slow the room down. Can you record the run and watch it afterwards?"),
                },
            },
            new Floor(8, "portals", "The Portal Bazaar", "Tabs, pop-ups and navigation",
                "Every door here opens somewhere new. Learn to follow a link into another tab, and find your way back.",
                bySlug["portal-bazaar"],
                new[]
                {
                    new QuizQuestion(
                        "Clicking a link opens a new tab. How does your test get hold of it?",
                        new[]
                        {
                            "Page switches to the new tab by itself",
                            "var popup = await Page.RunAndWaitForPopupAsync(() => link.ClickAsync());",
                            "await Page.Context.NewPageAsync()",
                            "await Page.GoForwardAsync()",
                        },
                        1,
                        "The new tab is a separate IPage. Wrapping the click means you're listening before it opens, so you can't miss it.",
                        "Page keeps pointing at the tab it started in. What would tell you a new one appeared?"),
                    new QuizQuestion(
                        "A page sends you on to another address a moment after it loads. How do you wait until you've arrived?",
                        new[]
                        {
                            "await Task.Delay(2000)",
                            "Read Page.Url straight away",
                            "await Page.WaitForURLAsync(\"**/stall\")",
                            "await Page.ReloadAsync()",
                        },
                        2,
                        "WaitForURLAsync waits for the address to match a glob, a regex or a predicate, however long the hop takes, and no longer.",
                        "A fixed sleep is either too long or too short. Is there something that waits for the address itself?"),
                    new QuizQuestion(
                        "Your test has opened three tabs. Where can it find all of them?",
                        new[]
                        {
                            "Page.Context.Pages",
                            "Page.Frames",
                            "Browser.Tabs",
                            "Page.Popups",
                        },
                        0,
                        "Tabs live in a browser context. Its Pages list holds every one, and new ones arrive through its Page event.",
                        "Frames are inside a page. What's one level up from a page?"),
                })
            {
                TypeScriptRiddles = new[]
                {
                    new QuizQuestion(
                        "Clicking a link opens a new tab. How does your test get hold of it?",
                        new[]
                        {
                            "page switches to the new tab by itself",
                            "const popup = page.waitForEvent('popup'); await link.click(); const tab = await popup;",
                            "await page.context().newPage()",
                            "await page.goForward()",
                        },
                        1,
                        "The new tab is a separate Page. Starting the wait before the click means you're listening before it opens, so you can't miss it.",
                        "page keeps pointing at the tab it started in. What would tell you a new one appeared?"),
                    new QuizQuestion(
                        "A page sends you on to another address a moment after it loads. How do you wait until you've arrived?",
                        new[]
                        {
                            "await page.waitForTimeout(2000)",
                            "Read page.url() straight away",
                            "await page.waitForURL('**/stall')",
                            "await page.reload()",
                        },
                        2,
                        "waitForURL waits for the address to match a glob, a regex or a predicate, however long the hop takes, and no longer.",
                        "A fixed sleep is either too long or too short. Is there something that waits for the address itself?"),
                    new QuizQuestion(
                        "Your test has opened three tabs. Where can it find all of them?",
                        new[]
                        {
                            "page.context().pages()",
                            "page.frames()",
                            "browser.tabs()",
                            "page.popups()",
                        },
                        0,
                        "Tabs live in a browser context. pages() lists every one, and new ones arrive through its 'page' event.",
                        "Frames are inside a page. What's one level up from a page?"),
                },
            },
            new Floor(9, "clockwork", "The Clockwork Foundry", "Mouse and keyboard",
                "Nothing in the foundry turns on a plain click. Learn the rest of the mouse: double-clicks, the right button, and dragging to an exact spot.",
                bySlug["tinkerers-workbench"],
                new[]
                {
                    new QuizQuestion(
                        "A bolt only turns on a double-click. Which line turns it?",
                        new[]
                        {
                            "await bolt.ClickAsync(); await bolt.ClickAsync();",
                            "await bolt.DblClickAsync();",
                            "await bolt.PressAsync(\"Enter\");",
                            "await bolt.HoverAsync();",
                        },
                        1,
                        "DblClickAsync sends one double-click, so the page gets its dblclick event. Two separate clicks are two single clicks, and no dblclick ever fires.",
                        "The page listens for one particular event. Do two clicks in a row send it?"),
                    new QuizQuestion(
                        "The options you need are on an element's right-click menu. How do you open it?",
                        new[]
                        {
                            "await gears.RightClickAsync();",
                            "await gears.PressAsync(\"ContextMenu\");",
                            "await gears.ClickAsync(new() { Button = MouseButton.Right });",
                            "await gears.ClickAsync(new() { Modifiers = new[] { KeyboardModifier.Alt } });",
                        },
                        2,
                        "Clicks take options: Button picks the mouse button, ClickCount, Modifiers and Position do the rest. A right click fires the page's contextmenu event.",
                        "There's no separate method for it. What could you tell ClickAsync about the click?"),
                    new QuizQuestion(
                        "A slider has no input box and ignores the keyboard. How do you set it to 40% of the way along?",
                        new[]
                        {
                            "await slider.FillAsync(\"40\");",
                            "Get its BoundingBoxAsync(), then Page.Mouse: MoveAsync to the handle, DownAsync, MoveAsync to box.X + box.Width * 0.4, UpAsync",
                            "await slider.ScrollIntoViewIfNeededAsync();",
                            "await slider.SelectOptionAsync(\"40%\");",
                        },
                        1,
                        "Page.Mouse moves a real pointer, and BoundingBoxAsync tells you where the element is, so you can work out exactly where to let go. (HoverAsync with a Position does the moving more safely: it waits until nothing is covering the spot.)",
                        "Fill and SelectOption need form fields. What would a person do with their hand?"),
                })
            {
                TypeScriptRiddles = new[]
                {
                    new QuizQuestion(
                        "A bolt only turns on a double-click. Which line turns it?",
                        new[]
                        {
                            "await bolt.click(); await bolt.click();",
                            "await bolt.dblclick();",
                            "await bolt.press('Enter');",
                            "await bolt.hover();",
                        },
                        1,
                        "dblclick() sends one double-click, so the page gets its dblclick event. Two separate clicks are two single clicks, and no dblclick ever fires.",
                        "The page listens for one particular event. Do two clicks in a row send it?"),
                    new QuizQuestion(
                        "The options you need are on an element's right-click menu. How do you open it?",
                        new[]
                        {
                            "await gears.rightClick();",
                            "await gears.press('ContextMenu');",
                            "await gears.click({ button: 'right' });",
                            "await gears.click({ modifiers: ['Alt'] });",
                        },
                        2,
                        "Clicks take options: button picks the mouse button, clickCount, modifiers and position do the rest. A right click fires the page's contextmenu event.",
                        "There's no separate method for it. What could you tell click() about the click?"),
                    new QuizQuestion(
                        "A slider has no input box and ignores the keyboard. How do you set it to 40% of the way along?",
                        new[]
                        {
                            "await slider.fill('40');",
                            "Get its boundingBox(), then page.mouse: move() to the handle, down(), move() to box.x + box.width * 0.4, up()",
                            "await slider.scrollIntoViewIfNeeded();",
                            "await slider.selectOption('40%');",
                        },
                        1,
                        "page.mouse moves a real pointer, and boundingBox() tells you where the element is, so you can work out exactly where to let go. (hover() with a position does the moving more safely: it waits until nothing is covering the spot.)",
                        "fill() and selectOption() need form fields. What would a person do with their hand?"),
                },
            },
            new Floor(10, "couriers", "The Courier Guild", "File upload and download",
                "The Guild trusts paper, not screens. Learn to catch a download and read it, and to hand files back without touching your disk.",
                bySlug["couriers-counter"],
                new[]
                {
                    new QuizQuestion(
                        "Clicking a link starts a download. How do you get hold of the file?",
                        new[]
                        {
                            "await link.ClickAsync(); then look in your Downloads folder",
                            "var download = await Page.RunAndWaitForDownloadAsync(() => link.ClickAsync());",
                            "await Page.WaitForURLAsync(\"**/*.txt\");",
                            "await link.GetAttributeAsync(\"download\");",
                        },
                        1,
                        "Start waiting before the click, so the download can't slip past. The Download it hands back has the file's SuggestedFilename and, from PathAsync, where it was saved.",
                        "A download is an event. When do you have to start listening for an event?"),
                    new QuizQuestion(
                        "You need to upload a file that doesn't exist on disk. What do you pass to SetInputFilesAsync?",
                        new[]
                        {
                            "A FilePayload with a Name, a MimeType and a Buffer of bytes",
                            "The text itself, as a string",
                            "A data: URL",
                            "Nothing: you have to save a file first",
                        },
                        0,
                        "A FilePayload is a file made in memory. The page sees its Name as the file name, so you control that too.",
                        "The page needs a name and some bytes. Does it care where they came from?"),
                    new QuizQuestion(
                        "The upload button opens the system file picker, and the input behind it is hidden. What do you do?",
                        new[]
                        {
                            "Click the button, then type the path into the picker",
                            "Wait for the FileChooser while you click the button, then SetFilesAsync on it",
                            "Make the input visible with EvaluateAsync first",
                            "Uploads with a hidden input can't be tested",
                        },
                        1,
                        "RunAndWaitForFileChooserAsync catches the picker before it opens, and FileChooser.SetFilesAsync answers it. SetInputFilesAsync on the hidden input works too, without the click.",
                        "Playwright never sees the operating system's picker. What could it catch instead?"),
                })
            {
                TypeScriptRiddles = new[]
                {
                    new QuizQuestion(
                        "Clicking a link starts a download. How do you get hold of the file?",
                        new[]
                        {
                            "await link.click(); then look in your Downloads folder",
                            "const downloadPromise = page.waitForEvent('download'); await link.click(); const download = await downloadPromise;",
                            "await page.waitForURL('**/*.txt');",
                            "await link.getAttribute('download');",
                        },
                        1,
                        "Start waiting before the click, so the download can't slip past. The Download it resolves to has suggestedFilename() and, from path(), where it was saved.",
                        "A download is an event. When do you have to start listening for an event?"),
                    new QuizQuestion(
                        "You need to upload a file that doesn't exist on disk. What do you pass to setInputFiles()?",
                        new[]
                        {
                            "An object with a name, a mimeType and a buffer",
                            "The text itself, as a string",
                            "A data: URL",
                            "Nothing: you have to save a file first",
                        },
                        0,
                        "An object with name, mimeType and buffer is a file made in memory. The page sees its name as the file name, so you control that too.",
                        "The page needs a name and some bytes. Does it care where they came from?"),
                    new QuizQuestion(
                        "The upload button opens the system file picker, and the input behind it is hidden. What do you do?",
                        new[]
                        {
                            "Click the button, then type the path into the picker",
                            "Start page.waitForEvent('filechooser') before clicking the button, then setFiles() on it",
                            "Make the input visible with evaluate() first",
                            "Uploads with a hidden input can't be tested",
                        },
                        1,
                        "waitForEvent('filechooser') catches the picker before it opens, and fileChooser.setFiles() answers it. setInputFiles() on the hidden input works too, without the click.",
                        "Playwright never sees the operating system's picker. What could it catch instead?"),
                },
            },
            new Floor(11, "masquerade", "The Masquerade", "Login state and browser contexts",
                "Everyone at the ball wears a mask, and nobody can wear two. Learn to save a login and reuse it, and to put two signed-in users in one test.",
                bySlug["masked-ball"],
                new[]
                {
                    new QuizQuestion(
                        "Every test signs in through the login page, and it's slow. How do you sign in once and reuse it?",
                        new[]
                        {
                            "Sign in in a [OneTimeSetUp] and share the Page between tests",
                            "Sign in once, save await context.StorageStateAsync(new() { Path = \"auth.json\" }), and create later contexts with StorageStatePath = \"auth.json\"",
                            "Turn off authentication on the test server",
                            "Copy the cookie value from your own browser into the test",
                        },
                        1,
                        "StorageState saves cookies and local storage to a file. Every new context created from it starts already signed in, and each test still gets its own fresh context.",
                        "What does a browser remember after you sign in, and can you write it down?"),
                    new QuizQuestion(
                        "A chat test needs two users signed in at the same time. What do you use?",
                        new[]
                        {
                            "Two pages in the same context",
                            "One page, signing out and in again between messages",
                            "Two contexts from Browser.NewContextAsync(), each signed in as a different user",
                            "Two separate test runs",
                        },
                        2,
                        "A browser context is a fresh, isolated profile with its own cookies and storage. Pages in the same context share one set of cookies, so they'd be the same user.",
                        "Two tabs in your own browser are signed in as the same person. Why?"),
                    new QuizQuestion(
                        "Why does every Playwright test get a brand new browser context?",
                        new[]
                        {
                            "So cookies and storage from one test can't leak into the next",
                            "Because contexts are faster than pages",
                            "Because a context can only open one page",
                            "It doesn't; tests share one context by default",
                        },
                        0,
                        "Isolation: each test starts with no cookies, no storage and no signed-in user unless you give it one. Contexts are cheap, so this costs almost nothing.",
                        "If test A signs in and test B expects to be signed out, what has to be true?"),
                })
            {
                TypeScriptRiddles = new[]
                {
                    new QuizQuestion(
                        "Every test signs in through the login page, and it's slow. How do you sign in once and reuse it?",
                        new[]
                        {
                            "Sign in in beforeAll and share the page between tests",
                            "Sign in once in a setup project, save page.context().storageState({ path: 'auth.json' }), and set use: { storageState: 'auth.json' }",
                            "Turn off authentication on the test server",
                            "Copy the cookie value from your own browser into the test",
                        },
                        1,
                        "storageState saves cookies and local storage to a file. Every new context created from it starts already signed in, and each test still gets its own fresh context.",
                        "What does a browser remember after you sign in, and can you write it down?"),
                    new QuizQuestion(
                        "A chat test needs two users signed in at the same time. What do you use?",
                        new[]
                        {
                            "Two pages in the same context",
                            "One page, signing out and in again between messages",
                            "Two contexts from browser.newContext(), each signed in as a different user",
                            "Two separate test runs",
                        },
                        2,
                        "A browser context is a fresh, isolated profile with its own cookies and storage. Pages in the same context share one set of cookies, so they'd be the same user.",
                        "Two tabs in your own browser are signed in as the same person. Why?"),
                    new QuizQuestion(
                        "Why does every Playwright test get a brand new browser context?",
                        new[]
                        {
                            "So cookies and storage from one test can't leak into the next",
                            "Because contexts are faster than pages",
                            "Because a context can only open one page",
                            "It doesn't; tests share one context by default",
                        },
                        0,
                        "Isolation: each test starts with no cookies, no storage and no signed-in user unless you give it one. Contexts are cheap, so this costs almost nothing.",
                        "If test A signs in and test B expects to be signed out, what has to be true?"),
                },
            },
            new Floor(12, "oracle", "The Oracle's Well", "API testing",
                "The Oracle has no buttons. Learn to send requests from your test, check status codes and headers, and read the JSON that comes back.",
                bySlug["oracles-well"],
                new[]
                {
                    new QuizQuestion(
                        "Your test needs a user to exist before it opens the page. What's the quickest way to make one?",
                        new[]
                        {
                            "Fill in the sign-up form at the start of every test",
                            "Call the API directly with Page.APIRequest, then open the page",
                            "Insert a row into the database from the test",
                            "Ask a teammate to create it by hand",
                        },
                        1,
                        "Page.APIRequest sends real HTTP from the test, sharing the context's cookies. Setting data up through the API is fast and doesn't depend on the UI you're about to test.",
                        "Which is faster: a person filling in a form, or the request the form would send?"),
                    new QuizQuestion(
                        "You PostAsync(url, new() { DataObject = new { name = \"Rae\" } }). How do you know it worked?",
                        new[]
                        {
                            "If no exception was thrown, it worked",
                            "Check the status: Assert.That(reply.Ok, Is.True), or compare reply.Status with 201",
                            "Take a screenshot of the reply",
                            "Wait a second and check the page",
                        },
                        1,
                        "An API reply with a 404 or 500 is still a reply: nothing throws. Asserting on the status (and then the body) is the test.",
                        "If the server answers 500, did the request succeed?"),
                    new QuizQuestion(
                        "A POST answers 201 Created with an empty body. Where is the thing you created?",
                        new[]
                        {
                            "Nowhere: 201 means it's still being made",
                            "At the URL in the Location header: reply.Headers[\"location\"]",
                            "In the cookies",
                            "In the reply's status text",
                        },
                        1,
                        "201 Created points at the new resource with a Location header. GET that URL to read it.",
                        "Created, but where? Which header names a place?"),
                })
            {
                TypeScriptRiddles = new[]
                {
                    new QuizQuestion(
                        "Your test needs a user to exist before it opens the page. What's the quickest way to make one?",
                        new[]
                        {
                            "Fill in the sign-up form at the start of every test",
                            "Call the API directly with page.request, then open the page",
                            "Insert a row into the database from the test",
                            "Ask a teammate to create it by hand",
                        },
                        1,
                        "page.request sends real HTTP from the test, sharing the context's cookies. Setting data up through the API is fast and doesn't depend on the UI you're about to test.",
                        "Which is faster: a person filling in a form, or the request the form would send?"),
                    new QuizQuestion(
                        "You post(url, { data: { name: 'Rae' } }). How do you know it worked?",
                        new[]
                        {
                            "If no exception was thrown, it worked",
                            "Check the status: expect(reply).toBeOK(), or compare reply.Status() with 201",
                            "Take a screenshot of the reply",
                            "Wait a second and check the page",
                        },
                        1,
                        "An API reply with a 404 or 500 is still a reply: nothing throws. Asserting on the status (and then the body) is the test.",
                        "If the server answers 500, did the request succeed?"),
                    new QuizQuestion(
                        "A POST answers 201 Created with an empty body. Where is the thing you created?",
                        new[]
                        {
                            "Nowhere: 201 means it's still being made",
                            "At the URL in the Location header: reply.headers()['location']",
                            "In the cookies",
                            "In the reply's status text",
                        },
                        1,
                        "201 Created points at the new resource with a Location header. GET that URL to read it.",
                        "Created, but where? Which header names a place?"),
                },
            },
            new Floor(13, "chancellery", "The Chancellery", "Boss: floors 8 to 12 in one chain",
                "The second boss. One errand, five floors: an API, two people signed in at once, a new tab, a steady hand and a file that goes out and comes back. Learn to carry values through a long test that spans contexts.",
                bySlug["chancellors-seal"],
                new[]
                {
                    new QuizQuestion(
                        "Your test calls Page.APIRequest.PostAsync(...). Whose cookies go with the request?",
                        new[]
                        {
                            "None: API requests never send cookies",
                            "The cookies of Page's browser context, and any cookie the reply sets lands there too",
                            "Every context's cookies at once",
                            "Only the ones you add to the headers yourself",
                        },
                        1,
                        "Page.APIRequest shares its context's cookie jar. Signing in through the API signs that context's pages in, and a page's cookies go out with its API calls.",
                        "Page.APIRequest hangs off a page. Which jar of cookies does that page use?"),
                    new QuizQuestion(
                        "A page in your second context has a link that opens a new tab. How do you catch the tab?",
                        new[]
                        {
                            "Page.RunAndWaitForPopupAsync(...) on the test's first page",
                            "chambers.RunAndWaitForPopupAsync(...), on the page that has the link",
                            "Browser.Contexts[0].Pages.Last()",
                            "You can't: popups only work in the first context",
                        },
                        1,
                        "A popup is an event on the page that opened it, and it opens in that page's context. Waiting on another page's events waits forever.",
                        "Which page fires the popup event: the one you started with, or the one you clicked on?"),
                    new QuizQuestion(
                        "The Clerk signs in on Page. You open the chambers with Page.Context.NewPageAsync() and sign the Chancellor in there. Now the Clerk is refused. Why?",
                        new[]
                        {
                            "New pages log everyone out",
                            "Both pages share one context, so the Chancellor's cookie replaced the Clerk's: the chambers need Browser.NewContextAsync()",
                            "The Clerk's page needs reloading",
                            "Cookies only work in the first tab",
                        },
                        1,
                        "Pages in one context share every cookie. Two people with the same cookie name need two contexts, each with its own jar.",
                        "Is a new tab in the same context a new cookie jar?"),
                })
            {
                TypeScriptRiddles = new[]
                {
                    new QuizQuestion(
                        "Your test calls page.request.post(...). Whose cookies go with the request?",
                        new[]
                        {
                            "None: API requests never send cookies",
                            "The cookies of page's browser context, and any cookie the reply sets lands there too",
                            "Every context's cookies at once",
                            "Only the ones you add to the headers yourself",
                        },
                        1,
                        "page.request shares its context's cookie jar. Signing in through the API signs that context's pages in, and a page's cookies go out with its API calls.",
                        "page.request hangs off a page. Which jar of cookies does that page use?"),
                    new QuizQuestion(
                        "A page in your second context has a link that opens a new tab. How do you catch the tab?",
                        new[]
                        {
                            "page.waitForEvent('popup') on the test's first page",
                            "chambers.waitForEvent('popup'), started before the click, on the page that has the link",
                            "browser.contexts()[0].pages().at(-1)",
                            "You can't: popups only work in the first context",
                        },
                        1,
                        "A popup is an event on the page that opened it, and it opens in that page's context. Waiting on another page's events waits forever.",
                        "Which page fires the popup event: the one you started with, or the one you clicked on?"),
                    new QuizQuestion(
                        "The Clerk signs in on page. You open the chambers with page.context().newPage() and sign the Chancellor in there. Now the Clerk is refused. Why?",
                        new[]
                        {
                            "New pages log everyone out",
                            "Both pages share one context, so the Chancellor's cookie replaced the Clerk's: the chambers need browser.newContext()",
                            "The Clerk's page needs reloading",
                            "Cookies only work in the first tab",
                        },
                        1,
                        "Pages in one context share every cookie. Two people with the same cookie name need two contexts, each with its own jar.",
                        "Is a new tab in the same context a new cookie jar?"),
                },
            },
            new Floor(14, "illusion", "The Illusionist's Theater", "Mocking responses",
                "Nothing on stage is what the server sent. Learn to catch requests in the browser, answer them yourself, change real replies and stop the ones you don't want.",
                bySlug["illusionists-stage"],
                new[]
                {
                    new QuizQuestion(
                        "Your test needs the shop to show an empty basket, but the real API always has items in it. What do you do?",
                        new[]
                        {
                            "Delete every item through the UI first",
                            "Page.RouteAsync(\"**/api/basket\", ...) and answer with your own JSON, before the page asks",
                            "Edit the page's HTML after it loads",
                            "Point the test at a different server",
                        },
                        1,
                        "A route catches the request in the browser. route.FulfillAsync(new() { Json = new[] { new { name = \"Rae\" } } }) answers it with whatever you like, and the real server never hears about it.",
                        "Who answers a request if you get to it first?"),
                    new QuizQuestion(
                        "You only want to change one field of the real reply. How?",
                        new[]
                        {
                            "Fake the whole reply by hand",
                            "In the route: var real = await route.FetchAsync(); read its JSON, change the field, and fulfill with Response = real and the new JSON",
                            "You can't: a route either mocks or doesn't",
                            "Use page.on('response') to edit it",
                        },
                        1,
                        "route.FetchAsync() makes the real request for you. Fulfilling with that response plus your changes keeps its status and headers and swaps the body.",
                        "Can you ask the real server first, then lie about what it said?"),
                    new QuizQuestion(
                        "The page fires an analytics request as it loads, and you added a route to block it after GotoAsync. It still got through. Why?",
                        new[]
                        {
                            "Analytics can't be blocked",
                            "The route was set up too late: call Page.RouteAsync before GotoAsync",
                            "route.AbortAsync() only works on images",
                            "The glob needs to start with https://",
                        },
                        1,
                        "A route only catches requests made after it exists. Requests fired while the page loads need their routes set up before you open the page.",
                        "When did the request happen, and when did your route start listening?"),
                })
            {
                TypeScriptRiddles = new[]
                {
                    new QuizQuestion(
                        "Your test needs the shop to show an empty basket, but the real API always has items in it. What do you do?",
                        new[]
                        {
                            "Delete every item through the UI first",
                            "page.route(\"**/api/basket\", ...) and answer with your own JSON, before the page asks",
                            "Edit the page's HTML after it loads",
                            "Point the test at a different server",
                        },
                        1,
                        "A route catches the request in the browser. route.fulfill({ json: [{ name: 'Rae' }] }) answers it with whatever you like, and the real server never hears about it.",
                        "Who answers a request if you get to it first?"),
                    new QuizQuestion(
                        "You only want to change one field of the real reply. How?",
                        new[]
                        {
                            "Fake the whole reply by hand",
                            "In the route: const response = await route.fetch(); read its JSON, change the field, and fulfill({ response, json })",
                            "You can't: a route either mocks or doesn't",
                            "Use page.on('response') to edit it",
                        },
                        1,
                        "route.fetch() makes the real request for you. Fulfilling with that response plus your changes keeps its status and headers and swaps the body.",
                        "Can you ask the real server first, then lie about what it said?"),
                    new QuizQuestion(
                        "The page fires an analytics request as it loads, and you added a route to block it after goto. It still got through. Why?",
                        new[]
                        {
                            "Analytics can't be blocked",
                            "The route was set up too late: call page.route before goto",
                            "route.abort() only works on images",
                            "The glob needs to start with https://",
                        },
                        1,
                        "A route only catches requests made after it exists. Requests fired while the page loads need their routes set up before you open the page.",
                        "When did the request happen, and when did your route start listening?"),
                },
            },
            new Floor(15, "atelier", "The Architect's Atelier", "Page Object Model",
                "Every blueprint here is the same blueprint, drawn again. Learn to wrap a page, or one part of it, in a class your tests can reuse.",
                bySlug["drafting-table"],
                new[]
                {
                    new QuizQuestion(
                        "What is a page object?",
                        new[]
                        {
                            "A copy of the page's HTML saved for later",
                            "A class that wraps a page (or part of one): it holds the locators and offers actions like SignInAsync",
                            "Playwright's internal name for a browser tab",
                            "A JSON file listing every selector",
                        },
                        1,
                        "Tests then say what they do (signIn, addToBasket) and the page object knows how. When the page changes, you fix one class instead of every test.",
                        "If the Sign in button moves, how many places should you have to change?"),
                    new QuizQuestion(
                        "A page has five identical product cards. How do you model them?",
                        new[]
                        {
                            "One component class that takes the card's ILocator as its root, made once per card",
                            "Five classes, one per card",
                            "One giant page object with card1Title, card2Title, ...",
                            "Don't: cards are too small for page objects",
                        },
                        0,
                        "A component object scoped to a root locator works for any card. Every locator inside it starts from the root, so it never reaches into the card next door.",
                        "What's different between the cards: the shape, or just where they are?"),
                    new QuizQuestion(
                        "Where should most assertions live?",
                        new[]
                        {
                            "In the page object's constructor",
                            "In the tests: page objects find things and do things, tests decide what's right",
                            "In a separate assertions page object",
                            "Nowhere: page objects make assertions unnecessary",
                        },
                        1,
                        "Keeping checks in the test keeps the test readable as a story. A page object that asserts can't be reused by a test that expects something else. The IPage goes in, behavior comes out.",
                        "Two tests use the same page object and expect different results. Who should know the difference?"),
                })
            {
                TypeScriptRiddles = new[]
                {
                    new QuizQuestion(
                        "What is a page object?",
                        new[]
                        {
                            "A copy of the page's HTML saved for later",
                            "A class that wraps a page (or part of one): it holds the locators and offers actions like signIn()",
                            "Playwright's internal name for a browser tab",
                            "A JSON file listing every selector",
                        },
                        1,
                        "Tests then say what they do (signIn, addToBasket) and the page object knows how. When the page changes, you fix one class instead of every test.",
                        "If the Sign in button moves, how many places should you have to change?"),
                    new QuizQuestion(
                        "A page has five identical product cards. How do you model them?",
                        new[]
                        {
                            "One component class that takes the card's Locator as its root, made once per card",
                            "Five classes, one per card",
                            "One giant page object with card1Title, card2Title, ...",
                            "Don't: cards are too small for page objects",
                        },
                        0,
                        "A component object scoped to a root locator works for any card. Every locator inside it starts from the root, so it never reaches into the card next door.",
                        "What's different between the cards: the shape, or just where they are?"),
                    new QuizQuestion(
                        "Where should most assertions live?",
                        new[]
                        {
                            "In the page object's constructor",
                            "In the tests: page objects find things and do things, tests decide what's right",
                            "In a separate assertions page object",
                            "Nowhere: page objects make assertions unnecessary",
                        },
                        1,
                        "Keeping checks in the test keeps the test readable as a story. A page object that asserts can't be reused by a test that expects something else. The Page goes in, behavior comes out.",
                        "Two tests use the same page object and expect different results. Who should know the difference?"),
                },
            },
            new Floor(16, "hydra", "The Hydra Pits", "Fixtures, setup and parallel tests",
                "Cut one head and it grows back. Learn to set things up once for many tests, keep each test's hands clean, and run tests side by side.",
                bySlug["severing-pit"],
                new[]
                {
                    new QuizQuestion(
                        "Five tests all need the same hunt, started once before any of them run. Where does that call go?",
                        new[]
                        {
                            "In a [SetUp] method, so every test has it",
                            "In a static [OneTimeSetUp] that keeps the result in a static field",
                            "In the first test, while the others wait for it",
                            "In the class's constructor",
                        },
                        1,
                        "[SetUp] runs before every test, so five tests would start five hunts. [OneTimeSetUp] runs once for the whole class, and a static field lets every test instance read what it made.",
                        "How many times does each kind of setup run?"),
                    new QuizQuestion(
                        "You marked the class [Parallelizable(ParallelScope.All)], and now tests click on each other's pages. Why?",
                        new[]
                        {
                            "Playwright can't run tests in parallel",
                            "NUnit shares one instance of the class between its tests, so they share its Page. Add [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]",
                            "Parallel tests need a separate browser each, set in the runsettings",
                            "The tests need Thread.Sleep between them",
                        },
                        1,
                        "By default NUnit makes one object per test class. Tests running at the same time in that one object share its fields, Page included. One instance per test case gives each test its own.",
                        "What does NUnit create once per class, and what lives on it?"),
                    new QuizQuestion(
                        "Five parallel tests only ever run four at a time on your laptop. What's holding the fifth back?",
                        new[]
                        {
                            "Chromium only allows four contexts",
                            "NUnit's worker count, which defaults to your number of CPU cores. Raise it with [assembly: LevelOfParallelism(5)]",
                            "The dungeon only accepts four connections",
                            "Parallel tests always run in batches of four",
                        },
                        1,
                        "NUnit runs as many tests at once as it has workers, and by default that's one per core. LevelOfParallelism sets it for the whole test project.",
                        "Who decides how many tests run at the same moment?"),
                })
            {
                TypeScriptRiddles = new[]
                {
                    new QuizQuestion(
                        "Five tests run in five workers and need one shared hunt. Why doesn't a test.beforeAll work?",
                        new[]
                        {
                            "beforeAll can't make API calls",
                            "Each worker is its own process and runs its own beforeAll, so you'd get five hunts. Start it once in a globalSetup",
                            "beforeAll runs after the tests",
                            "It does work, if the hook is async",
                        },
                        1,
                        "Playwright's workers are separate processes, and hooks run in every worker that needs them. globalSetup runs once, in the main process, before any worker starts.",
                        "How many times does a beforeAll run when its tests are spread over five workers?"),
                    new QuizQuestion(
                        "You set workers: 5, but the five tests in your file still run one after another. Why?",
                        new[]
                        {
                            "Five workers is too many for one file",
                            "By default a file's tests run in order, in one worker. Set fullyParallel: true, or test.describe.configure({ mode: 'parallel' })",
                            "Workers only apply to the HTML reporter",
                            "Tests named with a template string can't run in parallel",
                        },
                        1,
                        "Playwright spreads files over workers, but keeps the tests inside a file together, in order, unless you tell it they're independent.",
                        "What does Playwright assume about tests that live in the same file?"),
                    new QuizQuestion(
                        "Your globalSetup started the hunt. How do the tests in the workers find out its id?",
                        new[]
                        {
                            "They can't: globalSetup can't share anything",
                            "Set process.env.HUNT_ID in the globalSetup: workers start after it and inherit its environment",
                            "Return it from globalSetup and read it from test.info()",
                            "Each test calls globalSetup again",
                        },
                        1,
                        "Environment variables set in globalSetup are passed on to every worker it starts, so a short value like an id travels for free. Bigger things go in a file.",
                        "What do child processes inherit from the process that started them?"),
                },
            },
            new Floor(17, "gallery", "The Portrait Gallery", "Screenshots and visual comparison",
                "Some changes only show in the pixels. Learn to take screenshots of a page or one part of it, keep moving parts out of the picture, and compare what you see.",
                bySlug["curators-eye"],
                new[]
                {
                    new QuizQuestion(
                        "You want a picture of one card on the page, not the whole page. What do you call?",
                        new[]
                        {
                            "Page.ScreenshotAsync(), then crop it",
                            "ScreenshotAsync() on the card's locator: it captures just that element",
                            "Page.PdfAsync() for the card",
                            "There's no way to capture less than the viewport",
                        },
                        1,
                        "Every locator can take its own screenshot. Playwright scrolls the element into view and captures exactly its box, and returns the PNG as bytes (or saves it, with Path).",
                        "Who else can take a screenshot, besides the page?"),
                    new QuizQuestion(
                        "Two screenshots of the same page, a second apart, never match: a spinner keeps turning. How do you keep it out of the comparison?",
                        new[]
                        {
                            "Take the screenshot faster",
                            "Mask it (Mask = new[] { spinner }), or turn animations off with Animations = ScreenshotAnimations.Disabled",
                            "Compare only the file sizes",
                            "Retry until two shots match",
                        },
                        1,
                        "Mask paints a box over the parts you name, the same color every time. Disabled animations jump to their end state. Either way the moving part stops changing the picture.",
                        "Can you tell the screenshot what to leave out, or to hold still?"),
                    new QuizQuestion(
                        "Why compare screenshots at all, when you can read the page's HTML?",
                        new[]
                        {
                            "Screenshots are faster than reading HTML",
                            "Some bugs only live in the pixels: the HTML, the src and the alt text can be identical while what's drawn differs",
                            "HTML can't be read in a test",
                            "Screenshots never change between browsers",
                        },
                        1,
                        "A wrong color, a broken image, an overlap or a missing font all leave the DOM looking fine. Only what's drawn shows them, which is what visual comparison checks.",
                        "What could be wrong on a page whose HTML is perfect?"),
                })
            {
                TypeScriptRiddles = new[]
                {
                    new QuizQuestion(
                        "You want a picture of one card on the page, not the whole page. What do you call?",
                        new[]
                        {
                            "page.screenshot(), then crop it",
                            "screenshot() on the card's locator: it captures just that element",
                            "page.pdf() for the card",
                            "There's no way to capture less than the viewport",
                        },
                        1,
                        "Every locator can take its own screenshot. Playwright scrolls the element into view and captures exactly its box, and returns the PNG as a Buffer (or saves it, with path).",
                        "Who else can take a screenshot, besides the page?"),
                    new QuizQuestion(
                        "The first time you run expect(page).toHaveScreenshot(), it fails. Why?",
                        new[]
                        {
                            "toHaveScreenshot only works in CI",
                            "There's no baseline yet: the first run writes one, and later runs compare against it",
                            "It needs a full-page screenshot",
                            "The page wasn't loaded",
                        },
                        1,
                        "A visual assertion needs something to compare with. The first run saves the expected image next to the test; commit it, and from then on every run is checked against it (--update-snapshots makes a new one).",
                        "What does the assertion compare the screenshot with?"),
                    new QuizQuestion(
                        "Two screenshots of the same page, a second apart, never match: a spinner keeps turning. How do you keep it out of the comparison?",
                        new[]
                        {
                            "Take the screenshot faster",
                            "Mask it ({ mask: [spinner] }), or turn animations off with { animations: 'disabled' }",
                            "Compare only the file sizes",
                            "Retry until two shots match",
                        },
                        1,
                        "mask paints a box over the parts you name, the same color every time. Disabled animations jump to their end state. Either way the moving part stops changing the picture.",
                        "Can you tell the screenshot what to leave out, or to hold still?"),
                },
            },
        };
    }

    /// <summary>The deeper floors, shown on the map as sealed until they're built.</summary>
    public IReadOnlyList<(int Number, string Title, string Skill)> SealedFloors { get; } = new[]
    {
        (18, "The Clocktower of Elsewhere", "Emulation and the clock"),
        (19, "The Flake", "The final boss: a flaky-test hunt"),
    };

    public Floor? Find(string slug) => Floors.FirstOrDefault(f => f.Slug == slug);

    public Floor? FindByRoom(string roomSlug) => Floors.FirstOrDefault(f => f.Room.Slug == roomSlug);

    public static int MaxXp(int floorCount) => floorCount * (Rewards.Scroll + Rewards.Riddle + Rewards.Treasure);
}
