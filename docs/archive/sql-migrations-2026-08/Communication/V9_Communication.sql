-- Communication Full Module V9 - Extend messaging: announcements, scheduled sending, saved segments, template categories, two-way SMS, rule engine, analytics, per-learner log
-- Preserve provider abstraction, usage caps, opt-out

CREATE TABLE IF NOT EXISTS announcements (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  title VARCHAR(255) NOT NULL,
  body TEXT NOT NULL,
  audience_type VARCHAR(30) NOT NULL DEFAULT 'all',
  audience_filter_json JSON NOT NULL,
  expiry_date DATETIME NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  priority VARCHAR(20) NOT NULL DEFAULT 'normal',
  show_in_teacher_portal TINYINT(1) NOT NULL DEFAULT 1,
  show_in_parent_portal TINYINT(1) NOT NULL DEFAULT 1,
  show_in_student_portal TINYINT(1) NOT NULL DEFAULT 1,
  show_in_admin_dashboard TINYINT(1) NOT NULL DEFAULT 1,
  created_by_user_id BIGINT UNSIGNED NOT NULL,
  published_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_ann_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_ann_tenant_status_expiry (tenant_id, status, expiry_date),
  KEY idx_ann_tenant_portals (tenant_id, show_in_teacher_portal, show_in_parent_portal)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS scheduled_messages (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  batch_id BIGINT UNSIGNED NULL,
  title VARCHAR(255) NOT NULL,
  template_id BIGINT UNSIGNED NULL,
  channel VARCHAR(20) NOT NULL DEFAULT 'sms',
  audience_type VARCHAR(30) NOT NULL,
  audience_filter_json JSON NOT NULL,
  body TEXT NOT NULL,
  subject VARCHAR(500) NULL,
  scheduled_send_at DATETIME NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'scheduled',
  created_by_user_id BIGINT UNSIGNED NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_sched_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_sched_tenant_scheduled (tenant_id, scheduled_send_at, status)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS audience_segments (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  name VARCHAR(100) NOT NULL,
  description VARCHAR(500) NOT NULL DEFAULT '',
  audience_type VARCHAR(30) NOT NULL,
  filter_json JSON NOT NULL,
  is_dynamic TINYINT(1) NOT NULL DEFAULT 0,
  created_by_user_id BIGINT UNSIGNED NOT NULL,
  is_system TINYINT(1) NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_seg_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_seg_tenant_name (tenant_id, name),
  KEY idx_seg_tenant_type (tenant_id, audience_type)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS template_categories (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  name VARCHAR(50) NOT NULL,
  code VARCHAR(30) NOT NULL,
  description VARCHAR(255) NULL,
  color VARCHAR(7) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_cat_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_cat_tenant_code (tenant_id, code)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS categorized_templates (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  template_id BIGINT UNSIGNED NOT NULL,
  category_id BIGINT UNSIGNED NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_ct_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  CONSTRAINT fk_ct_template FOREIGN KEY (template_id) REFERENCES message_templates(id) ON DELETE CASCADE,
  CONSTRAINT fk_ct_category FOREIGN KEY (category_id) REFERENCES template_categories(id) ON DELETE CASCADE,
  UNIQUE KEY uq_ct_tenant_template_category (tenant_id, template_id, category_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS inbound_sms (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  from_number VARCHAR(20) NOT NULL,
  to_number VARCHAR(20) NOT NULL,
  body TEXT NOT NULL,
  provider VARCHAR(50) NULL,
  provider_reference VARCHAR(100) NULL,
  received_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  matched_guardian_id BIGINT UNSIGNED NULL,
  matched_student_id BIGINT UNSIGNED NULL,
  matched_school_slug VARCHAR(100) NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'received',
  reply_body TEXT NULL,
  replied_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_inbound_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_inbound_tenant_from (tenant_id, from_number),
  KEY idx_inbound_tenant_received (tenant_id, received_at)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS communication_rules (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  name VARCHAR(100) NOT NULL,
  code VARCHAR(50) NOT NULL,
  event_type VARCHAR(50) NOT NULL,
  description VARCHAR(500) NOT NULL DEFAULT '',
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  config_json JSON NOT NULL,
  template_id BIGINT UNSIGNED NULL,
  channel VARCHAR(20) NOT NULL DEFAULT 'sms',
  audience_type VARCHAR(30) NOT NULL DEFAULT 'dynamic',
  respect_opt_out TINYINT(1) NOT NULL DEFAULT 1,
  respect_contact_preferences TINYINT(1) NOT NULL DEFAULT 1,
  created_by_user_id BIGINT UNSIGNED NOT NULL,
  last_triggered_at DATETIME NULL,
  trigger_count INT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_rule_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_rule_tenant_code (tenant_id, code),
  KEY idx_rule_tenant_event_active (tenant_id, event_type, is_active)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS communication_logs (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  batch_id BIGINT UNSIGNED NULL,
  announcement_id BIGINT UNSIGNED NULL,
  rule_id BIGINT UNSIGNED NULL,
  inbound_sms_id BIGINT UNSIGNED NULL,
  student_id BIGINT UNSIGNED NOT NULL,
  guardian_id BIGINT UNSIGNED NULL,
  teacher_staff_id BIGINT UNSIGNED NULL,
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
  sent_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  delivered_at DATETIME NULL,
  read_at DATETIME NULL,
  failure_reason VARCHAR(500) NULL,
  is_opted_out_at_send TINYINT(1) NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_commlog_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_commlog_tenant_student (tenant_id, student_id),
  KEY idx_commlog_tenant_guardian (tenant_id, guardian_id),
  KEY idx_commlog_tenant_batch (tenant_id, batch_id),
  KEY idx_commlog_search (tenant_id, student_id, channel, status, sent_at),
  FULLTEXT KEY ft_commlog_body (message_body)
) ENGINE=InnoDB;

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
