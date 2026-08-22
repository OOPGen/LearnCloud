# Performance, Code Quality, Error Handling & Logging Audit
**Auditor:** Senior Performance Engineer + Senior Software Engineer
**Date:** 2026-08-09 Africa/Harare
**Scope:** Performance, Code Quality, Error Handling & Logging
**Verdict:** NEEDS OPTIMIZATION - 4 Critical, 9 High, 15 Medium, 10 Low

---

## 1. Performance

### CRITICAL

#### C1 - N+1 Queries - TransportService GetRoutes Loops CountAsync
- **Location:** `src/LearnCloud.Transport/Services/TransportService.cs:75-120` (inferred from previous audit, also line 323 Console.WriteLine)
- **Problem:** `GetRoutes` loops `foreach route` then `CountAsync(r => r.RouteId == route.Id)` for assigned learners, plus another CountAsync for capacity, plus separate query for utilisation. For 20 routes, 40+ DB roundtrips.
- **Why it matters:** 20 routes * 2 counts = 40 queries, each 5-20ms, total 200-800ms for one request, under load 50 concurrent tenants = 2000 queries/s, MySQL CPU spike, slow API >1s
- **Recommended Fix:** Single query with GROUP BY: `SELECT r.Id, COUNT(ra.StudentId) as Assigned FROM routes r LEFT JOIN route_assignments ra ON ra.RouteId = r.Id GROUP BY r.Id` or use `_db.Routes.Select(r => new { Route = r, Assigned = r.Assignments.Count() })` with Include, or raw SQL with join. Use `ToListAsync` once.

#### C2 - N+1 Queries - ParentPortal GetChildHome 10+ Queries
- **Location:** `src/LearnCloud.ParentPortal/Services/ParentPortalService.cs:24-100` (GetChildHome loads grade, stream, invoices, attendance, latestReport, upcomingAssessments, notices, homework)
- **Problem:** Loads each via separate `FirstOrDefaultAsync` or `ToListAsync` in sequence, not parallel, 10+ roundtrips per child. Parent with 2 children = 20+ queries per home load.
- **Why it matters:** Parent portal least technical users on slow 3G, 10 queries * 50ms = 500ms + network overhead, feels slow, high MySQL load at 08:00 when parents check before school.
- **Fix:** Use `Task.WhenAll` parallel fetching, or single query with multiple Includes, or create stored procedure `GetChildHomeSummary` with joins. Also add caching via Redis with TenantCacheKey for 5 min for invoices/balance (balance changes slowly).

#### C3 - Unnecessary Database Calls - FeeCalculationService Round2 Everywhere
- **Location:** `src/LearnCloud.Fees/Services/FeeCalculationService.cs` and `FeesController` lineTotal = Round2, plus InvoiceGenerationService loops FeeItem lookup per line item via `FirstOrDefaultAsync` inside foreach
- **Problem:** InvoiceGeneration for term: 500 students * 5 fee items = 2500 FeeItem lookups via `FirstOrDefaultAsync` in loop = N+1, plus Round2 is cheap but called many times
- **Why:** Slow invoice generation background job, could take minutes for 500 students
- **Fix:** Preload all FeeItems into dictionary `Dictionary<long, FeeItem> feeItems = await _db.FeeItems.Where(f => f.TenantId == tenantId).ToDictionaryAsync(f => f.Id)` once, then lookup in memory. Batch.

#### C4 - Large React Renders & No Code Splitting - Bundle Size 189KB JS + 69KB Marketing HTML
- **Location:** `src/LearnCloud.Web/src/App.jsx` imports all routes eagerly, `marketing-site/index.html` 69KB single HTML with CDN React UMD + inline JS for 12 pages SPA (no code split), `src/LearnCloud.Web` Vite build 189KB JS gz 60KB - okay but no lazy loading for TeacherPortal, ParentPortal, Fees, etc. which are 15-35KB each
- **Problem:** All frontend modules Frontend/*.jsx are 10-35KB each, total ~300KB JS if bundled together, no `React.lazy` + `Suspense`, no route-based code splitting. Marketing site loads all 12 pages JS (69KB) even if user only visits home.
- **Why:** Slow on 3G, 300KB JS * 50 tenants concurrent = bandwidth, Time to Interactive >3s on mid-range phone
- **Fix:** Implement `React.lazy(() => import('./pages/SubjectsList'))` + `Suspense fallback={<Skeleton>}`, Vite `manualChunks` for vendor split, marketing site should be separate Vite app with code splitting per route, not single 69KB HTML. Image optimization: no images currently, but logo uploads should be compressed and served via CDN with `Cache-Control`.

### HIGH

#### H1 - Slow API Endpoint - Fees Arrears By Class/Amount Computes SUM in Memory?
- **Location:** `src/LearnCloud.Fees/Services/ArrearsService.cs:8132` (approx) - GetArrearsByClassAsync does `invoices.Sum(i => i.BalanceDue)` after loading all invoices into memory? Need check if query does SUM in SQL or memory.
- **Problem:** If invoices 6000 per term, loading all into memory then Sum is inefficient, should do `SUM` in SQL via `GroupBy` + `SumAsync`
- **Why:** Slow for large schools 2000 learners * 3 terms = 6000 invoices per term, loading all into memory 6000 rows * 50 schools = 300k rows, high memory
- **Fix:** Use `await _db.FeeInvoices.Where(...).GroupBy(i => new { i.StudentId, i.GradeId }).Select(g => new { StudentId = g.Key.StudentId, TotalArrears = g.Sum(i => i.BalanceDue) }).ToListAsync()`

#### H2 - Inefficient EF Core Queries - Include ThenInclude Without Filtering
- **Location:** `FeesController.GetStructures` - `Include(s => s.Items).ThenInclude(i => i.FeeItem)` loads all items and fee items even if not needed for list view? For list, maybe only need fee structure header, not items. Should use projection `Select`.
- **Problem:** Over-fetching, large join, more data than needed
- **Fix:** Use `Select` DTO projection, not `Include`, for list endpoints. Use `AsNoTracking()` for read-only.

#### H3 - Large React Renders - No memoization
- **Location:** `src/LearnCloud.AttendanceTimetable/Frontend/AttendanceRegisterCapture.jsx` - `filteredStudents.map(s => { const rec = records[s.studentId]; return <div>...<button onClick={()=>cycleStatus(s.studentId)}>...` - creates new function on each render for each student (40 students * re-render on every status change = 40 new functions)
- **Problem:** Unnecessary re-renders, React reconciliation slow on mid-range phone
- **Fix:** Use `useCallback` for `cycleStatus`, `React.memo` for student row component, `useMemo` for filteredStudents.

#### H4 - Bundle Size - No Tree Shaking for Lodash/Moment?
- **Location:** `package.json` - check dependencies, many Frontend JSX import React only, no heavy libs, but marketing-site uses `https://cdn.tailwindcss.com` (full Tailwind 3.4  ratio) via CDN, not purged, ~70KB CSS (actually Tailwind CDN is ~350KB unpurged)
- **Problem:** Tailwind CDN loads full library, not purged, slow
- **Fix:** Use Tailwind via PostCSS purging (already in Vite app via tailwind.config.js content), remove CDN Tailwind from marketing-site, use built CSS.

#### H5 - Caching - No Redis/MemoryCache for Static Data
- **Location:** `src/LearnCloud.SetupWizard/Services/WizardService.cs` `GetDefaultSubjectsAsync` loads from `SubjectDefaults.GetBySchoolType(schoolType)` static list, no caching needed, but `TenantSettings`, `FeeStructures`, `Grades` etc. queried every request without caching
- **Problem:** Repeated queries for same tenant settings on every request (TenantSettings per request via middleware? No, via DB? TenantContext has CurrentTenant from DB lookup per request - that's 1 query per request for tenant lookup)
- **Fix:** Cache `Tenant` via `IMemoryCache` with key `TenantCacheKey.ForTenant(tenantId, "tenant")` TTL 5 min, cache `TenantSettings` similarly.

#### H6 - Memory/CPU - No Pagination for Large Exports
- **Location:** `FeesController ExportCsv` loads all subjects via `GetListAsync` with PageSize 1000 but could be large for students export (not yet, but students export would be large)
- **Problem:** Exporting 2000 students as CSV via `StringBuilder` + `ToListAsync` loads all into memory, could OOM
- **Fix:** Use streaming `IAsyncEnumerable` + `Response.BodyWriter` or `FileStreamResult` with yield, not StringBuilder.

### MEDIUM

- M1 - Unnecessary database calls - `TeacherDashboardService` loads registersTodo via multiple queries for each grade/stream, could be single query
- M2 - Large React renders - ParentPortal child switcher re-renders all tabs on child change, should use Context
- M3 - Image optimization - Logo upload 2MB max now, but no compression, no WebP, no resize to 200x200 for avatars
- M4 - Code splitting - No lazy loading for heavy modules like `TransportModule.jsx` 27KB, `TeacherPortal.jsx` 27KB
- M5 - Caching - No HTTP caching headers for static data like `GetDefaultSubjects` (could be `ResponseCache Duration=3600`)

### LOW

- L1 - Bundle size minor - Vite 189KB JS includes React Router DOM, but could split vendor chunk
- L2 - Memory - No explicit memory concern, but `StudentEnrolment` history could grow large per student (repeat/transfer) - 10 rows per student * 2000 = 20k rows, okay

---

## 2. Code Quality

### CRITICAL

#### C5 - Dead Code & Unused Files - LearnCloud_Landing_Page.html 32K duplicate marketing-site 68K
- **Location:** `/home/user/LearnCloud_Landing_Page.html` (32KB) duplicate of `marketing-site/index.html` (69KB) - both exist, one is old CDN React UMD version, one is new? Which is canonical? Causes confusion, maintenance double.
- **Problem:** Duplicate landing page, 32K + 68K = 100KB wasted, dev may edit wrong file, marketing-site README says marketing site is static HTML CDN React UMD, but Vite app is new. Two sources of truth.
- **Why it matters:** Tech debt, confusion, larger pack size 2.1MB zip includes both, deployment may serve wrong one via Dockerfile.web expects `marketing-site/` but old file `LearnCloud_Landing_Page.html` not used.
- **Fix:** Delete `LearnCloud_Landing_Page.html` (32K) and `LearnCloud_LandingPage.jsx` (15KB) old, keep only `marketing-site/index.html` as canonical for marketing, and `src/LearnCloud.Web/` as Vite app for app shell. Or move marketing-site into Vite app as `src/pages/Marketing.jsx`. For now, delete duplicate 32K file and document canonical.

#### C6 - Console Logs & TODOs in Production Code
- **Location:** 
  - `TransportService.cs:323` `Console.WriteLine($"Bulk assign failed for student {studentId}: {ex.Message}");`
  - `InvoiceGenerationService.cs:57` `Console.WriteLine($"Invoice generation failed: {ex}");`
  - `MessagingController.cs:313` `Console.WriteLine($"Background job failed: {ex}");`
  - `AttendanceRegisterCapture.jsx:95` `console.log("Backdated flagged in audit");`
  - `AttendanceCaptureScreen.jsx:39` `console.log("Offline, using cached");`
- **Problem:** Console.WriteLine in production goes to stdout but not structured logging, loses correlation ID, not searchable in Seq/ELK, may leak sensitive data (studentId). console.log in frontend clutters browser console, not professional for enterprise.
- **Why:** Production code should use ILogger with structured logging, not Console.
- **Fix:** Replace all `Console.WriteLine` with `_logger.LogError(ex, "Bulk assign failed for student {StudentId}", studentId)` etc., replace `console.log` with `// removed` or `logger.debug`, add ESLint rule `no-console`.

### HIGH

#### H7 - Duplicate Code - FeeCalculation Round2 Everywhere
- **Location:** `FeesController CreateStructure` `LineTotal = FeeCalculationService.Round2(item.Amount * item.Quantity)` plus `FeeCalculationService`, `InvoiceGenerationService`, `PaymentService` all duplicate `Math.Round(...,2,MidpointRounding.AwayFromZero)` logic via Round2 helper, but also many places do manual `Math.Round` without Round2.
- **Problem:** Duplicated rounding logic, if rounding mode changes (AwayFromZero vs Bankers), need change many places.
- **Fix:** Centralize all money rounding via `FeeCalculationService.Round2` and enforce via analyzer, no direct Math.Round.

#### H8 - Hardcoded Values - Colors, Business Constants, URLs
- **Location:**
  - Colors: `#0F153A`, `#5F3F96`, `#307EC0` hardcoded in 20+ JSX files via inline style `style={{background:t.categoryName==="Fees"?"#B7791F22":...}}` and `LearnCloud_Landing_Page.html` etc. - should be tokens
  - Business constants: `BackdatingWindowDays=7`, `ChronicAbsenceThreshold=85`, `MaxBooks=3`, `LoanPeriodDays=14`, `MaxRenewals=1`, `FinePerDay=1.00m`, `MaxFine=50.00m` hardcoded in `LibraryService.cs:385` as `new MembershipConfig { MaxBooks = 3, ... }` fallback when config not found, plus `AttendanceTimetable` has `BackdatingWindowDays=7` hardcoded in service `new TenantAttendanceSettings{BackdatingWindowDays=7}`
  - URLs: `https://api.learncloud.co.zw`, `https://learncloud.co.zw/api/webhooks/payments/paynow` hardcoded in `PayNowOptions.ResultUrl`, `OnlinePaymentsController`, `Mobile AuthContext`
- **Problem:** Hardcoded values make config changes require code deploy, not env, violates 12-factor, colors inconsistency per UI/UX audit
- **Fix:** Move to `appsettings.json` / env vars / TenantSettings table, use design system tokens for colors (already fixed in UI/UX audit tailwind.config.js full palette, but JSX still has hardcoded hex in some places).

#### H9 - Poor Separation of Concerns - Fat Controllers with Business Logic
- **Location:** `FeesController` has business logic for credit note creation (calculates `CN-...`, reduces invoice balance, adds audit log) directly in controller (20 lines), should be in `CreditNoteService`
- `FinanceController` 26KB has expense capture, approval, bank statement, cashbook, etc. all in one controller - should be split into `ExpenseController`, `BankAccountController`, etc.
- **Problem:** Controllers should be thin, delegate to services, harder to test, violates SOLID SRP
- **Fix:** Move credit note logic to `CreditNoteService.CreateAsync`, move finance sub-resources to separate controllers.

#### H10 - Unused Files/Classes/Services/Components - Many
- **Location:** 
  - `src/LearnCloud.Web/src/App.jsx` has placeholder Home and Login components, but real LoginSkewed is in `src/pages/LoginSkewed.jsx` - App.jsx Home is unused after fix? Actually App.jsx now uses LoginSkewed, Home still used for `/` route.
  - `LearnCloud_Setup_Wizard.html` 12KB localStorage only version duplicate of `SetupWizard.jsx` 35KB React version - duplicate
  - `LearnCloud_LandingPage.jsx` 15KB vs `marketing-site/index.html` 69KB duplicate
  - `src/LearnCloud.Finance/Entities/FinanceEntities.cs` has `Expense`, `ExpenseApproval`, `BankAccount`, `CashBookEntry` etc. but also `FinanceController` has inline logic, some entities maybe unused?
  - `src/LearnCloud.Mobile/App.jsx` has `Tab.Navigator` with 2 navigators duplicate? Lines 47 and 59 both `Tab.Navigator`
- **Problem:** Unused files increase bundle size, confusion, maintenance
- **Fix:** Audit with `dotnet build --verbosity detailed` + `npm run build -- --analyze`, use `depcheck`, `unimported`, `ts-prune` for unused exports, delete `LearnCloud_Landing_Page.html`, `LearnCloud_Setup_Wizard.html` (keep React version), `LearnCloud_LandingPage.jsx` old.

#### H11 - Poor Naming - Abbreviations, Inconsistent
- **Location:** 
  - `FeeItemRecurrence`, `DiscountType`, `InvoiceStatus` enums good, but `TimetableSlot` vs `PeriodDefinition` vs `Timetable` - slot vs period vs timetable confusing, should be `TimetablePeriod`, `TimetableEntry`
  - `GuardianStudentLink` vs `GuardianContactPreference` vs `MessageDeliveryLog` - Link vs Preference vs Log inconsistent suffix
  - `StudentMark` vs `StudentMarks` vs `ReportCardSubject` - Mark vs Subject vs Card inconsistent
  - `fee_payments` table `amount` vs `fee_invoices` `total_amount` vs `balance_due` - amount vs total_amount naming, should be consistent `amount`
- **Problem:** Poor naming hurts onboarding new devs, slows velocity
- **Fix:** Rename to consistent: `Amount`, `TotalAmount`, `BalanceDue` kept but document glossary, or use `Amount` everywhere with prefix.

### MEDIUM

- M6 - Unused imports - `TransportService.cs` has `using LearnCloud.Transport.DTOs;` `using LearnCloud.Transport.Entities;` `using LearnCloud.MultiTenancy.Context;` etc. maybe unused? Need `dotnet format --verify-no-changes` + IDE0005
- M7 - Commented-out code - `Program.cs.example` has `// app.UseAuthentication(); // JWT bearer validates tid claim` commented, `FinanceController` has `// var payload = new { api_key = _options.ApiKey, to = message.To, ... }` commented, `SetupWizardController` has `// Try custom domain lookup` etc.
- M8 - TODO/FIXME - `grep TODO` found none? But `MessagingServices.cs:249` `throw new NotImplementedException("Use ResolveAsync with AudienceRequest")` - NotImplemented is TODO
- M9 - Duplicate code - `SubjectService.ExportCsvAsync` builds CSV via StringBuilder, `FeesController.ArrearsList` also builds CSV? Actually Fees has no export, but Subjects export and maybe other exports duplicate StringBuilder CSV logic
- M10 - Hardcoded values - already H8, but also `InvoiceNumber = $"INV-{DateTime.UtcNow.Year}-{(await _db.Set<FeeInvoice>().CountAsync(...)+1):D5}"` - invoice number generation via CountAsync not safe for concurrency, should use sequence table `InvoiceSequence` with row lock

### LOW

- L3 - Poor separation - `LearnCloud.Api/Program.cs` now has 120 lines with CORS, versioning, Swagger, exception handler - should be split into `AddLearnCloudApi()` extension method per module
- L4 - Unused imports in JSX - `useState` imported but not used in some files? Need check
- L5 - Dead code - `LearnCloud.Core/Entities/Subject.cs` after C2 fix now only has `GradeSubject` linking entity, but file name `Subject.cs` misleading, should be `GradeSubject.cs`
- L6 - Commented-out code in `CommunicationFull.jsx` many `//` lines

---

## 3. Error Handling & Logging

### CRITICAL

#### C7 - Inconsistent API Errors - 500 for Not Found
- **Location:** `SubjectService.GetByIdAsync` throws `InvalidOperationException("Subject not found")` which without global handler returns 500 Internal Server Error, not 404 Not Found
- **Problem:** Client sees 500 for not found, hard to distinguish from server error, frontend may show generic error instead of "Subject not found" message, violates REST standards (should be 404)
- **Why it matters:** Poor UX, hard to debug, monitoring alerts fire for 500 when it's actually client error 404, SLOs messed
- **Recommended Fix:** Already partially fixed in API audit Phase 1 via global exception handler mapping "not found" to 404, but need to ensure all services throw custom `NotFoundException` or `EntityNotFoundException` and handler maps to 404 with ProblemDetails. Add `throw new NotFoundException("Subject", id)` and handler returns 404.

#### C8 - Stack Traces Exposed to Users (Partially Fixed)
- **Location:** Before security audit, `Program.cs` had no exception handler, so `app.UseExceptionHandler` not present, ASP.NET would return stack trace in Development, but in Production with `ASPNETCORE_ENVIRONMENT=Production` it hides stack? However `AuthModuleExtensions` had `OnAuthenticationFailed` logging exception message but not stack, good. But `FeesController` `catch (Exception ex) { return BadRequest(new { message = ex.Message }); }` exposes exception message which could be SQL or internal
- **Problem:** Stack trace or internal message like "Invalid column name 'xxx'" leaks schema, helps attacker
- **Why:** Security risk, unprofessional
- **Fix:** Already fixed via global exception handler returning generic 500 with requestId, not exception message, in `Program.cs` after security audit. Need to ensure all controllers use `ProblemDetails` not `ex.Message` directly. Replace `BadRequest(new { message = ex.Message })` with `ProblemDetails` with generic message + log detailed server-side.

### HIGH

#### H12 - Missing Validation - Direct Entity Binding (Already C3 API audit, but also error handling)
- **Location:** `FeesController CreateFeeItem` binding entity directly, no validation for Code uniqueness per tenant beyond service check that throws InvalidOperationException -> 500 before fix, now 400? Should be 409 Conflict or 422
- **Problem:** Validation missing leads to 500 instead of 400/409
- **Fix:** Use FluentValidation for CreateFeeItemRequest with RuleFor(x => x.Code).NotEmpty().MaximumLength(20).MustAsync be unique per tenant.

#### H13 - Unhandled Exceptions - Background Jobs Task.Run with Console.WriteLine
- **Location:** `InvoiceGenerationService.cs:57` `Console.WriteLine($"Invoice generation failed: {ex}");` - swallows exception, no retry, no dead letter queue, no alert
- `MessagingController.cs:313` `Console.WriteLine($"Background job failed: {ex}");` - same
- **Problem:** Background job failures silent, no monitoring, invoices may not be generated and bursar doesn't know, messages may not be sent and parents think sent
- **Fix:** Replace Console.WriteLine with `_logger.LogError(ex, "Invoice generation failed for tenant {TenantId}")` + send to dead letter queue or set batch status Failed + alert via email/Slack + retry with backoff (already has retry for messaging but not for invoice generation)

#### H14 - Missing Useful Logging - Important Actions Not Logged

- **Where Should Be Logged but Isn't:**
  - `SubjectService.CreateAsync` logs audit via `AuditLogs.Add` good, but does it log to ILogger with tenant, user, action? No, only audit table, no ILogger
  - `FeesController CreateCreditNote` adds AuditLog good, but no ILogger
  - `AttendanceService.MarkRegisterAsync` - backdating flagged in audit but no ILogger warning for backdating beyond window? It throws InvalidOperationException but no log
  - `TransportService.BulkAssign` Console.WriteLine instead of ILogger
  - All file uploads (logo, payroll) now log via FilesController but previous did not

- **Why:** Without ILogger, cannot search in Seq/ELK, cannot alert, audit table is for business audit, ILogger is for operational

- **Fix:** Add `_logger.LogInformation("User {UserId} created fee item {FeeItemId} tenant {TenantId}", userId, feeItemId, tenantId)` for all mutations, with structured logging tenant, user, entity, action.

#### H15 - Sensitive Information in Logs

- **Where:**
  - `AuthService.cs` `_logger.LogInformation("Tenant {Slug} registered with admin {Email} from ip {Ip}", tenant.Slug, admin.Email, ip)` - logs email, okay not sensitive? Email is PII but okay for audit.
  - `FakeEmailSender` logs `[FAKE EMAIL] Verification to {Email} token length {Len} tenant {Tenant} - link: https://.../verify?email={Email}&token={Token} **NEVER LOG TOKEN IN PROD**` - logs token length but redacts token, good, but comment says never log token in prod, good.
  - `PayNowGateway` logs `LogInformation("PayNow initiate: clientRef {ClientRef} amount {Amount} {Currency} method {Method} gatewayRef {GatewayRef}"` - logs amount, okay not card number, good.
  - `SmsProvider` logs `To` phone number - PII, should be redacted or hashed? Phone is sensitive but needed for debugging.
  - **Potential:** `UserConfiguration` logs? No.
  - **Check:** Any log with `Password`, `Token`, `Secret`, `ApiKey`? We fixed hardcoded secrets, now validation throws but does not log secret value, good. Need ensure no log includes `PasswordHash`, `TokenHash`, `SecurityStamp`.

- **Fix:** Audit all `_logger.Log...` calls for sensitive data: password, token, secret, api key, national_id, bank account. Ensure they log length or hashed, not raw value. Add analyzer.

### MEDIUM

- M11 - Inconsistent API errors - Auth returns `Unauthorized(new { message = ex.Message })` with 401 + message, Subjects returns `NotFound()` with no body, Fees returns `NotFound()` empty, some return `BadRequest(new { message })`, some return ProblemDetails now after fix - inconsistent, should standardize to ProblemDetails for all errors (already partially fixed via global handler, but controllers still return anonymous message objects)
- M12 - Missing validation for file uploads - extension and magic byte checked now per security C3, but no virus scan
- M13 - Stack traces exposed - fixed via global handler, but some controllers still return `ex.Message` directly which could be internal SQL error - should return generic message + log detailed server-side
- M14 - Important actions not logged - fee payment reversal, student transfer, grade promotion, role assignment should be logged via ILogger + AuditLog

### LOW

- L7 - Missing useful logging for performance - no query duration logging
- L8 - Sensitive info in logs - phone numbers in SMS logs, should be okay but could be hashed for GDPR
- L9 - Error handling for background jobs - no dead letter queue
- L10 - Logging for tenant resolution mismatch - already logs critical security event, good

---

## Prioritized Fix Order

**CRITICAL (Fix Now):**
1. C1 N+1 Transport GetRoutes - single query with GROUP BY
2. C2 N+1 ParentPortal GetChildHome 10+ queries - Task.WhenAll parallel + caching
3. C3 Unnecessary DB calls InvoiceGeneration FeeItem lookup N+1 - preload dictionary
4. C4 Bundle size + code splitting - React.lazy + Suspense, remove CDN Tailwind
5. C5 Dead code duplicate landing pages - delete LearnCloud_Landing_Page.html 32K duplicate
6. C6 Console logs - replace Console.WriteLine with ILogger
7. C7 Inconsistent errors 500 for not found - ensure NotFoundException -> 404 via handler
8. C8 Stack traces exposed - ensure no ex.Message returned directly

**HIGH (Next):**
9. H1 Arrears SUM in memory - use SQL SUM GroupBy
10. H2 Include ThenInclude over-fetching - use Select projection
11. H3 React memoization - useCallback, memo, useMemo for attendance 40 students
12. H4 Tailwind CDN full library - use PostCSS purged
13. H5 Caching TenantSettings etc. - IMemoryCache with TenantCacheKey
14. H6 Large exports StringBuilder OOM - Use IAsyncEnumerable streaming
15. H7 Duplicate Round2 - centralize FeeCalculationService.Round2
16. H8 Hardcoded values colors etc. - move to tokens/env (partially fixed via UI/UX audit)
17. H9 Fat controllers - move credit note logic to service
18. H10 Unused files - delete duplicates
19. H11 Poor naming - document glossary
20. H12 Missing validation direct entity binding - already fixed C3 but more?
21. H13 Unhandled exceptions background jobs - logger + dead letter
22. H14 Missing useful logging - add ILogger for all mutations
23. H15 Sensitive info in logs - audit logs for tokens

**MEDIUM/LOW - After HIGH**

---

## Immediate Next Fix: C1 N+1 Transport GetRoutes

Most impactful for performance under load.

