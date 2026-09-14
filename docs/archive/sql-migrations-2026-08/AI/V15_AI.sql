-- AI-Assisted Features V15 - Report card comment drafting, attendance anomaly detection, at-risk learner identification
-- Priority order: 1. Report comment drafting saves hours, teacher always reviews, nothing auto-written

CREATE TABLE IF NOT EXISTS ai_provider_settings (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  provider_name VARCHAR(50) NOT NULL DEFAULT 'RuleBased',
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  is_default TINYINT(1) NOT NULL DEFAULT 1,
  config_json JSON NULL,
  enable_comment_drafting TINYINT(1) NOT NULL DEFAULT 1,
  enable_attendance_anomaly TINYINT(1) NOT NULL DEFAULT 1,
  enable_at_risk_detection TINYINT(1) NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  UNIQUE KEY uq_ai_provider_tenant_default (tenant_id, is_default),
  KEY idx_ai_provider_tenant_active (tenant_id, is_active)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS report_comment_drafts (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  student_id BIGINT UNSIGNED NOT NULL,
  academic_year_id BIGINT UNSIGNED NOT NULL,
  term_id BIGINT UNSIGNED NOT NULL,
  report_card_id BIGINT UNSIGNED NULL,
  input_data_json JSON NOT NULL,
  draft_comment TEXT NOT NULL,
  tone VARCHAR(20) NOT NULL DEFAULT 'encouraging',
  length VARCHAR(20) NOT NULL DEFAULT 'medium',
  edited_comment TEXT NULL,
  is_edited TINYINT(1) NOT NULL DEFAULT 0,
  is_saved TINYINT(1) NOT NULL DEFAULT 0,
  edited_by_user_id BIGINT UNSIGNED NULL,
  edited_at DATETIME NULL,
  saved_by_user_id BIGINT UNSIGNED NULL,
  saved_at DATETIME NULL,
  provider_name VARCHAR(50) NOT NULL DEFAULT 'RuleBased',
  model VARCHAR(100) NULL,
  prompt_tokens INT NULL,
  completion_tokens INT NULL,
  cost DECIMAL(18,4) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  KEY idx_comment_tenant_student (tenant_id, student_id),
  KEY idx_comment_tenant_year_term (tenant_id, academic_year_id, term_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS attendance_anomalies (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  student_id BIGINT UNSIGNED NOT NULL,
  anomaly_type VARCHAR(30) NOT NULL,
  description VARCHAR(255) NOT NULL,
  explanation TEXT NOT NULL,
  confidence_score DECIMAL(5,2) NOT NULL,
  detected_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  period_from DATETIME NOT NULL,
  period_to DATETIME NOT NULL,
  data_json JSON NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'new',
  acknowledged_by_user_id BIGINT UNSIGNED NULL,
  acknowledged_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  KEY idx_anomaly_tenant_student (tenant_id, student_id),
  KEY idx_anomaly_tenant_type_status (tenant_id, anomaly_type, status)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS at_risk_flags (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  student_id BIGINT UNSIGNED NOT NULL,
  risk_level VARCHAR(20) NOT NULL DEFAULT 'medium',
  risk_score DECIMAL(5,2) NOT NULL,
  flag_reason TEXT NOT NULL,
  underlying_reasons_json JSON NOT NULL,
  detected_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  period_from DATETIME NOT NULL,
  period_to DATETIME NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'new',
  assigned_to_user_id BIGINT UNSIGNED NULL,
  follow_up_notes TEXT NULL,
  resolved_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  KEY idx_atrisk_tenant_student (tenant_id, student_id),
  KEY idx_atrisk_tenant_level_status (tenant_id, risk_level, status)
) ENGINE=InnoDB;

-- Seed default AI provider settings per tenant - RuleBased fallback works offline
INSERT INTO ai_provider_settings (tenant_id, provider_name, is_active, is_default, enable_comment_drafting, enable_attendance_anomaly, enable_at_risk_detection)
SELECT id, 'RuleBased', 1, 1, 1, 1, 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM ai_provider_settings WHERE tenant_id=tenants.id)
ON DUPLICATE KEY UPDATE provider_name=VALUES(provider_name);
