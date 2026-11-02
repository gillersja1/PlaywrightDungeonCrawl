using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Tests;

public class LeaderboardTests
{
    private SignedInDungeon dungeon = null!;

    [SetUp]
    public void StartDungeon() => dungeon = new SignedInDungeon();

    [TearDown]
    public void StopDungeon() => dungeon.Dispose();

    private IPlayerRecords Records => dungeon.Services.GetRequiredService<IPlayerRecords>();
    private DungeonCatalog Catalog => dungeon.Services.GetRequiredService<DungeonCatalog>();
    private string FlagFor(string room) => dungeon.Services.GetRequiredService<FlagService>().ForRoom(room);

    private static Task<HttpResponseMessage> ClaimAsync(HttpClient client, string flag) =>
        client.PostAsJsonAsync("/api/treasure/claim", new { flag });

    /// <summary>Razor Pages forms need the antiforgery token from the page that holds them.</summary>
    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string page, string action, Dictionary<string, string> fields)
    {
        var html = await client.GetStringAsync(page);
        fields["__RequestVerificationToken"] =
            Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        return await client.PostAsync(action, new FormUrlEncodedContent(fields));
    }

    private async Task PassRiddleAsync(HttpClient client, string floorSlug)
    {
        var riddles = Catalog.Find(floorSlug)!.Riddles;
        var answers = riddles.Select((r, i) => ($"Answers[{i}]", r.CorrectIndex.ToString())).ToDictionary();
        var page = $"/floors/{floorSlug}/riddle";
        var reply = await PostFormAsync(client, page, page, answers);
        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static Task<HttpResponseMessage> ListAsync(HttpClient client, bool show) =>
        PostFormAsync(client, "/leaderboard", "/leaderboard?handler=Listing", new() { ["show"] = show.ToString().ToLowerInvariant() });

    [Test]
    public async Task AValidFlagClaimedWhileSignedInIsVerified()
    {
        await ClaimAsync(dungeon.ClientFor("ada"), FlagFor("welcome-matt"));

        var record = await Records.GetAsync("id-ada");

        Assert.That(record!.Achievements, Has.Some.Matches<Achievement>(a => a.Kind == Achievement.Treasure && a.Slug == "welcome-matt"));
    }

    [Test]
    public async Task BadFlagsAndSignedOutClaimsRecordNothing()
    {
        await ClaimAsync(dungeon.ClientFor("ada"), "PDC{000000000000}");
        await ClaimAsync(dungeon.ClientFor(null), FlagFor("welcome-matt"));

        Assert.That(await Records.GetAsync("id-ada"), Is.Null);
    }

    [Test]
    public async Task APassedRiddleIsVerifiedAndAFailedOneIsNot()
    {
        var player = dungeon.ClientFor("ada");
        await PassRiddleAsync(player, "setup");
        var wrong = Catalog.Find("locators")!.Riddles
            .Select((r, i) => ($"Answers[{i}]", ((r.CorrectIndex + 1) % r.Options.Length).ToString())).ToDictionary();
        await PostFormAsync(player, "/floors/locators/riddle", "/floors/locators/riddle", wrong);

        var riddles = (await Records.GetAsync("id-ada"))!.Achievements.Where(a => a.Kind == Achievement.Riddle).Select(a => a.Slug);

        Assert.That(riddles, Is.EqualTo(new[] { "setup" }));
    }

    [Test]
    public async Task TheLeaderboardIsOptIn()
    {
        var player = dungeon.ClientFor("ada");
        await PassRiddleAsync(player, "setup");
        await ClaimAsync(player, FlagFor("welcome-matt"));

        var before = await dungeon.ClientFor("grace").GetStringAsync("/leaderboard");
        var join = await ListAsync(player, true);
        var listed = await dungeon.ClientFor("grace").GetStringAsync("/leaderboard");
        await ListAsync(player, false);
        var after = await dungeon.ClientFor("grace").GetStringAsync("/leaderboard");

        Assert.Multiple(() =>
        {
            Assert.That(before, Does.Not.Contain("@ada"));
            Assert.That(join.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(listed, Does.Contain("@ada").And.Contain($"1 / {Catalog.Floors.Count}"));
            Assert.That(after, Does.Not.Contain("@ada"));
        });
    }

    [Test]
    public async Task ProgressFromTheBrowserDoesntCount()
    {
        var player = dungeon.ClientFor("ada");
        var floors = Catalog.Floors;
        await player.PutAsJsonAsync("/api/progress", new
        {
            scrolls = floors.Select(f => f.Slug), riddles = floors.Select(f => f.Slug),
            treasures = floors.Select(f => f.Room.Slug), help = new Dictionary<string, int>(),
        });
        await ListAsync(player, true);

        var board = await dungeon.ClientFor("grace").GetStringAsync("/leaderboard");

        // Scrolls are self-reported anyway, so they score; nothing else does.
        Assert.That(board, Does.Contain("@ada").And.Contain($"0 / {floors.Count}").And.Contain($"{floors.Count * Rewards.Scroll} XP"));
    }

    [Test]
    public async Task ResettingProgressClearsVerifiedAchievements()
    {
        var player = dungeon.ClientFor("ada");
        await ClaimAsync(player, FlagFor("welcome-matt"));

        await player.DeleteAsync("/api/progress");

        Assert.That((await Records.GetAsync("id-ada"))!.Achievements, Is.Empty);
    }

    [Test]
    public void AFloorNeedsBothItsRiddleAndItsTreasure()
    {
        var now = DateTimeOffset.UtcNow;
        var record = new PlayerRecord("p", "ada", true,
        [
            new(Achievement.Riddle, "setup", now), new(Achievement.Treasure, "welcome-matt", now.AddMinutes(1)),
            new(Achievement.Riddle, "locators", now.AddMinutes(2)),
        ]);
        var progress = new PlayerProgress(["setup"], [], [], new() { ["nudge:hall-of-doors:0"] = 5 });

        var standing = Leaderboard.Score(record, progress, Catalog);

        Assert.Multiple(() =>
        {
            Assert.That(standing.FloorsCleared, Is.EqualTo(1));
            Assert.That(standing.Score, Is.EqualTo(Rewards.Scroll + 2 * Rewards.Riddle + Rewards.Treasure - 5));
            Assert.That(standing.ReachedAt, Is.EqualTo(now.AddMinutes(1)));
        });
    }

    [Test]
    public void RanksByFloorsThenScoreThenWhoGotThereFirst()
    {
        var early = DateTimeOffset.UtcNow;
        var late = early.AddHours(1);

        var ranked = Leaderboard.Rank(
        [
            new("high-score", 1, 500, early),
            new("more-floors", 2, 100, late),
            new("same-but-later", 1, 200, late),
            new("same-but-earlier", 1, 200, early),
            new("twin", 1, 200, early),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(ranked.Select(r => r.Standing.Login),
                Is.EqualTo(new[] { "more-floors", "high-score", "same-but-earlier", "twin", "same-but-later" }));
            Assert.That(ranked.Select(r => r.Rank), Is.EqualTo(new[] { 1, 2, 3, 3, 5 }));
        });
    }
}
