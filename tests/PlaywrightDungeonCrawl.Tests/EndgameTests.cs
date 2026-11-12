using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Tests;

/// <summary>Floor 18's Belfry (the traveler's readings) and floor 19's Flake (three rounds through the trials).</summary>
public class EndgameTests
{
    private WebApplicationFactory<Program> dungeon = null!;
    private HttpClient client = null!;

    [SetUp]
    public void Open()
    {
        dungeon = new WebApplicationFactory<Program>();
        client = dungeon.CreateClient();
    }

    [TearDown]
    public async Task Close()
    {
        client.Dispose();
        await dungeon.DisposeAsync();
    }

    private static readonly Belfry.Traveler Tokyo = Belfry.Travelers.Single(t => t.City == "Tokyo");

    /// <summary>Midnight in Tokyo is 15:00 UTC.</summary>
    private static readonly long TokyoMidnight = new DateTimeOffset(2026, 1, 1, 15, 0, 20, TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static string Readings(string timeZone = "Asia/Tokyo", string language = "ja-JP", double? lat = 35.68, double? lng = 139.7,
        bool touch = true, int width = 390, long? epoch = null) =>
        JsonSerializer.Serialize(new Belfry.Readings(timeZone, language, lat, lng, touch, width, epoch ?? TokyoMidnight),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Test]
    public void TheRightTravelerAtMidnightPasses() =>
        Assert.That(Belfry.Check(Tokyo, Readings()), Is.Empty);

    [Test]
    public void EveryWrongReadingIsNamed()
    {
        var oneHourEarly = TokyoMidnight - 3_600_000;
        Assert.Multiple(() =>
        {
            Assert.That(Belfry.Check(Tokyo, Readings(timeZone: "Europe/London")), Has.One.Contains("Tokyo time"));
            Assert.That(Belfry.Check(Tokyo, Readings(language: "en-US")), Has.One.Contains("ja-JP"));
            Assert.That(Belfry.Check(Tokyo, Readings(lat: 34.69, lng: 135.5)), Has.One.Contains("standing in Tokyo"));
            Assert.That(Belfry.Check(Tokyo, Readings(lat: null, lng: null)), Has.One.Contains("standing in Tokyo"));
            Assert.That(Belfry.Check(Tokyo, Readings(touch: false)), Has.One.Contains("phone"));
            Assert.That(Belfry.Check(Tokyo, Readings(width: 1280)), Has.One.Contains("phone"));
            Assert.That(Belfry.Check(Tokyo, Readings(epoch: oneHourEarly)), Has.One.Contains("23:00"));
            Assert.That(Belfry.Check(Tokyo, "not json"), Has.One.Contains("papers"));
        });
    }

    [Test]
    public async Task TheBellGivesItsWordOnlyToTheRightTraveler()
    {
        var attempt = await (await client.PostAsync("/api/rooms/belfry/attempts", null)).Content.ReadFromJsonAsync<JsonElement>();
        var id = attempt.GetProperty("attemptId").GetGuid();
        var briefing = attempt.GetProperty("briefing");
        var traveler = Belfry.Travelers.Single(t => t.City == briefing.GetProperty("city").GetString());
        var zone = TimeZoneInfo.FindSystemTimeZoneById(traveler.TimeZone);
        var midnight = TimeZoneInfo.ConvertTime(new DateTimeOffset(2026, 3, 3, 12, 0, 0, TimeSpan.Zero), zone).Date.AddDays(1);
        var epoch = new DateTimeOffset(midnight, zone.GetUtcOffset(midnight)).ToUnixTimeMilliseconds();
        var good = JsonSerializer.Serialize(new Belfry.Readings(traveler.TimeZone, traveler.Locale, traveler.Latitude, traveler.Longitude, true, 390, epoch),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var papers = await client.PostAsync($"/api/rooms/belfry/attempts/{id}/papers", new StringContent(good));
        var trial = await (await client.PostAsJsonAsync($"/api/rooms/belfry/attempts/{id}/trials", new { Answer = good })).Content.ReadFromJsonAsync<JsonElement>();
        var word = trial.GetProperty("unlocks").GetProperty("word").GetString();
        var answer = await (await client.PostAsJsonAsync($"/api/rooms/belfry/attempts/{id}/answer", new { Answer = word })).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(async () =>
        {
            Assert.That((await papers.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("problems").GetArrayLength(), Is.Zero);
            Assert.That(trial.GetProperty("outcome").GetString(), Is.EqualTo("correct"));
            Assert.That(answer.GetProperty("outcome").GetString(), Is.EqualTo("correct"));
        });
    }

    [Test]
    public async Task TheFlakeTakesThreeTrueNamesInARow()
    {
        var attempt = await (await client.PostAsync("/api/rooms/the-flake/attempts", null)).Content.ReadFromJsonAsync<JsonElement>();
        var id = attempt.GetProperty("attemptId").GetGuid();
        var round = attempt.GetProperty("briefing").GetProperty("first");
        var outcomes = new List<string?>();

        for (var i = 1; i < TheFlake.Rounds; i++)
        {
            var reply = await (await client.PostAsJsonAsync($"/api/rooms/the-flake/attempts/{id}/trials",
                new { Answer = round.GetProperty("trueName").GetString() })).Content.ReadFromJsonAsync<JsonElement>();
            outcomes.Add(reply.GetProperty("outcome").GetString());
            round = reply.GetProperty("unlocks").GetProperty("next");
        }
        var last = await (await client.PostAsJsonAsync($"/api/rooms/the-flake/attempts/{id}/answer",
            new { Answer = round.GetProperty("trueName").GetString() })).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(outcomes, Is.All.EqualTo("correct"));
            Assert.That(last.GetProperty("outcome").GetString(), Is.EqualTo("correct"));
        });
    }

    [Test]
    public async Task SpeakingTheFeintSpringsTheTrap()
    {
        var attempt = await (await client.PostAsync("/api/rooms/the-flake/attempts", null)).Content.ReadFromJsonAsync<JsonElement>();
        var id = attempt.GetProperty("attemptId").GetGuid();
        var feint = attempt.GetProperty("briefing").GetProperty("first").GetProperty("feint").GetString();

        var reply = await (await client.PostAsJsonAsync($"/api/rooms/the-flake/attempts/{id}/trials", new { Answer = feint }))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.That(reply.GetProperty("outcome").GetString(), Is.EqualTo("wrong"));
    }

    [Test]
    public async Task OnlyTheBelfryMayAskWhereYouAre()
    {
        var belfry = await client.GetAsync("/rooms/belfry");
        var home = await client.GetAsync("/");

        Assert.Multiple(() =>
        {
            Assert.That(belfry.Headers.GetValues("Permissions-Policy").Single(), Does.Contain("geolocation=(self)"));
            Assert.That(home.Headers.GetValues("Permissions-Policy").Single(), Does.Contain("geolocation=()"));
        });
    }

    [Test]
    public void EveryFloorIsDug()
    {
        var catalog = dungeon.Services.GetService(typeof(DungeonCatalog)) as DungeonCatalog;
        Assert.Multiple(() =>
        {
            Assert.That(catalog!.Floors.Select(f => f.Number), Is.EqualTo(Enumerable.Range(1, 19)));
            Assert.That(catalog.SealedFloors, Is.Empty);
        });
    }
}
