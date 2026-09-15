// Worker for the LearnCloud web app (wrangler.jsonc).
//
// Static files are served by Cloudflare's asset handling before this code runs, with
// unknown paths answered by index.html for client-side routing. run_worker_first sends
// only /api/* here, so the proxy keeps the API on the web app's own origin.

import { proxyApiRequest } from './apiProxy.js';

export default {
  async fetch(request, env) {
    const { pathname } = new URL(request.url);
    if (pathname.startsWith('/api/')) return proxyApiRequest(request, env);
    return env.ASSETS.fetch(request);
  },
};
