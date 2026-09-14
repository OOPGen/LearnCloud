-- HR Module V15 - Staff records, contracts expiry reminders, qualifications and documents, leave types entitlements, leave request approval workflow balance calculation leave calendar, appraisal cycles criteria, disciplinary restricted access, staff reporting headcount turnover leave liability

CREATE TABLE IF NOT EXISTS departments (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  name VARCHAR(100) NOT NULL,
  code VARCHAR(20) NOT NULL,
  hod_staff_id BIGINT UNSIGNED NULL,
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  UNIQUE KEY uq_dept_tenant_code (tenant_id, code),
  KEY idx_dept_tenant_active (tenant_id, is_active)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS staff (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  staff_number VARCHAR(20) NOT NULL,
  first_name VARCHAR(100) NOT NULL,
  last_name VARCHAR(100) NOT NULL,
  national_id VARCHAR(50) NULL,
  date_of_birth DATE NULL,
  gender VARCHAR(20) NOT NULL DEFAULT 'other',
  employment_type VARCHAR(20) NOT NULL DEFAULT 'permanent',
  employment_status VARCHAR(20) NOT NULL DEFAULT 'active',
  department_id BIGINT UNSIGNED NULL,
  designation VARCHAR(100) NULL,
  hire_date DATE NOT NULL,
  confirmation_date DATE NULL,
  phone VARCHAR(50) NULL,
  email VARCHAR(255) NULL,
  address VARCHAR(500) NULL,
  user_id BIGINT UNSIGNED NULL,
  photo_url VARCHAR(500) NULL,
  current_salary DECIMAL(18,2) NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  UNIQUE KEY uq_staff_tenant_number (tenant_id, staff_number),
  KEY idx_staff_tenant_status (tenant_id, employment_status),
  KEY idx_staff_tenant_dept (tenant_id, department_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS contracts (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  staff_id BIGINT UNSIGNED NOT NULL,
  contract_number VARCHAR(20) NOT NULL,
  contract_type VARCHAR(20) NOT NULL DEFAULT 'permanent',
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  probation_end_date DATE NULL,
  salary DECIMAL(18,2) NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  terms TEXT NULL,
  last_reminder_sent_at DATETIME NULL,
  created_by_user_id BIGINT UNSIGNED NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  UNIQUE KEY uq_contract_tenant_number (tenant_id, contract_number),
  KEY idx_contract_tenant_staff (tenant_id, staff_id),
  KEY idx_contract_tenant_end_date (tenant_id, end_date, status)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS qualifications (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  staff_id BIGINT UNSIGNED NOT NULL,
  qualification_name VARCHAR(255) NOT NULL,
  institution VARCHAR(255) NOT NULL,
  year_obtained INT NULL,
  grade VARCHAR(50) NULL,
  certificate_number VARCHAR(100) NULL,
  is_verified TINYINT(1) NOT NULL DEFAULT 0,
  verified_by_user_id BIGINT UNSIGNED NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  KEY idx_qual_tenant_staff (tenant_id, staff_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS staff_documents (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  staff_id BIGINT UNSIGNED NOT NULL,
  document_type VARCHAR(50) NOT NULL,
  file_name VARCHAR(255) NOT NULL,
  file_url VARCHAR(500) NOT NULL,
  file_size BIGINT NOT NULL,
  content_type VARCHAR(100) NOT NULL DEFAULT 'application/pdf',
  expiry_date DATE NULL,
  is_verified TINYINT(1) NOT NULL DEFAULT 0,
  uploaded_by_user_id BIGINT UNSIGNED NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  KEY idx_doc_tenant_staff (tenant_id, staff_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS leave_types (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  name VARCHAR(50) NOT NULL,
  code VARCHAR(20) NOT NULL,
  description VARCHAR(255) NOT NULL DEFAULT '',
  default_entitlement_days INT NOT NULL DEFAULT 0,
  is_paid TINYINT(1) NOT NULL DEFAULT 1,
  requires_document TINYINT(1) NOT NULL DEFAULT 0,
  is_carry_forward_allowed TINYINT(1) NOT NULL DEFAULT 0,
  max_carry_forward_days INT NOT NULL DEFAULT 0,
  accrual_rule VARCHAR(20) NOT NULL DEFAULT 'yearly',
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  UNIQUE KEY uq_leave_type_tenant_code (tenant_id, code)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS leave_entitlements (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  staff_id BIGINT UNSIGNED NOT NULL,
  leave_type_id BIGINT UNSIGNED NOT NULL,
  academic_year INT NOT NULL,
  entitled_days DECIMAL(5,2) NOT NULL,
  carried_forward_days DECIMAL(5,2) NOT NULL DEFAULT 0.00,
  used_days DECIMAL(5,2) NOT NULL DEFAULT 0.00,
  remaining_days DECIMAL(5,2) NOT NULL,
  expiry_date DATE NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  UNIQUE KEY uq_entitlement_tenant_staff_type_year (tenant_id, staff_id, leave_type_id, academic_year),
  KEY idx_entitlement_tenant_staff_year (tenant_id, staff_id, academic_year)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS leave_requests (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  staff_id BIGINT UNSIGNED NOT NULL,
  leave_type_id BIGINT UNSIGNED NOT NULL,
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  days_requested DECIMAL(5,2) NOT NULL,
  reason VARCHAR(500) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  approver_user_id BIGINT UNSIGNED NULL,
  approved_at DATETIME NULL,
  approver_comment VARCHAR(500) NULL,
  document_url VARCHAR(500) NULL,
  requested_by_user_id BIGINT UNSIGNED NOT NULL,
  is_half_day TINYINT(1) NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  KEY idx_leave_req_tenant_staff (tenant_id, staff_id),
  KEY idx_leave_req_tenant_status (tenant_id, status),
  KEY idx_leave_req_tenant_dates (tenant_id, start_date, end_date)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS appraisal_cycles (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  name VARCHAR(100) NOT NULL,
  academic_year_id BIGINT UNSIGNED NOT NULL,
  term_id BIGINT UNSIGNED NULL,
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  description VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  KEY idx_app_cycle_tenant_year (tenant_id, academic_year_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS appraisal_criteria (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  appraisal_cycle_id BIGINT UNSIGNED NOT NULL,
  name VARCHAR(100) NOT NULL,
  description VARCHAR(255) NULL,
  weight INT NOT NULL DEFAULT 1,
  max_score INT NOT NULL DEFAULT 5,
  sort_order INT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_criteria_cycle FOREIGN KEY (appraisal_cycle_id) REFERENCES appraisal_cycles(id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS appraisals (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  appraisal_cycle_id BIGINT UNSIGNED NOT NULL,
  staff_id BIGINT UNSIGNED NOT NULL,
  appraiser_user_id BIGINT UNSIGNED NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  overall_score DECIMAL(5,2) NOT NULL DEFAULT 0.00,
  overall_comment TEXT NULL,
  staff_comment TEXT NULL,
  submitted_at DATETIME NULL,
  acknowledged_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_appraisal_cycle FOREIGN KEY (appraisal_cycle_id) REFERENCES appraisal_cycles(id) ON DELETE CASCADE,
  UNIQUE KEY uq_appraisal_tenant_cycle_staff (tenant_id, appraisal_cycle_id, staff_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS appraisal_scores (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  appraisal_id BIGINT UNSIGNED NOT NULL,
  criterion_id BIGINT UNSIGNED NOT NULL,
  score INT NOT NULL,
  comment VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_score_appraisal FOREIGN KEY (appraisal_id) REFERENCES appraisals(id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS disciplinary_records (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  staff_id BIGINT UNSIGNED NOT NULL,
  incident_date DATE NOT NULL,
  incident_type VARCHAR(50) NOT NULL,
  title VARCHAR(255) NOT NULL,
  description TEXT NOT NULL,
  severity VARCHAR(20) NOT NULL DEFAULT 'low',
  action_taken VARCHAR(500) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'open',
  reported_by_user_id BIGINT UNSIGNED NOT NULL,
  assigned_to_user_id BIGINT UNSIGNED NULL,
  visibility VARCHAR(20) NOT NULL DEFAULT 'hr_only',
  is_confidential TINYINT(1) NOT NULL DEFAULT 1,
  resolution_date DATE NULL,
  resolution_notes TEXT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  KEY idx_disc_tenant_staff (tenant_id, staff_id),
  KEY idx_disc_tenant_visibility (tenant_id, visibility)
) ENGINE=InnoDB;

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
