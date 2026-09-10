// Run inside the gateway image with --network none; no WhatsApp traffic or QR.
import assert from 'node:assert/strict';
import {
  access, lstat, mkdir, mkdtemp, rm, readdir, readFile, writeFile,
} from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { WhatsAppClientFactory } from '../src/whatsapp-client-factory.js';
import { cleanupClient } from '../src/client-cleanup.js';
import { WhatsAppGatewayService } from '../src/gateway-service.js';
import { SessionRegistry } from '../src/session-registry.js';
import { DeliveryStore } from '../src/delivery-store.js';
import { cleanupStaleChromiumSingletons } from '../src/chromium-profile.js';

const empresaId = '11111111-1111-4111-8111-111111111111';
const key = `tenant-${empresaId.replaceAll('-', '')}`;
const logger = { info() {}, warn() {}, error() {} };
const options = { empresaId, logger };

async function fixture(t) {
  const root = await mkdtemp(path.join(os.tmpdir(), 'detara-real-browser-'));
  const factory = new WhatsAppClientFactory({
    sessionsPath: root, chromiumExecutablePath: '/usr/bin/chromium',
  });
  const clients = [];
  const create = factory.create.bind(factory);
  factory.create = (sessionKey, context) => {
    const client = create(sessionKey, context);
    clients.push(client);
    return client;
  };
  t.after(async () => {
    for (const client of clients) await cleanupClient(client, options);
    await rm(root, { recursive: true, force: true });
  });
  return { root, factory };
}

function partial(factory) {
  const client = factory.create(key);
  // Null triggers Puppeteer's setUserAgent TypeError after launch/newPage, before
  // Client.js assigns pupBrowser/pupPage or navigates to WhatsApp. Test-only input.
  client.options.userAgent = null;
  return client;
}

test('Chromium real: restore remove singletons obsoletos, preserva LocalAuth e respeita profile ativo', async (t) => {
  const { root, factory } = await fixture(t);
  const profile = path.join(root, `session-${key}`);
  await mkdir(profile, { recursive: true });
  await Promise.all([
    writeFile(path.join(profile, 'SingletonLock'), 'old-container-20'),
    writeFile(path.join(profile, 'SingletonCookie'), 'stale-cookie'),
    writeFile(path.join(profile, 'SingletonSocket'), 'stale-socket'),
    writeFile(path.join(profile, 'detara-preserve.txt'), 'local-auth-preservado'),
  ]);
  const client = partial(factory);
  t.after(() => cleanupClient(client, options));
  await assert.rejects(client.initialize());
  assert.equal(client.pupBrowser, null);
  assert.ok(client.ownedBrowser);
  const child = client.ownedBrowser.process();
  assert.equal(child.exitCode, null);
  assert.equal(
    await readFile(path.join(profile, 'detara-preserve.txt'), 'utf8'),
    'local-auth-preservado',
  );
  const activeCleanup = await cleanupStaleChromiumSingletons({
    profileDirectory: profile, empresaId, logger, processRoot: '/proc',
  });
  assert.equal(activeCleanup.skipped, 'active');
  for (const file of ['SingletonLock', 'SingletonCookie', 'SingletonSocket']) {
    await lstat(path.join(profile, file));
  }
  const tree = await descendants(child.pid);
  assert.ok(tree.length > 1);
  await cleanupClient(client, options);
  assert.ok(child.exitCode !== null || child.signalCode !== null);
  await assertNoLiveProcesses(tree);
  await access(profile);
  await cleanupClient(client, { ...options, logout: true });
  await assert.rejects(access(profile), { code: 'ENOENT' });
});

test('Chromium real: fallback de processo é restrito ao browser A e preserva B', async (t) => {
  const { factory } = await fixture(t);
  const clientA = partial(factory);
  const clientB = factory.create('tenant-22222222222242228222222222222222');
  clientB.options.userAgent = null;
  t.after(() => cleanupClient(clientB, options));
  t.after(() => cleanupClient(clientA, options));
  await Promise.all([assert.rejects(clientA.initialize()), assert.rejects(clientB.initialize())]);
  const treeA = await descendants(clientA.ownedBrowser.process().pid);
  clientA.destroy = async () => { throw new Error('forced destroy failure'); };
  clientA.ownedBrowser.close = async () => { throw new Error('forced close failure'); };
  await cleanupClient(clientA, { ...options, timeoutMs: 1_000 });
  await assertNoLiveProcesses(treeA);
  assert.equal(clientB.ownedBrowser.process().exitCode, null);
  assert.equal(clientB.ownedBrowser.connected, true);
});

test('Chromium real: upstream destroy ignora connected e logout lança TypeError antes de LocalAuth', async (t) => {
  const { root, factory } = await fixture(t);
  const client = partial(factory);
  await assert.rejects(client.initialize());
  client.pupBrowser = client.ownedBrowser;
  client.pupPage = { evaluate: async () => {} };
  assert.equal(typeof client.pupBrowser.isConnected, 'undefined');
  await client.destroy();
  assert.equal(client.pupBrowser.connected, true);
  await assert.rejects(client.logout(), /isConnected is not a function/);
  await access(path.join(root, `session-${key}`));
  await cleanupClient(client, { ...options, logout: true });
  await assert.rejects(access(path.join(root, `session-${key}`)), { code: 'ENOENT' });
});

test('Chromium real: SIGKILL atinge somente o grupo capturado quando SIGTERM não encerra', async (t) => {
  const { factory } = await fixture(t);
  const clientA = partial(factory);
  const clientB = factory.create('tenant-22222222222242228222222222222222');
  clientB.options.userAgent = null;
  await Promise.all([assert.rejects(clientA.initialize()), assert.rejects(clientB.initialize())]);
  const child = clientA.ownedBrowser.process();
  const tree = await descendants(child.pid);
  clientA.destroy = async () => {};
  clientA.ownedBrowser.close = async () => { throw new Error('forced close failure'); };
  // Suppress only the first fallback; the product sends real SIGKILL to its group.
  child.kill = (signal) => { assert.equal(signal, 'SIGTERM'); return false; };
  await cleanupClient(clientA, { ...options, timeoutMs: 1_000 });
  assert.equal(child.signalCode, 'SIGKILL');
  await assertNoLiveProcesses(tree);
  assert.equal(clientB.ownedBrowser.connected, true);
  assert.equal(clientB.ownedBrowser.process().exitCode, null);
});

test('Chromium real: retry espera teardown físico; DELETE remove profile e registry', async (t) => {
  const { root, factory } = await fixture(t);
  const clients = [];
  let release;
  const gate = new Promise((resolve) => { release = resolve; });
  const service = new WhatsAppGatewayService({
    clientFactory: { create() {
      if (clients.length) {
        const previous = clients.at(-1).ownedBrowser.process();
        assert.ok(previous.exitCode !== null || previous.signalCode !== null);
      }
      const client = partial(factory);
      if (!clients.length) client.destroy = () => gate;
      clients.push(client);
      return client;
    } },
    registry: new SessionRegistry(root), deliveryStore: new DeliveryStore(root),
    qrEncoder: async () => { throw new Error('QR prohibited'); },
    logger, connectWaitMs: 20,
  });
  t.after(async () => { release(); await service.shutdown(); });
  await service.start();
  await service.connect(empresaId);
  await until(() => service.contexts.get(empresaId).retiringClient);
  await service.connect(empresaId);
  assert.equal(clients.length, 1);
  release();
  await until(() => clients.length === 2);
  await assert.rejects(clients[1].initializationTask);
  await service.disconnect(empresaId);
  assert.equal(service.registry.get(empresaId), null);
  assert.equal(service.contexts.has(empresaId), false);
  await assert.rejects(access(path.join(root, `session-${key}`)), { code: 'ENOENT' });
  assert.equal((await service.disconnect(empresaId)).status, 'Disconnected');
});

// The fixture independently verifies that the product's /proc inspection sees
// the browser process and that teardown removes its complete process tree.
async function descendants(parent) {
  const rows = [];
  for (const entry of await readdir('/proc')) {
    if (!/^\d+$/.test(entry)) continue;
    try {
      const stat = await readFile(`/proc/${entry}/stat`, 'utf8');
      const fields = stat.slice(stat.lastIndexOf(')') + 2).split(' ');
      rows.push({ pid: Number(entry), ppid: Number(fields[1]) });
    } catch { /* A process can exit between listing and reading /proc. */ }
  }
  const ids = new Set([parent]);
  let size;
  do {
    size = ids.size;
    for (const row of rows) if (ids.has(row.ppid)) ids.add(row.pid);
  } while (size !== ids.size);
  return [...ids];
}

async function assertNoLiveProcesses(ids) {
  await until(async () => {
    let running = false;
    for (const pid of ids) {
      try {
        await readFile(`/proc/${pid}/stat`, 'utf8');
        running = true;
      } catch (error) {
        if (!['ENOENT', 'ESRCH'].includes(error.code)) throw error;
      }
    }
    return !running;
  }, 'Processos filhos ainda presentes em /proc (incluindo zombies)');
}

async function until(predicate, message = 'Condição não atingida no prazo') {
  const deadline = Date.now() + 5_000;
  while (!await predicate()) {
    assert.ok(Date.now() < deadline, message);
    await new Promise((resolve) => setTimeout(resolve, 20));
  }
}
