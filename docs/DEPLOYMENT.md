# Deploying LearnCloud: Railway and Cloudflare Workers

```
Browser ──► Cloudflare Worker: learncloud-app ──► Railway: LearnCloud API ──► Railway: PostgreSQL
            static web app                        Docker image, port $PORT
            /api/* proxy (worker/apiProxy.js)     migrations run before each deploy

Browser ──► Cloudflare Worker: learncloud ──────► Railway: LearnCloud API
            marketing site (static files)         (only the demo and contact form)
```

- **Web app** (`src/LearnCloud.Web`): a Cloudflare Worker with static assets, configured by
  `src/LearnCloud.Web/wrangler.jsonc`. Every `/api/*` request goes through
  `worker/apiProxy.js` to the API.
- **Marketing site** (`marketing-site`): a separate Worker, configured by
  `marketing-site/wrangler.jsonc`: static files, plus `worker/index.js`, which forwards the
  demo and contact forms to the API.
- **API** (`src/LearnCloud.Api`): built from `deployment/docker/Dockerfile.api` by Railway,
  configured by `railway.json` at the repository root.
- **Database**: Railway PostgreSQL. The schema comes from the EF Core migrations, applied by
  Railway's pre-deploy command on every deploy.

Why the proxy: the refresh token is an `HttpOnly`, `Secure`, `SameSite=Strict` cookie. A
browser only sends it to the site that set it, so the API has to appear on the web app's
own origin. The proxy also means the web app needs no CORS.

---

## 1. Railway: database and API

### Create the services

1. Create a Railway project.
2. **Add PostgreSQL** (New → Database → PostgreSQL).
3. **Add the API**: New → GitHub Repo → this repository. Leave the root directory as `/`.
   Railway reads `railway.json`, which sets:
   - Docker build from `deployment/docker/Dockerfile.api`
   - pre-deploy command `dotnet LearnCloud.Api.dll --migrate`
   - health check `/health/ready` (the database must answer), 120 s timeout
   - restart on failure

### API variables

Set these on the API service (Variables tab). Generate secrets with a password manager or
`openssl rand -base64 48`; never reuse the local development values.

| Variable | Value | Notes |
|---|---|---|
| `DATABASE_URL` | `${{Postgres.DATABASE_URL}}` | Railway reference to the database service. Converted to an Npgsql connection string at startup. |
| `Jwt__Secret` | 48+ random characters | Required. Changing it signs everyone out. |
| `Jwt__Issuer` | `https://api.learncloud.co.zw` | Must stay stable once users have tokens. |
| `Jwt__Audience` | `learncloud` | Same. |
| `Proxy__SharedSecret` | 32+ random characters | Must equal `API_PROXY_SECRET` on both Cloudflare Workers. Lets the API trust the client IP the proxy reports, for rate limiting. |
| `Proxy__RequireSecret` | `true` | Refuses requests that did not come through the Workers (403), so the Railway address cannot be used directly. Health checks stay open. Needs `Proxy__SharedSecret`. |
| `App__PublicUrl` | `https://app.learncloud.co.zw` | Required. The web app's address, used in links in emails (password reset, email confirmation). Until the custom domain is set up, use the Worker's address, e.g. `https://learncloud-app.<account>.workers.dev`. Without it the API logs a warning and the links do not work. |
| `Email__Provider` | `Resend` | `Resend` (HTTPS API), `Smtp`, or `Log` (sends nothing; the API warns at startup). Railway blocks outbound SMTP below the Pro plan, so use Resend there. |
| `Email__FromAddress` | `noreply@learncloud.co.zw` | Sender address on a domain verified with the provider. |
| `Email__FromName` | `LearnCloud` | Optional. |
| `Email__Resend__ApiKey` | secret | From resend.com, with sending access for the verified domain. |
| `Email__Smtp__Host`, `__Port`, `__Username`, `__Password`, `__Security` | only for `Smtp` | `Security`: `Auto`, `StartTls` or `SslOnConnect`. |
| `Sales__NotificationEmail` | `sales@learncloud.co.zw` | Receives demo requests and contact messages from the marketing site. Without it they are only stored (`GET /api/platform/enquiries`). |
| `Billing__ContactEmail` | `billing@learncloud.co.zw` | Named in read-only banners and billing emails. |
| `Billing__EnforceReadOnly` | `true` | Suspended, expired, cancelled and archived schools can read and export but not change records. Set `false` to let every school keep editing, e.g. while payments are not set up yet (see below). |
| `Jobs__Enabled` | `true` | Runs the background job worker (emails, message batches) and the schedules (daily dunning, hourly communication rules). Safe on every replica. |
| `Cors__AllowedOrigins__0` | optional | Only for other browser clients; the web app does not need CORS. |
| `AI__OpenAI__ApiKey` | optional | Without it AI features use the rule-based provider. |

SMS is not connected to a provider yet: SMS message batches fail with "SMS is not available
yet" instead of pretending to send.

#### Trials and read-only mode

New schools get a 14-day trial. The daily dunning job emails reminders on days 7 and 12
and, when the trial ends, makes the school read-only for 30 days, then archives it. There is
no payment page yet, so decide before launch: either extend trials from the platform admin
API (`POST /api/platform/overrides/extend-trial`), or set
`Billing__EnforceReadOnly=false` until payments exist. Schools see a banner in the web app
in either case.

Already set by the image, no action needed: `ASPNETCORE_ENVIRONMENT=Production`,
`ForwardedHeaders__Enabled=true`. Railway injects `PORT`; the API binds to it.

### Service settings

- **Networking → Generate Domain.** Note the URL; it becomes `API_ORIGIN` on Cloudflare.
- **Source → Wait for CI: on.** Railway then deploys only commits whose GitHub Actions
  checks pass (`.github/workflows/ci.yml`).
- **Replicas:** more than one is fine. Background jobs and schedules live in PostgreSQL
  (`background_jobs`, `scheduled_jobs`) and are claimed with leases, so each job runs once.
  Rate limits are counted per instance.

### Backups

Enable backups on the PostgreSQL service (Backups tab); availability and retention depend
on the Railway plan. Test a restore into a staging environment before go-live.

---

## 2. Cloudflare Worker: web app (`learncloud-app`)

`src/LearnCloud.Web/wrangler.jsonc` defines the Worker:

- static files from `dist`, with unknown paths answered by `index.html` so deep links work;
- only `/api/*` runs the Worker code, which proxies to `API_ORIGIN`;
- `public/_headers` sets the security headers and asset caching of the static files.

### Deploy

Requires Node 22+ (Wrangler 4.131 is pinned in `package.json`).

```bash
cd src/LearnCloud.Web
npm ci
npx wrangler login          # once per machine
npm run deploy:check        # builds and bundles, deploys nothing
npm run deploy              # builds and deploys learncloud-app
```

The first deploy creates the Worker at `https://learncloud-app.<account>.workers.dev`.

`VITE_ROOT_DOMAIN` is baked into the build, so set it in the shell that runs `npm run deploy`
(or in `.env.production.local`) when a platform domain is in use.

### Variables

Set on the Worker (Workers & Pages → learncloud-app → Settings → Variables and Secrets).
`keep_vars` in `wrangler.jsonc` keeps them across deploys.

| Variable | Type | Value |
|---|---|---|
| `API_ORIGIN` | text | The Railway API domain, e.g. `https://learncloud-api-production.up.railway.app` |
| `API_PROXY_SECRET` | secret | Same value as `Proxy__SharedSecret` on Railway (`npx wrangler secret put API_PROXY_SECRET`) |
| `ROOT_DOMAIN` | text | `learncloud.co.zw`, only once school subdomains are routed to this Worker |

Until `API_ORIGIN` is set, the app loads but every `/api` call returns
`503 LearnCloud is not connected yet`, so nobody can sign in. That is the expected state
before the API is deployed, not a failed Worker deploy: the Worker itself is serving. The
missing variable is named in the Worker's log, not shown to visitors.

### Deploying from GitHub instead (optional)

Workers & Pages → learncloud-app → Settings → Build → Connect: repository
`OOPGen/LearnCloud`, branch `main`, root directory `src/LearnCloud.Web`, build command
`npm ci && npm run build`, deploy command `npx wrangler deploy`. Cloudflare then deploys
every push to `main` without waiting for the CI workflow, so merge to `main` only through
branches whose CI has passed.

### Domains and school addresses

- Add the main hostname (for example `app.learncloud.co.zw`) under Settings → Domains &
  Routes → Custom Domain. The zone must be on Cloudflare.
- On a host without a school subdomain, the sign-in page asks for the **school code**.
- On `<school>.learncloud.co.zw` the school comes from the address, and the proxy forwards it
  so the API rejects a token from another school. Custom Domains do not accept wildcards;
  per-school subdomains need a Worker **route** `*.learncloud.co.zw/*` plus a proxied
  wildcard DNS record, and `ROOT_DOMAIN` / `VITE_ROOT_DOMAIN` set. Check that hostnames with
  their own Worker (the marketing site's `www`) still reach it. Until then, schools use the
  main hostname with their school code.

---

## 3. Cloudflare Worker: marketing site (`learncloud`)

`marketing-site/public` is served as static files. The site routes pages such as `/pricing`
in the browser, so `wrangler.jsonc` answers unknown paths with `index.html`.

The demo and contact forms post to `/api/public/enquiries` on the marketing site itself;
`worker/index.js` forwards only that request to the API. Set on the `learncloud` Worker:

| Variable | Type | Value |
|---|---|---|
| `API_ORIGIN` | text | The Railway API domain, as for the web app |
| `API_PROXY_SECRET` | secret | Same value as `Proxy__SharedSecret` |

```bash
cd marketing-site
npx wrangler@4.131.2 deploy
```

The analytics ID in `public/index.html` (`G-LEARNCLD`) is still a placeholder.

---

## 4. Staging and production

- **Railway:** create a `staging` environment in the project (its own database and variables).
- **Cloudflare:** deploy a separate staging Worker with
  `npx wrangler deploy --name learncloud-app-staging` (after `npm run build`), and point its
  `API_ORIGIN` at the staging API.
- Keep secrets different between environments.

## 5. Verify a deploy

From a machine with Node 18+:

```bash
# Read-only, safe against production:
node scripts/smoke-test.mjs https://app.learncloud.co.zw

# Full flow, STAGING ONLY (registers two throwaway schools):
node scripts/smoke-test.mjs https://learncloud-app-staging.<account>.workers.dev --full
```

The full run checks registration, sign-in, the refresh cookie flags, cookie refresh,
Subjects create/list/delete, validation, setting up a year, term and class, enrolling a
student with a guardian and recording them leaving, and that one school cannot see or use
another's subjects, students, guardians or classes.

## 6. Rollback

- **API:** Railway → Deployments → choose the last good deployment → Redeploy. Migrations
  only move forward: rolling code back past a schema change can fail. Ship schema changes
  in backward-compatible steps (add columns first, remove them in a later release).
- **Web app and marketing site:** Workers & Pages → the Worker → Deployments → Rollback, or
  `npx wrangler rollback` from the Worker's folder.

## 7. Security notes

- With `Proxy__RequireSecret=true` the Railway domain only answers health checks; everything
  else must come through the Workers. Without it, anyone can call the API directly: tenant
  isolation, authentication and validation still apply, and only rate limiting is weaker,
  because direct callers can influence `X-Forwarded-For`.
- Emails are queued as background jobs. A job's payload (which can hold a password reset
  link) is replaced once the email is sent or has finally failed, and finished jobs are
  deleted after 14 days (succeeded) or 90 days (failed).
- Rotate `Jwt__Secret` and `Proxy__SharedSecret` if they may have leaked. Rotating the JWT
  secret signs everyone out; rotate the proxy secret on both sides together.
- The production image runs as a non-root user and serves plain HTTP inside Railway; TLS is
  terminated by Railway and Cloudflare.
