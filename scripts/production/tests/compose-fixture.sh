#!/usr/bin/env bash
# Somente validação sintética: nunca executar contra produção.
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/../../.." && pwd)"
temp="$(mktemp -d)"
trap 'rm -f -- "$temp/env" "$temp/cert.pfx" "$temp/config.json"; rmdir -- "$temp"' EXIT
export DETARA_DATA_PROTECTION_CERTIFICATE="$temp/cert.pfx"
touch "$temp/cert.pfx"
while IFS= read -r line; do
  if [[ "$line" =~ ^DETARA_[A-Z0-9_]+=$ ]]; then printf '%sSYNTHETIC_NOT_A_SECRET\n' "$line"; else printf '%s\n' "$line"; fi
done < "$repo_root/.env.production.example" > "$temp/env"
docker compose --env-file "$temp/env" -f "$repo_root/compose.production.yml" config --quiet
docker compose --env-file "$temp/env" -f "$repo_root/compose.production.yml" config --format json > "$temp/config.json"
node - "$temp/config.json" <<'JS'
const fs = require('node:fs');
const assert = require('node:assert/strict');
const c = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
for (const [name, s] of Object.entries(c.services)) {
  if (name !== 'reverse-proxy') assert.equal(s.ports, undefined, `${name} publicou porta`);
  assert.equal(s.restart, 'unless-stopped');
  assert.equal(s.logging.driver, 'local');
}
assert.deepEqual(c.services['reverse-proxy'].ports.map(p => p.published).sort(), ['443', '80']);
assert.equal(c.services.sqlserver.environment.MSSQL_PID, 'Express');
assert.equal(c.services.api.depends_on['whatsapp-gateway'], undefined);
assert.equal(c.networks.data.internal, true);
assert.equal(c.services.api.environment.ASPNETCORE_ENVIRONMENT, 'Production');
assert.equal(c.services.api.secrets[0].target, 'detara-data-protection.pfx');
assert.deepEqual(c.services['reverse-proxy'].cap_add, ['NET_BIND_SERVICE']);
console.log('Compose: portas privadas, Express, redes e gateway opcional aprovados.');
JS
