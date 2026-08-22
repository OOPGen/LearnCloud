-- V18 Phase 2 Advanced Optimizations - Approved All Phases
-- Date: 2026-08-09
-- Includes: OPT-1 Composite FKs for tenant isolation, OPT-2 Partial unique for soft-delete reuse, OPT-3 Partitioning
-- WARNING: Requires downtime for large tables, test on staging first

-- ============================================================================
-- OPT-1: Composite FKs for Tenant Isolation - Prevent Cross-Tenant Reference at DB Level
-- ============================================================================
-- Problem: Single-column FK grade_id -> grades.id allows cross-tenant reference if id guessed
-- App guard exists in SaveChangesAsync but DB should enforce too
-- Solution: Add unique (tenant_id, id) on parent tables, then FK (tenant_id, grade_id) -> parent(tenant_id, id)

-- Add unique (tenant_id, id) on critical parent tables to allow composite FK reference
-- MySQL requires referenced columns have index, PK id alone is indexed but composite (tenant_id, id) needs its own unique

-- grades
CREATE UNIQUE INDEX IF NOT EXISTS uq_grades_tenant_id ON grades(tenant_id, id);
-- streams
CREATE UNIQUE INDEX IF NOT EXISTS uq_streams_tenant_id ON streams(tenant_id, id);
-- students
CREATE UNIQUE INDEX IF NOT EXISTS uq_students_tenant_id ON students(tenant_id, id);
-- academic_years
CREATE UNIQUE INDEX IF NOT EXISTS uq_academic_years_tenant_id ON academic_years(tenant_id, id);
-- terms
CREATE UNIQUE INDEX IF NOT EXISTS uq_terms_tenant_id ON terms(tenant_id, id);
-- subjects
CREATE UNIQUE INDEX IF NOT EXISTS uq_subjects_tenant_id ON subjects(tenant_id, id);
-- staff_profiles or staff
CREATE UNIQUE INDEX IF NOT EXISTS uq_staff_profiles_tenant_id ON staff_profiles(tenant_id, id);

-- Now add composite FKs for attendance_registers as example for hottest tables
-- Note: Need to drop existing single-column FKs first

-- attendance_registers: grade_id should belong to same tenant
ALTER TABLE attendance_registers DROP FOREIGN KEY IF EXISTS fk_att_reg_grade;
ALTER TABLE attendance_registers ADD CONSTRAINT fk_att_reg_tenant_grade FOREIGN KEY (tenant_id, grade_id) REFERENCES grades(tenant_id, id) ON DELETE RESTRICT ON UPDATE CASCADE;

ALTER TABLE attendance_registers DROP FOREIGN KEY IF EXISTS fk_att_reg_stream;
ALTER TABLE attendance_registers ADD CONSTRAINT fk_att_reg_tenant_stream FOREIGN KEY (tenant_id, stream_id) REFERENCES streams(tenant_id, id) ON DELETE RESTRICT ON UPDATE CASCADE;

-- student_enrolments: grade, stream, student, academic_year, term all should have composite FKs
ALTER TABLE student_enrolments DROP FOREIGN KEY IF EXISTS fk_enrol_grade;
ALTER TABLE student_enrolments ADD CONSTRAINT fk_enrol_tenant_grade FOREIGN KEY (tenant_id, grade_id) REFERENCES grades(tenant_id, id) ON DELETE RESTRICT;

ALTER TABLE student_enrolments DROP FOREIGN KEY IF EXISTS fk_enrol_stream;
ALTER TABLE student_enrolments ADD CONSTRAINT fk_enrol_tenant_stream FOREIGN KEY (tenant_id, stream_id) REFERENCES streams(tenant_id, id) ON DELETE RESTRICT;

ALTER TABLE student_enrolments DROP FOREIGN KEY IF EXISTS fk_enrol_student;
ALTER TABLE student_enrolments ADD CONSTRAINT fk_enrol_tenant_student FOREIGN KEY (tenant_id, student_id) REFERENCES students(tenant_id, id) ON DELETE RESTRICT;

-- fee_invoices: student
ALTER TABLE fee_invoices DROP FOREIGN KEY IF EXISTS fk_fee_inv_student;
ALTER TABLE fee_invoices ADD CONSTRAINT fk_fee_inv_tenant_student FOREIGN KEY (tenant_id, student_id) REFERENCES students(tenant_id, id) ON DELETE RESTRICT;

-- student_marks: assessment, student
-- assessments table needs unique (tenant_id, id) first
CREATE UNIQUE INDEX IF NOT EXISTS uq_assessments_tenant_id ON assessments(tenant_id, id);
ALTER TABLE student_marks DROP FOREIGN KEY IF EXISTS fk_marks_assessment;
ALTER TABLE student_marks ADD CONSTRAINT fk_marks_tenant_assessment FOREIGN KEY (tenant_id, assessment_id) REFERENCES assessments(tenant_id, id) ON DELETE RESTRICT;

ALTER TABLE student_marks DROP FOREIGN KEY IF EXISTS fk_marks_student;
ALTER TABLE student_marks ADD CONSTRAINT fk_marks_tenant_student FOREIGN KEY (tenant_id, student_id) REFERENCES students(tenant_id, id) ON DELETE RESTRICT;

-- timetable_slots: grade, stream, subject, teacher
CREATE UNIQUE INDEX IF NOT EXISTS uq_rooms_tenant_id ON rooms(tenant_id, id);
ALTER TABLE timetable_slots DROP FOREIGN KEY IF EXISTS fk_tt_grade;
ALTER TABLE timetable_slots ADD CONSTRAINT fk_tt_tenant_grade FOREIGN KEY (tenant_id, grade_id) REFERENCES grades(tenant_id, id) ON DELETE RESTRICT;

ALTER TABLE timetable_slots DROP FOREIGN KEY IF EXISTS fk_tt_stream;
ALTER TABLE timetable_slots ADD CONSTRAINT fk_tt_tenant_stream FOREIGN KEY (tenant_id, stream_id) REFERENCES streams(tenant_id, id) ON DELETE RESTRICT;

-- Add more composite FKs as needed for other tables

-- ============================================================================
-- OPT-2: Partial Unique for Soft-Delete Reuse - Allow business key reuse after soft delete
-- ============================================================================
-- Problem: Unique constraints block reuse after soft delete (e.g., tenants.slug petra soft-deleted, new tenant cannot reuse petra)
-- Solution: Generated column active_xxx = IF(is_deleted=0, business_key, NULL) and unique on (tenant_id, active_xxx)
-- MySQL unique allows multiple NULLs, so deleted rows with NULL active_xxx don't block

-- tenants.slug: allow reuse after soft delete
ALTER TABLE tenants ADD COLUMN IF NOT EXISTS active_slug VARCHAR(100) GENERATED ALWAYS AS (IF(is_deleted=0, slug, NULL)) STORED;
CREATE UNIQUE INDEX IF NOT EXISTS uq_tenants_active_slug ON tenants(active_slug);

-- students.student_number: allow reuse per tenant after soft delete
ALTER TABLE students ADD COLUMN IF NOT EXISTS active_student_number VARCHAR(50) GENERATED ALWAYS AS (IF(is_deleted=0, student_number, NULL)) STORED;
CREATE UNIQUE INDEX IF NOT EXISTS uq_students_tenant_active_number ON students(tenant_id, active_student_number);

-- grades.code per tenant+year: allow reuse after soft delete
ALTER TABLE grades ADD COLUMN IF NOT EXISTS active_code VARCHAR(20) GENERATED ALWAYS AS (IF(is_deleted=0, code, NULL)) STORED;
CREATE UNIQUE INDEX IF NOT EXISTS uq_grades_tenant_year_active_code ON grades(tenant_id, academic_year_id, active_code);

-- fee_invoices.invoice_number: allow reuse after void? Actually invoice numbers should never be reused even after void for audit, so keep unique without soft-delete reuse. Skip.

-- For other tables like subjects.code, streams name per grade, etc. similar pattern can be added

-- subjects.code
ALTER TABLE subjects ADD COLUMN IF NOT EXISTS active_code VARCHAR(20) GENERATED ALWAYS AS (IF(is_deleted=0, code, NULL)) STORED;
CREATE UNIQUE INDEX IF NOT EXISTS uq_subjects_tenant_active_code ON subjects(tenant_id, active_code);

-- streams name per grade year
ALTER TABLE streams ADD COLUMN IF NOT EXISTS active_name VARCHAR(50) GENERATED ALWAYS AS (IF(is_deleted=0, name, NULL)) STORED;
-- This needs composite with grade and year
CREATE UNIQUE INDEX IF NOT EXISTS uq_streams_tenant_grade_year_active_name ON streams(tenant_id, academic_year_id, grade_id, active_name);

-- ============================================================================
-- OPT-3: Partitioning for Large Tables - 10M rows handling
-- ============================================================================
-- WARNING: MySQL partitioned tables CANNOT have foreign keys. So we must DROP FKs first if partitioning.
-- For this migration, we will NOT partition attendance_records yet due to FK limitation, but create partitioned version as audit_logs partitioned (audit_logs has no FKs to other partitioned? It has tenant_id FK nullable, but we can drop FK)

-- audit_logs partitioning by RANGE YEAR(created_at) - good for 5-year retention and purging
-- Check if already partitioned
-- This is complex and requires rebuilding table, so we do via new table approach

-- For demonstration, create partitioned version of audit_logs

-- First, drop FKs on audit_logs if any (tenant_id, user_id may have FKs)
-- Note: audit_logs in V1 has no FKs defined, only indexes, so safe to partition

-- Convert audit_logs to partitioned by RANGE YEAR(created_at)
-- MySQL 8 supports RANGE COLUMNS partitioning

-- Check if table is already partitioned by querying information_schema
-- If not partitioned, alter table

-- Note: This ALTER will rebuild table and may lock for large tables - needs downtime or pt-online-schema-change

ALTER TABLE audit_logs
PARTITION BY RANGE (YEAR(created_at)) (
  PARTITION p2024 VALUES LESS THAN (2025),
  PARTITION p2025 VALUES LESS THAN (2026),
  PARTITION p2026 VALUES LESS THAN (2027),
  PARTITION p2027 VALUES LESS THAN (2028),
  PARTITION p2028 VALUES LESS THAN (2029),
  PARTITION pfuture VALUES LESS THAN MAXVALUE
);

-- For attendance_records, we cannot partition with FKs, so we document approach and skip actual partitioning in this migration
-- Alternative: Create attendance_records_partitioned table partitioned by HASH(tenant_id) or RANGE YEAR(attendance_date) without FKs, then migrate data

-- Create partitioned version for future use (without FKs, for reference)
CREATE TABLE IF NOT EXISTS attendance_records_partitioned (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  tenant_id BIGINT UNSIGNED NOT NULL,
  academic_year_id BIGINT UNSIGNED NOT NULL,
  term_id BIGINT UNSIGNED NOT NULL,
  student_id BIGINT UNSIGNED NOT NULL,
  grade_id BIGINT UNSIGNED NOT NULL,
  stream_id BIGINT UNSIGNED NOT NULL,
  attendance_date DATE NOT NULL,
  period_number TINYINT UNSIGNED NULL,
  status ENUM('present','absent','late','sick','excused') NOT NULL,
  comment VARCHAR(255) NULL,
  marked_by_user_id BIGINT UNSIGNED NOT NULL,
  marked_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  PRIMARY KEY (id, attendance_date), -- Partition key must be part of PK
  KEY idx_att_part_tenant_stream_date (tenant_id, stream_id, attendance_date, status),
  KEY idx_att_part_tenant_student_date (tenant_id, student_id, attendance_date)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
PARTITION BY RANGE (YEAR(attendance_date)) (
  PARTITION p2024 VALUES LESS THAN (2025),
  PARTITION p2025 VALUES LESS THAN (2026),
  PARTITION p2026 VALUES LESS THAN (2027),
  PARTITION p2027 VALUES LESS THAN (2028),
  PARTITION pfuture VALUES LESS THAN MAXVALUE
);

-- Add comment about FK limitation
ALTER TABLE attendance_records_partitioned COMMENT = 'Partitioned version - no FKs due to MySQL limitation partitioned tables cannot have FKs. Use app layer guard for tenant isolation.';

-- Also create attendance_records future migration note
-- To migrate: INSERT INTO attendance_records_partitioned SELECT ... FROM attendance_records; RENAME TABLES;

-- ============================================================================
-- Additional Phase 2: Fix nullable inconsistencies
-- ============================================================================

-- fee_invoices.enrolment_id should be NOT NULL per spec - check if any NULLs exist first
-- We will not alter yet, just add check to ensure no NULLs for new rows via trigger or app
-- For now, add a check constraint that if status != 'draft', enrolment_id must not be NULL

-- ALTER TABLE fee_invoices ADD CONSTRAINT chk_fee_inv_enrolment CHECK ( (status='draft' AND enrolment_id IS NULL) OR (enrolment_id IS NOT NULL) ); -- commented for safety

-- ============================================================================
-- Record migration
-- ============================================================================

INSERT INTO schema_migrations (version, checksum, description) VALUES ('V18', SHA2('Phase2_Advanced',256), 'Phase 2: composite FKs tenant isolation, partial unique soft-delete reuse via generated columns, partitioning audit_logs') ON DUPLICATE KEY UPDATE applied_at=CURRENT_TIMESTAMP;

