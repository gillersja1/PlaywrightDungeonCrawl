using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Tests;

public class ShopTests
{
    private SignedInDungeon site = null!;

    [OneTimeSetUp]
    public void StartDungeon() => site = new SignedInDungeon();

    [OneTimeTearDown]
    public void StopDungeon() => site.Dispose();

    private DungeonCatalog Catalog => site.Services.GetRequiredService<DungeonCatalog>();

    private static PlayerProgress With(Dictionary<string, int> loot, Dictionary<string, string> equipped) =>
        PlayerProgress.Empty with { Loot = loot, Equipped = equipped };

    [Test]
    public void Prices_are_positive_and_ids_unique()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Shop.Items.Select(i => i.Id), Is.Unique);
            Assert.That(Shop.Items.Select(i => i.Price), Is.All.Positive);
            Assert.That(Shop.Items.Where(i => i.Slot == LootSlot.Torch).Select(i => (i.Color, i.Dim)), Has.None.Matches<(string?, string?)>(c => c.Item1 is null || c.Item2 is null));
        });
    }

    [Test]
    public void Sanitize_drops_unknown_loot_and_impossible_prices()
    {
        var progress = With(
            new() { ["title-lockpicker"] = 40, ["title-made-up"] = 1, ["torch-ghostfire"] = 9999, ["torch-verdigris"] = -5 },
            []).Sanitize(Catalog);

        Assert.That(progress.Loot, Is.EqualTo(new Dictionary<string, int> { ["title-lockpicker"] = 40 }));
    }

    [Test]
    public void Sanitize_only_keeps_owned_items_in_the_slot_they_fit()
    {
        var progress = With(
            new() { ["title-lockpicker"] = 40, ["torch-ghostfire"] = 50 },
            new() { ["Title"] = "title-lockpicker", ["torch"] = "title-lockpicker", ["hat"] = "title-lockpicker", ["0"] = "" }).Sanitize(Catalog);

        Assert.That(progress.Equipped, Is.EqualTo(new Dictionary<string, string> { ["title"] = "title-lockpicker" }));
    }

    [Test]
    public void Wearing_an_item_you_dont_own_is_dropped()
    {
        var progress = With([], new() { ["title"] = "title-flake-eater" }).Sanitize(Catalog);

        Assert.That(progress.Equipped, Is.Empty);
    }

    [Test]
    public void Merge_keeps_every_purchase_at_its_first_price_and_the_latest_choice_of_what_to_wear()
    {
        var saved = With(new() { ["title-lockpicker"] = 40 }, new() { ["title"] = "title-lockpicker" });
        var incoming = With(new() { ["title-lockpicker"] = 0, ["torch-ghostfire"] = 50 }, new() { ["title"] = "", ["torch"] = "torch-ghostfire" });

        var merged = saved.Merge(incoming);

        Assert.Multiple(() =>
        {
            Assert.That(merged.Loot, Is.EqualTo(new Dictionary<string, int> { ["title-lockpicker"] = 40, ["torch-ghostfire"] = 50 }));
            // Taking the title off sticks, rather than the saved copy putting it back on.
            Assert.That(merged.Equipped, Is.EqualTo(new Dictionary<string, string> { ["title"] = "", ["torch"] = "torch-ghostfire" }));
        });
    }

    [Test]
    public void The_leaderboard_shows_a_title_only_when_the_verified_score_covers_the_shopping()
    {
        var progress = With(new() { ["title-lockpicker"] = 40, ["torch-ghostfire"] = 50 }, new() { ["title"] = "title-lockpicker" });

        Assert.Multiple(() =>
        {
            Assert.That(Shop.VerifiedTitle(progress, verifiedScore: 90), Is.EqualTo("Lockpicker"));
            Assert.That(Shop.VerifiedTitle(progress, verifiedScore: 89), Is.Null);
            Assert.That(Shop.VerifiedTitle(progress with { Equipped = new() { ["title"] = "" } }, 500), Is.Null);
        });
    }

    [Test]
    public async Task Purchases_sync_with_the_account()
    {
        using var dungeon = new SignedInDungeon();
        var client = dungeon.ClientFor("ada");

        var reply = await client.PutAsJsonAsync("/api/progress", new
        {
            scrolls = new[] { "setup" },
            riddles = Array.Empty<string>(),
            treasures = Array.Empty<string>(),
            help = new Dictionary<string, int>(),
            loot = new Dictionary<string, int> { ["torch-ghostfire"] = 50, ["title-fake"] = 0 },
            equipped = new Dictionary<string, string> { ["torch"] = "torch-ghostfire" },
        });
        var saved = await dungeon.Services.GetRequiredService<IProgressStore>().GetAsync("id-ada");

        Assert.Multiple(async () =>
        {
            Assert.That(reply.IsSuccessStatusCode, Is.True);
            Assert.That(saved.Loot.Keys, Is.EqualTo(new[] { "torch-ghostfire" }));
            Assert.That(saved.Equipped["torch"], Is.EqualTo("torch-ghostfire"));
            Assert.That(await reply.Content.ReadAsStringAsync(), Does.Contain("\"equipped\":{\"torch\":\"torch-ghostfire\"}"));
        });
    }

    [Test]
    public async Task The_respawn_room_lists_every_item_with_its_price()
    {
        using var dungeon = new SignedInDungeon();
        var html = await dungeon.ClientFor(null).GetStringAsync("/");

        Assert.Multiple(() =>
        {
            foreach (var item in Shop.Items)
            {
                Assert.That(html, Does.Contain($"data-loot=\"{item.Id}\""));
                Assert.That(html, Does.Contain($"aria-label=\"Buy {item.Name} for {item.Price} XP\""));
            }
            Assert.That(html, Does.Not.Contain("Closed for stocktaking"));
        });
    }

    [Test]
    public async Task The_leaderboard_shows_a_worn_title_as_a_tag_after_the_name()
    {
        using var dungeon = new SignedInDungeon();
        var records = dungeon.Services.GetRequiredService<IPlayerRecords>();
        var catalog = dungeon.Services.GetRequiredService<DungeonCatalog>();
        // Three verified floors and their scrolls: 255 XP, enough to have paid for a 250 XP title.
        foreach (var floor in catalog.Floors.Take(3))
        {
            await records.RecordAsync("id-ada", "ada", Achievement.Riddle, floor.Slug);
            await records.RecordAsync("id-ada", "ada", Achievement.Treasure, floor.Room.Slug);
        }
        await records.SetOnLeaderboardAsync("id-ada", "ada", true);
        await dungeon.ClientFor("ada").PutAsJsonAsync("/api/progress", new
        {
            scrolls = catalog.Floors.Take(3).Select(f => f.Slug).ToArray(),
            riddles = Array.Empty<string>(),
            treasures = Array.Empty<string>(),
            help = new Dictionary<string, int>(),
            loot = new Dictionary<string, int> { ["title-flake-eater"] = 250 },
            equipped = new Dictionary<string, string> { ["title"] = "title-flake-eater" },
        });

        var html = await dungeon.ClientFor(null).GetStringAsync("/leaderboard");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("<span class=\"worn-title\" title=\"A title from the loot shop\">Ate the Flaky Test</span>"));
            Assert.That(html, Does.Not.Contain("the Ate the Flaky Test"));
        });
    }
}
