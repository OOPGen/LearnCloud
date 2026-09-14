# Go-Live Checklist - LearnCloud Production Single Server
**For one operator, no dedicated ops team, simple and recoverable**

## Pre-Go-Live (1 Week Before)

- [ ] Domain learncloud.co.zw and *.learncloud.co.zw DNS A record points to server IP, TTL lowered to 300 seconds 48h before go-live
- [ ] Server provisioned: Ubuntu 22.04, 4 vCPU, 8GB RAM, 100GB SSD, backups enabled at provider level
- [ ] SSH key auth only, root login disabled, UFW firewall: allow 22, 80, 443, deny all else
- [ ] Docker and docker-compose-plugin installed, `docker --version`, `docker compose version`
- [ ] `/opt/learncloud/.env.production` created outside repo, chmod 600, all secrets generated via `openssl rand -base64 32`, no placeholder
- [ ] Secrets backup encrypted gpg stored in S3 bucket learncloud-secrets-backup with versioning, passphrase in 1Password
- [ ] TLS certs: Let's Encrypt account, `CERTBOT_EMAIL=admin@learncloud.co.zw`, test `certbot certonly --dry-run`
- [ ] S3 bucket for backups created `learncloud-backups-bulawayo` region af-south-1, lifecycle 30 days expiration, versioning enabled
- [ ] S3 bucket for secrets `learncloud-secrets-backup` lifecycle 90 days, versioning, no public access
- [ ] Monitoring: UptimeRobot check for https://learncloud.co.zw/health every 1 min, alert email admin@learncloud.co.zw
- [ ] Error tracking Sentry DSN created, added to .env.production SENTRY_DSN
- [ ] Log rotation: docker json-file max-size 20m max-file 5 configured in compose
- [ ] MySQL slow log enabled via `monitoring/mysql-slow-log.cnf` long_query_time 2s
- [ ] Backup script tested manually: `docker exec learncloud-backup /usr/local/bin/backup.sh` and `verify-backup.sh` both success, S3 upload success
- [ ] Restore runbook printed and kept in drawer, tested on fresh VM once

## Data & Migration

- [ ] All migrations applied to staging DB and tested: `V1_Auth_Migration.sql`, `V3_Attendance_Timetable.sql`, `V4_Fees.sql`, `V5_Messaging.sql`, `V6_PlatformBilling.sql`, `WizardProgress.sql`
- [ ] Tenant isolation suite passes: `dotnet test --filter MultiTenancy` green, no tenant A can read B
- [ ] Fee calculation tests pass cent-exact: 95.00, 324.34, 30 credit
- [ ] Subscription state machine tests pass every transition
- [ ] Attendance percentage and clash detection tests pass
- [ ] Seed plans: Starter $0.50 min $99 300 SMS, Growth $1.00 min $149 500 SMS, Scale $2.00 min $199 1000 SMS with feature flags
- [ ] Seed three messaging templates: fee reminder, absence notification, general notice

## Security

- [ ] JWT_SECRET min 32 chars, generated, not default
- [ ] MySQL root and app passwords different, strong
- [ ] BACKUP_ENCRYPTION_PASSPHRASE strong, different from DB passwords
- [ ] Nginx security headers: X-Frame-Options SAMEORIGIN, X-Content-Type-Options nosniff, HSTS max-age 31536000, Referrer-Policy strict-origin-when-cross-origin
- [ ] Rate limiting: login 5/min, general 100/min in nginx
- [ ] Request size limits: client_max_body_size 20M for logo/CSV upload, web 5M
- [ ] .env.production not in git, not in docker image, permissions 600
- [ ] No secrets logged: grep logs for JWT, password, secret - none

## Performance & Fast on Slow Connection

- [ ] Marketing site index.html 68KB, no heavy images, CSS mock screenshots, Tailwind CDN, React UMD 42KB gz
- [ ] Gzip enabled in nginx for text/css/js/json/svg
- [ ] Static asset caching 30d immutable
- [ ] API response <400ms p95 at 500 concurrent per tenant cluster test
- [ ] Page load <2s LCP on 3G 1.6Mbps 300ms RTT per NFR-01 Lighthouse

## Go-Live Day

- [ ] Announce maintenance window: 2 hours, e.g. Saturday 02:00-04:00 CAT low traffic
- [ ] Put old system in read-only if migrating from old system
- [ ] Final backup of old system
- [ ] Deploy via GitHub Actions: push to main triggers build, test, isolation suite, migrate, deploy, health check, rollback on failure
- [ ] Watch deploy logs: GitHub Actions -> Deploy job -> SSH output
- [ ] Post-deploy health checks: `curl -f https://learncloud.co.zw/health`, `curl -f https://learncloud.co.zw/api/health`, `https://petra.learncloud.co.zw` login, `https://hillcrest.learncloud.co.zw` login, tenant isolation: login as teacher A cannot access class of teacher B
- [ ] TLS valid: `echo | openssl s_client -connect learncloud.co.zw:443 | openssl x509 -noout -dates`
- [ ] Monitoring green: UptimeRobot, disk/memory check script, Sentry no errors
- [ ] Backup verified: `docker exec learncloud-backup /usr/local/bin/verify-backup.sh` success
- [ ] DNS TTL raised back to 3600 after stable 24h

## Post Go-Live 24h

- [ ] Check logs: `docker compose logs --tail 200 api`, no tenant_id mismatch errors
- [ ] Check billing: revenue by month, trials converting, tenants at risk dashboards
- [ ] Check arrears: 3 real balances still exact after prod deploy
- [ ] Check attendance: mark register 40 learners <10s server time
- [ ] Check messaging: send test SMS to self, delivery log shows provider ref, cost, status Sent, usage counter increments, cap warning works
- [ ] Announce go-live to schools: email with login link, setup wizard 9 steps, time-to-first-invoice 45 min
- [ ] Keep old server running 7 days in case rollback needed, then decommission

## Zero Downtime Deployment Procedure

We use blue-green for api only, web and nginx reload, mysql stays up.

1. **Current api container running as blue (latest tag)**
2. **GitHub Actions builds new images with tag SHA e.g. `abc123`, pushes to GHCR**
3. **SSH to server, pull new image, save previous image IDs to /tmp/prev_images.txt**
4. **Start new api container with --no-deps --force-recreate, keep old mysql/redis running**
   ```
   docker compose -f deployment/docker-compose.yml up -d --no-deps --force-recreate api
   ```
5. **Wait for health check: docker inspect --format='{{json .State.Health.Status}}' learncloud-api should become healthy within 30s (curl /health)**
6. **If healthy fails within 30s: rollback immediately to latest tag**
   ```
   export IMAGE_TAG=latest
   docker compose -f deployment/docker-compose.yml up -d --no-deps --force-recreate api
   ```
7. **If healthy: bring up web, nginx, backup with new images, but no downtime for mysql**
   ```
   docker compose -f deployment/docker-compose.yml up -d web nginx backup
   ```
8. **Apply migrations live: ./deployment/scripts/migrate-live.sh runs only new migrations not yet applied, with transaction**
9. **Reload nginx: docker exec learncloud-nginx nginx -s reload (zero downtime reload)**
10. **Post-deploy health: curl https://learncloud.co.zw/health and https://learncloud.co.zw/api/health**
11. **If health fails after migrations: rollback migrations (see migration policy) and rollback api to latest tag**

**Why zero downtime:** API health check ensures new version serves before old killed, nginx keepalive 32, mysql no restart, web static files nginx reload not restart.

**For second server scaling:** Add second server IP to nginx upstream `api_backend` { server api:8080; server 10.0.0.11:8080; }, change deploy to deploy to both servers via SSH, docker swarm or simple round-robin DNS.

## Rollback Plan

- Image rollback: `export IMAGE_TAG=latest` and `docker compose up -d --force-recreate api web`
- Migration rollback: see migration policy doc
- DNS rollback: if new server fails, point DNS back to old server IP (TTL 300 ensures 5 min propagation)
- Data rollback: restore from latest encrypted backup per restore runbook, RPO 4h
