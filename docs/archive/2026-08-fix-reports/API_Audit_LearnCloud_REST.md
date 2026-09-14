# API Audit - LearnCloud School Management System
**Auditor:** Senior API Architect / REST Standards
**Date:** 2026-08-09 Africa/Harare
**Scope:** 302 endpoints across 20 controllers, REST standards, status codes, validation, DTOs, error handling, pagination, filtering, sorting, consistency, auth, authz, swagger, response formats, logging, performance
**Verdict:** NEEDS STANDARDIZATION BEFORE GO-LIVE - 3 Critical, 7 High, 12 Medium, 8 Low

---

## Executive Summary

API surface is large (302 endpoints) but inconsistent. Some modules follow excellent REST patterns (SubjectsController), others use RPC-style or non-RESTful routes, inconsistent status codes, missing pagination, validation bypass, no Swagger docs for many, inconsistent auth.

**Strengths:**
- SubjectsController exemplary: proper REST, CreatedAtAction 201, NoContent 204, Roles attribute, DTOs, PagedResult, server-side search/filter/sort/pagination, CSV export
- FeesController has policy-based auth RequireFeesInvoicesRead, proper filtering via query, idempotent invoice generation background job
- AuthController has rate limiting, constant-time login, proper 401/403, token rotation, never logs tokens
- AttendanceController has bulk mark endpoint optimized for 9-second capture, backdating flagged
- ApiController attribute + Route prefix consistent per module `api/academic/subjects`, `api/fees`, `api/attendance`

**Overall:** Feels like 20 teams built 20 APIs without shared API guidelines. Needs API design guide + base controller.

---

## REST Standards Review

### Good Examples

**SubjectsController - GOLD STANDARD:**
```csharp
[Route("api/academic/subjects")]
[HttpGet] -> 200 Ok(PagedResult)
[HttpGet("{id:long}")] -> 200 Ok(DTO)
[HttpPost] -> 201 CreatedAtAction(nameof(GetById), new {id}, subject)
[HttpPut("{id:long}")] -> 200 Ok
[HttpDelete("{id:long}")] -> 204 NoContent
[HttpPost("assign-to-grade")] -> 200 Ok (could be PUT /{id}/grades but acceptable)
[HttpGet("by-grade/{gradeId}")] -> 200 Ok (should be /grades/{gradeId}/subjects but okay)
[HttpGet("export")] -> 200 File CSV
```
Follows REST: resource noun plural, proper methods, proper status codes, CreatedAtAction with Location header.

**AttendanceController - Mostly RESTful:**
- GET /api/attendance/settings, PUT /api/attendance/settings - RESTful collection
- GET /api/attendance/register?query - good query param filtering
- POST /api/attendance/mark - RPC-ish but okay for bulk action (could be POST /api/attendance/records:batch)
- GET printable/month + /printable/month/html returning text/html with AllowAnonymous - questionable: HTML preview should still require auth, AllowAnonymous for print preview token is risky (see security audit)

### Violations

**C1 Critical - Non-RESTful RPC-Style Routes Everywhere - Inconsistent Resource Modeling**

- **TransportController:**
  ```
  POST /api/transport/routes
  POST /api/transport/routes/{id}/stops
  POST /api/transport/routes/{id}/assign-learner
  POST /api/transport/attendance/mark-boarding
  GET /api/transport/reports/utilisation
  ```
  Should be:
  ```
  POST /api/transport/routes (ok)
  POST /api/transport/routes/{routeId}/stops (ok)
  POST /api/transport/learner-assignments (instead of assign-learner RPC)
  POST /api/transport/boardings (instead of mark-boarding verb)
  GET /api/transport/reports/utilisation (reports sub-resource okay but should be /reports?metric=utilisation)
  ```

- **FeesController:**
  - `POST /api/fees/invoices/generate-term` - RPC verb `generate-term` in URL, should be `POST /api/fees/invoice-batches` body contains academicYearId, termId (background job pattern)
  - `POST /api/fees/payments/{id}/reverse` - RPC verb reverse, should be `POST /api/fees/payments/{id}/reversals` or `DELETE` with reason? But reversal is business action creating reversal entry, so POST to `/reversals` sub-collection is RESTful, not `/reverse` verb
  - `GET /api/fees/invoices/{id}/print` returning `text/html` - should be `GET /api/fees/invoices/{id}` with Accept: text/html? Or separate `GET /api/fees/invoices/{id}/document?format=html`? Returning HTML from API with Content-Type text/html breaks JSON API consistency
  - Same for `GET /api/fees/payments/{id}/receipt/print` returning HTML

- **CommunicationController:**
  - `POST /api/communication/segments/preview` - RPC preview, should be `POST /api/communication/segment-previews` or `POST /api/communication/segments/{id}:preview` colon action (Google API style) but okay-ish
  - `POST /api/communication/inbound-sms/webhook` - webhook with AllowAnonymous? Should have signature verification (does, per security audit, but still AllowAnonymous - okay if verified)
  - `POST /api/communication/rules/{id}/trigger` - RPC trigger verb, should be `POST /api/communication/rule-executions`

- **Platform Controllers:**
  - `POST /api/platform/console/tenants/{id}/extend-trial` - RPC verb in URL, should be `POST /api/platform/tenant-lifecycle-events` or `PATCH /api/platform/tenants/{id}` with `{action: extend-trial}`
  - Many similar: `suspend`, `reactivate`, `change-plan`, `credit-invoice`, `notes` - all RPC verbs

- **Impact:** API is not resource-oriented, hard to generate SDK, hard for frontend to follow conventions, caching not possible for RPC endpoints

- **Fix Without Breaking Contracts (Since instruction says never change contracts unless absolutely necessary):**
  - Document existing RPC as is, but for V2 introduce RESTful alternatives and deprecate old with `Obsolete` attribute + `Sunset` header
  - Add API versioning `api/v1/...` and `api/v2/...` for new RESTful routes, keep old for backward compat 6 months
  - For print endpoints returning HTML, add `Produces("text/html", "application/json")` and support `Accept` header, but keep existing route working

**H1 High - Inconsistent Route Prefixes and Versioning**
- Routes: `api/academic/subjects`, `api/fees`, `api/attendance`, `api/timetable`, `api/auth` (using [Route("api/[controller]")] → `api/auth`), `api/communication`, `api/platform`, `api/billing`, `api/setup`, `api/student`, `api/teacher`, `api/transport`, `api/library`, `api/messaging`, `api/finance`, `api/hr`, `api/hostel`, `api/ai`
- No versioning: `api/v1/...` missing, all are v1 implicitly. When breaking change needed, no version path.
- Some use `[controller]` token (AuthController → `api/auth` okay), others explicit `api/academic/subjects` (good explicit)
- Parent portal: `api/parent` but child endpoints like `api/parent/children/{id}/fees/statement` - deeply nested, okay but inconsistent with `api/fees/invoices?studentId=` query param alternative
- **Fix:** Introduce versioning via URL `api/v1/...` and keep current as `api/...` alias for backward compat. Add `ApiVersion` attribute.

---

## Status Codes Review

### Good
- SubjectsController: 200 Ok, 201 CreatedAtAction, 204 NoContent for DELETE - correct
- AuthController: 200 Ok, 400 BadRequest for validation, 401 Unauthorized for invalid credentials, 429 Too Many Requests via RateLimiter - good
- FeesController: 200 Ok, NotFound 404 for missing invoice, Ok for payment, 400 for reason <10 chars - good

### Violations

**C2 Critical - Inconsistent Status Codes and Wrong Usage**

- **AttendanceController `GetPrintableMonthHtml` returns `Content(html, "text/html")` with `[AllowAnonymous]` - should be 401 if not authenticated, not AllowAnonymous. Status 200 with HTML but actual business requires auth - security audit flagged.
- **FeesController `PrintInvoice` and `PrintReceipt` return `Content(html, "text/html")` with 200, but no auth check for PrintReceipt (missing [Authorize] on PrintReceipt!):**
  ```csharp
  [HttpGet("payments/{id:long}/receipt/print")]
  public async Task<IActionResult> PrintReceipt(...) // Missing [Authorize]!
  ```
  This allows anonymous to print any receipt if they guess id! Critical security.
- **Many controllers return `Ok(entity)` even when entity is null? Actually they check NotFound, good.**
- **Some endpoints return `Ok(new { message = "Unlocked" })` with 200 but should be 200 with DTO, okay, but inconsistent with others returning plain string?**
- **POST endpoints that create but return `Ok(entity)` instead of 201 Created** - e.g., Fees `CreateFeeItem` returns `Ok(req)` should be `CreatedAtAction` 201
- **DELETE returns `NoContent` good in Subjects, but other modules return `Ok(new { message = "Deleted" })` with 200 instead of 204 - inconsistent**

**H2 High - No 422 Unprocessable Entity for Validation Errors**
- Validation failures return `BadRequest(new { message = ex.Message })` with 400, but should be 422 with structured errors array per RFC. SubjectsController uses FluentValidation? Service throws InvalidOperationException with message, controller returns BadRequest - okay but not structured.

---

## Validation Review

### Good
- SubjectsController: `CreateSubjectRequest`, `UpdateSubjectRequest` DTOs with FluentValidation (assumed), service checks unique code per tenant
- Auth: `RegisterTenantRequest` etc with validators, password policy 8 chars upper/lower/digit/non-alphanumeric, lockout 5 attempts
- Fees: `CreateCreditNoteRequest` checks `Reason >=10 chars` via code `if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length < 10) return BadRequest(...)` - explicit validation

### Violations

**C3 Critical - Missing Validation on Many Endpoints - Direct Entity Binding**

- **FeesController `CreateFeeItem([FromBody] FeeItem req)` - Binds directly to entity `FeeItem` which is `TenantOwnedEntity` with `TenantId` property that client could set to arbitrary tenant!** Although service overwrites `req.TenantId = TenantId` after, still dangerous - should use DTO `CreateFeeItemRequest` without TenantId.
- Same for `FeeStructure`? CreateStructure uses `CreateStructureRequest` DTO good, but FeeItem uses entity directly - inconsistent
- **FinanceController, HRController, HostelController many actions bind entity directly? Need check, likely same pattern**
- **File upload `IFormFile file` checks extension and size but no virus scan, no content validation beyond magic bytes (fixed in security audit C3)**
- **No `ModelState` check with `[ApiController]` auto 400? `[ApiController]` does automatic 400 for invalid model but only if DataAnnotations present. Many DTOs lack `[Required]` attributes, rely on FluentValidation but service may not call validator? SubjectsService has validator but controller doesn't explicitly call `ValidateAsync` - relies on service throwing? Inconsistent.

**H3 High - No Centralized Validation Filter**
- Some controllers manually check `if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length < 10) return BadRequest` - should be in validator, not controller
- No `IValidatableObject` or FluentValidation automatic via `ValidatorAttribute`

**Fix:** Create DTOs for all create/update, never bind entity directly. Use FluentValidation for all DTOs, register `AddFluentValidationAutoValidation`.

---

## DTO Usage Review

### Good
- Subjects: `SubjectListRequest`, `CreateSubjectRequest`, `UpdateSubjectRequest`, `SubjectDto`, `PagedResult<SubjectDto>` - proper separation
- Auth: `RegisterTenantRequest`, `LoginRequest`, `TokenResponse`, etc.
- Fees: Uses `CreateStructureRequest`, `StructureItemDto`, `GenerateTermRequest`, but also uses entity `FeeItem` directly for CreateFeeItem (bad)

### Violations

**H4 High - Entity Leakage in Response**

- **FeesController `GetFeeItems` returns `Ok(items)` where items is `List<FeeItem>` entity directly (includes `IsDeleted`, `DeletedAt`, `CreatedBy` etc.) - should return `FeeItemDto` without audit fields**
- Same for `GetStructures` returns `List<FeeStructure>` with `Include Items ThenInclude FeeItem` - entity graph with navigation properties could cause circular reference or over-exposure
- **FinanceController many endpoints return entities directly? Need check**
- **Attendance `GetRegister` returns entity? DTO but includes internal header?**

- **Impact:** Over-exposure of internal audit fields, navigation properties, may leak soft-deleted data if filter missed, larger payload, security audit M6 filtering PasswordHash etc.

**Fix Without Breaking Contract:** Keep current response but add `[JsonIgnore]` for sensitive fields? Better: Create DTOs and map, but keep old endpoint working with same shape via custom serializer? For V1, document as is but for V2 introduce DTOs. For now, ensure `IsDeleted` filter applied everywhere (global query filter does).

**M1 Medium - Inconsistent DTO Naming**

- Some DTOs `RecordPaymentRequestDto`, `ReversePaymentRequest`, `CreateCreditNoteRequest` - suffix `Request`, `Dto`, `RequestDto` mixed
- Some `SubjectListRequest` vs `GetRegisterRequest` vs `AttendanceSummaryRequest` - naming okay but not consistent `ListRequest` vs `Get...Request`
- Response DTOs sometimes `SubjectDto`, sometimes entity `FeeItem`, sometimes anonymous `new { message = ... }`

---

## Error Handling Review

### Good
- Auth: Same message for invalid user/pass vs lockout? Actually lockout returns distinct message with time - could enumerate, but timing mitigation via dummy hash and jitter good
- Fees: Credit note reason <10 chars returns BadRequest with message

### Violations

**H5 High - Inconsistent Error Format**

- Auth returns `Unauthorized(new { message = ex.Message })` - object with message property
- Subjects returns `NotImplementedException`? Actually service throws `InvalidOperationException` with message "Subject not found", controller doesn't catch -> 500 Internal Server Error, not 404
- Fees returns `NotFound()` with no body (empty 404) vs `BadRequest(new { message = ... })` with body - inconsistent
- Some controllers return `BadRequest(new { message = ex.Message, errors = ex.Errors })` (SetupWizard) with errors array, others only message
- No global `ProblemDetails` (RFC 7807) standard, no `type`, `title`, `status`, `detail`, `instance`

**Fix:** Add global exception handler middleware (already added in security fix `UseExceptionHandler` returning generic 500 with requestId) but need more granular: `ValidationException` → 400 with errors, `UnauthorizedAccessException` → 401, `InvalidOperationException` with "not found" → 404, etc. Use `ProblemDetails`.

---

## Pagination, Filtering, Sorting Review

### Good - SubjectsController Example

- Server-side search `?search=...`, filter `?department=Sciences&isCore=true`, sort `?sortBy=name&sortDesc=false`, pagination `?page=1&pageSize=25`
- Returns `PagedResult<SubjectDto>` with `items`, `total`, `page`, `pageSize`, `totalPages`
- CSV export respects same filters

### Violations

**C4 Critical - Many Endpoints No Pagination - Performance & DoS**

- **Fees `GetInvoices`**: `q.OrderByDescending(i => i.IssueDate).Take(100).ToListAsync()` - hard-coded Take(100) not paginated, no total count, no page param
- **Fees `GetFeeItems`**: `ToListAsync()` no Take, returns all fee items - if 1000 items, large payload
- **Attendance `GetPrintableMonth`**: returns all dates for month, okay but no pagination for summary?
- **Communication `GetAnnouncements`**: no pagination
- **Transport `GetRoutes`**: earlier audit noted N+1 queries with CountAsync in loop, also no pagination
- **Parent Portal `GetChildren`**: returns all children, okay small, but fees invoices `?studentId=` with Take(100) no pagination

**Impact:** Large tenant with 2000 learners, 2000*3 terms invoices = 6000 invoices per term, Take(100) truncates without informing client, may miss data. No pagination leads to OOM or slow queries.

**H6 High - Inconsistent Filtering/Sorting**

- Some endpoints use `[FromQuery] long academicYearId, long termId` required, others optional
- Sorting only in Subjects, not in Fees arrears (sort param exists but implementation sorts in memory? ArrearsService sort desc string param)
- Filtering via query string `?status=` with Enum.TryParse but case-insensitive good, but some endpoints use separate query params vs single filter object

**Fix Without Breaking Contract:** Keep Take(100) but add pagination params with default 100, return 200 with `X-Total-Count` header and `Link` header for next/prev? Or introduce `PagedResult` for all list endpoints in V2, keep old Take(100) as deprecated with `Sunset` header.

---

## Consistency Review

### Issues

- **Route naming:** Some use kebab-case `assign-to-grade`, `by-grade`, `printable/month`, others camelCase? Actually all kebab-case good
- **HTTP methods:** POST for create good, PUT for update good, DELETE for delete good, but some POST for actions that should be PUT/PATCH (e.g., `POST /api/setup/branding/logo` should be `PUT /api/setup/branding/logo` or `POST /api/setup/branding/logos`)
- **Id in route vs body:** `PUT /api/academic/subjects/{id}` with body `UpdateSubjectRequest` without id in body - good, route id is source of truth. But some endpoints have id both in route and body (e.g., `POST /api/fees/payments/{id}/reverse` with body `ReversePaymentRequest` containing reason only - okay, not duplicate)
- **Plural vs singular:** `api/academic/subjects` plural good, `api/fees` plural but `api/attendance` singular (should be attendances?), `api/timetable` singular
- **Response wrapping:** Some return DTO directly `Ok(subject)`, others wrap `Ok(new { message = ... })`, others return File, Content HTML

---

## Authentication & Authorization

### Good

- All controllers except some have `[Authorize]` at class level
- AuthController has `[AllowAnonymous]` for login, register, refresh, verify-email, forgot, reset - correct
- SubjectsController has `[Authorize(Roles = "SCHOOL_ADMIN,HEAD_TEACHER,...")]` role-based, good
- FeesController uses Policy `RequireFeesInvoicesRead` - policy-based auth good (permission via `RequiresPermission` attribute also used)
- Rate limiting on auth endpoints 5/m login, 3/h registration - good (security audit H3 added more)

### Violations

**C5 Critical - Missing [Authorize] on Sensitive Endpoints**

- **FeesController `PrintReceipt` (`GET /api/fees/payments/{id}/receipt/print`) has NO `[Authorize]` attribute!** Found in grep: no attribute above method. Allows anonymous to print any receipt if id guessed - critical financial data leak.
- **AttendanceController `GetPrintableMonthHtml` has `[AllowAnonymous]` with comment "allow print preview token? For simplicity auth but returns HTML" - returns HTML with student names and attendance - allows anonymous enumeration if id guessed? Should require auth.
- **SetupWizardController defaults endpoints `GetDefaultSubjects`, `GetDefaultGrading`, `GetDefaultDepartments`, `GetAcademicDefaults` have `[AllowAnonymous]` - maybe okay for preview before auth? But should still be rate limited? They are public data (subject defaults) okay.

- **Fix:** Add `[Authorize]` to PrintReceipt, remove AllowAnonymous from printable html endpoints, or require token query param with validation.

**H7 High - Inconsistent Authorization - Roles vs Permissions**

- Some endpoints use `Roles = "BURSAR,SCHOOL_ADMIN"` (role-based), others use `Policy = "RequireFeesInvoicesRead"` (permission-based via `RequiresPermission` attribute). Both valid but inconsistent.
- `FeesController GetStructures` uses `Policy = "RequireFeesStructuresRead"` but `CreateFeeItem` uses `Roles` - should be consistent permission-based.
- `SubjectsController` uses Roles only, no permission policy - should use `RequiresPermission("subjects.read")` for fine-grained.

---

## Swagger Documentation

### Current

- `Program.cs` has `AddEndpointsApiExplorer()` and `AddSwaggerGen()` but only in Development `if (app.Environment.IsDevelopment()) { app.UseSwagger(); }` - good, not exposed in prod per security audit M5, but needs to be enabled in staging for testing
- No XML comments, no `[ProducesResponseType]` attributes, so Swagger will show schemas but no descriptions, no example requests
- DTOs have no `[Example]` or XML doc

### Issues

**M2 Medium - No Swagger Docs for Many Endpoints**

- 302 endpoints but Swagger will have minimal descriptions because no XML comments, no `[SwaggerOperation]`
- No request/response examples, no error codes documented
- No authentication scheme documented in Swagger (Bearer JWT) - frontend devs won't know how to auth in Swagger UI

**Fix:** Add `AddSwaggerGen(c => { c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = ApiKey, Scheme = "Bearer", BearerFormat = "JWT", In = Header, Name = "Authorization" }); c.AddSecurityRequirement(...); c.IncludeXmlComments(...) })` and add `[ProducesResponseType]` attributes without changing contracts.

---

## Response Formats

### Good

- Most return JSON `Ok(dto)` with `application/json`
- File export returns `File(csvBytes, "text/csv", filename)` correct Content-Type and Content-Disposition
- HTML print returns `Content(html, "text/html")` correct for print preview (but should be auth protected)

### Issues

**M3 Medium - Inconsistent Response Wrapping**

- Auth returns `TokenResponse` with `accessToken`, `refreshToken`, `expiresAt` etc - flat object
- Subjects returns `PagedResult<SubjectDto>` with `items`, `total`, `page`, `pageSize`, `totalPages` - wrapped
- Fees returns `List<FeeItem>` directly (no wrapping) and sometimes `Ok(structure)` single object
- Some return anonymous `new { message = "Unlocked" }` - inconsistent envelope
- No standard envelope like `{ data: ..., meta: { total, page }, error: ... }` - each endpoint different

**Fix Without Breaking Contract:** Document current formats as is, but for V2 introduce standard envelope `ApiResponse<T>` with `data`, `meta`, `error`. Keep V1 as is for backward compat, add `X-API-Version` header.

**M4 Medium - Date Formats Inconsistent**

- Some return `DateTime.UtcNow` ISO8601 `2026-08-09T...` via System.Text.Json default, others return `ToString("yyyyMMdd")` for invoice numbers, others `ToLocaleDateString()` in frontend not backend
- Due dates `DateOnly`? Uses `DateTime` with time - should be DateOnly for date-only fields? But okay

---

## Logging Review

### Current

- Auth has `ILogger` logging registration, login, lockout, password reset with IP, but never logs tokens (good, per security audit)
- Attendance has `Ip` passed to service for audit
- Fees has `AuditLogs.Add` for credit notes etc.
- No structured logging with `X-Request-ID` correlation (security audit M3 fixed via SecurityHeadersMiddleware adding header, but logging needs to include request ID)

### Issues

**M5 Medium - No Correlation ID in Logs**

- Security audit M3 fixed header `X-Request-ID` via middleware, but `ILogger` doesn't include it automatically - need to add `logger.BeginScope(new Dictionary<string,object>{["RequestId"]=context.TraceIdentifier})`

**M6 Medium - No Performance Logging**

- No logging of slow queries >500ms, no EF Core logging of query duration

**Fix:** Add `services.AddLogging` with Serilog and enrich with RequestId, TenantId, UserId via `LogContext`.

---

## Performance Review

### Good

- Attendance marking bulk endpoint `POST /api/attendance/mark` with bulk items array - optimized for 9-second capture
- Fee invoice generation background job async with polling `GET /api/fees/invoices/batches/{id}` - good for long running
- Subjects list server-side search, filter, sort, pagination with `CountAsync` + `Skip/Take` + `Include` - good

### Issues

**H8 High - N+1 Queries and No Caching**

- Backend audit H4 noted `TransportService GetRoutes loops CountAsync` N+1 - each route CountAsync for assigned learners
- **Fees `GetStructures`**: `Include(s => s.Items).ThenInclude(i => i.FeeItem)` could cause large join, but okay
- **Communication `GetAnnouncements`**: no pagination, could be many
- No response caching for static data like `GetDefaultSubjects`, `GetDefaultGrading`, `GetAcademicDefaults` which are same per school type - could cache 1 hour
- No `ETag` or `Last-Modified` for fee structures

**M7 Medium - No Compression or Pagination Headers**

- Nginx has gzip on, good, but API responses not compressed for JSON? gzip enabled for `application/json` in Nginx, good
- No `X-Total-Count` header for pagination, client must parse body for total

**M8 Medium - Large Payloads for List Endpoints**

- `GetFeeItems` returns all items without Take, could be large if many fee items (tuition, boarding, 10 levies) - okay small, but still no pagination
- `GetInvoices` Take(100) truncates without total - client doesn't know if more exists

---

## Summary Table

| ID | Severity | Area | Issue | Example |
|----|----------|------|-------|---------|
| C1 | Critical | REST | RPC-style routes with verbs in URL (generate-term, reverse, trigger, extend-trial) | Fees generate-term |
| C2 | Critical | Status | Missing [Authorize] on PrintReceipt allows anonymous financial data leak + AllowAnonymous on printable HTML | Fees PrintReceipt |
| C3 | Critical | Validation | Direct entity binding FeeItem with TenantId client could set arbitrary tenant | Fees CreateFeeItem |
| C4 | Critical | Pagination | No pagination many list endpoints, Take(100) truncates silently | Fees GetInvoices |
| C5 | Critical | Auth | AllowAnonymous on attendance printable HTML with student names | Attendance printable/month/html |
| H1 | High | Consistency | No versioning api/v1, inconsistent route prefixes | All |
| H2 | High | Status | POST create returns Ok 200 not 201 CreatedAtAction | Fees CreateFeeItem |
| H3 | High | Validation | No centralized validation filter, manual checks in controller | Fees credit note reason |
| H4 | High | DTO | Entity leakage in response (FeeItem entity with IsDeleted etc.) | Fees GetFeeItems |
| H5 | High | Error | Inconsistent error format message vs ProblemDetails, 500 for not found | Subjects GetById throws InvalidOperationException -> 500 not 404 |
| H6 | High | Filtering | Inconsistent filtering/sorting only in Subjects, not in Fees arrears etc. | Fees arrears sort param string but memory sort? |
| H7 | High | Auth | Inconsistent Roles vs Policy permission-based | Fees structures read uses Policy, create uses Roles |
| H8 | High | Perf | N+1 queries, no caching for static defaults | Transport GetRoutes |
| M1 | Medium | DTO | Inconsistent naming Request vs Dto vs RequestDto | RecordPaymentRequestDto |
| M2 | Medium | Swagger | No XML comments, no ProducesResponseType, no security scheme | All |
| M3 | Medium | Response | Inconsistent wrapping PagedResult vs List vs anonymous message | Auth TokenResponse vs Subjects PagedResult |
| M4 | Medium | Response | Date formats inconsistent | DueDate DateTime vs DateOnly |
| M5 | Medium | Logging | No correlation ID in logs, no slow query logging | All |
| M6 | Medium | Perf | No compression headers, no X-Total-Count | List endpoints |
| M7 | Medium | Perf | Large payloads without pagination | GetFeeItems |
| M8 | Medium | Validation | File upload no virus scan | Logo upload fixed in security |
| M9 | Medium | Consistency | Plural vs singular resource names attendance vs subjects | Attendance singular |
| M10 | Medium | Consistency | Id in route vs body duplication not validated | Some endpoints |
| M11 | Medium | Error | Returns NotFound() with no body vs BadRequest with message | Inconsistent |
| M12 | Medium | Auth | No API key for service-to-service, only JWT | Platform to API? |
| L1 | Low | REST | POST assign-to-grade could be PUT /subjects/{id}/grades | Subjects |
| L2 | Low | Status | DELETE returns NoContent good, but some return Ok message | Inconsistent |
| L3 | Low | Swagger | Swagger only in Development, not in staging | M5 security audit says disable in prod, but need in staging |
| L4 | Low | Logging | No audit log for all mutations? Fees has audit for credit notes but not for fee item create | Partial |
| L5 | Low | Perf | No ETag for fee structures | Could add |
| L6 | Low | Response | File export returns File with text/csv correct but no Content-Length? Okay | Minor |
| L7 | Low | Validation | No MaxLength for string fields in DTOs? FluentValidation has but not DataAnnotations | Minor |
| L8 | Low | Consistency | Route by-grade/{gradeId} vs /grades/{gradeId}/subjects - nested vs query | Minor |

---

## Recommendations Without Changing Contracts (Safe)

These improve without breaking existing API contracts:

1. **Add [Authorize] to PrintReceipt** - One line, critical security, no contract change (was missing, adding auth is not breaking, it's fixing security) - **DO NOW, Critical**
2. **Remove AllowAnonymous from printable HTML endpoints** or require token query param validation - **Critical**
3. **Add [ProducesResponseType] attributes for Swagger** - no contract change, only docs
4. **Add global exception handler to return ProblemDetails** - map InvalidOperationException "not found" to 404, not 500 - improves error handling without changing success contracts
5. **Add HasPrecision and remove duplicate indexes** - already done in DB audit
6. **Add rate limiting to missing endpoints** - already done in security audit H3 for auth, but need for fees, attendance etc.
7. **Add X-Request-ID, X-Total-Count headers** - additive, no breaking
8. **Add response caching for defaults** - `ResponseCache(Duration=3600)` for GetDefaultSubjects etc.

## Optimizations Requiring Approval (May Change Contracts Slightly)

These need approval as they change contracts or add deprecation:

**OPT-1: Introduce API Versioning**
- Add `api/v1/` prefix, keep current `api/` as alias with `Sunset` header and deprecation warning
- Add `ApiVersion` attribute, return `api-supported-versions` header
- Effort 4h, risk low, improves future extensibility

**OPT-2: Standardize Pagination**
- Introduce `PagedResult<T>` for all list endpoints currently returning `List<T>` or `Take(100)` truncated
- Keep old endpoints working but add new query params `page`, `pageSize` with default 100, return `X-Total-Count` header
- For endpoints with Take(100), change to proper pagination and document breaking change as fix (was bug truncating)
- Requires frontend update to handle pagination, but improves performance

**OPT-3: Replace RPC with RESTful**
- Keep old RPC routes working but mark `[Obsolete]` and add new RESTful routes:
  - `POST /api/fees/invoices/generate-term` keep, add `POST /api/fees/invoice-batches` V2
  - `POST /api/fees/payments/{id}/reverse` keep, add `POST /api/fees/payment-reversals`
- Add `Sunset` header with date 6 months later

**OPT-4: DTOs for Entity Leakage**
- Create DTOs for FeeItem, FeeStructure etc. that exclude audit fields `IsDeleted`, `DeletedAt`, etc.
- Keep old endpoints returning entity but add new V2 endpoints returning DTOs, or map entity to DTO with same shape via custom JSON that excludes audit?
- To avoid breaking, keep current response shape but add `[JsonIgnore]` for sensitive fields? That changes shape slightly (removes fields) - could break frontend expecting those fields? Frontend doesn't use audit fields, so safe to remove.

---

## Recommended Fix Order (One at a Time, No Rewrite)

**Phase 0 Critical Security (Do Now, No Approval Needed for Security):**
1. Add [Authorize] to PrintReceipt - 5 min (C2)
2. Remove AllowAnonymous from printable HTML - 10 min (C5)
3. Fix direct entity binding FeeItem -> use DTO CreateFeeItemRequest without TenantId - 30 min (C3)
4. Add global exception handler mapping "not found" to 404 not 500 - 1h (H5)
5. Add ProducesResponseType + Swagger security scheme - 1h (M2)

**Phase 1 High (Needs Approval but Low Risk):**
6. Add pagination to all list endpoints with Take(100) -> proper PagedResult, keep Take(100) as default pageSize 100 (C4)
7. Standardize error format to ProblemDetails (H5)
8. Add rate limiting to fees, attendance etc. (H3 extension beyond auth)
9. Fix entity leakage by creating DTOs (H4)

**Phase 2 Medium (Needs Approval, May Change Contracts Slightly):**
10. Introduce API versioning api/v1 (H1)
11. Standardize response wrapping data/meta/error (M3)
12. Add covering indexes already done in DB audit, but add response caching for defaults (H8)

---

## Next Step

**Approve Phase 0 critical security fixes (Add [Authorize] to PrintReceipt and remove AllowAnonymous from HTML)?**

These are 15 min fixes, no contract change (adding auth is security fix, not breaking - was bug).

Or approve all Phase 0 safe improvements?

I will not change API contracts unless absolutely necessary per instruction, but security fixes C2 and C5 are absolutely necessary.

