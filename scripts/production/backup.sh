#!/usr/bin/env bash
set -euo pipefail

database="${DETARA_DATABASE_NAME:-Detara}"
backup_dir="${DETARA_SQL_BACKUP_DIR:-/var/opt/mssql/backups}"
sqlcmd="${SQLCMD_BIN:-/opt/mssql-tools18/bin/sqlcmd}"

if [[ ! "$database" =~ ^[A-Za-z0-9_]+$ ]]; then
  echo "DETARA_DATABASE_NAME contém caracteres inválidos." >&2
  exit 2
fi

if [[ -z "${SQLCMDPASSWORD:-}" ]]; then
  echo "SQLCMDPASSWORD deve ser fornecida pelo ambiente do processo." >&2
  exit 2
fi

mkdir -p "$backup_dir"
timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup_file="$backup_dir/${database}_full_${timestamp}.bak"

"$sqlcmd" \
  -S "${SQLCMDHOST:-localhost}" \
  -U "${SQLCMDUSER:-sa}" \
  -C \
  -b \
  -Q "BACKUP DATABASE [$database] TO DISK = N'$backup_file' WITH COPY_ONLY, COMPRESSION, CHECKSUM, INIT, STATS = 10"

"$sqlcmd" \
  -S "${SQLCMDHOST:-localhost}" \
  -U "${SQLCMDUSER:-sa}" \
  -C \
  -b \
  -Q "RESTORE VERIFYONLY FROM DISK = N'$backup_file' WITH CHECKSUM"

echo "$backup_file"
