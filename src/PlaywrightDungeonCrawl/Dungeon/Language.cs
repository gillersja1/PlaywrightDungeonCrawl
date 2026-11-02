namespace PlaywrightDungeonCrawl.Dungeon;

/// <summary>The language a player writes their tests in. The rooms are the same either way; the teaching differs.</summary>
public enum CodeLanguage { CSharp, TypeScript }

/// <summary>
/// Which language to teach in. The choice lives in a cookie so the server can
/// render (and grade) lessons, riddles and nudges in it. Any page takes
/// <c>?lang=ts</c> or <c>?lang=csharp</c> to switch, which makes links like
/// <c>/?lang=ts</c> shareable.
/// </summary>
public static class Languages
{
    public const string Cookie = "dungeon-lang";
    public const string Query = "lang";

    public static CodeLanguage Current(HttpContext context) =>
        Parse(context.Request.Cookies[Cookie]) ?? CodeLanguage.CSharp;

    public static CodeLanguage? Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "ts" or "typescript" => CodeLanguage.TypeScript,
        "csharp" or "cs" or "c#" => CodeLanguage.CSharp,
        _ => null,
    };

    public static string Code(this CodeLanguage language) => language == CodeLanguage.TypeScript ? "ts" : "csharp";

    public static string DisplayName(this CodeLanguage language) => language == CodeLanguage.TypeScript ? "TypeScript" : "C#";

    /// <summary>The language a floor is taught in: TypeScript only where it's been written.</summary>
    public static CodeLanguage For(this Floor floor, CodeLanguage wanted) =>
        wanted == CodeLanguage.TypeScript && floor.HasTypeScript ? CodeLanguage.TypeScript : CodeLanguage.CSharp;

    /// <summary>A GET with ?lang= saves the choice and redirects to the same page without it.</summary>
    public static void UseLanguageSwitch(this WebApplication app) => app.Use(async (context, next) =>
    {
        if (HttpMethods.IsGet(context.Request.Method)
            && context.Request.Query.TryGetValue(Query, out var value)
            && Parse(value) is { } language)
        {
            context.Response.Cookies.Append(Cookie, language.Code(), new CookieOptions
            {
                MaxAge = TimeSpan.FromDays(365),
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                IsEssential = true,
            });
            var rest = context.Request.Query
                .Where(q => !string.Equals(q.Key, Query, StringComparison.OrdinalIgnoreCase))
                .SelectMany(q => q.Value.Select(v => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(v ?? "")}"));
            var query = string.Join('&', rest);
            context.Response.Redirect($"{context.Request.PathBase}{context.Request.Path}{(query.Length > 0 ? "?" + query : "")}");
            return;
        }
        await next();
    });
}
