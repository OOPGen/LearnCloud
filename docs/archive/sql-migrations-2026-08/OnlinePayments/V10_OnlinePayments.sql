-- Online Payments Module V10 - IPaymentGateway abstraction, per tenant config, card/bank/mobile money, webhook idempotent signature-verified replay/out-of-order safe, receipt allocation reuse, reconciliation, settlement

CREATE TABLE IF NOT EXISTS payment_gateway_settings (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  gateway_name VARCHAR(50) NOT NULL DEFAULT 'PayNow',
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  is_default TINYINT(1) NOT NULL DEFAULT 1,
  config_json JSON NULL,
  supported_methods_json JSON NOT NULL DEFAULT '["card","bank_transfer","mobile_money","ecocash","onemoney"]',
  fee_percentage DECIMAL(5,2) NOT NULL DEFAULT 2.50,
  fee_fixed DECIMAL(18,2) NOT NULL DEFAULT 0.10,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  enable_card TINYINT(1) NOT NULL DEFAULT 1,
  enable_bank_transfer TINYINT(1) NOT NULL DEFAULT 1,
  enable_mobile_money TINYINT(1) NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_gateway_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_gateway_tenant_name_default (tenant_id, gateway_name, is_default),
  KEY idx_gateway_tenant_active (tenant_id, is_active)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS online_payment_initiations (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  student_id BIGINT UNSIGNED NOT NULL,
  guardian_id BIGINT UNSIGNED NOT NULL,
  invoice_id BIGINT UNSIGNED NULL,
  requested_amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  method VARCHAR(20) NOT NULL DEFAULT 'card',
  status INT NOT NULL DEFAULT 1 COMMENT '1=Initiated,2=Pending,3=Processing,4=Succeeded,5=Failed,6=Cancelled,7=Expired',
  idempotency_key VARCHAR(100) NOT NULL,
  client_reference VARCHAR(100) NOT NULL,
  gateway_reference VARCHAR(100) NULL,
  payment_url TEXT NULL,
  failure_reason VARCHAR(500) NULL,
  expires_at DATETIME NOT NULL,
  created_by_user_id BIGINT UNSIGNED NOT NULL,
  payment_id BIGINT UNSIGNED NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_online_init_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_online_init_client_ref (tenant_id, client_reference),
  UNIQUE KEY uq_online_init_idem (tenant_id, idempotency_key),
  KEY idx_online_init_tenant_student (tenant_id, student_id),
  KEY idx_online_init_status (tenant_id, status)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS gateway_transactions (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  gateway_name VARCHAR(50) NOT NULL DEFAULT 'PayNow',
  gateway_transaction_id VARCHAR(100) NULL,
  provider_reference VARCHAR(100) NULL,
  payload_json TEXT NOT NULL,
  signature VARCHAR(500) NULL,
  is_signature_verified TINYINT(1) NOT NULL DEFAULT 0,
  status VARCHAR(20) NOT NULL DEFAULT 'received',
  amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  method VARCHAR(20) NULL,
  client_reference VARCHAR(100) NULL,
  matched_initiation_id BIGINT UNSIGNED NULL,
  matched_payment_id BIGINT UNSIGNED NULL,
  received_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_replay TINYINT(1) NOT NULL DEFAULT 0,
  is_out_of_order TINYINT(1) NOT NULL DEFAULT 0,
  failure_reason VARCHAR(500) NULL,
  retry_count INT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_gateway_tx_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
  UNIQUE KEY uq_gateway_tx_id (tenant_id, gateway_transaction_id),
  KEY idx_gateway_tx_client_ref (tenant_id, client_reference),
  KEY idx_gateway_tx_status (tenant_id, status),
  KEY idx_gateway_tx_received (tenant_id, received_at)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS gateway_settlements (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  settlement_id VARCHAR(100) NOT NULL,
  settlement_date DATE NOT NULL,
  gross_amount DECIMAL(18,2) NOT NULL,
  fee_amount DECIMAL(18,2) NOT NULL,
  net_amount DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  status VARCHAR(20) NOT NULL DEFAULT 'pending',
  raw_data_json JSON NULL,
  reconciled_by_user_id BIGINT UNSIGNED NULL,
  reconciled_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  UNIQUE KEY uq_settlement_tenant_id (tenant_id, settlement_id),
  KEY idx_settlement_tenant_date (tenant_id, settlement_date)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS settlement_transactions (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BIGINT UNSIGNED NOT NULL,
  settlement_id BIGINT UNSIGNED NOT NULL,
  gateway_transaction_id BIGINT UNSIGNED NULL,
  payment_id BIGINT UNSIGNED NULL,
  amount DECIMAL(18,2) NOT NULL,
  fee DECIMAL(18,2) NOT NULL,
  net DECIMAL(18,2) NOT NULL,
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  is_matched TINYINT(1) NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_settle_tx_settlement FOREIGN KEY (settlement_id) REFERENCES gateway_settlements(id) ON DELETE CASCADE,
  KEY idx_settle_tx_tenant_settlement (tenant_id, settlement_id)
) ENGINE=InnoDB;

-- Seed default gateway settings for existing tenants - PayNow supports card, bank transfer, mobile money
INSERT INTO payment_gateway_settings (tenant_id, gateway_name, is_active, is_default, supported_methods_json, fee_percentage, fee_fixed, currency, enable_card, enable_bank_transfer, enable_mobile_money)
SELECT id, 'PayNow', 1, 1, '["card","bank_transfer","mobile_money","ecocash","onemoney"]', 2.50, 0.10, 'USD', 1, 1, 1 FROM tenants WHERE NOT EXISTS (SELECT 1 FROM payment_gateway_settings WHERE tenant_id=tenants.id AND gateway_name='PayNow')
ON DUPLICATE KEY UPDATE gateway_name=VALUES(gateway_name);
