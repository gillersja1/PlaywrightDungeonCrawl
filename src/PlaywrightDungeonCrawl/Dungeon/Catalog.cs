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
        };
    }

    /// <summary>The deeper floors, shown on the map as sealed until they're built.</summary>
    public IReadOnlyList<(int Number, string Title, string Skill)> SealedFloors { get; } = new[]
    {
        (11, "The Masquerade", "Login state and browser contexts"),
        (12, "The Oracle's Well", "API testing"),
        (13, "The Illusionist's Theater", "Mocking responses"),
        (14, "The Architect's Atelier", "Page Object Model"),
        (15, "The Hydra Pits", "Fixtures, setup and parallel tests"),
        (16, "The Haunted Archive", "Tracing and debugging"),
        (17, "The Portrait Gallery", "Screenshots and visual comparison"),
        (18, "The Clocktower of Elsewhere", "Emulation and the clock"),
        (19, "The Flake", "The final boss: a flaky-test hunt"),
    };

    public Floor? Find(string slug) => Floors.FirstOrDefault(f => f.Slug == slug);

    public Floor? FindByRoom(string roomSlug) => Floors.FirstOrDefault(f => f.Room.Slug == roomSlug);

    public static int MaxXp(int floorCount) => floorCount * (Rewards.Scroll + Rewards.Riddle + Rewards.Treasure);
}
