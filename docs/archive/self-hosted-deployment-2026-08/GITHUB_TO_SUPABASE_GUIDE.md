# GitHub to Supabase Deployment Guide - LearnCloud
**Date:** 2026-08-09
**Flow:** GitHub Repo → GitHub Actions → Supabase DB (migrations) + Fly.io/Render (API) + Vercel (Web)

## Overview

Supabase does NOT host .NET API (it hosts Postgres, Storage, Auth, Realtime, Edge Functions in Deno). So flow is:

**GitHub (code) → GitHub Actions (CI/CD) →**
- **Supabase DB:** Apply migrations via direct connection (psql or dotnet ef or supabase CLI)
- **Supabase Storage:** Create buckets with RLS (via SQL or supabase CLI)
- **API (.NET):** Deploy to Fly.io / Render / Azure (takes ConnectionStrings__Default = Supabase pooled URI)
- **Web (Vite React):** Deploy to Vercel / Netlify / Fly.io

Supabase has GitHub integration for DB migrations via `supabase` CLI and `supabase-github-action`.

## Step 1: Push to GitHub

### Create GitHub Repo

```bash
# On your local machine (where you have the pack)
cd LearnCloud
git init
git add .
git commit -m "Initial: LearnCloud with Postgres migration, 18 migrations translated, 302 endpoints, tenant isolation, security fixes, performance fixes, App Shell premium"

# Create repo on GitHub via gh CLI or web
gh repo create learncloud --public --source=. --remote=origin --push
# Or manual:
# git remote add origin https://github.com/YOUR_USERNAME/learncloud.git
# git branch -M main
# git push -u origin main
```

### Add .gitignore (already have, but ensure)

```
# .gitignore for LearnCloud - Postgres + Supabase
bin/
obj/
dist/
node_modules/
.env
.env.production
.env.local
appsettings.Development.json
*.log
backup_unused/
uploads/
exports/
*.db
*.sqlite
```

**Never commit `.env.production` with real secrets** - it lives in `/opt/learncloud/.env.production` outside repo and in GitHub Secrets.

## Step 2: Set Up GitHub Secrets for Supabase

Go to GitHub → Your Repo → Settings → Secrets and variables → Actions → New repository secret

Add these secrets (get from Supabase Dashboard → Database → Connection string + API):

| Secret Name | Value | Where From |
|---|---|---|
| `SUPABASE_DB_POOL_URL` | `postgres://postgres.[ref]:[pass]@aws-0-[region].pooler.supabase.com:6543/postgres?pgbouncer=true` | Supabase Dashboard → Database → Connection string → URI → Pooled (6543) |
| `SUPABASE_DB_DIRECT_URL` | `postgres://postgres:[pass]@db.[ref].supabase.co:5432/postgres` | Same → Direct (5432) for migrations |
| `SUPABASE_URL` | `https://[ref].supabase.co` | Dashboard → API → Project URL |
| `SUPABASE_ANON_KEY` | `eyJhbG...` anon key | Dashboard → API → anon key |
| `SUPABASE_SERVICE_ROLE_KEY` | `eyJhbG...` service_role key | Dashboard → API → service_role (keep secret!) |
| `JWT_SECRET` | `openssl rand -base64 48` | Generate via `openssl rand -base64 48` |
| `POSTGRES_PASSWORD` | For self-hosted fallback, but for Supabase use Supabase password | Same as DB password |
| `BACKUP_ENCRYPTION_PASSPHRASE` | `openssl rand -base64 24` | Generate |
| `FLY_API_TOKEN` | `fly auth token` | For deploying API to Fly.io (see below) |
| `VERCEL_TOKEN` | `vercel token` | For deploying Web to Vercel |

## Step 3: Supabase CLI + GitHub Integration

### Install Supabase CLI Locally (for linking)

```bash
# macOS/Linux
npm install -g supabase
# Or via brew
brew install supabase/tap/supabase

# Login
supabase login

# Link your local project to Supabase project
supabase link --project-ref [ref] --password [db-pass]
# This creates supabase/config.toml

# Check linked
supabase status
```

### Supabase Migrations via CLI (Alternative to dotnet ef)

Supabase recommends using `supabase/migrations` folder for SQL migrations, not dotnet ef. But we have dotnet ef migrations (V19, V20). We can:

**Option A: Use dotnet ef for migrations (our current approach, works with Supabase)**
- Keep V19_Postgres_Migration.sql and V20_Postgres_Remaining_Migrations.sql in `src/LearnCloud.Auth/Migrations/`
- In GitHub Actions, run `dotnet ef database update` with direct connection string
- Simple, no need to convert to Supabase CLI format

**Option B: Use Supabase CLI migrations (Supabase native)**
- Convert our SQL migrations to `supabase/migrations/` folder with timestamp prefix
- Then `supabase db push` pushes to Supabase
- Better for Supabase Dashboard UI to see migrations

We will support both - GitHub Actions will use dotnet ef (Option A) for simplicity, but also generate Supabase CLI migrations folder.

```bash
# Generate Supabase migration folder from our V19+V20
mkdir -p supabase/migrations
cp src/LearnCloud.Auth/Migrations/V19_Postgres_Migration.sql supabase/migrations/$(date +%Y%m%d%H%M%S)_v19_postgres_initial.sql
cp src/LearnCloud.Auth/Migrations/V20_Postgres_Remaining_Migrations.sql supabase/migrations/$(date -d "+1 second" +%Y%m%d%H%M%S)_v20_remaining.sql

# Push via Supabase CLI
supabase db push
```

## Step 4: GitHub Actions Workflows (Already Created + New Deploy Workflow)

We already have `.github/workflows/security.yml` with:
- gitleaks secrets scan
- tenant-isolation-guards (C1 IgnoreQueryFilters, C2 FromSqlRaw, file storage)
- owasp-dependency-check
- security-headers
- api-security

**New file to create:** `.github/workflows/deploy-supabase.yml` - Deploys DB migrations to Supabase + API to Fly.io + Web to Vercel on push to main

See file `deploy-supabase.yml` below.

## Step 5: Deploy API to Fly.io via GitHub

Fly.io hosts .NET API Docker container, points to Supabase pooled DB.

```bash
# Install flyctl
curl -L https://fly.io/install.sh | sh

# Launch
fly launch --name learncloud-api --region sin --no-deploy

# Set secrets (from GitHub Secrets, but also set via flyctl)
fly secrets set ConnectionStrings__Default="postgres://postgres.[ref]:[pass]@aws-0-[region].pooler.supabase.com:6543/postgres?pgbouncer=true"
fly secrets set Jwt__Secret="your-jwt-secret-48-chars"
fly secrets set Supabase__Url="https://[ref].supabase.co"
fly secrets set Supabase__Key="anon-key"
fly secrets set Supabase__ServiceRoleKey="service-role-key"

# Deploy
fly deploy --dockerfile deployment/docker/Dockerfile.api
```

GitHub Actions will do this automatically on push to main if `FLY_API_TOKEN` secret set.

## Step 6: Deploy Web to Vercel via GitHub

Vercel auto-deploys on push if connected to GitHub repo, or via GitHub Actions.

```bash
# Install Vercel CLI
npm i -g vercel
vercel login
vercel --prod --cwd src/LearnCloud.Web
# Set env vars in Vercel Dashboard: VITE_API_URL=https://learncloud-api.fly.dev
```

GitHub Actions can also deploy to Vercel via `vercel --prod --token $VERCEL_TOKEN`

## Step 7: Supabase Storage Buckets Creation via GitHub Actions

Buckets `logos`, `payroll`, etc. with RLS policies should be created via SQL migration, not manual Dashboard, so they are versioned in git.

Add to V21 migration:

```sql
-- Create storage buckets via Supabase storage API? Actually buckets are created via Dashboard or via supabase SQL?
-- Supabase storage buckets are in storage.buckets table
INSERT INTO storage.buckets (id, name, public) VALUES ('logos', 'logos', true) ON CONFLICT (id) DO NOTHING;
INSERT INTO storage.buckets (id, name, public) VALUES ('payroll', 'payroll', false) ON CONFLICT (id) DO NOTHING;
-- RLS policies for storage.objects already in SUPABASE_DEPLOYMENT.md
```

## Full Flow Diagram

```
Developer pushes to GitHub main
       ↓
GitHub Actions triggers on push
       |
       +---> Job: security (gitleaks, tenant isolation guards, OWASP, headers, API auth)
       |      ↓
       |     If fails → block deployment, notify
       |
       +---> Job: build-and-test
       |      - dotnet restore, build LearnCloud.sln
       |      - dotnet test --filter MultiTenancy (tenant isolation suite)
       |      - npm install && npm run build in src/LearnCloud.Web (42 modules)
       |
       +---> Job: deploy-db-to-supabase (if build-and-test passes)
       |      - Uses SUPABASE_DB_DIRECT_URL secret
       |      - Runs: psql $SUPABASE_DB_DIRECT_URL -f V19_Postgres_Migration.sql
       |      - Runs: psql $SUPABASE_DB_DIRECT_URL -f V20_Postgres_Remaining_Migrations.sql
       |      - Or: dotnet ef database update --connection $SUPABASE_DB_DIRECT_URL
       |      - Then: Enable RLS and create policies for 39 tables (from SUPABASE_DEPLOYMENT.md)
       |      - Then: Create storage buckets and RLS policies
       |
       +---> Job: deploy-api-to-flyio
       |      - Uses FLY_API_TOKEN, ConnectionStrings__Default = SUPABASE_DB_POOL_URL
       |      - fly deploy --remote-only
       |
       +---> Job: deploy-web-to-vercel
              - Uses VERCEL_TOKEN
              - vercel --prod
```

## GitHub Branch Protection

Settings → Branches → Add rule for main:
- Require status checks to pass before merging: security, build-and-test
- Require pull request reviews
- No direct push to main, only via PR

## Local Development with Supabase

For local dev, you can still use self-hosted Postgres via docker-compose.yml (postgres:15-alpine) with .env file pointing to localhost, or use Supabase local dev via `supabase start` which starts local Supabase stack (Postgres + Auth + Storage + Realtime) in Docker.

```bash
# Option: Supabase local dev
supabase start
# This starts local Supabase at http://localhost:54321
# DB at postgresql://postgres:postgres@localhost:54322/postgres
# Then set ConnectionStrings__Default to that local URL for dev
```

## Cost

- GitHub Actions: Free tier 2000 minutes/month, our workflows ~5 min per push, okay for small team
- Supabase: Free 500MB DB, Pro $25/mo for 8GB
- Fly.io: Free tier 3 VMs, 160GB egress, Pro $29/mo
- Vercel: Free tier for Web, Pro $20/mo

Total: ~$50-80/mo for 50 schools prod

## Files in Repo for GitHub → Supabase

- `.github/workflows/security.yml` - Already exists, runs on push/PR
- `.github/workflows/deploy-supabase.yml` - NEW, deploys DB + API + Web on push to main
- `supabase/config.toml` - Created via `supabase link`, contains project ref
- `supabase/migrations/` - Contains V19, V20 translated to Supabase CLI format (timestamp prefix)
- `src/LearnCloud.Auth/Migrations/V19_Postgres_Migration.sql` + `V20_...` - Our EF Core migrations, also used by GitHub Actions via psql
- `deployment/docker-compose.supabase.yml` - Without postgres service, API points to Supabase
- `SUPABASE_DEPLOYMENT.md` - RLS policies for 39 tables + 10 buckets
- `POSTGRES_MIGRATION_GUIDE.md` - MySQL→Postgres translation
- `GITHUB_TO_SUPABASE_GUIDE.md` - This file

## Troubleshooting

- **Migration fails with "role does not exist"**: Use direct connection (5432) not pooled (6543) for DDL
- **RLS blocks queries**: Ensure JWT has `tid` claim, and policy uses `auth.jwt() ->> 'tid'`, not `auth.jwt() -> 'tid'`
- **Storage RLS blocks upload**: Ensure bucket RLS policy uses `(storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint` and folder structure is `{tenantId}/{fileName}`
- **API cannot connect to Supabase pooled**: Check connection string has `?pgbouncer=true` and uses pooled port 6543, not 5432
- **Fly.io deploy fails**: Check `FLY_API_TOKEN` secret set, and `fly.toml` has correct region

## Next

1. Push current code to GitHub repo
2. Set GitHub Secrets for Supabase
3. Create Supabase project and get connection strings
4. Run GitHub Actions workflow `deploy-supabase.yml` manually via `workflow_dispatch` to test
5. Check Supabase Dashboard → Database → Tables → 39 tables exist with RLS enabled (green check)
6. Check Storage → Buckets → 10 buckets exist with policies
7. Test tenant isolation: tenant A cannot read tenant B via API
8. Go live!
