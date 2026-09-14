-- LearnCloud Auth Migration V1 - MySQL 8.0 utf8mb4_unicode_ci
-- Includes: tenants, users, roles, permissions, role_permissions, user_roles, refresh_tokens, user_tokens, subscription_plans, tenant_subscriptions
-- HQ Bulawayo, all tenant-owned indexes lead with tenant_id, soft delete columns, audit columns

CREATE DATABASE IF NOT EXISTS learncloud CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE learncloud;

-- tenants (not tenant-owned)
CREATE TABLE IF NOT EXISTS tenants (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  name VARCHAR(255) NOT NULL,
  slug VARCHAR(100) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'trial',
  city VARCHAR(100) NOT NULL DEFAULT 'Bulawayo',
  country CHAR(2) NOT NULL DEFAULT 'ZW',
  contact_email VARCHAR(255) NOT NULL,
  contact_phone VARCHAR(50) NULL,
  primary_color CHAR(7) NOT NULL DEFAULT '#0F153A',
  learner_count_band VARCHAR(20) NOT NULL,
  logo_url VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  UNIQUE KEY uq_tenants_slug (slug),
  KEY idx_tenants_status (status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- subscription_plans (global, tenant_id NULL)
CREATE TABLE IF NOT EXISTS subscription_plans (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NULL,
  code VARCHAR(50) NOT NULL,
  name VARCHAR(100) NOT NULL,
  max_learners INT UNSIGNED NOT NULL,
  price_monthly DECIMAL(18,2) NOT NULL,
  price_annual DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  features_json JSON NULL,
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  UNIQUE KEY uq_plans_code (code)
) ENGINE=InnoDB;

-- tenant_subscriptions
CREATE TABLE IF NOT EXISTS tenant_subscriptions (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  plan_id BIGINT UNSIGNED NOT NULL,
  billing_cycle VARCHAR(20) NOT NULL DEFAULT 'monthly',
  status VARCHAR(20) NOT NULL DEFAULT 'trialing',
  trial_ends_at DATETIME NULL,
  current_period_start DATETIME NOT NULL,
  current_period_end DATETIME NOT NULL,
  metered_active_students INT UNSIGNED NOT NULL DEFAULT 0,
  over_limit_flag TINYINT(1) NOT NULL DEFAULT 0,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_tenant_subs_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  CONSTRAINT fk_tenant_subs_plan FOREIGN KEY (plan_id) REFERENCES subscription_plans(id) ON DELETE RESTRICT,
  KEY idx_tenant_subs_tenant_status (tenant_id, status),
  KEY idx_tenant_subs_tenant_period (tenant_id, current_period_end)
) ENGINE=InnoDB;

-- users (tenant_id NULLABLE for platform)
CREATE TABLE IF NOT EXISTS users (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NULL,
  email VARCHAR(255) NOT NULL,
  phone VARCHAR(50) NULL,
  display_name VARCHAR(255) NOT NULL,
  password_hash VARCHAR(500) NOT NULL,
  email_verified TINYINT(1) NOT NULL DEFAULT 0,
  email_verified_at DATETIME NULL,
  token_version INT NOT NULL DEFAULT 1,
  security_stamp VARCHAR(100) NOT NULL,
  failed_login_count INT NOT NULL DEFAULT 0,
  lockout_end DATETIME NULL,
  last_login_at DATETIME NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'active',
  must_change_password TINYINT(1) NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_users_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_users_tenant_email (tenant_id, email),
  KEY idx_users_email_global (email),
  KEY idx_users_tenant_status (tenant_id, status),
  KEY idx_users_security_stamp (security_stamp)
) ENGINE=InnoDB;

-- roles (tenant_id NULL for system)
CREATE TABLE IF NOT EXISTS roles (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NULL,
  code VARCHAR(50) NOT NULL,
  name VARCHAR(100) NOT NULL,
  is_system TINYINT(1) NOT NULL DEFAULT 1,
  description VARCHAR(255) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  UNIQUE KEY uq_roles_tenant_code (tenant_id, code),
  KEY idx_roles_tenant_code (tenant_id, code)
) ENGINE=InnoDB;

-- permissions (tenant_id NULL global)
CREATE TABLE IF NOT EXISTS permissions (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NULL,
  code VARCHAR(100) NOT NULL,
  name VARCHAR(150) NOT NULL,
  module VARCHAR(50) NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  UNIQUE KEY uq_permissions_code (code)
) ENGINE=InnoDB;

-- role_permissions
CREATE TABLE IF NOT EXISTS role_permissions (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NULL,
  role_id BIGINT UNSIGNED NOT NULL,
  permission_id BIGINT UNSIGNED NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_rp_role FOREIGN KEY (role_id) REFERENCES roles(id) ON DELETE CASCADE,
  CONSTRAINT fk_rp_perm FOREIGN KEY (permission_id) REFERENCES permissions(id) ON DELETE CASCADE,
  UNIQUE KEY uq_rp_tenant_role_perm (tenant_id, role_id, permission_id),
  KEY idx_rp_tenant_role (tenant_id, role_id)
) ENGINE=InnoDB;

-- user_roles
CREATE TABLE IF NOT EXISTS user_roles (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NULL,
  user_id BIGINT UNSIGNED NOT NULL,
  role_id BIGINT UNSIGNED NOT NULL,
  academic_year_id BIGINT UNSIGNED NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_ur_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
  CONSTRAINT fk_ur_role FOREIGN KEY (role_id) REFERENCES roles(id) ON DELETE CASCADE,
  UNIQUE KEY uq_user_roles (tenant_id, user_id, role_id, academic_year_id),
  KEY idx_user_roles_tenant_user (tenant_id, user_id)
) ENGINE=InnoDB;

-- refresh_tokens - rotating, hashed, family
CREATE TABLE IF NOT EXISTS refresh_tokens (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NULL,
  user_id BIGINT UNSIGNED NOT NULL,
  token_hash VARCHAR(128) NOT NULL,
  family_id VARCHAR(100) NOT NULL,
  parent_token_id BIGINT UNSIGNED NULL,
  replaced_by_token_id BIGINT UNSIGNED NULL,
  expires_at DATETIME NOT NULL,
  revoked_at DATETIME NULL,
  revoked_reason VARCHAR(100) NULL,
  created_by_ip VARCHAR(45) NOT NULL,
  device VARCHAR(255) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_refresh_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
  CONSTRAINT fk_refresh_parent FOREIGN KEY (parent_token_id) REFERENCES refresh_tokens(id) ON DELETE SET NULL,
  UNIQUE KEY uq_refresh_token_hash (token_hash),
  KEY idx_refresh_tenant_user_family (tenant_id, user_id, family_id),
  KEY idx_refresh_tenant_user_exp (tenant_id, user_id, expires_at),
  KEY idx_refresh_family (family_id)
) ENGINE=InnoDB;

-- user_tokens - email verification, password reset - single-use hashed
CREATE TABLE IF NOT EXISTS user_tokens (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NULL,
  user_id BIGINT UNSIGNED NOT NULL,
  token_type INT NOT NULL, -- 1=EmailVerification, 2=PasswordReset
  token_hash VARCHAR(128) NOT NULL,
  expires_at DATETIME NOT NULL,
  used_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  CONSTRAINT fk_usertoken_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
  KEY idx_usertoken_hash (token_hash),
  KEY idx_usertoken_user_type_exp (user_id, token_type, expires_at),
  KEY idx_usertoken_expires (expires_at)
) ENGINE=InnoDB;

-- Seed default plans
INSERT INTO subscription_plans (code, name, max_learners, price_monthly, price_annual, currency, is_active) VALUES
('starter','Starter 300',300,49.00,490.00,'USD',1),
('growth','Growth 800',800,99.00,990.00,'USD',1),
('scale','Scale 2000',2000,199.00,1990.00,'USD',1)
ON DUPLICATE KEY UPDATE name=VALUES(name), max_learners=VALUES(max_learners), updated_at=CURRENT_TIMESTAMP;

-- Seed permissions from earlier Roles matrix (short excerpt, full list should be inserted via seed_roles.sql)
-- This migration keeps minimal perms for auth module to compile
INSERT INTO permissions (code, name, module) VALUES
('tenants.read','View tenants','tenancy'),
('tenants.create','Create tenant','tenancy'),
('users.read','View users','users'),
('users.disable','Disable users','users'),
('students.read','View students','students'),
('settings.read','View settings','settings'),
('dashboard.viewPlatform','View Platform dashboard','dashboard')
ON DUPLICATE KEY UPDATE name=VALUES(name);

-- Platform superadmin role
INSERT INTO roles (tenant_id, code, name, is_system, description) VALUES
(NULL,'PLATFORM_SUPERADMIN','Platform Superadmin',1,'LearnCloud staff')
ON DUPLICATE KEY UPDATE name=VALUES(name);

-- For demo, add role_permissions for platform superadmin
-- Handled in seed_roles.sql fully
