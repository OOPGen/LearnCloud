# Archived self-hosted and Supabase deployment

Retired in Phase 3 (September 2026) when the hosting target became **Railway** for the
API and PostgreSQL, and **Cloudflare Pages** for the web app. Current instructions:
`docs/DEPLOYMENT.md`.

What is here and why it no longer applies:

| Item | Replaced by |
|---|---|
| `docker-compose.yml` (Postgres, Redis, API, nginx, certbot, backup containers on one server) | Railway services; TLS and routing by Railway and Cloudflare |
| `docker-compose.supabase.yml`, `SUPABASE_DEPLOYMENT.md`, `GITHUB_TO_SUPABASE_GUIDE.md` | Railway PostgreSQL |
| `docker/Dockerfile.web`, `docker/web-entrypoint.sh`, `nginx/` | Cloudflare Pages static hosting plus the `/api` Pages Function |
| `docker/Dockerfile.backup`, `scripts/backup.sh`, `scripts/verify-backup.sh`, `docs/restore-runbook.md` | Railway database backups (see DEPLOYMENT.md) |
| `scripts/migrate-live.sh`, `docs/migration-policy.md` | EF Core migrations applied by Railway's pre-deploy command |
| `monitoring/` (MySQL slow log, disk checks) | Railway metrics and logs |
| `docs/go-live-checklist.md`, `docs/env-reference.md` | `docs/DEPLOYMENT.md` |

Several of these files still describe MySQL and the hand-written SQL scripts that were
retired in Phase 2. None of them were exercised against the current code.
