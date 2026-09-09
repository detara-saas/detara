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
reverse_proxy_container="$(dc ps --quiet reverse-proxy)"
[[ -n "$reverse_proxy_container" ]] || fail 'Reverse proxy ativo não encontrado para validar a configuração candidata.'
reverse_proxy_image="$(docker inspect --format '{{.Image}}' "$reverse_proxy_container")"
[[ "$reverse_proxy_image" =~ ^sha256:[a-f0-9]{64}$ ]] || fail 'Não foi possível identificar a imagem do reverse proxy ativo.'
candidate_caddyfile="$(readlink -f -- "$repo_root/deploy/Caddyfile")"
[[ -f "$candidate_caddyfile" && -r "$candidate_caddyfile" ]] || fail 'Caddyfile candidato não encontrado.'
# O serviço usa IP estático; valide o arquivo candidato fora da rede sem recriar o reverse-proxy.
docker run --rm --network none --read-only --user 1000:1000 \
  --cap-drop ALL --cap-add NET_BIND_SERVICE --security-opt no-new-privileges:true \
  --memory 256m --cpus 0.50 --pids-limit 100 \
  --tmpfs /tmp:size=32m,mode=1777 --tmpfs /data:size=16m,mode=0700 --tmpfs /config:size=16m,mode=0700 \
  -e DETARA_APP_HOST -e DETARA_API_HOST -e DETARA_ACME_EMAIL \
  -e "DETARA_TRUSTED_CIDRS=${DETARA_TRUSTED_CIDRS:-127.0.0.254/32}" \
  -e "DETARA_HSTS_MAX_AGE=${DETARA_HSTS_MAX_AGE:-0}" \
  --mount "type=bind,source=$candidate_caddyfile,target=/etc/caddy/Caddyfile,readonly" \
  "$reverse_proxy_image" caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile
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
