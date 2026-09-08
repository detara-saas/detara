// Verifica o ARTEFATO publicado, sem alterar service worker ou cache do usuário.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const root = process.argv[2];
const context = { self: {} };
vm.runInNewContext(fs.readFileSync(path.join(root, 'service-worker-assets.js'), 'utf8'), context);
for (const asset of context.self.assetsManifest.assets) {
  const bytes = fs.readFileSync(path.join(root, asset.url));
  assert.equal('sha256-' + crypto.createHash('sha256').update(bytes).digest('base64'), asset.hash, asset.url);
}
assert.equal(fs.existsSync(path.join(root, 'appsettings.Development.json')), false);
assert.match(JSON.parse(fs.readFileSync(path.join(root, 'appsettings.json'), 'utf8')).Api.BaseUrl, /^https:\/\//);
console.log(`PWA publicado: ${context.self.assetsManifest.assets.length} assets íntegros, sem configuração Development.`);
