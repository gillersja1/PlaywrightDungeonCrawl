using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PlaywrightDungeonCrawl.Tests;

/// <summary>Floors 13 and 14: the stage's real replies, and the drafting table's work order.</summary>
public class StageAndAtelierTests
{
    [Test]
    public async Task TheRealActKeepsTheCurtainDownAndCarriesTheWord()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var client = dungeon.CreateClient();
        var id = (await StartAsync(client, "illusionists-stage")).GetProperty("attemptId").GetGuid();
        var room = $"/api/rooms/illusionists-stage/attempts/{id}";

        var act = await client.GetFromJsonAsync<JsonElement>($"{room}/act");
        var heckler = await client.GetAsync($"{room}/heckler");
        var reply = await client.PostAsJsonAsync($"{room}/answer", new { Answer = act.GetProperty("word").GetString() });

        Assert.Multiple(() =>
        {
            Assert.That(act.GetProperty("curtain").GetString(), Is.EqualTo("down"));
            Assert.That(heckler.StatusCode, Is.EqualTo(HttpStatusCode.OK), "the heckler always turns up");
            Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task TheStageIsClosedForUnknownAttempts()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var reply = await dungeon.CreateClient().GetAsync($"/api/rooms/illusionists-stage/attempts/{Guid.NewGuid()}/act");

        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [TestCase(false, HttpStatusCode.OK)]
    [TestCase(true, HttpStatusCode.UnprocessableEntity)]
    public async Task EveryBlueprintMustMatchTheWorkOrder(bool oneWrong, HttpStatusCode expected)
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var client = dungeon.CreateClient();
        var attempt = await StartAsync(client, "drafting-table");
        var blueprints = attempt.GetProperty("briefing").GetProperty("blueprints").EnumerateArray()
            .Select(b => new
            {
                name = b.GetProperty("name").GetString()!,
                material = b.GetProperty("material").GetString(),
                length = b.GetProperty("length").GetInt32(),
                square = b.GetProperty("square").GetBoolean(),
            })
            .OrderBy(b => b.name, StringComparer.Ordinal)
            .Select((b, i) => oneWrong && i == 0 ? b with { length = b.length + 1 } : b)
            .ToArray();

        // The page sends the stamped panels sorted by name, the way JSON.stringify writes them.
        var reply = await client.PostAsJsonAsync($"/api/rooms/drafting-table/attempts/{attempt.GetProperty("attemptId").GetGuid()}/answer",
            new { Answer = JsonSerializer.Serialize(blueprints) });

        Assert.Multiple(() =>
        {
            Assert.That(blueprints, Has.Length.EqualTo(4));
            Assert.That(reply.StatusCode, Is.EqualTo(expected));
        });
    }

    private static async Task<JsonElement> StartAsync(HttpClient client, string slug) =>
        await (await client.PostAsync($"/api/rooms/{slug}/attempts", null)).Content.ReadFromJsonAsync<JsonElement>();
}
