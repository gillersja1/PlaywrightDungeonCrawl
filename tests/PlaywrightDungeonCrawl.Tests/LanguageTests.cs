using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Tests;

/// <summary>Choosing C# or TypeScript: saved in a cookie, and every floor teaches in it once it's been written.</summary>
public class LanguageTests
{
    private WebApplicationFactory<Program> dungeon = null!;

    [SetUp]
    public void StartDungeon() => dungeon = new WebApplicationFactory<Program>();

    [TearDown]
    public async Task StopDungeon() => await dungeon.DisposeAsync();

    private async Task<HttpClient> SpeakingAsync(string lang)
    {
        var client = dungeon.CreateClient();
        await client.GetAsync($"/?lang={lang}");
        return client;
    }

    [Test]
    public async Task TheSwitchSavesTheChoiceAndComesBackWithoutIt()
    {
        var client = dungeon.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var reply = await client.GetAsync("/floors/setup?lang=ts");

        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(reply.Headers.Location?.ToString(), Is.EqualTo("/floors/setup"));
        Assert.That(reply.Headers.GetValues("Set-Cookie"), Has.Some.StartsWith("dungeon-lang=ts"));
    }

    [Test]
    public async Task CSharpIsTheDefault()
    {
        var page = await dungeon.CreateClient().GetStringAsync("/floors/setup");

        Assert.That(page, Does.Contain("Install Visual Studio").And.Not.Contain("npm install"));
        Assert.That(page, Does.Match("<a href=\"/floors/setup\\?lang=csharp\" aria-current=\"true\""));
    }

    [Test]
    public async Task FloorOneTeachesTypeScript()
    {
        var client = await SpeakingAsync("ts");

        var lesson = await client.GetStringAsync("/floors/setup");
        Assert.That(lesson, Does.Contain("npm install").And.Contain("PlaywrightDungeonCrawl-Starter-TS"));
        Assert.That(lesson, Does.Not.Contain("Install Visual Studio"));

        var riddle = await client.GetStringAsync("/floors/setup/riddle");
        Assert.That(riddle, Does.Contain("playwright.config.ts").And.Contain("name=\"Lang\" value=\"ts\""));

        var room = await client.GetStringAsync("/rooms/welcome-matt");
        Assert.That(room, Does.Contain("tests/welcome-matt.spec.ts").And.Contain("dungeon.takeTreasure()"));
        Assert.That(room, Does.Not.Contain("TakeTreasureAsync"));
    }

    [Test]
    public async Task UntranslatedFloorsSayItAndShowCSharp()
    {
        var client = await SpeakingAsync("ts");

        var lesson = await client.GetStringAsync("/floors/locators");

        Assert.That(lesson, Does.Contain("hasn't been translated into TypeScript yet"));
        Assert.That(lesson, Does.Contain("Page.GetByRole"));
    }

    [Test]
    public async Task TypeScriptRiddlesAreGradedInTypeScript()
    {
        var client = await SpeakingAsync("ts");
        var html = await client.GetStringAsync("/floors/setup/riddle");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

        var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["Lang"] = "ts" };
        var correct = dungeon.Services.GetRequiredService<DungeonCatalog>().Find("setup")!.TypeScriptRiddles!;
        for (var i = 0; i < correct.Length; i++)
        {
            fields[$"Answers[{i}]"] = correct[i].CorrectIndex.ToString();
        }

        var reply = await client.PostAsync("/floors/setup/riddle", new FormUrlEncodedContent(fields));
        Assert.That(await reply.Content.ReadAsStringAsync(), Does.Contain("The riddle is solved."));
    }
}
