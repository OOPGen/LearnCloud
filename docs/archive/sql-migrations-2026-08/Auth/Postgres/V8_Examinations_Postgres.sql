-- Translated from MySQL V8_Examinations.sql to PostgreSQL (Supabase Compatible)
-- Original: /home/user/src/LearnCloud.Examinations/Migrations/V8_Examinations.sql
-- Translated: /home/user/src/LearnCloud.Auth/Migrations/Postgres/V8_Examinations_Postgres.sql
-- Date: 2026-08-09
-- Note: Manual review needed for ENUM->VARCHAR, generated columns, partitioning

-- Examinations Full Module V8 - Sessions grouping assessments, timetable venues invigilators, weighted composite, merit lists, promotion, transcripts, moderation, certificates

CREATE TABLE IF NOT EXISTS examination_sessions (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  session_type VARCHAR(20) NOT NULL DEFAULT 'final',
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  description VARCHAR(500) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_exam_sess_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_exam_sess_tenant_year_term (tenant_id, academic_year_id, term_id),
  KEY idx_exam_sess_tenant_status (tenant_id, status)
);

CREATE TABLE IF NOT EXISTS examination_session_assessments (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  examination_session_id BIGINT NOT NULL,
  assessment_id BIGINT NOT NULL,
  subject_id BIGINT NOT NULL,
  grade_id BIGINT NOT NULL,
  stream_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_exam_sess_ass_sess FOREIGN KEY (examination_session_id) REFERENCES examination_sessions(id) ON DELETE CASCADE,
  UNIQUE KEY uq_exam_sess_ass (tenant_id, examination_session_id, assessment_id)
);

CREATE TABLE IF NOT EXISTS examination_slots (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  examination_session_id BIGINT NOT NULL,
  subject_id BIGINT NOT NULL,
  grade_id BIGINT NOT NULL,
  stream_id BIGINT NULL,
  assessment_id BIGINT NOT NULL,
  exam_date DATE NOT NULL,
  start_time TIME NOT NULL,
  end_time TIME NOT NULL,
  venue_id BIGINT NULL,
  venue_name VARCHAR(100) NULL,
  invigilator_staff_id BIGINT NULL,
  invigilator_name VARCHAR(100) NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'scheduled',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_exam_slot_sess FOREIGN KEY (examination_session_id) REFERENCES examination_sessions(id) ON DELETE CASCADE,
  UNIQUE KEY uq_exam_slot_class (tenant_id, examination_session_id, exam_date, start_time, grade_id, stream_id),
  KEY idx_exam_slot_tenant_date (tenant_id, exam_date),
  KEY idx_exam_slot_teacher (tenant_id, invigilator_staff_id)
);

CREATE TABLE IF NOT EXISTS composite_weightings (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  grade_id BIGINT NULL,
  subject_id BIGINT NULL,
  continuous_assessment_weight DECIMAL(5,2) NOT NULL DEFAULT 30.00,
  examination_weight DECIMAL(5,2) NOT NULL DEFAULT 70.00,
  description VARCHAR(255) NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_composite_tenant_year_term_grade_subject (tenant_id, academic_year_id, term_id, grade_id, subject_id),
  KEY idx_composite_tenant_year_term (tenant_id, academic_year_id, term_id)
);

CREATE TABLE IF NOT EXISTS promotion_rules (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  from_grade_id BIGINT NOT NULL,
  to_grade_id BIGINT NOT NULL,
  minimum_aggregate DECIMAL(6,2) NOT NULL DEFAULT 50.00,
  minimum_average DECIMAL(5,2) NULL,
  minimum_attendance_percentage DECIMAL(5,2) NULL,
  max_failed_subjects INT NULL,
  required_subjects_json JSONB NULL,
  minimum_subject_score DECIMAL(5,2) NULL,
  description VARCHAR(500) NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_promotion_rule_tenant_year_from_to (tenant_id, academic_year_id, from_grade_id, to_grade_id)
);

CREATE TABLE IF NOT EXISTS promotion_decisions (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  from_grade_id BIGINT NOT NULL,
  to_grade_id BIGINT NOT NULL,
  from_academic_year_id BIGINT NOT NULL,
  to_academic_year_id BIGINT NOT NULL,
  from_term_id BIGINT NOT NULL,
  recommended_action VARCHAR(20) NOT NULL,
  final_action VARCHAR(20) NOT NULL,
  is_manual_override BOOLEAN NOT NULL DEFAULT 0,
  override_justification VARCHAR(1000) NULL,
  decided_by_user_id BIGINT NULL,
  decided_at TIMESTAMPTZ NULL,
  reason TEXT NOT NULL,
  aggregate_score DECIMAL(6,2) NOT NULL,
  average_score DECIMAL(5,2) NOT NULL,
  failed_subjects_count INT NOT NULL DEFAULT 0,
  attendance_percentage DECIMAL(5,2) NOT NULL DEFAULT 0.00,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  next_enrolment_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_promo_dec_tenant_student (tenant_id, student_id),
  KEY idx_promo_dec_tenant_year (tenant_id, from_academic_year_id, from_grade_id)
);

CREATE TABLE IF NOT EXISTS promotion_batches (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  batch_number VARCHAR(50) NOT NULL,
  from_academic_year_id BIGINT NOT NULL,
  to_academic_year_id BIGINT NOT NULL,
  from_grade_id BIGINT NOT NULL,
  to_grade_id BIGINT NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  total_students INT NOT NULL DEFAULT 0,
  promoted_count INT NOT NULL DEFAULT 0,
  repeat_count INT NOT NULL DEFAULT 0,
  conditional_count INT NOT NULL DEFAULT 0,
  failed_count INT NOT NULL DEFAULT 0,
  result_json JSONB NULL,
  started_at TIMESTAMPTZ NULL,
  completed_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_promo_batch_tenant_number (tenant_id, batch_number)
);

CREATE TABLE IF NOT EXISTS transcripts (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  transcript_number VARCHAR(50) NOT NULL,
  generated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  generated_by_user_id BIGINT NOT NULL,
  data_json JSONB NULL,
  pdf_url VARCHAR(500) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_transcript_tenant_number (tenant_id, transcript_number),
  KEY idx_transcript_tenant_student (tenant_id, student_id)
);

CREATE TABLE IF NOT EXISTS mark_moderations (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  assessment_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  subject_id BIGINT NULL,
  current_stage VARCHAR(20) NOT NULL DEFAULT 'teacher',
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  is_locked BOOLEAN NOT NULL DEFAULT 0,
  locked_at TIMESTAMPTZ NULL,
  locked_by_user_id BIGINT NULL,
  teacher_user_id BIGINT NULL,
  teacher_submitted_at TIMESTAMPTZ NULL,
  hod_user_id BIGINT NULL,
  hod_approved_at TIMESTAMPTZ NULL,
  head_user_id BIGINT NULL,
  head_approved_at TIMESTAMPTZ NULL,
  change_reason VARCHAR(1000) NULL,
  previous_score_json JSONB NULL,
  new_score_json JSONB NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_mark_mod_tenant_ass_student (tenant_id, assessment_id, student_id),
  KEY idx_mark_mod_tenant_ass (tenant_id, assessment_id)
);

CREATE TABLE IF NOT EXISTS certificates (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  certificate_type VARCHAR(30) NOT NULL,
  title VARCHAR(255) NOT NULL,
  description VARCHAR(500) NULL,
  data_json JSONB NULL,
  pdf_url VARCHAR(500) NULL,
  issued_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  issued_by_user_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_cert_tenant_student (tenant_id, student_id),
  KEY idx_cert_tenant_type (tenant_id, certificate_type)
);

-- Seed default composite weightings per subject and per level
INSERT INTO composite_weightings (tenant_id, academic_year_id, term_id, grade_id, subject_id, continuous_assessment_weight, examination_weight, description)
SELECT id, 2026, 1, NULL, NULL, 30.00, 70.00, 'Default CA 30% Exam 70% for all subjects and levels' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM composite_weightings WHERE tenant_id=tenants.id AND grade_id IS NULL AND subject_id IS NULL)
ON DUPLICATE KEY UPDATE continuous_assessment_weight=VALUES(continuous_assessment_weight);
