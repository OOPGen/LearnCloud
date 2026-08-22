-- WizardProgress table - stores progress after every step for resume
CREATE TABLE IF NOT EXISTS wizard_progress (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  current_step INT NOT NULL DEFAULT 1,
  is_completed TINYINT(1) NOT NULL DEFAULT 0,
  completed_at DATETIME NULL,
  last_saved_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  steps_status_json JSON NOT NULL,
  data_json JSON NULL,
  summary_json JSON NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_wizard_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_wizard_tenant (tenant_id),
  KEY idx_wizard_tenant_step (tenant_id, current_step)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- TenantSettings already exists from multi-tenancy, ensure columns for wizard
ALTER TABLE tenant_settings
  ADD COLUMN IF NOT EXISTS branding_json JSON NULL,
  ADD COLUMN IF NOT EXISTS features_json JSON NULL;
