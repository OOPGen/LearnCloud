# Environment Variable Reference - LearnCloud Production
**All settings, purpose, safe default, plus secrets management process**
**Assume one operator, no dedicated ops team, simple and recoverable**

## How to Use

- Copy `.env.example` to `.env.production` outside repository (never commit)
- Real secrets stored in `/opt/learncloud/.env.production` on server, permissions 600, owned by operator
- Docker-compose loads `../.env.production` via env_file - file outside repo, not in git
- For second server scaling, same .env file copied via secure scp, plus `TENANT_CONNECTION_RESOLVER` for dedicated DBs

## Secrets Management Process (Documented for One Operator)

**Why outside repository:** Git history never forgets, even if you delete. Secrets in repo = breach.

**Process:**

1. **Generation:** On your laptop, generate secrets with `openssl rand -base64 32` for JWT, `openssl rand -hex 16` for mysql passwords, etc. Never use same secret across envs.

2. **Storage:** Create file `/opt/learncloud/.env.production` on server via SSH, not via git. Example:
   ```
   mkdir -p /opt/learncloud
   chmod 700 /opt/learncloud
   nano /opt/learncloud/.env.production
   chmod 600 /opt/learncloud/.env.production
   chown operator:operator /opt/learncloud/.env.production
   ```

3. **Backup of secrets:** Secrets backup encrypted separately via `gpg --symmetric` with different passphrase stored in password manager (1Password, Bitwarden). File `learncloud-secrets-2026-08-02.gpg` stored in offsite S3 bucket `learncloud-secrets-backup` with versioning, NOT in code backup.

4. **Rotation:** JWT secret rotation requires all users re-login. Do at low traffic, announce maintenance. MySQL password rotation: update MySQL user, then update .env, then docker-compose restart mysql.

5. **Second server:** When scaling to second server, scp .env.production via `scp -P 22 /opt/learncloud/.env.production operator@second-server:/opt/learncloud/.env.production` over WireGuard VPN, not plain internet.

6. **CI/CD:** GitHub Actions uses repository secrets (Settings -> Secrets) not .env file. Secrets in CI never logged (masking). Deployment copies .env.production from server, does not overwrite secrets.

## Reference Table - Every Setting

| Variable | Purpose | Safe Default | Required? | Example |
|---|---|---|---|---|
| **MySQL** |
| MYSQL_ROOT_PASSWORD | MySQL root password for healthcheck and admin | (none) generate `openssl rand -base64 24` | Yes, secret | `r00t_S3cure_2026!` |
| MYSQL_DATABASE | Main DB name | `learncloud` | No | `learncloud` |
| MYSQL_USER | App DB user (not root) | `learncloud` | No | `learncloud` |
| MYSQL_PASSWORD | App DB user password | (none) generate | Yes, secret | `learncloud_app_2026!` |
| **JWT** |
| JWT_SECRET | HS256 signing key, min 32 chars | (none) `openssl rand -base64 48` | Yes, secret | `super-secret-key-must-be-32+chars-long-for-HS256!!` |
| JWT_ISSUER | JWT iss claim | `LearnCloud` | No | `LearnCloud` |
| JWT_AUDIENCE | JWT aud claim | `LearnCloud` | No | `LearnCloud` |
| JWT_ACCESS_MINUTES | Access token lifetime | `15` | No | `15` |
| JWT_REFRESH_DAYS | Refresh token lifetime | `14` | No | `14` |
| **App** |
| ASPNETCORE_ENVIRONMENT | Dotnet env | `Production` | No | `Production` |
| IMAGE_TAG | Docker image tag for deploy/rollback | `latest` | No | `20260802-abc123` |
| **Backup** |
| BACKUP_ENCRYPTION_PASSPHRASE | GPG symmetric passphrase for MySQL dump encryption | (none) generate `openssl rand -base64 24` | Yes, secret | `backup_encrypt_2026!` |
| BACKUP_RETENTION_DAYS | Days to keep backups locally and S3 | `30` | No | `30` |
| BACKUP_S3_BUCKET | S3 bucket for off-server backup sync | (empty = no off-server) | No | `learncloud-backups-bulawayo` |
| AWS_ACCESS_KEY_ID | S3 creds | (empty) | No, only if bucket set | `AKIA...` |
| AWS_SECRET_ACCESS_KEY | S3 creds | (empty) | No | `secret...` |
| AWS_DEFAULT_REGION | S3 region | `af-south-1` | No | `af-south-1` |
| RCLONE_CONFIG | Alternative to AWS CLI, rclone.conf content base64 | (empty) | No | `...` |
| **Rate Limit / Security** |
| RATE_LIMIT_LOGIN_PER_MIN | Login rate limit per IP | `5` | No | `5` |
| RATE_LIMIT_REG_PER_HOUR | Registration per IP per hour | `3` | No | `3` |
| PASSWORD_MAX_FAILED | Max failed login before lockout | `5` | No | `5` |
| PASSWORD_LOCKOUT_MIN | Lockout minutes | `15` | No | `15` |
| **Tenant / Billing** |
| TENANT_DEFAULT_CITY | Default city for new tenants | `Bulawayo` | No | `Bulawayo` |
| BILLING_MIN_CHARGE_STARTER | Starter min charge | `99` | No | `99` |
| BILLING_TRIAL_DAYS | Trial duration | `14` | No | `14` |
| BILLING_READONLY_WINDOW_DAYS | Read-only window after expiry | `30` | No | `30` |
| BILLING_PAST_DUE_GRACE_DAYS | Past due -> suspended grace | `7` | No | `7` |
| **Messaging** |
| SMS_PROVIDER | EcoCashSms or BulkSmsZw | `EcoCashSms` | No | `EcoCashSms` |
| SMS_API_KEY | SMS provider key | (empty) | No, secret if set | `sms_key...` |
| SMS_SENDER_ID | SMS sender ID | `LearnCloud` | No | `LearnCloud` |
| SMS_COST_PER | Cost per SMS USD | `0.05` | No | `0.05` |
| SMS_DAILY_CAP | Hard cap per tenant per day | `1000` | No | `1000` |
| EMAIL_PROVIDER | Smtp or SendGrid | `Smtp` | No | `Smtp` |
| SMTP_HOST | SMTP host | `smtp.mailtrap.io` | No | `smtp.mailtrap.io` |
| SMTP_PORT | SMTP port | `2525` | No | `2525` |
| **Monitoring** |
| SENTRY_DSN | Error tracking DSN (e.g., Sentry) | (empty = no error tracking) | No | `https://...@sentry.io/...` |
| LOG_LEVEL | Serilog level | `Information` | No | `Information` |
| UPTIME_CHECK_URL | UptimeRobot or self health URL | `https://learncloud.co.zw/health` | No | `https://learncloud.co.zw/health` |
| **Nginx / TLS** |
| CERTBOT_EMAIL | Let's Encrypt email | `admin@learncloud.co.zw` | No for first deploy, yes for TLS | `admin@learncloud.co.zw` |
| DOMAIN | Main domain | `learncloud.co.zw` | No | `learncloud.co.zw` |
| **Scaling to Second Server** |
| SECOND_SERVER_IP | IP of second API server for nginx upstream | (empty = single server) | No | `10.0.0.11` |
| TENANT_CONNECTION_RESOLVER | JSON map tenant_id -> connection string for dedicated DBs | (empty = all shared) | No | `{"42":"Server=...;Database=learncloud_tenant_petra;..."}` |

## .env.example File for Repo (Safe Defaults, No Secrets)

```env
# .env.example - SAFE TO COMMIT - no real secrets, only placeholders and defaults
MYSQL_DATABASE=learncloud
MYSQL_USER=learncloud
# MUST SET IN REAL .env.production OUTSIDE REPO:
# MYSQL_ROOT_PASSWORD=changeme-root-please-generate
# MYSQL_PASSWORD=changeme-app-please-generate
# JWT_SECRET=changeme-min-32-chars-please-generate-openssl-rand-base64-48
JWT_ISSUER=LearnCloud
JWT_AUDIENCE=LearnCloud
IMAGE_TAG=latest
BACKUP_RETENTION_DAYS=30
BACKUP_S3_BUCKET=
# BACKUP_ENCRYPTION_PASSPHRASE=changeme-gpg-passphrase
# AWS_ACCESS_KEY_ID=
# AWS_SECRET_ACCESS_KEY=
AWS_DEFAULT_REGION=af-south-1
# ... rest safe defaults as above table
```

## .env.production for Server (Outside Repo, Real Secrets)

Create `/opt/learncloud/.env.production` with:

```
MYSQL_ROOT_PASSWORD=<generated>
MYSQL_PASSWORD=<generated>
MYSQL_DATABASE=learncloud
MYSQL_USER=learncloud
JWT_SECRET=<generated 48 chars>
BACKUP_ENCRYPTION_PASSPHRASE=<generated>
BACKUP_S3_BUCKET=learncloud-backups-bulawayo
AWS_ACCESS_KEY_ID=AKIA...
AWS_SECRET_ACCESS_KEY=...
# ... plus all safe defaults
IMAGE_TAG=20260802-abc123
```

Permissions: `chmod 600 /opt/learncloud/.env.production`

## How to Manage Secrets Outside Repository Checklist

- [ ] .env.production lives in /opt/learncloud/, not in git, not in docker image
- [ ] File permissions 600, owned by operator
- [ ] Backup of secrets encrypted via gpg with different passphrase, stored in S3 bucket learncloud-secrets-backup versioning
- [ ] Password manager entry for each secret with rotation date
- [ ] CI/CD GitHub Secrets = same values as .env.production but masked, never echo in logs
- [ ] When scaling to second server, scp via WireGuard VPN, not email/Slack
- [ ] Rotate JWT secret at low traffic, announce 5 min, force re-login acceptable
- [ ] Rotate MySQL password: create new MySQL user with new password, update .env, restart api, then drop old user after verifying

## Simple and Recoverable Principle

- One operator can recover with just /opt/learncloud/.env.production + S3 backup bucket + this doc
- No Vault, no KMS, no clever - just file permission 600 + encrypted backup + password manager
- When you grow to team, migrate to Doppler or HashiCorp Vault without changing app code - app still reads env vars
