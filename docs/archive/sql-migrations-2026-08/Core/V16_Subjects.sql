-- Core Records Phase 1: Subjects - independent, no dependencies, foundation for teaching
-- Every tenant-owned table has tenant_id leading index, audit and soft-delete, unique per tenant

CREATE TABLE IF NOT EXISTS subjects (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  name VARCHAR(100) NOT NULL,
  code VARCHAR(20) NOT NULL,
  description VARCHAR(500) NULL,
  is_core TINYINT(1) NOT NULL DEFAULT 1,
  department VARCHAR(100) NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_subject_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_subject_tenant_code (tenant_id, code),
  KEY idx_subject_tenant_name (tenant_id, name),
  KEY idx_subject_tenant_department (tenant_id, department),
  KEY idx_subject_tenant_status (tenant_id, status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS grades (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  name VARCHAR(50) NOT NULL,
  code VARCHAR(20) NOT NULL,
  level_order INT NOT NULL DEFAULT 0,
  academic_year_id BIGINT UNSIGNED NOT NULL,
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_grade_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_grade_tenant_code_year (tenant_id, code, academic_year_id),
  KEY idx_grade_tenant_year_order (tenant_id, academic_year_id, level_order)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS grade_subjects (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  grade_id BIGINT UNSIGNED NOT NULL,
  subject_id BIGINT UNSIGNED NOT NULL,
  academic_year_id BIGINT UNSIGNED NOT NULL,
  is_compulsory TINYINT(1) NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_gs_grade FOREIGN KEY (grade_id) REFERENCES grades(id) ON DELETE CASCADE,
  CONSTRAINT fk_gs_subject FOREIGN KEY (subject_id) REFERENCES subjects(id) ON DELETE CASCADE,
  UNIQUE KEY uq_gs_tenant_grade_subject_year (tenant_id, grade_id, subject_id, academic_year_id),
  KEY idx_gs_tenant_year_grade (tenant_id, academic_year_id, grade_id)
) ENGINE=InnoDB;

-- Seed default subjects per tenant from SubjectDefaults (primary + secondary)
INSERT INTO subjects (tenant_id, name, code, description, is_core, department, status)
SELECT id, 'Mathematics', 'MATH', 'Core mathematics', 1, 'Sciences', 'active' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM subjects WHERE tenant_id=tenants.id AND code='MATH')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO subjects (tenant_id, name, code, is_core, department, status)
SELECT id, 'English', 'ENG', 1, 'Languages', 'active' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM subjects WHERE tenant_id=tenants.id AND code='ENG')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO subjects (tenant_id, name, code, is_core, department, status)
SELECT id, 'Science and Technology', 'SCI', 1, 'Sciences', 'active' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM subjects WHERE tenant_id=tenants.id AND code='SCI')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO subjects (tenant_id, name, code, is_core, department, status)
SELECT id, 'Heritage and Social Sciences', 'HER', 0, 'Arts', 'active' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM subjects WHERE tenant_id=tenants.id AND code='HER')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO subjects (tenant_id, name, code, is_core, department, status)
SELECT id, 'Computer Science', 'COMPSCI', 0, 'Sciences', 'active' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM subjects WHERE tenant_id=tenants.id AND code='COMPSCI')
ON DUPLICATE KEY UPDATE name=VALUES(name);
