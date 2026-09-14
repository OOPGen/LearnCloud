#!/bin/bash
# migrate-live.sh - runs only new migrations not yet applied, with backup before, idempotent check, manual approval for destructive
set -euo pipefail

MYSQL_HOST="${MYSQL_HOST:-mysql}"
MYSQL_DATABASE="${MYSQL_DATABASE:-learncloud}"
MYSQL_USER="${MYSQL_USER:-learncloud}"
MYSQL_PASSWORD="${MYSQL_PASSWORD:?must set}"
MIGRATIONS_DIR="${MIGRATIONS_DIR:-./deployment/migrations}"

echo "=== LearnCloud Live Migration Starting $(date -u) ==="

# 1. Backup before any migration
echo "Taking backup before migration..."
/usr/local/bin/backup.sh || { echo "Backup failed, aborting migration"; exit 1; }
# Verify backup restorable
/usr/local/bin/verify-backup.sh || { echo "Backup verification failed, aborting migration"; exit 1; }

# Ensure migrations table exists
mysql -h "${MYSQL_HOST}" -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" "${MYSQL_DATABASE}" -e "
CREATE TABLE IF NOT EXISTS __migrations (
  id INT AUTO_INCREMENT PRIMARY KEY,
  migration_name VARCHAR(255) NOT NULL UNIQUE,
  applied_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  applied_by VARCHAR(100),
  execution_time_ms INT,
  success TINYINT(1) NOT NULL,
  error_message TEXT NULL
);"

# Find migration files in order
for f in $(ls ${MIGRATIONS_DIR}/V*.sql 2>/dev/null | sort); do
  name=$(basename "$f")
  already=$(mysql -h "${MYSQL_HOST}" -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" -N -e "SELECT COUNT(*) FROM ${MYSQL_DATABASE}.__migrations WHERE migration_name='$name' AND success=1;" 2>/dev/null || echo 0)
  if [ "$already" -gt 0 ]; then
    echo "Skipping already applied $name"
    continue
  fi

  # Check for DESTRUCTIVE comment
  if grep -q "DESTRUCTIVE" "$f"; then
    echo "WARNING: Migration $name contains DESTRUCTIVE changes (DROP TABLE/COLUMN/DELETE data)"
    echo "Contents:"
    head -n 20 "$f"
    read -p "Type YES to continue with DESTRUCTIVE migration $name: " confirm
    if [ "$confirm" != "YES" ]; then
      echo "Aborting due to destructive migration not approved"
      exit 1
    fi
  fi

  echo "Applying $name..."
  START=$(date +%s%N | cut -b1-13)
  if mysql -h "${MYSQL_HOST}" -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" "${MYSQL_DATABASE}" < "$f" 2> /tmp/mig_error.log; then
    END=$(date +%s%N | cut -b1-13)
    DURATION=$((END-START))
    mysql -h "${MYSQL_HOST}" -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" "${MYSQL_DATABASE}" -e "INSERT INTO __migrations (migration_name, applied_by, execution_time_ms, success) VALUES ('$name', '${USER:-deployer}', $DURATION, 1);"
    echo "$name SUCCESS ${DURATION}ms"
  else
    END=$(date +%s%N | cut -b1-13)
    DURATION=$((END-START))
    ERROR=$(cat /tmp/mig_error.log | head -n 500 | sed "s/'/''/g")
    mysql -h "${MYSQL_HOST}" -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" "${MYSQL_DATABASE}" -e "INSERT INTO __migrations (migration_name, applied_by, execution_time_ms, success, error_message) VALUES ('$name', '${USER:-deployer}', $DURATION, 0, '${ERROR}');"
    echo "$name FAILED"
    cat /tmp/mig_error.log
    echo "Migration failed halfway, follow migration-policy.md Case handling. Do NOT continue."
    exit 1
  fi
done

echo "All migrations applied successfully"
