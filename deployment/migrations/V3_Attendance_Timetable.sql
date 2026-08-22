-- LearnCloud Attendance & Timetable V3 - MySQL 8.0
-- Tenant attendance settings, registers, records, period definitions, timetables, slots
-- All tenant-owned tables tenant_id leading index, soft-delete, audit

-- Tenant attendance settings
CREATE TABLE IF NOT EXISTS tenant_attendance_settings (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  mode INT NOT NULL DEFAULT 1 COMMENT '1=Daily, 2=PerPeriod',
  backdating_window_days INT NOT NULL DEFAULT 7,
  allow_backdating_beyond_window TINYINT(1) NOT NULL DEFAULT 1,
  chronic_absence_threshold DECIMAL(5,2) NOT NULL DEFAULT 85.00,
  count_late_as_present TINYINT(1) NOT NULL DEFAULT 1,
  count_excused_as_present TINYINT(1) NOT NULL DEFAULT 1,
  count_sick_as_present TINYINT(1) NOT NULL DEFAULT 0,
  auto_save_interval_seconds INT NOT NULL DEFAULT 5,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_att_settings_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_att_settings_tenant (tenant_id),
  KEY idx_att_settings_tenant (tenant_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Period definitions per tenant
CREATE TABLE IF NOT EXISTS period_definitions (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  period_number INT NOT NULL,
  name VARCHAR(50) NOT NULL,
  start_time TIME NOT NULL,
  end_time TIME NOT NULL,
  is_break TINYINT(1) NOT NULL DEFAULT 0,
  sort_order INT NOT NULL DEFAULT 0,
  academic_year_id BIGINT UNSIGNED NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_period_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_period_tenant_number (tenant_id, period_number, academic_year_id),
  KEY idx_period_tenant_sort (tenant_id, sort_order)
) ENGINE=InnoDB;

-- Attendance registers header - guards duplicate
CREATE TABLE IF NOT EXISTS attendance_registers (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  academic_year_id BIGINT UNSIGNED NOT NULL,
  term_id BIGINT UNSIGNED NOT NULL,
  grade_id BIGINT UNSIGNED NOT NULL,
  stream_id BIGINT UNSIGNED NOT NULL,
  attendance_date DATE NOT NULL,
  period_number INT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  is_backdated TINYINT(1) NOT NULL DEFAULT 0,
  submitted_at DATETIME NULL,
  submitted_by_user_id BIGINT UNSIGNED NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_att_reg_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  CONSTRAINT fk_att_reg_grade FOREIGN KEY (grade_id) REFERENCES grades(id) ON DELETE RESTRICT,
  CONSTRAINT fk_att_reg_stream FOREIGN KEY (stream_id) REFERENCES streams(id) ON DELETE RESTRICT,
  UNIQUE KEY uq_att_reg_tenant_class_date_period (tenant_id, grade_id, stream_id, attendance_date, period_number, academic_year_id, term_id),
  KEY idx_att_reg_tenant_stream_date (tenant_id, stream_id, attendance_date, period_number),
  KEY idx_att_reg_tenant_year_term (tenant_id, academic_year_id, term_id, attendance_date)
) ENGINE=InnoDB;

-- Attendance records per learner
CREATE TABLE IF NOT EXISTS attendance_records (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  register_id BIGINT UNSIGNED NOT NULL,
  student_id BIGINT UNSIGNED NOT NULL,
  grade_id BIGINT UNSIGNED NOT NULL,
  stream_id BIGINT UNSIGNED NOT NULL,
  academic_year_id BIGINT UNSIGNED NOT NULL,
  term_id BIGINT UNSIGNED NOT NULL,
  attendance_date DATE NOT NULL,
  period_number INT NULL,
  status INT NOT NULL COMMENT '1=Present,2=Absent,3=Late,4=Sick,5=Excused',
  absence_reason VARCHAR(100) NULL,
  note VARCHAR(255) NULL,
  marked_by_user_id BIGINT UNSIGNED NOT NULL,
  marked_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_backdated TINYINT(1) NOT NULL DEFAULT 0,
  backdate_reason VARCHAR(255) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_att_rec_register FOREIGN KEY (register_id) REFERENCES attendance_registers(id) ON DELETE CASCADE,
  CONSTRAINT fk_att_rec_student FOREIGN KEY (student_id) REFERENCES students(id) ON DELETE RESTRICT,
  CONSTRAINT fk_att_rec_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_att_rec_tenant_student_date_period (tenant_id, student_id, attendance_date, period_number),
  KEY idx_att_rec_tenant_stream_date (tenant_id, stream_id, attendance_date, status),
  KEY idx_att_rec_tenant_student_date (tenant_id, student_id, attendance_date),
  KEY idx_att_rec_tenant_year_term (tenant_id, academic_year_id, term_id, attendance_date)
) ENGINE=InnoDB;

-- Timetables effective dated
CREATE TABLE IF NOT EXISTS timetables (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  name VARCHAR(100) NOT NULL,
  academic_year_id BIGINT UNSIGNED NOT NULL,
  term_id BIGINT UNSIGNED NOT NULL,
  effective_from DATE NOT NULL,
  effective_to DATE NULL,
  version INT NOT NULL DEFAULT 1,
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  created_from_timetable_id BIGINT UNSIGNED NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_tt_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_tt_tenant_year_term_eff (tenant_id, academic_year_id, term_id, effective_from, effective_to),
  KEY idx_tt_tenant_status (tenant_id, status)
) ENGINE=InnoDB;

-- Timetable slots
CREATE TABLE IF NOT EXISTS timetable_slots (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  timetable_id BIGINT UNSIGNED NOT NULL,
  academic_year_id BIGINT UNSIGNED NOT NULL,
  term_id BIGINT UNSIGNED NOT NULL,
  grade_id BIGINT UNSIGNED NOT NULL,
  stream_id BIGINT UNSIGNED NOT NULL,
  subject_id BIGINT UNSIGNED NOT NULL,
  teacher_staff_id BIGINT UNSIGNED NOT NULL,
  room_id BIGINT UNSIGNED NULL,
  day_of_week TINYINT NOT NULL COMMENT '1=Mon..7=Sun',
  period_number INT NOT NULL,
  start_time TIME NOT NULL,
  end_time TIME NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_tt_slot_timetable FOREIGN KEY (timetable_id) REFERENCES timetables(id) ON DELETE CASCADE,
  CONSTRAINT fk_tt_slot_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_tt_slot_class (tenant_id, timetable_id, day_of_week, period_number, stream_id),
  UNIQUE KEY uq_tt_slot_teacher (tenant_id, timetable_id, day_of_week, period_number, teacher_staff_id),
  UNIQUE KEY uq_tt_slot_room (tenant_id, timetable_id, day_of_week, period_number, room_id),
  KEY idx_tt_slot_tenant_teacher (tenant_id, teacher_staff_id, academic_year_id, term_id, day_of_week),
  KEY idx_tt_slot_tenant_stream (tenant_id, stream_id, academic_year_id, term_id, day_of_week),
  KEY idx_tt_slot_tenant_grade (tenant_id, grade_id, academic_year_id, term_id)
) ENGINE=InnoDB;

-- Seed default periods for existing tenants (if none)
-- Handled in service GetPeriodsAsync creates 8 periods + breaks if none
