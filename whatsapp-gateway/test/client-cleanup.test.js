import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { mkdtemp, mkdir, writeFile, access, rm, symlink } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { cleanupClient } from '../src/client-cleanup.js';
import { WhatsAppClientFactory } from '../src/whatsapp-client-factory.js';
import { WhatsAppGatewayService } from '../src/gateway-service.js';
import { SessionRegistry } from '../src/session-registry.js';
import { DeliveryStore } from '../src/delivery-store.js';

const empresaA = '11111111-1111-4111-8111-111111111111';
const empresaB = '22222222-2222-4222-8222-222222222222';
const sessionKey = `tenant-${empresaA.replaceAll('-', '')}`;
const logger = { info() {}, warn() {}, error() {} };
const cleanupOptions = { empresaId: empresaA, logger, timeoutMs: 30 };

function deferred() {
  let resolve;
  const promise = new Promise((done) => { resolve = done; });
  return { promise, resolve };
}

function browserDouble() {
  const child = new EventEmitter();
  child.exitCode = null;
  child.signalCode = null;
  child.signals = [];
  child.kill = (signal) => {
    child.signals.push(signal);
    child.signalCode = signal;
    child.emit('exit', null, signal);
  };
  let connected = true;
  return {
    child,
    process: () => child,
    isConnected: () => connected,
    disconnect: async () => { connected = false; },
    close: async () => {
      connected = false;
      child.exitCode = 0;
      child.emit('exit', 0, null);
    },
  };
}

test('destroy que resolve sem fechar Chromium usa browser.close e confirma exit', async () => {
  const browser = browserDouble();
  await cleanupClient({ pupBrowser: browser, destroy: async () => {} }, cleanupOptions);
  assert.equal(browser.child.exitCode, 0);
  assert.equal(browser.isConnected(), false);
});

test('destroy rejeitado usa fallback somente do browser associado', async () => {
  const browser = browserDouble();
  const other = browserDouble();
  await cleanupClient({
    pupBrowser: browser,
    destroy: async () => { throw new TypeError('partial'); },
  }, cleanupOptions);
  assert.equal(browser.child.exitCode, 0);
  assert.equal(other.child.exitCode, null);
  assert.equal(other.isConnected(), true);
});

test('browser.close travado usa SIGTERM do ChildProcess capturado', async () => {
  const browser = browserDouble();
  browser.close = () => new Promise(() => {});
  await cleanupClient({ pupBrowser: browser, destroy: async () => {} }, cleanupOptions);
  assert.deepEqual(browser.child.signals, ['SIGTERM']);
  assert.equal(browser.isConnected(), false);
});

test('cleanup não confirma sucesso se o processo recusa todos os sinais', async () => {
  const browser = browserDouble();
  browser.close = async () => { throw new Error('close failure'); };
  browser.child.kill = () => false;
  await assert.rejects(cleanupClient({
    pupBrowser: browser, destroy: async () => {},
  }, cleanupOptions), { code: 'whatsapp_cleanup_pendente' });
});

test('SIGKILL é último recurso depois de SIGTERM sem exit', async () => {
  const browser = browserDouble();
  browser.close = async () => { throw new Error('close'); };
  browser.child.kill = (signal) => {
    browser.child.signals.push(signal);
    if (signal === 'SIGKILL') {
      browser.child.signalCode = signal;
      browser.child.emit('exit', null, signal);
    }
  };
  await cleanupClient({ pupBrowser: browser, destroy: async () => {} }, cleanupOptions);
  assert.deepEqual(browser.child.signals, ['SIGTERM', 'SIGKILL']);
});

test('timeout de logout não libera profile em DELETE repetido', async () => {
  const gate = deferred();
  let removals = 0;
  let logouts = 0;
  const client = {
    destroy: async () => {},
    logout: () => { logouts += 1; return gate.promise; },
    removeLocalAuth: async () => { removals += 1; },
  };
  const options = { ...cleanupOptions, logout: true, timeoutMs: 5 };
  await assert.rejects(cleanupClient(client, options));
  await assert.rejects(cleanupClient(client, options));
  assert.equal(logouts, 1);
  assert.equal(removals, 0);
  gate.resolve();
  await cleanupClient(client, options);
  assert.equal(removals, 1);
});

test('launch tardio bloqueia cleanup até seu browser poder ser fechado', async () => {
  const gate = deferred();
  const browser = browserDouble();
  const client = { destroy: async () => {}, launchPromise: gate.promise };
  let complete = false;
  const pending = cleanupClient(client, { ...cleanupOptions, timeoutMs: 1_000 })
    .then(() => { complete = true; });
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(complete, false);
  client.ownedBrowser = browser;
  gate.resolve(browser);
  await pending;
  assert.equal(browser.child.exitCode, 0);
});

test('remoção persistente lenta permanece single-flight após timeout', async () => {
  const gate = deferred();
  let removals = 0;
  const client = {
    destroy: async () => {}, logout: async () => {},
    removeLocalAuth: () => { removals += 1; return gate.promise; },
  };
  const options = { ...cleanupOptions, logout: true, timeoutMs: 5 };
  await assert.rejects(cleanupClient(client, options));
  await assert.rejects(cleanupClient(client, options));
  assert.equal(removals, 1);
  gate.resolve();
  await cleanupClient(client, options);
  assert.equal(removals, 1);
});

test('shutdown encerra browsers de ambos os tenants sem logout e ignora evento tardio', async (t) => {
  const h = await harness(t, (client) => queueMicrotask(() => client.emit('ready')));
  let logouts = 0;
  await h.service.connect(empresaA);
  await h.service.connect(empresaB);
  for (const client of h.clients) client.logout = async () => { logouts += 1; };
  const staleReady = h.clients[0].listeners('ready')[0];
  await h.service.shutdown();
  await staleReady();
  assert.equal(logouts, 0);
  assert.equal(h.service.contexts.size, 0);
  for (const client of h.clients) assert.equal(client.pupBrowser.child.exitCode, 0);
  assert.ok(h.service.registry.get(empresaA));
  assert.ok(h.service.registry.get(empresaB));
});

test('cleanup lento bloqueia geração B e não bloqueia outra empresa', async (t) => {
  const gate = deferred();
  const cleanupStarted = deferred();
  const h = await harness(t, (client, index) => {
    if (index === 0) {
      client.destroy = async () => { cleanupStarted.resolve(); await gate.promise; };
      throw new Error('initialize parcial');
    }
    queueMicrotask(() => client.emit('ready'));
  });
  t.after(() => gate.resolve());
  await h.service.connect(empresaA);
  await cleanupStarted.promise;
  const retry = h.service.connect(empresaA);
  assert.equal((await h.service.connect(empresaB)).status, 'Connected');
  await retry;
  assert.equal(h.clients.filter((c) => c.sessionKey === sessionKey).length, 1);
  assert.equal(h.clients[0].pupBrowser.child.exitCode, null);
  gate.resolve();
  await until(() => h.clients.filter((c) => c.sessionKey === sessionKey).length === 2);
  assert.equal(h.clients[0].pupBrowser.child.exitCode, 0);
  assert.equal((await h.service.getStatus(empresaB)).status, 'Connected');
});

test('cleanup definitivo falhado impede reconexão e DELETE permite repetir cleanup', async (t) => {
  const h = await harness(t, (client) => {
    client.pupBrowser.close = async () => { throw new Error('close'); };
    client.pupBrowser.child.kill = () => false;
    throw new Error('initialize');
  }, 10);
  await h.service.connect(empresaA);
  await h.service.contexts.get(empresaA).cleanupPromise.catch(() => {});
  await h.service.connect(empresaA);
  assert.equal(h.clients.length, 1);
  await assert.rejects(h.service.disconnect(empresaA), { code: 'whatsapp_cleanup_pendente' });
  assert.ok(h.service.registry.get(empresaA));
  h.clients[0].pupBrowser.child.kill = (signal) => {
    h.clients[0].pupBrowser.child.signalCode = signal;
    h.clients[0].pupBrowser.child.emit('exit', null, signal);
  };
  assert.equal((await h.service.disconnect(empresaA)).status, 'Disconnected');
  assert.equal(h.service.registry.get(empresaA), null);
});

test('logout TypeError em estado parcial remove LocalAuth real após teardown', async (t) => {
  const root = await tempRoot(t);
  const factory = new WhatsAppClientFactory({ sessionsPath: root });
  const client = factory.create(sessionKey);
  await client.authStrategy.beforeBrowserInitialized();
  const directory = client.authStrategy.userDataDir;
  await writeFile(path.join(directory, 'synthetic'), 'fixture');
  // Exact upstream TypeError: pupPage is undefined, before authStrategy.logout.
  await assert.rejects(client.logout(), TypeError);
  await access(directory);
  client.ownedBrowser = browserDouble();
  await cleanupClient(client, { ...cleanupOptions, logout: true });
  await assert.rejects(access(directory), { code: 'ENOENT' });
  assert.equal(client.ownedBrowser.child.exitCode, 0);
});

test('callback upstream não remove LocalAuth nem recria profile depois de DELETE', async (t) => {
  const root = await tempRoot(t);
  const client = new WhatsAppClientFactory({ sessionsPath: root }).create(sessionKey);
  await client.authStrategy.beforeBrowserInitialized();
  const directory = client.authStrategy.userDataDir;
  const file = path.join(directory, 'preserve');
  await writeFile(file, 'fixture');
  await client.authStrategy.logout();
  await access(file);
  await cleanupClient(client, { ...cleanupOptions, logout: true });
  await assert.rejects(access(directory), { code: 'ENOENT' });
  // Simulate continuation of the upstream framenavigated callback after teardown.
  await client.authStrategy.beforeBrowserInitialized();
  await assert.rejects(access(directory), { code: 'ENOENT' });
});

test('erro transitório preserva arquivos LocalAuth e shutdown não faz logout', async (t) => {
  const root = await tempRoot(t);
  const client = new WhatsAppClientFactory({ sessionsPath: root }).create(sessionKey);
  await client.authStrategy.beforeBrowserInitialized();
  const file = path.join(client.authStrategy.userDataDir, 'synthetic');
  await writeFile(file, 'fixture');
  client.ownedBrowser = browserDouble();
  await cleanupClient(client, cleanupOptions);
  await access(file);
});

test('DELETE concorrente é idempotente e conexão aguarda remoção persistente', async (t) => {
  const gate = deferred();
  const started = deferred();
  const h = await harness(t, (client) => queueMicrotask(() => client.emit('ready')));
  t.after(() => gate.resolve());
  await h.service.connect(empresaA);
  h.clients[0].removeLocalAuth = async () => { started.resolve(); await gate.promise; };
  const first = h.service.disconnect(empresaA);
  await started.promise;
  const second = h.service.disconnect(empresaA);
  const connect = h.service.connect(empresaA);
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(h.clients.length, 1);
  gate.resolve();
  assert.equal((await first).status, 'Disconnected');
  assert.equal((await second).status, 'Disconnected');
  assert.equal((await connect).status, 'Connected');
  assert.equal(h.clients.length, 2);
});

test('falha de remoção LocalAuth mantém registry/context e retorna erro seguro', async (t) => {
  const h = await harness(t, (client) => queueMicrotask(() => client.emit('ready')));
  await h.service.connect(empresaA);
  h.clients[0].removeLocalAuth = async () => { throw new Error('/private/profile'); };
  await assert.rejects(h.service.disconnect(empresaA), (error) => {
    assert.equal(error.code, 'whatsapp_cleanup_pendente');
    assert.doesNotMatch(error.message, /private/);
    return true;
  });
  assert.equal(h.service.registry.get(empresaA).status, 'Error');
  assert.ok(h.service.contexts.get(empresaA));
  await assert.rejects(h.service.connect(empresaA), { code: 'whatsapp_cleanup_pendente' });
  assert.equal(h.clients.length, 1);
  h.clients[0].removeLocalAuth = async () => {};
  await h.service.disconnect(empresaA);
});

test('logout rejeita sessionKey adulterado e symlink sem remover destino', async (t) => {
  const root = await tempRoot(t);
  const client = new WhatsAppClientFactory({ sessionsPath: root }).create(sessionKey);
  client.authStrategy.clientId = '../escape';
  await assert.rejects(client.removeLocalAuth());
  client.authStrategy.clientId = sessionKey;
  const target = path.join(root, 'other-profile');
  await mkdir(target);
  await writeFile(path.join(target, 'preserve'), 'fixture');
  await symlink(target, path.join(root, `session-${sessionKey}`), 'junction');
  await assert.rejects(client.removeLocalAuth());
  await access(path.join(target, 'preserve'));
});

async function tempRoot(t) {
  const root = await mkdtemp(path.join(os.tmpdir(), 'detara-cleanup-'));
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function harness(t, initialize, cleanupTimeoutMs = 1_000) {
  const root = await tempRoot(t);
  const clients = [];
  const service = new WhatsAppGatewayService({
    clientFactory: { create(key) {
      const client = new EventEmitter();
      client.sessionKey = key;
      client.pupBrowser = browserDouble();
      const index = clients.length;
      client.initialize = async () => initialize(client, index);
      client.destroy = async () => {};
      client.logout = async () => { throw new TypeError('partial'); };
      client.removeLocalAuth = async () => {};
      clients.push(client);
      return client;
    } },
    registry: new SessionRegistry(root), deliveryStore: new DeliveryStore(root),
    qrEncoder: async () => 'synthetic', logger, connectWaitMs: 20, cleanupTimeoutMs,
  });
  await service.start();
  t.after(() => service.shutdown());
  return { service, clients };
}

async function until(predicate) {
  const deadline = Date.now() + 2_000;
  while (!predicate()) {
    assert.ok(Date.now() < deadline, 'Condição não atingida no prazo');
    await new Promise((resolve) => setTimeout(resolve, 5));
  }
}
