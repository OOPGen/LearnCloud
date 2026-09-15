# Local development

## Prerequisites

- .NET SDK 8 or later
- Docker Desktop, running

## First run

From the repository root, in PowerShell:

```powershell
./scripts/dev-setup.ps1
dotnet run --project src/LearnCloud.Api
```

The setup script is safe to re-run. It:

1. Generates a database password and a JWT signing key once, and stores them with
   `dotnet user-secrets` for `src/LearnCloud.Api`. No secret is written into the repo.
2. Starts PostgreSQL 16 in Docker as `learncloud-dev-db`, on `127.0.0.1:55432`.
   Port 55432 avoids clashing with other local PostgreSQL servers on 5432.
3. Applies the EF Core migrations.

Then open:

| What | URL |
|---|---|
| Swagger | http://localhost:5080/swagger |
| Liveness | http://localhost:5080/health |
| Readiness (database) | http://localhost:5080/health/ready |

## Trying the API

1. `POST /api/auth/register-tenant` to create a school and its admin.
2. `POST /api/auth/login` with that email, password and the school slug.
3. Use the returned `accessToken` as a Bearer token.
4. `POST /api/academic/subjects`, then list, get, update and delete.
5. School records: add an academic year and its terms, a grade and a stream, then a student.
   The rules and endpoints are in [SCHOOL_RECORDS.md](SCHOOL_RECORDS.md).

Locally there are no subdomains. Send `X-Tenant-Slug: <slug>` to act as if the request
came from `<slug>.learncloud.co.zw`. A token for one school used with another school's
slug is rejected with 403.

## Schema changes

The EF Core model is the source of truth. After changing entities:

```powershell
dotnet ef migrations add <Name> --project src/LearnCloud.Api --startup-project src/LearnCloud.Api --output-dir Data/Migrations
dotnet run --project src/LearnCloud.Api -- --migrate
```

`dotnet ef migrations has-pending-model-changes` tells you whether a migration is needed.
The old hand-written SQL scripts are archived in `docs/archive/sql-migrations-2026-08`
and must not be run.

## Web app

```powershell
cd src/LearnCloud.Web
npm ci
npm run dev
```

Open http://localhost:5173. Vite proxies `/api` to the API on http://localhost:5080, so run
the API alongside it. On localhost the sign-in page asks for the school code; register a
school first or use one created by the smoke test.

## Tests

```powershell
dotnet test tests/LearnCloud.UnitTests          # fast, no Docker
dotnet test tests/LearnCloud.IntegrationTests   # needs Docker
node scripts/smoke-test.mjs http://localhost:5080 --full
```

The integration tests start their own disposable PostgreSQL container and do not touch the
development database. The smoke test runs against a running API and registers two
throwaway schools with `--full`.

## Configuration reference

| Key | Where it comes from locally | Notes |
|---|---|---|
| `ConnectionStrings:Default` | user-secrets | Required. |
| `Jwt:Secret` | user-secrets | Required, 32+ characters, rejected if it looks like a demo value. |
| `Jobs:Enabled` | `appsettings.Development.json` (false) | Scheduled dunning and communication-rule jobs. |
| `RateLimiting:ApiGeneralPerMinute` | default 60 | Requests per minute per signed-in user for most endpoints. The integration tests raise it. |
| `AI:OpenAI:ApiKey` | optional | Without it, AI features use the rule-based provider. |
| `Messaging:Sms`, `Messaging:Email` | optional | Needed only to actually send messages. |

In production every value comes from environment variables, for example
`ConnectionStrings__Default` and `Jwt__Secret`.

## Resetting the database

```powershell
$env:LEARNCLOUD_DEV_DB_PASSWORD = 'unused'
docker compose -f deployment/docker-compose.dev.yml down -v
./scripts/dev-setup.ps1
```

`down -v` deletes the data volume; the setup script recreates and migrates it.
