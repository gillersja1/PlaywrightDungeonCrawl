using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PlaywrightDungeonCrawl.Tests;

/// <summary>The boss room's trials: in order, checked on the server, one strike and you're out.</summary>
public class GauntletTests
{
    private const string Room = "/api/rooms/wardens-gauntlet/attempts";

    [Test]
    public async Task AllFiveTrialsInOrderEarnTheFlag()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var client = dungeon.CreateClient();
        var (id, briefing) = await StartAsync(client);

        var eye = await ClearAsync(client, id, OpenGate(briefing));
        var oath = await ClearAsync(client, id, eye.GetProperty("sigil").GetString()!);
        var mirror = await ClearAsync(client, id, SwearOath(oath));
        var whispers = await ClearAsync(client, id, mirror.GetProperty("mirrorRune").GetString()!);
        var trueName = whispers.GetProperty("whispers").GetProperty("trueName").GetString()!;

        var reply = await client.PostAsJsonAsync($"{Room}/{id}/answer", new { Answer = trueName });
        var body = await reply.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(body.GetProperty("flag").GetString(), Does.Match(@"^PDC\{[0-9a-f]{12}\}$"));
    }

    [Test]
    public async Task TheFinalAnswerDoesntCountBeforeTheTrials()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var client = dungeon.CreateClient();
        var (id, briefing) = await StartAsync(client);
        var eye = await ClearAsync(client, id, OpenGate(briefing));

        // Even the right sigil, offered as the final answer, is a skip.
        var reply = await client.PostAsJsonAsync($"{Room}/{id}/answer", new { Answer = eye.GetProperty("sigil").GetString() });
        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    [Test]
    public async Task AWrongTrialUsesTheAttemptUp()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var client = dungeon.CreateClient();
        var (id, briefing) = await StartAsync(client);

        var wrong = await client.PostAsJsonAsync($"{Room}/{id}/trials", new { Answer = "gate-nope" });
        Assert.That(wrong.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));

        var retry = await client.PostAsJsonAsync($"{Room}/{id}/trials", new { Answer = OpenGate(briefing) });
        Assert.That(retry.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task RoomsWithoutTrialsRefuseThem()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var client = dungeon.CreateClient();
        var attempt = await (await client.PostAsync("/api/rooms/hall-of-doors/attempts", null)).Content.ReadFromJsonAsync<JsonElement>();

        var reply = await client.PostAsJsonAsync(
            $"/api/rooms/hall-of-doors/attempts/{attempt.GetProperty("attemptId").GetGuid()}/trials", new { Answer = "anything" });
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

    private static string OpenGate(JsonElement briefing)
    {
        var target = briefing.GetProperty("target").GetString();
        return briefing.GetProperty("gates").EnumerateArray()
            .Single(g => g.GetProperty("label").GetString() == target && !g.GetProperty("sealed").GetBoolean())
            .GetProperty("id").GetString()!;
    }

    private static string SwearOath(JsonElement unlocks)
    {
        var terms = unlocks.GetProperty("oath").EnumerateArray()
            .ToDictionary(t => t.GetProperty("label").GetString()!, t => t.GetProperty("value").GetString()!);
        return JsonSerializer.Serialize(new
        {
            title = terms["Warden's title"],
            guild = terms["Guild"],
            torches = int.Parse(terms["Torches carried"]),
            sworn = true,
        });
    }
}
