using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Tests;

/// <summary>The Hall of Bugslayers: reporting, the keeper's review, and the badge.</summary>
public class BugReportTests
{
    private SignedInDungeon dungeon = null!;
    private WebApplicationFactory<Program> site = null!;

    [SetUp]
    public void StartDungeon()
    {
        dungeon = new SignedInDungeon();
        // "keeper" is the only admin (the test sign-in gives each player the id "id-<login>").
        site = dungeon.WithWebHostBuilder(b => b.UseSetting("Dungeon:Admins", "id-keeper"));
    }

    [TearDown]
    public void StopDungeon()
    {
        site.Dispose();
        dungeon.Dispose();
    }

    private IBugReports Bugs => site.Services.GetRequiredService<IBugReports>();

    private HttpClient ClientFor(string? player)
    {
        var client = site.CreateClient(new() { AllowAutoRedirect = false });
        if (player is not null)
        {
            client.DefaultRequestHeaders.Add(TestPlayerHandler.Header, player);
        }
        return client;
    }

    private static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private static async Task<HttpResponseMessage> ReportAsync(HttpClient client, string title, string details = "Steps: open floor 1. Expected: a door. Actual: a wall.")
    {
        var form = await client.GetStringAsync("/report-a-bug?from=/floors/setup");
        return await client.PostAsync("/report-a-bug", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(form),
            ["From"] = "/floors/setup",
            ["BugTitle"] = title,
            ["Details"] = details,
        }));
    }

    private async Task<HttpResponseMessage> ReviewAsync(HttpClient client, long id, string verdict)
    {
        var page = await client.GetStringAsync("/admin/bugs");
        return await client.PostAsync("/admin/bugs?handler=Review", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(page),
            ["id"] = id.ToString(),
            ["verdict"] = verdict,
        }));
    }

    [Test]
    public async Task ASignedInPlayerCanReportABugAndSeeItPending()
    {
        var ada = ClientFor("ada");

        var reply = await ReportAsync(ada, "The timer keeps running after the chest opens");
        var page = await reply.Content.ReadAsStringAsync();

        var saved = (await Bugs.ForPlayerAsync("id-ada")).Single();
        Assert.Multiple(() =>
        {
            Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(page, Does.Contain("Report received").And.Contain("The timer keeps running after the chest opens").And.Contain("Pending"));
            Assert.That(saved.Login, Is.EqualTo("ada"));
            Assert.That(saved.Page, Is.EqualTo("/floors/setup"));
            Assert.That(saved.Status, Is.EqualTo(BugStatus.Pending));
        });
    }

    [Test]
    public async Task SignedOutVisitorsAreAskedToSignIn()
    {
        var page = await ClientFor(null).GetStringAsync("/report-a-bug");

        Assert.That(page, Does.Contain("Sign in with GitHub first").And.Not.Contain("Send the report"));
    }

    [Test]
    public async Task AReportNeedsATitleAndDetails()
    {
        var reply = await ReportAsync(ClientFor("ada"), "", "");

        Assert.Multiple(async () =>
        {
            Assert.That(await reply.Content.ReadAsStringAsync(), Does.Contain("Give the bug a short name").And.Contain("Say what happened"));
            Assert.That(await Bugs.ForPlayerAsync("id-ada"), Is.Empty);
        });
    }

    [Test]
    public async Task OnlyFivePendingReportsAtATime()
    {
        var ada = ClientFor("ada");
        for (var i = 1; i <= BugRules.MaxPending; i++)
        {
            await ReportAsync(ada, $"Bug {i}");
        }

        var sixth = await (await ReportAsync(ada, "Bug 6")).Content.ReadAsStringAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(sixth, Does.Contain("reports waiting for review"));
            Assert.That(await Bugs.ForPlayerAsync("id-ada"), Has.Count.EqualTo(BugRules.MaxPending));
        });
    }

    [Test]
    public async Task TheReviewPageIsAPlain404ForEveryoneButTheKeeper()
    {
        var signedOut = await ClientFor(null).GetAsync("/admin/bugs");
        var player = await ClientFor("ada").GetAsync("/admin/bugs");
        var keeper = await ClientFor("keeper").GetAsync("/admin/bugs");

        Assert.Multiple(() =>
        {
            Assert.That(signedOut.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(player.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(keeper.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task APlayerCantConfirmTheirOwnReport()
    {
        var ada = ClientFor("ada");
        await ReportAsync(ada, "Free badge please");
        var id = (await Bugs.ForPlayerAsync("id-ada")).Single().Id;

        // No admin page to take a token from, so even a well-formed post is turned away.
        var reply = await ada.PostAsync("/admin/bugs?handler=Review",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = id.ToString(), ["verdict"] = "confirm" }));

        Assert.Multiple(async () =>
        {
            Assert.That(reply.StatusCode, Is.Not.EqualTo(HttpStatusCode.Redirect));
            Assert.That((await Bugs.ForPlayerAsync("id-ada")).Single().Status, Is.EqualTo(BugStatus.Pending));
        });
    }

    [Test]
    public async Task AConfirmedBugPutsItsFinderInTheHallAndOnTheLeaderboard()
    {
        var ada = ClientFor("ada");
        await ReportAsync(ada, "The timer keeps running after the chest opens");
        await ReportAsync(ada, "Typo on floor 3");
        var ids = (await Bugs.ForPlayerAsync("id-ada")).Select(r => r.Id).ToList();
        var records = site.Services.GetRequiredService<IPlayerRecords>();
        await records.SetOnLeaderboardAsync("id-ada", "ada", true);

        var keeper = ClientFor("keeper");
        var confirm = await ReviewAsync(keeper, ids[0], "confirm");
        await ReviewAsync(keeper, ids[1], "reject");

        var hall = await ClientFor(null).GetStringAsync("/hall-of-bugslayers");
        var leaderboard = await ClientFor(null).GetStringAsync("/leaderboard");
        Assert.Multiple(async () =>
        {
            Assert.That(confirm.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That((await Bugs.HallAsync()).Single(), Has.Property("Login").EqualTo("ada").And.Property("Confirmed").EqualTo(1));
            Assert.That(hall, Does.Contain("@ada").And.Contain("Bugslayer"));
            Assert.That(leaderboard, Does.Contain("🐞 Bugslayer").And.Contain("Found 1 confirmed bug in the dungeon"));
        });
    }

    [Test]
    public async Task AConfirmedBugIsWorthNoXp()
    {
        var ada = ClientFor("ada");
        await ReportAsync(ada, "A real bug");
        var id = (await Bugs.ForPlayerAsync("id-ada")).Single().Id;
        var records = site.Services.GetRequiredService<IPlayerRecords>();
        await records.SetOnLeaderboardAsync("id-ada", "ada", true);
        var before = await ClientFor(null).GetStringAsync("/leaderboard");

        await ReviewAsync(ClientFor("keeper"), id, "confirm");

        var after = await ClientFor(null).GetStringAsync("/leaderboard");
        Assert.That(Regex.Match(after, @"@ada[\s\S]*?(\d+) XP").Groups[1].Value,
            Is.EqualTo(Regex.Match(before, @"@ada[\s\S]*?(\d+) XP").Groups[1].Value));
    }

    [Test]
    public async Task TheHallIsEmptyUntilABugIsConfirmed()
    {
        await ReportAsync(ClientFor("ada"), "Pending, not confirmed");

        var hall = await ClientFor(null).GetStringAsync("/hall-of-bugslayers");

        Assert.That(hall, Does.Contain("No bugs slain yet").And.Not.Contain("@ada"));
    }
}
