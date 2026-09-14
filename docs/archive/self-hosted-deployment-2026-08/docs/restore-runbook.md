# Restore Runbook — Exact Commands to Rebuild Whole System on Fresh Server from Backups
**Written for stressed person at 2am, one operator, no dedicated ops team, simple and recoverable**

**Assumptions:** You have: fresh Ubuntu 22.04 server, IP, domain learncloud.co.zw pointed, access to S3 bucket learncloud-backups-bulawayo, and secrets backup file learncloud-secrets-2026-08-02.gpg + passphrase in password manager, and this repo.

**Goal:** From zero to live in <1 hour.

---

## Step 0: Breathe, Read This Whole Doc Before Typing (2 minutes)

You will: install docker, get secrets, restore .env.production, restore MySQL from latest encrypted backup, bring up docker-compose, verify, switch DNS if needed.

Never delete data. Never panic `rm -rf /`.

---

## Step 1: Prepare Fresh Server (5 minutes)

SSH as root or sudo user:

```bash
# Update
apt update && apt upgrade -y

# Install docker, compose, git, curl, gpg
apt install -y docker.io docker-compose-plugin git curl gnupg awscli

# Enable docker
systemctl enable --now docker

# Create operator user if not exists (replace operator with your username)
# adduser operator
# usermod -aG docker operator
# su - operator

# Create directory structure
sudo mkdir -p /opt/learncloud
sudo chown $USER:$USER /opt/learncloud
mkdir -p /opt/learncloud/backups
cd /opt/learncloud

# Clone repo (or scp from laptop)
git clone https://github.com/yourorg/learncloud.git .
# Or if repo private: git clone git@github.com:yourorg/learncloud.git .
```

---

## Step 2: Restore Secrets (3 minutes) — CRITICAL

You need `/opt/learncloud/.env.production` (real secrets) and backup encryption passphrase.

**Option A: Secrets backup file from S3**

```bash
# Configure AWS CLI with your backup IAM user (keys in password manager)
aws configure # enter AWS_ACCESS_KEY_ID, SECRET, region af-south-1

# List secrets backups
aws s3 ls s3://learncloud-secrets-backup/ --region af-south-1

# Download latest secrets file
aws s3 cp s3://learncloud-secrets-backup/learncloud-secrets-2026-08-02.gpg /tmp/secrets.gpg --region af-south-1

# Decrypt with passphrase from password manager (1Password entry "LearnCloud Secrets GPG")
gpg --decrypt -o /opt/learncloud/.env.production /tmp/secrets.gpg
# Enter passphrase when prompted

chmod 600 /opt/learncloud/.env.production
ls -l /opt/learncloud/.env.production # should be -rw------- 1 operator
```

**Option B: From password manager directly**

```bash
nano /opt/learncloud/.env.production
# Paste contents from 1Password entry "LearnCloud Production .env"
chmod 600 /opt/learncloud/.env.production
```

**Verify .env.production contains:**

```bash
cat /opt/learncloud/.env.production | grep -v PASSWORD | grep -v SECRET | grep -v KEY
# Should show non-secret vars, but not leak secrets in logs
# Check required vars present:
grep -E "MYSQL_ROOT_PASSWORD|MYSQL_PASSWORD|JWT_SECRET|BACKUP_ENCRYPTION_PASSPHRASE" /opt/learncloud/.env.production
# All four must exist, non-empty
```

---

## Step 3: Restore SSL Certs (2 minutes) - If You Have Off-Server Backup

If you backed up certs to S3:

```bash
aws s3 cp s3://learncloud-backups-bulawayo/certs/ /opt/learncloud/certs/ --recursive --region af-south-1 || echo "No certs backup, will use certbot to get new certs"
```

If no certs backup, we will get new certs via certbot after nginx up, that's okay, just needs DNS pointing.

---

## Step 4: Restore MySQL from Latest Encrypted Backup (15 minutes) — Most Critical

```bash
cd /opt/learncloud

# Set env vars from .env.production for backup script
export $(cat /opt/learncloud/.env.production | xargs)

# Find latest backup in S3
aws s3 ls s3://${BACKUP_S3_BUCKET:-learncloud-backups-bulawayo}/mysql/ --recursive --region ${AWS_DEFAULT_REGION:-af-south-1} | sort | tail -n 20

# Download latest backup file (replace date with latest you see)
LATEST_DATE=$(aws s3 ls s3://${BACKUP_S3_BUCKET}/mysql/ --recursive --region af-south-1 | sort | tail -n1 | awk '{print $4}')
echo "Latest: $LATEST_DATE"

mkdir -p /opt/learncloud/backups
aws s3 cp s3://${BACKUP_S3_BUCKET}/${LATEST_DATE} /opt/learncloud/backups/latest.sql.gpg --region af-south-1

# Decrypt
echo $BACKUP_ENCRYPTION_PASSPHRASE | gpg --batch --yes --passphrase-fd 0 --decrypt -o /opt/learncloud/backups/latest.sql /opt/learncloud/backups/latest.sql.gpg

ls -lh /opt/learncloud/backups/latest.sql # should be >10MB, not empty
head -n 20 /opt/learncloud/backups/latest.sql # should show CREATE TABLE

# Now bring up only mysql container first to restore into
docker compose -f deployment/docker-compose.yml up -d mysql

# Wait for mysql healthy
docker inspect --format='{{json .State.Health.Status}}' learncloud-mysql
# Repeat until "healthy" (60s)
watch -n 5 "docker inspect --format='{{json .State.Health.Status}}' learncloud-mysql"

# Restore
# Note: mysql container's MYSQL_ROOT_PASSWORD from env, user learncloud
# Use mysql client inside container or host mysql-client

# Method A: via docker exec
docker exec -i learncloud-mysql mysql -u root -p"${MYSQL_ROOT_PASSWORD}" -e "CREATE DATABASE IF NOT EXISTS ${MYSQL_DATABASE:-learncloud} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"

# Import (may take 5-15 minutes for 500MB dump)
# Use pv if available for progress, else plain
cat /opt/learncloud/backups/latest.sql | docker exec -i learncloud-mysql mysql -u ${MYSQL_USER:-learncloud} -p"${MYSQL_PASSWORD}" ${MYSQL_DATABASE:-learncloud}

# Verify
docker exec -i learncloud-mysql mysql -u ${MYSQL_USER} -p"${MYSQL_PASSWORD}" -e "USE ${MYSQL_DATABASE}; SHOW TABLES; SELECT COUNT(*) FROM tenants; SELECT COUNT(*) FROM users; SELECT COUNT(*) FROM students;" ${MYSQL_DATABASE}

# If counts look reasonable (tenants >0, users >0), success

# Shred unencrypted sql after successful restore (keep encrypted)
shred -u /opt/learncloud/backups/latest.sql || rm -f /opt/learncloud/backups/latest.sql

echo "MySQL restore SUCCESS"
```

**If restore fails halfway:**

- Do NOT drop database yet, check error log
- `docker logs learncloud-mysql | tail -n 100`
- Common: max_allowed_packet too small, foreign key checks
- Retry with `SET FOREIGN_KEY_CHECKS=0;` at top of dump (our dump already uses --single-transaction, but you can add)
- If fails repeatedly, restore previous day's backup: list S3 backups and pick one day earlier

---

## Step 5: Bring Up Whole System (5 minutes)

```bash
cd /opt/learncloud

# Copy .env.production to expected location for compose (../.env.production relative to deployment/docker-compose.yml)
# Our compose expects ../.env.production from deployment folder, so:
# If repo is at /opt/learncloud and compose at /opt/learncloud/deployment/docker-compose.yml,
# then env file should be at /opt/learncloud/.env.production (which we have) and compose uses ../.env.production -> actually deployment/docker-compose.yml's ../ would be /opt/learncloud/deployment/../ => /opt/learncloud/ => good, it will find ../.env.production as /opt/learncloud/.env.production? Wait compose file at deployment/docker-compose.yml, env_file: ../.env.production means deployment/../.env.production = /opt/learncloud/.env.production correct.

# Pull images and build
docker compose -f deployment/docker-compose.yml build --no-cache
# Or pull if using prebuilt images from GHCR:
# docker compose -f deployment/docker-compose.yml pull

# Up all services
docker compose -f deployment/docker-compose.yml up -d

# Watch health
docker ps
docker compose -f deployment/docker-compose.yml ps

# Check health for all
docker inspect --format='{{json .State.Health.Status}}' learncloud-mysql
docker inspect --format='{{json .State.Health.Status}}' learncloud-api
docker inspect --format='{{json .State.Health.Status}}' learncloud-nginx
docker inspect --format='{{json .State.Health.Status}}' learncloud-web

# Logs
docker logs learncloud-api --tail 100
docker logs learncloud-nginx --tail 100

# Wait until all healthy (api may take 30s to start)
for svc in mysql redis api web nginx backup; do echo "=== $svc ==="; docker inspect --format='{{json .State.Health.Status}}' learncloud-$svc; done
```

---

## Step 6: Verify and TLS (5 minutes)

```bash
# Local health checks
curl -f http://localhost:80/nginx-health || curl -f http://localhost:80/health
curl -f http://localhost:8080/health || docker exec -i learncloud-api curl -f http://localhost:8080/health

# If using host nginx as reverse proxy, check:
curl -H "Host: learncloud.co.zw" http://localhost/api/health || curl -H "Host: learncloud.co.zw" http://localhost/health

# TLS - get new certs if not restored
# If DNS already points to this new server IP, run certbot:

docker compose -f deployment/docker-compose.yml run --rm certbot certonly --webroot -w /var/www/certbot -d learncloud.co.zw -d *.learncloud.co.zw --email admin@learncloud.co.zw --agree-tos --no-eff-email

# If DNS not yet pointed, use --manual DNS challenge (requires adding TXT record)
# After certs obtained, restart nginx
docker compose -f deployment/docker-compose.yml restart nginx

# Check https
curl -k https://learncloud.co.zw/health || curl https://learncloud.co.zw/health
```

---

## Step 7: Switch DNS and Final Checks (5 minutes)

- If this is new server replacing old, update DNS A record learncloud.co.zw and *.learncloud.co.zw to new server IP in Cloudflare/Route53
- Wait 5 min for propagation (or lower TTL earlier to 5 min before migration)
- Test in browser: https://learncloud.co.zw should show marketing site
- Test tenant subdomain: https://petra.learncloud.co.zw should show login
- Login as school admin with test account, check students list loads (proves tenant isolation still works)
- Check billing: https://learncloud.co.zw/billing should show subscription state
- Check logs: docker logs learncloud-api --tail 50, ensure no errors about tenant_id guard

---

## Step 8: Re-enable Backups and Monitoring (2 minutes)

```bash
# Ensure backup container is running and has latest backup
docker logs learncloud-backup --tail 20

# Check backup dir
docker exec learncloud-backup ls -lh /backups | tail -n 20

# Verify last backup success file
docker exec learncloud-backup cat /tmp/last_backup_success && echo "Backup recent" || echo "No recent backup yet, will run at 02:00 CAT"

# Monitoring: ensure uptime check URL https://learncloud.co.zw/health is monitored via UptimeRobot
# Disk and memory alerts: check monitoring script
cat deployment/monitoring/check-disk-memory.sh
# Setup cron for disk/memory alerts if not in docker:
# crontab -e -> add 0 * * * * /opt/learncloud/deployment/monitoring/check-disk-memory.sh

# Error tracking: check SENTRY_DSN in .env.production, ensure api logs show no errors
```

---

## Emergency Contacts and Quick Commands

**Quick rollback to previous backup if new backup corrupt:**

```bash
# List backups in S3
aws s3 ls s3://learncloud-backups-bulawayo/mysql/ --recursive --region af-south-1 | tail -n 5

# Download previous day
aws s3 cp s3://learncloud-backups-bulawayo/mysql/2026-08-01/learncloud_2026-08-01_020000.sql.gpg /opt/learncloud/backups/prev.sql.gpg --region af-south-1
echo $BACKUP_ENCRYPTION_PASSPHRASE | gpg --batch --yes --passphrase-fd 0 -d -o /opt/learncloud/backups/prev.sql /opt/learncloud/backups/prev.sql.gpg
cat /opt/learncloud/backups/prev.sql | docker exec -i learncloud-mysql mysql -u learncloud -p"$MYSQL_PASSWORD" learncloud
```

**Quick restart all:**

```bash
cd /opt/learncloud
docker compose -f deployment/docker-compose.yml restart
```

**Quick logs:**

```bash
docker compose -f deployment/docker-compose.yml logs --tail 100 -f api
docker compose -f deployment/docker-compose.yml logs --tail 100 -f nginx
docker compose -f deployment/docker-compose.yml logs --tail 100 -f mysql
```

**Quick disk full:**

```bash
df -h
docker system prune -f
# Clear old backups if disk full (but ensure S3 has copies)
ls -lh /opt/learncloud/backups/
# Keep at least 2 latest local, delete older
```

**This runbook is designed for 2am stressed person: follow steps in order, don't skip, don't rm -rf, check health after each step, breathe.**

---

## Post-Restore Checklist

- [ ] .env.production restored and chmod 600
- [ ] MySQL restore verified with SELECT COUNT(*)
- [ ] All containers healthy
- [ ] https://learncloud.co.zw/health returns 200
- [ ] Tenant subdomain https://petra.learncloud.co.zw loads
- [ ] Login works, students list loads (tenant isolation)
- [ ] Billing page shows subscription state, not suspended incorrectly
- [ ] Backups running, S3 upload working
- [ ] Uptime check green
- [ ] DNS switched, old server stopped after 24h observation
- [ ] Notify schools: "Maintenance completed, system restored from backup, no data loss, RPO 4h"

End of runbook.

