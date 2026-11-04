using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PlaywrightDungeonCrawl.Tests;

/// <summary>Floor 10's counter: the seal only comes as a download, and only counts back as a file with the right name.</summary>
public class CourierTests
{
    private const string Room = "/api/rooms/couriers-counter/attempts";

    [Test]
    public async Task TheDispatchIsADownloadAndTheRightReplyEarnsTheFlag()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var client = dungeon.CreateClient();
        var id = await StartAsync(client);

        var file = await client.GetAsync($"{Room}/{id}/dispatch");
        var text = await file.Content.ReadAsStringAsync();
        var seal = Regex.Match(text, @"Seal: (\w+)").Groups[1].Value;
        var name = Regex.Match(text, @"file named (\S+\.txt)").Groups[1].Value;

        var reply = await client.PostAsJsonAsync($"{Room}/{id}/answer", new { Answer = $"{name}\n{seal}\n" });
        var body = await reply.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(file.Content.Headers.ContentDisposition?.DispositionType, Is.EqualTo("attachment"));
            Assert.That(file.Content.Headers.ContentDisposition?.FileName, Does.Match(@"^dispatch-\w{4}\.txt$"));
            Assert.That(seal, Has.Length.EqualTo(6));
            Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.GetProperty("flag").GetString(), Does.Match(@"^PDC\{[0-9a-f]{12}\}$"));
        });
    }

    [Test]
    public async Task TheRightSealUnderTheWrongNameSpringsTheTrap()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var client = dungeon.CreateClient();
        var id = await StartAsync(client);
        var text = await client.GetStringAsync($"{Room}/{id}/dispatch");
        var seal = Regex.Match(text, @"Seal: (\w+)").Groups[1].Value;

        var reply = await client.PostAsJsonAsync($"{Room}/{id}/answer", new { Answer = $"reply.txt\n{seal}" });

        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    [Test]
    public async Task NoDispatchForAnUnknownAttempt()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var reply = await dungeon.CreateClient().GetAsync($"{Room}/{Guid.NewGuid()}/dispatch");

        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private static async Task<Guid> StartAsync(HttpClient client)
    {
        var attempt = await (await client.PostAsync(Room, null)).Content.ReadFromJsonAsync<JsonElement>();
        return attempt.GetProperty("attemptId").GetGuid();
    }
}
