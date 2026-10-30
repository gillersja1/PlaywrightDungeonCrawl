using System.Xml.Linq;
using Npgsql;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Tests;

/// <summary>
/// Runs against a real Postgres when DUNGEON_TEST_POSTGRES holds a connection
/// string (CI starts one); skipped otherwise.
/// </summary>
public class PostgresTests
{
    private NpgsqlDataSource db = null!;

    [SetUp]
    public async Task Connect()
    {
        var connectionString = Environment.GetEnvironmentVariable("DUNGEON_TEST_POSTGRES");
        if (string.IsNullOrEmpty(connectionString))
        {
            Assert.Ignore("Set DUNGEON_TEST_POSTGRES to run the Postgres tests.");
            return;
        }
        db =NpgsqlDataSource.Create(PostgresDatabase.NormalizeConnectionString(connectionString));
        await PostgresDatabase.EnsureCreatedAsync(db);
        // Twice: creating the tables must be safe on every startup.
        await PostgresDatabase.EnsureCreatedAsync(db);
    }

    [TearDown]
    public async Task Disconnect()
    {
        if (db is not null)
        {
            await db.DisposeAsync();
        }
    }

    [Test]
    public async Task ProgressMergesAndResets()
    {
        var store = new PostgresProgressStore(db);
        var player = $"test-{Guid.NewGuid()}";

        await store.MergeAsync(player, "ada", new(["setup"], [], [], new() { ["hint:setup:0"] = 0 }));
        var merged = await store.MergeAsync(player, "ada", new([], ["setup"], ["welcome-matt"], []));
        await store.ResetAsync(player);
        var afterReset = await store.MergeAsync(player, "ada", PlayerProgress.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(merged.Scrolls, Is.EqualTo(new[] { "setup" }));
            Assert.That(merged.Riddles, Is.EqualTo(new[] { "setup" }));
            Assert.That(merged.Treasures, Is.EqualTo(new[] { "welcome-matt" }));
            Assert.That(merged.Help, Contains.Key("hint:setup:0"));
            Assert.That(afterReset.Scrolls, Is.Empty);
        });
    }

    [Test]
    public async Task VerifiedAchievementsAndListingPersist()
    {
        var records = new PostgresPlayerRecords(db);
        var player = $"test-{Guid.NewGuid()}";

        await records.RecordAsync(player, "ada", Achievement.Treasure, "welcome-matt");
        var first = (await records.GetAsync(player))!.Achievements.Single().EarnedAt;
        await records.RecordAsync(player, "ada", Achievement.Treasure, "welcome-matt");
        await records.RecordAsync(player, "ada", Achievement.Riddle, "setup");
        var unlisted = await records.ListedAsync();
        await records.SetOnLeaderboardAsync(player, "ada", true);
        var listed = await records.ListedAsync();
        var saved = await records.GetAsync(player);
        await records.ResetAsync(player);
        var afterReset = await records.GetAsync(player);

        Assert.Multiple(() =>
        {
            Assert.That(unlisted.Select(r => r.PlayerId), Does.Not.Contain(player));
            Assert.That(listed.Select(r => r.PlayerId), Does.Contain(player));
            Assert.That(saved!.Achievements, Has.Count.EqualTo(2), "recording twice keeps one entry");
            Assert.That(saved.Achievements.Single(a => a.Kind == Achievement.Treasure).EarnedAt, Is.EqualTo(first));
            Assert.That(afterReset!.Achievements, Is.Empty);
            Assert.That(afterReset.OnLeaderboard, Is.True, "a reset keeps the leaderboard choice");
        });
    }

    [Test]
    public void CookieKeysSurviveARestart()
    {
        var name = $"key-{Guid.NewGuid()}";
        new PostgresXmlRepository(db).StoreElement(new XElement("key", new XAttribute("id", name)), name);

        // A fresh repository, as after a restart, still sees the key.
        var keys = new PostgresXmlRepository(db).GetAllElements();

        Assert.That(keys.Select(k => (string?)k.Attribute("id")), Does.Contain(name));
    }
}
