-- V19 Postgres Migration - MySQL to PostgreSQL (Supabase Compatible)
-- Date: 2026-08-09
-- Converts MySQL schema to PostgreSQL 15+ with Supabase support
-- Changes: BIGINT UNSIGNED -> BIGINT, TINYINT(1) -> BOOLEAN, ENUM -> VARCHAR+CHECK, JSON -> JSONB, DATETIME -> TIMESTAMPTZ, IF() -> CASE, etc.

-- Enable extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Tenants (not tenant-owned, it IS the tenant)
CREATE TABLE IF NOT EXISTS tenants (
  id BIGSERIAL PRIMARY KEY,
  name VARCHAR(255) NOT NULL,
  slug VARCHAR(100) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'trial' CHECK (status IN ('trial','active','suspended','cancelled')),
  city VARCHAR(100) NOT NULL DEFAULT 'Bulawayo',
  country CHAR(2) NOT NULL DEFAULT 'ZW',
  contact_email VARCHAR(255) NOT NULL,
  contact_phone VARCHAR(50) NULL,
  primary_color CHAR(7) NOT NULL DEFAULT '#0F153A',
  learner_count_band VARCHAR(20) NOT NULL,
  logo_url VARCHAR(500) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT uq_tenants_slug UNIQUE (slug)
);
CREATE INDEX IF NOT EXISTS idx_tenants_status ON tenants(status);
-- Partial unique for soft-delete reuse (Postgres allows multiple NULLs in unique, so use generated column)
ALTER TABLE tenants ADD COLUMN IF NOT EXISTS active_slug VARCHAR(100) GENERATED ALWAYS AS (CASE WHEN is_deleted=false THEN slug ELSE NULL END) STORED;
CREATE UNIQUE INDEX IF NOT EXISTS uq_tenants_active_slug ON tenants(active_slug);

-- Subscription plans (global, no tenant_id)
CREATE TABLE IF NOT EXISTS subscription_plans (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NULL,
  code VARCHAR(50) NOT NULL,
  name VARCHAR(100) NOT NULL,
  max_learners INT NOT NULL,
  price_monthly DECIMAL(18,2) NOT NULL,
  price_annual DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  features_json JSONB NULL,
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT uq_plans_code UNIQUE (code)
);

-- Tenant subscriptions
CREATE TABLE IF NOT EXISTS tenant_subscriptions (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  plan_id BIGINT NOT NULL REFERENCES subscription_plans(id) ON DELETE RESTRICT,
  billing_cycle VARCHAR(20) NOT NULL DEFAULT 'monthly',
  status VARCHAR(20) NOT NULL DEFAULT 'trialing',
  trial_ends_at TIMESTAMPTZ NULL,
  current_period_start TIMESTAMPTZ NOT NULL,
  current_period_end TIMESTAMPTZ NOT NULL,
  metered_active_students INT NOT NULL DEFAULT 0,
  over_limit_flag BOOLEAN NOT NULL DEFAULT FALSE,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL
);
CREATE INDEX IF NOT EXISTS idx_tenant_subs_tenant_status ON tenant_subscriptions(tenant_id, status);
CREATE INDEX IF NOT EXISTS idx_tenant_subs_tenant_period ON tenant_subscriptions(tenant_id, current_period_end);

-- Users (tenant_id NULLABLE for platform admin)
CREATE TABLE IF NOT EXISTS users (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
  email VARCHAR(255) NOT NULL,
  phone VARCHAR(50) NULL,
  display_name VARCHAR(255) NOT NULL,
  password_hash VARCHAR(500) NOT NULL,
  email_verified BOOLEAN NOT NULL DEFAULT FALSE,
  email_verified_at TIMESTAMPTZ NULL,
  token_version INT NOT NULL DEFAULT 1,
  security_stamp VARCHAR(100) NOT NULL,
  failed_login_count INT NOT NULL DEFAULT 0,
  lockout_end TIMESTAMPTZ NULL,
  last_login_at TIMESTAMPTZ NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  must_change_password BOOLEAN NOT NULL DEFAULT FALSE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT uq_users_tenant_email UNIQUE (tenant_id, email)
);
CREATE INDEX IF NOT EXISTS idx_users_email_global ON users(email);
CREATE INDEX IF NOT EXISTS idx_users_tenant_status ON users(tenant_id, status);
CREATE INDEX IF NOT EXISTS idx_users_security_stamp ON users(security_stamp);
-- Partial unique for soft-delete reuse
ALTER TABLE users ADD COLUMN IF NOT EXISTS active_email VARCHAR(255) GENERATED ALWAYS AS (CASE WHEN is_deleted=false THEN email ELSE NULL END) STORED;
CREATE UNIQUE INDEX IF NOT EXISTS uq_users_tenant_active_email ON users(tenant_id, active_email);

-- Roles (tenant_id NULL for system)
CREATE TABLE IF NOT EXISTS roles (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NULL,
  code VARCHAR(50) NOT NULL,
  name VARCHAR(100) NOT NULL,
  is_system BOOLEAN NOT NULL DEFAULT TRUE,
  description VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT uq_roles_tenant_code UNIQUE (tenant_id, code)
);

-- Permissions (global)
CREATE TABLE IF NOT EXISTS permissions (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NULL,
  code VARCHAR(100) NOT NULL,
  name VARCHAR(150) NOT NULL,
  module VARCHAR(50) NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT uq_permissions_code UNIQUE (code)
);

-- Role permissions
CREATE TABLE IF NOT EXISTS role_permissions (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NULL,
  role_id BIGINT NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
  permission_id BIGINT NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT uq_rp_tenant_role_perm UNIQUE (tenant_id, role_id, permission_id)
);
CREATE INDEX IF NOT EXISTS idx_rp_tenant_role ON role_permissions(tenant_id, role_id);

-- User roles
CREATE TABLE IF NOT EXISTS user_roles (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NULL,
  user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  role_id BIGINT NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
  academic_year_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT uq_user_roles UNIQUE (tenant_id, user_id, role_id, academic_year_id)
);
CREATE INDEX IF NOT EXISTS idx_user_roles_tenant_user ON user_roles(tenant_id, user_id);

-- Refresh tokens
CREATE TABLE IF NOT EXISTS refresh_tokens (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NULL,
  user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  token_hash VARCHAR(128) NOT NULL,
  family_id VARCHAR(100) NOT NULL,
  parent_token_id BIGINT NULL REFERENCES refresh_tokens(id) ON DELETE SET NULL,
  replaced_by_token_id BIGINT NULL,
  expires_at TIMESTAMPTZ NOT NULL,
  revoked_at TIMESTAMPTZ NULL,
  revoked_reason VARCHAR(100) NULL,
  created_by_ip VARCHAR(45) NOT NULL,
  device VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT uq_refresh_token_hash UNIQUE (token_hash)
);
CREATE INDEX IF NOT EXISTS idx_refresh_tenant_user_family ON refresh_tokens(tenant_id, user_id, family_id);
CREATE INDEX IF NOT EXISTS idx_refresh_tenant_user_exp ON refresh_tokens(tenant_id, user_id, expires_at);
CREATE INDEX IF NOT EXISTS idx_refresh_family ON refresh_tokens(family_id);

-- User tokens (email verification, password reset)
CREATE TABLE IF NOT EXISTS user_tokens (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NULL,
  user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  token_type INT NOT NULL,
  token_hash VARCHAR(128) NOT NULL,
  expires_at TIMESTAMPTZ NOT NULL,
  used_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL
);
CREATE INDEX IF NOT EXISTS idx_usertoken_hash ON user_tokens(token_hash);
CREATE INDEX IF NOT EXISTS idx_usertoken_user_type_exp ON user_tokens(user_id, token_type, expires_at);

-- Academic Years
CREATE TABLE IF NOT EXISTS academic_years (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
  name VARCHAR(20) NOT NULL,
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  is_current BOOLEAN NOT NULL DEFAULT FALSE,
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT uq_academic_years_tenant_name UNIQUE (tenant_id, name)
);
CREATE INDEX IF NOT EXISTS idx_ay_tenant_current ON academic_years(tenant_id, is_current);

-- Terms
CREATE TABLE IF NOT EXISTS terms (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
  academic_year_id BIGINT NOT NULL REFERENCES academic_years(id) ON DELETE RESTRICT,
  name VARCHAR(50) NOT NULL,
  term_number SMALLINT NOT NULL,
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  is_current BOOLEAN NOT NULL DEFAULT FALSE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT uq_terms_tenant_year_number UNIQUE (tenant_id, academic_year_id, term_number)
);
CREATE INDEX IF NOT EXISTS idx_terms_tenant_year ON terms(tenant_id, academic_year_id);

-- Grades
CREATE TABLE IF NOT EXISTS grades (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
  academic_year_id BIGINT NOT NULL REFERENCES academic_years(id) ON DELETE RESTRICT,
  name VARCHAR(50) NOT NULL,
  code VARCHAR(20) NOT NULL,
  level_order INT NOT NULL DEFAULT 0,
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT uq_grades_tenant_code_year UNIQUE (tenant_id, code, academic_year_id)
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_grades_tenant_id ON grades(tenant_id, id);
CREATE INDEX IF NOT EXISTS idx_grades_tenant_year_order ON grades(tenant_id, academic_year_id, level_order);
-- Partial unique for soft-delete reuse
ALTER TABLE grades ADD COLUMN IF NOT EXISTS active_code VARCHAR(20) GENERATED ALWAYS AS (CASE WHEN is_deleted=false THEN code ELSE NULL END) STORED;
CREATE UNIQUE INDEX IF NOT EXISTS uq_grades_tenant_year_active_code ON grades(tenant_id, academic_year_id, active_code);

-- Streams
CREATE TABLE IF NOT EXISTS streams (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
  academic_year_id BIGINT NOT NULL,
  grade_id BIGINT NOT NULL,
  name VARCHAR(50) NOT NULL,
  full_name VARCHAR(100) NOT NULL,
  capacity INT NOT NULL DEFAULT 40,
  class_teacher_staff_id BIGINT NULL,
  room_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT uq_streams_tenant_grade_name_year UNIQUE (tenant_id, academic_year_id, grade_id, name)
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_streams_tenant_id ON streams(tenant_id, id);
ALTER TABLE streams ADD COLUMN IF NOT EXISTS active_name VARCHAR(50) GENERATED ALWAYS AS (CASE WHEN is_deleted=false THEN name ELSE NULL END) STORED;
CREATE UNIQUE INDEX IF NOT EXISTS uq_streams_tenant_grade_year_active_name ON streams(tenant_id, academic_year_id, grade_id, active_name);

-- Students
CREATE TABLE IF NOT EXISTS students (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
  student_number VARCHAR(50) NOT NULL,
  first_name VARCHAR(100) NOT NULL,
  last_name VARCHAR(100) NOT NULL,
  dob DATE NULL,
  gender VARCHAR(10) NULL CHECK (gender IN ('M','F','other')),
  national_id VARCHAR(50) NULL,
  photo_url VARCHAR(500) NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  current_enrolment_id BIGINT NULL,
  grade_id BIGINT NOT NULL,
  stream_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT uq_students_tenant_number UNIQUE (tenant_id, student_number)
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_students_tenant_id ON students(tenant_id, id);
ALTER TABLE students ADD COLUMN IF NOT EXISTS active_student_number VARCHAR(50) GENERATED ALWAYS AS (CASE WHEN is_deleted=false THEN student_number ELSE NULL END) STORED;
CREATE UNIQUE INDEX IF NOT EXISTS uq_students_tenant_active_number ON students(tenant_id, active_student_number);

-- Add more tables as needed for full schema - this is core, other tables can be added via EF migrations

-- Schema migrations tracking
CREATE TABLE IF NOT EXISTS schema_migrations (
  version VARCHAR(20) NOT NULL PRIMARY KEY,
  applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  checksum VARCHAR(64) NULL,
  description VARCHAR(255) NULL,
  execution_time_ms INT NULL
);

INSERT INTO schema_migrations (version, checksum, description) VALUES ('V19', encode(digest('Postgres_Migration', 'sha256'), 'hex'), 'Postgres initial migration from MySQL V1-V18') ON CONFLICT (version) DO UPDATE SET applied_at=NOW();

-- Enable RLS for tenant isolation (Supabase best practice)
ALTER TABLE students ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation_students ON students FOR ALL USING (tenant_id = (current_setting('app.current_tenant_id', true)::bigint)) WITH CHECK (tenant_id = (current_setting('app.current_tenant_id', true)::bigint));

-- Note: For Supabase, use auth.jwt() ->> 'tid' instead of current_setting
-- CREATE POLICY tenant_isolation_students_supabase ON students FOR ALL USING (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Repeat RLS for all tenant-owned tables as needed
