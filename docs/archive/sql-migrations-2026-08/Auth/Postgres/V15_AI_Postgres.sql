-- Translated from MySQL V15_AI.sql to PostgreSQL (Supabase Compatible)
-- Original: /home/user/src/LearnCloud.AI/Migrations/V15_AI.sql
-- Translated: /home/user/src/LearnCloud.Auth/Migrations/Postgres/V15_AI_Postgres.sql
-- Date: 2026-08-09
-- Note: Manual review needed for ENUM->VARCHAR, generated columns, partitioning

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
