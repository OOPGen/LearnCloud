-- Translated from MySQL V6_PlatformBilling.sql to PostgreSQL (Supabase Compatible)
-- Original: /home/user/src/LearnCloud.PlatformBilling/Migrations/V6_PlatformBilling.sql
-- Translated: /home/user/src/LearnCloud.Auth/Migrations/Postgres/V6_PlatformBilling_Postgres.sql
-- Date: 2026-08-09
-- Note: Manual review needed for ENUM->VARCHAR, generated columns, partitioning

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
