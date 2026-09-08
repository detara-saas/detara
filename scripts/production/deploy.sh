#!/usr/bin/env bash
set -euo pipefail
[[ "${1:-}" == --confirm-deploy && -n "${2:-}" ]] || { echo 'Uso: deploy.sh --confirm-deploy /opt/detara/releases/candidate.env [--dry-run]' >&2; exit 2; }
source "$(dirname "$0")/common.sh"
export DETARA_RELEASE_FILE="$2"
load_config
validate_release
bash "$repo_root/scripts/production/validate-env.sh"
docker info >/dev/null
dc config --quiet
if [[ "${3:-}" == --dry-run ]]; then echo 'Dry run: validaria Caddy, backup, pull, migration, aplicação e smoke; nada foi iniciado.'; exit 0; fi
lock_deploy
candidate="$(mktemp /opt/detara/releases/candidate.XXXXXX)"
trap 'rm -f -- "$candidate"' EXIT
cp -- "$DETARA_RELEASE_FILE" "$candidate"
chmod 600 "$candidate"
export DETARA_RELEASE_FILE="$candidate"
load_config
echo 'Etapa: validar Caddy.'
dc run --rm --no-deps reverse-proxy caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile
echo 'Etapa: backup obrigatório antes da migration.'
if [[ -f /opt/detara/releases/current.env ]]; then
  DETARA_RELEASE_FILE=/opt/detara/releases/current.env bash "$repo_root/scripts/production/backup-sql.sh"
else
  bash "$repo_root/scripts/production/backup-sql.sh"
fi
echo 'Etapa: obter imagens imutáveis.'
dc pull api web whatsapp-gateway
docker pull "$DETARA_MIGRATIONS_IMAGE"
echo 'Etapa: maintenance window e migration controlada.'
dc stop api
export ConnectionStrings__DefaultConnection="Server=sqlserver,1433;Database=Detara;User Id=detara_migrator;Password=$DETARA_SQL_MIGRATION_PASSWORD;Encrypt=True;TrustServerCertificate=True"
if ! docker run --rm --read-only --memory 768m --cpus 1 --pids-limit 150 --tmpfs /tmp:size=256m,mode=1777 --cap-drop ALL --security-opt no-new-privileges:true \
  --network detara-production_data -e ConnectionStrings__DefaultConnection "$DETARA_MIGRATIONS_IMAGE"; then
  unset ConnectionStrings__DefaultConnection
  fail 'Migration falhou. API permanece parada; investigar antes de retomar. Nenhum Down executado.'
fi
unset ConnectionStrings__DefaultConnection
# Gateway é opcional: sua saúde não bloqueia readiness da API nem o deploy do core.
dc up -d --no-deps whatsapp-gateway
dc up -d --no-deps --wait --wait-timeout 180 api web
dc up -d --no-deps --force-recreate --wait --wait-timeout 90 reverse-proxy
bash "$repo_root/scripts/production/smoke.sh"
if [[ -f /opt/detara/releases/current.env ]] && ! cmp -s "$candidate" /opt/detara/releases/current.env; then
  cp -- /opt/detara/releases/current.env /opt/detara/releases/previous.env
fi
cp -- "$candidate" /opt/detara/releases/current.env.new
mv -- /opt/detara/releases/current.env.new /opt/detara/releases/current.env
date -u +%Y-%m-%dT%H:%M:%SZ > /opt/detara/releases/deployed-at
echo 'Deploy aprovado; current/previous registrados. Sem limpeza automática de imagens/volumes.'
