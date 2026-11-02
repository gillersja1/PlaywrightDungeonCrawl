using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PlaywrightDungeonCrawl.Tests;

/// <summary>What a link to the dungeon looks like when shared, e.g. on LinkedIn.</summary>
public class SharingTests
{
    private SignedInDungeon dungeon = null!;

    [SetUp]
    public void StartDungeon() => dungeon = new SignedInDungeon();

    [TearDown]
    public void StopDungeon() => dungeon.Dispose();

    private static string Meta(string html, string property) =>
        Regex.Match(html, $"<meta property=\"{Regex.Escape(property)}\" content=\"([^\"]*)\"").Groups[1].Value;

    [Test]
    public async Task TheHomePagePreviewSaysWhatTheGameIs()
    {
        var html = await dungeon.ClientFor(null).GetStringAsync("/");

        Assert.Multiple(() =>
        {
            Assert.That(Meta(html, "og:title"), Is.EqualTo("Playwright Dungeon Crawl: learn Playwright by beating a dungeon"));
            Assert.That(Meta(html, "og:description"), Does.Contain("C# or TypeScript"));
            Assert.That(Meta(html, "og:image"), Does.EndWith("/img/brand/og-image-2.png"));
            Assert.That(Meta(html, "og:image:width"), Is.EqualTo("1200"));
            Assert.That(Meta(html, "og:image:height"), Is.EqualTo("630"));
        });
    }

    [Test]
    public async Task OtherPagesKeepTheirOwnTitles()
    {
        var html = await dungeon.ClientFor(null).GetStringAsync("/leaderboard");

        Assert.That(Meta(html, "og:title"), Is.EqualTo("Leaderboard"));
    }

    [Test]
    public async Task ThePreviewImageIsServed()
    {
        var reply = await dungeon.ClientFor(null).GetAsync("/img/brand/og-image-2.png");

        Assert.Multiple(() =>
        {
            Assert.That(reply.IsSuccessStatusCode, Is.True);
            Assert.That(reply.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/png"));
        });
    }

    [Test]
    public void TheAntiforgeryCookieIsSecureOnHttps() =>
        Assert.That(dungeon.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.Cookie.SecurePolicy,
            Is.EqualTo(CookieSecurePolicy.SameAsRequest));

    [TestCase(null, "Learn Playwright for .NET", "a short lesson with C# examples")]
    [TestCase("ts", "Learn Playwright in TypeScript", "a short lesson with TypeScript examples")]
    public async Task TheRespawnRoomSpeaksTheChosenLanguage(string? lang, string eyebrow, string scroll)
    {
        var client = dungeon.CreateClient();
        var html = await client.GetStringAsync(lang is null ? "/" : $"/?lang={lang}");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain(eyebrow));
            Assert.That(html, Does.Contain(scroll));
        });
    }
}
