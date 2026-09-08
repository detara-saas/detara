#!/usr/bin/env bash
# Biblioteca interna; arquivos de configuração nunca são executados como shell.
set -euo pipefail
umask 077
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
fail() { echo "ERRO: $*" >&2; exit 1; }
read_env() {
  local file="$1" mode="${2:-config}" key value line
  [[ -f "$file" && ! -L "$file" ]] || fail "Arquivo de configuração ausente ou link simbólico."
  [[ "$(stat -c '%a' "$file")" == 600 && "$(stat -c '%u:%g' "$file")" == 0:0 ]] || fail "Configuração exige root:root e modo 600."
  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    [[ -z "$line" || "$line" == \#* ]] && continue
    [[ "$line" == *=* ]] || fail "Linha de configuração inválida."
    key="${line%%=*}"; value="${line#*=}"
    [[ "$key" =~ ^DETARA_[A-Z0-9_]+$ ]] || fail "Nome de variável não permitido."
    if [[ "$mode" == release ]]; then
      case "$key" in DETARA_RELEASE_SHA|DETARA_API_IMAGE|DETARA_WEB_IMAGE|DETARA_WHATSAPP_GATEWAY_IMAGE|DETARA_MIGRATIONS_IMAGE) ;; *) fail "Manifesto deve conter somente SHA e imagens, nunca secrets." ;; esac
    fi
    [[ "$value" != *'$'* && "$value" != *'`'* && "$value" != *'"'* && "$value" != *"'"* && "$value" != *'#'* ]] || fail "Use valores literais sem interpolação ou aspas."
    export "$key=$value"
  done < "$file"
}
load_config() {
  DETARA_ENV_FILE="${DETARA_ENV_FILE:-/etc/detara/production.env}"
  read_env "$DETARA_ENV_FILE"
  if [[ -n "${DETARA_RELEASE_FILE:-}" ]]; then read_env "$DETARA_RELEASE_FILE" release; fi
}
dc() { docker compose --project-name detara-production --env-file "$DETARA_ENV_FILE" -f "$repo_root/compose.production.yml" "$@"; }
sql() {
  # Senha já pertence ao ambiente privado do SQL; não aparece em argv ou stdout.
  dc exec -T sqlserver bash -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -r1' "$@"
}
lock_deploy() {
  mkdir -p /opt/detara/releases
  exec 9>/opt/detara/releases/deploy.lock
  flock -n 9 || fail "Outro deploy/rollback está em execução."
}
validate_release() {
  local key value
  [[ "${DETARA_RELEASE_SHA:-}" =~ ^[a-f0-9]{40}$ ]] || fail "Release exige SHA completo."
  for key in DETARA_API_IMAGE DETARA_WEB_IMAGE DETARA_WHATSAPP_GATEWAY_IMAGE DETARA_MIGRATIONS_IMAGE; do
    value="${!key:-}"
    [[ "$value" =~ ^ghcr\.io/[a-z0-9_./-]+@sha256:[a-f0-9]{64}$ ]] || fail "$key exige digest GHCR imutável."
  done
}
