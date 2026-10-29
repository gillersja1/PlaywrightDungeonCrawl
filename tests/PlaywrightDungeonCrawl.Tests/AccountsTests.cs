using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Tests;

/// <summary>
/// The site with GitHub sign-in switched on, but with a stand-in sign-in:
/// requests carrying an X-Test-Player header are signed in as that player.
/// </summary>
public sealed class SignedInDungeon : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("GitHub:ClientId", "test-client");
        builder.UseSetting("GitHub:ClientSecret", "test-secret");
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestPlayerHandler>(TestPlayerHandler.SchemeName, null);
            services.PostConfigure<AuthenticationOptions>(options => options.DefaultScheme = TestPlayerHandler.SchemeName);
        });
    }

    public HttpClient ClientFor(string? player)
    {
        var client = CreateClient(new() { AllowAutoRedirect = false });
        if (player is not null)
        {
            client.DefaultRequestHeaders.Add(TestPlayerHandler.Header, player);
        }
        return client;
    }
}

public sealed class TestPlayerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestPlayer";
    public const string Header = "X-Test-Player";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers[Header] is not [{ } login])
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, $"id-{login}"), new Claim(ClaimTypes.Name, login)], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public class AccountsTests
{
    private SignedInDungeon dungeon = null!;

    [SetUp]
    public void StartDungeon() => dungeon = new SignedInDungeon();

    [TearDown]
    public void StopDungeon() => dungeon.Dispose();

    private static object Progress(string[]? scrolls = null, string[]? riddles = null, string[]? treasures = null, Dictionary<string, int>? help = null) =>
        new { scrolls = scrolls ?? [], riddles = riddles ?? [], treasures = treasures ?? [], help = help ?? [] };

    private static async Task<PlayerProgress> PutAsync(HttpClient client, object progress)
    {
        var reply = await client.PutAsJsonAsync("/api/progress", progress);
        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return (await reply.Content.ReadFromJsonAsync<PlayerProgress>())!;
    }

    [Test]
    public async Task ProgressNeedsASignedInPlayer()
    {
        var reply = await dungeon.ClientFor(null).PutAsJsonAsync("/api/progress", Progress());

        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task SavesMergeAcrossBrowsers()
    {
        var player = dungeon.ClientFor("ada");

        await PutAsync(player, Progress(scrolls: ["setup"], help: new() { ["hint:setup:0"] = 0 }));
        var merged = await PutAsync(player, Progress(riddles: ["setup"], treasures: ["welcome-matt"]));

        Assert.Multiple(() =>
        {
            Assert.That(merged.Scrolls, Is.EqualTo(new[] { "setup" }));
            Assert.That(merged.Riddles, Is.EqualTo(new[] { "setup" }));
            Assert.That(merged.Treasures, Is.EqualTo(new[] { "welcome-matt" }));
            Assert.That(merged.Help, Is.EqualTo(new Dictionary<string, int> { ["hint:setup:0"] = 0 }));
        });
    }

    [Test]
    public async Task UnknownProgressIsDropped()
    {
        var saved = await PutAsync(dungeon.ClientFor("ada"), Progress(
            scrolls: ["setup", "not-a-floor"],
            treasures: ["hall-of-doors", "setup"],
            help: new() { ["nudge:hall-of-doors:1"] = 10, ["<script>"] = 5, ["hint:setup:0"] = -50 }));

        Assert.Multiple(() =>
        {
            Assert.That(saved.Scrolls, Is.EqualTo(new[] { "setup" }));
            Assert.That(saved.Treasures, Is.EqualTo(new[] { "hall-of-doors" }));
            Assert.That(saved.Help, Is.EqualTo(new Dictionary<string, int> { ["nudge:hall-of-doors:1"] = 10 }));
        });
    }

    [Test]
    public async Task PlayersDontShareProgress()
    {
        await PutAsync(dungeon.ClientFor("ada"), Progress(scrolls: ["setup"]));

        var other = await PutAsync(dungeon.ClientFor("grace"), Progress());

        Assert.That(other.Scrolls, Is.Empty);
    }

    [Test]
    public async Task ResetWipesTheAccount()
    {
        var player = dungeon.ClientFor("ada");
        await PutAsync(player, Progress(scrolls: ["setup"]));

        var reset = await player.DeleteAsync("/api/progress");
        var after = await PutAsync(player, Progress());

        Assert.Multiple(() =>
        {
            Assert.That(reset.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(after.Scrolls, Is.Empty);
        });
    }

    [Test]
    public async Task HeaderOffersSignInOrShowsThePlayer()
    {
        var visitor = await dungeon.ClientFor(null).GetStringAsync("/");
        var player = await dungeon.ClientFor("ada").GetStringAsync("/");

        Assert.Multiple(() =>
        {
            Assert.That(visitor, Does.Contain("Sign in with GitHub"));
            Assert.That(player, Does.Contain("@ada").And.Contain("Sign out").And.Not.Contain("Sign in with GitHub"));
        });
    }

    [Test]
    public async Task SignInIsHiddenUntilGitHubIsConfigured()
    {
        using var plain = new WebApplicationFactory<Program>();

        var page = await plain.CreateClient().GetStringAsync("/");
        var signIn = await plain.CreateClient(new() { AllowAutoRedirect = false }).GetAsync("/signin");

        Assert.Multiple(() =>
        {
            Assert.That(page, Does.Not.Contain("Sign in with GitHub"));
            Assert.That(signIn.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [TestCase("/floors/setup", "/floors/setup")]
    [TestCase("/", "/")]
    [TestCase("//evil.example", "/")]
    [TestCase("/\\evil.example", "/")]
    [TestCase("https://evil.example", "/")]
    [TestCase(null, "/")]
    public void SignInOnlyReturnsToThisSite(string? returnUrl, string expected) =>
        Assert.That(Accounts.LocalPath(returnUrl), Is.EqualTo(expected));
}
