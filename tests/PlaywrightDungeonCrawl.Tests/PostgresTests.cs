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
    public async Task BugReportsAreSavedReviewedAndRanked()
    {
        var bugs = new PostgresBugReports(db);
        var ada = $"test-{Guid.NewGuid()}";
        var grace = $"test-{Guid.NewGuid()}";

        var first = await bugs.SubmitAsync(ada, "ada", "Timer keeps running", "Steps...", "/floors/setup");
        var second = await bugs.SubmitAsync(ada, "ada", "Typo on floor 3", "Steps...", null);
        var hers = await bugs.SubmitAsync(grace, "grace", "Door won't open", "Steps...", "/rooms/hall-of-doors");
        await bugs.ReviewAsync(first.Id, BugStatus.Confirmed);
        await bugs.ReviewAsync(second.Id, BugStatus.Confirmed);
        await bugs.ReviewAsync(hers.Id, BugStatus.Rejected);
        var missing = await bugs.ReviewAsync(-1, BugStatus.Confirmed);

        var mine = await bugs.ForPlayerAsync(ada);
        var hall = (await bugs.HallAsync()).Where(b => b.PlayerId == ada || b.PlayerId == grace).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(first.Status, Is.EqualTo(BugStatus.Pending));
            Assert.That(first.Page, Is.EqualTo("/floors/setup"));
            Assert.That(mine, Has.Count.EqualTo(2));
            Assert.That(mine.All(r => r.Status == BugStatus.Confirmed && r.ReviewedAt is not null));
            Assert.That(missing, Is.False);
            Assert.That(hall, Has.Count.EqualTo(1), "a rejected report doesn't count");
            Assert.That(hall[0].Login, Is.EqualTo("ada"));
            Assert.That(hall[0].Confirmed, Is.EqualTo(2));
        });
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
    public async Task CertificatesAreReprintedAndOldIdsStillFindThem()
    {
        var records = new PostgresPlayerRecords(db);
        var player = $"test-{Guid.NewGuid()}";
        var issuedAt = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
        var legacyId = Convert.ToHexString(Guid.NewGuid().ToByteArray())[..16].ToLowerInvariant();

        await records.IssueCertificateAsync(new(legacyId, player, "ada", "Ada", 6, 900, issuedAt));
        // An upgrade: new id and name, the old id kept. The stored date stays.
        var upgraded = await records.IssueCertificateAsync(new(
            CertificateRules.NewId(issuedAt), player, "ada", "Ada Lovelace", 6, 950, issuedAt.AddDays(1), legacyId));
        // A plain rename later doesn't lose the old id.
        var renamed = await records.IssueCertificateAsync(new(upgraded.Id, player, "ada", "Ada L.", 6, 950, issuedAt,
            Language: CodeLanguage.TypeScript));
        var byOldId = await records.CertificateAsync(legacyId);

        Assert.Multiple(() =>
        {
            Assert.That(upgraded.Id, Does.StartWith("PDC-20261030-"));
            Assert.That(upgraded.IssuedAt, Is.EqualTo(issuedAt));
            Assert.That(renamed.PreviousId, Is.EqualTo(legacyId));
            Assert.That(upgraded.Language, Is.EqualTo(CodeLanguage.CSharp), "the default");
            Assert.That(renamed.Language, Is.EqualTo(CodeLanguage.TypeScript), "a reprint can change it");
            Assert.That(byOldId, Is.EqualTo(renamed));
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

    [Test]
    public async Task FeedbackIsStored()
    {
        var message = $"test-{Guid.NewGuid()}";
        await new PostgresFeedbackStore(db).AddAsync(new(5, message, null, "/rooms/welcome-matt", "ada", DateTimeOffset.UtcNow));

        await using var read = db.CreateCommand("select rating, email, page, login from feedback where message = $1");
        read.Parameters.AddWithValue(message);
        await using var row = await read.ExecuteReaderAsync();
        Assert.That(await row.ReadAsync(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(row.GetInt16(0), Is.EqualTo(5));
            Assert.That(row.IsDBNull(1), Is.True);
            Assert.That(row.GetString(2), Is.EqualTo("/rooms/welcome-matt"));
            Assert.That(row.GetString(3), Is.EqualTo("ada"));
        });
    }

    [Test]
    public async Task EachTierIsAStoredCertificateOfItsOwn()
    {
        var records = new PostgresPlayerRecords(db);
        var player = $"test-{Guid.NewGuid()}";
        var issuedAt = DateTimeOffset.UtcNow;

        await records.IssueCertificateAsync(new(CertificateRules.NewId(issuedAt), player, "ada", "Ada", 7, 600, issuedAt));
        await records.IssueCertificateAsync(new(CertificateRules.NewId(issuedAt), player, "ada", "Ada", 13, 1100, issuedAt,
            Tier: CertificateTier.Journeyman));
        var all = await records.CertificatesForAsync(player);
        var journeyman = await records.CertificateForAsync(player, CertificateTier.Journeyman);

        Assert.Multiple(async () =>
        {
            Assert.That(all.Select(c => c.Tier), Is.EqualTo(new[] { CertificateTier.Apprentice, CertificateTier.Journeyman }));
            Assert.That(journeyman!.Floors, Is.EqualTo(13));
            Assert.That((await records.CertificateAsync(journeyman.Id))!.Tier, Is.EqualTo(CertificateTier.Journeyman));
        });
    }

    [Test]
    public async Task AReprintCanBringAnEarlyCertificateUpToTheFullTier()
    {
        var records = new PostgresPlayerRecords(db);
        var player = $"test-{Guid.NewGuid()}";
        var issuedAt = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
        var id = CertificateRules.NewId(issuedAt);

        await records.IssueCertificateAsync(new(id, player, "ada", "Ada", 6, 510, issuedAt));
        var updatedAt = new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
        var completed = await records.IssueCertificateAsync(new(id, player, "ada", "Ada", 7, 640, issuedAt, UpdatedAt: updatedAt));
        // A later rename doesn't clear the update date.
        var renamed = await records.IssueCertificateAsync(new(id, player, "ada", "Ada L.", 7, 640, issuedAt));

        Assert.Multiple(() =>
        {
            Assert.That(completed.UpdatedAt, Is.EqualTo(updatedAt));
            Assert.That(renamed.UpdatedAt, Is.EqualTo(updatedAt));
            Assert.That(completed.Floors, Is.EqualTo(7));
            Assert.That(completed.Score, Is.EqualTo(640));
            Assert.That(completed.Id, Is.EqualTo(id));
        });
    }

    [Test]
    public async Task CertificatesUpgradedBeforeTheUpdateDateExistedGetOne()
    {
        var records = new PostgresPlayerRecords(db);
        var october30 = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
        var upgraded = $"test-{Guid.NewGuid()}";
        var early = $"test-{Guid.NewGuid()}";
        var later = $"test-{Guid.NewGuid()}";
        // Covers floor 7 but was issued before floor 7 opened: it must have been upgraded.
        await records.IssueCertificateAsync(new(CertificateRules.NewId(october30), upgraded, "a", "A", 7, 640, october30));
        // Still early: never upgraded.
        await records.IssueCertificateAsync(new(CertificateRules.NewId(october30), early, "b", "B", 6, 510, october30));
        // Issued after floor 7 opened: earned whole, nothing to note.
        var november3 = october30.AddDays(4);
        await records.IssueCertificateAsync(new(CertificateRules.NewId(november3), later, "c", "C", 7, 640, november3));

        await PostgresDatabase.EnsureCreatedAsync(db);

        Assert.Multiple(async () =>
        {
            Assert.That((await records.CertificateForAsync(upgraded))!.UpdatedAt?.UtcDateTime.Date, Is.EqualTo(new DateTime(2026, 11, 2)));
            Assert.That((await records.CertificateForAsync(early))!.UpdatedAt, Is.Null);
            Assert.That((await records.CertificateForAsync(later))!.UpdatedAt, Is.Null);
        });
    }
}
