using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Tests;

public class TelemetryTests
{
    private sealed record Measurement(string Instrument, Dictionary<string, object?> Tags);

    [Test]
    public async Task RoomsCountAttemptsAnswersAndClaims()
    {
        var measurements = new List<Measurement>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == Telemetry.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            lock (measurements)
            {
                measurements.Add(new(instrument.Name, tags.ToArray().ToDictionary(t => t.Key, t => t.Value)));
            }
        });
        listener.Start();

        await using var dungeon = new WebApplicationFactory<Program>();
        var client = dungeon.CreateClient();
        var attempt = await (await client.PostAsync("/api/rooms/hall-of-doors/attempts", null))
            .Content.ReadFromJsonAsync<AttemptResponse>();
        await client.PostAsJsonAsync($"/api/rooms/hall-of-doors/attempts/{attempt!.AttemptId}/answer", new { Answer = "not-a-door" });
        await client.PostAsJsonAsync("/api/treasure/claim", new { Flag = "fake-flag" });

        lock (measurements)
        {
            Assert.That(measurements, Has.Some.Matches<Measurement>(m =>
                m.Instrument == "dungeon.room.attempts" && (string?)m.Tags["dungeon.room"] == "hall-of-doors"));
            Assert.That(measurements, Has.Some.Matches<Measurement>(m =>
                m.Instrument == "dungeon.room.answers" && (string?)m.Tags["dungeon.room"] == "hall-of-doors"
                && (string?)m.Tags["dungeon.outcome"] == "wrong"));
            Assert.That(measurements, Has.Some.Matches<Measurement>(m =>
                m.Instrument == "dungeon.treasure.claims" && (string?)m.Tags["dungeon.room"] == "invalid"));
        }
    }

    [Test]
    public async Task ExportIsOffWithoutAnEndpoint()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        Assert.That(dungeon.Services.GetService<TracerProvider>(), Is.Null);
    }

    [Test]
    public async Task ExportTurnsOnWithAnEndpoint()
    {
        // Nothing listens on the discard port; the exporter only tries in the background.
        await using var dungeon = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:9/otlp"));
        Assert.That(dungeon.Services.GetService<TracerProvider>(), Is.Not.Null);
    }

    [Test]
    public async Task VisitorTrackingIsOffWithoutACollectorUrl()
    {
        await using var dungeon = new WebApplicationFactory<Program>();
        var page = await dungeon.CreateClient().GetStringAsync("/");
        Assert.That(page, Does.Not.Contain("faro-init"));
    }

    [Test]
    public async Task VisitorTrackingTurnsOnWithACollectorUrl()
    {
        const string collectorUrl = "https://faro.example/collect/test-key";
        await using var dungeon = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("Faro:CollectorUrl", collectorUrl));
        var page = await dungeon.CreateClient().GetStringAsync("/");
        Assert.That(page, Does.Contain("https://unpkg.com/@grafana/faro-web-sdk@1/"));
        Assert.That(page, Does.Contain($"\"{collectorUrl}\""));
    }

    private sealed record AttemptResponse(Guid AttemptId);
}
