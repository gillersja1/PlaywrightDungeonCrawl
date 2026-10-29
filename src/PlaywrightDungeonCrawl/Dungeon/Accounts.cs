using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Npgsql;

namespace PlaywrightDungeonCrawl.Dungeon;

/// <summary>
/// Optional GitHub sign-in, so progress follows a player between browsers.
/// Without GitHub__ClientId and GitHub__ClientSecret the site works as before,
/// with progress in the browser only, and the sign-in link is hidden.
/// </summary>
public static class Accounts
{
    public const string GitHubScheme = "GitHub";

    public static bool IsEnabled(IConfiguration config) =>
        !string.IsNullOrEmpty(config["GitHub:ClientId"]) && !string.IsNullOrEmpty(config["GitHub:ClientSecret"]);

    public static void AddAccounts(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;
        var services = builder.Services;

        // ConnectionStrings__Dungeon: Postgres (e.g. Neon) for progress and the
        // cookie-signing keys. Without it, both last only until a restart.
        if (config.GetConnectionString("Dungeon") is { Length: > 0 } connectionString)
        {
            var db = NpgsqlDataSource.Create(PostgresDatabase.NormalizeConnectionString(connectionString));
            services.AddSingleton(db);
            services.AddSingleton<IProgressStore, PostgresProgressStore>();
            services.AddDataProtection().SetApplicationName("PlaywrightDungeonCrawl");
            services.Configure<KeyManagementOptions>(options => options.XmlRepository = new PostgresXmlRepository(db));
        }
        else
        {
            services.AddSingleton<IProgressStore, InMemoryProgressStore>();
        }

        var auth = services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "pdc-auth";
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;
                // Only the progress API needs a player: answer it with 401, not a redirect.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
            });
        if (IsEnabled(config))
        {
            auth.AddGitHub(GitHubScheme, options =>
            {
                options.ClientId = config["GitHub:ClientId"]!;
                options.ClientSecret = config["GitHub:ClientSecret"]!;
            });
        }
        services.AddAuthorization();
    }

    public static async Task PrepareAccountsAsync(this WebApplication app)
    {
        if (app.Services.GetService<NpgsqlDataSource>() is { } db)
        {
            await PostgresDatabase.EnsureCreatedAsync(db);
        }
        else if (IsEnabled(app.Configuration) && !app.Environment.IsDevelopment())
        {
            app.Logger.LogWarning("GitHub sign-in is on but ConnectionStrings__Dungeon isn't set: " +
                                  "saved progress and sign-ins will be lost on every restart.");
        }
    }

    public static void MapAccounts(this WebApplication app)
    {
        if (IsEnabled(app.Configuration))
        {
            app.MapGet("/signin", (string? returnUrl) => Results.Challenge(
                new AuthenticationProperties { RedirectUri = LocalPath(returnUrl), IsPersistent = true },
                [GitHubScheme]));
        }

        // A form post, so another site can't sign you out with a link.
        app.MapPost("/signout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/");
        });

        var progress = app.MapGroup("/api/progress").RequireAuthorization();

        // The browser sends what it has; the reply is that merged with what's saved.
        progress.MapPut("", async (PlayerProgress body, ClaimsPrincipal user, DungeonCatalog catalog, IProgressStore store) =>
            Results.Ok(await store.MergeAsync(PlayerId(user), user.Identity!.Name ?? "", body.Sanitize(catalog))));

        progress.MapDelete("", async (ClaimsPrincipal user, IProgressStore store) =>
        {
            await store.ResetAsync(PlayerId(user));
            return Results.NoContent();
        });
    }

    /// <summary>Only same-site paths, so sign-in can't be used to bounce people elsewhere.</summary>
    public static string LocalPath(string? returnUrl) =>
        returnUrl is ['/', not '/' and not '\\', ..] or "/" ? returnUrl : "/";

    private static string PlayerId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
