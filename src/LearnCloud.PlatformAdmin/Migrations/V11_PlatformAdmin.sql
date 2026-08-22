-- Platform Admin Console V11 - Tenant list, detail, business metrics, operational views, support notes, impersonation consented time-limited, announcement broadcast, second factor
-- Sits outside tenant scope and every action audited

CREATE TABLE IF NOT EXISTS support_notes (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  content TEXT NOT NULL,
  is_internal TINYINT(1) NOT NULL DEFAULT 1,
  category VARCHAR(50) NULL,
  created_by_user_id BIGINT UNSIGNED NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_support_note_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_support_note_tenant (tenant_id, created_at)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS impersonation_grants (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  granted_by_user_id BIGINT UNSIGNED NOT NULL,
  granted_by_role VARCHAR(50) NOT NULL DEFAULT 'SCHOOL_ADMIN',
  granted_to_role VARCHAR(50) NOT NULL DEFAULT 'PLATFORM_SUPERADMIN',
  has_consent TINYINT(1) NOT NULL DEFAULT 0,
  reason VARCHAR(500) NOT NULL,
  expires_at DATETIME NOT NULL,
  revoked_at DATETIME NULL,
  revoked_by_user_id BIGINT UNSIGNED NULL,
  token_hash VARCHAR(128) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_imp_grant_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_imp_grant_tenant_active (tenant_id, has_consent, expires_at, revoked_at),
  KEY idx_imp_grant_expires (expires_at)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS impersonation_sessions (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  grant_id BIGINT UNSIGNED NOT NULL,
  impersonator_user_id BIGINT UNSIGNED NOT NULL,
  impersonated_user_id BIGINT UNSIGNED NULL,
  started_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  expires_at DATETIME NOT NULL,
  ended_at DATETIME NULL,
  banner_message VARCHAR(500) NULL,
  ip_address VARCHAR(45) NULL,
  user_agent VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_imp_sess_grant FOREIGN KEY (grant_id) REFERENCES impersonation_grants(id) ON DELETE CASCADE,
  CONSTRAINT fk_imp_sess_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_imp_sess_tenant_active (tenant_id, ended_at, expires_at),
  KEY idx_imp_sess_impersonator (impersonator_user_id, ended_at, expires_at)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS announcement_broadcasts (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  title VARCHAR(255) NOT NULL,
  body TEXT NOT NULL,
  audience VARCHAR(50) NOT NULL DEFAULT 'all_school_admins',
  audience_filter_json JSON NULL,
  scheduled_at DATETIME NULL,
  expires_at DATETIME NULL,
  priority VARCHAR(20) NOT NULL DEFAULT 'normal',
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  created_by_user_id BIGINT UNSIGNED NOT NULL,
  sent_at DATETIME NULL,
  sent_count INT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  KEY idx_broadcast_status_scheduled (status, scheduled_at)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS tenant_health_scores (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  score INT NOT NULL DEFAULT 100,
  health_status VARCHAR(20) NOT NULL DEFAULT 'healthy',
  calculated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  factors_json JSON NULL,
  monthly_value DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  last_activity_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_health_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_health_tenant (tenant_id),
  KEY idx_health_status_score (health_status, score)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS background_job_records (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NULL,
  job_type VARCHAR(50) NOT NULL,
  job_id VARCHAR(100) NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  started_at DATETIME NULL,
  completed_at DATETIME NULL,
  retry_count INT NOT NULL DEFAULT 0,
  error_message TEXT NULL,
  result_json JSON NULL,
  created_by_user_id BIGINT UNSIGNED NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  KEY idx_job_tenant_type_status (tenant_id, job_type, status),
  KEY idx_job_status_created (status, created_at)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS error_rate_snapshots (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  snapshot_date DATE NOT NULL,
  tenant_id BIGINT UNSIGNED NULL,
  service VARCHAR(20) NOT NULL DEFAULT 'api',
  error_count INT NOT NULL DEFAULT 0,
  warning_count INT NOT NULL DEFAULT 0,
  request_count INT NOT NULL DEFAULT 0,
  error_rate DOUBLE NOT NULL DEFAULT 0.0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  KEY idx_error_snapshot_date (snapshot_date),
  KEY idx_error_tenant_service (tenant_id, service)
) ENGINE=InnoDB;
