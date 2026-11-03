using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Tests;

/// <summary>Cursed Runs: practice for anyone, run links for keeps, badges but never XP.</summary>
public class CursedRunTests
{
    private const string Room = "hall-of-doors";
    private SignedInDungeon dungeon = null!;

    [SetUp]
    public void StartDungeon() => dungeon = new SignedInDungeon();

    [TearDown]
    public void StopDungeon() => dungeon.Dispose();

    private IPlayerRecords Records => dungeon.Services.GetRequiredService<IPlayerRecords>();

    private static async Task<JsonElement> StartAsync(HttpClient client, string query, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var reply = await client.PostAsync($"/api/rooms/{Room}/attempts{query}", null);
        Assert.That(reply.StatusCode, Is.EqualTo(expected), query);
        return reply.StatusCode == HttpStatusCode.OK ? await reply.Content.ReadFromJsonAsync<JsonElement>() : default;
    }

    /// <summary>The hall's answer: the target door that isn't sealed.</summary>
    private static async Task<JsonElement> ClearAsync(HttpClient client, JsonElement attempt)
    {
        var briefing = attempt.GetProperty("briefing");
        var target = briefing.GetProperty("target").GetString();
        var door = briefing.GetProperty("doors").EnumerateArray()
            .Single(d => d.GetProperty("label").GetString() == target && !d.GetProperty("sealed").GetBoolean())
            .GetProperty("id").GetString();
        var reply = await client.PostAsJsonAsync($"/api/rooms/{Room}/attempts/{attempt.GetProperty("attemptId").GetGuid()}/answer", new { Answer = door });
        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return await reply.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    /// <summary>
    /// Asks the room page for a run link, as the signed-in player clicking "Start a cursed run". The anti-forgery
    /// token comes from the feedback form, so this works even when the room page shows no button.
    /// </summary>
    private static async Task<string> StartRunPageAsync(HttpClient player, string curse)
    {
        var page = await player.GetStringAsync("/feedback");
        var reply = await player.PostAsync($"/rooms/{Room}?handler=StartRun", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(page),
            ["curse"] = curse,
        }));
        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return WebUtility.HtmlDecode(await reply.Content.ReadAsStringAsync());
    }

    private static string? RunToken(string page) => Regex.Match(page, @"\?run=([0-9a-f]{16})") is { Success: true } m ? m.Groups[1].Value : null;

    private async Task ClearedByAsync(string login) => await Records.RecordAsync($"id-{login}", login, Achievement.Treasure, Room);

    [Test]
    public async Task APracticeHasteRunHalvesTheTimeAndEarnsNothing()
    {
        var anyone = dungeon.ClientFor(null);

        var normal = await StartAsync(anyone, "");
        var hasty = await StartAsync(anyone, "?curse=haste");
        var cleared = await ClearAsync(anyone, hasty);

        Assert.Multiple(() =>
        {
            Assert.That(hasty.GetProperty("timeLimitMs").GetInt32(), Is.EqualTo(normal.GetProperty("timeLimitMs").GetInt32() / 2));
            Assert.That(hasty.GetProperty("curse").GetProperty("code").GetString(), Is.EqualTo("haste"));
            Assert.That(hasty.GetProperty("curse").GetProperty("forKeeps").GetBoolean(), Is.False);
            Assert.That(normal.GetProperty("curse").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(cleared.GetProperty("curseBroken").ValueKind, Is.EqualTo(JsonValueKind.Null), "practice earns no badge");
        });
    }

    [Test]
    public async Task UnknownCursesAndCursesARoomDoesntHaveAreRefused()
    {
        var anyone = dungeon.ClientFor(null);

        await StartAsync(anyone, "?curse=frogs", HttpStatusCode.BadRequest);
        var noIds = await anyone.PostAsync("/api/rooms/flickering-torch/attempts?curse=missing-ids", null);
        var setup = await anyone.PostAsync("/api/rooms/welcome-matt/attempts?curse=haste", null);

        Assert.Multiple(() =>
        {
            Assert.That(noIds.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), "the torch room has no test ids to take away");
            Assert.That(setup.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), "the setup room has no curses");
        });
    }

    [Test]
    public async Task ARunLinkPlaysForItsOwnerFromASignedOutBrowser()
    {
        await ClearedByAsync("ada");
        var page = await StartRunPageAsync(dungeon.ClientFor("ada"), "shifting-walls");
        var token = RunToken(page);
        Assert.That(token, Is.Not.Null, page);

        // The player's test: a fresh browser, never signed in.
        var test = dungeon.ClientFor(null);
        var attempt = await StartAsync(test, $"?run={token}");
        var cleared = await ClearAsync(test, attempt);

        var record = await Records.GetAsync("id-ada");
        Assert.Multiple(() =>
        {
            Assert.That(attempt.GetProperty("curse").GetProperty("forKeeps").GetBoolean());
            Assert.That(attempt.GetProperty("curse").GetProperty("player").GetString(), Is.EqualTo("ada"));
            Assert.That(cleared.GetProperty("curseBroken").GetString(), Is.EqualTo("Curse of Shifting Walls"));
            Assert.That(record!.Achievements, Has.Some.Matches<Achievement>(a => a.Kind == "curse" && a.Slug == "hall-of-doors:shifting-walls"));
        });
    }

    [Test]
    public async Task ARunLinkIsDoneOnceTheCurseIsBroken()
    {
        await ClearedByAsync("ada");
        var token = RunToken(await StartRunPageAsync(dungeon.ClientFor("ada"), "haste"));
        var test = dungeon.ClientFor(null);
        await ClearAsync(test, await StartAsync(test, $"?run={token}"));

        await StartAsync(test, $"?run={token}", HttpStatusCode.Gone);
    }

    [Test]
    public async Task ARunLinkOnlyWorksInItsOwnRoom()
    {
        await ClearedByAsync("ada");
        var token = RunToken(await StartRunPageAsync(dungeon.ClientFor("ada"), "haste"));

        var elsewhere = await dungeon.ClientFor(null).PostAsync($"/api/rooms/flickering-torch/attempts?run={token}", null);

        Assert.That(elsewhere.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
    }

    [Test]
    public async Task CursedRunsOpenOnlyOnceTheRoomIsCleared()
    {
        var player = dungeon.ClientFor("grace");

        var room = await player.GetStringAsync($"/rooms/{Room}");
        var forced = await StartRunPageAsync(player, "haste");

        Assert.Multiple(() =>
        {
            Assert.That(room, Does.Contain("Cursed Runs").And.Contain("Practise it").And.Not.Contain("Start a cursed run"));
            Assert.That(RunToken(forced), Is.Null);
            Assert.That(forced, Does.Contain("Clear this room while signed in first"));
        });
    }

    [Test]
    public async Task FlawlessIsOneTryADay()
    {
        await ClearedByAsync("ada");
        var ada = dungeon.ClientFor("ada");

        var token = RunToken(await StartRunPageAsync(ada, "flawless"));
        var test = dungeon.ClientFor(null);
        await StartAsync(test, $"?run={token}");
        var again = await StartAsync(test, $"?run={token}", HttpStatusCode.Gone);
        var secondLink = await StartRunPageAsync(ada, "flawless");

        Assert.Multiple(() =>
        {
            Assert.That(RunToken(secondLink), Is.Null, "no second Flawless link today");
            Assert.That(secondLink, Does.Contain("today's Flawless try"));
        });
    }

    [Test]
    public async Task FlawlessNeedsAFloorWithNoHelpTaken()
    {
        await ClearedByAsync("ada");
        var ada = dungeon.ClientFor("ada");
        await ada.PutAsJsonAsync("/api/progress", new
        {
            scrolls = Array.Empty<string>(), riddles = Array.Empty<string>(), treasures = Array.Empty<string>(),
            help = new Dictionary<string, int> { [$"nudge:{Room}:0"] = 5 },
        });

        var page = await StartRunPageAsync(ada, "flawless");

        Assert.Multiple(() =>
        {
            Assert.That(RunToken(page), Is.Null);
            Assert.That(page, Does.Contain("taken help on this floor"));
        });
    }

    [Test]
    public async Task BrokenCursesShowAsABadgeAndAreWorthNoXp()
    {
        await ClearedByAsync("ada");
        await Records.SetOnLeaderboardAsync("id-ada", "ada", true);
        var before = await dungeon.ClientFor(null).GetStringAsync("/leaderboard");

        var token = RunToken(await StartRunPageAsync(dungeon.ClientFor("ada"), "haste"));
        var test = dungeon.ClientFor(null);
        await ClearAsync(test, await StartAsync(test, $"?run={token}"));

        var after = await dungeon.ClientFor(null).GetStringAsync("/leaderboard");
        var room = await dungeon.ClientFor("ada").GetStringAsync($"/rooms/{Room}");
        string Xp(string html) => Regex.Match(html, @"@ada[\s\S]*?(\d+) XP").Groups[1].Value;
        Assert.Multiple(() =>
        {
            Assert.That(after, Does.Contain("💀 1 curse"));
            Assert.That(Xp(after), Is.EqualTo(Xp(before)));
            Assert.That(room, Does.Contain("💀 Broken"));
        });
    }

    [Test]
    public void EveryTimedRoomOffersCursesAndMissingIdsOnlyWhereThereAreIds()
    {
        var catalog = dungeon.Services.GetRequiredService<DungeonCatalog>();
        foreach (var floor in catalog.Floors)
        {
            var offered = Curses.For(floor.Room).Select(c => c.Code).ToList();
            if (floor.Room.TimeLimit >= TimeSpan.FromMinutes(5))
            {
                Assert.That(offered, Is.Empty, floor.Room.Slug);
                continue;
            }
            Assert.That(offered, Is.SupersetOf(new[] { "haste", "shifting-walls", "flawless" }), floor.Room.Slug);
        }
        Assert.That(Curses.For(catalog.FindByRoom("flickering-torch")!.Room).Select(c => c.Code), Does.Not.Contain("missing-ids"));
        Assert.That(Curses.For(catalog.FindByRoom("portal-bazaar")!.Room).Select(c => c.Code), Does.Contain("missing-ids"));
    }
}
