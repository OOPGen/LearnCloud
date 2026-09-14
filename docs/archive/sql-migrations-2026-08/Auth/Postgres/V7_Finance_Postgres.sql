-- Translated from MySQL V7_Finance.sql to PostgreSQL (Supabase Compatible)
-- Original: /home/user/src/LearnCloud.Finance/Migrations/V7_Finance.sql
-- Translated: /home/user/src/LearnCloud.Auth/Migrations/Postgres/V7_Finance_Postgres.sql
-- Date: 2026-08-09
-- Note: Manual review needed for ENUM->VARCHAR, generated columns, partitioning

-- Finance Full Module V7 - Expense categories, approval thresholds, expenses, suppliers, budgets, cash book, bank accounts, petty cash, period locking

-- Expense categories
CREATE TABLE IF NOT EXISTS expense_categories (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  code VARCHAR(20) NOT NULL,
  type VARCHAR(30) NOT NULL DEFAULT 'operational',
  parent_category_id BIGINT NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  description VARCHAR(255) NULL,
  gl_code VARCHAR(50) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_exp_cat_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  CONSTRAINT fk_exp_cat_parent FOREIGN KEY (parent_category_id) REFERENCES expense_categories(id) ON DELETE SET NULL,
  UNIQUE KEY uq_exp_cat_tenant_code (tenant_id, code),
  KEY idx_exp_cat_tenant_type (tenant_id, type)
);

-- Approval thresholds configurable
CREATE TABLE IF NOT EXISTS approval_thresholds (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  min_amount DECIMAL(18,2) NOT NULL,
  max_amount DECIMAL(18,2) NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  required_approver_role VARCHAR(50) NOT NULL,
  required_approvals INT NOT NULL DEFAULT 1,
  auto_approve BOOLEAN NOT NULL DEFAULT 0,
  approval_order INT NOT NULL DEFAULT 1,
  description VARCHAR(255) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_approval_tenant_order (tenant_id, approval_order),
  KEY idx_approval_tenant_amount (tenant_id, min_amount, max_amount)
);

-- Suppliers
CREATE TABLE IF NOT EXISTS suppliers (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(255) NOT NULL,
  code VARCHAR(20) NOT NULL,
  contact_person VARCHAR(100) NULL,
  email VARCHAR(255) NULL,
  phone VARCHAR(50) NULL,
  address VARCHAR(500) NULL,
  tax_id VARCHAR(50) NULL,
  bank_account_number VARCHAR(50) NULL,
  bank_name VARCHAR(100) NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  total_purchases DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_supplier_tenant_code (tenant_id, code),
  KEY idx_supplier_tenant_name (tenant_id, name)
);

-- Expenses
CREATE TABLE IF NOT EXISTS expenses (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  expense_number VARCHAR(20) NOT NULL,
  category_id BIGINT NOT NULL,
  supplier_id BIGINT NULL,
  purchase_record_id BIGINT NULL,
  description VARCHAR(500) NOT NULL,
  amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  expense_date DATE NOT NULL,
  academic_year_id BIGINT NULL,
  term_id BIGINT NULL,
  budget_id BIGINT NULL,
  status VARCHAR(30) NOT NULL DEFAULT 'draft',
  payment_method VARCHAR(30) NULL,
  bank_account_id BIGINT NULL,
  petty_cash_disbursement_id BIGINT NULL,
  supporting_document_url VARCHAR(500) NULL,
  notes TEXT NULL,
  created_by_user_id BIGINT NOT NULL,
  approved_by_user_id BIGINT NULL,
  approved_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  deleted_at TIMESTAMPTZ NULL,
  deleted_by BIGINT NULL,
  CONSTRAINT fk_exp_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  CONSTRAINT fk_exp_category FOREIGN KEY (category_id) REFERENCES expense_categories(id) ON DELETE RESTRICT,
  CONSTRAINT fk_exp_supplier FOREIGN KEY (supplier_id) REFERENCES suppliers(id) ON DELETE SET NULL,
  UNIQUE KEY uq_exp_tenant_number (tenant_id, expense_number),
  KEY idx_exp_tenant_category_date (tenant_id, category_id, expense_date),
  KEY idx_exp_tenant_status (tenant_id, status),
  KEY idx_exp_tenant_year_term (tenant_id, academic_year_id, term_id)
);

-- Expense documents
CREATE TABLE IF NOT EXISTS expense_documents (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  expense_id BIGINT NOT NULL,
  file_name VARCHAR(255) NOT NULL,
  file_url VARCHAR(500) NOT NULL,
  file_size BIGINT NOT NULL,
  content_type VARCHAR(100) NOT NULL DEFAULT 'application/pdf',
  description VARCHAR(255) NULL,
  uploaded_by_user_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_exp_doc_exp FOREIGN KEY (expense_id) REFERENCES expenses(id) ON DELETE CASCADE,
  KEY idx_exp_doc_tenant_expense (tenant_id, expense_id)
);

-- Approval requests
CREATE TABLE IF NOT EXISTS approval_requests (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  expense_id BIGINT NOT NULL,
  threshold_id BIGINT NOT NULL,
  approver_user_id BIGINT NOT NULL,
  approver_role VARCHAR(50) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  comment VARCHAR(500) NULL,
  decided_at TIMESTAMPTZ NULL,
  approval_order INT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_approval_exp FOREIGN KEY (expense_id) REFERENCES expenses(id) ON DELETE CASCADE,
  CONSTRAINT fk_approval_threshold FOREIGN KEY (threshold_id) REFERENCES approval_thresholds(id) ON DELETE RESTRICT,
  KEY idx_approval_tenant_expense (tenant_id, expense_id),
  KEY idx_approval_tenant_status (tenant_id, status)
);

-- Budgets per category per term
CREATE TABLE IF NOT EXISTS budgets (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  category_id BIGINT NOT NULL,
  budgeted_amount DECIMAL(18,2) NOT NULL,
  actual_amount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  variance_amount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  variance_percentage DECIMAL(5,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  notes TEXT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_budget_category FOREIGN KEY (category_id) REFERENCES expense_categories(id) ON DELETE RESTRICT,
  UNIQUE KEY uq_budget_tenant_year_term_category (tenant_id, academic_year_id, term_id, category_id),
  KEY idx_budget_tenant_year_term (tenant_id, academic_year_id, term_id)
);

-- Bank accounts
CREATE TABLE IF NOT EXISTS bank_accounts (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  account_number VARCHAR(50) NOT NULL,
  bank_name VARCHAR(100) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  opening_balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  current_balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  account_type VARCHAR(20) NOT NULL DEFAULT 'bank',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_bank_acc_tenant_number (tenant_id, account_number),
  KEY idx_bank_acc_tenant_active (tenant_id, is_active)
);

-- Cash book entries
CREATE TABLE IF NOT EXISTS cash_book_entries (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  bank_account_id BIGINT NOT NULL,
  entry_date DATE NOT NULL,
  description VARCHAR(500) NOT NULL,
  reference VARCHAR(100) NOT NULL,
  debit DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  credit DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  entry_type VARCHAR(20) NOT NULL DEFAULT 'general',
  related_expense_id BIGINT NULL,
  related_fee_payment_id BIGINT NULL,
  related_platform_invoice_id BIGINT NULL,
  transfer_to_account_id BIGINT NULL,
  transfer_id VARCHAR(50) NULL,
  is_reconciled BOOLEAN NOT NULL DEFAULT 0,
  reconciled_at TIMESTAMPTZ NULL,
  reconciled_by_user_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_cash_bank FOREIGN KEY (bank_account_id) REFERENCES bank_accounts(id) ON DELETE CASCADE,
  KEY idx_cash_tenant_account_date (tenant_id, bank_account_id, entry_date),
  KEY idx_cash_tenant_reconciled (tenant_id, is_reconciled)
);

-- Bank statements and lines for reconciliation
CREATE TABLE IF NOT EXISTS bank_statements (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  bank_account_id BIGINT NOT NULL,
  file_name VARCHAR(255) NOT NULL,
  file_url VARCHAR(500) NOT NULL,
  statement_date DATE NOT NULL,
  from_date DATE NOT NULL,
  to_date DATE NOT NULL,
  opening_balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  closing_balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_stmt_bank FOREIGN KEY (bank_account_id) REFERENCES bank_accounts(id) ON DELETE CASCADE,
  KEY idx_stmt_tenant_account (tenant_id, bank_account_id)
);

CREATE TABLE IF NOT EXISTS bank_statement_lines (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  bank_statement_id BIGINT NOT NULL,
  bank_account_id BIGINT NOT NULL,
  transaction_date DATE NOT NULL,
  description VARCHAR(500) NOT NULL,
  amount DECIMAL(18,2) NOT NULL,
  debit DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  credit DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  is_reconciled BOOLEAN NOT NULL DEFAULT 0,
  matched_cash_book_entry_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_stmt_line_stmt FOREIGN KEY (bank_statement_id) REFERENCES bank_statements(id) ON DELETE CASCADE,
  KEY idx_stmt_line_tenant_reconciled (tenant_id, is_reconciled)
);

-- Petty cash
CREATE TABLE IF NOT EXISTS petty_cash_accounts (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(100) NOT NULL,
  float_amount DECIMAL(18,2) NOT NULL,
  current_balance DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  custodian_user_id BIGINT NOT NULL,
  is_active BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  KEY idx_petty_tenant_active (tenant_id, is_active)
);

CREATE TABLE IF NOT EXISTS petty_cash_disbursements (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  petty_cash_account_id BIGINT NOT NULL,
  amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  description VARCHAR(500) NOT NULL,
  recipient VARCHAR(255) NOT NULL,
  disbursement_date DATE NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'disbursed',
  receipt_url VARCHAR(500) NULL,
  related_expense_id BIGINT NULL,
  disbursed_by_user_id BIGINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_petty_disb_acc FOREIGN KEY (petty_cash_account_id) REFERENCES petty_cash_accounts(id) ON DELETE CASCADE,
  KEY idx_petty_disb_tenant_account (tenant_id, petty_cash_account_id, disbursement_date)
);

CREATE TABLE IF NOT EXISTS petty_cash_reconciliations (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  petty_cash_account_id BIGINT NOT NULL,
  reconciliation_date DATE NOT NULL,
  float_amount DECIMAL(18,2) NOT NULL,
  disbursed_total DECIMAL(18,2) NOT NULL,
  cash_counted DECIMAL(18,2) NOT NULL,
  variance DECIMAL(18,2) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  notes TEXT NULL,
  reconciled_by_user_id BIGINT NOT NULL,
  approved_by_user_id BIGINT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  CONSTRAINT fk_petty_rec_acc FOREIGN KEY (petty_cash_account_id) REFERENCES petty_cash_accounts(id) ON DELETE CASCADE,
  KEY idx_petty_rec_tenant_account (tenant_id, petty_cash_account_id)
);

-- Period locking
CREATE TABLE IF NOT EXISTS period_locks (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  academic_year_id BIGINT NOT NULL,
  term_id BIGINT NOT NULL,
  is_locked BOOLEAN NOT NULL DEFAULT 0,
  locked_at TIMESTAMPTZ NULL,
  locked_by_user_id BIGINT NULL,
  lock_reason VARCHAR(500) NULL,
  is_unlocked BOOLEAN NOT NULL DEFAULT 0,
  unlocked_at TIMESTAMPTZ NULL,
  unlocked_by_user_id BIGINT NULL,
  unlock_reason VARCHAR(1000) NULL,
  unlock_approver_role VARCHAR(50) NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() ,
  updated_by BIGINT NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE KEY uq_period_lock_tenant_year_term (tenant_id, academic_year_id, term_id),
  KEY idx_period_lock_tenant_locked (tenant_id, is_locked)
);

-- Seed default expense categories
INSERT INTO expense_categories (tenant_id, name, code, type, is_active, description, gl_code) 
SELECT id, 'Teaching Materials', 'TEACH_MAT', 'academic', 1, 'Books, lab materials, stationery for teaching', '5001' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM expense_categories WHERE tenant_id=tenants.id AND code='TEACH_MAT')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO expense_categories (tenant_id, name, code, type, is_active) 
SELECT id, 'Utilities', 'UTIL', 'operational', 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM expense_categories WHERE tenant_id=tenants.id AND code='UTIL')
ON DUPLICATE KEY UPDATE name=VALUES(name);

INSERT INTO expense_categories (tenant_id, name, code, type, is_active) 
SELECT id, 'Maintenance', 'MAINT', 'operational', 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM expense_categories WHERE tenant_id=tenants.id AND code='MAINT')
ON DUPLICATE KEY UPDATE name=VALUES(name);

-- Seed default approval thresholds: <100 auto-approve, 100-500 bursar+head, >500 board
INSERT INTO approval_thresholds (tenant_id, min_amount, max_amount, currency, required_approver_role, required_approvals, auto_approve, approval_order, description)
SELECT id, 0.00, 100.00, 'USD', 'BURSAR', 1, 1, 1, 'Auto-approve under $100' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM approval_thresholds WHERE tenant_id=tenants.id AND min_amount=0.00 AND max_amount=100.00)
ON DUPLICATE KEY UPDATE required_approver_role=VALUES(required_approver_role);

INSERT INTO approval_thresholds (tenant_id, min_amount, max_amount, currency, required_approver_role, required_approvals, auto_approve, approval_order, description)
SELECT id, 100.00, 500.00, 'USD', 'HEAD_TEACHER', 1, 0, 2, 'Head approval $100-$500' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM approval_thresholds WHERE tenant_id=tenants.id AND min_amount=100.00 AND max_amount=500.00)
ON DUPLICATE KEY UPDATE required_approver_role=VALUES(required_approver_role);

INSERT INTO approval_thresholds (tenant_id, min_amount, max_amount, currency, required_approver_role, required_approvals, auto_approve, approval_order, description)
SELECT id, 500.00, NULL, 'USD', 'DIRECTOR', 1, 0, 3, 'Director/Board approval >$500' FROM tenants WHERE NOT EXISTS (SELECT 1 FROM approval_thresholds WHERE tenant_id=tenants.id AND min_amount=500.00 AND max_amount IS NULL)
ON DUPLICATE KEY UPDATE required_approver_role=VALUES(required_approver_role);
