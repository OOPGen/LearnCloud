using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace LearnCloud.Api.Middleware;

// Sets the connection's remote IP from the Cloudflare Pages proxy's X-LearnCloud-Client-IP
// header, but only when the request also carries the shared proxy secret.
//
// Rate limiting and security logs key on the client IP. Behind Cloudflare and Railway the
// TCP peer is a proxy, and X-Forwarded-For can be supplied by anyone who reaches the API
// directly. A header trusted only with a secret known to the Pages function cannot be
// spoofed that way, whatever the edge in between does to X-Forwarded-For.
//
// Configure Proxy:SharedSecret on the API and API_PROXY_SECRET on the Pages project to
// the same random value. Without it the middleware does nothing.
public sealed class TrustedProxyClientIpMiddleware
{
    public const string SecretHeader = "X-LearnCloud-Proxy-Key";
    public const string ClientIpHeader = "X-LearnCloud-Client-IP";

    private readonly RequestDelegate _next;
    private readonly byte[]? _secret;

    public TrustedProxyClientIpMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        var secret = configuration["Proxy:SharedSecret"];
        _secret = string.IsNullOrWhiteSpace(secret) ? null : Encoding.UTF8.GetBytes(secret);
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Request.Headers;
        if (_secret is not null
            && headers.TryGetValue(SecretHeader, out var presented)
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented.ToString()), _secret)
            && IPAddress.TryParse(headers[ClientIpHeader].ToString(), out var clientIp))
        {
            context.Connection.RemoteIpAddress = clientIp;
        }

        // Never pass the secret further down the pipeline (logs, diagnostics).
        headers.Remove(SecretHeader);
        return _next(context);
    }
}
