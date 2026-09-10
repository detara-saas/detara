import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import vm from 'node:vm';
import { mkdtemp, rm, access, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { PostAuthCoordinator, postAuthStageEvent, classifyPostAuthError } from '../src/post-auth.js';
import { WhatsAppClientFactory } from '../src/whatsapp-client-factory.js';
import { WhatsAppGatewayService } from '../src/gateway-service.js';
import { SessionRegistry } from '../src/session-registry.js';
import { DeliveryStore } from '../src/delivery-store.js';

function deferred() {
  let resolve;
  const promise = new Promise((done) => { resolve = done; });
  return { promise, resolve };
}

class PageDouble {
  constructor() { this.reset(); }
  reset() {
    this.window = { AuthStore: {}, Debug: { VERSION: 'synthetic' },
      require: (name) => ({
        WAWebSocketModel: { Socket: { state: 'CONNECTED' } },
        WAWebConnModel: { Conn: { serialize: () => ({}) } },
        WAWebUserPrefsMeUser: { getMaybeMePnUser: () => null, getMaybeMeLidUser: () => null },
      })[name] };
  }
  isClosed() { return false; }
  async evaluate(fn, arg) {
    await this.beforeEvaluate?.(String(fn));
    return vm.runInNewContext(`(${String(fn)})(arg)`, { window: this.window, arg });
  }
  async waitForFunction() { await this.recover?.(); }
}

function fixture({ timeoutMs = 500 } = {}) {
  const client = new EventEmitter();
  client.pupPage = new PageDouble();
  client.authStrategy = { getAuthEventPayload: async () => {}, afterAuthReady: async () => {} };
  client.attachEventListeners = async () => {
    client.pupPage.window.onAddMessageEvent = () => {};
    client.pupPage.window.onAppStateChangedEvent = () => {};
  };
  const failures = [];
  const stages = [];
  let ready = 0;
  client.on('ready', () => { ready += 1; });
  client.on(postAuthStageEvent, (stage) => stages.push(stage));
  const coordinator = new PostAuthCoordinator(client, (error) => failures.push(error), { timeoutMs });
  return { client, coordinator, failures, stages, ready: () => ready };
}

test('factory desabilita HTML cache sem instalar listener de response ou tocar LocalAuth', async () => {
  const client = new WhatsAppClientFactory({ sessionsPath: os.tmpdir() })
    .create('tenant-11111111111141118111111111111111');
  client.pupPage = { on() { throw new Error('HTML response listener prohibited'); } };
  assert.equal(client.options.webVersionCache.type, 'none');
  await client.initWebVersionCache();
  assert.equal(client.currentIndexHtml, null);
  assert.ok(client.authStrategy);
});

test('post-auth single-flight chega a ready uma vez com WWebJS e listeners', async () => {
  const h = fixture();
  await Promise.all([h.coordinator.run(), h.coordinator.run(), h.coordinator.run()]);
  await h.coordinator.run();
  assert.equal(h.ready(), 1);
  assert.equal(h.failures.length, 0);
  assert.ok(h.client.info);
  assert.equal(h.stages.at(-1), 'READY_EMITTED');
});

test('LoadUtils rejeitado é classificado por fase sem expor mensagem sensível', async () => {
  const h = fixture();
  h.client.pupPage.beforeEvaluate = (fn) => {
    if (fn.includes('window.WWebJS = {}')) throw new Error('secret-phone-token-page');
  };
  await h.coordinator.run();
  assert.equal(h.ready(), 0);
  assert.equal(h.failures[0].postAuthStage, 'POST_AUTH_LOAD_UTILS_STARTED');
  assert.doesNotMatch(h.failures[0].message, /secret-phone/);
});

test('documento novo depois de ready exige nova preparação sem duplicar callbacks no mesmo documento', async () => {
  const h = fixture();
  await h.coordinator.run();
  h.client.pupPage.reset();
  await h.coordinator.run();
  await h.coordinator.run();
  assert.equal(h.ready(), 2);
  assert.equal(h.failures.length, 0);
  assert.equal(h.stages.filter((stage) => stage === 'POST_AUTH_LISTENERS_DONE').length, 2);
});

test('contexto substituído durante LoadUtils recupera no mesmo cliente, no máximo uma vez', async () => {
  const h = fixture();
  let loads = 0;
  h.client.pupPage.beforeEvaluate = (fn) => {
    if (fn.includes('window.WWebJS = {}') && ++loads === 1) {
      h.client.pupPage.reset();
      throw new Error('Execution context was destroyed');
    }
  };
  await h.coordinator.run();
  assert.equal(loads, 2);
  assert.equal(h.ready(), 1);
  assert.ok(h.stages.includes('POST_AUTH_CONTEXT_RETRY'));
});

test('falha de contexto repetida termina sem loop nem ready', async () => {
  const h = fixture();
  let loads = 0;
  h.client.pupPage.beforeEvaluate = (fn) => {
    if (fn.includes('window.WWebJS = {}')) { loads += 1; throw new Error('Execution context was destroyed'); }
  };
  await h.coordinator.run();
  assert.equal(loads, 2);
  assert.equal(h.failures[0].name, 'ContextChanged');
  assert.equal(h.ready(), 0);
});

test('navegação durante listeners não publica ready do documento antigo nem duplica listeners', async () => {
  const h = fixture();
  let calls = 0;
  h.client.attachEventListeners = async () => { calls += 1; h.client.pupPage.reset(); };
  await h.coordinator.run();
  assert.equal(calls, 1);
  assert.equal(h.ready(), 0);
  assert.equal(h.failures[0].name, 'ContextChanged');
});

test('callback atrasado após stop não emite ready nem falha de geração antiga', async () => {
  const h = fixture();
  const gate = deferred();
  const started = deferred();
  h.client.attachEventListeners = async () => { started.resolve(); await gate.promise; };
  const run = h.coordinator.run();
  await started.promise;
  h.coordinator.stop();
  gate.resolve();
  await run;
  assert.equal(h.ready(), 0);
  assert.equal(h.failures.length, 0);
});

test('timeout pós-auth é terminal e resolução tardia não publica ready', async () => {
  const h = fixture({ timeoutMs: 20 });
  const gate = deferred();
  h.client.attachEventListeners = () => gate.promise;
  await h.coordinator.run();
  assert.equal(h.failures[0].name, 'Timeout');
  gate.resolve();
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(h.ready(), 0);
});

test('stop libera watchdog mesmo quando operação antiga nunca resolve', async () => {
  const h = fixture({ timeoutMs: 30_000 });
  const started = deferred();
  h.client.attachEventListeners = () => { started.resolve(); return new Promise(() => {}); };
  const pending = h.coordinator.run();
  await started.promise;
  h.coordinator.stop();
  await pending;
  assert.equal(h.ready(), 0);
  assert.equal(h.failures.length, 0);
});

test('WWebJS ausente ou parcial não é considerado ready', async () => {
  const h = fixture();
  h.client.on(postAuthStageEvent, (stage) => {
    if (stage === 'POST_AUTH_LOAD_UTILS_DONE') h.client.pupPage.window.WWebJS = {};
  });
  await h.coordinator.run();
  assert.equal(h.ready(), 0);
  assert.equal(h.failures.length, 1);
});

test('socket conectado sozinho e listeners ausentes não passam pelo gate', async () => {
  const h = fixture();
  h.client.attachEventListeners = async () => {};
  await h.coordinator.run();
  assert.equal(h.ready(), 0);
  assert.equal(h.failures[0].postAuthStage, 'POST_AUTH_LISTENERS_STARTED');
});

test('erros conhecidos de CDP têm classificação limitada e sem conteúdo de página', () => {
  assert.equal(classifyPostAuthError(new Error('Target closed with secret')), 'TargetClosed');
  assert.equal(classifyPostAuthError(new Error('Failed to add page binding secret')), 'BindingError');
  assert.equal(classifyPostAuthError({ name: 'ProtocolError', message: 'secret' }), 'ProtocolError');
});

test('gateway: pós-auth falhado vira Error, preserva LocalAuth e não afeta empresa B', async (t) => {
  const root = await mkdtemp(path.join(os.tmpdir(), 'detara-postauth-service-'));
  const factory = new WhatsAppClientFactory({ sessionsPath: root });
  const create = factory.create.bind(factory);
  const clients = [];
  const a = '11111111-1111-4111-8111-111111111111';
  const b = '22222222-2222-4222-8222-222222222222';
  factory.create = (key) => {
    const client = create(key);
    clients.push(client);
    client.pupPage = new PageDouble();
    client.initialize = async () => {
      await client.authStrategy.beforeBrowserInitialized();
      await writeFile(path.join(client.authStrategy.userDataDir, 'synthetic'), 'fixture');
      client.pupPage.beforeEvaluate = (fn) => {
        if (key.includes(a.replaceAll('-', '')) && fn.includes('window.WWebJS = {}')) throw new Error('synthetic LoadUtils failure');
      };
      await client.postAuth.run();
    };
    client.attachEventListeners = async () => {
      client.pupPage.window.onAddMessageEvent = () => {};
      client.pupPage.window.onAppStateChangedEvent = () => {};
    };
    return client;
  };
  const service = new WhatsAppGatewayService({ clientFactory: factory,
    registry: new SessionRegistry(root), deliveryStore: new DeliveryStore(root),
    qrEncoder: async () => '', logger: { info() {}, warn() {}, error() {} }, connectWaitMs: 50 });
  t.after(async () => { await service.shutdown(); await rm(root, { recursive: true, force: true }); });
  await service.start();
  await service.connect(a);
  assert.equal((await service.connect(b)).status, 'Connected');
  await service.contexts.get(a).cleanupPromise;
  assert.equal((await service.getStatus(a)).status, 'Error');
  assert.equal(clients.length, 2);
  await access(path.join(clients[0].authStrategy.userDataDir, 'synthetic'));
  await service.disconnect(a);
  await assert.rejects(access(clients[0].authStrategy.userDataDir), { code: 'ENOENT' });
  assert.equal((await service.getStatus(b)).status, 'Connected');
});
