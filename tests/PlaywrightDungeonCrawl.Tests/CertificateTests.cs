using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Tests;

public class CertificateTests
{
    private SignedInDungeon dungeon = null!;

    [SetUp]
    public void StartDungeon() => dungeon = new SignedInDungeon();

    [TearDown]
    public void StopDungeon() => dungeon.Dispose();

    private IPlayerRecords Records => dungeon.Services.GetRequiredService<IPlayerRecords>();
    private DungeonCatalog Catalog => dungeon.Services.GetRequiredService<DungeonCatalog>();

    private async Task ClearFloorsAsync(string login, int count)
    {
        foreach (var floor in Catalog.Floors.Take(count))
        {
            await Records.RecordAsync($"id-{login}", login, Achievement.Riddle, floor.Slug);
            await Records.RecordAsync($"id-{login}", login, Achievement.Treasure, floor.Room.Slug);
        }
    }

    private static async Task<HttpResponseMessage> IssueAsync(HttpClient client, string name)
    {
        var html = await client.GetStringAsync("/certificate");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        return await client.PostAsync("/certificate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = name,
            ["__RequestVerificationToken"] = token,
        }));
    }

    [Test]
    public async Task NotEligibleUntilEveryFloorIsVerified()
    {
        var player = dungeon.ClientFor("ada");
        await ClearFloorsAsync("ada", Catalog.Floors.Count - 1);

        var page = await player.GetStringAsync("/certificate");

        Assert.Multiple(async () =>
        {
            Assert.That(page, Does.Contain("Not yet").And.Contain($"{Catalog.Floors.Count - 1} of {Catalog.Floors.Count} floors"));
            Assert.That(await Records.CertificateForAsync("id-ada"), Is.Null);
        });
    }

    [Test]
    public async Task IssuesACertificateWithItsOwnPublicPage()
    {
        var player = dungeon.ClientFor("ada");
        await ClearFloorsAsync("ada", Catalog.Floors.Count);

        var reply = await IssueAsync(player, "  Ada   Lovelace ");
        var url = reply.Headers.Location!.OriginalString;
        var stranger = await dungeon.ClientFor(null).GetStringAsync(url);
        var owner = await player.GetStringAsync(url);

        Assert.Multiple(() =>
        {
            Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(url, Does.Match($"^/certificates/PDC-{DateTime.UtcNow:yyyyMMdd}-[0-9A-HJKMNP-TV-Z]{{8}}$"));
            Assert.That(stranger, Does.Contain("Ada Lovelace").And.Contain("Certificate of Survival")
                .And.Contain("@ada").And.Contain(Catalog.Floors[^1].Skill));
            Assert.That(stranger, Does.Not.Contain("Add to LinkedIn"), "only the owner gets the share buttons");
            Assert.That(owner, Does.Contain("linkedin.com/profile/add").And.Contain("certId=" + url.Split('/')[^1]));
        });
    }

    [Test]
    public async Task ReprintingKeepsTheIdAndDate()
    {
        var player = dungeon.ClientFor("ada");
        await ClearFloorsAsync("ada", Catalog.Floors.Count);

        await IssueAsync(player, "Ada");
        var first = await Records.CertificateForAsync("id-ada");
        await IssueAsync(player, "Ada Lovelace");
        var second = await Records.CertificateForAsync("id-ada");

        Assert.Multiple(() =>
        {
            Assert.That(second!.Id, Is.EqualTo(first!.Id));
            Assert.That(second.IssuedAt, Is.EqualTo(first.IssuedAt));
            Assert.That(second.Name, Is.EqualTo("Ada Lovelace"));
        });
    }

    [Test]
    public async Task ACertificateSurvivesAProgressReset()
    {
        var player = dungeon.ClientFor("ada");
        await ClearFloorsAsync("ada", Catalog.Floors.Count);
        await IssueAsync(player, "Ada");

        await player.DeleteAsync("/api/progress");

        Assert.That(await Records.CertificateForAsync("id-ada"), Is.Not.Null);
    }

    [Test]
    public async Task ReprintingAnOldStyleIdUpgradesItAndRedirectsTheOldLink()
    {
        var player = dungeon.ClientFor("ada");
        await ClearFloorsAsync("ada", Catalog.Floors.Count);
        var issuedAt = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
        await Records.IssueCertificateAsync(new("f83e4b36c2981004", "id-ada", "ada", "Ada", 6, 510, issuedAt));

        await IssueAsync(player, "Ada Lovelace");
        var upgraded = await Records.CertificateForAsync("id-ada");
        var oldLink = await dungeon.ClientFor(null).GetAsync("/certificates/f83e4b36c2981004");

        Assert.Multiple(() =>
        {
            Assert.That(upgraded!.Id, Does.Match("^PDC-20261030-[0-9A-HJKMNP-TV-Z]{8}$"), "keeps the original issue date");
            Assert.That(upgraded.IssuedAt, Is.EqualTo(issuedAt));
            Assert.That(oldLink.StatusCode, Is.EqualTo(HttpStatusCode.MovedPermanently));
            Assert.That(oldLink.Headers.Location!.OriginalString, Is.EqualTo($"/certificates/{upgraded.Id}"));
        });
    }

    [Test]
    public void NewIdsCarryTheDateAndAreUnambiguous()
    {
        var ids = Enumerable.Range(0, 200).Select(_ => CertificateRules.NewId(new DateTimeOffset(2026, 10, 30, 23, 0, 0, TimeSpan.FromHours(-4)))).ToList();

        Assert.Multiple(() =>
        {
            // 23:00 in UTC-4 is already the 31st in UTC.
            Assert.That(ids, Has.All.Match("^PDC-20261031-[0-9A-HJKMNP-TV-Z]{8}$"));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
            Assert.That(CertificateRules.IsLegacyId("f83e4b36c2981004"), Is.True);
            Assert.That(CertificateRules.IsLegacyId(ids[0]), Is.False);
        });
    }

    [Test]
    public async Task UnknownCertificatesAreNotFound()
    {
        var reply = await dungeon.ClientFor(null).GetAsync("/certificates/0000000000000000");

        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [TestCase("Ada Lovelace", "Ada Lovelace")]
    [TestCase("  Ada \t Lovelace  ", "Ada Lovelace")]
    [TestCase("", null)]
    [TestCase("   ", null)]
    [TestCase("Ada\u0000Lovelace", null)]
    public void NamesAreTidiedOrRejected(string input, string? expected) =>
        Assert.That(CertificateRules.CleanName(input), Is.EqualTo(expected));

    [Test]
    public void NamesHaveALengthLimit() =>
        Assert.That(CertificateRules.CleanName(new string('a', CertificateRules.MaxNameLength + 1)), Is.Null);
}
