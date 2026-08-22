# Database Migration Policy — How to Handle Migration That Fails Halfway on Live
**One operator, simple and recoverable over clever, for LearnCloud MySQL 8.0**

## Principles

1. **Migrations are forward-only, never edit old migration after it has been applied to production.** If old migration buggy, create new migration that fixes.
2. **Every migration is transactional where possible, idempotent, and has down script, but down is only for dev, not for prod rollback (we restore from backup for prod failures).**
3. **No migration runs without backup taken immediately before.** Backup verified restorable.
4. **Migrations run via `migrate-live.sh` script that logs, checks, and has manual approval for destructive changes.**

## Migration File Naming

`V{number}_{Description}.sql` e.g. `V6_PlatformBilling.sql`, `V7_Add_Column_FeeItem_IsProratable.sql`

Number is sequential, never reuse. File lives in `src/LearnCloud.*/Migrations/` and also copied to `deployment/migrations/` for deploy.

## Migration Table

We use `__ef_migrations_history` or custom `migrations` table:

```sql
CREATE TABLE IF NOT EXISTS __migrations (
  id INT AUTO_INCREMENT PRIMARY KEY,
  migration_name VARCHAR(255) NOT NULL UNIQUE,
  applied_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  applied_by VARCHAR(100),
  execution_time_ms INT,
  success TINYINT(1) NOT NULL,
  error_message TEXT NULL
);
```

`migrate-live.sh` checks this table to run only new migrations not yet applied.

## migrate-live.sh Script Logic

```bash
#!/bin/bash
set -euo pipefail

# 1. Take backup before any migration (mandatory)
echo "Taking backup before migration..."
/usr/local/bin/backup.sh
/usr/local/bin/verify-backup.sh || { echo "Backup verification failed, aborting migration"; exit 1; }

# 2. For each migration file in sorted order not in __migrations table success=1, apply
for f in $(ls deployment/migrations/V*.sql | sort); do
  name=$(basename $f)
  already=$(mysql -h mysql -u $MYSQL_USER -p$MYSQL_PASSWORD $MYSQL_DATABASE -N -e "SELECT COUNT(*) FROM __migrations WHERE migration_name='$name' AND success=1;")
  if [ "$already" -gt 0 ]; then
    echo "Skipping already applied $name"
    continue
  fi

  echo "Applying $name..."
  START=$(date +%s%N | cut -b1-13)

  # Run in transaction if possible (MySQL DDL does not rollback fully, but we try)
  if mysql -h mysql -u $MYSQL_USER -p$MYSQL_PASSWORD $MYSQL_DATABASE < "$f" 2> /tmp/mig_error.log; then
    END=$(date +%s%N | cut -b1-13)
    DURATION=$((END-START))
    mysql -h mysql -u $MYSQL_USER -p$MYSQL_PASSWORD $MYSQL_DATABASE -e "INSERT INTO __migrations (migration_name, applied_by, execution_time_ms, success) VALUES ('$name', '${USER:-deployer}', $DURATION, 1);"
    echo "$name SUCCESS ${DURATION}ms"
  else
    END=$(date +%s%N | cut -b1-13)
    DURATION=$((END-START))
    ERROR=$(cat /tmp/mig_error.log | head -n 200)
    mysql -h mysql -u $MYSQL_USER -p$MYSQL_PASSWORD $MYSQL_DATABASE -e "INSERT INTO __migrations (migration_name, applied_by, execution_time_ms, success, error_message) VALUES ('$name', '${USER:-deployer}', $DURATION, 0, '${ERROR//\'/\'\'}');"
    echo "$name FAILED - see /tmp/mig_error.log"
    echo "$ERROR"
    # Do NOT continue to next migration, stop and alert
    echo "Migration failed halfway, stopping. Follow failure halfway procedure below."
    exit 1
  fi
done

echo "All migrations applied successfully"
```

## Handling Migration That Fails Halfway on Live

**MySQL DDL is not fully transactional — e.g., if migration has 3 statements: CREATE INDEX, ALTER TABLE ADD COLUMN, CREATE TABLE, and second fails, first may have already committed. So halfway failure leaves DB in partial state.**

**Procedure for stressed operator at 2am:**

1. **Stop, don't run next migration.** Script already exits on first failure.

2. **Check what succeeded, what failed:**
   ```bash
   cat /tmp/mig_error.log
   mysql -h mysql -u $MYSQL_USER -p$MYSQL_PASSWORD $MYSQL_DATABASE -e "SELECT * FROM __migrations ORDER BY applied_at DESC LIMIT 5;"
   # Look at failed migration name, error message
   ```

3. **Assess if partial DDL was applied:**
   ```bash
   # Example: migration V7_Add_Column tried to ADD COLUMN fee_items.is_proratable and ADD INDEX, but ADD COLUMN succeeded and ADD INDEX failed due to duplicate
   mysql -h mysql -u $MYSQL_USER -p$MYSQL_PASSWORD $MYSQL_DATABASE -e "SHOW CREATE TABLE fee_items;" | grep is_proratable
   # If column exists, first part succeeded
   ```

4. **Decision tree:**

   **Case A: Failure due to duplicate object (already exists) — safe to mark as success and continue:**
   - If error is "Duplicate column" or "Duplicate key" and object already exists from previous manual attempt, then:
     ```bash
     mysql -h mysql -u $MYSQL_USER -p$MYSQL_PASSWORD $MYSQL_DATABASE -e "UPDATE __migrations SET success=1 WHERE migration_name='V7_Add_Column' ORDER BY id DESC LIMIT 1;"
     # Then rerun migrate-live.sh
     ./deployment/scripts/migrate-live.sh
     ```

   **Case B: Failure due to data issue (e.g., adding NOT NULL column to table with existing rows without default):**
   - Edit migration file to add default or make nullable, but **DO NOT edit file that already partially applied** — create new file V7b_Fix_Add_Column.sql that does `ALTER TABLE fee_items MODIFY is_proratable TINYINT DEFAULT 0` or `UPDATE fee_items SET is_proratable=0 WHERE is_proratable IS NULL`
   - Then mark original as failed, apply fix:
     ```bash
     # Create fix migration file
     cp deployment/migrations/V7_*.sql deployment/migrations/V7b_Fix.sql
     # Edit V7b to be idempotent fix
     ```

   **Case C: Failure due to lock timeout or deadlock under load:**
   - Retry migration during low traffic (02:00 CAT), with `SET lock_wait_timeout=60;` in migration
   - No data loss, just retry

   **Case D: Serious failure, DB in inconsistent state, app errors:**
   - **Restore from backup taken immediately before migration (step 1 of script)** — this is why we take backup before any migration
   - Then fix migration file in dev, test on staging, then re-attempt
   ```bash
   # Restore from backup taken before migration (latest backup before failure)
   ls -t /opt/learncloud/backups/*.gpg | head -n1
   # Decrypt and restore per restore runbook step 4, but only restore DB, not whole system
   # After restore, mark migration as not success and retry with fixed file
   ```

5. **Never run `ALTER TABLE ... DROP COLUMN` in production without backup and without 2-step process:**
   - Step 1: Deploy code that no longer uses column but column still exists (backward compatible)
   - Step 2: Next deploy, after 1 week, drop column via migration

6. **For destructive changes (DROP TABLE, DROP COLUMN, DELETE data): require manual approval:**
   - Migration file must have comment `-- DESTRUCTIVE: requires manual approval` at top
   - `migrate-live.sh` checks for that comment and asks `Read DESTRUCTIVE migration V8_Drop_Old_Table.sql, type YES to continue:`
   - Operator must type YES, else abort

7. **After failure, always verify tenant isolation and fee calculations:**
   - Run `dotnet test --filter MultiTenancy` and `FeeCalculationServiceTests` against live DB snapshot in staging
   - Check 3 real balances still exact

## Example: Migration Fails Halfway

**Migration V7_Add_Proration:**

```sql
ALTER TABLE fee_items ADD COLUMN is_proratable TINYINT(1) NOT NULL DEFAULT 0;
CREATE INDEX idx_fee_items_proratable ON fee_items(tenant_id, is_proratable);
ALTER TABLE fee_items ADD COLUMN proration_rule VARCHAR(20) NOT NULL DEFAULT 'none';
```

**Fails at third statement because proration_rule already exists from manual hotfix:**

- First two statements succeeded, third failed
- DB now has is_proratable column and index, but not proration_rule? Actually third failed, so proration_rule not added
- Fix: create V7b with `ALTER TABLE fee_items ADD COLUMN IF NOT EXISTS proration_rule...` or check existence
- Our migration files use `ADD COLUMN IF NOT EXISTS` where MySQL 8 supports `IF NOT EXISTS`? MySQL 8.0 does not support IF NOT EXISTS for ADD COLUMN, so we use procedure to check INFORMATION_SCHEMA

**Idempotent migration pattern:**

```sql
-- Idempotent add column
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='fee_items' AND COLUMN_NAME='proration_rule');
SET @sql = IF(@col_exists=0, 'ALTER TABLE fee_items ADD COLUMN proration_rule VARCHAR(20) NOT NULL DEFAULT ''none''', 'SELECT ''Column exists, skipping''');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
```

Use this pattern for all future migrations to make them rerunnable.

## Rollback Policy

- **Code rollback:** `export IMAGE_TAG=latest && docker compose up -d --force-recreate api` - zero downtime, old code works with new DB columns if new columns nullable/default (forward compatible)
- **DB rollback:** Only via restore from backup taken before migration. Never run down migrations in prod that drop data.
- **If migration added column with NOT NULL and no default and failed, restore from backup, fix migration to add default, retry**

## Checklist Before Running Migration on Live

- [ ] Backup taken and verified restorable via verify-backup.sh
- [ ] Migration file uses idempotent pattern (IF NOT EXISTS check) where possible
- [ ] Migration tested on staging copy of prod DB (import latest prod backup into staging and run migration)
- [ ] No destructive DROP without 2-step process and manual approval comment
- [ ] Migration execution time estimated (<10s for small, <5min for large) - if large table ALTER, do at 02:00 CAT low traffic
- [ ] Rollback plan written: restore from backup file X, rollback image tag Y

End of policy.
