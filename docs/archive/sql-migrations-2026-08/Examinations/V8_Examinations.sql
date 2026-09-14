-- Examinations Full Module V8 - Sessions grouping assessments, timetable venues invigilators, weighted composite, merit lists, promotion, transcripts, moderation, certificates

CREATE TABLE IF NOT EXISTS examination_sessions (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  name VARCHAR(100) NOT NULL,
  academic_year_id BIGINT UNSIGNED NOT NULL,
  term_id BIGINT UNSIGNED NOT NULL,
  session_type VARCHAR(20) NOT NULL DEFAULT 'final',
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  description VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_exam_sess_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_exam_sess_tenant_year_term (tenant_id, academic_year_id, term_id),
  KEY idx_exam_sess_tenant_status (tenant_id, status)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS examination_session_assessments (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  examination_session_id BIGINT UNSIGNED NOT NULL,
  assessment_id BIGINT UNSIGNED NOT NULL,
  subject_id BIGINT UNSIGNED NOT NULL,
  grade_id BIGINT UNSIGNED NOT NULL,
  stream_id BIGINT UNSIGNED NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_exam_sess_ass_sess FOREIGN KEY (examination_session_id) REFERENCES examination_sessions(id) ON DELETE CASCADE,
  UNIQUE KEY uq_exam_sess_ass (tenant_id, examination_session_id, assessment_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS examination_slots (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  examination_session_id BIGINT UNSIGNED NOT NULL,
  subject_id BIGINT UNSIGNED NOT NULL,
  grade_id BIGINT UNSIGNED NOT NULL,
  stream_id BIGINT UNSIGNED NULL,
  assessment_id BIGINT UNSIGNED NOT NULL,
  exam_date DATE NOT NULL,
  start_time TIME NOT NULL,
  end_time TIME NOT NULL,
  venue_id BIGINT UNSIGNED NULL,
  venue_name VARCHAR(100) NULL,
  invigilator_staff_id BIGINT UNSIGNED NULL,
  invigilator_name VARCHAR(100) NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'scheduled',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_exam_slot_sess FOREIGN KEY (examination_session_id) REFERENCES examination_sessions(id) ON DELETE CASCADE,
  UNIQUE KEY uq_exam_slot_class (tenant_id, examination_session_id, exam_date, start_time, grade_id, stream_id),
  KEY idx_exam_slot_tenant_date (tenant_id, exam_date),
  KEY idx_exam_slot_teacher (tenant_id, invigilator_staff_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS composite_weightings (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  academic_year_id BIGINT UNSIGNED NOT NULL,
  term_id BIGINT UNSIGNED NOT NULL,
  grade_id BIGINT UNSIGNED NULL,
  subject_id BIGINT UNSIGNED NULL,
  continuous_assessment_weight DECIMAL(5,2) NOT NULL DEFAULT 30.00,
  examination_weight DECIMAL(5,2) NOT NULL DEFAULT 70.00,
  description VARCHAR(255) NULL,
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  UNIQUE KEY uq_composite_tenant_year_term_grade_subject (tenant_id, academic_year_id, term_id, grade_id, subject_id),
  KEY idx_composite_tenant_year_term (tenant_id, academic_year_id, term_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS promotion_rules (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  academic_year_id BIGINT UNSIGNED NOT NULL,
  from_grade_id BIGINT UNSIGNED NOT NULL,
  to_grade_id BIGINT UNSIGNED NOT NULL,
  minimum_aggregate DECIMAL(6,2) NOT NULL DEFAULT 50.00,
  minimum_average DECIMAL(5,2) NULL,
  minimum_attendance_percentage DECIMAL(5,2) NULL,
  max_failed_subjects INT NULL,
  required_subjects_json JSON NULL,
  minimum_subject_score DECIMAL(5,2) NULL,
  description VARCHAR(500) NULL,
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  UNIQUE KEY uq_promotion_rule_tenant_year_from_to (tenant_id, academic_year_id, from_grade_id, to_grade_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS promotion_decisions (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  student_id BIGINT UNSIGNED NOT NULL,
  from_grade_id BIGINT UNSIGNED NOT NULL,
  to_grade_id BIGINT UNSIGNED NOT NULL,
  from_academic_year_id BIGINT UNSIGNED NOT NULL,
  to_academic_year_id BIGINT UNSIGNED NOT NULL,
  from_term_id BIGINT UNSIGNED NOT NULL,
  recommended_action VARCHAR(20) NOT NULL,
  final_action VARCHAR(20) NOT NULL,
  is_manual_override TINYINT(1) NOT NULL DEFAULT 0,
  override_justification VARCHAR(1000) NULL,
  decided_by_user_id BIGINT UNSIGNED NULL,
  decided_at DATETIME NULL,
  reason TEXT NOT NULL,
  aggregate_score DECIMAL(6,2) NOT NULL,
  average_score DECIMAL(5,2) NOT NULL,
  failed_subjects_count INT NOT NULL DEFAULT 0,
  attendance_percentage DECIMAL(5,2) NOT NULL DEFAULT 0.00,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  next_enrolment_id BIGINT UNSIGNED NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  KEY idx_promo_dec_tenant_student (tenant_id, student_id),
  KEY idx_promo_dec_tenant_year (tenant_id, from_academic_year_id, from_grade_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS promotion_batches (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  batch_number VARCHAR(50) NOT NULL,
  from_academic_year_id BIGINT UNSIGNED NOT NULL,
  to_academic_year_id BIGINT UNSIGNED NOT NULL,
  from_grade_id BIGINT UNSIGNED NOT NULL,
  to_grade_id BIGINT UNSIGNED NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  total_students INT NOT NULL DEFAULT 0,
  promoted_count INT NOT NULL DEFAULT 0,
  repeat_count INT NOT NULL DEFAULT 0,
  conditional_count INT NOT NULL DEFAULT 0,
  failed_count INT NOT NULL DEFAULT 0,
  result_json JSON NULL,
  started_at DATETIME NULL,
  completed_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  UNIQUE KEY uq_promo_batch_tenant_number (tenant_id, batch_number)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS transcripts (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  student_id BIGINT UNSIGNED NOT NULL,
  transcript_number VARCHAR(50) NOT NULL,
  generated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  generated_by_user_id BIGINT UNSIGNED NOT NULL,
  data_json JSON NULL,
  pdf_url VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  UNIQUE KEY uq_transcript_tenant_number (tenant_id, transcript_number),
  KEY idx_transcript_tenant_student (tenant_id, student_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS mark_moderations (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  assessment_id BIGINT UNSIGNED NOT NULL,
  student_id BIGINT UNSIGNED NOT NULL,
  subject_id BIGINT UNSIGNED NULL,
  current_stage VARCHAR(20) NOT NULL DEFAULT 'teacher',
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  is_locked TINYINT(1) NOT NULL DEFAULT 0,
  locked_at DATETIME NULL,
  locked_by_user_id BIGINT UNSIGNED NULL,
  teacher_user_id BIGINT UNSIGNED NULL,
  teacher_submitted_at DATETIME NULL,
  hod_user_id BIGINT UNSIGNED NULL,
  hod_approved_at DATETIME NULL,
  head_user_id BIGINT UNSIGNED NULL,
  head_approved_at DATETIME NULL,
  change_reason VARCHAR(1000) NULL,
  previous_score_json JSON NULL,
  new_score_json JSON NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  UNIQUE KEY uq_mark_mod_tenant_ass_student (tenant_id, assessment_id, student_id),
  KEY idx_mark_mod_tenant_ass (tenant_id, assessment_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS certificates (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  student_id BIGINT UNSIGNED NOT NULL,
  academic_year_id BIGINT UNSIGNED NOT NULL,
  term_id BIGINT UNSIGNED NOT NULL,
  certificate_type VARCHAR(30) NOT NULL,
  title VARCHAR(255) NOT NULL,
  description VARCHAR(500) NULL,
  data_json JSON NULL,
  pdf_url VARCHAR(500) NULL,
  issued_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  issued_by_user_id BIGINT UNSIGNED NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  KEY idx_cert_tenant_student (tenant_id, student_id),
  KEY idx_cert_tenant_type (tenant_id, certificate_type)
) ENGINE=InnoDB;

-- Seed default composite weightings per subject and per level
INSERT INTO composite_weightings (tenant_id, academic_year_id, term_id, grade_id, subject_id, continuous_assessment_weight, examination_weight, description)
SELECT id, 2026, 1, NULL, NULL, 30.00, 70.00, 'Default CA 30% Exam 70% for all subjects and levels' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM composite_weightings WHERE tenant_id=tenants.id AND grade_id IS NULL AND subject_id IS NULL)
ON DUPLICATE KEY UPDATE continuous_assessment_weight=VALUES(continuous_assessment_weight);
