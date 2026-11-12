using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Tests;

/// <summary>Floor 17's gallery: six PNG portraits; after the forger strikes, exactly one differs.</summary>
public class GalleryTests
{
    private const string Room = "/api/rooms/curators-eye/attempts";

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private WebApplicationFactory<Program> dungeon = null!;
    private ManualClock clock = null!;
    private HttpClient client = null!;

    [SetUp]
    public void OpenTheGallery()
    {
        clock = new ManualClock();
        dungeon = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(clock)));
        client = dungeon.CreateClient();
    }

    [TearDown]
    public async Task CloseTheGallery()
    {
        client.Dispose();
        await dungeon.DisposeAsync();
    }

    [Test]
    public async Task ExactlyOnePortraitChangesAndItIsTheAnswer()
    {
        var (id, sitters) = await StartAsync();
        var before = await HangingAsync(id, 1);
        clock.Now += TimeSpan.FromSeconds(4);
        var after = await HangingAsync(id, 2);

        var changed = Enumerable.Range(0, CuratorsEye.Count).Where(i => !before[i].SequenceEqual(after[i])).ToList();
        var accuse = await (await client.PostAsJsonAsync($"{Room}/{id}/answer", new { Answer = sitters[changed.Single()] }))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(changed, Has.Count.EqualTo(1));
            Assert.That(before, Has.All.Matches<byte[]>(png => png!.Take(8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })));
            Assert.That(accuse.GetProperty("outcome").GetString(), Is.EqualTo("correct"));
        });
    }

    [Test]
    public async Task TheForgerCantBeSeenBeforeHeStrikes()
    {
        var (id, _) = await StartAsync();

        var early = await client.GetAsync($"{Room}/{id}/portraits/0?hanging=2");

        Assert.That(early.StatusCode, Is.EqualTo((HttpStatusCode)425));
    }

    [Test]
    public async Task UnknownPortraitsAreNotFoundAndRevealStaysShut()
    {
        var (id, _) = await StartAsync();

        Assert.Multiple(async () =>
        {
            Assert.That((await client.GetAsync($"{Room}/{id}/portraits/6")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await client.GetAsync($"{Room}/{Guid.NewGuid()}/portraits/0")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await client.GetAsync($"{Room}/{id}/reveal")).StatusCode, Is.EqualTo((HttpStatusCode)425));
        });
    }

    [Test]
    public void TheSameSeedPaintsTheSameBytesAndAFlawChangesThem()
    {
        var first = Portraits.Paint(42);
        var again = Portraits.Paint(42);
        var forged = Portraits.Paint(42, new Portraits.Flaw(20, 30));
        Assert.Multiple(() =>
        {
            Assert.That(again, Is.EqualTo(first));
            Assert.That(forged, Is.Not.EqualTo(first));
        });
    }

    private async Task<(Guid Id, string[] Sitters)> StartAsync()
    {
        var attempt = await (await client.PostAsync(Room, null)).Content.ReadFromJsonAsync<JsonElement>();
        var sitters = attempt.GetProperty("briefing").GetProperty("sitters").EnumerateArray().Select(s => s.GetString()!).ToArray();
        return (attempt.GetProperty("attemptId").GetGuid(), sitters);
    }

    private async Task<byte[][]> HangingAsync(Guid id, int hanging) =>
        await Task.WhenAll(Enumerable.Range(0, CuratorsEye.Count)
            .Select(i => client.GetByteArrayAsync($"{Room}/{id}/portraits/{i}?hanging={hanging}")));
}
