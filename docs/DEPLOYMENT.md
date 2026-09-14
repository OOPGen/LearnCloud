# Deploying LearnCloud: Railway and Cloudflare Pages

```
Browser ──► Cloudflare Pages ──────────────► Railway: LearnCloud API ──► Railway: PostgreSQL
            static web app                   Docker image, port $PORT
            /api/* Pages Function (proxy)    migrations run before each deploy
```

- **Web app** (`src/LearnCloud.Web`): static files on Cloudflare Pages. Every `/api/*`
  request goes through the Pages Function in `functions/api/[[path]].js` to the API.
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
| `Proxy__SharedSecret` | 32+ random characters | Must equal `API_PROXY_SECRET` on Cloudflare. Lets the API trust the client IP the proxy reports, for rate limiting. |
| `Jobs__Enabled` | `true` | Scheduled dunning and communication rules. Set `false` on extra replicas. |
| `Cors__AllowedOrigins__0` | optional | Only for other browser clients; the web app does not need CORS. |
| `AI__OpenAI__ApiKey` | optional | Without it AI features use the rule-based provider. |
| `Messaging__Sms__ApiKey`, `Messaging__Email__SmtpHost`, ... | optional | Needed only to actually send SMS and email. |

Already set by the image, no action needed: `ASPNETCORE_ENVIRONMENT=Production`,
`ForwardedHeaders__Enabled=true`. Railway injects `PORT`; the API binds to it.

### Service settings

- **Networking → Generate Domain.** Note the URL; it becomes `API_ORIGIN` on Cloudflare.
- **Source → Wait for CI: on.** Railway then deploys only commits whose GitHub Actions
  checks pass (`.github/workflows/ci.yml`).
- **Replicas:** start with one. The in-process message queue and scheduled jobs assume a
  single instance until a durable job store is added.

### Backups

Enable backups on the PostgreSQL service (Backups tab); availability and retention depend
on the Railway plan. Test a restore into a staging environment before go-live.

---

## 2. Cloudflare Pages: web app

1. Workers & Pages → Create → Pages → Connect to Git → this repository.
2. Build settings:
   - Production branch: `main`
   - Framework preset: `None`
   - Root directory: `src/LearnCloud.Web`
   - Build command: `npm ci && npm run build`
   - Build output directory: `dist`
3. Variables (Settings → Variables and Secrets), for **Production** and, with staging values,
   **Preview**:

| Variable | Type | Value |
|---|---|---|
| `API_ORIGIN` | text | The Railway API domain, e.g. `https://learncloud-api-production.up.railway.app` |
| `API_PROXY_SECRET` | secret | Same value as `Proxy__SharedSecret` on Railway |
| `ROOT_DOMAIN` | text | `learncloud.co.zw` (omit on preview hosts) |
| `VITE_ROOT_DOMAIN` | text, build time | `learncloud.co.zw` (omit on preview hosts) |
| `NODE_VERSION` | text, build time | `20` |

`functions/` sits inside the root directory, so Pages deploys the proxy automatically.
`public/_headers` sets the web app's security headers and asset caching.

### Domains and school addresses

- Add the main custom domain (for example `app.learncloud.co.zw`) to the Pages project.
- On a host without a school subdomain, the sign-in page asks for the **school code**.
- On `<school>.learncloud.co.zw` the school comes from the address, and the proxy forwards it
  so the API rejects a token from another school. Cloudflare Pages custom domains do not
  accept a wildcard, so per-school subdomains need either a custom domain per school or a
  Worker route on `*.learncloud.co.zw/*` in front of Pages. Until then, schools use the
  main domain with their school code.

---

## 3. Staging and production

- **Railway:** create a `staging` environment in the project (its own database and variables).
- **Cloudflare:** preview deployments use the Preview variables; point their `API_ORIGIN` at
  the staging API.
- Keep secrets different between environments.

## 4. Verify a deploy

From a machine with Node 18+:

```bash
# Read-only, safe against production:
node scripts/smoke-test.mjs https://app.learncloud.co.zw

# Full flow, STAGING ONLY (registers two throwaway schools):
node scripts/smoke-test.mjs https://<staging-pages-host> --full
```

The full run checks registration, sign-in, the refresh cookie flags, cookie refresh,
Subjects create/list/delete, validation, and that one school cannot see another's data.

## 5. Rollback

- **API:** Railway → Deployments → choose the last good deployment → Redeploy. Migrations
  only move forward: rolling code back past a schema change can fail. Ship schema changes
  in backward-compatible steps (add columns first, remove them in a later release).
- **Web:** Cloudflare Pages → Deployments → Rollback to a previous deployment.

## 6. Security notes

- The Railway domain is publicly reachable, so anyone can bypass Cloudflare and call the API
  directly. Tenant isolation, authentication and validation all live in the API and still
  apply. Only the client IP used for rate limiting is affected: direct callers can influence
  `X-Forwarded-For`, but not the proxy-reported IP, which requires `Proxy__SharedSecret`.
- Rotate `Jwt__Secret` and `Proxy__SharedSecret` if they may have leaked. Rotating the JWT
  secret signs everyone out; rotate the proxy secret on both sides together.
- The production image runs as a non-root user and serves plain HTTP inside Railway; TLS is
  terminated by Railway and Cloudflare.
