using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PlaywrightDungeonCrawl.Tests;

/// <summary>Floor 12's well: an API only. POST with the offering, follow the 201's Location, read the prophecy.</summary>
public class OracleTests
{
    private const string Room = "/api/rooms/oracles-well/attempts";

    [Test]
    public async Task AskFollowTheLocationAndSpeakTheProphecy()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var client = dungeon.CreateClient();
        var (id, offering) = await StartAsync(client);

        var ask = await SendAsync(client, HttpMethod.Post, $"{Room}/{id}/questions", offering, new { Question = "Will I pass?" });
        var answer = await SendAsync(client, HttpMethod.Get, ask.Headers.Location!.ToString(), offering);
        var prophecy = (await answer.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("prophecy").GetString();
        var reply = await client.PostAsJsonAsync($"{Room}/{id}/answer", new { Answer = prophecy });

        Assert.Multiple(async () =>
        {
            Assert.That(ask.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(await ask.Content.ReadAsStringAsync(), Does.Not.Contain(prophecy!), "the 201 only points the way");
            Assert.That(answer.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task NoOfferingNoAnswer()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var client = dungeon.CreateClient();
        var (id, offering) = await StartAsync(client);

        var unpaid = await client.PostAsJsonAsync($"{Room}/{id}/questions", new { Question = "Will I pass?" });
        var ask = await SendAsync(client, HttpMethod.Post, $"{Room}/{id}/questions", offering, new { Question = "Will I pass?" });
        var unpaidCollect = await client.GetAsync(ask.Headers.Location);
        var empty = await SendAsync(client, HttpMethod.Post, $"{Room}/{id}/questions", offering, new { Question = " " });

        Assert.Multiple(() =>
        {
            Assert.That(unpaid.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(unpaidCollect.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(empty.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task TheWellNeverLeaksThroughReveal()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var client = dungeon.CreateClient();
        var (id, _) = await StartAsync(client);

        Assert.That((await client.GetAsync($"{Room}/{id}/reveal")).StatusCode, Is.EqualTo((HttpStatusCode)425));
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string offering, object? body = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Add("X-Offering", offering);
        return client.SendAsync(request);
    }

    private static async Task<(Guid Id, string Offering)> StartAsync(HttpClient client)
    {
        var attempt = await (await client.PostAsync(Room, null)).Content.ReadFromJsonAsync<JsonElement>();
        return (attempt.GetProperty("attemptId").GetGuid(), attempt.GetProperty("briefing").GetProperty("offering").GetString()!);
    }
}
