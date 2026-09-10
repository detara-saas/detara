#!/usr/bin/env bash
# Funções puras compartilhadas pelo workflow e pelos entrypoints instalados.
set -Eeuo pipefail

validate_release_sha() {
  [[ "${1:-}" =~ ^[0-9a-f]{40}$ ]] || {
    echo 'Release exige SHA Git completo em minúsculas.' >&2
    return 2
  }
}

validate_deployment_id() {
  [[ "${1:-}" =~ ^[0-9]+-[0-9]+$ ]] || {
    echo 'Identificador de execução inválido.' >&2
    return 2
  }
}

release_staging_path() {
  local root="$1" sha="$2" deployment_id="$3"
  validate_release_sha "$sha" || return $?
  validate_deployment_id "$deployment_id" || return $?
  printf '%s/%s-%s\n' "${root%/}" "$sha" "$deployment_id"
}

validate_release_artifact() {
  local directory="$1" expected_sha="$2" file line key value
  local -a expected=(detara-migrate public-api-origin.txt release.env SHA256SUMS)
  local -a actual=() wanted=() checksums=() origins=()
  local -A values=()

  validate_release_sha "$expected_sha" || return $?
  [[ -d "$directory" && ! -L "$directory" ]] || {
    echo 'Diretório do artefato ausente ou inválido.' >&2
    return 2
  }

  mapfile -t actual < <(find "$directory" -mindepth 1 -maxdepth 1 -printf '%f\n' | LC_ALL=C sort)
  mapfile -t wanted < <(printf '%s\n' "${expected[@]}" | LC_ALL=C sort)
  [[ "${actual[*]}" == "${wanted[*]}" ]] || {
    echo 'Artefato deve conter somente os quatro arquivos esperados.' >&2
    return 2
  }
  for file in "${expected[@]}"; do
    [[ -f "$directory/$file" && ! -L "$directory/$file" ]] || {
      echo "Arquivo obrigatório inválido: $file" >&2
      return 2
    }
  done
  [[ -s "$directory/detara-migrate" ]] || {
    echo 'Bundle de migration ausente ou vazio.' >&2
    return 2
  }
  [[ "$(stat -c '%s' "$directory/detara-migrate")" -le 536870912 \
    && "$(stat -c '%s' "$directory/release.env")" -le 4096 \
    && "$(stat -c '%s' "$directory/public-api-origin.txt")" -le 1024 \
    && "$(stat -c '%s' "$directory/SHA256SUMS")" -le 1024 ]] || {
    echo 'Artefato excede o tamanho máximo permitido.' >&2
    return 2
  }

  mapfile -t checksums < "$directory/SHA256SUMS"
  [[ ${#checksums[@]} -eq 3 ]] || {
    echo 'SHA256SUMS deve listar exatamente os três arquivos da release.' >&2
    return 2
  }
  local index=0
  for file in detara-migrate release.env public-api-origin.txt; do
    line="${checksums[$index]}"
    [[ "$line" =~ ^[0-9a-f]{64}[[:space:]][[:space:]]${file//./\.}$ ]] || {
      echo 'SHA256SUMS possui formato ou caminho não permitido.' >&2
      return 2
    }
    index=$((index + 1))
  done
  (cd "$directory" && sha256sum --check --strict --quiet SHA256SUMS) || {
    echo 'Checksum do artefato de release divergente.' >&2
    return 2
  }

  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    [[ "$line" == *=* ]] || { echo 'Linha inválida em release.env.' >&2; return 2; }
    key="${line%%=*}"
    value="${line#*=}"
    case "$key" in
      DETARA_RELEASE_SHA|DETARA_API_IMAGE|DETARA_WEB_IMAGE|DETARA_WHATSAPP_GATEWAY_IMAGE|DETARA_MIGRATIONS_IMAGE) ;;
      *) echo 'Chave não permitida em release.env.' >&2; return 2 ;;
    esac
    [[ -z "${values[$key]+present}" ]] || { echo 'Chave duplicada em release.env.' >&2; return 2; }
    values[$key]="$value"
  done < "$directory/release.env"
  [[ ${#values[@]} -eq 5 && "${values[DETARA_RELEASE_SHA]:-}" == "$expected_sha" ]] || {
    echo 'release.env não identifica a release solicitada.' >&2
    return 2
  }

  if [[ ! "${values[DETARA_API_IMAGE]:-}" =~ ^ghcr\.io/detara-saas/detara/api@sha256:[0-9a-f]{64}$ ]] \
    || [[ ! "${values[DETARA_WEB_IMAGE]:-}" =~ ^ghcr\.io/detara-saas/detara/web@sha256:[0-9a-f]{64}$ ]] \
    || [[ ! "${values[DETARA_WHATSAPP_GATEWAY_IMAGE]:-}" =~ ^ghcr\.io/detara-saas/detara/whatsapp-gateway@sha256:[0-9a-f]{64}$ ]] \
    || [[ ! "${values[DETARA_MIGRATIONS_IMAGE]:-}" =~ ^ghcr\.io/detara-saas/detara/migrations@sha256:[0-9a-f]{64}$ ]]; then
    echo 'release.env não contém as quatro imagens Detara por digest.' >&2
    return 2
  fi

  mapfile -t origins < "$directory/public-api-origin.txt"
  [[ ${#origins[@]} -eq 1 && "${origins[0]}" =~ ^https://[A-Za-z0-9][A-Za-z0-9.-]*\.[A-Za-z]{2,}$ ]] || {
    echo 'Origem pública da API inválida.' >&2
    return 2
  }
}

release_value() {
  local file="$1" requested="$2" key value
  while IFS='=' read -r key value; do
    [[ "$key" == "$requested" ]] && { printf '%s\n' "$value"; return 0; }
  done < "$file"
  return 1
}
