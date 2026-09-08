#!/usr/bin/env bash
# Executar em container DESCARTÁVEL, --network none e SEM socket Docker.
set -euo pipefail
[[ -f /.dockerenv && ! -S /var/run/docker.sock ]] || exit 2
repo_root="$(cd "$(dirname "$0")/../../.." && pwd)"
mkdir -p /tmp/mock /etc/detara /var/backups/detara/sql
cp "$repo_root/scripts/production/tests/mock-docker" /tmp/mock/docker
cp "$repo_root/scripts/production/tests/mock-rclone" /tmp/mock/rclone
chmod 755 /tmp/mock/docker /tmp/mock/rclone
export PATH="/tmp/mock:$PATH"
export DETARA_ENV_FILE=/etc/detara/production.env
umask 077
while IFS= read -r line; do
  [[ "$line" == DETARA_*=* ]] || continue
  key="${line%%=*}"; value="${line#*=}"
  [[ -n "$value" ]] || value=SyntheticNotASecret00000000000001
  printf '%s=%s\n' "$key" "$value"
done < "$repo_root/.env.production.example" > "$DETARA_ENV_FILE"
age-keygen -o /tmp/identity 2>/dev/null
printf 'DETARA_BACKUP_RECIPIENT=%s\n' "$(age-keygen -y /tmp/identity)" >> "$DETARA_ENV_FILE"
printf 'DETARA_PLATFORM_JWT_KEY=PlatformSyntheticDifferent000000000001\nDETARA_SQL_ADMIN_PASSWORD=AdminSyntheticPassword000000001\nDETARA_SQL_RUNTIME_PASSWORD=RuntimeSyntheticPassword000000001\nDETARA_SQL_MIGRATION_PASSWORD=MigratorSyntheticPassword000000001\nDETARA_S3_ENDPOINT=https://storage.example.com\nDETARA_R2_ENDPOINT=https://backup.example.com\nDETARA_S3_BUCKET=synthetic-media\nDETARA_R2_BUCKET=synthetic-backups\n' >> "$DETARA_ENV_FILE"
touch /etc/detara/data-protection.pfx
source "$repo_root/scripts/production/common.sh"
load_config
bash "$repo_root/scripts/production/validate-env.sh"
printf 'DETARA_JWT_KEY=forbidden\n' > /tmp/bad-release
if (read_env /tmp/bad-release release) 2>/dev/null; then fail 'Manifesto aceitou secret.'; fi
printf 'DETARA_TEST=$(touch /tmp/should-not-exist)\n' > /tmp/injection
if (read_env /tmp/injection) 2>/dev/null; then fail 'Parser aceitou interpolação.'; fi
[[ ! -e /tmp/should-not-exist ]]
chmod 644 /tmp/injection
if (read_env /tmp/injection) 2>/dev/null; then fail 'Parser aceitou modo inseguro.'; fi
bash "$repo_root/scripts/production/backup-sql.sh"
age -d -i /tmp/identity /tmp/remote/synthetic-backups/latest/database.bak.gz.age | gzip -dc | grep -q 'synthetic SQL bytes'
before="$(sha256sum /var/backups/detara/sql/last-success)"
# Evitar colisão de nome sem espera: retirar SOMENTE cifrado local já validado desta fixture.
find /var/backups/detara/sql -maxdepth 1 -type f -name '*.age' -delete
if QA_UPLOAD_FAIL=true bash "$repo_root/scripts/production/backup-sql.sh" >/dev/null 2>&1; then fail 'Upload com falha aceito.'; fi
[[ "$before" == "$(sha256sum /var/backups/detara/sql/last-success)" ]]
find /var/backups/detara/sql -maxdepth 1 -type f -name '*.age' -delete
if QA_CORRUPT=true bash "$repo_root/scripts/production/backup-sql.sh" >/dev/null 2>&1; then fail 'Checksum divergente aceito.'; fi
[[ "$before" == "$(sha256sum /var/backups/detara/sql/last-success)" ]]
mkdir -p /opt/detara/releases
printf 'DETARA_RELEASE_SHA=%040d\n' 1 > /opt/detara/releases/candidate.env
for key in DETARA_API_IMAGE DETARA_WEB_IMAGE DETARA_WHATSAPP_GATEWAY_IMAGE DETARA_MIGRATIONS_IMAGE; do
  printf '%s=ghcr.io/detara-saas/synthetic@sha256:%064d\n' "$key" 1 >> /opt/detara/releases/candidate.env
done
find /var/backups/detara/sql -maxdepth 1 -type f -name '*.age' -delete
if QA_UPLOAD_FAIL=true bash "$repo_root/scripts/production/deploy.sh" --confirm-deploy /opt/detara/releases/candidate.env >/dev/null 2>&1; then fail 'Deploy aceitou backup com falha.'; fi
if grep -q MIGRATION /tmp/docker-calls; then fail 'Migration executada sem backup.'; fi
[[ ! -e /opt/detara/releases/current.env ]]
printf 'Safety fixtures: parser, permissões, manifesto, age, upload, checksum e deploy bloqueado sem backup aprovados (R2/SQL simulados).\n'
