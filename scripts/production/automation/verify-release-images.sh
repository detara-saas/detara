#!/usr/bin/env bash
set -Eeuo pipefail

automation_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$automation_dir/lib.sh"

[[ $# -eq 3 ]] || {
  echo 'Uso: verify-release-images.sh <full-artifact|production-metadata> <diretório> <release-sha>' >&2
  exit 2
}
validation_scope="$1"
artifact_dir="$2"
release_sha="$3"
case "$validation_scope" in
  full-artifact) validate_release_artifact "$artifact_dir" "$release_sha" ;;
  production-metadata) validate_production_deployment_metadata "$artifact_dir" "$release_sha" ;;
  *) echo 'Escopo de validação inválido.' >&2; exit 2 ;;
esac
command -v docker >/dev/null || { echo 'Docker não encontrado para validar imagens.' >&2; exit 1; }
docker buildx version >/dev/null || { echo 'Docker Buildx não encontrado para validar manifests.' >&2; exit 1; }

registry='ghcr.io/detara-saas/detara'
for entry in \
  'DETARA_API_IMAGE:api' \
  'DETARA_WEB_IMAGE:web' \
  'DETARA_WHATSAPP_GATEWAY_IMAGE:whatsapp-gateway' \
  'DETARA_MIGRATIONS_IMAGE:migrations'; do
  variable="${entry%%:*}"
  component="${entry#*:}"
  immutable="$(release_value "$artifact_dir/release.env" "$variable")"
  expected_digest="${immutable##*@}"
  published_digest="$(docker buildx imagetools inspect "$registry/$component:$release_sha" --format '{{.Manifest.Digest}}')"
  [[ "$published_digest" == "$expected_digest" ]] || {
    echo "Digest publicado não corresponde ao manifesto: $component" >&2
    exit 1
  }
  docker buildx imagetools inspect "$immutable" >/dev/null
  printf 'Imagem imutável validada: %s@%s\n' "$component" "$expected_digest"
done
