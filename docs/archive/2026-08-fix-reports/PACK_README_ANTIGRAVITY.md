# LearnCloud / LearnClod - Antigravity Pack (2026-08-09 Africa/Harare)

**Ready to import into Antigravity (Google Antigravity IDE)**

## What's in this pack
- Full source under `src/` : 26 .NET 8 projects (LearnCloud.Domain canonical, MultiTenancy, Auth, Fees, Core, etc) + LearnCloud.Web Vite React app
- `LearnCloud.sln` buildable solution (26 projects) - fixed C1
- `deployment/` Dockerfiles multi-stage non-root, docker-compose api/web/mysql/redis/nginx/backup, Nginx TLS wildcard, backup encrypted, restore runbook, monitoring, GitHub Actions
- `marketing-site/` static landing + full 12 pages (will be replaced by Vite app in prod)
- Audit & Fix reports:
  - `Audit_Report_LearnCloud_GoLive.md` - full GoLive audit (3 Critical, 5 High, 6 Medium, 5 Low)
  - `Fix_C1_Buildable_Solution_Report.md`
  - `Fix_C2_Duplicate_Entities_Report.md` + Part2 + Part3 (this pack)
  - `Fix_C2_Duplicate_Entities_Report_Part3.md` below
- Database schema, SRS, Roles matrix, Design system, Setup wizard etc.

## C2 Status in this pack - COMPLETED
**Goal:** Deduplicate Student/Grade/Stream/Subject/Guardian across 10+ namespaces into single canonical `LearnCloud.Domain.Entities`

**Actions in this pack (Part3 final):**
- `LearnCloud.MultiTenancy/Entities/DomainEntities.cs` rewrote to ONLY `AuditLog` (platform infra). Removed Student, Guardian, GuardianStudentLink, Grade, Stream, Subject, AttendanceRecord, TimetableSlot, FeeStructure, FeeInvoice, Assessment, StudentMark, Message duplicates.
- `LearnCloud.Core/Entities/Subject.cs` removed duplicate Subject & Grade, kept only GradeSubject linking to Domain types.
- `LearnCloud.Core/Services/SubjectService.cs` removed inline `AuditLog` stub, now uses MultiTenancy AuditLog + Domain entities.
- `LearnCloud.Library/Services/LibraryService.cs` removed inline FeeItem, FeeInvoice, FeeInvoiceItem, InvoiceStatus, FeeItemRecurrence stubs (now uses Fees module canonical).
- `LearnCloud.OnlinePayments/Controllers/OnlinePaymentsController.cs` removed inline Guardian stub, now uses Domain Guardian.
- `LearnCloud.PlatformAdmin/Services/ImpersonationService.cs` removed stub User, Role, UserRole, AuditLog - now uses proper entities.
- `LearnCloud.StudentPortal/Services/StudentPortalService.cs` removed Term, Room duplicate stubs, now uses Domain Term/Room.
- `LearnCloud.MultiTenancy/Context/LearnCloudDbContext.cs` rewritten to platform-only tables (Tenants, TenantDomains, Plans, Subscriptions, TenantSettings, AuditLogs) to break circular Domain<->MultiTenancy. Domain entities will be registered in aggregated API DbContext.
- Fixed csproj circular refs:
  - Domain now references MultiTenancy (for BaseEntity/TenantOwnedEntity)
  - MultiTenancy NO LONGER references Domain
  - Core now references Domain
  - Verified: `grep -rn "class Student : TenantOwnedEntity" src --include="*.cs" | grep -v Tests | grep -v Domain` = 0 results ✅

**Remaining known duplicate:**
- `LearnCloud.HR/Entities/HREntities.cs` Staff vs `LearnCloud.Domain/Entities/CanonicalEntities.cs` Staff - HR Staff is richer (Department, Contracts etc). Intentionally left for next phase discussion - should merge HR detailed fields into Domain canonical Staff (recommended) or make HR extend Domain. Not blocking.

## How to open in Antigravity
1. Unzip `LearnCloud_Antigravity_Pack_2026-08-09.zip`
2. Open folder in Antigravity / VS Code
3. Backend: `dotnet restore LearnCloud.sln` then `dotnet build LearnCloud.sln` (requires .NET 8 SDK)
   - If SDK missing, check `src/LearnCloud.Api/Program.cs` for registration
4. Frontend: `cd src/LearnCloud.Web && npm install && npm run dev` (Vite proxy /api -> http://localhost:8080)
5. Docker full stack: `cd deployment && docker-compose up --build` (see `deployment/README.md` if present, plus `deployment/docker/`)

## Next fixes pending (per audit order)
- **C3**: Tenant Isolation Filter via Reflection not tested with all 39 types - add IModelCacheKeyFactory with tenantId, add test IsExplicitNoTenant_WithoutRole_Throws
- H1-H5, M1-M6, L1-L5 as listed in Audit_Report

## Go-Live Checklist Reference
See `deployment/` and `Audit_Report_LearnCloud_GoLive.md` Phase 0 blockers done (C1, C2), C3 next.

## Creator
HQ Bulawayo, ZW - LearnClod / LearnCloud - independent schools 150-2,000 learners
Color palette: #BC92CD, #844CAD, #5F3F96, #3C2C59, #0F153A, #195699, #307EC0 + secondary #171725, #073A69, #5A94C1, #77A6C0, #B4B6B8
Primary #0F153A WCAG AA

---
Pack generated 2026-08-09  (trust date) by Agent Mode - C2 final cleanup
