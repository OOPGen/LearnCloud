-- Student Portal Migration - School-level setting controlling whether students may see fee info, UserId link for student login

-- Add user_id to students table for secure login
ALTER TABLE students ADD COLUMN IF NOT EXISTS user_id BIGINT UNSIGNED NULL;
ALTER TABLE students ADD CONSTRAINT IF NOT EXISTS fk_students_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS idx_students_tenant_user ON students(tenant_id, user_id);

CREATE TABLE IF NOT EXISTS student_portal_settings (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  allow_students_view_fees TINYINT(1) NOT NULL DEFAULT 0 COMMENT 'School-level setting controlling whether students may see fee information at all',
  allow_students_view_guardian_contacts TINYINT(1) NOT NULL DEFAULT 0,
  allow_students_submit_assignments TINYINT(1) NOT NULL DEFAULT 1,
  allow_students_view_attendance TINYINT(1) NOT NULL DEFAULT 1,
  allow_students_view_results TINYINT(1) NOT NULL DEFAULT 1,
  allow_students_view_timetable TINYINT(1) NOT NULL DEFAULT 1,
  allow_students_message_teacher TINYINT(1) NOT NULL DEFAULT 0,
  welcome_message VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  UNIQUE KEY uq_student_portal_settings_tenant (tenant_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Student assignment submissions
CREATE TABLE IF NOT EXISTS student_assignment_submissions (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  assignment_id BIGINT UNSIGNED NOT NULL,
  student_id BIGINT UNSIGNED NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  submitted_at DATETIME NULL,
  file_url VARCHAR(500) NULL,
  file_name VARCHAR(255) NULL,
  note VARCHAR(500) NULL,
  teacher_feedback VARCHAR(500) NULL,
  score DECIMAL(5,2) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  UNIQUE KEY uq_submission_tenant_assignment_student (tenant_id, assignment_id, student_id),
  KEY idx_submission_tenant_student (tenant_id, student_id)
) ENGINE=InnoDB;

-- Insert default settings for existing tenants - fees not visible by default (school must opt-in)
INSERT INTO student_portal_settings (tenant_id, allow_students_view_fees, allow_students_submit_assignments)
SELECT id, 0, 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM student_portal_settings WHERE tenant_id=tenants.id)
ON DUPLICATE KEY UPDATE allow_students_view_fees=VALUES(allow_students_view_fees);
