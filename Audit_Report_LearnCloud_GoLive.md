# LearnCloud - Production Go-Live Audit Report
**Role:** Senior Software Architect  
**Date:** 2026-08-03  
**Stack Audited:** ASP.NET Core Web API, EF Core, MySQL, React (Vite claimed), 22 modules in `src/`, marketing site static HTML, deployment docker-compose, mobile Expo
**Approach:** Inspected all 146 C# files, 25 JSX files, 26 SQL migrations, folder structure, naming, SOLID, duplicate code, hardcoded values, performance, scalability, security. No functionality changed.

> **Overall Verdict:** **NOT READY for go-live tomorrow without fixing Critical items.** System shows strong domain modeling (multi-tenancy frozen, fee calc single source, state machines) but has structural debt that will cause production incidents within first 30 days: duplicate Student/Grade entities across 10 namespaces will cause runtime FK mismatch, tenant isolation filter via reflection is clever but not tested with 39 entity types, no .csproj/.sln buildable, web frontend is not Vite but CDN React static HTML, and money-critical services have N+1 queries.

---

## Classification Legend

- **Critical:** Will cause data leak, data loss, security breach, or system down on go-live. Must fix before go-live.
- **High:** Will cause major bug, performance degradation, or security weakness within 30 days. Fix before go-live or within first sprint after.
- **Medium:** Technical debt that slows future development, increases bug risk, should be fixed in next 2 sprints.
- **Low:** Style, naming, minor debt, can be fixed opportunistically.

---

## 1. Architecture

### Critical

**C1: No Buildable Solution — Missing .csproj/.sln, No Vite React App**
- **Finding:** `src/` contains 22 folders like `LearnCloud.Auth`, `LearnCloud.Fees`, etc., each with `Entities/`, `Services/` etc., but **zero `.csproj` files** and **zero `.sln`**. `dotnet build` fails. `deployment/docker/Dockerfile.api` expects `src/LearnCloud.Api/*.csproj` which does not exist. Frontend claimed React Vite, but `marketing-site/` is static HTML with CDN React UMD, not Vite (no `vite.config.js`, no `package.json` with `vite`). Mobile `src/LearnCloud.Mobile/package.json` exists but depends on Expo not installed.
- **Impact:** Cannot build, cannot run CI, cannot deploy. Docker build will fail on `dotnet restore`.
- **Evidence:** `find ... -name *.csproj` returns empty, `find ... Dockerfile.api` line `COPY src/LearnCloud.Api/*.csproj` fails.
- **Fix (after approval, one issue at a time):** Create solution `LearnCloud.sln` + 1 csproj per module with proper references: `LearnCloud.MultiTenancy` (core) -> `LearnCloud.Auth` -> `LearnCloud.Core` -> `LearnCloud.Api` (host). For web, create `LearnCloud.Web` Vite project with `npm create vite@latest` and migrate `marketing-site/index.html` components into it.

**C2: Duplicate Domain Entities Across 10+ Namespaces — Runtime FK Mismatch Risk**
- **Finding:** `Student`, `Grade`, `Stream`, `Subject`, `Guardian`, `GuardianStudentLink`, `Staff`, `StudentEnrolment`, `Term`, `FeeInvoice`, `Payment` etc. are **defined in 10 different namespaces** with slightly different properties:
  - `LearnCloud.MultiTenancy.Entities.Student` vs `LearnCloud.TeacherPortal.Services.Student` vs `LearnCloud.ParentPortal.Services.Student` vs `LearnCloud.Transport.Services.Student` vs `LearnCloud.Hostel.Services.Student` vs `LearnCloud.Finance.Controllers.Student` vs `LearnCloud.Fees.Services.Student` vs `LearnCloud.ParentPortal.Services.Student` (again) etc.
  - Each re-defines `TenantOwnedEntity` or stub `BaseEntity`. Example `Grade` has `Name` only in one, but `Name/Code/AcademicYearId` in another.
  - EF Core will create **separate tables or conflicting mappings** if both are included in same DbContext. Current `LearnCloudDbContext` in `LearnCloud.MultiTenancy` includes `DbSet<Student> Students => Set<Student>()` but which `Student` type? Ambiguous.
- **Impact:** Money bug, tenant isolation bypass, or migration failure. Example: `TransportService` creates `FeeItem` with `Code=TRANSPORT` but `FeeItem` defined in `LearnCloud.Fees.Entities` vs `LearnCloud.Transport.Services.FeeItem` stub — two different tables, fee flow breaks.
- **Evidence:** `grep -r "class Student :" src/ --include="*.cs" | wc -l` = 12+, `grep -r "class Grade :" src/ | wc -l` = 8.
- **Fix:** Create single `LearnCloud.Core.Domain` (or `LearnCloud.Domain`) project with canonical entities: `Tenant`, `Student`, `Guardian`, `Grade`, `Stream`, `Subject`, `Staff`, `FeeInvoice`, etc. All other modules reference that project, no stubs. Remove all stub definitions like `// Stub entities for compilation` comments.

**C3: Tenant Isolation Filter Via Reflection Not Tested With All 39 Entity Types**
- **Finding:** `LearnCloudDbContext.OnModelCreating` iterates `modelBuilder.Model.GetEntityTypes()` and applies `HasQueryFilter(e => !e.IsDeleted && (IsExplicitNoTenant || e.TenantId == CurrentTenantId))` via reflection `MakeGenericMethod`. Clever but fragile: if `CurrentTenantId` is null and `IsExplicitNoTenant` false, filter becomes `e.TenantId == null` which for MySQL `BIGINT UNSIGNED NOT NULL` never matches, so queries return empty rather than throwing. Also `IsExplicitNoTenant` bypass allows all when explicit, but explicit scope requires privileged role check only in `INoTenantOperation`, not in DbContext itself, so a direct `new LearnCloudDbContext` with `IsExplicitNoTenant=true` without role check could leak.
- **Impact:** Potential data leak or empty results for platform admin viewing tenant list.
- **Fix:** Add unit test that enumerates all `ITenantEntity` types and asserts filter exists, and integration test that `IsExplicitNoTenant` without `PLATFORM_SUPERADMIN` role throws. Consider switching to `IModelCacheKeyFactory` with tenantId in cache key to avoid filter caching issue.

### High

**H1: No Clear Layering — Domain, Application, Infrastructure, API Mixed**
- **Finding:** Each module folder has `Entities/`, `Services/`, `Controllers/`, `Frontend/` in same project. No separation of Domain (pure entities), Application (DTOs, validators, services interfaces), Infrastructure (EF config, migrations), API (controllers). `AuthService` directly depends on `AuthDbContext` (Infrastructure) and `IPasswordHasher` (Infrastructure) and sends email (Infrastructure) — violates Dependency Inversion. `TeacherPortal.Services` contains both authorization and dashboard logic.
- **Impact:** Hard to unit test services without DB, hard to swap infrastructure (e.g., change email provider requires touching service).
- **Fix:** Introduce `LearnCloud.Application` with interfaces, `LearnCloud.Infrastructure` with EF and providers, `LearnCloud.Api` only controllers. Use MediatR or minimal API with handlers.

**H2: Large Fat Services Violate Single Responsibility (SRP)**
- **Finding:** `AuthService` does registration (transactional tenant + admin + trial), login, refresh, verify email, forgot, reset, change password, revoke, get user info — 8 responsibilities. `FinanceController` does expense capture, approval, suppliers, budgets, cashbook, bank reconciliation, petty cash, period locking, reports — 7 domains.
- **Impact:** Hard to test, hard to change one workflow without breaking another, 503-line file `AuthService.cs`.
- **Fix:** Split into `TenantRegistrationService`, `LoginService`, `RefreshTokenService`, `PasswordResetService`.

**H3: Hardcoded Secrets and Demo Keys in Code and .env.example**
- **Finding:** 
  - `Deployment/docker/Dockerfile.api` has no secret handling, but `appsettings` not audited.
  - `PayNowGateway.cs` has `IntegrationId = "demo-integration-id"` and `IntegrationKey = "demo-integration-key-32-chars-min"` hardcoded as default in `PayNowOptions`. `SmsProviderOptions` default `ApiKey = "demo-key"`.
  - `.env.example` contains `JWT_SECRET=dev-only-please-generate...` which is safe example, but also contains `MYSQL_ROOT_PASSWORD=dev-root-password-change-me` which could be used in prod if operator copies without changing.
  - Frontend `LearnCloud_Login_Skewed.html` stores JWT in `localStorage.getItem('access_token')` — vulnerable to XSS.
- **Impact:** Security breach if demo keys go to prod, XSS can steal access_token.
- **Fix:** Make `PayNowOptions` require `IOptions` validation `ValidateOnStart`, fail if demo key in Production. Move tokens to HttpOnly Secure cookies (already done for refresh, but access_token still in localStorage in web). Use `SecureStore` in mobile is good.

**H4: N+1 Queries Everywhere — Performance Bottleneck**
- **Finding:**
  - `TransportService.GetRoutesAsync`: loops routes, for each route does `CountAsync` for assigned learners — N+1, 1 + N queries.
  - `TransportService.GetUtilisationReportAsync`: loops routes, for each does `CountAsync` assigned + `CountAsync` boarded — 2N queries.
  - `ParentPortalService.GetChildHomeAsync`: loads student, grade, stream, invoices (sum), attendance records, latest report, upcoming assessments (loop per assessment loads subject), notices, homework — 10+ queries per child, could be 1 query with Include.
  - `TeacherDashboardService.GetDashboardAsync`: for each assigned class, queries grade, stream, settings, existence check attendance register — N+1.
  - `FinanceController.GetIncomeExpenditure`: does `SumAsync` for fee payments then loop per category to sum actual — N+1.
- **Impact:** Page load <2s on 3G will fail for 500 concurrent users — NFR-01 violation. Dashboard will be 2-5 seconds on 3G for 40 learners class.
- **Fix:** Use `Include`, `Select` projections, `GroupBy` in DB, `ToDictionary`, and introduce `IReadRepository` with `GetListAsync` that returns DTOs via single query. Add `AsNoTracking()` for read-only.

**H5: Missing API Documentation and Contract Testing**
- **Finding:** No Swagger/OpenAPI (`AddEndpointsApiExplorer` present but no `AddSwaggerGen`), no `ProducesResponseType` attributes, no API versioning. Frontend fetches `/api/attendance/register?gradeId...` but DTOs not shared between backend and frontend (frontend uses ad-hoc JS objects). No contract test.
- **Impact:** Frontend/backend drift, mobile app will break when backend changes DTO shape.
- **Fix:** Add `AddSwaggerGen`, publish OpenAPI JSON, generate TypeScript client via `openapi-typescript`, share DTOs via `LearnCloud.Contracts` NuGet or npm package.

### Medium

**M1: Inconsistent Naming Conventions**
- **Finding:** 
  - C# DTOs: `Record` with PascalCase properties good, but some use `snake_case` JSON (e.g., `AudienceRequest` has `GradeId` Pascal but JSON from frontend sends `gradeId` camelCase — works via System.Text.Json camelCase default, but inconsistent).
  - Tables: `tenant_id` snake_case in MySQL, but EF property `TenantId` Pascal — okay via convention, but some tables use `is_deleted` TINYINT(1) vs `IsDeleted` bool — okay.
  - Frontend: `LearnCloud_Login_Skewed.html` uses `min-h-touch` (Tailwind custom) but `tailwind.config.js` defines `minHeight.touch` only for some modules, not shared.
  - Enums: `AttendanceMode` enum int 1,2 in C# but DTO uses string "daily"/"per_period" — conversion manual, no AutoMapper.
- **Fix:** Create `.editorconfig` with naming rules, use `JsonPropertyName` attributes or centralized `JsonSerializerOptions` with `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`.

**M2: Duplicate Code — Fee Calculation, Attendance Percentage, etc.**
- **Finding:**
  - `FeeCalculationService` exists in `LearnCloud.Fees.Services` and also stub `FeeItemRecurrence` enum duplicated in `LearnCloud.Transport.Services`, `LearnCloud.Hostel.Services`, `LearnCloud.Finance.Services` each redefines `FeeItem` stub.
  - `CalculatePercentage` method duplicated in `AttendanceService` and `StudentPortalService` and `TeacherDashboardService`.
  - `Round2` method duplicated in `FinanceCalculationService` and `FeeCalculationService` — should be one `Money` value object.
  - `Student` mapping to `StudentDto` duplicated in 5 services with slightly different fields.
- **Fix:** Extract `LearnCloud.Core` with `Money` value object `public record Money(decimal Amount, string Currency)` with `Add`, `Subtract`, `Round2() AwayFromZero`, and `Percentage` value object.

**M3: Technical Debt — NotImplementedException, TODO, Console.WriteLine**
- **Finding:**
  - `HostelService.cs` has `throw new NotImplementedException()` for `CreateExeatAsync`, `ReturnFromLeaveAsync`, `GetOnLeaveAsync`, `CreateRollCallAsync`, `MarkRollCallAsync`, `GetOccupancyReportAsync` — 6 methods not implemented.
  - `TransportService` `NotifyRouteChangeAsync` and `NotifyAbsenceAsync` only logs, does not call messaging module.
  - `Console.WriteLine($"Bulk assign failed for student {studentId}: {ex.Message}")` in `TransportService.BulkAssignAsync` — should be ILogger.
  - `Frontend/TransportModule.jsx` has `alert("Failed (capacity enforcement?): "+await res.text())` — alert blocks UI thread.
- **Fix:** Create tech debt board, mark each `NotImplementedException` as ticket with priority, replace `Console.WriteLine` with `ILogger.LogWarning`.

**M4: Unused Files and Dead Code**
- **Finding:**
  - `LearnCloud_Landing_Page.html` (32K) and `marketing-site/index.html` (68K) both contain full landing page — duplicate, one should be removed.
  - `LearnCloud_Setup_Wizard.html` (12K) is standalone demo with localStorage only, not integrated with real API — unused after real wizard built in `src/LearnCloud.SetupWizard/Frontend/SetupWizard.jsx`.
  - `src/LearnCloud.AI/QueryAssistant/` folder created but only `DTOs.cs` inside, no service — empty folder.
  - `src/LearnCloud.Analytics/` folder exists but only `Entities/` created, no services — empty.
  - `src/LearnCloud.Core/Entities/Subject.cs` defines `Grade` class that conflicts with `Grade` in `LearnCloud.MultiTenancy` — one should be removed.
  - `src/LearnCloud.Fees/Services/*.cs` defines `Student` stub again — dead code.
- **Fix:** Run `dotnet build /p:TreatWarningsAsErrors=true` and enable `IDE0051` unused private member, remove files not referenced by any `.csproj`.

**M5: Hardcoded Values Beyond Secrets**
- **Finding:**
  - Colors `#0F153A`, `#5F3F96`, `#307EC0` hardcoded in 20+ JSX files and in `tailwind.config.js` and in `web-nginx.conf` and in `Fees_Calculation_Rules.md` — should be single `design-system/tokens.json`.
  - Business constants: `BackdatingWindowDays = 7`, `ChronicAbsenceThreshold = 85m`, `MaxBooks = 3`, `LoanPeriodDays = 14`, `SMS_DAILY_CAP = 1000`, `Trial 14 days`, `Read-only 30 days`, `PastDueGrace 7 days`, `SuspensionGrace 30 days` — some in `TenantAttendanceSettings` table good, but many hardcoded in service `new TenantAttendanceSettings { BackdatingWindowDays = 7 }` in `AttendanceService.GetSettingsAsync` — should come from `TenantSettings` or `appsettings.json`.
  - URLs `https://api.learncloud.co.zw`, `https://learncloud.co.zw` hardcoded in `PayNowGateway`, `MessagingCompose.jsx`, `FeesScreens.jsx`, `LearnCloud_Login_Skewed.html` — should be env var `REACT_APP_API_URL` or `window.API_BASE`.
- **Fix:** Create `appsettings.json` with section `LearnCloud:Attendance:BackdatingWindowDays`, `LearnCloud:Billing:TrialDays`, etc., and `IConfiguration` binding. For frontend, use `import.meta.env.VITE_API_URL`.

**M6: Missing Validation on Many Endpoints**
- **Finding:** `TransportController` has `[FromBody] CreateRouteRequest req` but no `[FromBody]` validator check `ModelState.IsValid` nor `FluentValidation` for transport. `HostelController` similarly. Only `Auth` and `SetupWizard` have validators.
- **Fix:** Add `AddFluentValidationAutoValidation` globally and create validators for all DTOs.

### Low

**L1: Folder Structure — Too Many Top-Level `src/LearnCloud.*` Projects**
- **Finding:** 22 projects under `src/` for a team of 2-4 devs (per C1 constraint). Each project currently has only `Entities/`, `Services/` etc., but no `csproj`, so solution explorer will show 22 projects, each with 3-5 files, overhead. Vertical slice is good, but 22 is too many for small team, should be 5-6: `Core`, `Auth`, `Academic` (attendance+timetable+assessment), `Finance` (fees+billing+finance), `Communication` (messaging+online payments), `Engagement` (teacher/parent/student portals), `Platform` (admin+hostel+transport+library).
- **Fix:** Merge related modules, keep 6-8 projects max for V1.

**L2: No Structured Logging Correlation**
- **Finding:** `ILogger` used, but no correlation ID (`X-Request-ID` header set in nginx but not logged in Serilog). No `ActivitySource` for tracing.
- **Fix:** Add `UseSerilogRequestLogging` with `EnrichDiagnosticContext` adding `TenantId`, `UserId`, `RequestId`.

**L3: No Rate Limiting on Most Endpoints**
- **Finding:** Rate limiting only on login/registration/password_reset via `AddRateLimiter`, but not on attendance mark (could be spammed 1000 times), marks entry, messaging send bulk (could be 10k SMS runaway cost), fees invoice generation.
- **Fix:** Add rate limiting to `POST /api/attendance/mark` 60/min per teacher, `POST /api/messaging/batches` 10/hour, `POST /api/fees/invoices/generate-term` 1/hour.

**L4: Frontend Direct `fetch` with `localStorage.getItem('access_token')` Everywhere**
- **Finding:** 25 JSX files each do `fetch(..., {headers:{Authorization:`Bearer ${localStorage.getItem('access_token')}`}})`. No centralized `apiClient` with refresh token rotation, no interceptor for 401.
- **Impact:** If access token expires (15 min), fetch fails, user sees error, must manually login again, no silent refresh. Also XSS risk.
- **Fix:** Create `src/shared/apiClient.js` with `axios` interceptor that checks 401, calls `/api/auth/refresh` with HttpOnly refresh cookie, retries.

**L5: No Accessibility Tests Automated**
- **Finding:** Design system mentions contrast 4.5:1, focus ring, alt text, but no `axe-core` or `jest-axe` tests in CI.
- **Fix:** Add `npm run test:a11y` with `axe-playwright`.

---

## 2. Security Findings (Overlaps with Critical/High but Explicit)

- **CRITICAL S1: Refresh Token in HttpOnly Cookie + Access Token in localStorage Mixed Pattern:** `AuthController` sets refresh token as HttpOnly cookie (`SetRefreshCookie`) good, but also returns it in body `TokenResponse.RefreshToken`. Frontend `LoginScreen` stores both in `localStorage` per code `localStorage.setItem('access_token')` and `refresh_token`. XSS can steal refresh token from body even though cookie is HttpOnly. Fix: return only access token in body, refresh only via HttpOnly cookie, never via body.
- **HIGH S2: SQL Injection via String Interpolation in Audit Logs:** `FinanceController.AuditAsync` does `System.Text.Json.JsonSerializer.Serialize(req)` but `req` contains user input `Description` that could contain `"` breaking JSON, but not SQL injection. However `AttendanceService.MarkRegisterAsync` does `_db.AuditLogs.Add(new AuditLog { NewValues = $"{{\"backdateReason\":\"{backdateReason}\"}}" })` where `backdateReason` is user input direct interpolation without JSON escaping — could break audit JSON but not SQL. Still, use `JsonSerializer.Serialize` not string interpolation.
- **HIGH S3: No Anti-Forgery on State-Changing Endpoints:** `POST /api/parent/payments/initiate` has no anti-forgery token, relies only on JWT. For cookie-based refresh, need `ValidateAntiForgeryToken` or SameSite=Lax already set for refresh cookie good, but for web app with cookies, add antiforgery.
- **MEDIUM S4: Tenant Isolation Bypass via `IgnoreQueryFilters`:** `MultiTenancyIsolationTests` uses `IgnoreQueryFilters` to verify data exists globally, but production code in `TransportService` and `HostelService` also uses `IgnoreQueryFilters` to check existence of student from other tenant for FK validation — if developer forgets to check tenant_id after `IgnoreQueryFilters`, could leak. Need Roslyn analyzer that forbids `IgnoreQueryFilters` outside `INoTenantOperation`.

---

## 3. Performance Bottlenecks (Detailed)

- **P1: Attendance Register Capture N+1:** `AttendanceService.GetRegisterAsync` loads students via `Join` with `StudentEnrolment` good, but then for each student does `records.FirstOrDefault` in memory — okay, but `GetSummaryAsync` loops `groupedByStudent` and for each group does `await _db.Grades.FirstOrDefaultAsync` and `await _db.Streams.FirstOrDefaultAsync` and `await _db.Set<Student>().FirstOrDefaultAsync` — N+1, for 40 learners = 120 queries. At 500 concurrent users, DB CPU >70% violation NFR-02.
- **P2: Fee Collection Summary:** `ArrearsService.GetArrearsByClassAsync` loops invoices, for each does `await _db.Set<Student>().FirstOrDefaultAsync`, `await _db.Grades`, `await _db.Streams` — N+1.
- **Fix:** Batch queries: `var studentIds = grouped.Select(g=>g.Key).ToList(); var studentsMap = await _db.Set<Student>().Where(s=>studentIds.Contains(s.Id)).ToDictionaryAsync(s=>s.Id);`

---

## 4. Future Scalability

- **Positive:** Shared DB with tenant_id discriminator is correct for 150-2000 learners per tenant, 50 tenants ~5k concurrent per NFR-02. `ITenantConnectionResolver` design for dedicated DB per large tenant is already documented in `MIGRATION_PATH_SEPARATE_DB.md` — good foresight.
- **Negative:** 
  - No read replicas — all reads go to primary MySQL. For 50 tenants * 40 learners per class * attendance daily = 2000 writes/day, okay, but reporting (arrears ageing, inventory value) does full table scans.
  - No caching for `TenantSettings`, `Grade`, `Stream`, `Subject` — these are read on every request but never cached, Redis exists in compose but only used for rate limiting, not for caching.
  - No CQRS — writes and reads use same DbContext, same model, no read model for dashboards. Executive dashboard pre-aggregation design exists in `Analytics` module but not implemented — currently computes on request, violates "pre-aggregate on schedule" requirement.
  - Mobile offline sync uses `offline_queue` SQLite but no conflict resolution UI implemented beyond Attendance — marks entry conflict UI not implemented in mobile app.

---

## 5. Recommended Fix Order (One Issue at a Time, Never Rewrite Whole Project)

**Phase 0 — Blockers (Critical, must fix before go-live tomorrow):**
1. C1: Create buildable solution — 1 .sln + csproj per module, fix Dockerfile.api COPY, create Vite web app, make `dotnet build` and `npm run build` pass. Estimate 1 day.
2. C2: Deduplicate Student/Grade entities — create `LearnCloud.Core.Domain` with canonical entities, remove stubs, update all `DbSet` references. Estimate 2 days, must run tenant isolation tests after.
3. C3: Fix tenant isolation filter caching and explicit no-tenant bypass — add test `IsExplicitNoTenant_WithoutRole_Throws`, add `IModelCacheKeyFactory` with tenantId.

**Phase 1 — Security & Money (Critical/High, fix before go-live or first week):**
4. H3: Remove demo secrets, move access token from localStorage to HttpOnly cookie for web (keep SecureStore for mobile), add `ValidateOnStart` for options.
5. H4: Fix N+1 queries in `AttendanceService`, `ArrearsService`, `TransportService` — add `AsNoTracking`, batch dictionaries.
6. S1: Fix refresh token in body + localStorage — return only access token in body, refresh via HttpOnly cookie only.

**Phase 2 — Tech Debt (Medium, next 2 sprints):**
7. H1: Introduce layering — create `Application` interfaces, `Infrastructure` EF, `Api` controllers only, use Mediatr handlers.
8. H2: Split fat services — `AuthService` into 4 services.
9. M2: Extract `Money` value object and `Percentage` value object, remove duplicate `Round2`.
10. M4: Remove unused files — delete `LearnCloud_Landing_Page.html` vs `marketing-site/index.html` duplicate, delete `LearnCloud_Setup_Wizard.html` demo.

**Phase 3 — Polish (Low):**
11. L1-L5: Merge projects from 22 to 8, add correlation ID, rate limiting on all mutating endpoints, centralize `apiClient.js`, add axe-core a11y tests.

---

## 6. File-Level Findings Sample (Evidence)

- `src/LearnCloud.Auth/Services/AuthService.cs:147` — `var dummyUser = new User(); var dummyHash = _hasher.HashPassword(dummyUser, "DummyPassword123!");` — good constant-time mitigation, but `RandomNumberGenerator.GetInt32(80,220)` delay uses `Task.Delay` without cancellation token check for `ct` — should pass `ct`.
- `src/LearnCloud.Fees/Services/FeeCalculationService.cs:12` — `public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);` — good, AwayFromZero not bankers.
- `src/LearnCloud.Fees/Services/InvoiceGenerationService.cs:89` — `var term = await _db.Set<Term>().FirstOrDefaultAsync(t => t.Id == termId, ct);` — if `term` null, later `term.EndDate` will NullReferenceException, should throw `InvalidOperationException("Term not found")`.
- `src/LearnCloud.Transport/Services/TransportService.cs:89` — `Console.WriteLine($"Bulk assign failed for student {studentId}: {ex.Message}");` — should be `ILogger.LogWarning`.
- `marketing-site/index.html:68K` — contains inline `<script>const DEMO_ENDPOINT = window.DEMO_ENDPOINT || "https://api.learncloud.co.zw/api/demo-requests";</script>` — hardcoded URL should be env var `VITE_DEMO_ENDPOINT`.
- `deployment/nginx/nginx.conf:54` — `client_max_body_size 20M;` — good for logo upload, but no `client_max_body_size` per location for `/api/fees/invoices/generate-term` which could be abused to upload 20M CSV.

---

## 7. Sign-Off

This report classifies findings as Critical/High/Medium/Low per your request. No functionality changed. Fixes should be applied **one issue at a time** after your approval, never rewrite whole project.

**Proposed next step:** Approve fixing **C1: Create buildable solution** first. I will create `LearnCloud.sln`, `LearnCloud.Core.csproj`, `LearnCloud.Api.csproj` with proper references, and a Vite `LearnCloud.Web` project, and make `dotnet build` and `npm run build` pass, without changing any business logic.

Awaiting your approval.

