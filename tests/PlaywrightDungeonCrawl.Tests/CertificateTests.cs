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
    private static int Apprentice => CertificateRules.For(CertificateTier.Apprentice).Floors;

    private async Task ClearFloorsAsync(string login, int count)
    {
        foreach (var floor in Catalog.Floors.Take(count))
        {
            await Records.RecordAsync($"id-{login}", login, Achievement.Riddle, floor.Slug);
            await Records.RecordAsync($"id-{login}", login, Achievement.Treasure, floor.Room.Slug);
        }
    }

    private static async Task<HttpResponseMessage> IssueAsync(HttpClient client, string name, string? lang = null)
    {
        var html = await client.GetStringAsync("/certificate");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var fields = new Dictionary<string, string>
        {
            ["Name"] = name,
            ["__RequestVerificationToken"] = token,
        };
        if (lang is not null)
        {
            fields["Lang"] = lang;
        }
        return await client.PostAsync("/certificate", new FormUrlEncodedContent(fields));
    }

    [Test]
    public async Task TheCertificateNamesTheLanguageItWasClearedIn()
    {
        var player = dungeon.ClientFor("ada");
        await ClearFloorsAsync("ada", Catalog.Floors.Count);

        var reply = await IssueAsync(player, "Ada", "ts");
        var page = await player.GetStringAsync(reply.Headers.Location!.OriginalString);

        Assert.Multiple(async () =>
        {
            Assert.That((await Records.CertificateForAsync("id-ada"))!.Language, Is.EqualTo(CodeLanguage.TypeScript));
            Assert.That(page, Does.Contain("Playwright tests in TypeScript"));
            Assert.That(page, Does.Contain("name=" + Uri.EscapeDataString("Playwright Dungeon Crawl: Playwright for TypeScript (Apprentice)")));
        });
    }

    [Test]
    public async Task CertificatesDefaultToCSharpAndCanSwitchOnReprint()
    {
        var player = dungeon.ClientFor("ada");
        await ClearFloorsAsync("ada", Catalog.Floors.Count);

        await IssueAsync(player, "Ada");
        var first = await Records.CertificateForAsync("id-ada");
        await IssueAsync(player, "Ada", "ts");
        var second = await Records.CertificateForAsync("id-ada");

        Assert.Multiple(() =>
        {
            Assert.That(first!.Language, Is.EqualTo(CodeLanguage.CSharp));
            Assert.That(second!.Language, Is.EqualTo(CodeLanguage.TypeScript));
            Assert.That(second.Id, Is.EqualTo(first.Id), "same certificate, reprinted");
        });
    }

    [Test]
    public async Task NotEligibleUntilEveryApprenticeFloorIsVerified()
    {
        var player = dungeon.ClientFor("ada");
        await ClearFloorsAsync("ada", Apprentice - 1);

        var page = await player.GetStringAsync("/certificate");

        Assert.Multiple(async () =>
        {
            Assert.That(page, Does.Contain("Not yet").And.Contain($"{Apprentice - 1} of {Catalog.Floors.Count} floors"));
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
                .And.Contain("@ada").And.Contain(Catalog.Floors[Apprentice - 1].Skill).And.Not.Contain(Catalog.Floors[Apprentice].Skill));
            Assert.That(stranger, Does.Not.Contain("Add to LinkedIn"), "only the owner gets the share buttons");
            Assert.That(owner, Does.Contain("linkedin.com/profile/add").And.Contain("certId=" + url.Split('/')[^1])
                .And.Contain("organizationId=" + CertificateRules.LinkedInOrganizationId));
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

    private static async Task<HttpResponseMessage> IssueTierAsync(HttpClient client, string tier)
    {
        var html = await client.GetStringAsync("/certificate");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        return await client.PostAsync("/certificate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Ada",
            ["Tier"] = tier,
            ["__RequestVerificationToken"] = token,
        }));
    }

    [Test]
    public async Task ThePathShowsEachTierAndWhetherItsOpen()
    {
        var player = dungeon.ClientFor("ada");
        await ClearFloorsAsync("ada", CertificateRules.For(CertificateTier.Apprentice).Floors);

        var page = await player.GetStringAsync("/certificate");

        Assert.Multiple(() =>
        {
            Assert.That(page, Does.Contain("class=\"panel cert-tier ready\" aria-labelledby=\"tier-apprentice\""));
            Assert.That(page, Does.Contain("Issue my Apprentice certificate"));
            // Floors 8 and on aren't built yet, so the deeper tiers are sealed.
            Assert.That(page, Does.Contain("class=\"panel cert-tier sealed\" aria-labelledby=\"tier-journeyman\""));
            Assert.That(page, Does.Contain("class=\"panel cert-tier sealed\" aria-labelledby=\"tier-master\""));
        });
    }

    [Test]
    public async Task ASealedTierCantBeClaimed()
    {
        var player = dungeon.ClientFor("ada");
        await ClearFloorsAsync("ada", Catalog.Floors.Count);

        var reply = await IssueTierAsync(player, "journeyman");

        Assert.Multiple(async () =>
        {
            Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await reply.Content.ReadAsStringAsync(), Does.Contain("The Journeyman certificate opens once floors 1 to 13 are built."));
            Assert.That(await Records.CertificatesForAsync("id-ada"), Is.Empty);
        });
    }

    [Test]
    public async Task TheCertificateNamesItsTier()
    {
        var player = dungeon.ClientFor("ada");
        await ClearFloorsAsync("ada", Catalog.Floors.Count);

        var reply = await IssueTierAsync(player, "apprentice");
        var page = await player.GetStringAsync(reply.Headers.Location!.OriginalString);
        var issued = await Records.CertificateForAsync("id-ada", CertificateTier.Apprentice);

        Assert.Multiple(() =>
        {
            Assert.That(issued!.Tier, Is.EqualTo(CertificateTier.Apprentice));
            Assert.That(issued.Floors, Is.EqualTo(7));
            Assert.That(page, Does.Contain("Apprentice tier"));
            Assert.That(page, Does.Contain(Uri.EscapeDataString("Playwright for .NET (Apprentice)")));
        });
    }

    [Test]
    public async Task EachTierIsItsOwnCertificate()
    {
        var player = dungeon.ClientFor("ada");
        await ClearFloorsAsync("ada", Catalog.Floors.Count);
        var issuedAt = DateTimeOffset.UtcNow;
        await Records.IssueCertificateAsync(new(CertificateRules.NewId(issuedAt), "id-ada", "ada", "Ada", 13, 1100, issuedAt,
            Tier: CertificateTier.Journeyman));

        await IssueTierAsync(player, "apprentice");
        var all = await Records.CertificatesForAsync("id-ada");
        var page = await player.GetStringAsync("/certificate");

        Assert.Multiple(() =>
        {
            Assert.That(all.Select(c => c.Tier), Is.EqualTo(new[] { CertificateTier.Apprentice, CertificateTier.Journeyman }));
            Assert.That(all.Select(c => c.Id).Distinct().Count(), Is.EqualTo(2));
            Assert.That(page, Does.Contain("class=\"panel cert-tier earned\" aria-labelledby=\"tier-apprentice\""));
            Assert.That(page, Does.Contain("class=\"panel cert-tier earned\" aria-labelledby=\"tier-journeyman\""));
        });
    }

    [Test]
    public void TiersOpenOnlyWhenTheirFloorsExist()
    {
        var journeyman = CertificateRules.For(CertificateTier.Journeyman);
        var cleared = new Standing("ada", 13, 1000, DateTimeOffset.UtcNow);

        Assert.Multiple(() =>
        {
            Assert.That(CertificateRules.IsOpen(CertificateRules.For(CertificateTier.Apprentice), Catalog), Is.True);
            Assert.That(CertificateRules.IsOpen(journeyman, Catalog), Is.EqualTo(Catalog.Floors.Count >= 13));
            Assert.That(CertificateRules.IsEligible(cleared, journeyman, Catalog), Is.EqualTo(Catalog.Floors.Count >= 13));
            Assert.That(CertificateRules.Tiers.Select(t => t.Floors), Is.Ordered.Ascending);
            Assert.That(CertificateRules.ParseTier("MASTER"), Is.EqualTo(CertificateTier.Master));
            Assert.That(CertificateRules.ParseTier("grandmaster"), Is.Null);
        });
    }
}
