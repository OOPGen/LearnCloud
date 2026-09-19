using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace LearnCloud.Api.Middleware;

// Sets the connection's remote IP from the Cloudflare Worker proxy's X-LearnCloud-Client-IP
// header, but only when the request also carries the shared proxy secret.
//
// Rate limiting and security logs key on the client IP. Behind Cloudflare and Railway the
// TCP peer is a proxy, and X-Forwarded-For can be supplied by anyone who reaches the API
// directly. A header trusted only with a secret known to the Worker proxy cannot be
// spoofed that way, whatever the edge in between does to X-Forwarded-For.
//
// Configure Proxy:SharedSecret on the API and API_PROXY_SECRET on the web app Worker to
// the same random value. Without it the middleware does nothing.
//
// With Proxy:RequireSecret=true as well, requests without the secret are refused (403), so
// the API cannot be used directly on its Railway address, only through the Workers. Health
// endpoints stay open for Railway's health check.
public sealed class TrustedProxyClientIpMiddleware
{
    public const string SecretHeader = "X-LearnCloud-Proxy-Key";
    public const string ClientIpHeader = "X-LearnCloud-Client-IP";

    private readonly RequestDelegate _next;
    private readonly byte[]? _secret;
    private readonly bool _requireSecret;

    public TrustedProxyClientIpMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        var secret = configuration["Proxy:SharedSecret"];
        _secret = string.IsNullOrWhiteSpace(secret) ? null : Encoding.UTF8.GetBytes(secret);
        _requireSecret = configuration.GetValue("Proxy:RequireSecret", false);
        if (_requireSecret && _secret is null)
            throw new InvalidOperationException("Proxy:RequireSecret is true but Proxy:SharedSecret is not set.");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Request.Headers;
        var fromProxy = _secret is not null
            && headers.TryGetValue(SecretHeader, out var presented)
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented.ToString()), _secret);

        if (fromProxy && IPAddress.TryParse(headers[ClientIpHeader].ToString(), out var clientIp))
            context.Connection.RemoteIpAddress = clientIp;

        // Never pass the secret further down the pipeline (logs, diagnostics).
        headers.Remove(SecretHeader);

        if (_requireSecret && !fromProxy && !context.Request.Path.StartsWithSegments("/health"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { title = "Direct access is not allowed", detail = "Use the LearnCloud web address." });
            return;
        }

        await _next(context);
    }
}
