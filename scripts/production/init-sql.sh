#!/usr/bin/env bash
set -euo pipefail
[[ "${1:-}" == --confirm-initialization ]] || { echo 'Exige --confirm-initialization. Não executa migrations nem seed.' >&2; exit 2; }
source "$(dirname "$0")/common.sh"
load_config
bash "$repo_root/scripts/production/validate-env.sh"
lock_deploy
install -d -o 10001 -g 0 -m 700 /var/backups/detara/sql
install -d -o 1000 -g 1000 -m 700 /var/lib/detara/whatsapp
dc run --rm --no-deps --user 0 --cap-add CHOWN --entrypoint sh reverse-proxy -c 'chown 1000:1000 /data /config'
dc up -d --wait --wait-timeout 180 sqlserver
# Credenciais só entram por stdin. Nunca fazer ALTER LOGIN silencioso em rerun.
{
  printf "IF DB_ID(N'Detara') IS NULL CREATE DATABASE [Detara];\nGO\nUSE [Detara];\nGO\n"
  for role in runtime migration; do
    if [[ "$role" == runtime ]]; then password="$DETARA_SQL_RUNTIME_PASSWORD"; login=detara_runtime; else password="$DETARA_SQL_MIGRATION_PASSWORD"; login=detara_migrator; fi
    printf "IF SUSER_ID(N'%s') IS NULL CREATE LOGIN [%s] WITH PASSWORD=N'%s', CHECK_POLICY=ON;\n" "$login" "$login" "$password"
    printf "IF USER_ID(N'%s') IS NULL CREATE USER [%s] FOR LOGIN [%s];\n" "$login" "$login" "$login"
  done
  printf "ALTER ROLE db_datareader ADD MEMBER detara_runtime;\nALTER ROLE db_datawriter ADD MEMBER detara_runtime;\nALTER ROLE db_owner ADD MEMBER detara_migrator;\n"
} | sql >/dev/null 2>&1 || fail "Inicialização SQL falhou. Verifique estado com operador autorizado; secrets não foram registrados."
echo "Banco e identidades SQL preparados. Runtime sem db_owner; migrations continuam explícitas."
