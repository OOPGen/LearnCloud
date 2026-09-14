# LearnCloud Multi-Tenancy Specification - Freeze After Acceptance
**This is the prompt that matters most - full session, read every line**

**HQ:** Bulawayo, ZW | **Pattern:** Shared Database, Shared Schema, tenant_id discriminator | **Status:** FROZEN V1

## 1. Entities Delivered

Screenshot of Entities/Tenant.cs:

- **Tenant (school):** id, name, slug unique (petra.learncloud.co.zw), status trial/active/suspended/cancelled, city Bulawayo default, contact email/phone, primaryColor #0F153A, learnerCountBand, logoUrl, BaseEntity audit
- **TenantDomain:** TenantOwnedEntity, domain unique (portal.hillcrest.ac.zw), is_primary, is_verified, is_custom bool, tenant_id FK
- **Plan:** BaseEntity platform-owned (no tenant_id), code starter/growth/scale, maxLearners, priceMonthly/Annual DECIMAL(18,2) + currency USD, featuresJson
- **Subscription:** TenantOwnedEntity, plan_id FK, billing_cycle monthly/annual, status trialing/active/past_due/cancelled, trial_ends_at 14d, current_period_start/end, meteredActiveStudents, overLimitFlag
- **TenantSettings:** TenantOwnedEntity 1-1 Tenant, timezone Africa/Harare, dateFormat DD/MM/YYYY, baseCurrency USD, ZWG enabled, primaryColor #0F153A, secondary #5F3F96, logoUrl, currentAcademicYearId/TermId, allowCustomDomain, featuresJson

Plus sample domain entities all TenantOwnedEntity for isolation proof: Student, Guardian, GuardianStudentLink, Grade, Stream, AttendanceRecord, Subject, TimetableSlot, FeeStructure, FeeInvoice, Assessment, StudentMark, Message, AuditLog (BaseEntity tenant_id nullable)

## 2. ITenantEntity + BaseEntity

```csharp
abstract BaseEntity { Id, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, DeletedAt, DeletedBy }
interface ITenantEntity { long TenantId {get;set;} }
abstract TenantOwnedEntity : BaseEntity, ITenantEntity
```

Every tenant-owned table has tenant_id, every index leading with tenant_id per SRS. Money DECIMAL(18,2)+currency. Audit carries academic_year_id, term_id.

## 3. ITenantContext

```csharp
interface ITenantContext {
  long? TenantId, SubdomainTenantId, TokenTenantId, Tenant, IsResolved, IsPlatform, IsExplicitNoTenant, ResolutionSource (Subdomain/JwtToken/ExplicitNoTenant), Reason, ActorUserId, ActorRole
  IDisposable BeginNoTenantScope(reason,userId,role)
  IDisposable BeginTenantScope(tenantId)
  void SetResolvedTenant(...)
}
```

Implementation TenantContext uses AsyncLocal<State> with stack for nesting, works for HTTP + background jobs.

- **Anonymous (login, registration):** Resolves tenant from subdomain purely to select login context branding. Extracts slug from host petra.learncloud.co.zw or X-Tenant-Slug header for local dev, lookup Tenants by slug or TenantDomain by full host portal.hillcrest.ac.zw. Sets SubdomainTenantId as CurrentTenantId with Source=Subdomain.
- **Authenticated:** Validated JWT claim tid is source of truth. Middleware reads claim after Authentication.

## 4. Tenant Resolution Middleware Ordered Correctly

File Middleware/TenantResolutionMiddleware.cs

**Correct order in Program.cs:**

```csharp
UseRouting()
UseRateLimiter()
UseAuthentication() // validates JWT
UseMiddleware<TenantResolutionMiddleware>() // AFTER auth, BEFORE authz
UseAuthorization()
```

Logic:

1. Resolve subdomainTenantId from host + TenantDomain table
2. If anonymous -> SetSubdomainTenant -> TenantId = subdomainTenantId for login branding
3. If authenticated -> tokenTenantId from claim tid, actorUserId from uid, isPlatformRole check PLATFORM_SUPERADMIN
   - Load tokenTenant, if not found -> 403
   - **If subdomainTenantId.HasValue && tokenTenantId != subdomainTenantId -> 403 + logs security event** ILogger.LogCritical + AuditLog Action=security_violation Reason=subdomain_token_mismatch + IP + UserAgent + path, returns JSON {message:"Tenant mismatch",code:"TENANT_MISMATCH"}
   - Else SetResolvedTenant(tokenTenantId, JwtToken, subdomain..., token..., actor)
4. Store in HttpContext.Items["Tenant"]

## 5. DbContext Global Query Filter via Reflection

LearnCloudDbContext:

- Injects ITenantContext + AuditInterceptor
- OnModelCreating iterates modelBuilder.Model.GetEntityTypes() where ITenantEntity && BaseEntity -> builds `e=>!e.IsDeleted && (IsExplicitNoTenant || e.TenantId == CurrentTenantId)` via reflection GetTenantFilter<TEntity>() and HasQueryFilter
- For BaseEntity non-tenant (Tenant, Plan, AuditLog) adds IsDeleted filter only via GetSoftDeleteFilter
- No manual filter per entity - automatic by reflection

Unique constraints like (TenantId, StudentNumber) etc.

## 6. Automatic TenantId Assignment + Guard

Override SaveChangesAsync:

- Added ITenantEntity TenantId==0 -> auto-assign from _tenantContext.TenantId, if no context and not explicit no-tenant throw "Cannot save without TenantId - must be explicit scope with privileged role"
- Added with TenantId !=0 and context TenantId != entity TenantId and not explicit -> throw "TenantId mismatch: Possible cross-tenant reference attack - FOREIGN KEY in create request with other tenant's ID"
- Modified: prevent changing TenantId (OriginalValues vs Current), guard mismatch on update
- UpdatedAt/UpdatedBy, CreatedAt/CreatedBy from actorUserId
- Deleted -> soft delete IsDeleted=true, DeletedAt, DeletedBy

## 7. Audit Interceptor

AuditInterceptor:

- CaptureAuditEntries before save: iterates ChangeTracker BaseEntity Added/Modified/Deleted, captures EntityType, EntityId (0 for Added), Action create/update/soft_delete/delete, TenantId, UserId, Ip, UserAgent, Reason if explicit, AcademicYearId/TermId if prop exists, OldValues/NewValues JSON diff (skip audit timestamps)
- After base SaveChanges, WriteAuditsAsync writes AuditLog rows (BaseEntity tenant_id nullable) via second SaveChanges, avoids loop

AuditLog: tenant_id nullable for platform, user_id, entity_type, entity_id, action, old_values JSON, new_values JSON, ip, user_agent, academic_year_id, term_id, reason

## 8. Explicit No-Tenant Mechanism

INoTenantOperation + NoTenantOperation:

- AllowedRoles = PLATFORM_SUPERADMIN, SYSTEM_JOB, MIGRATION
- BeginScope(reason,userId,role) validates role in allowed, reason >=10 chars, logs critical StackTrace, writes AuditLog explicit_no_tenant_begin with reason, returns TenantContext.BeginNoTenantScope which sets IsExplicitNoTenant=true, pushes previous state onto stack
- Global filter `IsExplicitNoTenant || e.TenantId==CurrentTenantId` allows bypass when explicit, but still audited and requires privileged role
- Usage for platform admin listing all tenants, nightly billing metering all tenants, migrations

## 9. Integration Test Suite Must Run in CI

File tests/MultiTenancyIsolationTests.cs - seeds two tenants Petra and Hillcrest with overlapping data same StudentNumber 2026-0001 same InvoiceNumber same names Thabo Ndlovu

Tests:

- Tenant_A_Cannot_Read_Tenant_B_By_Guessing_ID: A FirstOrDefault studentBId => null due to filter, own succeeds
- Filtering/Sorting/Searching: Where StudentNumber=2026-0001 returns 1 own not 2, Thabo search 1, sorting 1, FeeInvoice same number 1
- Cannot_Update: Load B via IgnoreQueryFilters then SaveChanges with changed FirstName => throws TenantId mismatch, own update succeeds
- Cannot_Delete: FirstOrDefault B returns null cannot delete, attacker creating Student with TenantId=tenantB while context A throws mismatch, own delete soft-deletes IsDeleted=true
- Cannot_Reference_FK_In_Create: Create AttendanceRecord with StudentId=studentBId, filtered query returns null => blocked, check global IgnoreQueryFilters exists but tenant mismatch detected, valid own succeeds
- Subdomain_Token_Mismatch_403_Security_Event: Sets context token A subdomain B, asserts mismatch stored, writes AuditLog security_violation
- Explicit_NoTenant_Requires_Privileged_Role_And_Audited: TEACHER role throws Unauthorized, SYSTEM_JOB succeeds can query all tenants via IgnoreQueryFilters, audit explicit_no_tenant_begin exists
- Every_Module_Isolated Theory: Students, Guardians, Grades, Streams, AttendanceRecords, FeeInvoices, Assessments, StudentMarks, Messages, TimetableSlots, FeeStructures - count per tenant 1, global total with IgnoreQueryFilters >= sum, proving filter isolation

CI: .github/workflows/ci.yml - MySQL 8.0 service, Run Migration, Run Multi-Tenancy Isolation Tests with filter MultiTenancy, checks tenant_id leading indexes, forbids IgnoreQueryFilters outside explicit scope

## 10. Migration Path to Dedicated DB

See MIGRATION_PATH_SEPARATE_DB.md - exact steps:

- Introduce ITenantConnectionResolver GetConnectionString(tenantId), IsDedicatedDatabase
- Factory for DbContext per tenant based on resolver
- Keep TenantId column even in dedicated DB for code uniformity
- Zero-downtime export TenantId=42 data via explicit no-tenant scope, mysqldump where TenantId=42, import into learncloud_tenant_petra, update Tenants.ConnectionStringEncrypted, IsDedicated=true
- Background jobs loop over dedicated DBs
- Rollback via IsDedicated=false
- What changes: connection resolver, factory, middleware stores connection, background jobs dual, what stays: entities, global filter, SaveChanges guard, audit, auth handler

Freeze after acceptance - this layer is foundation for all downstream.

## Files Delivered

- Entities/Abstractions.cs, Tenant.cs, DomainEntities.cs
- Context/ITenantContext.cs, TenantContext.cs, LearnCloudDbContext.cs
- Middleware/TenantResolutionMiddleware.cs
- Interceptors/AuditInterceptor.cs
- Security/NoTenantScope.cs
- Extensions/MultiTenancyExtensions.cs
- Program.cs.example (correct ordering)
- tests/MultiTenancyIsolationTests.cs
- .github/workflows/ci.yml
- MIGRATION_PATH_SEPARATE_DB.md
- README.md

All tenant-owned indexes lead with tenant_id per spec.

End of frozen spec.
