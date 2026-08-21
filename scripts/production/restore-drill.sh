#!/usr/bin/env bash
set -euo pipefail

backup_name="${1:-}"
target_database="${2:-DetaraRestoreDrill_$(date -u +%Y%m%dT%H%M%SZ)}"
backup_dir="${DETARA_SQL_BACKUP_DIR:-/var/opt/mssql/backups}"
data_dir="${DETARA_SQL_DATA_DIR:-/var/opt/mssql/data}"
sqlcmd="${SQLCMD_BIN:-/opt/mssql-tools18/bin/sqlcmd}"

if [[ ! "$backup_name" =~ ^[A-Za-z0-9_.-]+\.bak$ ]]; then
  echo "Informe somente o nome de um arquivo .bak existente no diretório de backups." >&2
  exit 2
fi

if [[ ! "$target_database" =~ ^DetaraRestoreDrill_[A-Za-z0-9_]+$ ]]; then
  echo "O banco temporário deve começar com DetaraRestoreDrill_." >&2
  exit 2
fi

if [[ -z "${SQLCMDPASSWORD:-}" ]]; then
  echo "SQLCMDPASSWORD deve ser fornecida pelo ambiente do processo." >&2
  exit 2
fi

backup_file="$backup_dir/$backup_name"
if [[ ! -f "$backup_file" ]]; then
  echo "Backup não encontrado: $backup_file" >&2
  exit 2
fi

cleanup() {
  "$sqlcmd" -S "${SQLCMDHOST:-localhost}" -U "${SQLCMDUSER:-sa}" -C -b \
    -Q "IF DB_ID(N'$target_database') IS NOT NULL BEGIN ALTER DATABASE [$target_database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$target_database]; END" || true
}
trap cleanup EXIT

mapfile -t logical_names < <(
  "$sqlcmd" -S "${SQLCMDHOST:-localhost}" -U "${SQLCMDUSER:-sa}" -C -b -h -1 -W -s '|' \
    -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'$backup_file'" \
    | awk -F'|' 'NF > 2 {gsub(/^ +| +$/, "", $1); if ($1 != "") print $1}'
)

if [[ "${#logical_names[@]}" -lt 2 ]]; then
  echo "Não foi possível identificar os nomes lógicos do backup." >&2
  exit 3
fi

data_logical="${logical_names[0]}"
log_logical="${logical_names[1]}"
data_logical_sql="${data_logical//\'/\'\'}"
log_logical_sql="${log_logical//\'/\'\'}"

"$sqlcmd" -S "${SQLCMDHOST:-localhost}" -U "${SQLCMDUSER:-sa}" -C -b \
  -Q "RESTORE DATABASE [$target_database] FROM DISK = N'$backup_file' WITH MOVE N'$data_logical_sql' TO N'$data_dir/$target_database.mdf', MOVE N'$log_logical_sql' TO N'$data_dir/${target_database}_log.ldf', RECOVERY, CHECKSUM, STATS = 10"

"$sqlcmd" -S "${SQLCMDHOST:-localhost}" -U "${SQLCMDUSER:-sa}" -C -b \
  -Q "DBCC CHECKDB (N'$target_database') WITH NO_INFOMSGS, ALL_ERRORMSGS"

echo "Restore drill validado em $target_database; o banco temporário será removido."
