#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
env_file="${1:-$repo_root/.env.production}"

if [[ ! -f "$env_file" ]]; then
  echo "Arquivo de ambiente não encontrado: $env_file" >&2
  exit 2
fi

docker compose \
  --env-file "$env_file" \
  -f "$repo_root/compose.production.yml" \
  config --quiet

echo "Compose Production válido. Nenhum container foi iniciado."
