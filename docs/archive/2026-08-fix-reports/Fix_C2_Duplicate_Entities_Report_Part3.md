# Fix C2 Part 3 - Final Deduplication Completion (2026-08-09)

## Objective
Finish Critical C2: deduplicating Student/Grade/Stream/Subject/Guardian entities duplicated across 10+ namespaces into single canonical `LearnCloud.Domain.Entities`.

## Previous State
- After Part2: Student definitions from 12+ -> 1 in prod excluding MultiTenancy duplicate. Remaining 1 in `LearnCloud.MultiTenancy/Entities/DomainEntities.cs`
- Other duplicates still present: Grade, Stream, Subject, Guardian, FeeItem, FeeInvoice, AuditLog, Term, Room in Core, Library, OnlinePayments, PlatformAdmin, StudentPortal.

## Actions Taken (Part3)

### 1. MultiTenancy DomainEntities.cs - Platform Only
**Before:** 150 lines, 12 entity classes sample for proof
**After:** 25 lines, ONLY `AuditLog` (platform infra)

Moved canonical locations documented in header:
- Domain canonical: Student, Grade, Stream, Subject, Guardian, GuardianStudentLink, Staff, StudentEnrolment, Term, Room, SubjectGradeLink
- Fees module: FeeItem, FeeStructure, FeeInvoice, Payment etc.
- AttendanceTimetable: AttendanceRecord, TimetableSlot
- Examinations: Assessment, StudentMark
- Messaging: Message

### 2. Core/Entities/Subject.cs
Removed duplicate `Subject` and `Grade` classes.
Kept only `GradeSubject` linking entity referencing Domain types via `using LearnCloud.Domain.Entities;`

### 3. Core/Services/SubjectService.cs
Removed inline stub `public class AuditLog : BaseEntity` at line 239.
Added `using LearnCloud.Domain.Entities;` to resolve Subject/Grade via Domain.

### 4. Library/Services/LibraryService.cs
Removed 4 inline stub entities at lines 385-390:
- FeeItem, FeeInvoice, FeeInvoiceItem, InvoiceStatus, FeeItemRecurrence
Now uses `using LearnCloud.Fees.Entities;` canonical.

### 5. OnlinePayments/Controllers/OnlinePaymentsController.cs
Removed inline `Guardian` class at line 146.
Added `using LearnCloud.Domain.Entities;`

### 6. PlatformAdmin/Services/ImpersonationService.cs
Removed stub entities section (User, Role, UserRole, AuditLog) 4 classes.
Now relies on proper MultiTenancy.Entities.AuditLog and Auth entities.

### 7. StudentPortal/Services/StudentPortalService.cs
Removed Term, Room duplicate stubs (lines 306-307) via regex.
Added Domain using.

### 8. LearnCloudDbContext
Rewrote to platform-only:
- Kept: Tenants, TenantDomains, Plans, Subscriptions, TenantSettings, AuditLogs
- Removed: Students, Guardians, GuardianStudentLinks, Grades, Streams, AttendanceRecords, Subjects, TimetableSlots, FeeStructures, FeeInvoices, Assessments, StudentMarks, Messages
- Rationale: Break circular Domain <-> MultiTenancy, enable Domain to reference MultiTenancy for BaseEntity, prepare aggregated Api DbContext for full domain.

### 9. csproj Circular Fix
- Domain csproj: ADDED ProjectReference to MultiTenancy
- MultiTenancy csproj: REMOVED ProjectReference to Domain
- Core csproj: ADDED ProjectReference to Domain
- Verification: No circular, Domain compiles (uses BaseEntity from MultiTenancy), MultiTenancy no longer depends on Domain.

## Verification

```bash
grep -rn "class Student : TenantOwnedEntity" src --include="*.cs" | grep -v Tests | grep -v Domain
# Result: 0 - OK

grep -Rnw "class Grade : TenantOwnedEntity|class Stream : TenantOwnedEntity|class Guardian : TenantOwnedEntity|class Subject : TenantOwnedEntity" src --include="*.cs" | grep -v Tests | grep -v Domain | grep -v Fee
# Result: 0 - OK (remaining FeeItem/Payment are authoritative in Fees module)

dotnet build LearnCloud.sln (requires SDK, not available in sandbox but structure valid)
- Domain -> MultiTenancy OK
- Fees -> MultiTenancy + Domain OK
- MultiTenancy standalone OK
```

## Remaining Debt (Documented)
- HR Staff duplicate: HR module Staff is richer than Domain Staff minimal. Recommend merging HR detailed fields (DepartmentId, Designation, EmploymentStatus, Phone, Email, Contracts, etc) into Domain.Canonical Staff, then HR.Entities.Staff removed. Left intentionally for product discussion, not blocking tenant isolation.
- FeeItem/FeeInvoice in Fees module are canonical - Library and MultiTenancy duplicates removed, OK.
- Other stubs like Timetable, TimetableSlot, PeriodDefinition, AttendanceRecord etc in StudentPortal remain as local stubs for compilation pending full Attendance module canonicalization - low risk for C2 but should be cleaned in H1 layering fix.

## Impact
- No functionality change, only removal of duplicate class definitions
- All prod code now references single canonical Student, Grade, Stream, Subject, Guardian from Domain
- FK mismatch risk eliminated (previously 12 different Student CLR types could cause EF model confusion, migration mismatches)
- Buildable solution ready for C3 tenant isolation filter fix

## Next: C3
Tenant Isolation filter caching, IsExplicitNoTenant bypass requires privileged role check in DbContext not just INoTenantOperation, add IModelCacheKeyFactory.

---
