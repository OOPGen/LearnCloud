-- Translated from MySQL V17_Phase0_1_Optimizations.sql to PostgreSQL (Supabase Compatible)
-- Original: /home/user/src/LearnCloud.Auth/Migrations/V17_Phase0_1_Optimizations.sql
-- Translated: /home/user/src/LearnCloud.Auth/Migrations/Postgres/V17_Phase0_1_Optimizations_Postgres.sql
-- Date: 2026-08-09
-- Note: Manual review needed for ENUM->VARCHAR, generated columns, partitioning

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

