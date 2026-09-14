-- V20 Postgres Remaining Migrations - Translated from MySQL V3-V18
-- Combined file for all remaining tables not in V19
-- Date: 2026-08-09
-- This file includes translations of V3-V18 MySQL migrations to Postgres


-- ============================================================================
-- From V15_AI.sql - LearnCloud.AI
-- Original: /home/user/src/LearnCloud.AI/Migrations/V15_AI.sql
-- ============================================================================
-- AI-Assisted Features V15 - Report card comment drafting, attendance anomaly detection, at-risk learner identification
-- Priority order: 1. Report comment drafting saves hours, teacher always reviews, nothing auto-written

CREATE TABLE IF NOT EXISTS ai_provider_settings (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  provider_name VARCHAR(50) NOT NULL DEFAULT 'RuleBased',
  is_active BOOLEAN NOT NULL DEFAULT 1,
  is_default BOOLEAN NOT NULL DEFAULT 1,
  config_json JSONB NULL,
  enable_comment_drafting BOOLEAN NOT NULL DEFAULT 1,
  enable_attendance_anomaly BOOLEAN NOT NULL DEFAULT 1,
  enable_at_risk_detection BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_ai_provider_tenant_default (tenant_id, is_default),
  KEY idx_ai_provider_tenant_active (tenant_id, is_active)
);

CREATE TABLE IF NOT EXISTS report_comment_drafts (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  report_card_id BIGINT NULL,
  input_data_json JSONB NOT NULL,
  draft_comment TEXT NOT NULL,
  tone VARCHAR(20) NOT NULL DEFAULT 'encouraging',
  length VARCHAR(20) NOT NULL DEFAULT 'medium',
  edited_comment TEXT NULL,
  is_edited BOOLEAN NOT NULL DEFAULT 0,
  is_saved BOOLEAN NOT NULL DEFAULT 0,
  edited_by_user_id BIGINT NULL,
  edited_at TIMESTAMPTZ NULL,
  saved_by_user_id BIGINT NULL,
  saved_at TIMESTAMPTZ NULL,
  provider_name VARCHAR(50) NOT NULL DEFAULT 'RuleBased',
  model VARCHAR(100) NULL,
  prompt_tokens INT NULL,
  completion_tokens INT NULL,
  cost DECIMAL(18,4) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_comment_tenant_student (tenant_id, student_id),
  KEY idx_comment_tenant_year_term (tenant_id, academic_year_id, term_id)
);

CREATE TABLE IF NOT EXISTS attendance_anomalies (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  anomaly_type VARCHAR(30) NOT NULL,
  description VARCHAR(255) NOT NULL,
  explanation TEXT NOT NULL,
  confidence_score DECIMAL(5,2) NOT NULL,
  detected_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  period_from TIMESTAMPTZ NOT NULL,
  period_to TIMESTAMPTZ NOT NULL,
  data_json JSONB NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'new',
  acknowledged_by_user_id BIGINT NULL,
  acknowledged_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_anomaly_tenant_student (tenant_id, student_id),
  KEY idx_anomaly_tenant_type_status (tenant_id, anomaly_type, status)
);

CREATE TABLE IF NOT EXISTS at_risk_flags (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  risk_level VARCHAR(20) NOT NULL DEFAULT 'medium',
  risk_score DECIMAL(5,2) NOT NULL,
  flag_reason TEXT NOT NULL,
  underlying_reasons_json JSONB NOT NULL,
  detected_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  period_from TIMESTAMPTZ NOT NULL,
  period_to TIMESTAMPTZ NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'new',
  assigned_to_user_id BIGINT NULL,
  follow_up_notes TEXT NULL,
  resolved_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_atrisk_tenant_student (tenant_id, student_id),
  KEY idx_atrisk_tenant_level_status (tenant_id, risk_level, status)
);

-- Seed default AI provider settings per tenant - RuleBased fallback works offline
INSERT INTO ai_provider_settings (tenant_id, provider_name, is_active, is_default, enable_comment_drafting, enable_attendance_anomaly, enable_at_risk_detection)
SELECT id, 'RuleBased', 1, 1, 1, 1, 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM ai_provider_settings WHERE tenant_id=tenants.id)
ON DUPLICATE KEY UPDATE provider_name=VALUES(provider_name);


-- ============================================================================
-- From V3_Attendance_Timetable.sql - LearnCloud.AttendanceTimetable
-- Original: /home/user/src/LearnCloud.AttendanceTimetable/Migrations/V3_Attendance_Timetable.sql
-- ============================================================================
-- LearnCloud Attendance & Timetable V3 - MySQL 8.0
-- Tenant attendance settings, registers, records, period definitions, timetables, slots
-- All tenant-owned tables tenant_id leading index, soft-delete, audit

-- Tenant attendance settings
CREATE TABLE IF NOT EXISTS tenant_attendance_settings (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  mode INT NOT NULL DEFAULT 1 COMMENT '1=Daily, 2=PerPeriod',
  backdating_window_days INT NOT NULL DEFAULT 7,
  allow_backdating_beyond_window BOOLEAN NOT NULL DEFAULT 1,
  chronic_absence_threshold DECIMAL(5,2) NOT NULL DEFAULT 85.00,
  count_late_as_present BOOLEAN NOT NULL DEFAULT 1,
  count_excused_as_present BOOLEAN NOT NULL DEFAULT 1,
  count_sick_as_present BOOLEAN NOT NULL DEFAULT 0,
  auto_save_interval_seconds INT NOT NULL DEFAULT 5,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_att_settings_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_att_settings_tenant (tenant_id),
  KEY idx_att_settings_tenant (tenant_id)
);

-- Period definitions per tenant
CREATE TABLE IF NOT EXISTS period_definitions (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  period_number INT NOT NULL,
  name VARCHAR(50) NOT NULL,
  start_time TIME NOT NULL,
  end_time TIME NOT NULL,
  is_break BOOLEAN NOT NULL DEFAULT 0,
  sort_order INT NOT NULL DEFAULT 0,
  academic_year_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_period_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_period_tenant_number (tenant_id, period_number, academic_year_id),
  KEY idx_period_tenant_sort (tenant_id, sort_order)
);

-- Attendance registers header - guards duplicate
CREATE TABLE IF NOT EXISTS attendance_registers (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  grade_id BIGINT NOT NULL,
  stream_id BIGINT NOT NULL,
  attendance_date DATE NOT NULL,
  period_number INT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  is_backdated BOOLEAN NOT NULL DEFAULT 0,
  submitted_at TIMESTAMPTZ NULL,
  submitted_by_user_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_att_reg_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  CONSTRAINT fk_att_reg_grade FOREIGN KEY (grade_id) REFERENCES grades(id) ON DELETE RESTRICT,
  CONSTRAINT fk_att_reg_stream FOREIGN KEY (stream_id) REFERENCES streams(id) ON DELETE RESTRICT,
  UNIQUE KEY uq_att_reg_tenant_class_date_period (tenant_id, grade_id, stream_id, attendance_date, period_number, academic_year_id, term_id),
  KEY idx_att_reg_tenant_stream_date (tenant_id, stream_id, attendance_date, period_number),
  KEY idx_att_reg_tenant_year_term (tenant_id, academic_year_id, term_id, attendance_date)
);

-- Attendance records per learner
CREATE TABLE IF NOT EXISTS attendance_records (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  register_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  grade_id BIGINT NOT NULL,
  stream_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  attendance_date DATE NOT NULL,
  period_number INT NULL,
  status INT NOT NULL COMMENT '1=Present,2=Absent,3=Late,4=Sick,5=Excused',
  absence_reason VARCHAR(100) NULL,
  note VARCHAR(255) NULL,
  marked_by_user_id BIGINT NOT NULL,
  marked_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_backdated BOOLEAN NOT NULL DEFAULT 0,
  backdate_reason VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_att_rec_register FOREIGN KEY (register_id) REFERENCES attendance_registers(id) ON DELETE CASCADE,
  CONSTRAINT fk_att_rec_student FOREIGN KEY (student_id) REFERENCES students(id) ON DELETE RESTRICT,
  CONSTRAINT fk_att_rec_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_att_rec_tenant_student_date_period (tenant_id, student_id, attendance_date, period_number),
  KEY idx_att_rec_tenant_stream_date (tenant_id, stream_id, attendance_date, status),
  KEY idx_att_rec_tenant_student_date (tenant_id, student_id, attendance_date),
  KEY idx_att_rec_tenant_year_term (tenant_id, academic_year_id, term_id, attendance_date)
);

-- Timetables effective dated
CREATE TABLE IF NOT EXISTS timetables (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  effective_from DATE NOT NULL,
  effective_to DATE NULL,
  version INT NOT NULL DEFAULT 1,
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  created_from_timetable_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_tt_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_tt_tenant_year_term_eff (tenant_id, academic_year_id, term_id, effective_from, effective_to),
  KEY idx_tt_tenant_status (tenant_id, status)
);

-- Timetable slots
CREATE TABLE IF NOT EXISTS timetable_slots (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  timetable_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  grade_id BIGINT NOT NULL,
  stream_id BIGINT NOT NULL,
  subject_id BIGINT NOT NULL,
  teacher_staff_id BIGINT NOT NULL,
  room_id BIGINT NULL,
  day_of_week SMALLINT NOT NULL COMMENT '1=Mon..7=Sun',
  period_number INT NOT NULL,
  start_time TIME NOT NULL,
  end_time TIME NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_tt_slot_timetable FOREIGN KEY (timetable_id) REFERENCES timetables(id) ON DELETE CASCADE,
  CONSTRAINT fk_tt_slot_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_tt_slot_class (tenant_id, timetable_id, day_of_week, period_number, stream_id),
  UNIQUE KEY uq_tt_slot_teacher (tenant_id, timetable_id, day_of_week, period_number, teacher_staff_id),
  UNIQUE KEY uq_tt_slot_room (tenant_id, timetable_id, day_of_week, period_number, room_id),
  KEY idx_tt_slot_tenant_teacher (tenant_id, teacher_staff_id, academic_year_id, term_id, day_of_week),
  KEY idx_tt_slot_tenant_stream (tenant_id, stream_id, academic_year_id, term_id, day_of_week),
  KEY idx_tt_slot_tenant_grade (tenant_id, grade_id, academic_year_id, term_id)
);

-- Seed default periods for existing tenants (if none)
-- Handled in service GetPeriodsAsync creates 8 periods + breaks if none


-- ============================================================================
-- From V17_Phase0_1_Optimizations.sql - LearnCloud.Auth
-- Original: /home/user/src/LearnCloud.Auth/Migrations/V17_Phase0_1_Optimizations.sql
-- ============================================================================
-- V17 Phase 0+1 Optimizations - Approved (No Business Logic Change + Low Risk)
-- Date: 2026-08-09
-- Fixes: C1 duplicate indexes, C2 missing tenant_id leading, C3 CASCADE->RESTRICT, H1 unique soft-delete handling via rename, H3 billing unique, H6 covering indexes, H8 precision doc

-- Phase 0 Safe

-- C1: Remove duplicate indexes
-- roles has uq_roles_tenant_code and idx_roles_tenant_code same columns (tenant_id, code) - drop non-unique
ALTER TABLE roles DROP INDEX IF EXISTS idx_roles_tenant_code;

-- Check other duplicate indexes: subscription_plans tenant_id nullable index not needed? Keep.

-- C2: Add missing tenant_id leading indexes
-- audit_logs missing tenant_id leading for tenant queries
CREATE INDEX IF NOT EXISTS idx_audit_tenant_created ON audit_logs(tenant_id, created_at);
CREATE INDEX IF NOT EXISTS idx_audit_tenant_entity_created ON audit_logs(tenant_id, entity_type, entity_id, created_at);

-- period_definitions ordering index
CREATE INDEX IF NOT EXISTS idx_period_tenant_academic_sort ON period_definitions(tenant_id, academic_year_id, sort_order);

-- fee_structure_items already has idx_fsi_tenant_structure, good

-- Phase 1 Low Risk (Approved)

-- C3: Change ON DELETE CASCADE to RESTRICT for tenant-owned business tables to prevent hard delete wiping Ministry 7-year data
-- For MySQL, need to drop FK and re-add with RESTRICT

-- users -> tenants: should be RESTRICT not CASCADE (soft delete only)
-- Check existing FK name may be fk_users_tenant
ALTER TABLE users DROP FOREIGN KEY IF EXISTS fk_users_tenant;
ALTER TABLE users ADD CONSTRAINT fk_users_tenant_restrict FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE RESTRICT ON UPDATE CASCADE;

-- grades -> tenants
ALTER TABLE grades DROP FOREIGN KEY IF EXISTS fk_grade_tenant;
ALTER TABLE grades DROP FOREIGN KEY IF EXISTS fk_grades_tenant;
ALTER TABLE grades ADD CONSTRAINT fk_grades_tenant_restrict FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE RESTRICT ON UPDATE CASCADE;

-- Note: streams, subjects, students etc need similar - do for most business tables
-- Due to many tables, we do for critical ones: attendance_registers, fee_invoices, student_marks, fee_payments

-- attendance_registers grade_id currently RESTRICT (good) but tenant_id CASCADE -> should be RESTRICT
ALTER TABLE attendance_registers DROP FOREIGN KEY IF EXISTS fk_att_reg_tenant;
ALTER TABLE attendance_registers ADD CONSTRAINT fk_att_reg_tenant_restrict FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE RESTRICT;

-- fee_invoices tenant_id
ALTER TABLE fee_invoices DROP FOREIGN KEY IF EXISTS fk_fee_inv_tenant;
ALTER TABLE fee_invoices DROP FOREIGN KEY IF EXISTS fk_fee_invoices_tenant;
ALTER TABLE fee_invoices ADD CONSTRAINT fk_fee_invoices_tenant_restrict FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE RESTRICT;

-- fee_invoice_items -> fee_invoices should be RESTRICT not CASCADE (void should not hard delete lines)
ALTER TABLE fee_invoice_items DROP FOREIGN KEY IF EXISTS fk_fee_inv_items_invoice;
ALTER TABLE fee_invoice_items DROP FOREIGN KEY IF EXISTS fk_fee_invoice_items_invoice;
ALTER TABLE fee_invoice_items ADD CONSTRAINT fk_fee_invoice_items_invoice_restrict FOREIGN KEY (invoice_id) REFERENCES fee_invoices(id) ON DELETE RESTRICT;

-- fee_payment_allocations -> fee_payments and fee_invoices
ALTER TABLE fee_payment_allocations DROP FOREIGN KEY IF EXISTS fk_alloc_payment;
ALTER TABLE fee_payment_allocations ADD CONSTRAINT fk_alloc_payment_restrict FOREIGN KEY (payment_id) REFERENCES fee_payments(id) ON DELETE RESTRICT;
ALTER TABLE fee_payment_allocations DROP FOREIGN KEY IF EXISTS fk_alloc_invoice;
ALTER TABLE fee_payment_allocations ADD CONSTRAINT fk_alloc_invoice_restrict FOREIGN KEY (invoice_id) REFERENCES fee_invoices(id) ON DELETE RESTRICT;

-- H6: Add covering indexes for hottest queries

-- Parent portal fee balance + latest invoice: status + due_date sort
CREATE INDEX IF NOT EXISTS idx_inv_tenant_student_status_due ON fee_invoices(tenant_id, student_id, status, due_date DESC, balance_due);

-- Fee arrears by class and amount: tenant + status + due_date + balance
CREATE INDEX IF NOT EXISTS idx_inv_tenant_status_due_balance ON fee_invoices(tenant_id, status, due_date, balance_due, student_id);

-- Inbox unread count + recent: recipient + is_read + message_id + sent_at via join
-- message_recipients already has idx_mr_tenant_user (tenant_id, recipient_user_id, is_read) - add covering message_id
CREATE INDEX IF NOT EXISTS idx_mr_tenant_user_read_msg ON message_recipients(tenant_id, recipient_user_id, is_read, message_id);

-- Students name ordering: already idx_students_tenant_name (tenant_id, last_name, first_name) - ensure covering id
-- Add covering index for attendance: tenant + student + date + status
CREATE INDEX IF NOT EXISTS idx_att_records_tenant_student_date_status ON attendance_records(tenant_id, student_id, attendance_date, status);

-- Timetable teacher view already has idx_tt_tenant_teacher, good

-- H3: Billing contact unique - one billing contact per student per year
-- Use generated column approach: billing_flag = CASE WHEN is_billing_contact=true THEN student_id ELSE NULL END then unique (tenant_id, academic_year_id, billing_flag) allows only one billing=1 per student/year

ALTER TABLE guardian_student_links ADD COLUMN IF NOT EXISTS billing_unique_key BIGINT GENERATED ALWAYS AS (CASE WHEN is_billing_contact=1, student_id, NULL END) STORED;

-- MySQL unique with NULL allows multiple NULLs, so only one row with same student_id and is_billing=1 per tenant+year
CREATE UNIQUE INDEX IF NOT EXISTS uq_gsl_tenant_year_billing ON guardian_student_links(tenant_id, academic_year_id, billing_unique_key);

-- H2: Fix nullable inconsistency - fee_invoices.enrolment_id should be NOT NULL per spec
-- First check if any NULLs exist, if so fill with 0 or allow? For safety, we add NOT NULL with default after cleanup in app
-- ALTER TABLE fee_invoices MODIFY enrolment_id BIGINT NOT NULL; -- commented out for now, need data cleanup first, will be done after verifying no NULLs

-- H8: Add Check constraints for is_deleted boolean
ALTER TABLE tenants ADD CONSTRAINT IF NOT EXISTS chk_tenants_is_deleted CHECK (is_deleted IN (0,1));
ALTER TABLE users ADD CONSTRAINT IF NOT EXISTS chk_users_is_deleted CHECK (is_deleted IN (0,1));
ALTER TABLE students ADD CONSTRAINT IF NOT EXISTS chk_students_is_deleted CHECK (is_deleted IN (0,1));
-- Add for other tables as needed - MySQL 8.0 supports CHECK

-- Phase 0: Create schema_migrations table for version tracking (M4 fix)
CREATE TABLE IF NOT EXISTS schema_migrations (
  version VARCHAR(20) NOT NULL PRIMARY KEY,
  applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  checksum VARCHAR(64) NULL,
  description VARCHAR(255) NULL,
  execution_time_ms INT NULL
);

-- Record this migration
INSERT INTO schema_migrations (version, checksum, description) VALUES ('V17', SHA2('Phase0_1_Optimizations',256), 'Phase 0+1: duplicate indexes, tenant leading, CASCADE->RESTRICT, covering indexes, billing unique') ON DUPLICATE KEY UPDATE applied_at=CURRENT_TIMESTAMP;

-- Add comments for composite FK intent (documentation, no schema change)
ALTER TABLE attendance_registers COMMENT = 'INTENT: grade_id must belong to same tenant_id - enforced at app layer + composite FK future. See DB audit C3.';
ALTER TABLE fee_invoices COMMENT = 'INTENT: student_id must belong to same tenant_id - enforced at app layer SaveChangesAsync guard';



-- ============================================================================
-- From V18_Phase2_Advanced_Optimizations.sql - LearnCloud.Auth
-- Original: /home/user/src/LearnCloud.Auth/Migrations/V18_Phase2_Advanced_Optimizations.sql
-- ============================================================================
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
-- Solution: Generated column active_xxx = CASE WHEN is_deleted=false THEN business_key ELSE NULL END and unique on (tenant_id, active_xxx)
-- MySQL unique allows multiple NULLs, so deleted rows with NULL active_xxx don't block

-- tenants.slug: allow reuse after soft delete
ALTER TABLE tenants ADD COLUMN IF NOT EXISTS active_slug VARCHAR(100) GENERATED ALWAYS AS (CASE WHEN is_deleted=0, slug, NULL END) STORED;
CREATE UNIQUE INDEX IF NOT EXISTS uq_tenants_active_slug ON tenants(active_slug);

-- students.student_number: allow reuse per tenant after soft delete
ALTER TABLE students ADD COLUMN IF NOT EXISTS active_student_number VARCHAR(50) GENERATED ALWAYS AS (CASE WHEN is_deleted=0, student_number, NULL END) STORED;
CREATE UNIQUE INDEX IF NOT EXISTS uq_students_tenant_active_number ON students(tenant_id, active_student_number);

-- grades.code per tenant+year: allow reuse after soft delete
ALTER TABLE grades ADD COLUMN IF NOT EXISTS active_code VARCHAR(20) GENERATED ALWAYS AS (CASE WHEN is_deleted=0, code, NULL END) STORED;
CREATE UNIQUE INDEX IF NOT EXISTS uq_grades_tenant_year_active_code ON grades(tenant_id, academic_year_id, active_code);

-- fee_invoices.invoice_number: allow reuse after void? Actually invoice numbers should never be reused even after void for audit, so keep unique without soft-delete reuse. Skip.

-- For other tables like subjects.code, streams name per grade, etc. similar pattern can be added

-- subjects.code
ALTER TABLE subjects ADD COLUMN IF NOT EXISTS active_code VARCHAR(20) GENERATED ALWAYS AS (CASE WHEN is_deleted=0, code, NULL END) STORED;
CREATE UNIQUE INDEX IF NOT EXISTS uq_subjects_tenant_active_code ON subjects(tenant_id, active_code);

-- streams name per grade year
ALTER TABLE streams ADD COLUMN IF NOT EXISTS active_name VARCHAR(50) GENERATED ALWAYS AS (CASE WHEN is_deleted=0, name, NULL END) STORED;
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
  id BIGINT NOT NULL AUTO_INCREMENT,
  tenant_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  grade_id BIGINT NOT NULL,
  stream_id BIGINT NOT NULL,
  attendance_date DATE NOT NULL,
  period_number SMALLINT UNSIGNED NULL,
  status VARCHAR(50) NOT NULL,
  comment VARCHAR(255) NULL,
  marked_by_user_id BIGINT NOT NULL,
  marked_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  PRIMARY KEY (id, attendance_date), -- Partition key must be part of PK
  KEY idx_att_part_tenant_stream_date (tenant_id, stream_id, attendance_date, status),
  KEY idx_att_part_tenant_student_date (tenant_id, student_id, attendance_date)
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



-- ============================================================================
-- From V9_Communication.sql - LearnCloud.Communication
-- Original: /home/user/src/LearnCloud.Communication/Migrations/V9_Communication.sql
-- ============================================================================
-- Communication Full Module V9 - Extend messaging: announcements, scheduled sending, saved segments, template categories, two-way SMS, rule engine, analytics, per-learner log
-- Preserve provider abstraction, usage caps, opt-out

CREATE TABLE IF NOT EXISTS announcements (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  title VARCHAR(255) NOT NULL,
  body TEXT NOT NULL,
  audience_type VARCHAR(30) NOT NULL DEFAULT 'all',
  audience_filter_json JSONB NOT NULL,
  expiry_date TIMESTAMPTZ NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  priority VARCHAR(20) NOT NULL DEFAULT 'normal',
  show_in_teacher_portal BOOLEAN NOT NULL DEFAULT 1,
  show_in_parent_portal BOOLEAN NOT NULL DEFAULT 1,
  show_in_student_portal BOOLEAN NOT NULL DEFAULT 1,
  show_in_admin_dashboard BOOLEAN NOT NULL DEFAULT 1,
  created_by_user_id BIGINT NOT NULL,
  published_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_ann_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_ann_tenant_status_expiry (tenant_id, status, expiry_date),
  KEY idx_ann_tenant_portals (tenant_id, show_in_teacher_portal, show_in_parent_portal)
);

CREATE TABLE IF NOT EXISTS scheduled_messages (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  batch_id BIGINT NULL,
  title VARCHAR(255) NOT NULL,
  template_id BIGINT NULL,
  channel VARCHAR(20) NOT NULL DEFAULT 'sms',
  audience_type VARCHAR(30) NOT NULL,
  audience_filter_json JSONB NOT NULL,
  body TEXT NOT NULL,
  subject VARCHAR(500) NULL,
  scheduled_send_at TIMESTAMPTZ NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'scheduled',
  created_by_user_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_sched_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_sched_tenant_scheduled (tenant_id, scheduled_send_at, status)
);

CREATE TABLE IF NOT EXISTS audience_segments (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  description VARCHAR(500) NOT NULL DEFAULT '',
  audience_type VARCHAR(30) NOT NULL,
  filter_json JSONB NOT NULL,
  is_dynamic BOOLEAN NOT NULL DEFAULT 0,
  created_by_user_id BIGINT NOT NULL,
  is_system BOOLEAN NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_seg_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_seg_tenant_name (tenant_id, name),
  KEY idx_seg_tenant_type (tenant_id, audience_type)
);

CREATE TABLE IF NOT EXISTS template_categories (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(50) NOT NULL,
  code VARCHAR(30) NOT NULL,
  description VARCHAR(255) NULL,
  color VARCHAR(7) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_cat_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_cat_tenant_code (tenant_id, code)
);

CREATE TABLE IF NOT EXISTS categorized_templates (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  template_id BIGINT NOT NULL,
  category_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_ct_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  CONSTRAINT fk_ct_template FOREIGN KEY (template_id) REFERENCES message_templates(id) ON DELETE CASCADE,
  CONSTRAINT fk_ct_category FOREIGN KEY (category_id) REFERENCES template_categories(id) ON DELETE CASCADE,
  UNIQUE KEY uq_ct_tenant_template_category (tenant_id, template_id, category_id)
);

CREATE TABLE IF NOT EXISTS inbound_sms (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  from_number VARCHAR(20) NOT NULL,
  to_number VARCHAR(20) NOT NULL,
  body TEXT NOT NULL,
  provider VARCHAR(50) NULL,
  provider_reference VARCHAR(100) NULL,
  received_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  matched_guardian_id BIGINT NULL,
  matched_student_id BIGINT NULL,
  matched_school_slug VARCHAR(100) NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'received',
  reply_body TEXT NULL,
  replied_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_inbound_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_inbound_tenant_from (tenant_id, from_number),
  KEY idx_inbound_tenant_received (tenant_id, received_at)
);

CREATE TABLE IF NOT EXISTS communication_rules (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  code VARCHAR(50) NOT NULL,
  event_type VARCHAR(50) NOT NULL,
  description VARCHAR(500) NOT NULL DEFAULT '',
  is_active BOOLEAN NOT NULL DEFAULT 1,
  config_json JSONB NOT NULL,
  template_id BIGINT NULL,
  channel VARCHAR(20) NOT NULL DEFAULT 'sms',
  audience_type VARCHAR(30) NOT NULL DEFAULT 'dynamic',
  respect_opt_out BOOLEAN NOT NULL DEFAULT 1,
  respect_contact_preferences BOOLEAN NOT NULL DEFAULT 1,
  created_by_user_id BIGINT NOT NULL,
  last_triggered_at TIMESTAMPTZ NULL,
  trigger_count INT NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_rule_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_rule_tenant_code (tenant_id, code),
  KEY idx_rule_tenant_event_active (tenant_id, event_type, is_active)
);

CREATE TABLE IF NOT EXISTS communication_logs (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  batch_id BIGINT NULL,
  announcement_id BIGINT NULL,
  rule_id BIGINT NULL,
  inbound_sms_id BIGINT NULL,
  student_id BIGINT NOT NULL,
  guardian_id BIGINT NULL,
  teacher_staff_id BIGINT NULL,
  channel VARCHAR(20) NOT NULL,
  direction VARCHAR(20) NOT NULL DEFAULT 'outbound',
  recipient_address VARCHAR(255) NOT NULL,
  message_body TEXT NOT NULL,
  subject VARCHAR(500) NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'sent',
  provider VARCHAR(50) NULL,
  provider_reference VARCHAR(100) NULL,
  cost DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  sent_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  delivered_at TIMESTAMPTZ NULL,
  read_at TIMESTAMPTZ NULL,
  failure_reason VARCHAR(500) NULL,
  is_opted_out_at_send BOOLEAN NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_commlog_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_commlog_tenant_student (tenant_id, student_id),
  KEY idx_commlog_tenant_guardian (tenant_id, guardian_id),
  KEY idx_commlog_tenant_batch (tenant_id, batch_id),
  KEY idx_commlog_search (tenant_id, student_id, channel, status, sent_at),
  FULLTEXT KEY ft_commlog_body (message_body)
);

-- Seed default template categories for existing tenants
INSERT INTO template_categories (tenant_id, name, code, description, color)
SELECT id, 'Fees', 'fees', 'Fee reminders, invoices, receipts', '#B7791F' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM template_categories WHERE tenant_id=tenants.id AND code='fees')
ON DUPLICATE KEY UPDATE name=VALUES(name);
INSERT INTO template_categories (tenant_id, name, code, description, color)
SELECT id, 'Attendance', 'attendance', 'Absence, late, chronic absence', '#C62828' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM template_categories WHERE tenant_id=tenants.id AND code='attendance')
ON DUPLICATE KEY UPDATE name=VALUES(name);
INSERT INTO template_categories (tenant_id, name, code, description, color)
SELECT id, 'Academic', 'academic', 'Report cards, assessments, homework', '#5A94C1' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM template_categories WHERE tenant_id=tenants.id AND code='academic')
ON DUPLICATE KEY UPDATE name=VALUES(name);
INSERT INTO template_categories (tenant_id, name, code, description, color)
SELECT id, 'General', 'general', 'General notices', '#0F153A' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM template_categories WHERE tenant_id=tenants.id AND code='general')
ON DUPLICATE KEY UPDATE name=VALUES(name);

-- Seed default communication rules per tenant: absence 3 days, arrears over 100, report card published, invoice due 7 days
INSERT INTO communication_rules (tenant_id, name, code, event_type, description, is_active, config_json, channel, audience_type, respect_opt_out, created_by_user_id)
SELECT id, 'Absence 3 consecutive days', 'absence_3_days', 'absence_n_days', 'Send SMS to guardians when learner absent 3 consecutive days', 1, '{"n":3}', 'sms', 'dynamic', 1, 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM communication_rules WHERE tenant_id=tenants.id AND code='absence_3_days')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO communication_rules (tenant_id, name, code, event_type, description, is_active, config_json, channel, audience_type, respect_opt_out, created_by_user_id)
SELECT id, 'Arrears over $100', 'arrears_over_100', 'arrears_over_threshold', 'Send fee reminder when arrears over threshold', 1, '{"threshold":100,"currency":"USD"}', 'sms', 'dynamic', 1, 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM communication_rules WHERE tenant_id=tenants.id AND code='arrears_over_100')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO communication_rules (tenant_id, name, code, event_type, description, is_active, config_json, channel, audience_type, respect_opt_out, created_by_user_id)
SELECT id, 'Report card published', 'report_published', 'report_card_published', 'Notify guardian when report card published', 1, '{}', 'sms', 'dynamic', 1, 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM communication_rules WHERE tenant_id=tenants.id AND code='report_published')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO communication_rules (tenant_id, name, code, event_type, description, is_active, config_json, channel, audience_type, respect_opt_out, created_by_user_id)
SELECT id, 'Invoice due in 7 days', 'invoice_due_7', 'invoice_due_7_days', 'Reminder 7 days before invoice due', 1, '{"daysBeforeDue":7}', 'sms', 'dynamic', 1, 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM communication_rules WHERE tenant_id=tenants.id AND code='invoice_due_7')
ON DUPLICATE KEY UPDATE name=VALUES(name);


-- ============================================================================
-- From V16_Subjects.sql - LearnCloud.Core
-- Original: /home/user/src/LearnCloud.Core/Migrations/V16_Subjects.sql
-- ============================================================================
-- Core Records Phase 1: Subjects - independent, no dependencies, foundation for teaching
-- Every tenant-owned table has tenant_id leading index, audit and soft-delete, unique per tenant

CREATE TABLE IF NOT EXISTS subjects (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  code VARCHAR(20) NOT NULL,
  description VARCHAR(500) NULL,
  is_core BOOLEAN NOT NULL DEFAULT 1,
  department VARCHAR(100) NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_subject_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_subject_tenant_code (tenant_id, code),
  KEY idx_subject_tenant_name (tenant_id, name),
  KEY idx_subject_tenant_department (tenant_id, department),
  KEY idx_subject_tenant_status (tenant_id, status)
);

CREATE TABLE IF NOT EXISTS grades (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(50) NOT NULL,
  code VARCHAR(20) NOT NULL,
  level_order INT NOT NULL DEFAULT 0,
  academic_year_id BIGINT NOT NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_grade_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_grade_tenant_code_year (tenant_id, code, academic_year_id),
  KEY idx_grade_tenant_year_order (tenant_id, academic_year_id, level_order)
);

CREATE TABLE IF NOT EXISTS grade_subjects (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  grade_id BIGINT NOT NULL,
  subject_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  is_compulsory BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_gs_grade FOREIGN KEY (grade_id) REFERENCES grades(id) ON DELETE CASCADE,
  CONSTRAINT fk_gs_subject FOREIGN KEY (subject_id) REFERENCES subjects(id) ON DELETE CASCADE,
  UNIQUE KEY uq_gs_tenant_grade_subject_year (tenant_id, grade_id, subject_id, academic_year_id),
  KEY idx_gs_tenant_year_grade (tenant_id, academic_year_id, grade_id)
);

-- Seed default subjects per tenant from SubjectDefaults (primary + secondary)
INSERT INTO subjects (tenant_id, name, code, description, is_core, department, status)
SELECT id, 'Mathematics', 'MATH', 'Core mathematics', 1, 'Sciences', 'active' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM subjects WHERE tenant_id=tenants.id AND code='MATH')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO subjects (tenant_id, name, code, is_core, department, status)
SELECT id, 'English', 'ENG', 1, 'Languages', 'active' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM subjects WHERE tenant_id=tenants.id AND code='ENG')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO subjects (tenant_id, name, code, is_core, department, status)
SELECT id, 'Science and Technology', 'SCI', 1, 'Sciences', 'active' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM subjects WHERE tenant_id=tenants.id AND code='SCI')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO subjects (tenant_id, name, code, is_core, department, status)
SELECT id, 'Heritage and Social Sciences', 'HER', 0, 'Arts', 'active' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM subjects WHERE tenant_id=tenants.id AND code='HER')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO subjects (tenant_id, name, code, is_core, department, status)
SELECT id, 'Computer Science', 'COMPSCI', 0, 'Sciences', 'active' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM subjects WHERE tenant_id=tenants.id AND code='COMPSCI')
ON DUPLICATE KEY UPDATE name=VALUES(name);


-- ============================================================================
-- From V8_Examinations.sql - LearnCloud.Examinations
-- Original: /home/user/src/LearnCloud.Examinations/Migrations/V8_Examinations.sql
-- ============================================================================
-- Examinations Full Module V8 - Sessions grouping assessments, timetable venues invigilators, weighted composite, merit lists, promotion, transcripts, moderation, certificates

CREATE TABLE IF NOT EXISTS examination_sessions (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  session_type VARCHAR(20) NOT NULL DEFAULT 'final',
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  description VARCHAR(500) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_exam_sess_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_exam_sess_tenant_year_term (tenant_id, academic_year_id, term_id),
  KEY idx_exam_sess_tenant_status (tenant_id, status)
);

CREATE TABLE IF NOT EXISTS examination_session_assessments (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  examination_session_id BIGINT NOT NULL,
  assessment_id BIGINT NOT NULL,
  subject_id BIGINT NOT NULL,
  grade_id BIGINT NOT NULL,
  stream_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_exam_sess_ass_sess FOREIGN KEY (examination_session_id) REFERENCES examination_sessions(id) ON DELETE CASCADE,
  UNIQUE KEY uq_exam_sess_ass (tenant_id, examination_session_id, assessment_id)
);

CREATE TABLE IF NOT EXISTS examination_slots (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  examination_session_id BIGINT NOT NULL,
  subject_id BIGINT NOT NULL,
  grade_id BIGINT NOT NULL,
  stream_id BIGINT NULL,
  assessment_id BIGINT NOT NULL,
  exam_date DATE NOT NULL,
  start_time TIME NOT NULL,
  end_time TIME NOT NULL,
  venue_id BIGINT NULL,
  venue_name VARCHAR(100) NULL,
  invigilator_staff_id BIGINT NULL,
  invigilator_name VARCHAR(100) NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'scheduled',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_exam_slot_sess FOREIGN KEY (examination_session_id) REFERENCES examination_sessions(id) ON DELETE CASCADE,
  UNIQUE KEY uq_exam_slot_class (tenant_id, examination_session_id, exam_date, start_time, grade_id, stream_id),
  KEY idx_exam_slot_tenant_date (tenant_id, exam_date),
  KEY idx_exam_slot_teacher (tenant_id, invigilator_staff_id)
);

CREATE TABLE IF NOT EXISTS composite_weightings (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  grade_id BIGINT NULL,
  subject_id BIGINT NULL,
  continuous_assessment_weight DECIMAL(5,2) NOT NULL DEFAULT 30.00,
  examination_weight DECIMAL(5,2) NOT NULL DEFAULT 70.00,
  description VARCHAR(255) NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_composite_tenant_year_term_grade_subject (tenant_id, academic_year_id, term_id, grade_id, subject_id),
  KEY idx_composite_tenant_year_term (tenant_id, academic_year_id, term_id)
);

CREATE TABLE IF NOT EXISTS promotion_rules (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  from_grade_id BIGINT NOT NULL,
  to_grade_id BIGINT NOT NULL,
  minimum_aggregate DECIMAL(6,2) NOT NULL DEFAULT 50.00,
  minimum_average DECIMAL(5,2) NULL,
  minimum_attendance_percentage DECIMAL(5,2) NULL,
  max_failed_subjects INT NULL,
  required_subjects_json JSONB NULL,
  minimum_subject_score DECIMAL(5,2) NULL,
  description VARCHAR(500) NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_promotion_rule_tenant_year_from_to (tenant_id, academic_year_id, from_grade_id, to_grade_id)
);

CREATE TABLE IF NOT EXISTS promotion_decisions (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  from_grade_id BIGINT NOT NULL,
  to_grade_id BIGINT NOT NULL,
  from_academic_year_id BIGINT NOT NULL,
  to_academic_year_id BIGINT NOT NULL,
  from_term_id BIGINT NOT NULL,
  recommended_action VARCHAR(20) NOT NULL,
  final_action VARCHAR(20) NOT NULL,
  is_manual_override BOOLEAN NOT NULL DEFAULT 0,
  override_justification VARCHAR(1000) NULL,
  decided_by_user_id BIGINT NULL,
  decided_at TIMESTAMPTZ NULL,
  reason TEXT NOT NULL,
  aggregate_score DECIMAL(6,2) NOT NULL,
  average_score DECIMAL(5,2) NOT NULL,
  failed_subjects_count INT NOT NULL DEFAULT 0,
  attendance_percentage DECIMAL(5,2) NOT NULL DEFAULT 0.00,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  next_enrolment_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_promo_dec_tenant_student (tenant_id, student_id),
  KEY idx_promo_dec_tenant_year (tenant_id, from_academic_year_id, from_grade_id)
);

CREATE TABLE IF NOT EXISTS promotion_batches (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  batch_number VARCHAR(50) NOT NULL,
  from_academic_year_id BIGINT NOT NULL,
  to_academic_year_id BIGINT NOT NULL,
  from_grade_id BIGINT NOT NULL,
  to_grade_id BIGINT NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  total_students INT NOT NULL DEFAULT 0,
  promoted_count INT NOT NULL DEFAULT 0,
  repeat_count INT NOT NULL DEFAULT 0,
  conditional_count INT NOT NULL DEFAULT 0,
  failed_count INT NOT NULL DEFAULT 0,
  result_json JSONB NULL,
  started_at TIMESTAMPTZ NULL,
  completed_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_promo_batch_tenant_number (tenant_id, batch_number)
);

CREATE TABLE IF NOT EXISTS transcripts (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  transcript_number VARCHAR(50) NOT NULL,
  generated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  generated_by_user_id BIGINT NOT NULL,
  data_json JSONB NULL,
  pdf_url VARCHAR(500) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_transcript_tenant_number (tenant_id, transcript_number),
  KEY idx_transcript_tenant_student (tenant_id, student_id)
);

CREATE TABLE IF NOT EXISTS mark_moderations (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  assessment_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  subject_id BIGINT NULL,
  current_stage VARCHAR(20) NOT NULL DEFAULT 'teacher',
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  is_locked BOOLEAN NOT NULL DEFAULT 0,
  locked_at TIMESTAMPTZ NULL,
  locked_by_user_id BIGINT NULL,
  teacher_user_id BIGINT NULL,
  teacher_submitted_at TIMESTAMPTZ NULL,
  hod_user_id BIGINT NULL,
  hod_approved_at TIMESTAMPTZ NULL,
  head_user_id BIGINT NULL,
  head_approved_at TIMESTAMPTZ NULL,
  change_reason VARCHAR(1000) NULL,
  previous_score_json JSONB NULL,
  new_score_json JSONB NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_mark_mod_tenant_ass_student (tenant_id, assessment_id, student_id),
  KEY idx_mark_mod_tenant_ass (tenant_id, assessment_id)
);

CREATE TABLE IF NOT EXISTS certificates (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  certificate_type VARCHAR(30) NOT NULL,
  title VARCHAR(255) NOT NULL,
  description VARCHAR(500) NULL,
  data_json JSONB NULL,
  pdf_url VARCHAR(500) NULL,
  issued_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  issued_by_user_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_cert_tenant_student (tenant_id, student_id),
  KEY idx_cert_tenant_type (tenant_id, certificate_type)
);

-- Seed default composite weightings per subject and per level
INSERT INTO composite_weightings (tenant_id, academic_year_id, term_id, grade_id, subject_id, continuous_assessment_weight, examination_weight, description)
SELECT id, 2026, 1, NULL, NULL, 30.00, 70.00, 'Default CA 30% Exam 70% for all subjects and levels' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM composite_weightings WHERE tenant_id=tenants.id AND grade_id IS NULL AND subject_id IS NULL)
ON DUPLICATE KEY UPDATE continuous_assessment_weight=VALUES(continuous_assessment_weight);


-- ============================================================================
-- From V4_Fees.sql - LearnCloud.Fees
-- Original: /home/user/src/LearnCloud.Fees/Migrations/V4_Fees.sql
-- ============================================================================
-- LearnCloud Fees Module V4 - Money critical, decimal(18,2) + currency, tenant_id leading indexes
-- Scope: billing learners only

CREATE TABLE IF NOT EXISTS fee_items (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  code VARCHAR(20) NOT NULL,
  recurrence INT NOT NULL DEFAULT 1 COMMENT '1=PerTerm,2=PerYear,3=OneOff',
  is_proratable BOOLEAN NOT NULL DEFAULT 0,
  is_optional BOOLEAN NOT NULL DEFAULT 0,
  gl_code VARCHAR(50) NULL,
  description VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_fee_items_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_fee_items_tenant_code (tenant_id, code),
  KEY idx_fee_items_tenant (tenant_id)
);

CREATE TABLE IF NOT EXISTS fee_structures (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  grade_id BIGINT NULL,
  stream_id BIGINT NULL,
  student_id BIGINT NULL COMMENT 'individual override',
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  is_mandatory BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_fee_struct_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_fee_struct_tenant_year_term (tenant_id, academic_year_id, term_id),
  KEY idx_fee_struct_tenant_grade_stream (tenant_id, grade_id, stream_id),
  KEY idx_fee_struct_tenant_student (tenant_id, student_id)
);

CREATE TABLE IF NOT EXISTS fee_structure_items (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  fee_structure_id BIGINT NOT NULL,
  fee_item_id BIGINT NOT NULL,
  description VARCHAR(255) NOT NULL,
  amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  quantity INT NOT NULL DEFAULT 1,
  line_total DECIMAL(18,2) NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_fsi_structure FOREIGN KEY (fee_structure_id) REFERENCES fee_structures(id) ON DELETE CASCADE,
  CONSTRAINT fk_fsi_fee_item FOREIGN KEY (fee_item_id) REFERENCES fee_items(id) ON DELETE RESTRICT,
  KEY idx_fsi_tenant_structure (tenant_id, fee_structure_id)
);

CREATE TABLE IF NOT EXISTS discounts (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  type INT NOT NULL COMMENT '1=Percentage,2=Fixed',
  value DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  applies_to_fee_item_ids_json JSONB NULL,
  reason VARCHAR(255) NOT NULL,
  approver_user_id BIGINT NOT NULL,
  approved_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  status VARCHAR(20) NOT NULL DEFAULT 'approved',
  effective_from DATE NULL,
  effective_to DATE NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_discount_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_discount_tenant_student_year_term (tenant_id, student_id, academic_year_id, term_id),
  KEY idx_discount_tenant_status (tenant_id, status)
);

CREATE TABLE IF NOT EXISTS invoice_sequences (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  year INT NOT NULL,
  last_number INT NOT NULL DEFAULT 0,
  prefix VARCHAR(10) NOT NULL DEFAULT 'INV',
  format VARCHAR(50) NOT NULL DEFAULT '{prefix}-{year}-{number:5}',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_inv_seq_tenant_year (tenant_id, year),
  KEY idx_inv_seq_tenant (tenant_id)
);

CREATE TABLE IF NOT EXISTS receipt_sequences (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  year INT NOT NULL,
  last_number INT NOT NULL DEFAULT 0,
  prefix VARCHAR(10) NOT NULL DEFAULT 'REC',
  format VARCHAR(50) NOT NULL DEFAULT '{prefix}-{year}-{number:5}',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_rec_seq_tenant_year (tenant_id, year)
);

CREATE TABLE IF NOT EXISTS fee_invoices (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  invoice_number VARCHAR(50) NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  enrolment_id BIGINT NULL,
  fee_structure_id BIGINT NULL,
  structure_hash VARCHAR(128) NOT NULL COMMENT 'hash of structure for idempotent generation',
  subtotal_amount DECIMAL(18,2) NOT NULL,
  discount_amount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  total_amount DECIMAL(18,2) NOT NULL,
  amount_paid DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  balance_due DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  issue_date DATE NOT NULL,
  due_date DATE NOT NULL,
  status INT NOT NULL DEFAULT 2 COMMENT '1=Draft,2=Issued,3=Partial,4=Paid,5=Overdue,6=Void',
  is_prorated BOOLEAN NOT NULL DEFAULT 0,
  proration_note VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_fee_inv_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_fee_inv_tenant_number (tenant_id, invoice_number),
  UNIQUE KEY uq_fee_inv_tenant_student_term_hash (tenant_id, student_id, academic_year_id, term_id, structure_hash),
  KEY idx_fee_inv_tenant_student (tenant_id, student_id, academic_year_id, term_id),
  KEY idx_fee_inv_tenant_status_due (tenant_id, status, due_date),
  KEY idx_fee_inv_tenant_year_term (tenant_id, academic_year_id, term_id)
);

CREATE TABLE IF NOT EXISTS fee_invoice_items (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  invoice_id BIGINT NOT NULL,
  fee_structure_item_id BIGINT NULL,
  fee_item_id BIGINT NULL,
  description VARCHAR(255) NOT NULL,
  quantity INT NOT NULL DEFAULT 1,
  unit_amount DECIMAL(18,2) NOT NULL,
  line_total DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  is_prorated BOOLEAN NOT NULL DEFAULT 0,
  proration_detail VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_fee_inv_item_inv FOREIGN KEY (invoice_id) REFERENCES fee_invoices(id) ON DELETE CASCADE,
  KEY idx_fee_inv_item_tenant_invoice (tenant_id, invoice_id)
);

CREATE TABLE IF NOT EXISTS payments (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  method INT NOT NULL DEFAULT 1 COMMENT '1=Cash,2=Bank,3=EcoCash etc',
  reference VARCHAR(100) NULL,
  payment_date DATE NOT NULL,
  receipt_number VARCHAR(50) NOT NULL,
  proof_url VARCHAR(500) NULL,
  status INT NOT NULL DEFAULT 2 COMMENT '1=Pending,2=Confirmed,3=Reversed',
  reversed_by_payment_id BIGINT NULL,
  original_payment_id BIGINT NULL,
  reversal_reason VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_pay_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_pay_tenant_receipt (tenant_id, receipt_number),
  KEY idx_pay_tenant_student_date (tenant_id, student_id, payment_date),
  KEY idx_pay_tenant_reference (tenant_id, reference)
);

CREATE TABLE IF NOT EXISTS payment_allocations (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  payment_id BIGINT NOT NULL,
  invoice_id BIGINT NOT NULL,
  invoice_item_id BIGINT NULL,
  allocated_amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  is_manual_override BOOLEAN NOT NULL DEFAULT 0,
  is_reversal BOOLEAN NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_alloc_pay FOREIGN KEY (payment_id) REFERENCES payments(id) ON DELETE CASCADE,
  CONSTRAINT fk_alloc_inv FOREIGN KEY (invoice_id) REFERENCES fee_invoices(id) ON DELETE CASCADE,
  KEY idx_alloc_tenant_payment (tenant_id, payment_id),
  KEY idx_alloc_tenant_invoice (tenant_id, invoice_id)
);

CREATE TABLE IF NOT EXISTS learner_credits (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  source VARCHAR(20) NOT NULL DEFAULT 'overpayment',
  source_payment_id BIGINT NULL,
  source_credit_note_id BIGINT NULL,
  is_utilized BOOLEAN NOT NULL DEFAULT 0,
  utilized_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_credit_tenant_student (tenant_id, student_id, currency),
  KEY idx_credit_tenant_student_utilized (tenant_id, student_id, is_utilized)
);

CREATE TABLE IF NOT EXISTS credit_notes (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  invoice_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  reason VARCHAR(255) NOT NULL,
  approver_user_id BIGINT NOT NULL,
  approved_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  status VARCHAR(20) NOT NULL DEFAULT 'approved',
  credit_note_number VARCHAR(50) NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_cn_inv FOREIGN KEY (invoice_id) REFERENCES fee_invoices(id) ON DELETE CASCADE,
  UNIQUE KEY uq_cn_tenant_number (tenant_id, credit_note_number),
  KEY idx_cn_tenant_student (tenant_id, student_id)
);

CREATE TABLE IF NOT EXISTS fee_invoice_batches (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  batch_number VARCHAR(50) NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  total_students INT NOT NULL DEFAULT 0,
  processed INT NOT NULL DEFAULT 0,
  created_count INT NOT NULL DEFAULT 0,
  skipped_count INT NOT NULL DEFAULT 0,
  failed_count INT NOT NULL DEFAULT 0,
  result_json JSONB NULL,
  started_at TIMESTAMPTZ NULL,
  completed_at TIMESTAMPTZ NULL,
  progress_percent INT NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_batch_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_batch_tenant_number (tenant_id, batch_number),
  KEY idx_batch_tenant_year_term (tenant_id, academic_year_id, term_id)
);


-- ============================================================================
-- From V7_Finance.sql - LearnCloud.Finance
-- Original: /home/user/src/LearnCloud.Finance/Migrations/V7_Finance.sql
-- ============================================================================
-- Finance Full Module V7 - Expense categories, approval thresholds, expenses, suppliers, budgets, cash book, bank accounts, petty cash, period locking

-- Expense categories
CREATE TABLE IF NOT EXISTS expense_categories (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  code VARCHAR(20) NOT NULL,
  type VARCHAR(30) NOT NULL DEFAULT 'operational',
  parent_category_id BIGINT NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  description VARCHAR(255) NULL,
  gl_code VARCHAR(50) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_exp_cat_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  CONSTRAINT fk_exp_cat_parent FOREIGN KEY (parent_category_id) REFERENCES expense_categories(id) ON DELETE SET NULL,
  UNIQUE KEY uq_exp_cat_tenant_code (tenant_id, code),
  KEY idx_exp_cat_tenant_type (tenant_id, type)
);

-- Approval thresholds configurable
CREATE TABLE IF NOT EXISTS approval_thresholds (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  min_amount DECIMAL(18,2) NOT NULL,
  max_amount DECIMAL(18,2) NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  required_approver_role VARCHAR(50) NOT NULL,
  required_approvals INT NOT NULL DEFAULT 1,
  auto_approve BOOLEAN NOT NULL DEFAULT 0,
  approval_order INT NOT NULL DEFAULT 1,
  description VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_approval_tenant_order (tenant_id, approval_order),
  KEY idx_approval_tenant_amount (tenant_id, min_amount, max_amount)
);

-- Suppliers
CREATE TABLE IF NOT EXISTS suppliers (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(255) NOT NULL,
  code VARCHAR(20) NOT NULL,
  contact_person VARCHAR(100) NULL,
  email VARCHAR(255) NULL,
  phone VARCHAR(50) NULL,
  address VARCHAR(500) NULL,
  tax_id VARCHAR(50) NULL,
  bank_account_number VARCHAR(50) NULL,
  bank_name VARCHAR(100) NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  total_purchases DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_supplier_tenant_code (tenant_id, code),
  KEY idx_supplier_tenant_name (tenant_id, name)
);

-- Expenses
CREATE TABLE IF NOT EXISTS expenses (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  expense_number VARCHAR(20) NOT NULL,
  category_id BIGINT NOT NULL,
  supplier_id BIGINT NULL,
  purchase_record_id BIGINT NULL,
  description VARCHAR(500) NOT NULL,
  amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  expense_date DATE NOT NULL,
  academic_year_id BIGINT NULL,
  term_id BIGINT NULL,
  budget_id BIGINT NULL,
  status VARCHAR(30) NOT NULL DEFAULT 'draft',
  payment_method VARCHAR(30) NULL,
  bank_account_id BIGINT NULL,
  petty_cash_disbursement_id BIGINT NULL,
  supporting_document_url VARCHAR(500) NULL,
  notes TEXT NULL,
  created_by_user_id BIGINT NOT NULL,
  approved_by_user_id BIGINT NULL,
  approved_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_exp_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  CONSTRAINT fk_exp_category FOREIGN KEY (category_id) REFERENCES expense_categories(id) ON DELETE RESTRICT,
  CONSTRAINT fk_exp_supplier FOREIGN KEY (supplier_id) REFERENCES suppliers(id) ON DELETE SET NULL,
  UNIQUE KEY uq_exp_tenant_number (tenant_id, expense_number),
  KEY idx_exp_tenant_category_date (tenant_id, category_id, expense_date),
  KEY idx_exp_tenant_status (tenant_id, status),
  KEY idx_exp_tenant_year_term (tenant_id, academic_year_id, term_id)
);

-- Expense documents
CREATE TABLE IF NOT EXISTS expense_documents (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  expense_id BIGINT NOT NULL,
  file_name VARCHAR(255) NOT NULL,
  file_url VARCHAR(500) NOT NULL,
  file_size BIGINT NOT NULL,
  content_type VARCHAR(100) NOT NULL DEFAULT 'application/pdf',
  description VARCHAR(255) NULL,
  uploaded_by_user_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_exp_doc_exp FOREIGN KEY (expense_id) REFERENCES expenses(id) ON DELETE CASCADE,
  KEY idx_exp_doc_tenant_expense (tenant_id, expense_id)
);

-- Approval requests
CREATE TABLE IF NOT EXISTS approval_requests (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  expense_id BIGINT NOT NULL,
  threshold_id BIGINT NOT NULL,
  approver_user_id BIGINT NOT NULL,
  approver_role VARCHAR(50) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  comment VARCHAR(500) NULL,
  decided_at TIMESTAMPTZ NULL,
  approval_order INT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_approval_exp FOREIGN KEY (expense_id) REFERENCES expenses(id) ON DELETE CASCADE,
  CONSTRAINT fk_approval_threshold FOREIGN KEY (threshold_id) REFERENCES approval_thresholds(id) ON DELETE RESTRICT,
  KEY idx_approval_tenant_expense (tenant_id, expense_id),
  KEY idx_approval_tenant_status (tenant_id, status)
);

-- Budgets per category per term
CREATE TABLE IF NOT EXISTS budgets (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  category_id BIGINT NOT NULL,
  budgeted_amount DECIMAL(18,2) NOT NULL,
  actual_amount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  variance_amount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  variance_percentage DECIMAL(5,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  notes TEXT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_budget_category FOREIGN KEY (category_id) REFERENCES expense_categories(id) ON DELETE RESTRICT,
  UNIQUE KEY uq_budget_tenant_year_term_category (tenant_id, academic_year_id, term_id, category_id),
  KEY idx_budget_tenant_year_term (tenant_id, academic_year_id, term_id)
);

-- Bank accounts
CREATE TABLE IF NOT EXISTS bank_accounts (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  account_number VARCHAR(50) NOT NULL,
  bank_name VARCHAR(100) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  opening_balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  current_balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  account_type VARCHAR(20) NOT NULL DEFAULT 'bank',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_bank_acc_tenant_number (tenant_id, account_number),
  KEY idx_bank_acc_tenant_active (tenant_id, is_active)
);

-- Cash book entries
CREATE TABLE IF NOT EXISTS cash_book_entries (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  bank_account_id BIGINT NOT NULL,
  entry_date DATE NOT NULL,
  description VARCHAR(500) NOT NULL,
  reference VARCHAR(100) NOT NULL,
  debit DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  credit DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  entry_type VARCHAR(20) NOT NULL DEFAULT 'general',
  related_expense_id BIGINT NULL,
  related_fee_payment_id BIGINT NULL,
  related_platform_invoice_id BIGINT NULL,
  transfer_to_account_id BIGINT NULL,
  transfer_id VARCHAR(50) NULL,
  is_reconciled BOOLEAN NOT NULL DEFAULT 0,
  reconciled_at TIMESTAMPTZ NULL,
  reconciled_by_user_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_cash_bank FOREIGN KEY (bank_account_id) REFERENCES bank_accounts(id) ON DELETE CASCADE,
  KEY idx_cash_tenant_account_date (tenant_id, bank_account_id, entry_date),
  KEY idx_cash_tenant_reconciled (tenant_id, is_reconciled)
);

-- Bank statements and lines for reconciliation
CREATE TABLE IF NOT EXISTS bank_statements (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  bank_account_id BIGINT NOT NULL,
  file_name VARCHAR(255) NOT NULL,
  file_url VARCHAR(500) NOT NULL,
  statement_date DATE NOT NULL,
  from_date DATE NOT NULL,
  to_date DATE NOT NULL,
  opening_balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  closing_balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_stmt_bank FOREIGN KEY (bank_account_id) REFERENCES bank_accounts(id) ON DELETE CASCADE,
  KEY idx_stmt_tenant_account (tenant_id, bank_account_id)
);

CREATE TABLE IF NOT EXISTS bank_statement_lines (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  bank_statement_id BIGINT NOT NULL,
  bank_account_id BIGINT NOT NULL,
  transaction_date DATE NOT NULL,
  description VARCHAR(500) NOT NULL,
  amount DECIMAL(18,2) NOT NULL,
  debit DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  credit DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  is_reconciled BOOLEAN NOT NULL DEFAULT 0,
  matched_cash_book_entry_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_stmt_line_stmt FOREIGN KEY (bank_statement_id) REFERENCES bank_statements(id) ON DELETE CASCADE,
  KEY idx_stmt_line_tenant_reconciled (tenant_id, is_reconciled)
);

-- Petty cash
CREATE TABLE IF NOT EXISTS petty_cash_accounts (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  float_amount DECIMAL(18,2) NOT NULL,
  current_balance DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  custodian_user_id BIGINT NOT NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_petty_tenant_active (tenant_id, is_active)
);

CREATE TABLE IF NOT EXISTS petty_cash_disbursements (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  petty_cash_account_id BIGINT NOT NULL,
  amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  description VARCHAR(500) NOT NULL,
  recipient VARCHAR(255) NOT NULL,
  disbursement_date DATE NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'disbursed',
  receipt_url VARCHAR(500) NULL,
  related_expense_id BIGINT NULL,
  disbursed_by_user_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_petty_disb_acc FOREIGN KEY (petty_cash_account_id) REFERENCES petty_cash_accounts(id) ON DELETE CASCADE,
  KEY idx_petty_disb_tenant_account (tenant_id, petty_cash_account_id, disbursement_date)
);

CREATE TABLE IF NOT EXISTS petty_cash_reconciliations (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  petty_cash_account_id BIGINT NOT NULL,
  reconciliation_date DATE NOT NULL,
  float_amount DECIMAL(18,2) NOT NULL,
  disbursed_total DECIMAL(18,2) NOT NULL,
  cash_counted DECIMAL(18,2) NOT NULL,
  variance DECIMAL(18,2) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  notes TEXT NULL,
  reconciled_by_user_id BIGINT NOT NULL,
  approved_by_user_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_petty_rec_acc FOREIGN KEY (petty_cash_account_id) REFERENCES petty_cash_accounts(id) ON DELETE CASCADE,
  KEY idx_petty_rec_tenant_account (tenant_id, petty_cash_account_id)
);

-- Period locking
CREATE TABLE IF NOT EXISTS period_locks (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  is_locked BOOLEAN NOT NULL DEFAULT 0,
  locked_at TIMESTAMPTZ NULL,
  locked_by_user_id BIGINT NULL,
  lock_reason VARCHAR(500) NULL,
  is_unlocked BOOLEAN NOT NULL DEFAULT 0,
  unlocked_at TIMESTAMPTZ NULL,
  unlocked_by_user_id BIGINT NULL,
  unlock_reason VARCHAR(1000) NULL,
  unlock_approver_role VARCHAR(50) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_period_lock_tenant_year_term (tenant_id, academic_year_id, term_id),
  KEY idx_period_lock_tenant_locked (tenant_id, is_locked)
);

-- Seed default expense categories
INSERT INTO expense_categories (tenant_id, name, code, type, is_active, description, gl_code) 
SELECT id, 'Teaching Materials', 'TEACH_MAT', 'academic', 1, 'Books, lab materials, stationery for teaching', '5001' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM expense_categories WHERE tenant_id=tenants.id AND code='TEACH_MAT')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO expense_categories (tenant_id, name, code, type, is_active) 
SELECT id, 'Utilities', 'UTIL', 'operational', 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM expense_categories WHERE tenant_id=tenants.id AND code='UTIL')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO expense_categories (tenant_id, name, code, type, is_active) 
SELECT id, 'Maintenance', 'MAINT', 'operational', 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM expense_categories WHERE tenant_id=tenants.id AND code='MAINT')
ON DUPLICATE KEY UPDATE name=VALUES(name);

-- Seed default approval thresholds: <100 auto-approve, 100-500 bursar+head, >500 board
INSERT INTO approval_thresholds (tenant_id, min_amount, max_amount, currency, required_approver_role, required_approvals, auto_approve, approval_order, description)
SELECT id, 0.00, 100.00, 'USD', 'BURSAR', 1, 1, 1, 'Auto-approve under $100' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM approval_thresholds WHERE tenant_id=tenants.id AND min_amount=0.00 AND max_amount=100.00)
ON DUPLICATE KEY UPDATE required_approver_role=VALUES(required_approver_role);

INSERT INTO approval_thresholds (tenant_id, min_amount, max_amount, currency, required_approver_role, required_approvals, auto_approve, approval_order, description)
SELECT id, 100.00, 500.00, 'USD', 'HEAD_TEACHER', 1, 0, 2, 'Head approval $100-$500' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM approval_thresholds WHERE tenant_id=tenants.id AND min_amount=100.00 AND max_amount=500.00)
ON DUPLICATE KEY UPDATE required_approver_role=VALUES(required_approver_role);

INSERT INTO approval_thresholds (tenant_id, min_amount, max_amount, currency, required_approver_role, required_approvals, auto_approve, approval_order, description)
SELECT id, 500.00, NULL, 'USD', 'DIRECTOR', 1, 0, 3, 'Director/Board approval >$500' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM approval_thresholds WHERE tenant_id=tenants.id AND min_amount=500.00 AND max_amount IS NULL)
ON DUPLICATE KEY UPDATE required_approver_role=VALUES(required_approver_role);


-- ============================================================================
-- From V15_HR.sql - LearnCloud.HR
-- Original: /home/user/src/LearnCloud.HR/Migrations/V15_HR.sql
-- ============================================================================
-- HR Module V15 - Staff records, contracts expiry reminders, qualifications and documents, leave types entitlements, leave request approval workflow balance calculation leave calendar, appraisal cycles criteria, disciplinary restricted access, staff reporting headcount turnover leave liability

CREATE TABLE IF NOT EXISTS departments (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  code VARCHAR(20) NOT NULL,
  hod_staff_id BIGINT NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_dept_tenant_code (tenant_id, code),
  KEY idx_dept_tenant_active (tenant_id, is_active)
);

CREATE TABLE IF NOT EXISTS staff (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  staff_number VARCHAR(20) NOT NULL,
  first_name VARCHAR(100) NOT NULL,
  last_name VARCHAR(100) NOT NULL,
  national_id VARCHAR(50) NULL,
  date_of_birth DATE NULL,
  gender VARCHAR(20) NOT NULL DEFAULT 'other',
  employment_type VARCHAR(20) NOT NULL DEFAULT 'permanent',
  employment_status VARCHAR(20) NOT NULL DEFAULT 'active',
  department_id BIGINT NULL,
  designation VARCHAR(100) NULL,
  hire_date DATE NOT NULL,
  confirmation_date DATE NULL,
  phone VARCHAR(50) NULL,
  email VARCHAR(255) NULL,
  address VARCHAR(500) NULL,
  user_id BIGINT NULL,
  photo_url VARCHAR(500) NULL,
  current_salary DECIMAL(18,2) NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_staff_tenant_number (tenant_id, staff_number),
  KEY idx_staff_tenant_status (tenant_id, employment_status),
  KEY idx_staff_tenant_dept (tenant_id, department_id)
);

CREATE TABLE IF NOT EXISTS contracts (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  staff_id BIGINT NOT NULL,
  contract_number VARCHAR(20) NOT NULL,
  contract_type VARCHAR(20) NOT NULL DEFAULT 'permanent',
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  probation_end_date DATE NULL,
  salary DECIMAL(18,2) NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  terms TEXT NULL,
  last_reminder_sent_at TIMESTAMPTZ NULL,
  created_by_user_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_contract_tenant_number (tenant_id, contract_number),
  KEY idx_contract_tenant_staff (tenant_id, staff_id),
  KEY idx_contract_tenant_end_date (tenant_id, end_date, status)
);

CREATE TABLE IF NOT EXISTS qualifications (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  staff_id BIGINT NOT NULL,
  qualification_name VARCHAR(255) NOT NULL,
  institution VARCHAR(255) NOT NULL,
  year_obtained INT NULL,
  grade VARCHAR(50) NULL,
  certificate_number VARCHAR(100) NULL,
  is_verified BOOLEAN NOT NULL DEFAULT 0,
  verified_by_user_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_qual_tenant_staff (tenant_id, staff_id)
);

CREATE TABLE IF NOT EXISTS staff_documents (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  staff_id BIGINT NOT NULL,
  document_type VARCHAR(50) NOT NULL,
  file_name VARCHAR(255) NOT NULL,
  file_url VARCHAR(500) NOT NULL,
  file_size BIGINT NOT NULL,
  content_type VARCHAR(100) NOT NULL DEFAULT 'application/pdf',
  expiry_date DATE NULL,
  is_verified BOOLEAN NOT NULL DEFAULT 0,
  uploaded_by_user_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_doc_tenant_staff (tenant_id, staff_id)
);

CREATE TABLE IF NOT EXISTS leave_types (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(50) NOT NULL,
  code VARCHAR(20) NOT NULL,
  description VARCHAR(255) NOT NULL DEFAULT '',
  default_entitlement_days INT NOT NULL DEFAULT 0,
  is_paid BOOLEAN NOT NULL DEFAULT 1,
  requires_document BOOLEAN NOT NULL DEFAULT 0,
  is_carry_forward_allowed BOOLEAN NOT NULL DEFAULT 0,
  max_carry_forward_days INT NOT NULL DEFAULT 0,
  accrual_rule VARCHAR(20) NOT NULL DEFAULT 'yearly',
  is_active BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_leave_type_tenant_code (tenant_id, code)
);

CREATE TABLE IF NOT EXISTS leave_entitlements (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  staff_id BIGINT NOT NULL,
  leave_type_id BIGINT NOT NULL,
  academic_year INT NOT NULL,
  entitled_days DECIMAL(5,2) NOT NULL,
  carried_forward_days DECIMAL(5,2) NOT NULL DEFAULT 0.00,
  used_days DECIMAL(5,2) NOT NULL DEFAULT 0.00,
  remaining_days DECIMAL(5,2) NOT NULL,
  expiry_date DATE NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_entitlement_tenant_staff_type_year (tenant_id, staff_id, leave_type_id, academic_year),
  KEY idx_entitlement_tenant_staff_year (tenant_id, staff_id, academic_year)
);

CREATE TABLE IF NOT EXISTS leave_requests (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  staff_id BIGINT NOT NULL,
  leave_type_id BIGINT NOT NULL,
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  days_requested DECIMAL(5,2) NOT NULL,
  reason VARCHAR(500) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  approver_user_id BIGINT NULL,
  approved_at TIMESTAMPTZ NULL,
  approver_comment VARCHAR(500) NULL,
  document_url VARCHAR(500) NULL,
  requested_by_user_id BIGINT NOT NULL,
  is_half_day BOOLEAN NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_leave_req_tenant_staff (tenant_id, staff_id),
  KEY idx_leave_req_tenant_status (tenant_id, status),
  KEY idx_leave_req_tenant_dates (tenant_id, start_date, end_date)
);

CREATE TABLE IF NOT EXISTS appraisal_cycles (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NULL,
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  description VARCHAR(500) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_app_cycle_tenant_year (tenant_id, academic_year_id)
);

CREATE TABLE IF NOT EXISTS appraisal_criteria (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  appraisal_cycle_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  description VARCHAR(255) NULL,
  weight INT NOT NULL DEFAULT 1,
  max_score INT NOT NULL DEFAULT 5,
  sort_order INT NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_criteria_cycle FOREIGN KEY (appraisal_cycle_id) REFERENCES appraisal_cycles(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS appraisals (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  appraisal_cycle_id BIGINT NOT NULL,
  staff_id BIGINT NOT NULL,
  appraiser_user_id BIGINT NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  overall_score DECIMAL(5,2) NOT NULL DEFAULT 0.00,
  overall_comment TEXT NULL,
  staff_comment TEXT NULL,
  submitted_at TIMESTAMPTZ NULL,
  acknowledged_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_appraisal_cycle FOREIGN KEY (appraisal_cycle_id) REFERENCES appraisal_cycles(id) ON DELETE CASCADE,
  UNIQUE KEY uq_appraisal_tenant_cycle_staff (tenant_id, appraisal_cycle_id, staff_id)
);

CREATE TABLE IF NOT EXISTS appraisal_scores (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  appraisal_id BIGINT NOT NULL,
  criterion_id BIGINT NOT NULL,
  score INT NOT NULL,
  comment VARCHAR(500) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_score_appraisal FOREIGN KEY (appraisal_id) REFERENCES appraisals(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS disciplinary_records (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  staff_id BIGINT NOT NULL,
  incident_date DATE NOT NULL,
  incident_type VARCHAR(50) NOT NULL,
  title VARCHAR(255) NOT NULL,
  description TEXT NOT NULL,
  severity VARCHAR(20) NOT NULL DEFAULT 'low',
  action_taken VARCHAR(500) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'open',
  reported_by_user_id BIGINT NOT NULL,
  assigned_to_user_id BIGINT NULL,
  visibility VARCHAR(20) NOT NULL DEFAULT 'hr_only',
  is_confidential BOOLEAN NOT NULL DEFAULT 1,
  resolution_date DATE NULL,
  resolution_notes TEXT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_disc_tenant_staff (tenant_id, staff_id),
  KEY idx_disc_tenant_visibility (tenant_id, visibility)
);

-- Seed default leave types per tenant
INSERT INTO leave_types (tenant_id, name, code, description, default_entitlement_days, is_paid, requires_document, is_carry_forward_allowed, max_carry_forward_days, accrual_rule)
SELECT id, 'Annual Leave', 'ANNUAL', 'Annual leave per NEC', 22, 1, 0, 1, 5, 'yearly' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM leave_types WHERE tenant_id=tenants.id AND code='ANNUAL')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO leave_types (tenant_id, name, code, description, default_entitlement_days, is_paid, requires_document, is_carry_forward_allowed, max_carry_forward_days, accrual_rule)
SELECT id, 'Sick Leave', 'SICK', '90 days full pay per NEC', 90, 1, 1, 0, 0, 'yearly' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM leave_types WHERE tenant_id=tenants.id AND code='SICK')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO leave_types (tenant_id, name, code, description, default_entitlement_days, is_paid, requires_document, is_carry_forward_allowed, max_carry_forward_days, accrual_rule)
SELECT id, 'Maternity Leave', 'MATERNITY', '98 days per labour act', 98, 1, 1, 0, 0, 'none' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM leave_types WHERE tenant_id=tenants.id AND code='MATERNITY')
ON DUPLICATE KEY UPDATE name=VALUES(name);

-- Seed departments
INSERT INTO departments (tenant_id, name, code, is_active)
SELECT id, 'Administration', 'ADMIN', 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM departments WHERE tenant_id=tenants.id AND code='ADMIN')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO departments (tenant_id, name, code, is_active)
SELECT id, 'Sciences', 'SCI', 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM departments WHERE tenant_id=tenants.id AND code='SCI')
ON DUPLICATE KEY UPDATE name=VALUES(name);


-- ============================================================================
-- From V14_Hostel.sql - LearnCloud.Hostel
-- Original: /home/user/src/LearnCloud.Hostel/Migrations/V14_Hostel.sql
-- ============================================================================
-- Hostel and Boarding Module V14 - Blocks, rooms, beds with gender, allocation with conflict detection and waiting list, house masters matrons, boarding fees integrated, exeat leave register, nightly roll call, visitor log, incident sick bay with access restrictions, reports occupancy vacancies on leave boarding revenue, printable bed allocation and leave register for gate

CREATE TABLE IF NOT EXISTS hostel_blocks (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  code VARCHAR(20) NOT NULL,
  gender_designation VARCHAR(20) NOT NULL DEFAULT 'male',
  capacity INT NOT NULL,
  total_rooms INT NOT NULL DEFAULT 0,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  description VARCHAR(500) NULL,
  location VARCHAR(100) NULL,
  house_master_staff_id BIGINT NULL,
  matron_staff_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_block_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_block_tenant_code (tenant_id, code),
  KEY idx_block_tenant_gender (tenant_id, gender_designation, is_active)
);

CREATE TABLE IF NOT EXISTS hostel_rooms (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  room_number VARCHAR(20) NOT NULL,
  floor INT NOT NULL DEFAULT 0,
  capacity INT NOT NULL,
  gender_designation VARCHAR(20) NOT NULL DEFAULT 'male',
  is_active BOOLEAN NOT NULL DEFAULT 1,
  facilities VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_room_block FOREIGN KEY (block_id) REFERENCES hostel_blocks(id) ON DELETE CASCADE,
  UNIQUE KEY uq_room_tenant_block_number (tenant_id, block_id, room_number),
  KEY idx_room_tenant_block (tenant_id, block_id)
);

CREATE TABLE IF NOT EXISTS hostel_beds (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  room_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  bed_number VARCHAR(20) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'available',
  "condition" VARCHAR(20) NOT NULL DEFAULT 'good',
  current_student_id BIGINT NULL,
  current_allocation_id BIGINT NULL,
  last_issued_at TIMESTAMPTZ NULL,
  last_returned_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_bed_room FOREIGN KEY (room_id) REFERENCES hostel_rooms(id) ON DELETE CASCADE,
  CONSTRAINT fk_bed_block FOREIGN KEY (block_id) REFERENCES hostel_blocks(id) ON DELETE CASCADE,
  UNIQUE KEY uq_bed_tenant_room_number (tenant_id, room_id, bed_number),
  KEY idx_bed_tenant_status (tenant_id, status),
  KEY idx_bed_tenant_block (tenant_id, block_id)
);

CREATE TABLE IF NOT EXISTS block_staff_assignments (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  staff_id BIGINT NOT NULL,
  role VARCHAR(30) NOT NULL DEFAULT 'house_master',
  assigned_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  unassigned_date TIMESTAMPTZ NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  responsibilities VARCHAR(500) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_block_staff_block FOREIGN KEY (block_id) REFERENCES hostel_blocks(id) ON DELETE CASCADE,
  KEY idx_block_staff_tenant_block_active (tenant_id, block_id, is_active)
);

CREATE TABLE IF NOT EXISTS bed_allocations (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  bed_id BIGINT NOT NULL,
  room_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  allocation_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  allocated_by_user_id BIGINT NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  vacated_date TIMESTAMPTZ NULL,
  vacated_reason VARCHAR(255) NULL,
  fee_applied BOOLEAN NOT NULL DEFAULT 0,
  fee_structure_item_id BIGINT NULL,
  waiting_list_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_alloc_bed FOREIGN KEY (bed_id) REFERENCES hostel_beds(id) ON DELETE RESTRICT,
  CONSTRAINT fk_alloc_room FOREIGN KEY (room_id) REFERENCES hostel_rooms(id) ON DELETE RESTRICT,
  CONSTRAINT fk_alloc_block FOREIGN KEY (block_id) REFERENCES hostel_blocks(id) ON DELETE RESTRICT,
  UNIQUE KEY uq_alloc_tenant_bed_active (tenant_id, bed_id, status, academic_year_id, term_id),
  UNIQUE KEY uq_alloc_tenant_student_year_term (tenant_id, student_id, academic_year_id, term_id),
  KEY idx_alloc_tenant_block (tenant_id, block_id, status),
  KEY idx_alloc_tenant_student (tenant_id, student_id)
);

CREATE TABLE IF NOT EXISTS waiting_lists (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  preferred_block_id BIGINT NULL,
  preferred_room_id BIGINT NULL,
  gender VARCHAR(20) NOT NULL DEFAULT 'male',
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  priority INT NOT NULL DEFAULT 0,
  queue_position INT NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'waiting',
  reason VARCHAR(255) NULL,
  requested_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  requested_by_user_id BIGINT NOT NULL,
  allocated_at TIMESTAMPTZ NULL,
  allocated_bed_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_waiting_tenant_year_term_status (tenant_id, academic_year_id, term_id, status, queue_position)
);

CREATE TABLE IF NOT EXISTS boarding_fee_links (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  bed_allocation_id BIGINT NOT NULL,
  fee_item_id BIGINT NOT NULL,
  fee_structure_id BIGINT NOT NULL,
  fee_structure_item_id BIGINT NOT NULL,
  amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_board_fee_alloc FOREIGN KEY (bed_allocation_id) REFERENCES bed_allocations(id) ON DELETE CASCADE,
  KEY idx_board_fee_tenant_alloc (tenant_id, bed_allocation_id)
);

CREATE TABLE IF NOT EXISTS exeat_registers (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  bed_allocation_id BIGINT NULL,
  leave_type VARCHAR(20) NOT NULL DEFAULT 'exeat',
  reason VARCHAR(500) NOT NULL,
  departure_date_time TIMESTAMPTZ NOT NULL,
  expected_return_date_time TIMESTAMPTZ NOT NULL,
  actual_return_date_time TIMESTAMPTZ NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'approved',
  authorised_by_user_id BIGINT NOT NULL,
  authoriser_role VARCHAR(30) NOT NULL DEFAULT 'house_master',
  approved_by_user_id BIGINT NULL,
  contact_phone_during_leave VARCHAR(50) NULL,
  destination_address VARCHAR(500) NULL,
  accompanying_person VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_exeat_block FOREIGN KEY (block_id) REFERENCES hostel_blocks(id) ON DELETE CASCADE,
  KEY idx_exeat_tenant_student (tenant_id, student_id, status),
  KEY idx_exeat_tenant_block (tenant_id, block_id, status),
  KEY idx_exeat_tenant_dates (tenant_id, departure_date_time, expected_return_date_time)
);

CREATE TABLE IF NOT EXISTS roll_calls (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  room_id BIGINT NULL,
  roll_call_date DATE NOT NULL,
  roll_call_type VARCHAR(20) NOT NULL DEFAULT 'nightly',
  status VARCHAR(20) NOT NULL DEFAULT 'in_progress',
  conducted_by_user_id BIGINT NOT NULL,
  completed_at TIMESTAMPTZ NULL,
  total_expected INT NOT NULL DEFAULT 0,
  total_present INT NOT NULL DEFAULT 0,
  total_absent INT NOT NULL DEFAULT 0,
  total_on_leave INT NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_rollcall_block FOREIGN KEY (block_id) REFERENCES hostel_blocks(id) ON DELETE CASCADE,
  UNIQUE KEY uq_rollcall_tenant_block_date_type (tenant_id, block_id, roll_call_date, roll_call_type),
  KEY idx_rollcall_tenant_date (tenant_id, roll_call_date)
);

CREATE TABLE IF NOT EXISTS roll_call_entries (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  roll_call_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  room_id BIGINT NULL,
  bed_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  roll_call_date DATE NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'present',
  notes VARCHAR(255) NULL,
  marked_by_user_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_roll_entry_rollcall FOREIGN KEY (roll_call_id) REFERENCES roll_calls(id) ON DELETE CASCADE,
  KEY idx_roll_entry_tenant_rollcall (tenant_id, roll_call_id),
  KEY idx_roll_entry_tenant_student (tenant_id, student_id, roll_call_date)
);

CREATE TABLE IF NOT EXISTS visitor_logs (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  visitor_name VARCHAR(255) NOT NULL,
  relationship VARCHAR(50) NOT NULL,
  id_number VARCHAR(50) NOT NULL,
  phone VARCHAR(50) NOT NULL,
  check_in_date_time TIMESTAMPTZ NOT NULL,
  check_out_date_time TIMESTAMPTZ NULL,
  purpose VARCHAR(500) NOT NULL,
  authorised_by_user_id BIGINT NOT NULL,
  belongings VARCHAR(500) NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'checked_in',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_visitor_tenant_student (tenant_id, student_id, check_in_date_time),
  KEY idx_visitor_tenant_block (tenant_id, block_id)
);

CREATE TABLE IF NOT EXISTS incident_records (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  room_id BIGINT NULL,
  student_id BIGINT NOT NULL,
  incident_type VARCHAR(30) NOT NULL DEFAULT 'incident',
  title VARCHAR(255) NOT NULL,
  description TEXT NOT NULL,
  severity VARCHAR(20) NOT NULL DEFAULT 'low',
  incident_date_time TIMESTAMPTZ NOT NULL,
  reported_by_user_id BIGINT NOT NULL,
  action_taken TEXT NULL,
  follow_up_required TEXT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'open',
  visibility VARCHAR(30) NOT NULL DEFAULT 'house_master',
  is_confidential BOOLEAN NOT NULL DEFAULT 0,
  assigned_to_user_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_incident_tenant_student (tenant_id, student_id),
  KEY idx_incident_tenant_block (tenant_id, block_id),
  KEY idx_incident_tenant_visibility (tenant_id, visibility)
);

CREATE TABLE IF NOT EXISTS sick_bay_records (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  check_in_date_time TIMESTAMPTZ NOT NULL,
  check_out_date_time TIMESTAMPTZ NULL,
  symptoms TEXT NOT NULL,
  diagnosis VARCHAR(500) NULL,
  treatment TEXT NULL,
  medication VARCHAR(500) NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'admitted',
  admitted_by_user_id BIGINT NOT NULL,
  discharged_by_user_id BIGINT NULL,
  visibility VARCHAR(30) NOT NULL DEFAULT 'matron',
  is_confidential BOOLEAN NOT NULL DEFAULT 1,
  notes TEXT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_sickbay_tenant_student (tenant_id, student_id),
  KEY idx_sickbay_tenant_block (tenant_id, block_id)
);

-- Seed default boarding fee item per tenant
INSERT INTO fee_items (tenant_id, name, code, recurrence, is_proratable, is_optional, description)
SELECT id, 'Boarding', 'BOARDING', 1, 0, 1, 'Boarding fee per term - flows into existing fee invoicing' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM fee_items WHERE tenant_id=tenants.id AND code='BOARDING')
ON DUPLICATE KEY UPDATE name=VALUES(name);


-- ============================================================================
-- From V12_Library.sql - LearnCloud.Library
-- Original: /home/user/src/LearnCloud.Library/Migrations/V12_Library.sql
-- ============================================================================
-- Library Module V12 - Catalogue, copies/barcode, membership, issue/return, renewals, reservations, fines posting to fee account, lost/damaged, stock take, reports

CREATE TABLE IF NOT EXISTS library_categories (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  code VARCHAR(20) NOT NULL,
  description VARCHAR(255) NULL,
  parent_category_id BIGINT NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_lib_cat_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_lib_cat_tenant_code (tenant_id, code)
);

CREATE TABLE IF NOT EXISTS books (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  title VARCHAR(255) NOT NULL,
  author VARCHAR(255) NOT NULL,
  isbn VARCHAR(20) NULL,
  category_id BIGINT NOT NULL,
  publisher VARCHAR(100) NULL,
  publication_year INT NULL,
  edition VARCHAR(20) NULL,
  shelf_location VARCHAR(50) NOT NULL,
  total_copies INT NOT NULL DEFAULT 0,
  available_copies INT NOT NULL DEFAULT 0,
  replacement_price DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  description TEXT NULL,
  cover_image_url VARCHAR(500) NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_books_category FOREIGN KEY (category_id) REFERENCES library_categories(id) ON DELETE RESTRICT,
  KEY idx_books_tenant_category (tenant_id, category_id),
  KEY idx_books_tenant_title (tenant_id, title),
  FULLTEXT KEY ft_books_title_author (title, author)
);

CREATE TABLE IF NOT EXISTS book_copies (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  book_id BIGINT NOT NULL,
  accession_number VARCHAR(50) NOT NULL,
  barcode VARCHAR(50) NOT NULL,
  shelf_location VARCHAR(50) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'available',
  "condition" VARCHAR(20) NOT NULL DEFAULT 'good',
  last_issued_at TIMESTAMPTZ NULL,
  last_returned_at TIMESTAMPTZ NULL,
  current_loan_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_copy_book FOREIGN KEY (book_id) REFERENCES books(id) ON DELETE CASCADE,
  UNIQUE KEY uq_copy_tenant_accession (tenant_id, accession_number),
  UNIQUE KEY uq_copy_tenant_barcode (tenant_id, barcode),
  KEY idx_copy_tenant_book_status (tenant_id, book_id, status),
  KEY idx_copy_tenant_status (tenant_id, status)
);

CREATE TABLE IF NOT EXISTS membership_configs (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  membership_type VARCHAR(20) NOT NULL,
  max_books INT NOT NULL DEFAULT 3,
  loan_period_days INT NOT NULL DEFAULT 14,
  max_renewals INT NOT NULL DEFAULT 1,
  fine_per_day DECIMAL(18,2) NOT NULL DEFAULT 1.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  max_fine DECIMAL(18,2) NOT NULL DEFAULT 50.00,
  allow_reservations BOOLEAN NOT NULL DEFAULT 1,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_membership_config_tenant_type (tenant_id, membership_type)
);

CREATE TABLE IF NOT EXISTS library_members (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  membership_number VARCHAR(20) NOT NULL,
  member_type VARCHAR(20) NOT NULL,
  student_id BIGINT NULL,
  staff_id BIGINT NULL,
  user_id BIGINT NULL,
  full_name VARCHAR(255) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  currently_borrowed INT NOT NULL DEFAULT 0,
  total_borrowed INT NOT NULL DEFAULT 0,
  outstanding_fines DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  membership_expiry_date DATE NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_lib_member_tenant_number (tenant_id, membership_number),
  KEY idx_lib_member_tenant_student (tenant_id, student_id),
  KEY idx_lib_member_tenant_staff (tenant_id, staff_id)
);

CREATE TABLE IF NOT EXISTS loans (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  book_copy_id BIGINT NOT NULL,
  book_id BIGINT NOT NULL,
  member_id BIGINT NOT NULL,
  student_id BIGINT NULL,
  staff_id BIGINT NULL,
  issue_date TIMESTAMPTZ NOT NULL,
  due_date TIMESTAMPTZ NOT NULL,
  return_date TIMESTAMPTZ NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'issued',
  issued_by_user_id BIGINT NOT NULL,
  returned_by_user_id BIGINT NULL,
  renewal_count INT NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_loan_copy FOREIGN KEY (book_copy_id) REFERENCES book_copies(id) ON DELETE RESTRICT,
  KEY idx_loan_tenant_member (tenant_id, member_id, status),
  KEY idx_loan_tenant_book (tenant_id, book_id),
  KEY idx_loan_tenant_due (tenant_id, due_date, status),
  KEY idx_loan_tenant_student (tenant_id, student_id)
);

CREATE TABLE IF NOT EXISTS loan_renewals (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  loan_id BIGINT NOT NULL,
  renewal_date TIMESTAMPTZ NOT NULL,
  previous_due_date TIMESTAMPTZ NOT NULL,
  new_due_date TIMESTAMPTZ NOT NULL,
  renewal_number INT NOT NULL,
  approved_by_user_id BIGINT NOT NULL,
  reason VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_renewal_loan FOREIGN KEY (loan_id) REFERENCES loans(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS reservations (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  book_id BIGINT NOT NULL,
  member_id BIGINT NOT NULL,
  student_id BIGINT NULL,
  reservation_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  expiry_date TIMESTAMPTZ NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  queue_position INT NOT NULL,
  fulfilled_at TIMESTAMPTZ NULL,
  fulfilled_loan_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_res_tenant_book_status (tenant_id, book_id, status, queue_position),
  KEY idx_res_tenant_member (tenant_id, member_id)
);

CREATE TABLE IF NOT EXISTS fines (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  loan_id BIGINT NOT NULL,
  member_id BIGINT NOT NULL,
  student_id BIGINT NULL,
  fine_type VARCHAR(20) NOT NULL DEFAULT 'overdue',
  amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  days_overdue INT NOT NULL DEFAULT 0,
  fine_per_day DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  posted_to_fee_account BOOLEAN NOT NULL DEFAULT 0,
  fee_invoice_id BIGINT NULL,
  fee_invoice_item_id BIGINT NULL,
  paid_at TIMESTAMPTZ NULL,
  waived_reason VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_fine_loan FOREIGN KEY (loan_id) REFERENCES loans(id) ON DELETE CASCADE,
  KEY idx_fine_tenant_member_status (tenant_id, member_id, status),
  KEY idx_fine_tenant_student (tenant_id, student_id)
);

CREATE TABLE IF NOT EXISTS lost_damaged_records (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  book_copy_id BIGINT NOT NULL,
  loan_id BIGINT NOT NULL,
  member_id BIGINT NOT NULL,
  type VARCHAR(20) NOT NULL,
  "condition" VARCHAR(50) NOT NULL,
  replacement_charge DECIMAL(18,2) NOT NULL,
  fine_amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  fee_invoice_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_lost_copy FOREIGN KEY (book_copy_id) REFERENCES book_copies(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS stock_takes (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  completed_at TIMESTAMPTZ NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'in_progress',
  created_by_user_id BIGINT NOT NULL,
  total_expected INT NOT NULL DEFAULT 0,
  total_counted INT NOT NULL DEFAULT 0,
  discrepancies INT NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_stock_take_tenant_status (tenant_id, status)
);

CREATE TABLE IF NOT EXISTS stock_take_items (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  stock_take_id BIGINT NOT NULL,
  book_copy_id BIGINT NOT NULL,
  expected_status VARCHAR(20) NOT NULL,
  counted_status VARCHAR(20) NOT NULL,
  discrepancy_type VARCHAR(20) NOT NULL DEFAULT 'none',
  notes VARCHAR(255) NULL,
  counted_by_user_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_stock_item_take FOREIGN KEY (stock_take_id) REFERENCES stock_takes(id) ON DELETE CASCADE,
  CONSTRAINT fk_stock_item_copy FOREIGN KEY (book_copy_id) REFERENCES book_copies(id) ON DELETE RESTRICT,
  KEY idx_stock_item_tenant_take (tenant_id, stock_take_id)
);

-- Seed default membership configs per tenant
INSERT INTO membership_configs (tenant_id, membership_type, max_books, loan_period_days, max_renewals, fine_per_day, currency, max_fine, allow_reservations)
SELECT id, 'student', 3, 14, 1, 1.00, 'USD', 50.00, 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM membership_configs WHERE tenant_id=tenants.id AND membership_type='student')
ON DUPLICATE KEY UPDATE max_books=VALUES(max_books);

INSERT INTO membership_configs (tenant_id, membership_type, max_books, loan_period_days, max_renewals, fine_per_day, currency, max_fine, allow_reservations)
SELECT id, 'teacher', 10, 30, 2, 0.50, 'USD', 50.00, 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM membership_configs WHERE tenant_id=tenants.id AND membership_type='teacher')
ON DUPLICATE KEY UPDATE max_books=VALUES(max_books);

INSERT INTO membership_configs (tenant_id, membership_type, max_books, loan_period_days, max_renewals, fine_per_day, currency, max_fine, allow_reservations)
SELECT id, 'staff', 5, 21, 1, 1.00, 'USD', 50.00, 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM membership_configs WHERE tenant_id=tenants.id AND membership_type='staff')
ON DUPLICATE KEY UPDATE max_books=VALUES(max_books);

-- Seed default library categories
INSERT INTO library_categories (tenant_id, name, code, is_active) 
SELECT id, 'Fiction', 'FIC', 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM library_categories WHERE tenant_id=tenants.id AND code='FIC')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO library_categories (tenant_id, name, code, is_active) 
SELECT id, 'Non-Fiction', 'NONFIC', 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM library_categories WHERE tenant_id=tenants.id AND code='NONFIC')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO library_categories (tenant_id, name, code, is_active) 
SELECT id, 'Textbooks', 'TEXT', 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM library_categories WHERE tenant_id=tenants.id AND code='TEXT')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO library_categories (tenant_id, name, code, is_active) 
SELECT id, 'Reference', 'REF', 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM library_categories WHERE tenant_id=tenants.id AND code='REF')
ON DUPLICATE KEY UPDATE name=VALUES(name);


-- ============================================================================
-- From V5_Messaging.sql - LearnCloud.Messaging
-- Original: /home/user/src/LearnCloud.Messaging/Migrations/V5_Messaging.sql
-- ============================================================================
-- LearnCloud Messaging Module V5 - SMS & Email to guardians
-- Provider abstraction, audience, templates merge fields, delivery log, usage counter, cap, opt-out

CREATE TABLE IF NOT EXISTS message_templates (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  code VARCHAR(50) NOT NULL,
  channel INT NOT NULL COMMENT '1=Sms,2=Email,3=Portal',
  subject VARCHAR(255) NOT NULL DEFAULT '',
  body TEXT NOT NULL,
  is_system BOOLEAN NOT NULL DEFAULT 0,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  description VARCHAR(500) NULL,
  merge_fields_json JSONB NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_msg_tpl_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_msg_tpl_tenant_code (tenant_id, code),
  KEY idx_msg_tpl_tenant_channel (tenant_id, channel)
);

CREATE TABLE IF NOT EXISTS message_batches (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  batch_number VARCHAR(50) NOT NULL,
  title VARCHAR(255) NOT NULL,
  template_id BIGINT NULL,
  channel INT NOT NULL,
  audience_type INT NOT NULL,
  audience_filter_json JSONB NOT NULL,
  body TEXT NOT NULL,
  subject VARCHAR(500) NULL,
  total_recipients INT NOT NULL DEFAULT 0,
  sent_count INT NOT NULL DEFAULT 0,
  delivered_count INT NOT NULL DEFAULT 0,
  failed_count INT NOT NULL DEFAULT 0,
  estimated_cost DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  actual_cost DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  status INT NOT NULL DEFAULT 1 COMMENT '1=Draft,2=Queued,3=Sending,4=Sent,5=Delivered,6=Failed,7=Cancelled',
  queued_at TIMESTAMPTZ NULL,
  started_at TIMESTAMPTZ NULL,
  completed_at TIMESTAMPTZ NULL,
  created_by_user_id BIGINT NOT NULL,
  cost_estimate_json JSONB NULL,
  is_previewed BOOLEAN NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_msg_batch_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  CONSTRAINT fk_msg_batch_template FOREIGN KEY (template_id) REFERENCES message_templates(id) ON DELETE SET NULL,
  UNIQUE KEY uq_msg_batch_tenant_number (tenant_id, batch_number),
  KEY idx_msg_batch_tenant_status (tenant_id, status),
  KEY idx_msg_batch_tenant_created (tenant_id, created_at)
);

CREATE TABLE IF NOT EXISTS message_delivery_logs (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  batch_id BIGINT NOT NULL,
  guardian_id BIGINT NULL,
  student_id BIGINT NULL,
  recipient_name VARCHAR(255) NOT NULL,
  recipient_address VARCHAR(255) NOT NULL COMMENT 'phone or email',
  channel INT NOT NULL,
  status INT NOT NULL DEFAULT 2,
  provider VARCHAR(50) NULL,
  provider_reference VARCHAR(100) NULL,
  cost DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  rendered_body TEXT NOT NULL,
  rendered_subject VARCHAR(500) NULL,
  retry_count INT NOT NULL DEFAULT 0,
  last_attempt_at TIMESTAMPTZ NULL,
  delivered_at TIMESTAMPTZ NULL,
  failure_reason VARCHAR(500) NULL,
  is_opted_out BOOLEAN NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_msg_log_batch FOREIGN KEY (batch_id) REFERENCES message_batches(id) ON DELETE CASCADE,
  CONSTRAINT fk_msg_log_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_msg_log_tenant_batch (tenant_id, batch_id),
  KEY idx_msg_log_tenant_guardian (tenant_id, guardian_id),
  KEY idx_msg_log_tenant_status (tenant_id, status),
  KEY idx_msg_log_provider_ref (provider_reference)
);

CREATE TABLE IF NOT EXISTS tenant_messaging_usage (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  year INT NOT NULL,
  month INT NOT NULL,
  sms_count INT NOT NULL DEFAULT 0,
  email_count INT NOT NULL DEFAULT 0,
  sms_cost DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  email_cost DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  sms_limit INT NOT NULL DEFAULT 1000,
  email_limit INT NOT NULL DEFAULT 5000,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_usage_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_usage_tenant_year_month (tenant_id, year, month)
);

CREATE TABLE IF NOT EXISTS guardian_contact_preferences (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  guardian_id BIGINT NOT NULL,
  sms_opt_in BOOLEAN NOT NULL DEFAULT 1,
  email_opt_in BOOLEAN NOT NULL DEFAULT 1,
  sms_opt_out BOOLEAN NOT NULL DEFAULT 0,
  email_opt_out BOOLEAN NOT NULL DEFAULT 0,
  sms_opt_out_at TIMESTAMPTZ NULL,
  email_opt_out_at TIMESTAMPTZ NULL,
  opt_out_reason VARCHAR(255) NULL,
  preferred_language VARCHAR(10) NULL DEFAULT 'en',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_pref_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_pref_tenant_guardian (tenant_id, guardian_id)
);

CREATE TABLE IF NOT EXISTS messaging_provider_settings (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  channel INT NOT NULL,
  provider_name VARCHAR(50) NOT NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  is_default BOOLEAN NOT NULL DEFAULT 1,
  config_json JSONB NULL,
  cost_per_sms DECIMAL(18,4) NOT NULL DEFAULT 0.0500,
  cost_per_email DECIMAL(18,4) NOT NULL DEFAULT 0.0100,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  rate_limit_per_second INT NOT NULL DEFAULT 10,
  daily_cap INT NOT NULL DEFAULT 1000,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_provider_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_provider_tenant_channel_default (tenant_id, channel, is_default),
  KEY idx_provider_tenant_channel (tenant_id, channel, is_active)
);

-- Seed default provider settings for existing tenants (EcoCashSms + Smtp)
INSERT INTO messaging_provider_settings (tenant_id, channel, provider_name, is_active, is_default, cost_per_sms, cost_per_email, currency, rate_limit_per_second, daily_cap)
SELECT id, 1, 'EcoCashSms', 1, 1, 0.05, 0.00, 'USD', 10, 1000 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM messaging_provider_settings WHERE tenant_id=tenants.id AND channel=1)
ON DUPLICATE KEY UPDATE provider_name=VALUES(provider_name);

INSERT INTO messaging_provider_settings (tenant_id, channel, provider_name, is_active, is_default, cost_per_sms, cost_per_email, currency, rate_limit_per_second, daily_cap)
SELECT id, 2, 'Smtp', 1, 1, 0.00, 0.01, 'USD', 20, 5000 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM messaging_provider_settings WHERE tenant_id=tenants.id AND channel=2)
ON DUPLICATE KEY UPDATE provider_name=VALUES(provider_name);


-- ============================================================================
-- From V10_OnlinePayments.sql - LearnCloud.OnlinePayments
-- Original: /home/user/src/LearnCloud.OnlinePayments/Migrations/V10_OnlinePayments.sql
-- ============================================================================
-- Online Payments Module V10 - IPaymentGateway abstraction, per tenant config, card/bank/mobile money, webhook idempotent signature-verified replay/out-of-order safe, receipt allocation reuse, reconciliation, settlement

CREATE TABLE IF NOT EXISTS payment_gateway_settings (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  gateway_name VARCHAR(50) NOT NULL DEFAULT 'PayNow',
  is_active BOOLEAN NOT NULL DEFAULT 1,
  is_default BOOLEAN NOT NULL DEFAULT 1,
  config_json JSONB NULL,
  supported_methods_json JSONB NOT NULL DEFAULT '["card","bank_transfer","mobile_money","ecocash","onemoney"]',
  fee_percentage DECIMAL(5,2) NOT NULL DEFAULT 2.50,
  fee_fixed DECIMAL(18,2) NOT NULL DEFAULT 0.10,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  enable_card BOOLEAN NOT NULL DEFAULT 1,
  enable_bank_transfer BOOLEAN NOT NULL DEFAULT 1,
  enable_mobile_money BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_gateway_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_gateway_tenant_name_default (tenant_id, gateway_name, is_default),
  KEY idx_gateway_tenant_active (tenant_id, is_active)
);

CREATE TABLE IF NOT EXISTS online_payment_initiations (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  guardian_id BIGINT NOT NULL,
  invoice_id BIGINT NULL,
  requested_amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  method VARCHAR(20) NOT NULL DEFAULT 'card',
  status INT NOT NULL DEFAULT 1 COMMENT '1=Initiated,2=Pending,3=Processing,4=Succeeded,5=Failed,6=Cancelled,7=Expired',
  idempotency_key VARCHAR(100) NOT NULL,
  client_reference VARCHAR(100) NOT NULL,
  gateway_reference VARCHAR(100) NULL,
  payment_url TEXT NULL,
  failure_reason VARCHAR(500) NULL,
  expires_at TIMESTAMPTZ NOT NULL,
  created_by_user_id BIGINT NOT NULL,
  payment_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_online_init_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_online_init_client_ref (tenant_id, client_reference),
  UNIQUE KEY uq_online_init_idem (tenant_id, idempotency_key),
  KEY idx_online_init_tenant_student (tenant_id, student_id),
  KEY idx_online_init_status (tenant_id, status)
);

CREATE TABLE IF NOT EXISTS gateway_transactions (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  gateway_name VARCHAR(50) NOT NULL DEFAULT 'PayNow',
  gateway_transaction_id VARCHAR(100) NULL,
  provider_reference VARCHAR(100) NULL,
  payload_json TEXT NOT NULL,
  signature VARCHAR(500) NULL,
  is_signature_verified BOOLEAN NOT NULL DEFAULT 0,
  status VARCHAR(20) NOT NULL DEFAULT 'received',
  amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  method VARCHAR(20) NULL,
  client_reference VARCHAR(100) NULL,
  matched_initiation_id BIGINT NULL,
  matched_payment_id BIGINT NULL,
  received_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_replay BOOLEAN NOT NULL DEFAULT 0,
  is_out_of_order BOOLEAN NOT NULL DEFAULT 0,
  failure_reason VARCHAR(500) NULL,
  retry_count INT NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_gateway_tx_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_gateway_tx_id (tenant_id, gateway_transaction_id),
  KEY idx_gateway_tx_client_ref (tenant_id, client_reference),
  KEY idx_gateway_tx_status (tenant_id, status),
  KEY idx_gateway_tx_received (tenant_id, received_at)
);

CREATE TABLE IF NOT EXISTS gateway_settlements (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  settlement_id VARCHAR(100) NOT NULL,
  settlement_date DATE NOT NULL,
  gross_amount DECIMAL(18,2) NOT NULL,
  fee_amount DECIMAL(18,2) NOT NULL,
  net_amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  raw_data_json JSONB NULL,
  reconciled_by_user_id BIGINT NULL,
  reconciled_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_settlement_tenant_id (tenant_id, settlement_id),
  KEY idx_settlement_tenant_date (tenant_id, settlement_date)
);

CREATE TABLE IF NOT EXISTS settlement_transactions (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  settlement_id BIGINT NOT NULL,
  gateway_transaction_id BIGINT NULL,
  payment_id BIGINT NULL,
  amount DECIMAL(18,2) NOT NULL,
  fee DECIMAL(18,2) NOT NULL,
  net DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  is_matched BOOLEAN NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_settle_tx_settlement FOREIGN KEY (settlement_id) REFERENCES gateway_settlements(id) ON DELETE CASCADE,
  KEY idx_settle_tx_tenant_settlement (tenant_id, settlement_id)
);

-- Seed default gateway settings for existing tenants - PayNow supports card, bank transfer, mobile money
INSERT INTO payment_gateway_settings (tenant_id, gateway_name, is_active, is_default, supported_methods_json, fee_percentage, fee_fixed, currency, enable_card, enable_bank_transfer, enable_mobile_money)
SELECT id, 'PayNow', 1, 1, '["card","bank_transfer","mobile_money","ecocash","onemoney"]', 2.50, 0.10, 'USD', 1, 1, 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM payment_gateway_settings WHERE tenant_id=tenants.id AND gateway_name='PayNow')
ON DUPLICATE KEY UPDATE gateway_name=VALUES(gateway_name);


-- ============================================================================
-- From V11_PlatformAdmin.sql - LearnCloud.PlatformAdmin
-- Original: /home/user/src/LearnCloud.PlatformAdmin/Migrations/V11_PlatformAdmin.sql
-- ============================================================================
-- Platform Admin Console V11 - Tenant list, detail, business metrics, operational views, support notes, impersonation consented time-limited, announcement broadcast, second factor
-- Sits outside tenant scope and every action audited

CREATE TABLE IF NOT EXISTS support_notes (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  content TEXT NOT NULL,
  is_internal BOOLEAN NOT NULL DEFAULT 1,
  category VARCHAR(50) NULL,
  created_by_user_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_support_note_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_support_note_tenant (tenant_id, created_at)
);

CREATE TABLE IF NOT EXISTS impersonation_grants (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  granted_by_user_id BIGINT NOT NULL,
  granted_by_role VARCHAR(50) NOT NULL DEFAULT 'SCHOOL_ADMIN',
  granted_to_role VARCHAR(50) NOT NULL DEFAULT 'PLATFORM_SUPERADMIN',
  has_consent BOOLEAN NOT NULL DEFAULT 0,
  reason VARCHAR(500) NOT NULL,
  expires_at TIMESTAMPTZ NOT NULL,
  revoked_at TIMESTAMPTZ NULL,
  revoked_by_user_id BIGINT NULL,
  token_hash VARCHAR(128) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_imp_grant_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_imp_grant_tenant_active (tenant_id, has_consent, expires_at, revoked_at),
  KEY idx_imp_grant_expires (expires_at)
);

CREATE TABLE IF NOT EXISTS impersonation_sessions (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  grant_id BIGINT NOT NULL,
  impersonator_user_id BIGINT NOT NULL,
  impersonated_user_id BIGINT NULL,
  started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  expires_at TIMESTAMPTZ NOT NULL,
  ended_at TIMESTAMPTZ NULL,
  banner_message VARCHAR(500) NULL,
  ip_address VARCHAR(45) NULL,
  user_agent VARCHAR(500) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_imp_sess_grant FOREIGN KEY (grant_id) REFERENCES impersonation_grants(id) ON DELETE CASCADE,
  CONSTRAINT fk_imp_sess_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_imp_sess_tenant_active (tenant_id, ended_at, expires_at),
  KEY idx_imp_sess_impersonator (impersonator_user_id, ended_at, expires_at)
);

CREATE TABLE IF NOT EXISTS announcement_broadcasts (
  id BIGSERIAL PRIMARY KEY,
  title VARCHAR(255) NOT NULL,
  body TEXT NOT NULL,
  audience VARCHAR(50) NOT NULL DEFAULT 'all_school_admins',
  audience_filter_json JSONB NULL,
  scheduled_at TIMESTAMPTZ NULL,
  expires_at TIMESTAMPTZ NULL,
  priority VARCHAR(20) NOT NULL DEFAULT 'normal',
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  created_by_user_id BIGINT NOT NULL,
  sent_at TIMESTAMPTZ NULL,
  sent_count INT NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_broadcast_status_scheduled (status, scheduled_at)
);

CREATE TABLE IF NOT EXISTS tenant_health_scores (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  score INT NOT NULL DEFAULT 100,
  health_status VARCHAR(20) NOT NULL DEFAULT 'healthy',
  calculated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  factors_json JSONB NULL,
  monthly_value DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  last_activity_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_health_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_health_tenant (tenant_id),
  KEY idx_health_status_score (health_status, score)
);

CREATE TABLE IF NOT EXISTS background_job_records (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NULL,
  job_type VARCHAR(50) NOT NULL,
  job_id VARCHAR(100) NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  started_at TIMESTAMPTZ NULL,
  completed_at TIMESTAMPTZ NULL,
  retry_count INT NOT NULL DEFAULT 0,
  error_message TEXT NULL,
  result_json JSONB NULL,
  created_by_user_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_job_tenant_type_status (tenant_id, job_type, status),
  KEY idx_job_status_created (status, created_at)
);

CREATE TABLE IF NOT EXISTS error_rate_snapshots (
  id BIGSERIAL PRIMARY KEY,
  snapshot_date DATE NOT NULL,
  tenant_id BIGINT NULL,
  service VARCHAR(20) NOT NULL DEFAULT 'api',
  error_count INT NOT NULL DEFAULT 0,
  warning_count INT NOT NULL DEFAULT 0,
  request_count INT NOT NULL DEFAULT 0,
  error_rate DOUBLE NOT NULL DEFAULT 0.0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_error_snapshot_date (snapshot_date),
  KEY idx_error_tenant_service (tenant_id, service)
);


-- ============================================================================
-- From V6_PlatformBilling.sql - LearnCloud.PlatformBilling
-- Original: /home/user/src/LearnCloud.PlatformBilling/Migrations/V6_PlatformBilling.sql
-- ============================================================================
-- LearnCloud Platform Billing V6 - How schools pay you, not how learners pay school
-- Plans, subscriptions state machine, platform invoices, payments, dunning, overrides, feature flags

CREATE TABLE IF NOT EXISTS plans (
  id BIGSERIAL PRIMARY KEY,
  name VARCHAR(100) NOT NULL,
  code VARCHAR(50) NOT NULL,
  description TEXT NULL,
  price_per_learner_per_term DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  minimum_charge DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  learner_limit INT NOT NULL DEFAULT 300,
  included_sms_bundle INT NOT NULL DEFAULT 500,
  included_email_bundle INT NOT NULL DEFAULT 2000,
  included_modules_json JSONB NOT NULL COMMENT '["students","attendance","fees",...]',
  features_json JSONB NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  is_trial_plan BOOLEAN NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  UNIQUE KEY uq_plans_code (code),
  KEY idx_plans_active (is_active)
);

-- Subscriptions per tenant with state machine
CREATE TABLE IF NOT EXISTS subscriptions (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  plan_id BIGINT NOT NULL,
  state INT NOT NULL DEFAULT 1 COMMENT '1=Trialing,2=Active,3=PastDue,4=Suspended,5=Cancelled,6=Expired,7=Archived',
  previous_state VARCHAR(20) NULL,
  trial_started_at TIMESTAMPTZ NULL,
  trial_ends_at TIMESTAMPTZ NULL,
  trial_converted_at TIMESTAMPTZ NULL,
  academic_year_id BIGINT NULL,
  term_id BIGINT NULL,
  current_period_start TIMESTAMPTZ NOT NULL,
  current_period_end TIMESTAMPTZ NOT NULL,
  billable_learner_count INT NOT NULL DEFAULT 0,
  current_learner_count INT NOT NULL DEFAULT 0,
  past_due_grace_days INT NOT NULL DEFAULT 7,
  suspension_grace_days INT NOT NULL DEFAULT 30,
  past_due_since TIMESTAMPTZ NULL,
  suspended_since TIMESTAMPTZ NULL,
  expired_since TIMESTAMPTZ NULL,
  cancelled_at TIMESTAMPTZ NULL,
  cancellation_reason VARCHAR(255) NULL,
  read_only_until TIMESTAMPTZ NULL COMMENT '30-day read-only window after expiry before archival',
  pending_plan_id BIGINT NULL,
  pending_plan_effective_at TIMESTAMPTZ NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_sub_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  CONSTRAINT fk_sub_plan FOREIGN KEY (plan_id) REFERENCES plans(id) ON DELETE RESTRICT,
  UNIQUE KEY uq_sub_tenant (tenant_id),
  KEY idx_sub_state (state),
  KEY idx_sub_tenant_state (tenant_id, state),
  KEY idx_sub_trial_ends (trial_ends_at),
  KEY idx_sub_period_end (current_period_end)
);

CREATE TABLE IF NOT EXISTS platform_invoices (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  subscription_id BIGINT NOT NULL,
  invoice_number VARCHAR(50) NOT NULL,
  academic_year_id BIGINT NULL,
  term_id BIGINT NULL,
  issue_date DATE NOT NULL,
  due_date DATE NOT NULL,
  subtotal DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  minimum_charge_applied DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  pro_rata_adjustment DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  discount_amount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  total_amount DECIMAL(18,2) NOT NULL,
  amount_paid DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  balance_due DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  notes TEXT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_plat_inv_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  CONSTRAINT fk_plat_inv_sub FOREIGN KEY (subscription_id) REFERENCES subscriptions(id) ON DELETE CASCADE,
  UNIQUE KEY uq_plat_inv_number (invoice_number),
  KEY idx_plat_inv_tenant_status (tenant_id, status),
  KEY idx_plat_inv_tenant_due (tenant_id, due_date),
  KEY idx_plat_inv_due_date (due_date)
);

CREATE TABLE IF NOT EXISTS platform_invoice_lines (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  invoice_id BIGINT NOT NULL,
  description VARCHAR(500) NOT NULL,
  quantity INT NOT NULL DEFAULT 1,
  unit_price DECIMAL(18,2) NOT NULL,
  line_total DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  metadata_json JSONB NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_plat_line_inv FOREIGN KEY (invoice_id) REFERENCES platform_invoices(id) ON DELETE CASCADE,
  KEY idx_plat_line_tenant_invoice (tenant_id, invoice_id)
);

CREATE TABLE IF NOT EXISTS platform_payments (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  invoice_id BIGINT NOT NULL,
  amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  method VARCHAR(20) NOT NULL DEFAULT 'manual',
  reference VARCHAR(100) NULL,
  payment_date DATE NOT NULL,
  notes TEXT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'confirmed',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_plat_pay_inv FOREIGN KEY (invoice_id) REFERENCES platform_invoices(id) ON DELETE CASCADE,
  KEY idx_plat_pay_tenant (tenant_id),
  KEY idx_plat_pay_date (payment_date)
);

CREATE TABLE IF NOT EXISTS dunning_events (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  subscription_id BIGINT NOT NULL,
  invoice_id BIGINT NULL,
  event_type VARCHAR(50) NOT NULL,
  channel VARCHAR(20) NOT NULL DEFAULT 'email',
  recipient VARCHAR(255) NOT NULL,
  subject VARCHAR(255) NULL,
  body TEXT NULL,
  sent_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_success BOOLEAN NOT NULL DEFAULT 1,
  failure_reason VARCHAR(500) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_dunning_tenant_sub (tenant_id, subscription_id),
  KEY idx_dunning_type (event_type),
  KEY idx_dunning_sent_at (sent_at)
);

CREATE TABLE IF NOT EXISTS plan_change_logs (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  subscription_id BIGINT NOT NULL,
  from_plan_id BIGINT NOT NULL,
  to_plan_id BIGINT NOT NULL,
  change_type VARCHAR(20) NOT NULL COMMENT 'upgrade,downgrade',
  effective_type VARCHAR(20) NOT NULL COMMENT 'immediate,next_period',
  effective_at TIMESTAMPTZ NOT NULL,
  pro_rata_charge DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  reason VARCHAR(255) NULL,
  changed_by_user_id BIGINT NOT NULL,
  notes TEXT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_plan_change_tenant (tenant_id)
);

CREATE TABLE IF NOT EXISTS billing_overrides (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  subscription_id BIGINT NULL,
  invoice_id BIGINT NULL,
  override_type VARCHAR(30) NOT NULL COMMENT 'extend_trial,credit_invoice,extend_suspension_grace,change_plan',
  details_json JSONB NOT NULL,
  reason VARCHAR(500) NOT NULL,
  admin_user_id BIGINT NOT NULL,
  created_at_override TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_override_tenant (tenant_id),
  KEY idx_override_type (override_type)
);

-- Seed default plans: Starter, Growth, Scale with feature flags
INSERT INTO plans (name, code, description, price_per_learner_per_term, minimum_charge, learner_limit, included_sms_bundle, included_email_bundle, included_modules_json, is_active, currency, is_trial_plan) VALUES
('Starter', 'starter', 'For small schools 150-300 learners, core modules', 0.50, 99.00, 300, 300, 1000, '["students","guardians","staff","attendance","academic","settings"]', 1, 'USD', 1),
('Growth', 'growth', 'For growing schools 301-800, includes fees and assessments', 1.00, 149.00, 800, 500, 2000, '["students","guardians","staff","attendance","timetable","fees","assessments","report_cards","academic","settings","reports"]', 1, 'USD', 0),
('Scale', 'scale', 'For large schools 801-2000, all modules including messaging', 2.00, 199.00, 2000, 1000, 5000, '["students","guardians","staff","attendance","timetable","fees","assessments","report_cards","messaging","reports","settings","academic","admissions"]', 1, 'USD', 0)
ON DUPLICATE KEY UPDATE name=VALUES(name), price_per_learner_per_term=VALUES(price_per_learner_per_term), included_modules_json=VALUES(included_modules_json);


-- ============================================================================
-- From WizardProgress.sql - LearnCloud.SetupWizard
-- Original: /home/user/src/LearnCloud.SetupWizard/Migrations/WizardProgress.sql
-- ============================================================================
-- WizardProgress table - stores progress after every step for resume
CREATE TABLE IF NOT EXISTS wizard_progress (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  current_step INT NOT NULL DEFAULT 1,
  is_completed BOOLEAN NOT NULL DEFAULT 0,
  completed_at TIMESTAMPTZ NULL,
  last_saved_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  steps_status_json JSONB NOT NULL,
  data_json JSONB NULL,
  summary_json JSONB NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_wizard_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_wizard_tenant (tenant_id),
  KEY idx_wizard_tenant_step (tenant_id, current_step)
);

-- TenantSettings already exists from multi-tenancy, ensure columns for wizard
ALTER TABLE tenant_settings
  ADD COLUMN IF NOT EXISTS branding_json JSONB NULL,
  ADD COLUMN IF NOT EXISTS features_json JSONB NULL;


-- ============================================================================
-- From V13_Transport.sql - LearnCloud.Transport
-- Original: /home/user/src/LearnCloud.Transport/Migrations/V13_Transport.sql
-- ============================================================================
-- Transport Module V13 - Routes with ordered stops, vehicles capacity registration insurance licence expiry reminders, drivers assistants licence expiry, learner assignment capacity enforcement, transport fees flow into existing fee structure, boarding attendance, route change absence notifications via messaging, reports utilisation revenue unassigned, printable manifest

CREATE TABLE IF NOT EXISTS routes (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  code VARCHAR(20) NOT NULL,
  description VARCHAR(500) NULL,
  direction VARCHAR(20) NOT NULL DEFAULT 'both',
  is_active BOOLEAN NOT NULL DEFAULT 1,
  academic_year_id BIGINT NULL,
  term_id BIGINT NULL,
  total_distance_km DECIMAL(8,2) NOT NULL DEFAULT 0.00,
  estimated_duration_minutes INT NOT NULL DEFAULT 60,
  fee_amount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  vehicle_id BIGINT NULL,
  driver_id BIGINT NULL,
  assistant_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_route_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_route_tenant_code (tenant_id, code),
  KEY idx_route_tenant_active (tenant_id, is_active)
);

CREATE TABLE IF NOT EXISTS route_stops (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  route_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  address VARCHAR(500) NULL,
  latitude DECIMAL(10,8) NULL,
  longitude DECIMAL(11,8) NULL,
  order_number INT NOT NULL,
  expected_arrival_time TIME NULL,
  expected_departure_time TIME NULL,
  distance_from_start_km DECIMAL(8,2) NOT NULL DEFAULT 0.00,
  estimated_minutes_from_start INT NOT NULL DEFAULT 0,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_stop_route FOREIGN KEY (route_id) REFERENCES routes(id) ON DELETE CASCADE,
  KEY idx_stop_tenant_route_order (tenant_id, route_id, order_number),
  UNIQUE KEY uq_stop_tenant_route_order (tenant_id, route_id, order_number)
);

CREATE TABLE IF NOT EXISTS vehicles (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  registration_number VARCHAR(20) NOT NULL,
  make VARCHAR(50) NOT NULL,
  model VARCHAR(50) NOT NULL,
  capacity INT NOT NULL,
  year INT NOT NULL,
  fuel_type VARCHAR(20) NULL,
  insurance_expiry DATE NOT NULL,
  licence_expiry DATE NOT NULL,
  fitness_expiry DATE NULL,
  service_due_date DATE NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  notes TEXT NULL,
  last_insurance_reminder_sent_at TIMESTAMPTZ NULL,
  last_licence_reminder_sent_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_vehicle_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_vehicle_tenant_reg (tenant_id, registration_number),
  KEY idx_vehicle_tenant_expiry (tenant_id, insurance_expiry, licence_expiry)
);

CREATE TABLE IF NOT EXISTS drivers (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  full_name VARCHAR(255) NOT NULL,
  role VARCHAR(20) NOT NULL DEFAULT 'driver',
  staff_id BIGINT NULL,
  licence_number VARCHAR(50) NULL,
  licence_type VARCHAR(20) NULL,
  licence_expiry DATE NULL,
  medical_expiry DATE NULL,
  phone VARCHAR(50) NULL,
  email VARCHAR(255) NULL,
  id_number VARCHAR(50) NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  notes TEXT NULL,
  last_licence_reminder_sent_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_driver_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_driver_tenant_role (tenant_id, role, is_active),
  KEY idx_driver_tenant_licence_expiry (tenant_id, licence_expiry)
);

CREATE TABLE IF NOT EXISTS transport_assignments (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  route_id BIGINT NOT NULL,
  pickup_stop_id BIGINT NOT NULL,
  drop_stop_id BIGINT NULL,
  assigned_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  assigned_by_user_id BIGINT NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  unassigned_date TIMESTAMPTZ NULL,
  unassigned_reason VARCHAR(255) NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  fee_structure_item_id BIGINT NULL,
  fee_applied BOOLEAN NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_assign_route FOREIGN KEY (route_id) REFERENCES routes(id) ON DELETE CASCADE,
  CONSTRAINT fk_assign_pickup_stop FOREIGN KEY (pickup_stop_id) REFERENCES route_stops(id) ON DELETE RESTRICT,
  CONSTRAINT fk_assign_drop_stop FOREIGN KEY (drop_stop_id) REFERENCES route_stops(id) ON DELETE SET NULL,
  UNIQUE KEY uq_assign_tenant_student_year_term (tenant_id, student_id, academic_year_id, term_id),
  KEY idx_assign_tenant_route_status (tenant_id, route_id, status),
  KEY idx_assign_tenant_student (tenant_id, student_id)
);

CREATE TABLE IF NOT EXISTS transport_fee_links (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  transport_assignment_id BIGINT NOT NULL,
  fee_item_id BIGINT NOT NULL,
  fee_structure_id BIGINT NOT NULL,
  fee_structure_item_id BIGINT NOT NULL,
  amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_fee_link_assign FOREIGN KEY (transport_assignment_id) REFERENCES transport_assignments(id) ON DELETE CASCADE,
  KEY idx_fee_link_tenant_assignment (tenant_id, transport_assignment_id)
);

CREATE TABLE IF NOT EXISTS transport_attendances (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  route_id BIGINT NOT NULL,
  route_stop_id BIGINT NULL,
  student_id BIGINT NOT NULL,
  transport_assignment_id BIGINT NOT NULL,
  trip_date DATE NOT NULL,
  trip_type VARCHAR(20) NOT NULL DEFAULT 'morning',
  status VARCHAR(20) NOT NULL DEFAULT 'boarded',
  actual_boarding_time TIME NULL,
  marked_by_user_id BIGINT NOT NULL,
  notes VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_ta_route FOREIGN KEY (route_id) REFERENCES routes(id) ON DELETE CASCADE,
  UNIQUE KEY uq_ta_tenant_route_student_date_type (tenant_id, route_id, student_id, trip_date, trip_type),
  KEY idx_ta_tenant_route_date (tenant_id, route_id, trip_date)
);

CREATE TABLE IF NOT EXISTS transport_notification_logs (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  route_id BIGINT NOT NULL,
  student_id BIGINT NULL,
  notification_type VARCHAR(30) NOT NULL,
  title VARCHAR(255) NOT NULL,
  body TEXT NOT NULL,
  message_batch_id BIGINT NULL,
  recipients_json JSONB NULL,
  sent_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  sent_by_user_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_tn_route FOREIGN KEY (route_id) REFERENCES routes(id) ON DELETE CASCADE,
  KEY idx_tn_tenant_route (tenant_id, route_id)
);

-- Seed default transport fee item per tenant (flows into existing fee structure)
INSERT INTO fee_items (tenant_id, name, code, recurrence, is_proratable, is_optional, description)
SELECT id, 'Transport', 'TRANSPORT', 1, 0, 1, 'Transport fee per term - flows into existing fee invoicing' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM fee_items WHERE tenant_id=tenants.id AND code='TRANSPORT')
ON DUPLICATE KEY UPDATE name=VALUES(name);

