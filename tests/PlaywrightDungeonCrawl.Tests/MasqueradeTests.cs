using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PlaywrightDungeonCrawl.Tests;

/// <summary>Floor 11's ball: every guest wears a cookie called mask, so Fox and Owl need two cookie jars at once.</summary>
public class MasqueradeTests
{
    private const string Room = "/api/rooms/masked-ball/attempts";

    [Test]
    public async Task TwoGuestsInTwoContextsEarnTheFlag()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var fox = dungeon.CreateClient();
        var owl = dungeon.CreateClient();   // its own cookie jar: a second browser context
        var (id, foxPassword, owlPassword) = await StartAsync(fox);

        await LoginAsync(fox, id, "Fox", foxPassword);
        await LoginAsync(owl, id, "Owl", owlPassword);
        var whisper = (await (await owl.PostAsync($"{Room}/{id}/curtsy", null)).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("whisper").GetString();
        var unmask = await fox.PostAsJsonAsync($"{Room}/{id}/unmask", new { Answer = whisper });
        var phrase = (await unmask.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("phrase").GetString();
        var reply = await fox.PostAsJsonAsync($"{Room}/{id}/answer", new { Answer = phrase });

        Assert.Multiple(async () =>
        {
            Assert.That(unmask.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await reply.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("flag").GetString(), Does.Match(@"^PDC\{[0-9a-f]{12}\}$"));
        });
    }

    [Test]
    public async Task OneContextCanOnlyWearOneMask()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var both = dungeon.CreateClient();
        var (id, foxPassword, owlPassword) = await StartAsync(both);

        await LoginAsync(both, id, "Fox", foxPassword);
        await LoginAsync(both, id, "Owl", owlPassword);   // Owl's mask replaces Fox's
        var whisper = (await (await both.PostAsync($"{Room}/{id}/curtsy", null)).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("whisper").GetString();
        var asOwl = await both.PostAsJsonAsync($"{Room}/{id}/unmask", new { Answer = whisper });
        await LoginAsync(both, id, "Fox", foxPassword);   // a new Fox, whom Owl never bowed to
        var asNewFox = await both.PostAsJsonAsync($"{Room}/{id}/unmask", new { Answer = whisper });

        Assert.Multiple(() =>
        {
            Assert.That(asOwl.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(asNewFox.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        });
    }

    [Test]
    public async Task TheWrongPasswordIsTurnedAwayAndTheBallStaysPrivate()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var client = dungeon.CreateClient();
        var (id, _, _) = await StartAsync(client);

        var login = await client.PostAsJsonAsync($"{Room}/{id}/login", new { Guest = "Fox", Password = "letmein" });
        var reveal = await client.GetAsync($"{Room}/{id}/reveal");

        Assert.Multiple(() =>
        {
            Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(reveal.StatusCode, Is.EqualTo((HttpStatusCode)425), "the ball's state never leaks through /reveal");
        });
    }

    private static async Task LoginAsync(HttpClient client, Guid id, string guest, string password)
    {
        var reply = await client.PostAsJsonAsync($"{Room}/{id}/login", new { Guest = guest, Password = password });
        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"{guest} signs in");
    }

    private static async Task<(Guid Id, string Fox, string Owl)> StartAsync(HttpClient client)
    {
        var attempt = await (await client.PostAsync(Room, null)).Content.ReadFromJsonAsync<JsonElement>();
        var guests = attempt.GetProperty("briefing").GetProperty("guests");
        return (attempt.GetProperty("attemptId").GetGuid(),
            guests[0].GetProperty("password").GetString()!, guests[1].GetProperty("password").GetString()!);
    }
}
