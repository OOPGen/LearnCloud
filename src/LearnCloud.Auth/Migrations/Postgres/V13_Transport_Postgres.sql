-- Translated from MySQL V13_Transport.sql to PostgreSQL (Supabase Compatible)
-- Original: /home/user/src/LearnCloud.Transport/Migrations/V13_Transport.sql
-- Translated: /home/user/src/LearnCloud.Auth/Migrations/Postgres/V13_Transport_Postgres.sql
-- Date: 2026-08-09
-- Note: Manual review needed for ENUM->VARCHAR, generated columns, partitioning

-- Transport Module V13 - Routes with ordered stops, vehicles capacity registration insurance licence expiry reminders, drivers assistants licence expiry, learner assignment capacity enforcement, transport fees flow into existing fee structure, boarding attendance, route change absence notifications via messaging, reports utilisation revenue unassigned, printable manifest

CREATE TABLE IF NOT EXISTS routes (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  code VARCHAR(20) NOT NULL,
  description VARCHAR(500) NULL,
  direction VARCHAR(20) NOT NULL DEFAULT 'both',
  is_active BOOLEAN NOT NULL DEFAULT 1,
  academic_year_id BIGINT NULL,
  term_id BIGINT NULL,
  total_distance_km DECIMAL(8,2) NOT NULL DEFAULT 0.00,
  estimated_duration_minutes INT NOT NULL DEFAULT 60,
  fee_amount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  vehicle_id BIGINT NULL,
  driver_id BIGINT NULL,
  assistant_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_route_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_route_tenant_code (tenant_id, code),
  KEY idx_route_tenant_active (tenant_id, is_active)
);

CREATE TABLE IF NOT EXISTS route_stops (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  route_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  address VARCHAR(500) NULL,
  latitude DECIMAL(10,8) NULL,
  longitude DECIMAL(11,8) NULL,
  order_number INT NOT NULL,
  expected_arrival_time TIME NULL,
  expected_departure_time TIME NULL,
  distance_from_start_km DECIMAL(8,2) NOT NULL DEFAULT 0.00,
  estimated_minutes_from_start INT NOT NULL DEFAULT 0,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_stop_route FOREIGN KEY (route_id) REFERENCES routes(id) ON DELETE CASCADE,
  KEY idx_stop_tenant_route_order (tenant_id, route_id, order_number),
  UNIQUE KEY uq_stop_tenant_route_order (tenant_id, route_id, order_number)
);

CREATE TABLE IF NOT EXISTS vehicles (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  registration_number VARCHAR(20) NOT NULL,
  make VARCHAR(50) NOT NULL,
  model VARCHAR(50) NOT NULL,
  capacity INT NOT NULL,
  year INT NOT NULL,
  fuel_type VARCHAR(20) NULL,
  insurance_expiry DATE NOT NULL,
  licence_expiry DATE NOT NULL,
  fitness_expiry DATE NULL,
  service_due_date DATE NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  notes TEXT NULL,
  last_insurance_reminder_sent_at TIMESTAMPTZ NULL,
  last_licence_reminder_sent_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_vehicle_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_vehicle_tenant_reg (tenant_id, registration_number),
  KEY idx_vehicle_tenant_expiry (tenant_id, insurance_expiry, licence_expiry)
);

CREATE TABLE IF NOT EXISTS drivers (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  full_name VARCHAR(255) NOT NULL,
  role VARCHAR(20) NOT NULL DEFAULT 'driver',
  staff_id BIGINT NULL,
  licence_number VARCHAR(50) NULL,
  licence_type VARCHAR(20) NULL,
  licence_expiry DATE NULL,
  medical_expiry DATE NULL,
  phone VARCHAR(50) NULL,
  email VARCHAR(255) NULL,
  id_number VARCHAR(50) NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  notes TEXT NULL,
  last_licence_reminder_sent_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_driver_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  KEY idx_driver_tenant_role (tenant_id, role, is_active),
  KEY idx_driver_tenant_licence_expiry (tenant_id, licence_expiry)
);

CREATE TABLE IF NOT EXISTS transport_assignments (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  student_id BIGINT NOT NULL,
  route_id BIGINT NOT NULL,
  pickup_stop_id BIGINT NOT NULL,
  drop_stop_id BIGINT NULL,
  assigned_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  assigned_by_user_id BIGINT NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  unassigned_date TIMESTAMPTZ NULL,
  unassigned_reason VARCHAR(255) NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  fee_structure_item_id BIGINT NULL,
  fee_applied BOOLEAN NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_assign_route FOREIGN KEY (route_id) REFERENCES routes(id) ON DELETE CASCADE,
  CONSTRAINT fk_assign_pickup_stop FOREIGN KEY (pickup_stop_id) REFERENCES route_stops(id) ON DELETE RESTRICT,
  CONSTRAINT fk_assign_drop_stop FOREIGN KEY (drop_stop_id) REFERENCES route_stops(id) ON DELETE SET NULL,
  UNIQUE KEY uq_assign_tenant_student_year_term (tenant_id, student_id, academic_year_id, term_id),
  KEY idx_assign_tenant_route_status (tenant_id, route_id, status),
  KEY idx_assign_tenant_student (tenant_id, student_id)
);

CREATE TABLE IF NOT EXISTS transport_fee_links (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  transport_assignment_id BIGINT NOT NULL,
  fee_item_id BIGINT NOT NULL,
  fee_structure_id BIGINT NOT NULL,
  fee_structure_item_id BIGINT NOT NULL,
  amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_fee_link_assign FOREIGN KEY (transport_assignment_id) REFERENCES transport_assignments(id) ON DELETE CASCADE,
  KEY idx_fee_link_tenant_assignment (tenant_id, transport_assignment_id)
);

CREATE TABLE IF NOT EXISTS transport_attendances (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  route_id BIGINT NOT NULL,
  route_stop_id BIGINT NULL,
  student_id BIGINT NOT NULL,
  transport_assignment_id BIGINT NOT NULL,
  trip_date DATE NOT NULL,
  trip_type VARCHAR(20) NOT NULL DEFAULT 'morning',
  status VARCHAR(20) NOT NULL DEFAULT 'boarded',
  actual_boarding_time TIME NULL,
  marked_by_user_id BIGINT NOT NULL,
  notes VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_ta_route FOREIGN KEY (route_id) REFERENCES routes(id) ON DELETE CASCADE,
  UNIQUE KEY uq_ta_tenant_route_student_date_type (tenant_id, route_id, student_id, trip_date, trip_type),
  KEY idx_ta_tenant_route_date (tenant_id, route_id, trip_date)
);

CREATE TABLE IF NOT EXISTS transport_notification_logs (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  route_id BIGINT NOT NULL,
  student_id BIGINT NULL,
  notification_type VARCHAR(30) NOT NULL,
  title VARCHAR(255) NOT NULL,
  body TEXT NOT NULL,
  message_batch_id BIGINT NULL,
  recipients_json JSONB NULL,
  sent_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  sent_by_user_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_tn_route FOREIGN KEY (route_id) REFERENCES routes(id) ON DELETE CASCADE,
  KEY idx_tn_tenant_route (tenant_id, route_id)
);

-- Seed default transport fee item per tenant (flows into existing fee structure)
INSERT INTO fee_items (tenant_id, name, code, recurrence, is_proratable, is_optional, description)
SELECT id, 'Transport', 'TRANSPORT', 1, 0, 1, 'Transport fee per term - flows into existing fee invoicing' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM fee_items WHERE tenant_id=tenants.id AND code='TRANSPORT')
ON DUPLICATE KEY UPDATE name=VALUES(name);
