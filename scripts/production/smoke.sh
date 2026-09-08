#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/common.sh"
load_config
for url in "https://$DETARA_APP_HOST/" "https://$DETARA_API_HOST/health/live" "https://$DETARA_API_HOST/health/ready"; do
  curl --fail --silent --show-error --max-time 20 --retry 5 --retry-delay 3 "$url" >/dev/null || fail "Smoke HTTP falhou."
done
echo 'Web, liveness e readiness aprovados. Faça smoke autenticado manual sem credenciais em scripts.'
