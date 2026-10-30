using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace PlaywrightDungeonCrawl.Dungeon;

/// <summary>
/// Optional OpenTelemetry: traces, metrics and logs over OTLP (e.g. to Grafana
/// Cloud). Off unless OTEL_EXPORTER_OTLP_ENDPOINT is set; the token goes in
/// OTEL_EXPORTER_OTLP_HEADERS, which the exporter reads itself.
/// </summary>
public static class Telemetry
{
    public const string ServiceName = "playwright-dungeon-crawl";
    public const string MeterName = "PlaywrightDungeonCrawl";

    // The game's own numbers. Players aren't identified, only rooms and outcomes.
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Attempts =
        Meter.CreateCounter<long>("dungeon.room.attempts", "{attempt}", "Trap room attempts started");
    private static readonly Counter<long> Answers =
        Meter.CreateCounter<long>("dungeon.room.answers", "{answer}", "Answers given in trap rooms, by outcome");
    private static readonly Counter<long> Claims =
        Meter.CreateCounter<long>("dungeon.treasure.claims", "{claim}", "Treasure flags handed in, valid or not");

    public static void AttemptStarted(string room) =>
        Attempts.Add(1, new KeyValuePair<string, object?>("dungeon.room", room));

    public static void Answered(string room, string outcome) =>
        Answers.Add(1, new KeyValuePair<string, object?>("dungeon.room", room),
            new KeyValuePair<string, object?>("dungeon.outcome", outcome));

    public static void TreasureClaimed(string? room) =>
        Claims.Add(1, new KeyValuePair<string, object?>("dungeon.room", room ?? "invalid"));

    public static bool IsEnabled(IConfiguration config) =>
        !string.IsNullOrEmpty(config["OTEL_EXPORTER_OTLP_ENDPOINT"]);

    public static void AddTelemetry(this WebApplicationBuilder builder)
    {
        if (!IsEnabled(builder.Configuration))
        {
            return;
        }

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName)
                .AddAttributes([
                    new("deployment.environment.name", builder.Environment.EnvironmentName.ToLowerInvariant()),
                ]))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !IsNoise(context.Request.Path))
                // GitHub sign-in's calls to github.com, and the progress database.
                .AddHttpClientInstrumentation()
                .AddSource("Npgsql"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("System.Runtime")
                .AddMeter(MeterName))
            // Grafana Cloud's OTLP gateway speaks HTTP, not the exporter's default gRPC.
            .UseOtlpExporter(OtlpExportProtocol.HttpProtobuf, new Uri(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]!));
    }

    // Render's health checks and static files would drown out the real requests.
    private static bool IsNoise(PathString path) =>
        path.StartsWithSegments("/health") || Path.HasExtension(path.Value);
}
