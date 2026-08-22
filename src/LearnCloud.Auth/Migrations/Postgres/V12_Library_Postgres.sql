-- Translated from MySQL V12_Library.sql to PostgreSQL (Supabase Compatible)
-- Original: /home/user/src/LearnCloud.Library/Migrations/V12_Library.sql
-- Translated: /home/user/src/LearnCloud.Auth/Migrations/Postgres/V12_Library_Postgres.sql
-- Date: 2026-08-09
-- Note: Manual review needed for ENUM->VARCHAR, generated columns, partitioning

-- Library Module V12 - Catalogue, copies/barcode, membership, issue/return, renewals, reservations, fines posting to fee account, lost/damaged, stock take, reports

CREATE TABLE IF NOT EXISTS library_categories (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  code VARCHAR(20) NOT NULL,
  description VARCHAR(255) NULL,
  parent_category_id BIGINT NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_lib_cat_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_lib_cat_tenant_code (tenant_id, code)
);

CREATE TABLE IF NOT EXISTS books (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  title VARCHAR(255) NOT NULL,
  author VARCHAR(255) NOT NULL,
  isbn VARCHAR(20) NULL,
  category_id BIGINT NOT NULL,
  publisher VARCHAR(100) NULL,
  publication_year INT NULL,
  edition VARCHAR(20) NULL,
  shelf_location VARCHAR(50) NOT NULL,
  total_copies INT NOT NULL DEFAULT 0,
  available_copies INT NOT NULL DEFAULT 0,
  replacement_price DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  description TEXT NULL,
  cover_image_url VARCHAR(500) NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_books_category FOREIGN KEY (category_id) REFERENCES library_categories(id) ON DELETE RESTRICT,
  KEY idx_books_tenant_category (tenant_id, category_id),
  KEY idx_books_tenant_title (tenant_id, title),
  FULLTEXT KEY ft_books_title_author (title, author)
);

CREATE TABLE IF NOT EXISTS book_copies (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  book_id BIGINT NOT NULL,
  accession_number VARCHAR(50) NOT NULL,
  barcode VARCHAR(50) NOT NULL,
  shelf_location VARCHAR(50) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'available',
  "condition" VARCHAR(20) NOT NULL DEFAULT 'good',
  last_issued_at TIMESTAMPTZ NULL,
  last_returned_at TIMESTAMPTZ NULL,
  current_loan_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_copy_book FOREIGN KEY (book_id) REFERENCES books(id) ON DELETE CASCADE,
  UNIQUE KEY uq_copy_tenant_accession (tenant_id, accession_number),
  UNIQUE KEY uq_copy_tenant_barcode (tenant_id, barcode),
  KEY idx_copy_tenant_book_status (tenant_id, book_id, status),
  KEY idx_copy_tenant_status (tenant_id, status)
);

CREATE TABLE IF NOT EXISTS membership_configs (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  membership_type VARCHAR(20) NOT NULL,
  max_books INT NOT NULL DEFAULT 3,
  loan_period_days INT NOT NULL DEFAULT 14,
  max_renewals INT NOT NULL DEFAULT 1,
  fine_per_day DECIMAL(18,2) NOT NULL DEFAULT 1.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  max_fine DECIMAL(18,2) NOT NULL DEFAULT 50.00,
  allow_reservations BOOLEAN NOT NULL DEFAULT 1,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_membership_config_tenant_type (tenant_id, membership_type)
);

CREATE TABLE IF NOT EXISTS library_members (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  membership_number VARCHAR(20) NOT NULL,
  member_type VARCHAR(20) NOT NULL,
  student_id BIGINT NULL,
  staff_id BIGINT NULL,
  user_id BIGINT NULL,
  full_name VARCHAR(255) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  currently_borrowed INT NOT NULL DEFAULT 0,
  total_borrowed INT NOT NULL DEFAULT 0,
  outstanding_fines DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  membership_expiry_date DATE NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_lib_member_tenant_number (tenant_id, membership_number),
  KEY idx_lib_member_tenant_student (tenant_id, student_id),
  KEY idx_lib_member_tenant_staff (tenant_id, staff_id)
);

CREATE TABLE IF NOT EXISTS loans (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  book_copy_id BIGINT NOT NULL,
  book_id BIGINT NOT NULL,
  member_id BIGINT NOT NULL,
  student_id BIGINT NULL,
  staff_id BIGINT NULL,
  issue_date TIMESTAMPTZ NOT NULL,
  due_date TIMESTAMPTZ NOT NULL,
  return_date TIMESTAMPTZ NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'issued',
  issued_by_user_id BIGINT NOT NULL,
  returned_by_user_id BIGINT NULL,
  renewal_count INT NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_loan_copy FOREIGN KEY (book_copy_id) REFERENCES book_copies(id) ON DELETE RESTRICT,
  KEY idx_loan_tenant_member (tenant_id, member_id, status),
  KEY idx_loan_tenant_book (tenant_id, book_id),
  KEY idx_loan_tenant_due (tenant_id, due_date, status),
  KEY idx_loan_tenant_student (tenant_id, student_id)
);

CREATE TABLE IF NOT EXISTS loan_renewals (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  loan_id BIGINT NOT NULL,
  renewal_date TIMESTAMPTZ NOT NULL,
  previous_due_date TIMESTAMPTZ NOT NULL,
  new_due_date TIMESTAMPTZ NOT NULL,
  renewal_number INT NOT NULL,
  approved_by_user_id BIGINT NOT NULL,
  reason VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_renewal_loan FOREIGN KEY (loan_id) REFERENCES loans(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS reservations (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  book_id BIGINT NOT NULL,
  member_id BIGINT NOT NULL,
  student_id BIGINT NULL,
  reservation_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  expiry_date TIMESTAMPTZ NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  queue_position INT NOT NULL,
  fulfilled_at TIMESTAMPTZ NULL,
  fulfilled_loan_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_res_tenant_book_status (tenant_id, book_id, status, queue_position),
  KEY idx_res_tenant_member (tenant_id, member_id)
);

CREATE TABLE IF NOT EXISTS fines (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  loan_id BIGINT NOT NULL,
  member_id BIGINT NOT NULL,
  student_id BIGINT NULL,
  fine_type VARCHAR(20) NOT NULL DEFAULT 'overdue',
  amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  days_overdue INT NOT NULL DEFAULT 0,
  fine_per_day DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  posted_to_fee_account BOOLEAN NOT NULL DEFAULT 0,
  fee_invoice_id BIGINT NULL,
  fee_invoice_item_id BIGINT NULL,
  paid_at TIMESTAMPTZ NULL,
  waived_reason VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_fine_loan FOREIGN KEY (loan_id) REFERENCES loans(id) ON DELETE CASCADE,
  KEY idx_fine_tenant_member_status (tenant_id, member_id, status),
  KEY idx_fine_tenant_student (tenant_id, student_id)
);

CREATE TABLE IF NOT EXISTS lost_damaged_records (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  book_copy_id BIGINT NOT NULL,
  loan_id BIGINT NOT NULL,
  member_id BIGINT NOT NULL,
  type VARCHAR(20) NOT NULL,
  "condition" VARCHAR(50) NOT NULL,
  replacement_charge DECIMAL(18,2) NOT NULL,
  fine_amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  fee_invoice_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_lost_copy FOREIGN KEY (book_copy_id) REFERENCES book_copies(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS stock_takes (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  completed_at TIMESTAMPTZ NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'in_progress',
  created_by_user_id BIGINT NOT NULL,
  total_expected INT NOT NULL DEFAULT 0,
  total_counted INT NOT NULL DEFAULT 0,
  discrepancies INT NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_stock_take_tenant_status (tenant_id, status)
);

CREATE TABLE IF NOT EXISTS stock_take_items (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  stock_take_id BIGINT NOT NULL,
  book_copy_id BIGINT NOT NULL,
  expected_status VARCHAR(20) NOT NULL,
  counted_status VARCHAR(20) NOT NULL,
  discrepancy_type VARCHAR(20) NOT NULL DEFAULT 'none',
  notes VARCHAR(255) NULL,
  counted_by_user_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_stock_item_take FOREIGN KEY (stock_take_id) REFERENCES stock_takes(id) ON DELETE CASCADE,
  CONSTRAINT fk_stock_item_copy FOREIGN KEY (book_copy_id) REFERENCES book_copies(id) ON DELETE RESTRICT,
  KEY idx_stock_item_tenant_take (tenant_id, stock_take_id)
);

-- Seed default membership configs per tenant
INSERT INTO membership_configs (tenant_id, membership_type, max_books, loan_period_days, max_renewals, fine_per_day, currency, max_fine, allow_reservations)
SELECT id, 'student', 3, 14, 1, 1.00, 'USD', 50.00, 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM membership_configs WHERE tenant_id=tenants.id AND membership_type='student')
ON DUPLICATE KEY UPDATE max_books=VALUES(max_books);

INSERT INTO membership_configs (tenant_id, membership_type, max_books, loan_period_days, max_renewals, fine_per_day, currency, max_fine, allow_reservations)
SELECT id, 'teacher', 10, 30, 2, 0.50, 'USD', 50.00, 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM membership_configs WHERE tenant_id=tenants.id AND membership_type='teacher')
ON DUPLICATE KEY UPDATE max_books=VALUES(max_books);

INSERT INTO membership_configs (tenant_id, membership_type, max_books, loan_period_days, max_renewals, fine_per_day, currency, max_fine, allow_reservations)
SELECT id, 'staff', 5, 21, 1, 1.00, 'USD', 50.00, 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM membership_configs WHERE tenant_id=tenants.id AND membership_type='staff')
ON DUPLICATE KEY UPDATE max_books=VALUES(max_books);

-- Seed default library categories
INSERT INTO library_categories (tenant_id, name, code, is_active) 
SELECT id, 'Fiction', 'FIC', 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM library_categories WHERE tenant_id=tenants.id AND code='FIC')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO library_categories (tenant_id, name, code, is_active) 
SELECT id, 'Non-Fiction', 'NONFIC', 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM library_categories WHERE tenant_id=tenants.id AND code='NONFIC')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO library_categories (tenant_id, name, code, is_active) 
SELECT id, 'Textbooks', 'TEXT', 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM library_categories WHERE tenant_id=tenants.id AND code='TEXT')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO library_categories (tenant_id, name, code, is_active) 
SELECT id, 'Reference', 'REF', 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM library_categories WHERE tenant_id=tenants.id AND code='REF')
ON DUPLICATE KEY UPDATE name=VALUES(name);
