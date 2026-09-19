// Proxies every /api/* request from the web app's Worker to the LearnCloud API on Railway.
//
// Why a proxy rather than calling Railway from the browser:
// - The refresh token is a SameSite=Strict, __Host- cookie. It is only sent to the
//   origin that set it, so the API must share the web app's origin.
// - No CORS configuration is needed for the web app.
//
// Worker variables (Workers & Pages > learncloud-app > Settings > Variables and Secrets):
//   API_ORIGIN   required, e.g. https://learncloud-api-production.up.railway.app
//   ROOT_DOMAIN  optional, e.g. learncloud.co.zw; enables school-from-subdomain
//   API_PROXY_SECRET  secret; the same value as Proxy__SharedSecret on the API, so the API
//                     trusts the client IP this proxy reports and, with
//                     Proxy__RequireSecret, accepts only requests from the Workers

const RESERVED_SUBDOMAINS = new Set(['www', 'app', 'api', 'admin', 'platform']);

// Headers Cloudflare adds that the API has no use for.
const STRIPPED_HEADERS = [
  'host', 'cf-connecting-ip', 'cf-ipcountry', 'cf-ray', 'cf-visitor', 'cdn-loop', 'x-real-ip',
  // Only this proxy may set these; see TrustedProxyClientIpMiddleware in the API.
  'x-learncloud-proxy-key', 'x-learncloud-client-ip',
];

export function tenantSlugFromHost(hostname, rootDomain) {
  if (!rootDomain) return null;
  const host = hostname.toLowerCase();
  const root = rootDomain.toLowerCase();
  if (!host.endsWith(`.${root}`)) return null;
  const slug = host.slice(0, -(root.length + 1));
  return /^[a-z0-9-]{3,50}$/.test(slug) && !RESERVED_SUBDOMAINS.has(slug) ? slug : null;
}

export async function proxyApiRequest(request, env) {
  if (!env.API_ORIGIN) {
    // The visitor gets plain language; the fix goes to the Worker log, where it belongs.
    console.error('API_ORIGIN is not set on this Worker, so every /api request fails.');
    return Response.json(
      {
        title: 'LearnCloud is not connected yet',
        detail: 'This address is live but its server is not connected yet, so signing in is not available. Please try again later.',
      },
      { status: 503, headers: { 'content-type': 'application/problem+json', 'retry-after': '3600' } },
    );
  }

  const incoming = new URL(request.url);
  const target = new URL(incoming.pathname + incoming.search, env.API_ORIGIN);

  const headers = new Headers(request.headers);
  for (const name of STRIPPED_HEADERS) headers.delete(name);

  // The browser must not choose its tenant. The API rejects a token whose school differs
  // from this header, so it is derived only from the host the user actually visited.
  headers.delete('x-tenant-slug');
  const slug = tenantSlugFromHost(incoming.hostname, env.ROOT_DOMAIN);
  if (slug) headers.set('x-tenant-slug', slug);

  // Replace, never append to, any client-supplied X-Forwarded-For: the API rate limits
  // by client IP, and appending would let a caller pick their own address.
  const clientIp = request.headers.get('cf-connecting-ip');
  if (clientIp) headers.set('x-forwarded-for', clientIp);
  else headers.delete('x-forwarded-for');
  headers.set('x-forwarded-proto', 'https');
  headers.set('x-forwarded-host', incoming.host);
  if (env.API_PROXY_SECRET) {
    headers.set('x-learncloud-proxy-key', env.API_PROXY_SECRET);
    if (clientIp) headers.set('x-learncloud-client-ip', clientIp);
  }

  const hasBody = !['GET', 'HEAD'].includes(request.method);
  let upstream;
  try {
    upstream = await fetch(target, {
      method: request.method,
      headers,
      body: hasBody ? request.body : undefined,
      redirect: 'manual',
    });
  } catch {
    return Response.json(
      { title: 'API unavailable', detail: 'The LearnCloud API could not be reached. Please try again shortly.' },
      { status: 502, headers: { 'content-type': 'application/problem+json' } },
    );
  }

  // Pass status, body and headers through unchanged, including every Set-Cookie.
  return new Response(upstream.body, upstream);
}
