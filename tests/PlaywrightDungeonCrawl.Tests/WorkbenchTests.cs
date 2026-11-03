using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PlaywrightDungeonCrawl.Tests;

/// <summary>Floor 9's workbench: bolts, then the right job, then the temperature, each checked on the server.</summary>
public class WorkbenchTests
{
    private const string Room = "/api/rooms/tinkerers-workbench/attempts";

    [Test]
    public async Task BoltsThenTheJobThenTheTemperatureEarnTheFlag()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var client = dungeon.CreateClient();
        var (id, briefing) = await StartAsync(client);

        var cover = await ClearAsync(client, id, "loosened");
        var options = cover.GetProperty("options").EnumerateArray().Select(o => o.GetString()).ToArray();
        var note = cover.GetProperty("note").GetString()!;
        var furnace = await ClearAsync(client, id, note);
        var temperature = furnace.GetProperty("temperature").GetInt32();

        var reply = await client.PostAsJsonAsync($"{Room}/{id}/answer", new { Answer = temperature.ToString() });
        var body = await reply.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(briefing.GetProperty("bolts").GetInt32(), Is.EqualTo(4));
            Assert.That(options, Has.Length.EqualTo(5).And.Contains(note));
            Assert.That(temperature % 10, Is.Zero);
            Assert.That(temperature, Is.InRange(100, 450), "never at either end of the dial");
            Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.GetProperty("flag").GetString(), Does.Match(@"^PDC\{[0-9a-f]{12}\}$"));
        });
    }

    [Test]
    public async Task TheWrongJobSpringsTheTrap()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var client = dungeon.CreateClient();
        var (id, _) = await StartAsync(client);
        var cover = await ClearAsync(client, id, "loosened");
        var note = cover.GetProperty("note").GetString();
        var wrong = cover.GetProperty("options").EnumerateArray().Select(o => o.GetString()).First(o => o != note);

        var reply = await client.PostAsJsonAsync($"{Room}/{id}/trials", new { Answer = wrong });

        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    [Test]
    public async Task TheTemperatureDoesntCountBeforeTheJobs()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var client = dungeon.CreateClient();
        var (id, _) = await StartAsync(client);

        var reply = await client.PostAsJsonAsync($"{Room}/{id}/answer", new { Answer = "250" });

        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    private static async Task<(Guid Id, JsonElement Briefing)> StartAsync(HttpClient client)
    {
        var attempt = await (await client.PostAsync(Room, null)).Content.ReadFromJsonAsync<JsonElement>();
        return (attempt.GetProperty("attemptId").GetGuid(), attempt.GetProperty("briefing"));
    }

    private static async Task<JsonElement> ClearAsync(HttpClient client, Guid id, string answer)
    {
        var reply = await client.PostAsJsonAsync($"{Room}/{id}/trials", new { Answer = answer });
        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"trial answer {answer}");
        return (await reply.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("unlocks");
    }
}
