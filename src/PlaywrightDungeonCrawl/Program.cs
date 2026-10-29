using PlaywrightDungeonCrawl.Dungeon;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AttemptStore>();
builder.Services.AddSingleton<Room, WelcomeMat>();
builder.Services.AddSingleton<Room, HallOfDoors>();
builder.Services.AddSingleton<Room, FlickeringTorch>();
builder.Services.AddSingleton<Room, ScribesLedger>();
builder.Services.AddSingleton<Room, MirrorChamber>();
builder.Services.AddSingleton<Room, WhisperingVault>();
builder.Services.AddSingleton<DungeonCatalog>();

// Set Dungeon__FlagSecret in any shared deployment; the default only suits local play.
var flagSecret = builder.Configuration["Dungeon:FlagSecret"] ?? "local-dungeon-secret";
builder.Services.AddSingleton(new FlagService(flagSecret));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();
app.MapDungeonApi();
app.MapGet("/health", () => Results.Ok(new { Status = "ok" }));

app.Run();

public partial class Program;
