using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Tests;

/// <summary>
/// Floor 16's pit: one hunt, five heads, each faced by its own hand. The heads rear once all five are faced, and
/// all five must fall within the window.
/// </summary>
public class HydraTests
{
    private const string Attempts = "/api/rooms/severing-pit/attempts";
    private const string Hunts = "/api/rooms/severing-pit/hunts";

    /// <summary>A clock the test moves by hand, so the hydra loses patience without real waiting.</summary>
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed record Crawler(HttpClient Client, Guid Attempt);

    private WebApplicationFactory<Program> dungeon = null!;
    private ManualClock clock = null!;
    private Guid hunt;

    [SetUp]
    public async Task OpenThePit()
    {
        clock = new ManualClock();
        dungeon = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(clock)));
        var reply = await dungeon.CreateClient().PostAsync(Hunts, null);
        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        hunt = (await reply.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("huntId").GetGuid();
    }

    [TearDown]
    public async Task CloseThePit() => await dungeon.DisposeAsync();

    [Test]
    public async Task FiveCleanHandsAtOnceSlayTheHydra()
    {
        var crawlers = await FaceAllAsync();
        var rearing = await StateAsync(crawlers[0]);

        foreach (var (crawler, head) in crawlers.Select((c, i) => (c, i + 1)))
        {
            Assert.That((await CutAsync(crawler, head)).StatusCode, Is.EqualTo(HttpStatusCode.OK), $"head {head}");
            clock.Now += TimeSpan.FromMilliseconds(500);
        }
        var state = await StateAsync(crawlers[0]);
        var fang = state.GetProperty("fang").GetString();
        var answer = await crawlers[0].Client.PostAsJsonAsync($"{Attempts}/{crawlers[0].Attempt}/answer", new { Answer = fang });

        Assert.Multiple(() =>
        {
            Assert.That(rearing.GetProperty("state").GetString(), Is.EqualTo("rearing"));
            Assert.That(state.GetProperty("state").GetString(), Is.EqualTo("slain"));
            Assert.That(fang, Is.Not.Null.And.Not.Empty);
            Assert.That(answer.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task OneHandCantFaceTwoHeads()
    {
        var crawler = await EnterAsync();
        var second = new Crawler(crawler.Client, await StartAttemptAsync(crawler.Client));

        var first = await FaceAsync(crawler, 1);
        var again = await FaceAsync(second, 2);

        Assert.Multiple(async () =>
        {
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(again.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(await again.Content.ReadAsStringAsync(), Does.Contain("scarred"));
        });
    }

    [Test]
    public async Task HeadsDontRearUntilEveryCrawlerIsThere()
    {
        var crawler = await EnterAsync();
        await FaceAsync(crawler, 1);

        var cut = await CutAsync(crawler, 1);

        Assert.That(cut.StatusCode, Is.EqualTo((HttpStatusCode)425));
    }

    [Test]
    public async Task CrawlersArrivingOneAfterAnotherAreTooLate()
    {
        var early = await EnterAsync();
        await FaceAsync(early, 1);
        clock.Now += SeveringPit.Patience + TimeSpan.FromSeconds(1);

        var late = await FaceAsync(await EnterAsync(), 2);
        var state = await StateAsync(early);

        Assert.Multiple(() =>
        {
            Assert.That(late.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(state.GetProperty("state").GetString(), Is.EqualTo("regrown"));
        });
    }

    [Test]
    public async Task HeadsCutTooFarApartGrowBack()
    {
        var crawlers = await FaceAllAsync();

        await CutAsync(crawlers[0], 1);
        clock.Now += SeveringPit.Window + TimeSpan.FromSeconds(1);
        var late = await CutAsync(crawlers[1], 2);
        var state = await StateAsync(crawlers[0]);

        Assert.Multiple(() =>
        {
            Assert.That(late.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(state.GetProperty("state").GetString(), Is.EqualTo("regrown"));
            Assert.That(state.GetProperty("fang").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });
    }

    [Test]
    public async Task OnlyTheCrawlerFacingAHeadCanCutIt()
    {
        var crawlers = await FaceAllAsync();

        var wrong = await CutAsync(crawlers[0], 2);

        Assert.That(wrong.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task OnlyACutterGetsTheFang()
    {
        var crawlers = await FaceAllAsync();
        foreach (var (crawler, head) in crawlers.Select((c, i) => (c, i + 1)))
        {
            await CutAsync(crawler, head);
        }
        var bystander = await EnterAsync();

        var state = await StateAsync(bystander);

        Assert.Multiple(() =>
        {
            Assert.That(state.GetProperty("state").GetString(), Is.EqualTo("slain"));
            Assert.That(state.GetProperty("fang").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });
    }

    [Test]
    public async Task UnknownHuntsHeadsAndAttemptsAreNotFound()
    {
        var crawler = await EnterAsync();
        var client = crawler.Client;

        Assert.Multiple(async () =>
        {
            Assert.That((await client.PostAsJsonAsync($"{Hunts}/{Guid.NewGuid()}/heads/1/face", new { crawler.Attempt })).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await FaceAsync(crawler, 6)).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await FaceAsync(crawler with { Attempt = Guid.NewGuid() }, 1)).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await client.GetAsync($"{Hunts}/{Guid.NewGuid()}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public void ThePitOffersNoCurses() =>
        Assert.That(Curses.For(new SeveringPit()), Is.Empty);

    /// <summary>A new crawler: their own client (their own cookies, like a fresh browser context) and attempt.</summary>
    private async Task<Crawler> EnterAsync()
    {
        var client = dungeon.CreateClient();
        return new Crawler(client, await StartAttemptAsync(client));
    }

    private async Task<List<Crawler>> FaceAllAsync()
    {
        var crawlers = new List<Crawler>();
        for (var head = 1; head <= SeveringPit.Heads; head++)
        {
            var crawler = await EnterAsync();
            Assert.That((await FaceAsync(crawler, head)).StatusCode, Is.EqualTo(HttpStatusCode.OK), $"face {head}");
            crawlers.Add(crawler);
        }
        return crawlers;
    }

    private static async Task<Guid> StartAttemptAsync(HttpClient client) =>
        (await (await client.PostAsync(Attempts, null)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("attemptId").GetGuid();

    private Task<HttpResponseMessage> FaceAsync(Crawler crawler, int head) =>
        crawler.Client.PostAsJsonAsync($"{Hunts}/{hunt}/heads/{head}/face", new { crawler.Attempt });

    private Task<HttpResponseMessage> CutAsync(Crawler crawler, int head) =>
        crawler.Client.PostAsJsonAsync($"{Hunts}/{hunt}/heads/{head}/cut", new { crawler.Attempt });

    private async Task<JsonElement> StateAsync(Crawler crawler) =>
        await crawler.Client.GetFromJsonAsync<JsonElement>($"{Hunts}/{hunt}?attempt={crawler.Attempt}");
}
