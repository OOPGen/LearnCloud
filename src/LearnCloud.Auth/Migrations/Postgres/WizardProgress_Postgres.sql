-- Translated from MySQL WizardProgress.sql to PostgreSQL (Supabase Compatible)
-- Original: /home/user/src/LearnCloud.SetupWizard/Migrations/WizardProgress.sql
-- Translated: /home/user/src/LearnCloud.Auth/Migrations/Postgres/WizardProgress_Postgres.sql
-- Date: 2026-08-09
-- Note: Manual review needed for ENUM->VARCHAR, generated columns, partitioning

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
