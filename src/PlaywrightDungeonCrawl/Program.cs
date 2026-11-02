using Microsoft.AspNetCore.HttpOverrides;
using PlaywrightDungeonCrawl.Dungeon;

var builder = WebApplication.CreateBuilder(args);

// Hosts like Render tell the app which port to listen on through PORT.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Behind a hosting proxy, trust its X-Forwarded-* headers so the site knows
// visitors arrived over https (used, e.g., in the starter code's URLs).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AttemptStore>();
builder.Services.AddSingleton<Room, WelcomeMatt>();
builder.Services.AddSingleton<Room, HallOfDoors>();
builder.Services.AddSingleton<Room, FlickeringTorch>();
builder.Services.AddSingleton<Room, ScribesLedger>();
builder.Services.AddSingleton<Room, MirrorChamber>();
builder.Services.AddSingleton<Room, WhisperingVault>();
builder.Services.AddSingleton<Room, WardensGauntlet>();
builder.Services.AddSingleton<Room, PortalBazaar>();
builder.Services.AddSingleton<DungeonCatalog>();

// Set Dungeon__FlagSecret in any shared deployment; the default only suits local play.
var flagSecret = builder.Configuration["Dungeon:FlagSecret"] ?? "local-dungeon-secret";
builder.Services.AddSingleton(new FlagService(flagSecret));

builder.AddAccounts();
builder.AddTelemetry();

var app = builder.Build();
await app.PrepareAccountsAsync();

app.UseForwardedHeaders();
app.UseLanguageSwitch();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();
app.MapDungeonApi();
app.MapAccounts();
app.MapGet("/health", () => Results.Ok(new { Status = "ok" }));

app.Run();

public partial class Program;
