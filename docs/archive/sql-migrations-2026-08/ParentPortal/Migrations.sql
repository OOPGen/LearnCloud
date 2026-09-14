-- Parent Portal Migration - Guardian invitation, parent-teacher messaging, settings

CREATE TABLE IF NOT EXISTS guardian_invitations (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  guardian_id BIGINT UNSIGNED NOT NULL,
  email VARCHAR(255) NOT NULL,
  phone VARCHAR(50) NULL,
  token_hash VARCHAR(128) NOT NULL,
  expires_at DATETIME NOT NULL,
  used_at DATETIME NULL,
  created_by_user_id BIGINT UNSIGNED NOT NULL,
  invitation_link VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_inv_guardian FOREIGN KEY (guardian_id) REFERENCES guardians(id) ON DELETE CASCADE,
  CONSTRAINT fk_inv_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_inv_token_hash (token_hash),
  KEY idx_inv_tenant_guardian (tenant_id, guardian_id),
  KEY idx_inv_expires (expires_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS parent_teacher_messages (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  guardian_id BIGINT UNSIGNED NOT NULL,
  student_id BIGINT UNSIGNED NOT NULL,
  grade_id BIGINT UNSIGNED NOT NULL,
  stream_id BIGINT UNSIGNED NOT NULL,
  sender_user_id BIGINT UNSIGNED NOT NULL,
  recipient_teacher_staff_id BIGINT UNSIGNED NULL,
  recipient_user_id BIGINT UNSIGNED NULL,
  subject VARCHAR(255) NOT NULL DEFAULT '',
  body TEXT NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'pending' COMMENT 'pending,approved,rejected,sent',
  requires_moderation TINYINT(1) NOT NULL DEFAULT 0,
  moderated_by_user_id BIGINT UNSIGNED NULL,
  moderated_at DATETIME NULL,
  moderation_note VARCHAR(255) NULL,
  rate_limit_key VARCHAR(100) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_ptm_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_ptm_tenant_guardian_student (tenant_id, guardian_id, student_id),
  KEY idx_ptm_tenant_grade_stream (tenant_id, grade_id, stream_id),
  KEY idx_ptm_rate_limit (rate_limit_key, created_at)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS parent_messaging_settings (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  enable_parent_teacher_messaging TINYINT(1) NOT NULL DEFAULT 1,
  require_moderation TINYINT(1) NOT NULL DEFAULT 0,
  rate_limit_per_hour INT NOT NULL DEFAULT 5,
  rate_limit_per_day INT NOT NULL DEFAULT 20,
  allowed_roles JSON NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  UNIQUE KEY uq_parent_msg_settings_tenant (tenant_id)
) ENGINE=InnoDB;

-- Insert default settings for existing tenants
INSERT INTO parent_messaging_settings (tenant_id, enable_parent_teacher_messaging, require_moderation, rate_limit_per_hour, rate_limit_per_day)
SELECT id, 1, 0, 5, 20 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM parent_messaging_settings WHERE tenant_id=tenants.id)
ON DUPLICATE KEY UPDATE enable_parent_teacher_messaging=VALUES(enable_parent_teacher_messaging);
