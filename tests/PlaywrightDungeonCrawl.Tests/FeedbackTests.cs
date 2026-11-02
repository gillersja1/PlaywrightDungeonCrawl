using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using PlaywrightDungeonCrawl.Dungeon;
using PlaywrightDungeonCrawl.Pages;

namespace PlaywrightDungeonCrawl.Tests;

public class FeedbackTests
{
    private SignedInDungeon dungeon = null!;

    [SetUp]
    public void StartDungeon() => dungeon = new SignedInDungeon();

    [TearDown]
    public void StopDungeon() => dungeon.Dispose();

    private InMemoryFeedbackStore Store => (InMemoryFeedbackStore)dungeon.Services.GetRequiredService<IFeedbackStore>();

    private static async Task<string> SendAsync(HttpClient client, Dictionary<string, string> fields, string from = "/rooms/hall-of-doors")
    {
        var page = $"/feedback?from={Uri.EscapeDataString(from)}";
        var html = await client.GetStringAsync(page);
        fields["__RequestVerificationToken"] =
            Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        fields.TryAdd("From", from);
        var reply = await client.PostAsync("/feedback", new FormUrlEncodedContent(fields));
        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return await reply.Content.ReadAsStringAsync();
    }

    [Test]
    public async Task Feedback_is_saved_with_the_page_it_came_from()
    {
        var html = await SendAsync(dungeon.ClientFor("ada"), new() { ["Rating"] = "4", ["Message"] = "  The doors are great.  ", ["Email"] = "ada@example.com" });

        Assert.That(html, Does.Contain("Thanks, crawler."));
        var entry = Store.Entries.Single();
        Assert.That(entry, Is.EqualTo(entry with { Rating = 4, Message = "The doors are great.", Email = "ada@example.com", Page = "/rooms/hall-of-doors", Login = "ada" }));
    }

    [Test]
    public async Task Rating_and_email_are_optional_and_sign_in_is_not_needed()
    {
        await SendAsync(dungeon.ClientFor(null), new() { ["Message"] = "Hi" });

        var entry = Store.Entries.Single();
        Assert.That((entry.Rating, entry.Email, entry.Login), Is.EqualTo(((int?)null, (string?)null, (string?)null)));
    }

    [Test]
    public async Task An_empty_message_or_a_bad_email_is_sent_back_with_a_reason()
    {
        var client = dungeon.ClientFor(null);
        Assert.That(await SendAsync(client, new() { ["Message"] = " " }), Does.Contain("Tell the dungeon something first."));
        Assert.That(await SendAsync(client, new() { ["Message"] = "Hi", ["Email"] = "not-an-email" }), Does.Contain("doesn&#x27;t look like an email"));
        Assert.That(Store.Entries, Is.Empty);
    }

    [Test]
    public async Task Bots_that_fill_the_hidden_field_are_thanked_and_ignored()
    {
        var html = await SendAsync(dungeon.ClientFor(null), new() { ["Message"] = "Buy pills", ["Website"] = "http://spam.example" });

        Assert.That(html, Does.Contain("Thanks, crawler."));
        Assert.That(Store.Entries, Is.Empty);
    }

    [Test]
    public async Task Off_site_return_paths_fall_back_to_the_respawn_room()
    {
        await SendAsync(dungeon.ClientFor(null), new() { ["Message"] = "Hi" }, from: "https://evil.example/");

        Assert.That(Store.Entries.Single().Page, Is.EqualTo("/"));
    }

    [Test]
    public async Task One_visitor_can_only_send_a_few_at_a_time()
    {
        var client = dungeon.ClientFor(null);
        for (var i = 0; i < FeedbackModel.PerWindow; i++)
        {
            await SendAsync(client, new() { ["Message"] = $"Note {i}" });
        }

        Assert.That(await SendAsync(client, new() { ["Message"] = "One too many" }), Does.Contain("a lot of feedback at once"));
        Assert.That(Store.Entries, Has.Count.EqualTo(FeedbackModel.PerWindow));
    }

    [Test]
    public async Task Every_page_links_to_feedback_from_itself()
    {
        var html = await dungeon.ClientFor(null).GetStringAsync("/leaderboard");

        Assert.That(html, Does.Contain("href=\"/feedback?from=%2Fleaderboard\""));
    }

    [TestCase("a@b.co", true)]
    [TestCase("first.last@mail.example.org", true)]
    [TestCase("no-at-sign.com", false)]
    [TestCase("@example.com", false)]
    [TestCase("a@b", false)]
    [TestCase("a@b.", false)]
    [TestCase("a b@c.com", false)]
    public void Email_check_is_light_but_catches_typos(string email, bool ok) =>
        Assert.That(FeedbackRules.LooksLikeEmail(email), Is.EqualTo(ok));
}
