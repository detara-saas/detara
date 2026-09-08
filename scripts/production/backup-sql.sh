#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/common.sh"
load_config
bash "$repo_root/scripts/production/validate-env.sh"
for tool in age rclone gzip sha256sum; do command -v "$tool" >/dev/null || fail "Instale $tool no host."; done
stage=/var/backups/detara/sql
[[ -d "$stage" ]] || fail "Staging não provisionado."
exec 8>"$stage/backup.lock"
flock -n 8 || fail "Outro backup está em execução."
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
name="Detara_full_${stamp}.bak"
sql <<SQL >/dev/null
BACKUP DATABASE [Detara] TO DISK=N'/var/opt/mssql/backups/$name' WITH COPY_ONLY, NO_COMPRESSION, CHECKSUM, INIT;
RESTORE VERIFYONLY FROM DISK=N'/var/opt/mssql/backups/$name' WITH CHECKSUM;
SQL
[[ -s "$stage/$name" ]] || fail "Backup vazio/ausente."
gzip -c "$stage/$name" | age -r "$DETARA_BACKUP_RECIPIENT" -o "$stage/$name.gz.age"
export RCLONE_CONFIG_DETARABACKUP_TYPE=s3 RCLONE_CONFIG_DETARABACKUP_PROVIDER=Cloudflare
export RCLONE_CONFIG_DETARABACKUP_ENDPOINT="$DETARA_R2_ENDPOINT"
export RCLONE_CONFIG_DETARABACKUP_ACCESS_KEY_ID="$DETARA_R2_ACCESS_KEY"
export RCLONE_CONFIG_DETARABACKUP_SECRET_ACCESS_KEY="$DETARA_R2_SECRET_KEY"
export RCLONE_CONFIG_DETARABACKUP_REGION=auto
remote="detarabackup:$DETARA_R2_BUCKET"
upload() {
  local file="$1" destination="$2" expected actual
  expected="$(sha256sum "$file" | cut -d' ' -f1)"
  rclone copyto "$file" "$remote/$destination" --retries 3 --low-level-retries 3 --log-level ERROR
  actual="$(rclone cat "$remote/$destination" --log-level ERROR | sha256sum | cut -d' ' -f1)"
  [[ "$expected" == "$actual" ]] || fail "Checksum remoto divergente."
}
upload "$stage/$name.gz.age" "daily/$name.gz.age"
[[ "$(date -u +%u)" != 7 ]] || upload "$stage/$name.gz.age" "weekly/$name.gz.age"
[[ "$(date -u +%d)" != 01 ]] || upload "$stage/$name.gz.age" "monthly/$name.gz.age"
# O key ring é essencial para recuperar MFA Platform. PFX/senha ficam no cofre externo.
key_volume=detara-production_detara-data-protection-keys
if docker volume inspect "$key_volume" >/dev/null 2>&1; then
  docker run --rm --network none --read-only --user 0 --entrypoint tar \
    --mount "type=volume,source=$key_volume,target=/keys,readonly" \
    "$DETARA_API_IMAGE" -C /keys -czf - . | age -r "$DETARA_BACKUP_RECIPIENT" -o "$stage/keyring_${stamp}.tar.gz.age"
  upload "$stage/keyring_${stamp}.tar.gz.age" "keyring/keyring_${stamp}.tar.gz.age"
  upload "$stage/keyring_${stamp}.tar.gz.age" latest/keyring.tar.gz.age
else
  # Só o banco ainda sem migrations pode anteceder a criação do volume da API.
  sql <<'SQL' >/dev/null
USE [Detara];
IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL THROW 51000, 'Key ring ausente: backup incompleto.', 1;
SQL
fi
# Promover SQL por último: falha no key ring não substitui o último SQL válido.
# latest não possui lifecycle. O key ring é cumulativo; não remover chaves antigas.
upload "$stage/$name.gz.age" latest/database.bak.gz.age
printf '%s\n' "$stamp" > "$stage/last-success.tmp"
mv -- "$stage/last-success.tmp" "$stage/last-success"
echo "Backup SQL criptografado, upload e checksum remoto confirmados: $stamp."
# Somente arquivo plaintext desta execução, após cópia externa confirmada.
rm -- "$stage/$name"
# Retenção local curta apenas de arquivos cifrados antigos; latest externo permanece.
find /var/backups/detara/sql -maxdepth 1 -type f \( -name 'Detara_full_*.bak.gz.age' -o -name 'keyring_*.tar.gz.age' \) -mtime +2 -delete
