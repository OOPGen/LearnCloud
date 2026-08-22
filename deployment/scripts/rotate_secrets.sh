#!/bin/bash
# SECURITY FIX: Rotate secrets for production - WAF, gitleaks, secrets rotation
# Run this on production server to generate new strong secrets

set -e

echo "=== LearnCloud Secrets Rotation Script ==="
echo "Generating strong secrets via openssl..."

# Generate secrets
JWT_SECRET=$(openssl rand -base64 48 | tr -d '\n')
MYSQL_ROOT_PASSWORD=$(openssl rand -base64 24 | tr -d '\n')
MYSQL_PASSWORD=$(openssl rand -base64 24 | tr -d '\n')
BACKUP_ENCRYPTION_PASSPHRASE=$(openssl rand -base64 32 | tr -d '\n')
PAYNOW_INTEGRATION_ID=${PAYNOW_INTEGRATION_ID:-$(openssl rand -hex 16)}
PAYNOW_INTEGRATION_KEY=$(openssl rand -base64 48 | tr -d '\n')
SMS_API_KEY=${SMS_API_KEY:-$(openssl rand -base64 32)}

ENV_FILE="/opt/learncloud/.env.production"

echo "Backing up existing .env.production if exists..."
if [ -f "$ENV_FILE" ]; then
  cp "$ENV_FILE" "${ENV_FILE}.backup.$(date +%Y%m%d%H%M%S)"
  echo "Backup created: ${ENV_FILE}.backup.*"
fi

cat > "$ENV_FILE" << EOF
# LearnCloud Production Secrets - Generated $(date -u +%Y-%m-%dT%H:%M:%SZ)
# chmod 600 this file, owned by root, never commit to git

# MySQL
MYSQL_DATABASE=learncloud
MYSQL_USER=learncloud
MYSQL_ROOT_PASSWORD=$MYSQL_ROOT_PASSWORD
MYSQL_PASSWORD=$MYSQL_PASSWORD

# JWT - 48 bytes base64 = 64 chars, 512-bit, min 32 required
JWT_SECRET=$JWT_SECRET
JWT_ISSUER=LearnCloud
JWT_AUDIENCE=LearnCloud
JWT_ACCESS_MINUTES=15
JWT_REFRESH_DAYS=14

# Backup encryption
BACKUP_ENCRYPTION_PASSPHRASE=$BACKUP_ENCRYPTION_PASSPHRASE
BACKUP_RETENTION_DAYS=30

# PayNow - Get real keys from https://www.paynow.co.zw dashboard
PAYNOW_INTEGRATION_ID=$PAYNOW_INTEGRATION_ID
PAYNOW_INTEGRATION_KEY=$PAYNOW_INTEGRATION_KEY
PAYNOW_MERCHANT_ID=learncloud-prod
PAYNOW_RESULT_URL=https://learncloud.co.zw/api/webhooks/payments/paynow
PAYNOW_RETURN_URL=https://learncloud.co.zw/parent/payments/return

# SMS/Email - get from provider dashboard
SMS_API_KEY=$SMS_API_KEY
SMS_PROVIDER=EcoCashSms
SMS_SENDER_ID=LearnCloud
SMTP_HOST=smtp.sendgrid.net
SMTP_PORT=587
SMTP_USER=apikey
SMTP_PASS=your-sendgrid-api-key

# Security - rotate every 90 days
# Last rotated: $(date -u +%Y-%m-%dT%H:%M:%SZ)
# Next rotation due: $(date -u -d "+90 days" +%Y-%m-%d)
EOF

chmod 600 "$ENV_FILE"
chown root:root "$ENV_FILE"

echo ""
echo "✅ Secrets generated and written to $ENV_FILE"
echo "   chmod 600, chown root:root done"
echo ""
echo "IMPORTANT:"
echo "1. Update PayNow IntegrationId/Key from https://www.paynow.co.zw dashboard - current generated is placeholder"
echo "2. Update SMS_API_KEY from EcoCash SMS dashboard"
echo "3. Update SMTP_PASS from SendGrid"
echo "4. Restart services: docker-compose -f /opt/learncloud/docker-compose.yml up -d --force-recreate api"
echo "5. Old refresh tokens will be invalidated after JWT secret rotation - users will need to re-login (expected)"
echo "6. Test: curl https://learncloud.co.zw/health"
echo ""
echo "Next rotation due: 90 days"
echo "To rotate, run this script again"
