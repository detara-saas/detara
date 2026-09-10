#!/usr/bin/env bash
# Exercita o wrapper inteiro em container descartável, sem rede nem Docker socket.
set -Eeuo pipefail
[[ -f /.dockerenv && ! -S /var/run/docker.sock ]] || exit 2
workspace=/tmp/detara-ops-wrapper
rm -rf -- "$workspace"
mkdir -p "$workspace/source"
trap 'rm -rf -- "$workspace"' EXIT

(cd /repo && git ls-files -co --exclude-standard -z | tar --null -T - -cf -) | tar -xf - -C "$workspace/source"
chmod +x "$workspace/source/scripts/production/automation/"*.sh \
  "$workspace/source/scripts/production/automation/detara-stage-release" \
  "$workspace/source/scripts/production/automation/detara-deploy-release"

# O deploy sintético prova somente delegação/propagação; não reproduz lógica de produção.
cat > "$workspace/source/scripts/production/deploy.sh" <<'SH'
#!/usr/bin/env bash
set -Eeuo pipefail
[[ "$1" == --confirm-deploy && -f "$2" ]]
printf '%s\n' "$2" > /tmp/authoritative-deploy-called
cp -- "$2" /opt/detara/releases/current.env
SH
chmod 755 "$workspace/source/scripts/production/deploy.sh"
git -C "$workspace/source" init -q -b main
git -C "$workspace/source" config user.name 'Detara QA'
git -C "$workspace/source" config user.email 'qa@detara.invalid'
git -C "$workspace/source" config gc.auto 0
git -C "$workspace/source" add .
git -C "$workspace/source" commit -qm 'synthetic OPS-01 fixture'
sha="$(git -C "$workspace/source" rev-parse HEAD)"
digest='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'

install -d -o root -g root -m 0700 /var/lib/detara-deploy-trust /opt/detara/releases /etc/detara
git clone -q --mirror "$workspace/source" /var/lib/detara-deploy-trust/repository.git
git --git-dir=/var/lib/detara-deploy-trust/repository.git remote set-url origin https://github.com/detara-saas/detara.git
git config --global url."file://$workspace/source".insteadOf https://github.com/detara-saas/detara.git
chown -R root:root /var/lib/detara-deploy-trust
chmod 0700 /var/lib/detara-deploy-trust /var/lib/detara-deploy-trust/repository.git
ssh-keygen -q -t ed25519 -N '' -f "$workspace/deploy-key"
bash "$workspace/source/scripts/production/automation/bootstrap-deploy-user.sh" "$workspace/deploy-key.pub" >/dev/null
bash "$workspace/source/scripts/production/automation/bootstrap-deploy-user.sh" "$workspace/deploy-key.pub" >/dev/null
[[ "$(stat -c '%U:%G %a' /var/lib/detara-deploy)" == 'root:detaradeploy 710' ]]
[[ "$(stat -c '%U:%G %a' /var/lib/detara-deploy/incoming)" == 'root:detaradeploy 1730' ]]
grep -q '^restrict ssh-ed25519 ' /var/lib/detara-deploy/.ssh/authorized_keys
[[ "$(stat -c '%U:%G %a' /var/lib/detara-deploy/.ssh/authorized_keys)" == 'root:root 644' ]]
visudo -cf /etc/sudoers.d/detara-deploy-release >/dev/null
if id -nG detaradeploy | tr ' ' '\n' | grep -qx docker; then exit 1; fi
printf '%s\n' 'DETARA_API_HOST=api.detara.com.br' > /etc/detara/production.env
chmod 600 /etc/detara/production.env

cat > /usr/bin/docker <<SH
#!/usr/bin/env bash
set -Eeuo pipefail
if [[ "\$*" == 'buildx version' ]]; then exit 0; fi
if [[ "\$*" == *"--format {{.Manifest.Digest}}"* ]]; then printf 'sha256:%s\n' '$digest'; exit 0; fi
if [[ "\$*" == buildx\ imagetools\ inspect* ]]; then exit 0; fi
exit 1
SH
chmod 755 /usr/bin/docker

run_id=9001-1
stage="/var/lib/detara-deploy/incoming/$sha-$run_id"
sudo -u detaradeploy /usr/local/bin/detara-stage-release "$sha" "$run_id" >/dev/null
printf '#!/usr/bin/env sh\nexit 0\n' > "$stage/detara-migrate"
printf '%s\n' 'https://api.detara.com.br' > "$stage/public-api-origin.txt"
printf '%s\n' \
  "DETARA_RELEASE_SHA=$sha" \
  "DETARA_API_IMAGE=ghcr.io/detara-saas/detara/api@sha256:$digest" \
  "DETARA_WEB_IMAGE=ghcr.io/detara-saas/detara/web@sha256:$digest" \
  "DETARA_WHATSAPP_GATEWAY_IMAGE=ghcr.io/detara-saas/detara/whatsapp-gateway@sha256:$digest" \
  "DETARA_MIGRATIONS_IMAGE=ghcr.io/detara-saas/detara/migrations@sha256:$digest" > "$stage/release.env"
(cd "$stage" && sha256sum detara-migrate release.env public-api-origin.txt > SHA256SUMS)
chown -R detaradeploy:detaradeploy "$stage"
chmod 700 "$stage"
chmod 600 "$stage/"*
chmod 700 "$stage/detara-migrate"

/usr/local/sbin/detara-deploy-release "$sha" "$run_id" >/dev/null
target="/opt/detara/releases/$sha"
[[ -f /tmp/authoritative-deploy-called ]]
[[ "$(</tmp/authoritative-deploy-called)" == "$target/candidate.env" ]]
[[ "$(readlink -f /opt/detara/current)" == "$target" ]]
[[ "$(<"$target/.detara-release-sha")" == "$sha" ]]
[[ ! -e "$stage" ]]
[[ -f "$target/release-assets/SHA256SUMS" ]]
printf 'Wrapper completo: main root-owned, digests, deploy.sh e promoção aprovados.\n'
