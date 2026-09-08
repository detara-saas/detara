#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/common.sh"
load_config
for key in DETARA_APP_HOST DETARA_API_HOST DETARA_ACME_EMAIL DETARA_SQL_ADMIN_PASSWORD DETARA_SQL_RUNTIME_PASSWORD DETARA_SQL_MIGRATION_PASSWORD DETARA_JWT_KEY DETARA_PLATFORM_JWT_KEY DETARA_DATA_PROTECTION_PASSWORD DETARA_S3_ENDPOINT DETARA_S3_BUCKET DETARA_S3_REGION DETARA_S3_ACCESS_KEY DETARA_S3_SECRET_KEY DETARA_RESEND_API_KEY DETARA_EMAIL_FROM_ADDRESS DETARA_WHATSAPP_GATEWAY_API_KEY DETARA_R2_ENDPOINT DETARA_R2_BUCKET DETARA_R2_ACCESS_KEY DETARA_R2_SECRET_KEY DETARA_BACKUP_RECIPIENT; do
  value="${!key:-}"
  [[ -n "$value" && "${value^^}" != *CHANGE_ME* ]] || fail "$key ausente/placeholder."
done
for key in DETARA_APP_HOST DETARA_API_HOST; do
  [[ "${!key}" =~ ^[a-zA-Z0-9][a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$ ]] || fail "$key inválido."
done
[[ "$DETARA_APP_HOST" != "$DETARA_API_HOST" ]] || fail "Web e API devem ter hosts distintos."
for key in DETARA_JWT_KEY DETARA_PLATFORM_JWT_KEY DETARA_WHATSAPP_GATEWAY_API_KEY; do
  value="${!key}"; [[ ${#value} -ge 32 ]] || fail "$key exige ao menos 32 caracteres aleatórios."
done
[[ "$DETARA_JWT_KEY" != "$DETARA_PLATFORM_JWT_KEY" ]] || fail "JWT tenant/Platform devem ser diferentes."
for key in DETARA_SQL_ADMIN_PASSWORD DETARA_SQL_RUNTIME_PASSWORD DETARA_SQL_MIGRATION_PASSWORD; do
  value="${!key}"
  [[ "$value" =~ ^[A-Za-z0-9_!@%+=.-]{24,128}$ && "$value" =~ [A-Z] && "$value" =~ [a-z] && "$value" =~ [0-9] ]] || fail "$key não satisfaz comprimento/alfabeto/complexidade."
done
[[ "$DETARA_SQL_ADMIN_PASSWORD" != "$DETARA_SQL_RUNTIME_PASSWORD" && "$DETARA_SQL_MIGRATION_PASSWORD" != "$DETARA_SQL_RUNTIME_PASSWORD" && "$DETARA_SQL_MIGRATION_PASSWORD" != "$DETARA_SQL_ADMIN_PASSWORD" ]] || fail "Use credenciais SQL distintas."
for key in DETARA_S3_ENDPOINT DETARA_R2_ENDPOINT; do
  [[ "${!key}" =~ ^https://[A-Za-z0-9.-]+(:[0-9]+)?$ ]] || fail "$key exige origem HTTPS explícita."
done
[[ "$DETARA_BACKUP_RECIPIENT" =~ ^age1[a-z0-9]+$ ]] || fail "Chave pública age inválida."
for key in DETARA_S3_BUCKET DETARA_R2_BUCKET; do
  [[ "${!key}" =~ ^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$ ]] || fail "$key inválido."
done
[[ "${DETARA_HSTS_MAX_AGE:-0}" =~ ^[0-9]+$ ]] || fail "HSTS inválido."
for cidr in ${DETARA_TRUSTED_CIDRS:-127.0.0.254/32}; do
  [[ "$cidr" =~ ^[0-9a-fA-F:.]+/[0-9]+$ && "$cidr" != */0 ]] || fail "CIDR confiável inválido."
done
[[ -f "${DETARA_DATA_PROTECTION_CERTIFICATE:-/etc/detara/data-protection.pfx}" ]] || fail "PFX de Data Protection ausente."
[[ "${DETARA_BACKUP_PATH:-/var/backups/detara/sql}" == /var/backups/detara/sql ]] || fail "Staging de produção deve ser /var/backups/detara/sql."
[[ "${DETARA_WHATSAPP_PATH:-/var/lib/detara/whatsapp}" == /var/lib/detara/whatsapp ]] || fail "Diretório WhatsApp fora do padrão."
echo "Configuração validada; nenhum valor secreto foi exibido."
