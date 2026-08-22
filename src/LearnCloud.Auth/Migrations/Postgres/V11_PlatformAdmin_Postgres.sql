-- Translated from MySQL V11_PlatformAdmin.sql to PostgreSQL (Supabase Compatible)
-- Original: /home/user/src/LearnCloud.PlatformAdmin/Migrations/V11_PlatformAdmin.sql
-- Translated: /home/user/src/LearnCloud.Auth/Migrations/Postgres/V11_PlatformAdmin_Postgres.sql
-- Date: 2026-08-09
-- Note: Manual review needed for ENUM->VARCHAR, generated columns, partitioning

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
