#!/bin/bash
# Static tenant-isolation guards. Run from anywhere; paths resolve from the repo root.
#
# This script used to grep a hard-coded /home/user/src. That directory did not exist
# in the repository or on CI, grep found nothing, and every check reported PASS.
# It now refuses to run if the source directory is missing.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="$ROOT/src"
if [ ! -d "$SRC" ]; then
  echo "❌ Source directory not found: $SRC"
  exit 2
fi

cs_grep() {
  grep -rn "$1" "$SRC" --include="*.cs" --exclude-dir=obj --exclude-dir=bin --exclude-dir=Migrations | grep -v "/Tests/" || true
}

echo "=== Tenant Isolation CI Guards ($SRC) ==="

echo "Checking C1: IgnoreQueryFilters..."
# Allowed only where no tenant can be known yet: the tenant infrastructure itself, and
# custom-domain resolution in TenantResolutionMiddleware.
VIOLATIONS_C1=$(cs_grep '\.IgnoreQueryFilters()' | grep -v "NoTenantScope.cs" | grep -v "TenantContext.cs" | grep -v "TenantResolutionMiddleware.cs" || true)
if [ -n "$VIOLATIONS_C1" ]; then
  echo "❌ C1 VIOLATION:"
  echo "$VIOLATIONS_C1"
  exit 1
else
  echo "✅ C1 PASS"
fi

echo ""
echo "Checking C2: raw SQL..."
VIOLATIONS_C2=$(cs_grep 'FromSqlRaw\|FromSqlInterpolated\|ExecuteSqlRaw\|ExecuteSqlInterpolated\|SqlQueryRaw')
if [ -n "$VIOLATIONS_C2" ]; then
  echo "❌ C2 VIOLATION:"
  echo "$VIOLATIONS_C2"
  exit 1
else
  echo "✅ C2 PASS: No raw SQL methods"
fi

echo ""
echo "Checking C5: explicit TenantId predicates (informational)..."
echo "ℹ️  The global query filter enforces isolation; explicit predicates are defence in depth."
cs_grep 'Set<.*>().Where' | grep -v "TenantId" | grep -v "Tenants" | grep -v "Permissions" | grep -v "Roles" | wc -l | xargs echo "Queries without an explicit TenantId predicate (heuristic, includes global tables):"

echo ""
echo "Checking file storage..."
VIOLATIONS_FILE=$(cs_grep '"/exports/\|"/uploads/' | grep "fileUrl" | grep -v "api/files" | grep -v "AppContext.BaseDirectory" || true)
if [ -n "$VIOLATIONS_FILE" ]; then
  echo "❌ FILE STORAGE VIOLATION:"
  echo "$VIOLATIONS_FILE"
  exit 1
else
  echo "✅ File storage PASS"
fi

echo ""
echo "=== All Tenant Isolation CI Guards Passed ==="
