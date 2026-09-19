// Worker for the marketing site (wrangler.jsonc). Static files are served by Cloudflare's
// asset handling; only /api/* reaches this code, and the only request it forwards is the
// demo and contact form submission, to the LearnCloud API.
//
// Variables (Workers & Pages > learncloud > Settings > Variables and Secrets):
//   API_ORIGIN        the API address, e.g. https://learncloud-api-production.up.railway.app
//   API_PROXY_SECRET  secret; the same value as Proxy__SharedSecret on the API, so the API
//                     accepts the request when direct access is locked down and can rate
//                     limit per visitor

const ENQUIRY_PATH = '/api/public/enquiries';

function problem(status, title, detail) {
  return Response.json({ title, detail }, { status, headers: { 'content-type': 'application/problem+json' } });
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname !== ENQUIRY_PATH) {
      return url.pathname.startsWith('/api/') ? problem(404, 'Not found') : env.ASSETS.fetch(request);
    }
    if (request.method !== 'POST') return problem(405, 'Method not allowed');
    if (!env.API_ORIGIN) return problem(502, 'Form not connected', 'Set API_ORIGIN on the marketing Worker.');
    if (!(request.headers.get('content-type') || '').includes('application/json')) return problem(415, 'Send JSON');

    const body = await request.text();
    if (body.length > 10_000) return problem(413, 'Too large');

    const headers = { 'content-type': 'application/json', 'x-forwarded-proto': 'https' };
    const clientIp = request.headers.get('cf-connecting-ip');
    if (clientIp) headers['x-forwarded-for'] = clientIp;
    if (env.API_PROXY_SECRET) {
      headers['x-learncloud-proxy-key'] = env.API_PROXY_SECRET;
      if (clientIp) headers['x-learncloud-client-ip'] = clientIp;
    }

    try {
      const upstream = await fetch(new URL(ENQUIRY_PATH, env.API_ORIGIN), { method: 'POST', headers, body, redirect: 'manual' });
      // Pass the status and body through, but no upstream headers such as cookies.
      return new Response(upstream.body, {
        status: upstream.status,
        headers: { 'content-type': upstream.headers.get('content-type') || 'application/json' },
      });
    } catch {
      return problem(502, 'Form unavailable', 'Please try again shortly or email hello@learncloud.co.zw.');
    }
  },
};
