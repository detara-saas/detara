#!/usr/bin/env bash
set -euo pipefail
umask 077
[[ "${1:-}" == --confirm-disposable && -f "${2:-}" && -f "${3:-}" ]] || { echo 'Uso: restore-test.sh --confirm-disposable backup.bak.gz.age /caminho/identity.agekey' >&2; exit 2; }
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
temp="$(mktemp -d /var/tmp/detara-restore.XXXXXX)"
name="detara-restore-$(openssl rand -hex 12)"
container=''
cleanup() {
  if [[ -n "$container" ]]; then
    [[ "$(docker inspect --format '{{index .Config.Labels "detara.disposable"}}' "$container")" == "$name" ]] || return 1
    docker rm -f "$container" >/dev/null
  fi
  # Somente os dois arquivos conhecidos do diretório criado por mktemp.
  rm -f -- "$temp/input.bak" "$temp/backup.gz"
  rmdir -- "$temp"
}
trap cleanup EXIT
age -d -i "$3" -o "$temp/backup.gz" "$2"
gzip -dc "$temp/backup.gz" > "$temp/input.bak"
[[ -s "$temp/input.bak" ]] || { echo 'Backup vazio.' >&2; exit 1; }
MSSQL_SA_PASSWORD="Aa1!$(openssl rand -hex 24)"
export MSSQL_SA_PASSWORD
image='mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04@sha256:ba4c8329f48fb8f02e1416be6a930ebfd71268caee78aa985f3af4315e457c89'
container="$(docker run -d --name "$name" --label "detara.disposable=$name" --network none --memory 2560m -e ACCEPT_EULA=Y -e MSSQL_PID=Express -e MSSQL_SA_PASSWORD "$image")"
ready=false
for ((i=0;i<60;i++)); do
  if docker exec "$container" bash -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S tcp:127.0.0.1,1433 -U sa -C -b -l 3 -Q "SELECT 1"' >/dev/null 2>&1; then ready=true; break; fi
  sleep 2
done
[[ "$ready" == true ]] || { echo 'SQL descartável não ficou pronto.' >&2; exit 1; }
docker exec "$container" mkdir -p /var/opt/mssql/backups
docker cp "$temp/input.bak" "$container:/var/opt/mssql/backups/input.bak"
docker cp "$repo_root/scripts/production/restore-drill.sh" "$container:/tmp/restore-drill.sh"
docker exec --user 0 "$container" chown 10001:0 /var/opt/mssql/backups/input.bak
docker exec "$container" bash -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" SQLCMDHOST=tcp:127.0.0.1,1433; bash /tmp/restore-drill.sh --confirm-disposable input.bak'
echo 'Restore real e CHECKDB aprovados em SQL Express isolado; ambiente descartável será removido.'
