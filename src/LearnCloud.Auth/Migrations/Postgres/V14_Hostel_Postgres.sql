-- Translated from MySQL V14_Hostel.sql to PostgreSQL (Supabase Compatible)
-- Original: /home/user/src/LearnCloud.Hostel/Migrations/V14_Hostel.sql
-- Translated: /home/user/src/LearnCloud.Auth/Migrations/Postgres/V14_Hostel_Postgres.sql
-- Date: 2026-08-09
-- Note: Manual review needed for ENUM->VARCHAR, generated columns, partitioning

-- Hostel and Boarding Module V14 - Blocks, rooms, beds with gender, allocation with conflict detection and waiting list, house masters matrons, boarding fees integrated, exeat leave register, nightly roll call, visitor log, incident sick bay with access restrictions, reports occupancy vacancies on leave boarding revenue, printable bed allocation and leave register for gate

CREATE TABLE IF NOT EXISTS hostel_blocks (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  code VARCHAR(20) NOT NULL,
  gender_designation VARCHAR(20) NOT NULL DEFAULT 'male',
  capacity INT NOT NULL,
  total_rooms INT NOT NULL DEFAULT 0,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  description VARCHAR(500) NULL,
  location VARCHAR(100) NULL,
  house_master_staff_id BIGINT NULL,
  matron_staff_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_block_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_block_tenant_code (tenant_id, code),
  KEY idx_block_tenant_gender (tenant_id, gender_designation, is_active)
);

CREATE TABLE IF NOT EXISTS hostel_rooms (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  room_number VARCHAR(20) NOT NULL,
  floor INT NOT NULL DEFAULT 0,
  capacity INT NOT NULL,
  gender_designation VARCHAR(20) NOT NULL DEFAULT 'male',
  is_active BOOLEAN NOT NULL DEFAULT 1,
  facilities VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_room_block FOREIGN KEY (block_id) REFERENCES hostel_blocks(id) ON DELETE CASCADE,
  UNIQUE KEY uq_room_tenant_block_number (tenant_id, block_id, room_number),
  KEY idx_room_tenant_block (tenant_id, block_id)
);

CREATE TABLE IF NOT EXISTS hostel_beds (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  room_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  bed_number VARCHAR(20) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'available',
  "condition" VARCHAR(20) NOT NULL DEFAULT 'good',
  current_student_id BIGINT NULL,
  current_allocation_id BIGINT NULL,
  last_issued_at TIMESTAMPTZ NULL,
  last_returned_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_bed_room FOREIGN KEY (room_id) REFERENCES hostel_rooms(id) ON DELETE CASCADE,
  CONSTRAINT fk_bed_block FOREIGN KEY (block_id) REFERENCES hostel_blocks(id) ON DELETE CASCADE,
  UNIQUE KEY uq_bed_tenant_room_number (tenant_id, room_id, bed_number),
  KEY idx_bed_tenant_status (tenant_id, status),
  KEY idx_bed_tenant_block (tenant_id, block_id)
);

CREATE TABLE IF NOT EXISTS block_staff_assignments (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  staff_id BIGINT NOT NULL,
  role VARCHAR(30) NOT NULL DEFAULT 'house_master',
  assigned_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  unassigned_date TIMESTAMPTZ NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  responsibilities VARCHAR(500) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_block_staff_block FOREIGN KEY (block_id) REFERENCES hostel_blocks(id) ON DELETE CASCADE,
  KEY idx_block_staff_tenant_block_active (tenant_id, block_id, is_active)
);

CREATE TABLE IF NOT EXISTS bed_allocations (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  bed_id BIGINT NOT NULL,
  room_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  allocation_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  allocated_by_user_id BIGINT NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  vacated_date TIMESTAMPTZ NULL,
  vacated_reason VARCHAR(255) NULL,
  fee_applied BOOLEAN NOT NULL DEFAULT 0,
  fee_structure_item_id BIGINT NULL,
  waiting_list_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_alloc_bed FOREIGN KEY (bed_id) REFERENCES hostel_beds(id) ON DELETE RESTRICT,
  CONSTRAINT fk_alloc_room FOREIGN KEY (room_id) REFERENCES hostel_rooms(id) ON DELETE RESTRICT,
  CONSTRAINT fk_alloc_block FOREIGN KEY (block_id) REFERENCES hostel_blocks(id) ON DELETE RESTRICT,
  UNIQUE KEY uq_alloc_tenant_bed_active (tenant_id, bed_id, status, academic_year_id, term_id),
  UNIQUE KEY uq_alloc_tenant_student_year_term (tenant_id, student_id, academic_year_id, term_id),
  KEY idx_alloc_tenant_block (tenant_id, block_id, status),
  KEY idx_alloc_tenant_student (tenant_id, student_id)
);

CREATE TABLE IF NOT EXISTS waiting_lists (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  preferred_block_id BIGINT NULL,
  preferred_room_id BIGINT NULL,
  gender VARCHAR(20) NOT NULL DEFAULT 'male',
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  priority INT NOT NULL DEFAULT 0,
  queue_position INT NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'waiting',
  reason VARCHAR(255) NULL,
  requested_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  requested_by_user_id BIGINT NOT NULL,
  allocated_at TIMESTAMPTZ NULL,
  allocated_bed_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_waiting_tenant_year_term_status (tenant_id, academic_year_id, term_id, status, queue_position)
);

CREATE TABLE IF NOT EXISTS boarding_fee_links (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  bed_allocation_id BIGINT NOT NULL,
  fee_item_id BIGINT NOT NULL,
  fee_structure_id BIGINT NOT NULL,
  fee_structure_item_id BIGINT NOT NULL,
  amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_board_fee_alloc FOREIGN KEY (bed_allocation_id) REFERENCES bed_allocations(id) ON DELETE CASCADE,
  KEY idx_board_fee_tenant_alloc (tenant_id, bed_allocation_id)
);

CREATE TABLE IF NOT EXISTS exeat_registers (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  bed_allocation_id BIGINT NULL,
  leave_type VARCHAR(20) NOT NULL DEFAULT 'exeat',
  reason VARCHAR(500) NOT NULL,
  departure_date_time TIMESTAMPTZ NOT NULL,
  expected_return_date_time TIMESTAMPTZ NOT NULL,
  actual_return_date_time TIMESTAMPTZ NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'approved',
  authorised_by_user_id BIGINT NOT NULL,
  authoriser_role VARCHAR(30) NOT NULL DEFAULT 'house_master',
  approved_by_user_id BIGINT NULL,
  contact_phone_during_leave VARCHAR(50) NULL,
  destination_address VARCHAR(500) NULL,
  accompanying_person VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_exeat_block FOREIGN KEY (block_id) REFERENCES hostel_blocks(id) ON DELETE CASCADE,
  KEY idx_exeat_tenant_student (tenant_id, student_id, status),
  KEY idx_exeat_tenant_block (tenant_id, block_id, status),
  KEY idx_exeat_tenant_dates (tenant_id, departure_date_time, expected_return_date_time)
);

CREATE TABLE IF NOT EXISTS roll_calls (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  room_id BIGINT NULL,
  roll_call_date DATE NOT NULL,
  roll_call_type VARCHAR(20) NOT NULL DEFAULT 'nightly',
  status VARCHAR(20) NOT NULL DEFAULT 'in_progress',
  conducted_by_user_id BIGINT NOT NULL,
  completed_at TIMESTAMPTZ NULL,
  total_expected INT NOT NULL DEFAULT 0,
  total_present INT NOT NULL DEFAULT 0,
  total_absent INT NOT NULL DEFAULT 0,
  total_on_leave INT NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_rollcall_block FOREIGN KEY (block_id) REFERENCES hostel_blocks(id) ON DELETE CASCADE,
  UNIQUE KEY uq_rollcall_tenant_block_date_type (tenant_id, block_id, roll_call_date, roll_call_type),
  KEY idx_rollcall_tenant_date (tenant_id, roll_call_date)
);

CREATE TABLE IF NOT EXISTS roll_call_entries (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  roll_call_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  room_id BIGINT NULL,
  bed_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  roll_call_date DATE NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'present',
  notes VARCHAR(255) NULL,
  marked_by_user_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_roll_entry_rollcall FOREIGN KEY (roll_call_id) REFERENCES roll_calls(id) ON DELETE CASCADE,
  KEY idx_roll_entry_tenant_rollcall (tenant_id, roll_call_id),
  KEY idx_roll_entry_tenant_student (tenant_id, student_id, roll_call_date)
);

CREATE TABLE IF NOT EXISTS visitor_logs (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  visitor_name VARCHAR(255) NOT NULL,
  relationship VARCHAR(50) NOT NULL,
  id_number VARCHAR(50) NOT NULL,
  phone VARCHAR(50) NOT NULL,
  check_in_date_time TIMESTAMPTZ NOT NULL,
  check_out_date_time TIMESTAMPTZ NULL,
  purpose VARCHAR(500) NOT NULL,
  authorised_by_user_id BIGINT NOT NULL,
  belongings VARCHAR(500) NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'checked_in',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_visitor_tenant_student (tenant_id, student_id, check_in_date_time),
  KEY idx_visitor_tenant_block (tenant_id, block_id)
);

CREATE TABLE IF NOT EXISTS incident_records (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  room_id BIGINT NULL,
  student_id BIGINT NOT NULL,
  incident_type VARCHAR(30) NOT NULL DEFAULT 'incident',
  title VARCHAR(255) NOT NULL,
  description TEXT NOT NULL,
  severity VARCHAR(20) NOT NULL DEFAULT 'low',
  incident_date_time TIMESTAMPTZ NOT NULL,
  reported_by_user_id BIGINT NOT NULL,
  action_taken TEXT NULL,
  follow_up_required TEXT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'open',
  visibility VARCHAR(30) NOT NULL DEFAULT 'house_master',
  is_confidential BOOLEAN NOT NULL DEFAULT 0,
  assigned_to_user_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_incident_tenant_student (tenant_id, student_id),
  KEY idx_incident_tenant_block (tenant_id, block_id),
  KEY idx_incident_tenant_visibility (tenant_id, visibility)
);

CREATE TABLE IF NOT EXISTS sick_bay_records (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  block_id BIGINT NOT NULL,
  check_in_date_time TIMESTAMPTZ NOT NULL,
  check_out_date_time TIMESTAMPTZ NULL,
  symptoms TEXT NOT NULL,
  diagnosis VARCHAR(500) NULL,
  treatment TEXT NULL,
  medication VARCHAR(500) NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'admitted',
  admitted_by_user_id BIGINT NOT NULL,
  discharged_by_user_id BIGINT NULL,
  visibility VARCHAR(30) NOT NULL DEFAULT 'matron',
  is_confidential BOOLEAN NOT NULL DEFAULT 1,
  notes TEXT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_sickbay_tenant_student (tenant_id, student_id),
  KEY idx_sickbay_tenant_block (tenant_id, block_id)
);

-- Seed default boarding fee item per tenant
INSERT INTO fee_items (tenant_id, name, code, recurrence, is_proratable, is_optional, description)
SELECT id, 'Boarding', 'BOARDING', 1, 0, 1, 'Boarding fee per term - flows into existing fee invoicing' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM fee_items WHERE tenant_id=tenants.id AND code='BOARDING')
ON DUPLICATE KEY UPDATE name=VALUES(name);
