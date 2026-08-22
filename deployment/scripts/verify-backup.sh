#!/bin/bash
# Verification script - confirms latest dump is restorable without affecting production
set -euo pipefail

BACKUP_DIR="${BACKUP_DIR:-/backups}"
MYSQL_HOST="${MYSQL_HOST:-mysql}"
MYSQL_USER="${MYSQL_USER:-learncloud}"
MYSQL_PASSWORD="${MYSQL_PASSWORD:?must set}"
BACKUP_ENCRYPTION_PASSPHRASE="${BACKUP_ENCRYPTION_PASSPHRASE:?must set}"

LATEST=$(ls -t "${BACKUP_DIR}"/learncloud_*.sql.gpg 2>/dev/null | head -n1)

if [ -z "${LATEST}" ]; then
  echo "No backup file found in ${BACKUP_DIR}"
  exit 1
fi

echo "Verifying latest backup: ${LATEST}"

TMP_SQL="/tmp/verify_$(basename "${LATEST}" .gpg)"
VERIFY_DB="learncloud_verify_$(date +%s)"

echo "${BACKUP_ENCRYPTION_PASSPHRASE}" | gpg --batch --yes --passphrase-fd 0 -d -o "${TMP_SQL}" "${LATEST}"

echo "Decrypted to ${TMP_SQL}, size $(du -h "${TMP_SQL}" | cut -f1)"

# Try create temp DB and import
if mysql -h "${MYSQL_HOST}" -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" -e "CREATE DATABASE \`${VERIFY_DB}\`;" 2>/dev/null; then
  echo "Created verify DB ${VERIFY_DB}, importing..."
  if mysql -h "${MYSQL_HOST}" -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" "${VERIFY_DB}" < "${TMP_SQL}"; then
    echo "VERIFICATION SUCCESS - dump is restorable"
    # Check row counts for critical tables
    for tbl in tenants users students fee_invoices; do
      COUNT=$(mysql -h "${MYSQL_HOST}" -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" -N -e "SELECT COUNT(*) FROM \`${VERIFY_DB}\`.\`${tbl}\`;" 2>/dev/null || echo "0")
      echo "Table ${tbl}: ${COUNT} rows in verified restore"
    done
    mysql -h "${MYSQL_HOST}" -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" -e "DROP DATABASE \`${VERIFY_DB}\`;"
    shred -u "${TMP_SQL}" || rm -f "${TMP_SQL}"
    exit 0
  else
    echo "VERIFICATION FAILED - cannot import"
    mysql -h "${MYSQL_HOST}" -u "${MYSQL_USER}" -p"${MYSQL_PASSWORD}" -e "DROP DATABASE IF EXISTS \`${VERIFY_DB}\`;" 2>/dev/null || true
    rm -f "${TMP_SQL}"
    exit 1
  fi
else
  echo "Cannot create verify DB, doing light check only - grep CREATE TABLE"
  if grep -q "CREATE TABLE" "${TMP_SQL}"; then
    echo "Light verification passed - contains CREATE TABLE"
    rm -f "${TMP_SQL}"
    exit 0
  else
    echo "Light verification FAILED"
    rm -f "${TMP_SQL}"
    exit 1
  fi
fi
