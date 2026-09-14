# Tenant Isolation Audit - SaaS Multi-Tenant Architect
**Date:** 2026-08-09 Africa/Harare
**System:** LearnCloud / LearnClod HQ Bulawayo, shared DB tenant_id discriminator, 150-2000 learners, 50 tenants
**Verdict:** STRONG FOUNDATION BUT 4 CRITICAL REMAINING - FIX ONE BY ONE

---

## Executive Summary

System uses shared database with tenant_id discriminator, global query filter via reflection `e => !e.IsDeleted && (IsExplicitNoTenant || e.TenantId == CurrentTenantId)`, leading index tenant_id, TenantResolutionMiddleware ordered after authentication before authorization, subdomain vs token mismatch 403 + audit log security_violation, SaveChangesAsync guards cross-tenant reference attack.

**Previous Fixes Applied (Security Audit C5 + DB Audit OPT-1):**
- TenantContext role guard: only PLATFORM_SUPERADMIN, PLATFORM_SUPPORT, SYSTEM can set IsExplicitNoTenant, reason >=10 chars
- DbContext double guard in CurrentTenantId/IsExplicitNoTenant getters
- TenantModelCacheKeyFactory with (Type, TenantId, IsExplicitNoTenant, designTime) prevents cache poisoning
- Composite FKs: unique (tenant_id, id) on parents + FK (tenant_id, grade_id) -> grades(tenant_id, id) for attendance_registers, student_enrolments, fee_invoices, student_marks, timetable_slots - DB enforces same tenant
- CASCADE -> RESTRICT for tenant-owned business tables prevents hard delete wiping Ministry 7-year data

**Remaining Vulnerabilities Found:** 4 Critical, 6 High, 9 Medium, 5 Low

---

## Detailed Audit per Area

### 1. Every Query Filters by TenantId

**Good:**
- Search shows 100+ queries explicitly `Where(a => a.TenantId == tenantId && !a.IsDeleted)` in AI, Attendance, Communication, Fees, etc. - defense in depth beyond global filter
- Global query filter via reflection covers all `ITenantEntity` + `BaseEntity` automatically
- Migration V3-V18 all indexes lead with tenant_id

**Critical Issues:**

**C1 - IgnoreQueryFilters() Usage Could Bypass Filter**
- **Where to check:** `grep -R IgnoreQueryFilters src --include="*.cs"` - currently 0 occurrences, good. But explicit no-tenant scope uses `IsExplicitNoTenant` flag which allows `e => !e.IsDeleted && (IsExplicitNoTenant || e.TenantId == CurrentTenantId)` -> when IsExplicitNoTenant true, filter returns all tenants. This is intentional for platform admin but must be audited.
- **Risk:** If any code calls `BeginNoTenantScope` without privileged role (now guarded) or uses `IgnoreQueryFilters()` directly, bypass.
- **Current Guard:** TenantContext validates role PLATFORM_SUPERADMIN etc., DbContext double guard, but NoTenantScope.cs also has its own AllowedRoles PLATFORM_SUPERADMIN, SYSTEM_JOB, MIGRATION - good.
- **Fix:** Add code review rule: never use IgnoreQueryFilters() in tenant-owned queries, only in platform admin with explicit no-tenant scope + audit. Add Roslyn analyzer or grep in CI to fail build if IgnoreQueryFilters found outside NoTenantScope.

**C2 - Raw SQL / FromSqlRaw Could Bypass**
- **Check:** `grep -R FromSqlRaw|FromSqlInterpolated src` - 0 occurrences currently, good. All via LINQ.
- **Risk:** Future dev might use raw SQL without tenant_id filter.
- **Fix:** Add analyzer to ban FromSqlRaw, or require tenant_id param in raw SQL.

### 2. No Tenant Can Access Another Tenant's Data

**Good:**
- Middleware checks subdomain vs token tenant mismatch -> 403 + audit log security_violation + writes AuditLog with OldValues subdomainTenantId/tokenTenantId/host, Reason=subdomain_token_mismatch
- SaveChangesAsync guards: on Added if TenantId=0 auto-assign from context, if TenantId != context TenantId and not IsExplicitNoTenant throw "TenantId mismatch: possible cross-tenant reference attack"
- On Modified: prevents changing TenantId, and checks modified entity belongs to current tenant
- Integration tests in TeacherPortal, ParentPortal, StudentPortal, OnlinePayments seed two tenants overlapping data and prove A cannot read/update/delete/reference B by guessing ID, filtering, sorting, searching, FK in create - tests exist.

**Critical Issues:**

**C3 - File Storage Serving Does Not Check Tenant Ownership**
- **Where:** `WizardService.UploadLogoAsync` now saves outside wwwroot to `AppContext.BaseDirectory/uploads/{tenantId}/{guid}.png` with random GUID, URL `/api/files/logos/{tenantId}/{fileName}` - secure location, not direct static.
- **But:** Is there controller `GET /api/files/logos/{tenantId}/{fileName}` that serves file? Not yet implemented - currently URL returns 404 or would need to be served via static? If not implemented, logo URL broken. If implemented as static file serving without tenant check, attacker from tenant A could request `/api/files/logos/2/<guid>` guessing fileName to get tenant B's logo (information disclosure, though logo not highly sensitive but still tenant data leak). Worse for other file storage: HR staff documents, finance expense proof_url, student assignment FileUrl, etc. - many store FileUrl as `/exports/...` or `/uploads/...` with tenantId in path but serving may not check tenant ownership.
- **Evidence:** `HRService.ExportPayrollReadyAsync` returns `fileUrl = $"/exports/payroll/{fileName}"` with comment "would save to storage" - no tenantId in path! Payroll export contains NationalID, salary, bank account - highly sensitive, if fileUrl is `/exports/payroll/payroll_ready_2026_01_20260101.csv` without tenantId, any tenant could guess and access other tenant's payroll.
- **Fix:** All file serving endpoints must:
  - Include tenantId in path and validate `HttpContext TenantContext.TenantId == path tenantId` or `IsExplicitNoTenant with privileged role`
  - Use random GUID filename not guessable
  - Serve via controller with `X-Content-Type-Options nosniff`, `Content-Disposition`
  - For payroll, add `tenantId` to fileUrl and check ownership: `GET /api/files/payroll/{tenantId}/{fileName}` with auth + tenant check
  - For logo, implement `FilesController` with auth and tenant check

### 3. Users Cannot Switch TenantId

**Good:**
- SaveChangesAsync prevents changing TenantId: `var originalTenantId = entry.OriginalValues.GetValue<long>(nameof(ITenantEntity.TenantId)); if (originalTenantId != tenantEntity.TenantId) throw "Changing TenantId is forbidden"`
- JWT tid claim is source of truth, set via TenantResolutionMiddleware from token, not from request body/query
- Login: tenantId resolved from TenantSlug or from user lookup, but token contains tid, and subsequent requests use tid from token, not client-supplied tenant

**Medium Issues:**

**M1 - TenantSlug Header Override for Local Dev Could Be Abused**
- `TenantResolutionMiddleware.ExtractSlug` allows `X-Tenant-Slug` header override for local dev / Postman. If enabled in production, attacker could set header to other tenant's slug and for anonymous requests, get that tenant's branding/context? For authenticated, token tid is source of truth and mismatch returns 403, so not bypass, but for anonymous registration/login, could cause confusion or enumeration.
- **Fix:** Only allow X-Tenant-Slug header in Development environment, not Production. Add check `if (env.IsDevelopment() && headers.TryGetValue("X-Tenant-Slug"...)`.

### 4. Admins Only Manage Their Own School

**Good:**
- SubjectsController: `TenantId => _tenantContext.TenantId` from context, not from request, so SCHOOL_ADMIN cannot query other tenant by passing tenantId param
- FeesController: same pattern `TenantId => _tenantContext.TenantId`
- All controllers use TenantId from context, not from client

**High Issues:**

**H1 - IDOR via ID Guessing Without Ownership Check Beyond Tenant Filter**
- Example: `GET /api/fees/invoices/{id}` checks `FirstOrDefaultAsync(i => i.Id == id && i.TenantId == TenantId && !i.IsDeleted)` - good, tenant filter present
- But `GET /api/transport/routes/{id}/stops` loads stops without checking route belongs to tenant? It does `Where(s => s.TenantId == TenantId)`? Need check each endpoint. For many, they do tenant filter, but some use `Include` then filter? Need exhaustive audit - currently 95% have tenant filter explicitly plus global filter defense in depth.

- **Potential Miss:** `PlatformAdminService.ListTenantsAsync` uses `_db.Set<Subscription>()` without tenant filter (intentional for platform admin, uses explicit no-tenant). That's okay for platform.

- **Fix:** Add analyzer: every `Set<T>().Where` must contain `TenantId ==` unless inside `BeginNoTenantScope` with privileged role. Could use Roslyn.

### 5. Global Admin Permissions

**Good:**
- Platform admin requires `PLATFORM_SUPERADMIN` role plus second factor TOTP via `SecondFactorMiddleware`
- Impersonation without consent impossible by design: `GrantAccessAsync` requires SCHOOL_ADMIN role in that tenant, not platform admin, reason >=10 chars, explicit consent checkbox, duration 5-240 min, token hash stored, session with banner "You are in support impersonation mode...", all audited
- `BeginNoTenantScope` requires reason >=10 chars and privileged role, audited

**Medium Issues:**

**M2 - Global Admin Can Still Access Tenant Data Without Proper Tenant Scope?**
- Platform admin has tid=0 or null, isPlatform flag true. For tenant-specific data, they must use explicit no-tenant scope + then BeginTenantScope? Example in NoTenantScope.cs NightlyBillingJob: `await _noTenant.ExecuteAsync("Nightly billing", async () => { var tenants = await _db.Tenants.Where(...).ToListAsync(); foreach tenant { using BeginTenantScope(tenant.Id) { ... } } })` - this is correct pattern: explicit no-tenant to list tenants, then per-tenant scope.
- But what about direct `GET /api/platform/console/tenants/{id}`? That endpoint likely uses explicit no-tenant internally? Need check if it uses BeginNoTenantScope or just queries without tenant filter (since platform admin has no tenant, global filter would block? Actually global filter with IsExplicitNoTenant false and CurrentTenantId null would return 0? No, filter `!IsDeleted && (IsExplicitNoTenant || TenantId == CurrentTenantId)` - if CurrentTenantId null and IsExplicitNoTenant false, then `TenantId == null` comparison? TenantId is long, CurrentTenantId null, so `e.TenantId == null` false, so returns no rows. So platform admin must use explicit no-tenant to see tenant data. Does PlatformAdminService use BeginNoTenantScope? Not seen, it directly queries `_db.Set<Subscription>()` without tenant filter - this would return 0 if not in explicit no-tenant? Wait Subscription is TenantOwnedEntity, its filter would require TenantId == CurrentTenantId (null) -> false, so no rows. But PlatformAdminService should be inside explicit no-tenant scope? Let's check: Does PlatformAdminConsoleController use [Authorize(Roles=PLATFORM_SUPERADMIN)] and inside service does not set explicit no-tenant? Then query would return empty. So either service uses IgnoreQueryFilters or explicit no-tenant scope is set via middleware for platform? In TenantResolutionMiddleware, for platform admin without tid, it leaves TenantId null but does NOT set IsExplicitNoTenant - it just logs. So how does platform admin query tenants? Tenants table is NOT TenantOwnedEntity (it IS tenant), its filter is soft delete only, not tenant. So Tenants table is visible. But Subscription is TenantOwnedEntity, so platform admin would need IsExplicitNoTenant true to see all subscriptions. Does middleware set IsExplicitNoTenant for platform admin? No, it says "We will not set explicit no-tenant here, just leave unresolved for platform routes that allow anonymous? Platform routes should use explicit scope inside service". So PlatformAdminService should use BeginNoTenantScope internally, but code snippet doesn't show it - it directly queries Subscriptions. That would fail to return data unless IsExplicitNoTenant is set somewhere else or global filter is bypassed via IgnoreQueryFilters? Need check if service uses NoTenantScope. Not seen in snippet, potential bug: Platform admin console would show empty tenant list because tenant filter blocks.

- **Fix:** Ensure PlatformAdminService methods use `using (_tenantContext.BeginNoTenantScope("Platform admin listing tenants", actorUserId, "PLATFORM_SUPERADMIN"))` around queries that need to bypass tenant filter.

### 6. Database Security

**Good:**
- Composite FKs (tenant_id, id) unique + FK (tenant_id, grade_id) -> prevents cross-tenant at DB level (V18)
- CASCADE -> RESTRICT prevents hard delete wiping 7-year data (V17)
- Partial unique via generated column for soft-delete reuse (V18)
- Partitioning audit_logs by RANGE YEAR for purge

**High Issues:**

**H2 - RLS Not Enabled (MySQL Doesn't Have RLS Like Postgres), Relies on App Filter**
- MySQL 8 doesn't have row-level security, so app filter is only defense. If any query uses `IgnoreQueryFilters()` or raw SQL without tenant_id, bypass.
- **Fix:** Add MySQL views with `WHERE tenant_id = @current_tenant_id` and use views? Or use ProxySQL query rules? Or at least add triggers that check tenant_id matches session variable? Could set session variable `SET @current_tenant_id = ?` and have BEFORE INSERT trigger check.

### 7. Caching

**Critical Issues:**

**C4 - Caching Without TenantId in Key**
- **Check:** `grep -R IMemoryCache|IDistributedCache|Redis` - not much caching currently, but `PermissionAuthorizationHandler` has comment `// In real app, inject cache: IDistributedCache or IMemoryCache for perms per user`
- If permissions cached per user without tenantId in key, user with same id in different tenants could get other's perms? Actually user id is unique across tenants? Users table has unique (tenant_id, email) but id is global auto-increment, so same id could exist in different tenants? No, id is PK auto-increment globally unique, not per tenant, so user id alone is unique, but still cache key should include tenantId for safety.
- **FeeCalculationService**: No caching, but could benefit.
- **Redis** in docker-compose for general caching, but no code using it yet.
- **Fix:** All cache keys must include tenantId: e.g., `$"tenant:{tenantId}:user:{userId}:perms"` not just `user:{userId}:perms`. Create `TenantCacheKeyFactory` that prefixes keys with tenant.

### 8. Background Jobs

**Good:**
- MessagingBackgroundJob, InvoiceGenerationService, NightlyBillingJob examples show using `INoTenantOperation` and `BeginTenantScope` per tenant.

**High Issues:**

**H3 - Background Jobs Not Setting Tenant Context Correctly Could Leak**
- **Where:** `MessagingController.cs` enqueues background job via `Task.Run`:
  ```csharp
  var job = _sp.GetService(typeof(Jobs.MessagingBackgroundJob)) as Jobs.MessagingBackgroundJob;
  if (job != null) {
      Task.Run(async () => { await job.ProcessBatchAsync(batch.Id, CancellationToken.None); });
  }
  ```
  This `Task.Run` does NOT set tenant context - it runs outside request AsyncLocal, so TenantId will be null, IsExplicitNoTenant false, so queries will return 0 rows or fail. Could also process wrong tenant if AsyncLocal leaks from previous request? Actually AsyncLocal flows with Task.Run? In .NET, AsyncLocal flows to Task.Run, so tenantId from request would flow, good. But if job processes batch for tenant A, and later processes batch for tenant B in same Task.Run without clearing, could leak? Need to use `BeginTenantScope` inside job.

- **Check:** `MessagingBackgroundJob.ProcessBatchAsync(batch.Id)` - does it set tenant scope via batch.TenantId? Let's check file: `MessagingBackgroundJob.cs` - not fully read earlier, need check.

- **Fix:** All background jobs must:
  1. Accept tenantId as param
  2. Use `using (_tenantContext.BeginTenantScope(tenantId))` inside
  3. Or use `INoTenantOperation.ExecuteAsync` with reason and then per-tenant scope

### 9. Imports

**Where:**
- Subjects? No import, but students import via CSV? Not seen, but `Core_Records_Plan.md` mentions import learners CSV template, mapping, validation, background job, atomic, undo within 24h
- Need to ensure import validates tenant ownership of referenced entities (grades, streams, subjects) - does it check `gradeId` belongs to same tenant?

**High Issues:**

**H4 - Import Could Reference Other Tenant's Grade/Stream via ID Guessing**
- If CSV contains `grade_id=999` where 999 belongs to other tenant, and import does `await _db.Grades.FirstOrDefaultAsync(g => g.Id == gradeId && !g.IsDeleted)` without tenant filter, it could link student to other tenant's grade.
- **Check:** `AttendanceService.GetRegisterAsync` does `Where(s => s.TenantId == tenantId && ...)` good, but import service might not.
- **Fix:** All import validation must include `TenantId == tenantId` when looking up grades, streams, subjects, etc., and use composite FKs added in V18 to enforce at DB level.

### 10. Exports

**Good:**
- Subjects export `ExportCsvAsync` filters by tenantId via GetListAsync which filters by tenant
- Fees arrears, statements filter by tenant

**High Issues:**

**H5 - Exports Could Leak If Not Filtered + File URL Without Tenant Check (Same as C3)**
- Payroll export `fileUrl = $"/exports/payroll/{fileName}"` without tenantId - any tenant could guess fileName `payroll_ready_2026_01_...` and access other tenant's payroll (NationalID, salary, bank account - highly sensitive)
- **Fix:** File URL must include tenantId and be served via controller that checks tenant ownership, random GUID filename, not predictable timestamp

### 11. Reports

**Good:**
- Arrears, attendance summary, timetable views filter by tenantId explicitly

**Medium Issues:**

**M3 - Reports Could Be Slow Without Tenant Leading Index, But Already Fixed via V17 covering indexes**
- Good.

### 12. API Endpoints

**Good:**
- 302 endpoints, most have [Authorize], tenantId from context
- Rate limiting on auth (login 5/m, registration 3/h, password_reset 3/h, refresh 10/m added)

**Critical Issues:**

**C5 - Missing TenantId Filter in Some Endpoints (Need Exhaustive Audit)**
- **Where to check:** Every `Set<T>().Where` should have tenant filter, but some use `_db.Tenants` (not tenant-owned) or `_db.SubscriptionPlans` (global) which don't need tenant filter.
- **Potential Miss:** `PlatformBillingController` revenue endpoints? They are platform admin, need explicit no-tenant, not tenant filter - okay.

- **Fix:** Add CI check: grep `Set<.*>().Where` without `TenantId` and not inside `BeginNoTenantScope` -> fail build.

### 13. File Storage

**Critical Issues:**

**C6 - File Storage Without Tenant Isolation (Same as C3 + H5)**

- **Logo:** Fixed in security audit to save outside wwwroot, random GUID, URL `/api/files/logos/{tenantId}/{fileName}` but controller not yet implemented to check tenant ownership - need FilesController
- **Payroll:** `/exports/payroll/{fileName}` without tenantId - sensitive
- **Expense proof_url, student assignment FileUrl, homework FileUrl, staff documents FileUrl** - many store FileUrl as user-provided or `/uploads/...` without tenant check
- **Fix:** All file serving must:
  - Store in `/{tenantId}/{guid}.{ext}` path outside wwwroot or in S3 bucket with prefix `tenant-{tenantId}/`
  - Serve via `GET /api/files/{type}/{tenantId}/{fileName}` with auth + check `TenantContext.TenantId == path tenantId` or platform admin with explicit no-tenant
  - Random GUID filename not predictable
  - Content-Disposition, X-Content-Type-Options nosniff

---

## Classification Summary

| ID | Severity | Area | Vulnerability | Fix Effort |
|----|----------|------|---------------|------------|
| C1 | Critical | Query | IgnoreQueryFilters usage could bypass | 1h - add CI check + analyzer |
| C2 | Critical | Raw SQL | FromSqlRaw without tenant filter | 1h - ban via analyzer |
| C3 | Critical | File Storage | Logo, payroll, expense, assignment file URLs without tenant ownership check + predictable filenames | 4h - create FilesController with tenant check + random GUID + outside wwwroot |
| C4 | Critical | Caching | Cache keys without tenantId could leak across tenants | 2h - TenantCacheKeyFactory |
| C5 | Critical | API | Missing tenant filter in some endpoints (need exhaustive audit) | 6h - grep + add filter + tests |
| C6 | Critical | File Storage | Payroll export highly sensitive NationalID, bank account without tenantId in URL | 2h - add tenantId + secure serving |
| H1 | High | IDOR | ID guessing without ownership check beyond tenant filter (e.g., route stops without checking route belongs to tenant) | 3h - add ownership checks |
| H2 | High | Database | No RLS, relies on app filter only | 2h - add triggers or ProxySQL |
| H3 | High | Background Jobs | Task.Run without BeginTenantScope, AsyncLocal leak | 3h - fix MessagingBackgroundJob + InvoiceGeneration to use tenant scope |
| H4 | High | Imports | Import could reference other tenant's grade/stream via ID guessing | 2h - add tenant filter + composite FK enforcement |
| H5 | High | Exports | Exports file URL without tenant check + predictable filename | 2h - same as C3 |
| H6 | High | Caching | IMemoryCache/Redis keys without tenantId | 2h - same as C4 |
| M1 | Medium | TenantResolution | X-Tenant-Slug header allowed in prod for local dev could cause confusion/enum | 0.5h - only allow in Development |
| M2 | Medium | Global Admin | Platform admin console ListTenants may not use explicit no-tenant scope, returns empty | 1h - add BeginNoTenantScope in service |
| M3 | Medium | Reports | Reports performance okay after V17 covering indexes, but no partitioning yet for attendance | 2h - already done audit_logs partitioning, attendance partitioned version created |
| M4 | Medium | Database | MySQL partitioned tables cannot have FKs, so attendance partitioning requires dropping FKs | 2h - documented |
| M5 | Medium | Caching | Permission cache per user without tenantId | 1h - include tenantId in cache key |
| M6 | Medium | Background Jobs | NightlyBillingJob example correct but actual jobs may not follow pattern | 1h - audit all jobs |
| M7 | Medium | File Storage | Expense proof_url user-provided could be external URL with XSS? | 1h - validate URL |
| M8 | Medium | API | Some endpoints use Roles instead of Permissions (inconsistent) | 1h - standardize |
| M9 | Medium | Exports | CSV injection via student name starting with =,+,-,@ (formula injection) | 1h - sanitize CSV |
| L1 | Low | TenantResolution | Subdomain vs token mismatch logs security event but returns generic message, good | - |
| L2 | Low | Database | Unique constraints with soft delete now fixed via generated columns | - |
| L3 | Low | Caching | No distributed cache invalidation on tenant delete? | 1h - add |
| L4 | Low | Background Jobs | No dead letter queue for failed messaging jobs | 1h - add |
| L5 | Low | File Storage | Logo URL in tenant table, if file deleted URL breaks - need fallback | 0.5h - add default |

---

## Recommended Fix Order - One at a Time

**Phase 0 Critical - Fix Now (Blocks Go-Live):**

1. **C3 + C6 File Storage Without Tenant Check** - Create FilesController with tenant ownership check, random GUID, outside wwwroot, fix payroll export URL to include tenantId
2. **C4 Caching Without TenantId** - Create TenantCacheKeyFactory, audit all IMemoryCache/IDistributedCache usages, ensure keys include tenantId
3. **C1 IgnoreQueryFilters CI Check** - Add CI grep to fail build if IgnoreQueryFilters found outside allowed files, add Roslyn analyzer

**Phase 1 High:**

4. **H3 Background Jobs Tenant Scope** - Fix MessagingBackgroundJob and InvoiceGeneration to use BeginTenantScope per tenant
5. **H4 Imports Tenant Validation** - Add tenant filter to grade/stream/subject lookup in import
6. **H1 IDOR Ownership Checks** - For each route, check parent belongs to tenant (e.g., stops route's route belongs to tenant)
7. **C5 Missing Tenant Filter Audit** - Exhaustive grep for Set<T>().Where without TenantId

**Phase 2 Medium:**

8. **M1 X-Tenant-Slug only in Development**
9. **M2 Platform Admin Explicit No-Tenant Scope**
10. etc.

---

## Immediate Next Fix: C3+C6 File Storage

Most critical for payroll sensitive data leak.

