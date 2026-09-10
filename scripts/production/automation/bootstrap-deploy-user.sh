#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

[[ $EUID -eq 0 ]] || { echo 'Execute este bootstrap como root.' >&2; exit 1; }
[[ $# -eq 1 ]] || { echo 'Uso: bootstrap-deploy-user.sh <arquivo-chave-publica-ed25519>' >&2; exit 2; }
public_key_file="$1"
[[ -f "$public_key_file" && ! -L "$public_key_file" ]] || { echo 'Arquivo de chave pública inválido.' >&2; exit 2; }
mapfile -t public_key_lines < "$public_key_file"
[[ ${#public_key_lines[@]} -eq 1 && "${public_key_lines[0]%% *}" == ssh-ed25519 ]] || {
  echo 'Forneça exatamente uma chave pública ssh-ed25519 dedicada.' >&2
  exit 2
}
ssh-keygen -l -f "$public_key_file" >/dev/null

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
for file in lib.sh detara-stage-release detara-deploy-release; do
  [[ -f "$script_dir/$file" && ! -L "$script_dir/$file" ]] || { echo "Arquivo do bootstrap ausente: $file" >&2; exit 1; }
done
for command in useradd install visudo git ssh-keygen getent; do
  command -v "$command" >/dev/null || { echo "Instale o comando obrigatório: $command" >&2; exit 1; }
done

if ! id detaradeploy >/dev/null 2>&1; then
  useradd --system --user-group --create-home --home-dir /var/lib/detara-deploy --shell /bin/bash detaradeploy
  echo 'Usuário detaradeploy criado.'
else
  [[ "$(getent passwd detaradeploy | cut -d: -f6)" == /var/lib/detara-deploy ]] || {
    echo 'Usuário existente possui home inesperado.' >&2; exit 1;
  }
fi
[[ "$(id -gn detaradeploy)" == detaradeploy ]] || {
  echo 'Usuário existente não possui grupo primário dedicado.' >&2; exit 1;
}
if id -nG detaradeploy | tr ' ' '\n' | grep -qx docker; then
  echo 'detaradeploy não pode pertencer ao grupo docker.' >&2
  exit 1
fi

install -d -o root -g detaradeploy -m 0710 /var/lib/detara-deploy
install -d -o root -g detaradeploy -m 1730 /var/lib/detara-deploy/incoming
install -d -o root -g root -m 0755 /var/lib/detara-deploy/.ssh
install -d -o root -g root -m 0755 /usr/local/bin /usr/local/sbin /usr/local/lib /etc/sudoers.d
install -d -o root -g root -m 0755 /opt/detara /opt/detara/releases
authorized_keys="$(mktemp)"
sudoers_candidate="$(mktemp)"
trap 'rm -f -- "$authorized_keys" "$sudoers_candidate"' EXIT
printf 'restrict %s\n' "${public_key_lines[0]}" > "$authorized_keys"
install -o root -g root -m 0644 "$authorized_keys" /var/lib/detara-deploy/.ssh/authorized_keys

install -o root -g root -m 0644 "$script_dir/lib.sh" /usr/local/lib/detara-deploy-validation.sh
install -o root -g root -m 0755 "$script_dir/detara-stage-release" /usr/local/bin/detara-stage-release
install -o root -g root -m 0755 "$script_dir/detara-deploy-release" /usr/local/sbin/detara-deploy-release

install -d -o root -g root -m 0700 /var/lib/detara-deploy-trust
if [[ ! -d /var/lib/detara-deploy-trust/repository.git ]]; then
  git clone --mirror https://github.com/detara-saas/detara.git /var/lib/detara-deploy-trust/repository.git
fi
[[ "$(git --git-dir=/var/lib/detara-deploy-trust/repository.git config --get remote.origin.url)" == https://github.com/detara-saas/detara.git ]] || {
  echo 'Mirror existente aponta para origin inesperado.' >&2; exit 1;
}
chown -R root:root /var/lib/detara-deploy-trust
chmod 0700 /var/lib/detara-deploy-trust /var/lib/detara-deploy-trust/repository.git

printf '%s\n' \
  'Defaults:detaradeploy !requiretty,secure_path=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin' \
  'detaradeploy ALL=(root) NOPASSWD: /usr/local/sbin/detara-deploy-release' > "$sudoers_candidate"
visudo -cf "$sudoers_candidate" >/dev/null
install -o root -g root -m 0440 "$sudoers_candidate" /etc/sudoers.d/detara-deploy-release
visudo -cf /etc/sudoers.d/detara-deploy-release >/dev/null

echo 'Bootstrap concluído: chave dedicada restrita, staging, mirror root-owned, wrapper e sudoers instalados.'
echo 'Nenhuma configuração do sshd, secret de aplicação ou credencial GHCR foi alterada.'
