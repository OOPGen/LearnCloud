# LearnCloud Multi-Tenancy Layer - Freeze After Acceptance
**HQ Bulawayo, Shared Database with tenant_id discriminator**

This module implements complete multi-tenancy per spec - the prompt that matters most.

## 1. Entities Delivered

- `Tenant` (school) - BaseEntity, slug unique, city Bulawayo default
- `TenantDomain` : TenantOwnedEntity - custom domain portal.hillcrest.ac.zw, is_primary, is_verified
- `Plan` : BaseEntity platform-owned (no tenant_id) - starter/growth/scale, DECIMAL(18,2)
- `Subscription` : TenantOwnedEntity - tenant_id, plan_id, billing_cycle, status trialing/active, trial_ends_at
- `TenantSettings` : TenantOwnedEntity 1-1 with Tenant - timezone Africa/Harare, date format DD/MM/YYYY, base currency USD, ZWG enabled, primary color #0F153A

Additional domain sample entities for proof (all TenantOwnedEntity):
- Student, Guardian, GuardianStudentLink, Grade, Stream, AttendanceRecord, Subject, TimetableSlot, FeeStructure, FeeInvoice, Assessment, StudentMark, Message
- AuditLog : BaseEntity with TenantId nullable for platform events

All have BaseEntity audit: id BIGINT UNSIGNED, created_at, created_by, updated_at, updated_by, is_deleted, deleted_at, deleted_by

## 2. ITenantEntity + BaseEntity

```csharp
Interface ITenantEntity { long TenantId {get;set;} }
Abstract BaseEntity { Id, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, DeletedAt, DeletedBy }
Abstract TenantOwnedEntity : BaseEntity, ITenantEntity { TenantId }
```

Every tenant-owned table has tenant_id, every index leading with tenant_id per spec (see Migrations).

## 3. ITenantContext

Resolves per request from validated JWT claim tid (source of truth) for authenticated, and from subdomain for anonymous.

Interface:
- TenantId, SubdomainTenantId, TokenTenantId, CurrentTenant, IsResolved, IsPlatform, IsExplicitNoTenant, ResolutionSource (Subdomain, JwtToken, ExplicitNoTenant, Header), Reason, ActorUserId, ActorRole
- `BeginNoTenantScope(reason, actorUserId, role)` for explicit tenant-less
- `BeginTenantScope(tenantId, source)` for tests and background jobs

Implementation `TenantContext` uses AsyncLocal<State> with stack for nesting, works in HTTP and background jobs.

- For anonymous login: resolves tenant from subdomain petra.learncloud.co.zw -> slug petra -> lookup tenants table (or TenantDomain for custom). Sets context for branding, not for data access beyond login page.
- For authenticated: JWT claim tid is truth, loads Tenant entity.

## 4. Tenant Resolution Middleware Ordered Correctly

**Order in Program.cs:**
```
UseRouting()
UseRateLimiter()
UseAuthentication() // validates JWT, populates User with tid claim
UseMiddleware<TenantResolutionMiddleware>() // after auth, before authz
UseAuthorization()
```

Middleware logic:
- Extract slug from host (first label) or X-Tenant-Slug header for dev, ignore reserved api/www/platform
- Lookup Tenant by slug or TenantDomain by full host
- If anonymous: SetSubdomainTenant -> TenantId = subdomainTenantId, Source=Subdomain
- If authenticated: get tokenTenantId from claim tid, actorUserId from uid, check platform role PLATFORM_SUPERADMIN
  - Load token tenant, if not found -> 403
  - If subdomainTenantId exists and != tokenTenantId -> **403 + logs security event** critical + writes AuditLog Action=security_violation Reason=subdomain_token_mismatch + IP + UserAgent
  - SetResolvedTenant(tokenTenantId, tenant, JwtToken, subdomainTenantId, tokenTenantId, actorUserId)
- Store in HttpContext.Items["Tenant"], ["TenantContext"]

Security event logged with ILogger.LogCritical.

## 5. DbContext Global Query Filter via Reflection - Not Filter by Filter

`LearnCloudDbContext` - OnModelCreating iterates `modelBuilder.Model.GetEntityTypes()` where clrType implements ITenantEntity && BaseEntity:

```csharp
foreach(var entityType in modelBuilder.Model.GetEntityTypes()){
  if(typeof(ITenantEntity).IsAssignableFrom(clrType) && typeof(BaseEntity).IsAssignableFrom(clrType)){
    var method = GetType().GetMethod(nameof(GetTenantFilter)).MakeGenericMethod(clrType);
    var filter = method.Invoke(this,null); // Expression<Func<TEntity,bool>> e=>!e.IsDeleted && (IsExplicitNoTenant||e.TenantId==CurrentTenantId)
    modelBuilder.Entity(clrType).HasQueryFilter((LambdaExpression)filter);
  }
}
```

This ensures every new ITenantEntity automatically gets tenant_id + IsDeleted filter - cannot forget.

For BaseEntity non-tenant (Tenant, Plan, AuditLog) adds IsDeleted filter only.

## 6. Automatic TenantId Assignment + Guard

Override SaveChangesAsync:

- Added entities ITenantEntity with TenantId==0 -> auto-assign from _tenantContext.TenantId, if no context and not explicit no-tenant throw InvalidOperationException security
- Added with TenantId !=0 and context TenantId != entity TenantId and not explicit -> throw InvalidOperationException "TenantId mismatch: Possible cross-tenant reference attack - FOREIGN KEY in create request with other tenant's ID"
- Modified: prevent changing TenantId (OriginalValues vs Current), guard mismatch on update
- UpdatedAt/UpdatedBy, CreatedAt/CreatedBy set from actorUserId
- Deleted -> convert to soft delete IsDeleted=true, DeletedAt, DeletedBy

## 7. Audit Interceptor

`AuditInterceptor` CaptureAuditEntries before save:

- Iterates ChangeTracker BaseEntity Added/Modified/Deleted
- Captures entity type, id (0 for added, patched after save), action create/update/soft_delete/delete, tenant, actor, ip, user-agent, reason if explicit no-tenant, academicYearId/termId if present
- For Modified: diff Original vs Current into OldValues/NewValues JSON (skip audit timestamps)
- For Added: NewValues all props JSON

After base SaveChanges, WriteAuditsAsync writes AuditLog rows (BaseEntity not tenant-filtered for platform events). Avoids infinite loop.

## 8. Explicit No-Tenant Mechanism

`INoTenantOperation` + `NoTenantOperation`:

- Requires reason >=10 chars, actorUserId, actorRole must be in allowed set [PLATFORM_SUPERADMIN, SYSTEM_JOB, MIGRATION]
- Logs critical + writes AuditLog Action=explicit_no_tenant_begin with reason
- Returns IDisposable scope that sets IsExplicitNoTenant=true in TenantContext via BeginNoTenantScope, push stack
- Global filter: `e => IsExplicitNoTenant || e.TenantId==CurrentTenantId` - when explicit, filter bypasses (allows all) but must use IgnoreQueryFilters() for full scan? Actually filter allows all when IsExplicitNoTenant true due to OR
- Usage:

```csharp
using var scope = noTenantOp.BeginScope("Nightly billing - meter all", actorUserId:1, role:"SYSTEM_JOB");
var tenants = await db.Tenants.ToListAsync(); // works
foreach(var tenant in tenants){
  using var tenantScope = tenantContext.BeginTenantScope(tenant.Id);
  var count = await db.Students.CountAsync(); // filtered to that tenant
}
```

Requires privileged role, audited, explicit.

## 9. Integration Test Suite - Proves Isolation

File `tests/MultiTenancyIsolationTests.cs` - seeds two tenants Petra and Hillcrest with overlapping data same StudentNumber 2026-0001 same InvoiceNumber INV-2026-0001 same names Thabo Ndlovu

Tests (all run in CI):

- **Tenant_A_Cannot_Read_Tenant_B_By_Guessing_ID**: A queries Students.FirstOrDefault(s=>s.Id==studentBId) => null due to global filter (404 equivalent)
- **Filtering_Sorting_Searching**: Filter by StudentNumber overlapping => only 1 result own tenant, searching Thabo => single, sorting => single, invoices same number => single
- **Cannot_Update**: Attempt to load B's student via IgnoreQueryFilters then SaveChanges with modified name -> throws TenantId mismatch, own update succeeds
- **Cannot_Delete**: FirstOrDefault for B returns null, cannot delete, attacker creating Student with TenantId=tenantBId while context=tenantAId throws mismatch, own delete soft-deletes IsDeleted=true
- **Cannot_Reference_FK_In_Create**: Create AttendanceRecord with StudentId = B's student id - service layer checks Student exists via filtered query => null => blocked, valid own succeeds
- **Subdomain_Token_Mismatch_403_Security_Event**: Simulate token tid A subdomain B mismatch, middleware would return 403 and write AuditLog security_violation reason subdomain_token_mismatch
- **Explicit_NoTenant_Requires_Privileged_Role_And_Audited**: TEACHER role throws Unauthorized, SYSTEM_JOB succeeds, audit log explicit_no_tenant_begin written
- **Every_Module_Isolated** Theory: Students, Guardians, Grades, Streams, AttendanceRecords, FeeInvoices, Assessments, StudentMarks, Messages, TimetableSlots, FeeStructures - each count per tenant =1, global count with IgnoreQueryFilters =2, proving isolation via filter

CI: `.github/workflows/ci.yml` runs MySQL service, migrations, runs `dotnet test --filter MultiTenancy`, checks tenant_id leading indexes, forbids IgnoreQueryFilters outside explicit scope.

## 10. Migration Path to Dedicated DB

See `MIGRATION_PATH_SEPARATE_DB.md` - exact steps:

- Introduce ITenantConnectionResolver, per-tenant connection string encrypted in Tenant table
- Factory for DbContext per tenant
- Zero-downtime export TenantId=42 data via explicit no-tenant scope, import into dedicated DB learncloud_tenant_petra
- Update resolver IsDedicated=true, next request goes to dedicated
- Keep TenantId column even in dedicated (constant) for code uniformity
- Background jobs loop over dedicated DBs
- Rollback via IsDedicated=false

Freeze after acceptance - this layer must not be bypassed.

## Files

- Entities/Abstractions.cs, Tenant.cs, DomainEntities.cs
- Context/ITenantContext.cs, TenantContext.cs, LearnCloudDbContext.cs
- Middleware/TenantResolutionMiddleware.cs
- Interceptors/AuditInterceptor.cs
- Security/NoTenantScope.cs
- Extensions/MultiTenancyExtensions.cs
- tests/MultiTenancyIsolationTests.cs
- .github/workflows/ci.yml
- MIGRATION_PATH_SEPARATE_DB.md
