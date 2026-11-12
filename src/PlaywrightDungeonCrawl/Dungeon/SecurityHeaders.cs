using System.Security.Cryptography;

namespace PlaywrightDungeonCrawl;

/// <summary>
/// Security headers on every response, the ones Gremlin found missing. The Content-Security-Policy lets
/// scripts run only from this site, from unpkg (Grafana Faro's SDK), or inline with this request's nonce,
/// so an injected script wouldn't run even if something let one onto a page.
/// </summary>
public static class SecurityHeaders
{
    private const string NonceKey = "csp-nonce";

    /// <summary>This request's nonce: put it on every inline &lt;script&gt; as nonce="...".</summary>
    public static string CspNonce(this HttpContext context) =>
        context.Items[NonceKey] as string ?? throw new InvalidOperationException("UseSecurityHeaders hasn't run for this request.");

    /// <summary>The policy for one response; <paramref name="faroOrigin"/> is where Faro sends its data, if it's on.</summary>
    public static string Policy(string nonce, string? faroOrigin) => string.Join("; ",
        "default-src 'self'",
        $"script-src 'self' 'nonce-{nonce}' https://unpkg.com",
        // Inline style attributes are used throughout (torch colors, room layouts); styles can't run code.
        "style-src 'self' 'unsafe-inline'",
        // GitHub avatars on the leaderboard redirect to avatars.githubusercontent.com.
        "img-src 'self' data: https://github.com https://avatars.githubusercontent.com",
        $"connect-src 'self'{(faroOrigin is null ? "" : " " + faroOrigin)}",
        // The mirror rooms (floors 5 and 7) frame /frames/mirror.html from this site, so framing is same-origin, not none.
        "frame-src 'self'",
        "frame-ancestors 'self'",
        "form-action 'self'",
        "base-uri 'self'",
        "object-src 'none'");

    public static void UseSecurityHeaders(this WebApplication app)
    {
        var faroOrigin = app.Configuration["Faro:CollectorUrl"] is { Length: > 0 } url && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Authority)
            : null;

        app.Use(async (context, next) =>
        {
            // URL-safe base64: CSP accepts it, and it needs no escaping in an HTML attribute.
            var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            context.Items[NonceKey] = nonce;
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = Policy(nonce, faroOrigin);
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "SAMEORIGIN";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = PermissionsPolicy(context.Request.Path);
            await next();
        });
    }

    /// <summary>
    /// Nothing gets the camera, microphone, location or payments, except that floor 18's Belfry may ask where the
    /// (emulated) traveler is standing, on its own page only.
    /// </summary>
    public static string PermissionsPolicy(PathString path) =>
        path.Equals("/rooms/belfry", StringComparison.OrdinalIgnoreCase)
            ? "camera=(), microphone=(), geolocation=(self), payment=()"
            : "camera=(), microphone=(), geolocation=(), payment=()";
}
