#!/usr/bin/env bash
# Fixture isolada: não usa Docker socket, SSH, GitHub ou produção.
set -Eeuo pipefail
[[ -f /.dockerenv && ! -S /var/run/docker.sock ]] || exit 2
repo_root="$(cd "$(dirname "$0")/../../../.." && pwd)"
source "$repo_root/scripts/production/automation/lib.sh"
temporary="$(mktemp -d)"
trap 'rm -rf --one-file-system -- "$temporary"' EXIT
sha='1111111111111111111111111111111111111111'
digest='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
artifact="$temporary/artifact"
mkdir "$artifact"
printf '#!/usr/bin/env sh\nexit 0\n' > "$artifact/detara-migrate"
chmod 700 "$artifact/detara-migrate"
printf '%s\n' 'https://api.detara.com.br' > "$artifact/public-api-origin.txt"
printf '%s\n' \
  "DETARA_RELEASE_SHA=$sha" \
  "DETARA_API_IMAGE=ghcr.io/detara-saas/detara/api@sha256:$digest" \
  "DETARA_WEB_IMAGE=ghcr.io/detara-saas/detara/web@sha256:$digest" \
  "DETARA_WHATSAPP_GATEWAY_IMAGE=ghcr.io/detara-saas/detara/whatsapp-gateway@sha256:$digest" \
  "DETARA_MIGRATIONS_IMAGE=ghcr.io/detara-saas/detara/migrations@sha256:$digest" > "$artifact/release.env"
(cd "$artifact" && sha256sum detara-migrate release.env public-api-origin.txt > SHA256SUMS)

validate_release_sha "$sha"
validate_release_artifact "$artifact" "$sha"
[[ "$(release_staging_path /var/lib/detara-deploy/incoming "$sha" 123-1)" == "/var/lib/detara-deploy/incoming/$sha-123-1" ]]

if validate_release_sha '../etc/passwd' 2>/dev/null; then echo 'SHA malformado aceito.' >&2; exit 1; fi
if validate_release_sha 'ABCDEF1111111111111111111111111111111111' 2>/dev/null; then echo 'SHA uppercase aceito.' >&2; exit 1; fi
if validate_deployment_id '../1' 2>/dev/null; then echo 'Traversal no deployment ID aceito.' >&2; exit 1; fi
if release_staging_path /var/lib/detara-deploy/incoming "$sha" '../../etc' >/dev/null 2>&1; then echo 'Traversal de staging aceito.' >&2; exit 1; fi

cp -a "$artifact" "$temporary/missing"
rm "$temporary/missing/SHA256SUMS"
if validate_release_artifact "$temporary/missing" "$sha" 2>/dev/null; then echo 'Manifesto ausente aceito.' >&2; exit 1; fi

cp -a "$artifact" "$temporary/invalid"
sed -i 's#/api@#/other@#' "$temporary/invalid/release.env"
(cd "$temporary/invalid" && sha256sum detara-migrate release.env public-api-origin.txt > SHA256SUMS)
if validate_release_artifact "$temporary/invalid" "$sha" 2>/dev/null; then echo 'Imagem fora da allowlist aceita.' >&2; exit 1; fi

install -m 0644 "$repo_root/scripts/production/automation/lib.sh" /usr/local/lib/detara-deploy-validation.sh
if bash "$repo_root/scripts/production/automation/detara-deploy-release" "$sha" 123-1 extra >/dev/null 2>&1; then
  echo 'Wrapper aceitou argumentos não suportados.' >&2; exit 1
fi
if bash "$repo_root/scripts/production/automation/detara-deploy-release" "$sha" 123-1 >/dev/null 2>&1; then
  echo 'Wrapper aceitou payload ausente.' >&2; exit 1
fi
if bash "$repo_root/scripts/production/automation/detara-stage-release" '../etc' 123-1 >/dev/null 2>&1; then
  echo 'Helper de staging aceitou SHA malformado.' >&2; exit 1
fi
if su nobody -s /bin/bash -c "bash '$repo_root/scripts/production/automation/bootstrap-deploy-user.sh' '$temporary/no-key'" >/dev/null 2>&1; then
  echo 'Bootstrap aceitou execução sem root.' >&2; exit 1
fi
grep -Fq 'detaradeploy ALL=(root) NOPASSWD: /usr/local/sbin/detara-deploy-release' \
  "$repo_root/scripts/production/automation/bootstrap-deploy-user.sh"
product_scripts=(
  "$repo_root/scripts/production/automation/bootstrap-deploy-user.sh"
  "$repo_root/scripts/production/automation/detara-deploy-release"
  "$repo_root/scripts/production/automation/detara-stage-release"
  "$repo_root/scripts/production/automation/lib.sh"
  "$repo_root/scripts/production/automation/verify-release-images.sh"
)
if grep -Eq 'NOPASSWD:[[:space:]]*ALL|StrictHostKeyChecking[=[:space:]]+no|docker system prune|docker compose down' \
  "${product_scripts[@]}"; then
  echo 'Padrão de automação inseguro encontrado.' >&2; exit 1
fi
printf 'Automation fixtures: SHA, traversal, artefato, wrapper, bootstrap e sudo mínimo aprovados.\n'
