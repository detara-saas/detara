#!/usr/bin/env bash
set -euo pipefail
[[ "${1:-}" == --confirm-schema-compatible ]] || { echo 'Exige --confirm-schema-compatible após revisão humana do schema.' >&2; exit 2; }
source "$(dirname "$0")/common.sh"
export DETARA_RELEASE_FILE=/opt/detara/releases/previous.env
load_config
validate_release
bash "$repo_root/scripts/production/validate-env.sh"
lock_deploy
dc pull api web whatsapp-gateway
dc up -d --no-deps whatsapp-gateway
dc up -d --no-deps --wait --wait-timeout 180 api web
bash "$repo_root/scripts/production/smoke.sh"
cp -- /opt/detara/releases/current.env /opt/detara/releases/rollback-from.env
cp -- /opt/detara/releases/previous.env /opt/detara/releases/current.env.new
mv -- /opt/detara/releases/current.env.new /opt/detara/releases/current.env
mv -- /opt/detara/releases/rollback-from.env /opt/detara/releases/previous.env
echo 'Rollback de aplicação aprovado. Schema e dados não foram revertidos.'
