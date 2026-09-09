import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { access, mkdtemp, mkdir, rm, writeFile } from 'node:fs/promises';
import http from 'node:http';
import os from 'node:os';
import path from 'node:path';
import { afterEach, test } from 'node:test';
import { createApp } from '../src/app.js';
import { DeliveryStore } from '../src/delivery-store.js';
import { WhatsAppGatewayService } from '../src/gateway-service.js';
import { SessionRegistry } from '../src/session-registry.js';
import { createLogger } from '../src/logger.js';
import {
  injectionFailureEvent,
  removeStaleChromiumLocks,
} from '../src/whatsapp-client-factory.js';

const apiKey = 'gateway-test-key-with-at-least-32-characters';
const empresaA = '11111111-1111-4111-8111-111111111111';
const empresaB = '22222222-2222-4222-8222-222222222222';
const cleanups = [];

afterEach(async () => {
  await Promise.allSettled(cleanups.splice(0).map((cleanup) => cleanup()));
});

test('gateway bloqueia chamada sem autenticação', async () => {
  const harness = await createHarness();
  const response = await fetch(`${harness.baseUrl}/healthz`);
  assert.equal(response.status, 401);
});

test('gateway rejeita EmpresaId inválido', async () => {
  const harness = await createHarness();
  const response = await harness.request('/sessions/invalida/status', {
    tenantId: 'invalida',
  });
  assert.equal(response.status, 400);
});

test('sessões A e B usam clientes e QR Codes isolados', async () => {
  const harness = await createHarness({
    initialize: (client) => {
      queueMicrotask(() => {
        if (client.sessionKey.includes(empresaA.replaceAll('-', ''))) {
          client.emit('ready');
        } else {
          client.emit('qr', 'qr-empresa-b');
        }
      });
    },
  });

  const connectA = await harness.request(`/sessions/${empresaA}/connect`, {
    method: 'POST',
    tenantId: empresaA,
  });
  const connectB = await harness.request(`/sessions/${empresaB}/connect`, {
    method: 'POST',
    tenantId: empresaB,
  });
  const statusA = await connectA.json();
  assert.equal(statusA.status, 'Connected');
  assert.equal(statusA.phoneNumber, '5541999990000');
  const statusB = await connectB.json();
  assert.equal(statusB.status, 'WaitingQRCode');
  assert.equal(statusB.qrCode, 'data:image/png;base64,cXItZW1wcmVzYS1i');
  assert.equal(harness.factory.clients.size, 2);

  const mismatch = await harness.request(`/sessions/${empresaB}/status`, {
    tenantId: empresaA,
  });
  assert.equal(mismatch.status, 403);
});

test('envio conectado usa somente o cliente do tenant e é idempotente', async () => {
  const harness = await createHarness({
    initialize: (client) => queueMicrotask(() => client.emit('ready')),
  });
  await harness.request(`/sessions/${empresaA}/connect`, {
    method: 'POST',
    tenantId: empresaA,
  });
  await harness.request(`/sessions/${empresaB}/connect`, {
    method: 'POST',
    tenantId: empresaB,
  });
  const payload = {
    empresaId: empresaA,
    telefone: '+55 (41) 99999-0000',
    mensagem: 'Veículo pronto para retirada.',
    chaveIdempotencia: 'comunicacao-whatsapp/teste-0001',
  };

  const first = await harness.request('/messages/send', {
    method: 'POST',
    tenantId: empresaA,
    body: payload,
  });
  const repeated = await harness.request('/messages/send', {
    method: 'POST',
    tenantId: empresaA,
    body: payload,
  });
  assert.equal(first.status, 200);
  assert.equal(repeated.status, 200);
  assert.equal((await repeated.json()).reused, true);

  const clientA = harness.factory.forTenant(empresaA);
  const clientB = harness.factory.forTenant(empresaB);
  assert.deepEqual(clientA.sent, [
    {
      chatId: '5541999990000@c.us',
      message: 'Veículo pronto para retirada.',
      options: { sendSeen: false },
    },
  ]);
  assert.deepEqual(clientA.numberLookups, ['5541999990000']);
  assert.equal(clientB.sent.length, 0);
});

test('envio sem sessão conectada retorna conflito seguro', async () => {
  const harness = await createHarness();
  const response = await harness.request('/messages/send', {
    method: 'POST',
    tenantId: empresaA,
    body: {
      empresaId: empresaA,
      telefone: '5541999990000',
      mensagem: 'Veículo pronto.',
      chaveIdempotencia: 'comunicacao-whatsapp/teste-0002',
    },
  });
  assert.equal(response.status, 409);
  assert.equal((await response.json()).code, 'whatsapp_nao_conectado');
});

test('envio com telefone inválido é rejeitado antes do provider', async () => {
  const harness = await createHarness();
  const response = await harness.request('/messages/send', {
    method: 'POST',
    tenantId: empresaA,
    body: {
      empresaId: empresaA,
      telefone: '123',
      mensagem: 'Veículo pronto.',
      chaveIdempotencia: 'comunicacao-whatsapp/teste-0003',
    },
  });
  assert.equal(response.status, 400);
});

test('registro persistente restaura a sessão após reinício do serviço', async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), 'detara-whatsapp-restart-'));
  cleanups.push(() => rm(root, { recursive: true, force: true }));
  const firstFactory = new FakeClientFactory((client) =>
    queueMicrotask(() => client.emit('ready')),
  );
  const first = createService(root, firstFactory);
  await first.start();
  await first.connect(empresaA);
  await first.shutdown();

  const restoredFactory = new FakeClientFactory((client) =>
    queueMicrotask(() => client.emit('ready')),
  );
  const restored = createService(root, restoredFactory);
  await restored.start();
  const status = await waitForStatus(restored, empresaA, 'Connected');
  assert.equal(status.status, 'Connected');
  assert.equal(restoredFactory.clients.size, 1);
  await restored.shutdown();
});

test('sessão restaurada não aceita envio antes do evento ready', async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), 'detara-whatsapp-ready-'));
  cleanups.push(() => rm(root, { recursive: true, force: true }));
  const firstFactory = new FakeClientFactory((client) =>
    queueMicrotask(() => client.emit('ready')),
  );
  const first = createService(root, firstFactory);
  await first.start();
  await first.connect(empresaA);
  await first.shutdown();

  const restoredFactory = new FakeClientFactory();
  const restored = createService(root, restoredFactory);
  await restored.start();
  assert.equal((await restored.getStatus(empresaA)).status, 'Reconnecting');
  await assert.rejects(
    restored.sendMessage({
      empresaId: empresaA,
      phone: '5541999990000',
      message: 'Veículo pronto.',
      idempotencyKey: 'comunicacao-whatsapp/restart-0001',
    }),
    (error) => error?.code === 'whatsapp_nao_conectado',
  );

  restoredFactory.forTenant(empresaA).emit('ready');
  assert.equal(
    (await waitForStatus(restored, empresaA, 'Connected')).status,
    'Connected',
  );
  await restored.shutdown();
});

test('desconexão remove somente a sessão solicitada e exige novo QR', async () => {
  const harness = await createHarness({
    initialize: (client) => queueMicrotask(() => client.emit('ready')),
  });
  await harness.request(`/sessions/${empresaA}/connect`, {
    method: 'POST',
    tenantId: empresaA,
  });
  await harness.request(`/sessions/${empresaB}/connect`, {
    method: 'POST',
    tenantId: empresaB,
  });

  const disconnected = await harness.request(`/sessions/${empresaA}`, {
    method: 'DELETE',
    tenantId: empresaA,
  });

  assert.equal(disconnected.status, 200);
  assert.equal((await disconnected.json()).status, 'Disconnected');
  assert.equal(harness.factory.forTenant(empresaA).logoutCount, 1);
  assert.equal((await harness.service.getStatus(empresaA)).status, 'Disconnected');
  assert.equal((await harness.service.getStatus(empresaB)).status, 'Connected');
});

test('registro adulterado não cria cliente nem atravessa diretório de sessão', async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), 'detara-whatsapp-tamper-'));
  cleanups.push(() => rm(root, { recursive: true, force: true }));
  await writeFile(
    path.join(root, 'registry.json'),
    JSON.stringify({
      version: 1,
      sessions: [
        {
          empresaId: empresaA,
          sessionKey: '../../outra-empresa',
          status: 'Connected',
        },
      ],
    }),
  );
  const factory = new FakeClientFactory();
  const service = createService(root, factory);

  await service.start();

  assert.equal(factory.clients.size, 0);
  assert.equal((await service.getStatus(empresaA)).status, 'Disconnected');
  await service.shutdown();
});

test('logger remove QR, telefone, mensagem e token dos metadados', () => {
  const entries = [];
  const logger = createLogger({
    log: (entry) => entries.push(entry),
    warn: (entry) => entries.push(entry),
    error: (entry) => entries.push(entry),
  });

  logger.info('Evento seguro.', {
    empresaId: empresaA,
    qrCode: 'segredo-qr',
    phone: '5541999990000',
    message: 'conteudo-privado',
    token: 'token-secreto',
  });

  const entry = JSON.parse(entries[0]);
  assert.equal(entry.empresaId, empresaA);
  assert.equal(entry.qrCode, undefined);
  assert.equal(entry.phone, undefined);
  assert.equal(entry.message, 'Evento seguro.');
  assert.notEqual(entry.message, 'conteudo-privado');
  assert.equal(entry.token, undefined);
});

test('preparação do perfil remove somente locks temporários do Chromium', async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), 'detara-whatsapp-locks-'));
  cleanups.push(() => rm(root, { recursive: true, force: true }));
  const sessionKey = `tenant-${empresaA.replaceAll('-', '')}`;
  const sessionDirectory = path.join(root, `session-${sessionKey}`);
  await mkdir(sessionDirectory, { recursive: true });
  await Promise.all([
    writeFile(path.join(sessionDirectory, 'SingletonLock'), 'stale'),
    writeFile(path.join(sessionDirectory, 'SingletonSocket'), 'stale'),
    writeFile(path.join(sessionDirectory, 'SingletonCookie'), 'stale'),
    writeFile(path.join(sessionDirectory, 'Default'), 'preservar'),
  ]);

  removeStaleChromiumLocks(root, sessionKey);

  await assert.rejects(access(path.join(sessionDirectory, 'SingletonLock')));
  await assert.rejects(access(path.join(sessionDirectory, 'SingletonSocket')));
  await assert.rejects(access(path.join(sessionDirectory, 'SingletonCookie')));
  await access(path.join(sessionDirectory, 'Default'));
});

test('conexões simultâneas reutilizam uma única inicialização por empresa', async () => {
  const harness = await createHarness({
    initialize: (client) => queueMicrotask(() => client.emit('qr', 'qr-unico')),
  });

  const responses = await Promise.all(
    Array.from({ length: 6 }, () =>
      harness.request(`/sessions/${empresaA}/connect`, {
        method: 'POST',
        tenantId: empresaA,
      })),
  );

  assert.deepEqual(
    await Promise.all(responses.map((response) => response.json().then((x) => x.status))),
    Array(6).fill('WaitingQRCode'),
  );
  assert.equal(harness.factory.historyForTenant(empresaA).length, 1);
  assert.equal(harness.factory.forTenant(empresaA).initializeCount, 1);

  await harness.request(`/sessions/${empresaA}/connect`, {
    method: 'POST',
    tenantId: empresaA,
  });
  assert.equal(harness.factory.forTenant(empresaA).initializeCount, 1);
});

test('polling de status não cria cliente nem dispara inicialização', async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), 'detara-whatsapp-poll-'));
  cleanups.push(() => rm(root, { recursive: true, force: true }));
  const factory = new FakeClientFactory();
  const service = createService(root, factory);
  await service.registry.load();
  await service.deliveryStore.load();
  const timestamp = new Date().toISOString();
  await service.registry.upsert({
    id: 'status-read-only',
    empresaId: empresaA,
    sessionKey: `tenant-${empresaA.replaceAll('-', '')}`,
    status: 'Reconnecting',
    createdAt: timestamp,
    updatedAt: timestamp,
    lastConnectedAt: null,
    phoneNumber: null,
  });

  for (let index = 0; index < 10; index += 1) {
    assert.equal((await service.getStatus(empresaA)).status, 'Reconnecting');
  }

  assert.equal(factory.created.length, 0);
  await service.shutdown();
});

test('authenticated duplicado não antecipa ready nem repete log', async () => {
  const entries = [];
  const logger = {
    info: (message) => entries.push(message),
    warn() {},
    error() {},
  };
  const harness = await createHarness({
    logger,
    connectWaitMs: 20,
    initialize: (client) => queueMicrotask(() => {
      client.emit('authenticated');
      client.emit('authenticated');
    }),
  });

  const response = await harness.request(`/sessions/${empresaA}/connect`, {
    method: 'POST',
    tenantId: empresaA,
  });

  assert.equal((await response.json()).status, 'Connecting');
  assert.equal(
    entries.filter((message) => message === 'Sessão WhatsApp autenticada.').length,
    1,
  );
  assert.equal(harness.factory.forTenant(empresaA).initializeCount, 1);

  await harness.request(`/sessions/${empresaA}/connect`, {
    method: 'POST',
    tenantId: empresaA,
  });
  assert.equal(harness.factory.forTenant(empresaA).initializeCount, 1);
});

test('falha síncrona de initialize é controlada e próxima conexão usa cliente novo', async () => {
  let attempts = 0;
  const harness = await createHarness({
    initialize: (client) => {
      attempts += 1;
      if (attempts === 1) {
        throw new Error(
          'Execution context was destroyed, most likely because of a navigation.',
        );
      }
      queueMicrotask(() => client.emit('ready'));
    },
  });

  const first = await harness.request(`/sessions/${empresaA}/connect`, {
    method: 'POST',
    tenantId: empresaA,
  });
  assert.equal((await first.json()).status, 'Error');
  const firstClient = harness.factory.historyForTenant(empresaA)[0];
  assert.equal(firstClient.destroyCount, 1);

  const second = await harness.request(`/sessions/${empresaA}/connect`, {
    method: 'POST',
    tenantId: empresaA,
  });
  assert.equal((await second.json()).status, 'Connected');
  assert.equal(harness.factory.historyForTenant(empresaA).length, 2);
  assert.equal(harness.factory.forTenant(empresaA).initializeCount, 1);
});

test('falha síncrona da factory não se torna rejeição fora do ciclo de vida', async () => {
  const harness = await createHarness({
    create: () => {
      throw new Error('Chromium indisponível');
    },
  });

  const response = await harness.request(`/sessions/${empresaA}/connect`, {
    method: 'POST',
    tenantId: empresaA,
  });

  assert.equal(response.status, 200);
  assert.equal((await response.json()).status, 'Error');
  assert.equal(harness.factory.created.length, 0);
});

test('evento tardio do cliente descartado não altera a sessão substituta', async () => {
  let attempts = 0;
  const harness = await createHarness({
    connectWaitMs: 100,
    initialize: () => {
      attempts += 1;
      if (attempts === 1) throw new Error('falha inicial');
    },
  });

  await harness.request(`/sessions/${empresaA}/connect`, {
    method: 'POST',
    tenantId: empresaA,
  });
  const discarded = harness.factory.historyForTenant(empresaA)[0];
  const reconnect = harness.request(`/sessions/${empresaA}/connect`, {
    method: 'POST',
    tenantId: empresaA,
  });
  await waitForClientCount(harness.factory, empresaA, 2);
  discarded.emit('ready');
  assert.equal((await harness.service.getStatus(empresaA)).status, 'Connecting');

  harness.factory.forTenant(empresaA).emit('ready');
  assert.equal((await (await reconnect).json()).status, 'Connected');
});

test('disconnected aposenta cliente e reconexão não reutiliza instância antiga', async () => {
  const harness = await createHarness({
    initialize: (client) => queueMicrotask(() => client.emit('ready')),
  });
  await harness.request(`/sessions/${empresaA}/connect`, {
    method: 'POST',
    tenantId: empresaA,
  });
  const firstClient = harness.factory.forTenant(empresaA);
  firstClient.emit('disconnected', 'NAVIGATION');
  assert.equal(
    (await waitForStatus(harness.service, empresaA, 'Disconnected')).status,
    'Disconnected',
  );
  assert.equal(firstClient.destroyCount, 1);

  const reconnected = await harness.request(`/sessions/${empresaA}/connect`, {
    method: 'POST',
    tenantId: empresaA,
  });
  assert.equal((await reconnected.json()).status, 'Connected');
  assert.equal(harness.factory.historyForTenant(empresaA).length, 2);
});

test('falha de reinjeção é isolada ao tenant e não derruba outra empresa', async () => {
  const harness = await createHarness({
    initialize: (client) => queueMicrotask(() => {
      if (client.sessionKey.includes(empresaA.replaceAll('-', ''))) {
        client.emit(
          injectionFailureEvent,
          new Error('Failed to add page binding with name onQRChangedEvent'),
        );
      } else {
        client.emit('ready');
      }
    }),
  });

  const [responseA, responseB] = await Promise.all([
    harness.request(`/sessions/${empresaA}/connect`, {
      method: 'POST',
      tenantId: empresaA,
    }),
    harness.request(`/sessions/${empresaB}/connect`, {
      method: 'POST',
      tenantId: empresaB,
    }),
  ]);

  assert.equal((await responseA.json()).status, 'Error');
  assert.equal((await responseB.json()).status, 'Connected');
  assert.equal(harness.factory.forTenant(empresaA).destroyCount, 1);
  assert.equal(harness.factory.forTenant(empresaB).destroyCount, 0);
});

async function createHarness(options = {}) {
  const root = await mkdtemp(path.join(os.tmpdir(), 'detara-whatsapp-test-'));
  const factory = new FakeClientFactory(options.initialize, options.create);
  const service = createService(root, factory, options);
  await service.start();
  const app = createApp({ service, apiKey, logger: silentLogger });
  const server = http.createServer(app);
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  const address = server.address();
  const baseUrl = `http://127.0.0.1:${address.port}`;
  cleanups.push(async () => {
    await new Promise((resolve) => server.close(resolve));
    await service.shutdown();
    await rm(root, { recursive: true, force: true });
  });
  return {
    baseUrl,
    factory,
    service,
    request: (route, options = {}) =>
      fetch(`${baseUrl}${route}`, {
        method: options.method ?? 'GET',
        headers: {
          authorization: `Bearer ${apiKey}`,
          'x-detara-tenant-id': options.tenantId ?? empresaA,
          ...(options.body ? { 'content-type': 'application/json' } : {}),
        },
        body: options.body ? JSON.stringify(options.body) : undefined,
      }),
  };
}

function createService(root, factory, options = {}) {
  return new WhatsAppGatewayService({
    clientFactory: factory,
    qrEncoder: async (value) =>
      `data:image/png;base64,${Buffer.from(value).toString('base64')}`,
    registry: new SessionRegistry(root),
    deliveryStore: new DeliveryStore(root),
    logger: options.logger ?? silentLogger,
    connectWaitMs: options.connectWaitMs ?? 200,
  });
}

async function waitForClientCount(factory, empresaId, expectedCount) {
  const deadline = Date.now() + 1_000;
  while (Date.now() < deadline) {
    if (factory.historyForTenant(empresaId).length === expectedCount) return;
    await new Promise((resolve) => setTimeout(resolve, 5));
  }
  assert.equal(factory.historyForTenant(empresaId).length, expectedCount);
}

async function waitForStatus(service, empresaId, expectedStatus) {
  const deadline = Date.now() + 1_000;
  while (Date.now() < deadline) {
    const status = await service.getStatus(empresaId);
    if (status.status === expectedStatus) {
      return status;
    }
    await new Promise((resolve) => setTimeout(resolve, 5));
  }
  return service.getStatus(empresaId);
}

class FakeClientFactory {
  constructor(initialize, create) {
    this.initialize = initialize;
    this.createBehavior = create;
    this.clients = new Map();
    this.created = [];
  }

  create(sessionKey) {
    this.createBehavior?.(sessionKey);
    const client = new FakeClient(sessionKey, this.initialize);
    this.clients.set(sessionKey, client);
    this.created.push(client);
    return client;
  }

  forTenant(empresaId) {
    return this.clients.get(`tenant-${empresaId.replaceAll('-', '')}`);
  }

  historyForTenant(empresaId) {
    const sessionKey = `tenant-${empresaId.replaceAll('-', '')}`;
    return this.created.filter((client) => client.sessionKey === sessionKey);
  }
}

class FakeClient extends EventEmitter {
  constructor(sessionKey, initialize) {
    super();
    this.sessionKey = sessionKey;
    this.initializeBehavior = initialize;
    this.sent = [];
    this.numberLookups = [];
    this.logoutCount = 0;
    this.initializeCount = 0;
    this.destroyCount = 0;
    this.info = { wid: { user: '5541999990000' } };
  }

  async initialize() {
    this.initializeCount += 1;
    return this.initializeBehavior?.(this);
  }

  async getNumberId(phone) {
    this.numberLookups.push(phone);
    return { _serialized: `${phone}@c.us` };
  }

  async sendMessage(chatId, message, options) {
    this.sent.push({ chatId, message, options });
    return { id: { _serialized: `message-${this.sent.length}` } };
  }

  async destroy() {
    this.destroyCount += 1;
  }

  async logout() {
    this.logoutCount += 1;
  }
}

const silentLogger = Object.freeze({
  info() {},
  warn() {},
  error() {},
});
