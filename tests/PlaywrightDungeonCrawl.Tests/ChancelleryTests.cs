using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PlaywrightDungeonCrawl.Tests;

/// <summary>Floor 13, the second boss: every link of the Chancellor's chain is checked on the server.</summary>
public class ChancelleryTests
{
    private const string Room = "/api/rooms/chancellors-seal/attempts";

    [Test]
    public async Task TheWholeChainInTwoContextsEarnsTheFlag()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var clerk = dungeon.CreateClient();
        var chancellor = dungeon.CreateClient();   // its own cookie jar: a second browser context
        var (id, petition, clerkPassword) = await StartAsync(clerk);

        var password = await AskTheRegistryAsync(clerk, id, petition);
        await SignInAsync(clerk, id, "Clerk", clerkPassword);
        await SignInAsync(chancellor, id, "Chancellor", password);
        var charter = await SealAndCollectAsync(chancellor, id);
        var filed = await FileAsync(clerk, id, charter);
        var phrase = (await filed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("phrase").GetString();
        var reply = await clerk.PostAsJsonAsync($"{Room}/{id}/answer", new { Answer = phrase });

        Assert.Multiple(async () =>
        {
            Assert.That(filed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await reply.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("flag").GetString(), Does.Match(@"^PDC\{[0-9a-f]{12}\}$"));
        });
    }

    [Test]
    public async Task TheRegistryWantsThePetitionNumberAndTheRightOffice()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var client = dungeon.CreateClient();
        var (id, petition, _) = await StartAsync(client);

        var unnumbered = await client.PostAsJsonAsync($"{Room}/{id}/registry", new { Office = "Chancellor" });
        var wrongOffice = await PetitionAsync(client, id, petition, "Treasurer");
        var filed = await PetitionAsync(client, id, petition, "Chancellor");
        var readWithout = await client.GetAsync(filed.Headers.Location);

        Assert.Multiple(() =>
        {
            Assert.That(unnumbered.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(wrongOffice.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(filed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(readWithout.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        });
    }

    [Test]
    public async Task OneContextCanOnlyWearOneSeal()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var both = dungeon.CreateClient();
        var (id, petition, clerkPassword) = await StartAsync(both);
        var password = await AskTheRegistryAsync(both, id, petition);

        await SignInAsync(both, id, "Clerk", clerkPassword);
        await SignInAsync(both, id, "Chancellor", password);   // the Chancellor's seal replaces the Clerk's
        var charter = await SealAndCollectAsync(both, id);
        var asChancellor = await FileAsync(both, id, charter);
        await SignInAsync(both, id, "Clerk", clerkPassword);   // a new Clerk, for whom nothing was sealed
        var asNewClerk = await FileAsync(both, id, charter);

        Assert.Multiple(() =>
        {
            Assert.That(asChancellor.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(asNewClerk.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        });
    }

    [Test]
    public async Task NothingIsSealedWithoutAClerkAndTheNameMustMatch()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var clerk = dungeon.CreateClient();
        var chancellor = dungeon.CreateClient();
        var (id, petition, clerkPassword) = await StartAsync(clerk);
        var password = await AskTheRegistryAsync(clerk, id, petition);

        await SignInAsync(chancellor, id, "Chancellor", password);
        var tooSoon = await chancellor.PostAsync($"{Room}/{id}/seal", null);
        await SignInAsync(clerk, id, "Clerk", clerkPassword);
        var charter = await SealAndCollectAsync(chancellor, id);
        var seal = Regex.Match(charter, @"Seal: (\w+)").Groups[1].Value;
        var wrongName = await clerk.PostAsJsonAsync($"{Room}/{id}/file", new { Answer = $"charter.txt\n{seal}" });
        var reveal = await clerk.GetAsync($"{Room}/{id}/reveal");

        Assert.Multiple(() =>
        {
            Assert.That(tooSoon.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(wrongName.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(reveal.StatusCode, Is.EqualTo((HttpStatusCode)425), "the Chancellery's state never leaks through /reveal");
        });
    }

    [Test]
    public async Task AChancellorWhoSignsInAgainBreaksTheChain()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var clerk = dungeon.CreateClient();
        var chancellor = dungeon.CreateClient();
        var (id, petition, clerkPassword) = await StartAsync(clerk);
        var password = await AskTheRegistryAsync(clerk, id, petition);

        await SignInAsync(clerk, id, "Clerk", clerkPassword);
        await SignInAsync(chancellor, id, "Chancellor", password);
        var charter = await SealAndCollectAsync(chancellor, id);
        await SignInAsync(chancellor, id, "Chancellor", password);
        var filed = await FileAsync(clerk, id, charter);

        Assert.That(filed.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    private static Task<HttpResponseMessage> PetitionAsync(HttpClient client, Guid id, string petition, string office)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{Room}/{id}/registry") { Content = JsonContent.Create(new { Office = office }) };
        request.Headers.Add("X-Petition", petition);
        return client.SendAsync(request);
    }

    private static async Task<string> AskTheRegistryAsync(HttpClient client, Guid id, string petition)
    {
        var filed = await PetitionAsync(client, id, petition, "Chancellor");
        Assert.That(filed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var request = new HttpRequestMessage(HttpMethod.Get, filed.Headers.Location);
        request.Headers.Add("X-Petition", petition);
        var record = await (await client.SendAsync(request)).Content.ReadFromJsonAsync<JsonElement>();
        return record.GetProperty("password").GetString()!;
    }

    private static async Task SignInAsync(HttpClient client, Guid id, string role, string password)
    {
        var reply = await client.PostAsJsonAsync($"{Room}/{id}/login", new { Role = role, Password = password });
        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"{role} signs in");
    }

    private static async Task<string> SealAndCollectAsync(HttpClient chancellor, Guid id)
    {
        var sealedReply = await chancellor.PostAsync($"{Room}/{id}/seal", null);
        Assert.That(sealedReply.StatusCode, Is.EqualTo(HttpStatusCode.OK), "the charter is sealed");
        var url = (await sealedReply.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("charter").GetString();
        var download = await chancellor.GetAsync(url);
        Assert.That(download.Content.Headers.ContentDisposition?.FileName?.Trim('"'), Is.EqualTo("sealed-charter.txt"));
        return await download.Content.ReadAsStringAsync();
    }

    private static Task<HttpResponseMessage> FileAsync(HttpClient clerk, Guid id, string charter)
    {
        var name = Regex.Match(charter, @"under the name (\S+\.txt)").Groups[1].Value;
        return clerk.PostAsJsonAsync($"{Room}/{id}/file", new { Answer = $"{name}\n{charter}" });
    }

    private static async Task<(Guid Id, string Petition, string ClerkPassword)> StartAsync(HttpClient client)
    {
        var attempt = await (await client.PostAsync(Room, null)).Content.ReadFromJsonAsync<JsonElement>();
        var briefing = attempt.GetProperty("briefing");
        return (attempt.GetProperty("attemptId").GetGuid(),
            briefing.GetProperty("petition").GetString()!, briefing.GetProperty("clerkPassword").GetString()!);
    }
}
