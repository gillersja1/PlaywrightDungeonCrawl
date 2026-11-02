using System.Net;

namespace PlaywrightDungeonCrawl.Tests;

/// <summary>The far side of floor 8's portal: the pages the trap room opens in a new tab.</summary>
public class PortalTests
{
    private SignedInDungeon dungeon = null!;

    [SetUp]
    public void StartDungeon() => dungeon = new SignedInDungeon();

    [TearDown]
    public void StopDungeon() => dungeon.Dispose();

    [Test]
    public async Task ThePortalAndTheStallBothRender()
    {
        var client = dungeon.ClientFor(null);
        var id = Guid.NewGuid();

        var portal = await client.GetStringAsync($"/portal/{id}");
        var stall = await client.GetStringAsync($"/portal/{id}/stall");

        Assert.Multiple(() =>
        {
            Assert.That(portal, Does.Contain($"data-portal=\"{id}\" data-stage=\"portal\""));
            Assert.That(stall, Does.Contain($"data-portal=\"{id}\" data-stage=\"stall\""));
            Assert.That(portal, Does.Match(@"/js/portal(\.\w+)?\.js"));
        });
    }

    [Test]
    public async Task APortalNeedsAnAttemptId()
    {
        var reply = await dungeon.ClientFor(null).GetAsync("/portal/not-an-attempt");

        Assert.That(reply.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
