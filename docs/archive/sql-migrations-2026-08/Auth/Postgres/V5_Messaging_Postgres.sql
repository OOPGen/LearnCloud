-- Translated from MySQL V5_Messaging.sql to PostgreSQL (Supabase Compatible)
-- Original: /home/user/src/LearnCloud.Messaging/Migrations/V5_Messaging.sql
-- Translated: /home/user/src/LearnCloud.Auth/Migrations/Postgres/V5_Messaging_Postgres.sql
-- Date: 2026-08-09
-- Note: Manual review needed for ENUM->VARCHAR, generated columns, partitioning

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
