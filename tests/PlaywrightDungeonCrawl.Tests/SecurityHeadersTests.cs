using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Tests;

public class SecurityHeadersTests
{
    private SignedInDungeon dungeon = null!;

    [SetUp]
    public void StartDungeon() => dungeon = new SignedInDungeon();

    [TearDown]
    public void StopDungeon() => dungeon.Dispose();

    private static string Nonce(HttpResponseMessage reply) =>
        Regex.Match(reply.Headers.GetValues("Content-Security-Policy").Single(), "'nonce-([^']+)'").Groups[1].Value;

    [TestCase("/")]
    [TestCase("/frames/mirror.html")]
    [TestCase("/js/progress.js")]
    public async Task EveryResponseCarriesTheHeaders(string path)
    {
        var reply = await dungeon.ClientFor(null).GetAsync(path);
        string Header(string name) => string.Join(",", reply.Headers.TryGetValues(name, out var v) ? v : []);

        Assert.Multiple(() =>
        {
            Assert.That(reply.IsSuccessStatusCode, Is.True);
            Assert.That(Header("Content-Security-Policy"), Does.Contain("default-src 'self'").And.Contain("frame-ancestors 'self'")
                .And.Contain("object-src 'none'").And.Not.Contain("unsafe-eval"));
            Assert.That(Header("X-Content-Type-Options"), Is.EqualTo("nosniff"));
            // Same-origin, not DENY: the mirror rooms frame /frames/mirror.html.
            Assert.That(Header("X-Frame-Options"), Is.EqualTo("SAMEORIGIN"));
            Assert.That(Header("Referrer-Policy"), Is.EqualTo("strict-origin-when-cross-origin"));
        });
    }

    [Test]
    public async Task ScriptsOnlyRunInlineWithANonceThatChangesEveryRequest()
    {
        var client = dungeon.ClientFor(null);
        var first = Nonce(await client.GetAsync("/"));
        var second = Nonce(await client.GetAsync("/"));

        Assert.Multiple(() =>
        {
            Assert.That(first, Has.Length.GreaterThanOrEqualTo(22));
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(SecurityHeaders.Policy("abc", null), Does.Contain("script-src 'self' 'nonce-abc' https://unpkg.com;")
                .And.Not.Contain("'unsafe-inline' https://unpkg"));
        });
    }

    // Every page with an inline script. A new inline script without the nonce would be blocked in browsers: this catches it first.
    [TestCase("/")]
    [TestCase("/floors/setup")]
    [TestCase("/floors/setup/riddle")]
    [TestCase("/floors/portals?lang=ts")]
    [TestCase("/rooms/welcome-matt")]
    [TestCase("/leaderboard")]
    [TestCase("/certificate")]
    [TestCase("/feedback")]
    public async Task EveryInlineScriptHasThisRequestsNonce(string path)
    {
        var reply = await dungeon.ClientFor("ada").GetAsync(path);
        var html = await reply.Content.ReadAsStringAsync();
        var nonce = Nonce(reply);

        var inline = Regex.Matches(html, "<script(?![^>]*\\bsrc=)(?![^>]*application/json)[^>]*>").Select(m => m.Value).ToList();

        Assert.That(inline, Has.All.Contain($"nonce=\"{nonce}\""), string.Join("\n", inline));
    }

    [Test]
    public async Task TheFaroCollectorIsTheOnlyOtherPlaceDataMayGo()
    {
        using var withFaro = new SignedInDungeon().WithWebHostBuilder(b =>
            b.UseSetting("Faro:CollectorUrl", "https://faro-collector-prod-us-east-0.grafana.net/collect/abc123"));

        var reply = await withFaro.CreateClient().GetAsync("/");
        var policy = reply.Headers.GetValues("Content-Security-Policy").Single();
        var html = await reply.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(policy, Does.Contain("connect-src 'self' https://faro-collector-prod-us-east-0.grafana.net;"));
            Assert.That(html, Does.Contain($"<script id=\"faro-init\" nonce=\"{Nonce(reply)}\">"));
        });
    }
}
