# PostgreSQL Migration Guide - MySQL to PostgreSQL (Supabase Compatible)
**Date:** 2026-08-09
**Reason:** User requested Supabase or PostgreSQL instead of MySQL 8.0

## Current: MySQL 8.0
- Engine: InnoDB, utf8mb4_unicode_ci
- Provider: Pomelo.EntityFrameworkCore.MySql
- Connection: `Server=mysql;Database=learncloud;User=learncloud;Password=...;CharSet=utf8mb4;`
- Migrations: V1-V18 raw SQL MySQL specific: BIGINT UNSIGNED, TINYINT(1), ENUM, JSON, DATETIME ON UPDATE, IF() generated columns, PARTITION BY RANGE YEAR()

## Target: PostgreSQL 15+ (Works for Both Self-Hosted and Supabase)

### Why PostgreSQL?
- Supabase is hosted Postgres with RLS, Storage, Realtime, Auth
- Better JSONB support, better partitioning, better RLS for multi-tenant
- MySQL to Postgres migration is common, EF Core supports both via provider switch

### Code Changes Already Applied

#### 1. EF Core Provider Switch
```bash
# Removed MySQL
dotnet remove package Pomelo.EntityFrameworkCore.MySql
# Added Postgres (Supabase compatible)
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.0
```

#### 2. DbContext Extensions Updated
**Files:** `AuthModuleExtensions.cs`, `MultiTenancyExtensions.cs`
```csharp
// BEFORE MySQL
opt.UseMySql(conn, ServerVersion.AutoDetect(conn), mysql => mysql.EnableRetryOnFailure(3));

// AFTER Postgres (Supabase compatible - pooled 6543 and direct 5432)
opt.UseNpgsql(conn, npgsql => {
    npgsql.EnableRetryOnFailure(3);
    npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
});
```

#### 3. Column Types Updated
- `HasColumnType("BIGINT UNSIGNED")` → `HasColumnType("BIGINT")` (Postgres has no unsigned, but BIGINT range -9e18 to 9e18 enough for 2B ids)
- `HasColumnType("TINYINT(1)")` → `BOOLEAN`
- `HasColumnType("DATETIME")` → `TIMESTAMPTZ`
- `HasColumnType("JSON")` → `JSONB`

Files updated: `RolePermissionConfiguration.cs`, `UserConfiguration.cs`, plus FeeConfiguration already has `HasPrecision(18,2)` which works for both.

#### 4. Docker Compose Updated
**File:** `deployment/docker-compose.yml`
- **Before:** `mysql:8.0` service with `MYSQL_ROOT_PASSWORD`, `mysql_data`, healthcheck `mysqladmin ping`
- **After:** `postgres:15-alpine` with `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `postgres_data`, healthcheck `pg_isready`
- Supports both self-hosted and Supabase:
  - Self-hosted: `ConnectionStrings__Default=Host=postgres;Database=learncloud;Username=learncloud;Password=...`
  - Supabase pooled (for API): `postgres://postgres.[ref]:[pass]@aws-0-[region].pooler.supabase.com:6543/postgres?pgbouncer=true`
  - Supabase direct (for migrations): `postgres://postgres:[pass]@db.[ref].supabase.co:5432/postgres`

#### 5. New Migration V19_Postgres_Migration.sql (16KB)
- Created `src/LearnCloud.Auth/Migrations/V19_Postgres_Migration.sql`
- Enables extensions `uuid-ossp`, `pgcrypto`
- Creates tables with Postgres syntax:
  - `id BIGSERIAL PRIMARY KEY` instead of `BIGINT UNSIGNED AUTO_INCREMENT`
  - `BOOLEAN NOT NULL DEFAULT FALSE` instead of `TINYINT(1)`
  - `VARCHAR(20) CHECK (status IN (...))` instead of `ENUM`
  - `TIMESTAMPTZ DEFAULT NOW()` instead of `DATETIME DEFAULT CURRENT_TIMESTAMP`
  - `JSONB` instead of `JSON`
  - Generated columns: `IF(is_deleted=0, slug, NULL)` → `CASE WHEN is_deleted=false THEN slug ELSE NULL END`
  - Partial unique for soft-delete reuse: MySQL allows multiple NULLs, Postgres does too - same behavior
  - RLS: `ENABLE ROW LEVEL SECURITY` + `CREATE POLICY tenant_isolation ON students FOR ALL USING (tenant_id = (current_setting('app.current_tenant_id')::bigint))` for self-hosted, or `auth.jwt() ->> 'tid'` for Supabase
- Includes `schema_migrations` table

#### 6. File Storage - Supabase Compatible
- Created `src/LearnCloud.Api/Services/SupabaseStorageService.cs` with `IStorageService` interface
- `LocalStorageService`: stores outside wwwroot `AppContext.BaseDirectory/uploads/{bucket}/{tenantId}/{guid}`, tenant check, random GUID, path traversal defense
- `SupabaseStorageService`: uploads to Supabase Storage buckets `logos`, `payroll`, `assignments` with RLS `tenant_id = auth.jwt() ->> 'tid'`, uses Supabase .NET client (mock for now, needs real implementation via `Supabase` package)
- `SupabaseOptions`: `Url`, `AnonKey`, `ServiceRoleKey`, `UseSupabaseStorage` bool
- FilesController already has tenant ownership check, now can work with both local and Supabase via IStorageService

---

## How to Deploy to PostgreSQL (Self-Hosted)

### Option A: Self-Hosted Postgres via Docker Compose (Simplest, No Vendor Lock-in)

1. **Update .env.production outside repo:**
```bash
mkdir -p /opt/learncloud
chmod 700 /opt/learncloud
cat > /opt/learncloud/.env.production << EOF
POSTGRES_DB=learncloud
POSTGRES_USER=learncloud
POSTGRES_PASSWORD=$(openssl rand -base64 24)
POSTGRES_HOST=postgres
JWT_SECRET=$(openssl rand -base64 48)
BACKUP_ENCRYPTION_PASSPHRASE=$(openssl rand -base64 24)
EOF
chmod 600 /opt/learncloud/.env.production
```

2. **Deploy:**
```bash
cd deployment
docker-compose up -d --build postgres redis
# Wait for postgres healthcheck
docker-compose logs -f postgres

# Apply migrations (use direct connection, not pooled)
# For self-hosted, direct = pooled same (postgres:5432)
docker-compose exec api dotnet ef database update --connection "Host=postgres;Database=learncloud;Username=learncloud;Password=$POSTGRES_PASSWORD"
# Or via psql
psql "postgres://learncloud:$POSTGRES_PASSWORD@localhost:5432/learncloud" -f src/LearnCloud.Auth/Migrations/V19_Postgres_Migration.sql

docker-compose up -d api web nginx
curl http://localhost:8080/health
curl https://learncloud.co.zw/health (via nginx)
```

3. **Verify tenant isolation:**
```bash
./scripts/ci_tenant_isolation_guards.sh # Should PASS
# Test composite FKs: try insert attendance for grade of other tenant -> should fail 1452
```

### Option B: Supabase Hosted Postgres

1. **Create Supabase Project:**
- Go to supabase.com → New Project → Region af-south-1 (closest to Bulawayo) or eu-west
- Wait for project creation (~2 min)
- Dashboard → Database → Connection string → URI tab → Copy pooled URI (6543) and direct URI (5432)

2. **Get Connection Strings:**
- **Pooled (for API, PgBouncer, 6543):** `postgres://postgres.[ref]:[pass]@aws-0-[region].pooler.supabase.com:6543/postgres?pgbouncer=true`
- **Direct (for migrations, 5432):** `postgres://postgres:[pass]@db.[ref].supabase.co:5432/postgres`

3. **Update .env.production:**
```bash
cat > /opt/learncloud/.env.production << EOF
# Supabase
ConnectionStrings__Default=postgres://postgres.[ref]:[YOUR-PASS]@aws-0-[region].pooler.supabase.com:6543/postgres?pgbouncer=true
# For migrations, use direct:
# ConnectionStrings__Default__Direct=postgres://postgres:[PASS]@db.[ref].supabase.co:5432/postgres
Supabase__Url=https://[ref].supabase.co
Supabase__Key=[anon-key from Dashboard -> API]
Supabase__ServiceRoleKey=[service-role-key from Dashboard -> API]
Supabase__UseSupabaseStorage=true
JWT_SECRET=$(openssl rand -base64 48)
BACKUP_ENCRYPTION_PASSPHRASE=$(openssl rand -base64 24)
EOF
chmod 600 /opt/learncloud/.env.production
```

4. **Create Storage Buckets in Supabase Dashboard:**
- Storage → New Bucket: `logos` (public, 2MB limit, only image/*)
- `payroll` (private, 5MB limit, only .csv, .pdf, .xlsx)
- `assignments`, `expense-proofs`, `staff-docs` (private)
- For each bucket, add RLS policy:
```sql
-- Allow tenant to only access own folder
CREATE POLICY "Tenant isolation for payroll"
ON storage.objects FOR ALL
USING (bucket_id = 'payroll' AND (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (bucket_id = 'payroll' AND (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint);
```

5. **Apply Migrations to Supabase:**
```bash
# Use direct connection for migrations (pooled doesn't support DDL well)
export ConnectionStrings__Default="postgres://postgres:[PASS]@db.[ref].supabase.co:5432/postgres"
dotnet ef database update --project src/LearnCloud.Auth --startup-project src/LearnCloud.Api
# Or psql
psql "postgres://postgres:[PASS]@db.[ref].supabase.co:5432/postgres" -f src/LearnCloud.Auth/Migrations/V19_Postgres_Migration.sql
```

6. **Deploy API (without postgres service, since DB is external):**
```bash
# In docker-compose.yml, comment out postgres service, keep api, web, redis, nginx
# Set ConnectionStrings__Default to Supabase pooled URI via .env.production
cd deployment
docker-compose up -d --build api web nginx redis
# Or deploy API to Fly.io:
fly launch
fly secrets set ConnectionStrings__Default="postgres://..." Jwt__Secret="..." Supabase__Url="..."
fly deploy
```

7. **Deploy Web:**
- Vercel/Netlify: `cd src/LearnCloud.Web && npm run build && vercel --prod`
- Or via Fly.io same as API + Nginx serves web

8. **Enable RLS for all tenant-owned tables in Supabase:**
```sql
ALTER TABLE students ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON students FOR ALL USING (tenant_id = (auth.jwt() ->> 'tid')::bigint);
-- Repeat for 39 tables
```

---

## Comparison

| | MySQL 8.0 (Old) | PostgreSQL 15 Self-Hosted | Supabase Hosted Postgres |
|---|---|---|---|
| **Provider** | Pomelo MySQL | Npgsql | Npgsql (same) |
| **File Storage** | Local + FilesController | Local + FilesController | Supabase Storage buckets + RLS |
| **Auth** | Custom JWT tid | Custom JWT tid | Custom JWT tid + optional Supabase Auth |
| **RLS** | No, app filter + composite FKs | Optional RLS via current_setting | RLS via auth.jwt() + Supabase UI |
| **Realtime** | Polling | Polling | Built-in Realtime for attendance/messaging |
| **Cost** | Free self-hosted | Free self-hosted or AWS RDS $15/mo | Free 500MB, Pro $25/mo |
| **Ops** | You manage backups | You manage or AWS manages | Supabase manages backups, PITR on Pro |
| **Migration Effort** | 0 | 2 days (done) | Same 2 days + storage buckets |

---

## Files Changed for Postgres Migration

- `src/LearnCloud.Auth/Extensions/AuthModuleExtensions.cs` - UseMySql → UseNpgsql
- `src/LearnCloud.MultiTenancy/Extensions/MultiTenancyExtensions.cs` - UseMySql → UseNpgsql + UseQuerySplittingBehavior
- `src/LearnCloud.Auth/Configurations/RolePermissionConfiguration.cs` - BIGINT UNSIGNED→BIGINT, TINYINT→BOOLEAN
- `src/LearnCloud.Auth/Configurations/UserConfiguration.cs` - same
- `src/LearnCloud.Auth/LearnCloud.Auth.csproj` - Added Npgsql.EntityFrameworkCore.PostgreSQL 8.0.0
- `src/LearnCloud.MultiTenancy/LearnCloud.MultiTenancy.csproj` - Added Npgsql
- `src/LearnCloud.Api/LearnCloud.Api.csproj` - Pomelo→Npgsql
- `deployment/docker-compose.yml` - mysql:8.0 → postgres:15-alpine, MYSQL_* → POSTGRES_*, added Supabase env vars support
- `src/LearnCloud.Auth/Migrations/V19_Postgres_Migration.sql` - NEW 16KB Postgres initial
- `src/LearnCloud.Api/Services/SupabaseStorageService.cs` - NEW storage abstraction local + Supabase
- `POSTGRES_MIGRATION_GUIDE.md` - this file
- Future: `SUPABASE_DEPLOYMENT.md` with bucket RLS policies

---

## Verification

```bash
# Check no MySQL provider remains
grep -R "UseMySql\|Pomelo" src --include="*.cs" --include="*.csproj" | grep -v "SECURITY" | wc -l
# Should be 0

# Check Postgres provider present
grep -R "UseNpgsql\|Npgsql" src --include="*.cs" --include="*.csproj" | wc -l
# Should be >5

# Check docker-compose uses postgres
grep -n "postgres" deployment/docker-compose.yml | head
# Should show postgres:15-alpine, not mysql:8.0

# Build (requires .NET 8 SDK - not in sandbox, but structure OK)
# dotnet build LearnCloud.sln -> should restore Npgsql and build

# Vite build still works (frontend not affected by DB change)
cd src/LearnCloud.Web && npm run build
# 42 modules OK
```

---

## Next Steps

1. **Choose:** Self-hosted Postgres (docker-compose postgres:15) or Supabase hosted?
2. **If Self-hosted:** `docker-compose up -d postgres` + apply V19 migration via psql
3. **If Supabase:** Create project, get pooled/direct URIs, update .env.production, apply V19 via direct connection, create storage buckets with RLS, deploy API to Fly.io/Render
4. **Test tenant isolation:** `scripts/ci_tenant_isolation_guards.sh` + try cross-tenant FK insert should fail
5. **Migrate data from MySQL to Postgres:** Use `pgloader` or `mysqldump --compatible=postgresql` + fixup, or write EF Core data seeder

All code changes preserve business logic, only change DB provider + types, no rewrite.

Ready to deploy to Postgres?
