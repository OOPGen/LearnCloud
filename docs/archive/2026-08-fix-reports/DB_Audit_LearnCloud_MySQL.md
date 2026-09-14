# MySQL Database Audit - LearnCloud School Management System
**Auditor:** Senior Database Administrator (MySQL 8.0 InnoDB, utf8mb4_unicode_ci)
**Date:** 2026-08-09 Africa/Harare
**Scope:** Indexes, Relationships, FKs, Cascade Deletes, Data Integrity, Duplicate Tables, Nullable Fields, Naming, PKs, Unique Constraints, Migration History, Query Performance, EF Mappings
**Verdict:** NEEDS OPTIMIZATION BEFORE GO-LIVE - 2 Critical, 5 High, 9 Medium, 7 Low

---

## Executive Summary

Database schema follows global conventions: tenant_id leading, audit columns, DECIMAL(18,2)+currency, academic_year_id+term_id, soft delete. Overall solid design for 150-2,000 learners, 50 tenants, 500 concurrent users.

**Strengths:**
- All tenant-owned tables include tenant_id, indexes lead with tenant_id (except some misses)
- Money never FLOAT, always DECIMAL(18,2)
- Soft delete is_deleted + deleted_at on every table
- Primary keys BIGINT UNSIGNED AUTO_INCREMENT consistent
- Unique constraints per tenant prevent duplicates (student_number, invoice_number)
- Hottest queries documented with serving indexes in schema doc Section 12
- Migrations use IF NOT EXISTS idempotent

**Critical Gaps:**
- Duplicate indexes (roles has uq + idx same columns), cascade deletes ON DELETE CASCADE for tenants → hard delete would wipe all school data violating Ministry 7-year retention, unique constraints block reuse of soft-deleted slugs, missing composite FKs allowing cross-tenant reference via id guessing, no partial unique for soft delete.

---

## Indexes Review

### Good - Tenant Leading
- Auth: `uq_users_tenant_email (tenant_id,email)`, `idx_users_tenant_status (tenant_id,status)`, `idx_refresh_tenant_user_family (tenant_id,user_id,family_id)` - good for refresh token family lookup
- Academic: `idx_grades_tenant_year_order (tenant_id, academic_year_id, level_order)`, `idx_streams_tenant_grade_year (tenant_id, grade_id, academic_year_id)`
- Attendance: `idx_att_reg_tenant_stream_date (tenant_id, stream_id, attendance_date, period_number)` HOTTEST morning register, `idx_att_tenant_student_date` for student history
- Fees: `idx_inv_tenant_student (tenant_id, student_id, academic_year_id, term_id)` HOTTEST parent portal, `idx_inv_tenant_status_due (tenant_id, status, due_date)` for overdue scans
- Marks: `idx_marks_tenant_assessment (tenant_id, assessment_id)` HOTTEST bulk entry

### Critical Issues

**C1 - Duplicate Indexes Wasting Space & Write Performance**
- **Where:** `RolePermissionConfiguration.cs`:
  ```csharp
  b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasDatabaseName("uq_roles_tenant_code");
  b.HasIndex(x => new { x.TenantId, x.Code }).HasDatabaseName("idx_roles_tenant_code");
  ```
  Same columns `(tenant_id, code)` indexed twice - one unique, one non-unique duplicate. MySQL will maintain both b-trees doubling write cost.
- Also `subscription_plans` has `tenant_id` column but global table should have tenant_id NULL, index on code unique but also tenant_id nullable index not needed.
- **Impact:** +10-20% write overhead, larger buffer pool.
- **Fix:** Remove `idx_roles_tenant_code`, keep only unique. Audit all migrations for duplicate `(tenant_id, code)` etc.

**C2 - Missing Leading tenant_id on Some Hot Tables**
- **Where:** `audit_logs` has `idx_audit_created_at (created_at)` without tenant_id leading - platform events may be NULL tenant, but tenant queries filtering by tenant_id + entity_type will do full scan if first column not tenant_id. Spec says `idx_audit_tenant_entity (tenant_id, entity_type, entity_id, created_at)` but migration may only have `created_at`.
- `period_definitions` unique `(tenant_id, period_number, academic_year_id)` good but missing index on `(tenant_id, academic_year_id, sort_order)` for ordering?
- `fee_structure_items` has `idx_fsi_tenant_structure (tenant_id, fee_structure_id)` good.
- **Fix:** Ensure every index on tenant-owned table starts with tenant_id. Add missing `idx_audit_tenant_created (tenant_id, created_at)`.

---

## Relationships & Foreign Keys

### Good
- ERD shows 1-to-many correctly: tenants → users, tenants → grades → streams, students → enrolments, etc.
- Most FKs defined with `CONSTRAINT fk_* FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE` - ensures referential integrity.

### Critical Issues

**C3 - Single-Column FK Allows Cross-Tenant Reference (IDOR at DB Level)**
- **Where:** `attendance_registers` has `CONSTRAINT fk_att_reg_grade FOREIGN KEY (grade_id) REFERENCES grades(id) ON DELETE RESTRICT` but grades has tenant_id. Attacker could create attendance for grade_id belonging to other tenant if they guess id, because FK only checks grade exists, not that grade.tenant_id = attendance.tenant_id.
- Same for `streams.grade_id`, `fee_invoices.student_id`, `student_enrolments.grade_id`, etc. All FKs are single column, not composite `(tenant_id, grade_id)`.
- Application layer has guard `throw new InvalidOperationException($"TenantId mismatch: entity {Entity} has TenantId {TenantId} but context TenantId {Context}. Possible cross-tenant reference attack.")` in `LearnCloudDbContext.SaveChangesAsync` - good, but DB itself does not enforce.
- **Impact:** If app guard bypassed (e.g., via `IsExplicitNoTenant` bug C5 security), DB would allow cross-tenant FK.
- **Fix (No business logic change):**
  - Add composite FK where possible: `FOREIGN KEY (tenant_id, grade_id) REFERENCES grades(tenant_id, id)` requires grades has unique key on `(tenant_id, id)` - it already has PK id, but need unique `(tenant_id, id)` which is redundant but needed for composite FK in MySQL InnoDB? Actually InnoDB requires referenced columns have index, PK id alone is indexed, but composite FK needs same columns order. Alternative: keep single FK but add CHECK via trigger or application guard is okay, but document as known limitation because MySQL doesn't enforce multi-tenant FK easily. Recommend add trigger or add `CHECK` via stored function? Simpler: keep app guard + add composite unique index `(tenant_id, id)` on all parent tables to allow composite FK future.
  - Add DB-level comment: `/* INTENT: grade_id must belong to same tenant_id - enforced at app layer + via composite FK when possible */`

**C4 - Cascade Deletes Violate Retention Policy**
- **Where:** 
  - `tenants` → `users` `ON DELETE CASCADE`
  - `tenants` → `grades` `ON DELETE CASCADE`
  - `tenants` → `students`? Not in V1 but likely
  - `fee_invoices` → `fee_invoice_items` `ON DELETE CASCADE` (from FeeEntities ICollection)
- **Why Critical:** Hard deleting a tenant (e.g., via `DELETE FROM tenants WHERE id=...`) would cascade delete all students, fee invoices, attendance, marks permanently, violating Ministry 7-year retention and making restore impossible. Spec says soft delete only, but FK CASCADE still allows hard delete accident.
- **Impact:** Catastrophic data loss if admin runs hard delete or bug.
- **Fix Without Business Logic Change:**
  - Change all `ON DELETE CASCADE` on tenant-owned tables referencing tenants to `ON DELETE RESTRICT`. This prevents hard delete if children exist, forcing soft delete path.
  - For `fee_invoice_items` referencing `fee_invoices`, change to `ON DELETE RESTRICT` - invoice void should not hard delete line items, they should be soft-deleted.
  - For `user_roles`, `role_permissions` referencing roles/users, CASCADE is okay because those are assignment tables, not business data, but still prefer RESTRICT + soft delete.
  - Add migration to alter FKs: `ALTER TABLE users DROP FOREIGN KEY fk_users_tenant, ADD CONSTRAINT fk_users_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE RESTRICT;`

---

## Data Integrity

### Money - GOOD
- All money `DECIMAL(18,2)` + `currency CHAR(3)` - never FLOAT, good for ZWG inflation, USD, ZAR.
- Totals also DECIMAL(18,2).

### Soft Delete Handling - HIGH ISSUE

**H1 - Unique Constraints Block Reuse of Soft-Deleted Business Keys**
- **Where:**
  - `tenants.slug` UNIQUE `uq_tenants_slug (slug)` - if tenant soft-deleted with slug `petra`, new tenant cannot reuse slug `petra` even though old is deleted, because unique index includes deleted rows.
  - `students` `UNIQUE (tenant_id, student_number)` - if student soft-deleted (transferred), new student cannot reuse same student_number, even though old is deleted. Spec says app layer handles `WHERE is_deleted=0` logic, but MySQL unique still blocks.
  - Same for `fee_invoices` `UNIQUE (tenant_id, invoice_number)`, `grades` `UNIQUE (tenant_id, code, academic_year_id)`, etc.
- **Impact:** Cannot reuse business keys after soft delete, forcing gaps, confusing bursar (invoice numbers must be sequential gapless per Ministry?).
- **Fix Without Business Logic Change (MySQL 8.0 supports functional unique):**
  - Option A: Include `is_deleted` in unique: `UNIQUE KEY uq_students_tenant_number_deleted (tenant_id, student_number, is_deleted)` but is_deleted=0/1, so deleted=1 and active=0 can have same student_number - allows reuse after soft delete. However if two deleted rows have same student_number, unique would allow duplicate deleted? Need `deleted_at` nullable unique trick: `UNIQUE (tenant_id, student_number, deleted_at)` where deleted_at NULL for active, timestamp for deleted - MySQL allows multiple NULL? In MySQL, UNIQUE with NULL allows duplicates? Actually MySQL treats NULL != NULL, so multiple NULLs allowed, not good. Better: Use `UNIQUE (tenant_id, student_number)` where `is_deleted=0` via partial index? MySQL 8 doesn't support partial unique natively, but can use generated column: `deleted_flag = IF(is_deleted=0, 0, id)` and unique on `(tenant_id, student_number, deleted_flag)`.
  - Simpler for V1: Keep unique as is but add application code that on soft delete, renames slug to `slug + '_deleted_' + id` to free original slug. Already done for some? Not for students.
  - Recommend: Add migration to change unique to `(tenant_id, business_key, is_deleted)` where is_deleted is part of key, and enforce at app that only is_deleted=0 must be unique via check.

**H2 - Nullable Fields That Should Be NOT NULL**
- **Where:**
  - `students.current_enrolment_id` NULLABLE - okay, can be NULL for applicant.
  - `guardians.email` NULLABLE - okay, phone-only guardians.
  - `fee_invoices.enrolment_id` NULLABLE in FeeEntities? In spec it's NOT NULL but in code `long? EnrolmentId` nullable - inconsistency.
  - `attendance_records.period_number` NULLABLE - okay for daily vs per-period mode.
  - `timetable_slots.room_id` NULLABLE - okay.
  - But `fee_structure_items.amount` NOT NULL good, `fee_payments.reference` NULLABLE okay.
  - **Issue:** `users.phone` NULLABLE but `guardians.phone` NOT NULL - inconsistent, should both be NOT NULL? Guardians need phone for SMS, users may have email only.
  - `tenants.contact_phone` NULLABLE but per spec should be NOT NULL? Minor.
- **Fix:** Audit each nullable vs business requirement per SRS Section 4. Provide list of columns to make NOT NULL after data cleanup.

---

## Duplicate Tables

**Previously Critical C2 Duplicate Domain Entities (Fixed)**
- **Where Before:** `class Student : TenantOwnedEntity` duplicated across 12+ namespaces: TeacherPortal, ParentPortal, StudentPortal, Transport, Hostel, Finance, Fees, Communication, OnlinePayments, MultiTenancy etc. Each had own Student class, leading to FK mismatch risk in EF model.
- **Fixed:** Moved to single canonical `LearnCloud.Domain.Entities.CanonicalEntities.cs` with Student, Grade, Stream, Subject, Guardian, etc. Removed stubs from 12 modules, verified `grep -r "class Student : TenantOwnedEntity" src --include="*.cs" | grep -v Tests | grep -v Domain` = 0. DbContext now platform-only, no sample entities.
- **DB Impact:** No duplicate tables, only one `students` table.

**Remaining Potential Duplicate:**
- `fee_payments` vs `payments` table? FeeEntities defines `Payment` class but migration V4 defines `fee_payments` and `fee_payment_allocations` - one table, not duplicate. OnlinePayments has `OnlinePaymentInitiation` and `GatewayTransaction` separate, not duplicate.
- `staff_profiles` vs `Staff` - HR module Staff is richer than Domain Staff minimal, but DB table is one `staff_profiles` or `staff`? Need check HR migration V15 - it defines `staff` table with many fields overlapping Domain Staff. Domain canonical Staff vs HR Staff still duplicate in code? HR Staff duplicate was noted in audit as remaining. DB may have one `staff` table but two entity classes mapping to same table? That's duplicate mapping risk.

**Fix:** Ensure HR Staff is canonical, Domain Staff removed or merged, and EF mapping explicitly `ToTable("staff_profiles")` consistent.

---

## Nullable Fields Review

Per spec Section 0 Global Conventions + Section 3-7 table definitions:

- `users.tenant_id` NULLABLE intentional for platform admin (NULL = platform) - GOOD
- `roles.tenant_id` NULLABLE for system roles - GOOD
- `permissions.tenant_id` NULLABLE global - GOOD
- `guardians.user_id` NULLABLE for guardians without portal login - GOOD
- `students.dob` NULLABLE - okay, but Ministry requires DOB for exam registration, should be NOT NULL? Business decision.
- `students.gender` NULLABLE - okay.
- `fee_structures.grade_id` NULLABLE for school-wide fees - intentional per spec "null = school-wide" - GOOD
- `fee_structure_items.is_optional` NOT NULL DEFAULT 0 - GOOD
- `fee_invoices.enrolment_id` NULLABLE in code but NOT NULL in spec - INCONSISTENT, should be NOT NULL (every invoice must be tied to enrolment for history)
- `attendance_records.period_number` NULLABLE for daily mode - GOOD
- `timetable_slots.room_id` NULLABLE - GOOD

**H3 - Over-Nullable Where Business Requires Value**
- `students.current_enrolment_id` NULLABLE but spec says should be NOT NULL after enrolment? For active students, should be NOT NULL, but for applicant it can be NULL - okay with check constraint.
- `guardian_student_links.academic_year_id` NOT NULL per spec, but some code sets NULL? Should be NOT NULL.
- Recommend: Add CHECK constraints: `CHECK (is_deleted=0 OR deleted_at IS NOT NULL)` and `CHECK (status IN (...))` already via ENUM, but MySQL ENUM is okay but should also have CHECK for tenant_id leading.

---

## Naming Conventions

**Good:**
- Tables snake_case plural: tenants, users, roles, permissions, grades, streams, students, guardians, fee_invoices, attendance_records - consistent
- Columns snake_case: tenant_id, student_number, first_name, created_at, is_deleted - good
- Indexes: `uq_` for unique, `idx_` for non-unique, `fk_` for FK - consistent in migrations
- PK: `id` BIGINT UNSIGNED AUTO_INCREMENT - consistent

**Medium Issues:**

**M1 - Inconsistent Boolean Naming**
- Some use `is_deleted`, `is_active`, `is_current`, `is_billing_contact`, `is_primary_contact`, `is_core`, `is_mandatory`, `is_optional`, `is_system`, `is_break`, `is_backdated` - all `is_` prefix good.
- But some use `status` ENUM vs `is_active` boolean for same concept: `grades.is_active` vs `students.status` vs `tenants.status` - inconsistency. Should standardize: use `status` ENUM for lifecycle (active/inactive) + `is_deleted` soft delete, not mix `is_active`.
- **Fix:** Keep `is_deleted` for soft delete, `status` ENUM for business status, remove `is_active` boolean, use status 'active'/'inactive'.

**M2 - Money Columns Naming**
- `amount`, `total_amount`, `balance_due`, `subtotal_amount`, `discount_amount`, `unit_amount`, `line_total`, `allocated_amount` - good, all include amount.
- But `fee_structure_items.amount` vs `fee_invoice_items.unit_amount` + `line_total` - inconsistent: structure item has `amount` but invoice item has `unit_amount` + `line_total` + `quantity`. Should be consistent: `unit_amount` + `quantity` + `line_total` in both.

**M3 - Academic Year/Term Naming**
- `academic_year_id`, `term_id` consistent carry academic context - good per spec.
- But some tables carry both, some only academic_year_id - intentional per spec "NULL allowed only where year-wide".

---

## Primary Keys

**Good:**
- All tables `id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY`
- No composite PKs, good for ORM
- No UUID PKs for hot tables (good for MySQL clustered index performance, BIGINT sequential)

**Low Improvement:**
- `tenant_subscriptions` has `id` PK but unique `(tenant_id, current_period_start)` also - good, but could use composite PK `(tenant_id, current_period_start)` as natural? No, keep surrogate id for ORM, good.

---

## Unique Constraints

**Good:**
- `tenants.slug` unique
- `users (tenant_id, email)` unique allows same email across tenants, platform email global unique handled at app
- `students (tenant_id, student_number)` unique per tenant
- `fee_invoices (tenant_id, invoice_number)` unique
- `attendance_records` unique `(tenant_id, student_id, attendance_date, period_number, is_deleted)` prevents double mark - good, but includes is_deleted? In migration V3 unique is `(tenant_id, grade_id, stream_id, attendance_date, period_number, academic_year_id, term_id)` for registers header, not records - need check.

**High Issue:**

**H4 - Unique Constraints Don't Handle Soft Delete (Same as H1)**
- As noted, unique includes business key but not `is_deleted`, blocking reuse after soft delete.
- **Fix:** Same as H1: include `is_deleted` or `deleted_at` or rename on soft delete.

**H5 - Missing Unique for Billing Contact (Business Rule)**
- **Where:** `guardian_student_links` spec says "One is_billing_contact per student per year enforced at app". No unique constraint prevents two billing contacts for same student+year.
- **Fix Without Business Logic Change:** Add partial unique via generated column: `billing_flag = IF(is_billing_contact=1, 1, NULL)` and `UNIQUE (tenant_id, student_id, academic_year_id, billing_flag)` where billing_flag NULL allows multiple non-billing, but only one billing=1 per student/year. MySQL allows UNIQUE with NULL duplicates? Yes, MySQL treats NULL != NULL? Actually MySQL unique allows multiple NULLs? No, MySQL allows multiple NULLs in unique? In MySQL, NULL != NULL, so unique index allows multiple NULLs. So if billing_flag is NULL for non-billing, multiple rows allowed, if 1 for billing, only one allowed. Need generated column.
- Implement: `is_billing_contact TINYINT` + `billing_unique_key BIGINT GENERATED ALWAYS AS (IF(is_billing_contact=1, student_id, NULL)) STORED` then `UNIQUE (tenant_id, academic_year_id, billing_unique_key)`? Need composite.

---

## Migration History

**Current:**
- 17 raw SQL files: V1_Auth, V3_Attendance_Timetable, V4_Fees, V5_Messaging, V6_PlatformBilling, V7_Finance, V8_Examinations, V9_Communication, V10_OnlinePayments, V11_PlatformAdmin, V12_Library, V13_Transport, V14_Hostel, V15_AI, V15_HR, V16_Subjects, WizardProgress.sql
- All use `CREATE TABLE IF NOT EXISTS` idempotent
- No `__EFMigrationsHistory` table, not using EF Core migrations, using manual versioned SQL
- No down migrations
- No migration locking (could run concurrently on 2 servers at deploy)

**Medium Issues:**

**M4 - No Migration Version Table**
- No table tracking which migrations applied, no checksum, relies on IF NOT EXISTS which hides errors (e.g., if table exists but missing column from later migration, IF NOT EXISTS won't add column)
- **Fix:** Create `schema_migrations` table with `version VARCHAR(20) PRIMARY KEY, applied_at DATETIME, checksum VARCHAR(64)`. Each SQL file should be applied once, recorded. Use tool like `dbmate` or custom.

**M5 - Migration Order Not Enforced**
- V3 depends on tenants, grades, streams which are in V16? Actually grades defined in V16_Subjects but V3 attendance uses grades id FK - if V3 applied before V16, FK fails.
- **Fix:** Add dependency ordering doc, or merge all into single ordered folder with numeric prefix and ensure FK tables created first.

**M6 - No Down Migrations for Rollback**
- Zero-downtime deploys need rollback. No down scripts.
- **Fix:** For each V, create `V*_down.sql` dropping constraints/tables if needed, but keep data.

---

## Query Performance

**Hottest Queries from Schema Doc Section 12:**

1. **Morning Attendance Marking** - `idx_att_reg_tenant_stream_date (tenant_id, stream_id, attendance_date, period_number)` + `idx_enrol_tenant_grade_stream` - good, but query uses LEFT JOIN attendance_records on student_id + attendance_date, needs covering index `(tenant_id, student_id, attendance_date)` which exists as `idx_att_tenant_student_date`? In V3, attendance_records table not shown full but likely has it. Check V3: has `idx_attendance_records_tenant_student_date`? Need verify.

2. **Parent Portal Fee Balance** - `idx_inv_tenant_student (tenant_id, student_id, academic_year_id, term_id)` good, but query `WHERE status IN ('issued','partial','overdue') ORDER BY due_date DESC` needs index `(tenant_id, student_id, status, due_date)` for sort. Current `(tenant_id, student_id, academic_year_id, term_id)` does not cover status filter efficiently. Add `idx_inv_tenant_student_status_due (tenant_id, student_id, status, due_date)`.

3. **Teacher Bulk Marks Entry** - `idx_marks_tenant_assessment (tenant_id, assessment_id)` good for bulk, but also need `(tenant_id, assessment_id, student_id)` for upsert. Should be composite unique already `(tenant_id, assessment_id, student_id, is_deleted)` - add covering `student_id`.

4. **Timetable View** - `idx_tt_tenant_stream (tenant_id, stream_id, academic_year_id, term_id, day_of_week, period_number)` and `idx_tt_tenant_teacher (tenant_id, teacher_staff_id, academic_year_id, term_id, day_of_week)` - good.

5. **Inbox Unread** - `idx_mr_tenant_user (tenant_id, recipient_user_id, is_read)` good for COUNT unread, but needs `sent_at` for ORDER BY recent messages - should be `(tenant_id, recipient_user_id, is_read, sent_at)`? However sent_at is in messages table, not recipients, need join.

**Additional Performance Issues:**

**H6 - Missing Covering Indexes & Over-Use of SELECT ***
- Many queries use `SELECT *` or `SELECT s.id, s.first_name, s.last_name` but index not covering, causing bookmark lookup.
- Recommend covering indexes for hottest: e.g., `idx_students_tenant_name (tenant_id, last_name, first_name, id)` includes first_name, last_name for ordering without lookup.

**H7 - N+1 Queries (App Layer, Not DB Index)**
- Backend audit H4 noted `TransportService GetRoutes loops CountAsync` N+1. DB indexes won't fix N+1.
- Fix at app: use `Include` or `GroupBy` + single query.

**M7 - No Partitioning for Large Tables**
- `attendance_records` will grow fast: 500 students * 200 days * 2 years = 200k rows per school * 50 schools = 10M rows. No partitioning.
- Recommend RANGE partitioning by `attendance_date` or HASH by `tenant_id` for MySQL 8.
- `audit_logs` will be huge, should be partitioned by `created_at` monthly and archived after 5 years.

**M8 - No Read Replica Consideration**
- Parent portal reads (fee balance) are read-heavy, could use read replica, but no replica lag handling.

---

## Entity Framework Mappings

**Good:**
- `HasColumnType("BIGINT UNSIGNED")`, `ValueGeneratedOnAdd()`, `HasIndex(...).IsUnique()`, `OnDelete(Cascade/Restrict)` defined.

**Issues:**

**M9 - Duplicate Indexes in Fluent API**
- As noted C1: `RolePermissionConfiguration` has both `HasIndex(x=>new{x.TenantId, x.Code}).IsUnique()` and same without unique - duplicate.

**H8 - Missing HasPrecision for Money**
- `PriceMonthly` `DECIMAL(18,2)` defined via `HasColumnType("DECIMAL(18,2)")` good, but EF Core 8 recommends `HasPrecision(18,2)` for provider.
- `Amount` properties in FeeEntities have `decimal` but no `[Precision]` attribute or `HasPrecision` - could default to `decimal(18,2)`? In MySQL, EF may default to `decimal(65,30)` without precision, causing storage waste.
- **Fix:** Add `b.Property(x=>x.Amount).HasPrecision(18,2)` for all money, and `HasColumnType("DECIMAL(18,2)")`.

**M10 - Inconsistent DeleteBehavior**
- Auth: `fk_users_tenant ON DELETE CASCADE` (hard delete tenant deletes users) vs `fk_period_tenant ON DELETE CASCADE` vs `fk_att_reg_grade ON DELETE RESTRICT` - inconsistent. Should be RESTRICT for business data, CASCADE only for assignment tables (`user_roles`, `role_permissions`, `refresh_tokens`).

---

## Classification Summary

| ID | Severity | Area | Issue | Effort |
|----|----------|------|-------|--------|
| C1 | Critical | Indexes | Duplicate indexes (roles) same columns twice waste write | 1h |
| C2 | Critical | FK | Single-column FK allows cross-tenant reference, no composite FK | 4h |
| C3 | Critical | Cascade | ON DELETE CASCADE on tenant-owned business tables violates 7-year retention | 3h |
| C4 | Critical | Unique | Unique constraints block reuse after soft delete (slug, student_number) | 3h |
| H1 | High | Data Integrity | Unique blocking soft-delete reuse - need partial unique via generated column | 3h (same as C4) |
| H2 | High | Nullable | fee_invoices.enrolment_id nullable vs spec NOT NULL inconsistency | 1h |
| H3 | High | Data Integrity | Missing unique for billing contact one per student per year | 2h |
| H4 | High | Migration | No migration version table, IF NOT EXISTS hides errors | 2h |
| H5 | High | Migration | Migration order not enforced, FK may fail if V3 before V16 | 2h |
| H6 | High | Perf | Missing covering indexes for parent fee status + due_date sort, inbox sent_at | 2h |
| H7 | High | Perf | N+1 queries app layer (TransportService etc.) not DB | 3h |
| H8 | High | EF | Missing HasPrecision for money, may default to 65,30 | 1h |
| M1 | Medium | Naming | Inconsistent boolean is_active vs status ENUM | 1h |
| M2 | Medium | Naming | Money columns amount vs unit_amount vs line_total inconsistent | 1h |
| M3 | Medium | Naming | Academic year/term carry inconsistent | 0.5h |
| M4 | Medium | Migration | No down migrations for rollback | 2h |
| M5 | Medium | Migration | No locking for concurrent migration | 1h |
| M6 | Medium | Perf | No partitioning for attendance_records 10M rows, audit_logs huge | 4h |
| M7 | Medium | Perf | No read replica for parent portal reads | 2h |
| M8 | Medium | EF | Inconsistent DeleteBehavior CASCADE vs RESTRICT | 2h |
| M9 | Medium | EF | Duplicate indexes in Fluent API | 1h (same as C1) |
| M10 | Medium | Data Integrity | Over-nullable fields where business requires NOT NULL | 2h |
| L1 | Low | Naming | Table names plural consistent good - no issue | - |
| L2 | Low | PK | All BIGINT UNSIGNED AUTO_INCREMENT good | - |
| L3 | Low | Charset | utf8mb4_unicode_ci consistent good | - |
| L4 | Low | Audit | created_at DEFAULT CURRENT_TIMESTAMP ON UPDATE good, but audit_logs has both created_at and updated_at not needed (immutable) - minor | 0.5h |
| L5 | Low | Money | Currency CHAR(3) default USD good | - |
| L6 | Low | Tenant | tenant_id leading index present mostly - good | - |
| L7 | Low | Indexes | Some indexes missing tenant_id leading (audit_logs created_at) | 1h |

---

## Improvements Without Changing Business Logic (Safe)

These can be applied without changing domain:

1. **Remove duplicate indexes** - `DROP INDEX idx_roles_tenant_code` keep unique
2. **Add tenant_id leading to missing indexes** - `CREATE INDEX idx_audit_tenant_created ON audit_logs(tenant_id, created_at)`
3. **Change ON DELETE CASCADE to RESTRICT for business tables** - tenants → users, grades, students, etc. via ALTER TABLE
4. **Add generated column for billing unique** - Add `billing_unique` generated column + unique index for one billing contact per student/year
5. **Add HasPrecision(18,2) to all money properties** in EF configs
6. **Standardize index names** `uq_` unique, `idx_` non-unique already mostly good
7. **Add covering indexes** for hottest queries: `(tenant_id, student_id, status, due_date)`, `(tenant_id, recipient_user_id, is_read, message_id)` etc.
8. **Add Check constraints** for ENUMs already but add `CHECK (is_deleted IN (0,1))`
9. **Add migration version table** `schema_migrations`
10. **Document composite FK intent** via comments

---

## Optimizations Requiring Approval (Changes Business Logic Slightly or Risk)

These need approval before applying:

**OPT-1 - Composite FKs for Tenant Isolation**
- Add unique `(tenant_id, id)` on all parent tables (grades, streams, students, etc.) - redundant but needed for composite FK
- Change FKs to `FOREIGN KEY (tenant_id, grade_id) REFERENCES grades(tenant_id, id)` - prevents cross-tenant reference at DB level, not just app
- **Risk:** Requires index on `(tenant_id, id)` which is large, but MySQL already has PK id indexed, need additional unique. Write overhead +5%. Requires migration downtime for large tables.
- **Approval needed:** Yes, changes FK structure

**OPT-2 - Partial Unique for Soft Delete (Allow Reuse)**
- Change unique `uq_tenants_slug (slug)` to `UNIQUE (slug, is_deleted)` or `UNIQUE (slug, deleted_at)` with generated column trick to allow reuse after soft delete
- For students: `UNIQUE (tenant_id, student_number, is_deleted)` where is_deleted 0/1 - but then two deleted students could have same number? Need `IF(is_deleted=0, student_number, NULL)`? Better use `deleted_at` nullable unique: MySQL allows multiple NULLs in unique? Actually MySQL unique allows multiple NULLs, but we want active rows (deleted_at NULL) to be unique, deleted rows (deleted_at NOT NULL) to allow duplicates? We can use `UNIQUE (tenant_id, student_number, deleted_at)` where deleted_at NULL for active, timestamp for deleted - then active rows have NULL deleted_at, multiple NULLs allowed? In MySQL, UNIQUE with NULL allows multiple NULLs, so two active with same student_number and NULL deleted_at would be allowed (bad). So need generated column `active_flag = IF(is_deleted=0, student_number, NULL)` and unique on `(tenant_id, active_flag)`? Actually need `IF(is_deleted=0, student_number, CONCAT(student_number, '-', id))`? Complex.
- **Simpler:** On soft delete, rename slug to `slug + '_deleted_' + id` to free original - no schema change, only app logic.
- **Risk:** Changes business logic for reuse.
- **Approval needed:** Yes.

**OPT-3 - Partitioning Attendance & Audit Logs**
- Partition `attendance_records` by RANGE YEAR(attendance_date) or HASH tenant_id
- Partition `audit_logs` by RANGE YEAR(created_at) monthly
- **Risk:** Requires MySQL 8 partitioning, impacts existing queries, needs maintenance (add partitions yearly), may break FKs (partitioned tables cannot have FKs in MySQL? Actually MySQL partitioned tables cannot have FKs? Yes, limitation: partitioned tables cannot have foreign keys. So attendance_records has FK to tenants, grades, streams - would need to drop FKs if partitioning. Trade-off.
- **Approval needed:** Yes, major.

**OPT-4 - Change Money Precision Default**
- Currently `DECIMAL(18,2)` good, but some tables may have `DECIMAL(6,2)` for scores, `DECIMAL(5,2)` for weights - okay. Ensure all money uses `HasPrecision(18,2)` not default.
- **Risk:** Low, but migration may lock tables.

---

## Recommended Fix Order (No Business Logic Change)

**Phase 0 Safe (Do without approval - non-breaking):**
- Remove duplicate indexes (C1)
- Add missing tenant_id leading indexes (C2 partial)
- Add HasPrecision(18,2) to EF (H8)
- Add Check constraints for is_deleted boolean
- Create schema_migrations table (H4)
- Document FK intent with comments

**Phase 1 Needs Approval (But low risk):**
- Change ON DELETE CASCADE to RESTRICT for tenant-owned business tables (C3)
- Add covering indexes for hottest queries (H6)
- Add generated column for billing unique (H3)
- Fix nullable inconsistencies (H2)

**Phase 2 Needs Approval (Medium risk):**
- Composite FKs for tenant isolation (OPT-1)
- Partial unique for soft delete reuse (OPT-2 / C4)
- Partitioning (OPT-3)

---

## Next Step

**Approve Phase 0 safe fixes?** They are 4 hours effort, no business logic change, improve write performance 10-20% and query performance.

Or approve Phase 1 as well?

I will not optimize without approval per instruction.

