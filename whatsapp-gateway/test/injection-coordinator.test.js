import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { mkdtemp, rm } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { afterEach, test } from 'node:test';
import {
  InjectionCoordinator,
  WhatsAppClientFactory,
  injectionFailureEvent,
} from '../src/whatsapp-client-factory.js';

const cleanups = [];

afterEach(async () => {
  await Promise.allSettled(cleanups.splice(0).map((cleanup) => cleanup()));
});

test('coordenador compartilha uma única reinjeção concorrente', async () => {
  const coordinator = new InjectionCoordinator();
  const client = createClientDouble();
  let complete;
  let calls = 0;
  const operation = () => {
    calls += 1;
    return new Promise((resolve) => {
      complete = resolve;
    });
  };

  const first = coordinator.run(client, operation);
  const second = coordinator.run(client, operation);
  const third = coordinator.run(client, operation);

  assert.equal(calls, 1);
  complete('ready');
  assert.deepEqual(await Promise.all([first, second, third]), [
    'ready',
    'ready',
    'ready',
  ]);
});

test('erro de navegação aguarda novo contexto e reinjeta de forma limitada', async () => {
  const coordinator = new InjectionCoordinator();
  const client = createClientDouble();
  let calls = 0;

  const result = await coordinator.run(client, async () => {
    calls += 1;
    if (calls === 1) {
      throw new Error(
        'Execution context was destroyed, most likely because of a navigation.',
      );
    }
    return 'injetado';
  });

  assert.equal(result, 'injetado');
  assert.equal(calls, 2);
  assert.equal(client.pageWaitCount, 1);
  assert.equal(client.failures.length, 0);
});

test('binding Puppeteer duplicado é removido pela API antes da reinjeção', async () => {
  const coordinator = new InjectionCoordinator();
  const client = createClientDouble();
  let calls = 0;

  await coordinator.run(client, async () => {
    calls += 1;
    if (calls === 1) {
      throw new Error(
        "Failed to add page binding with name onQRChangedEvent: window['onQRChangedEvent'] already exists!",
      );
    }
  });

  assert.equal(calls, 2);
  assert.deepEqual(client.removedBindings, ['onQRChangedEvent']);
  assert.equal(client.failures.length, 0);
});

test('falha recuperável esgotada vira evento controlado e não rejeição global', async () => {
  const coordinator = new InjectionCoordinator(3);
  const client = createClientDouble();
  let calls = 0;

  const result = await coordinator.run(client, async () => {
    calls += 1;
    throw new Error(
      'Execution context was destroyed, most likely because of a navigation.',
    );
  });

  assert.equal(result, undefined);
  assert.equal(calls, 3);
  assert.equal(client.pageWaitCount, 2);
  assert.equal(client.failures.length, 1);
});

test('erro não classificado também é entregue ao boundary controlado', async () => {
  const coordinator = new InjectionCoordinator();
  const client = createClientDouble();

  const result = await coordinator.run(client, async () => {
    throw new TypeError('falha não transitória');
  });

  assert.equal(result, undefined);
  assert.equal(client.failures.length, 1);
  assert.equal(client.failures[0].message, 'falha não transitória');
});

test('factory mantém LocalAuth isolado e Chromium explicitamente configurado', async () => {
  const sessionsPath = await mkdtemp(path.join(os.tmpdir(), 'detara-factory-'));
  cleanups.push(() => rm(sessionsPath, { recursive: true, force: true }));
  const sessionKey = 'tenant-11111111111141118111111111111111';
  const factory = new WhatsAppClientFactory({
    sessionsPath,
    chromiumExecutablePath: '/usr/bin/chromium',
  });

  const client = factory.create(sessionKey);

  assert.equal(client.options.authStrategy.clientId, sessionKey);
  assert.equal(client.options.authStrategy.dataPath, sessionsPath);
  assert.equal(client.options.puppeteer.executablePath, '/usr/bin/chromium');
  assert.equal(client.options.puppeteer.headless, true);
  assert.ok(client.options.puppeteer.args.includes('--disable-dev-shm-usage'));
  assert.equal(
    client.options.userAgent,
    'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_14_0) '
      + 'AppleWebKit/537.36 (KHTML, like Gecko) '
      + 'Chrome/101.0.4951.67 Safari/537.36',
  );
});

function createClientDouble() {
  const client = new EventEmitter();
  client.options = { authTimeoutMs: 30_000 };
  client.pageWaitCount = 0;
  client.removedBindings = [];
  client.failures = [];
  client.pupPage = {
    isClosed: () => false,
    waitForFunction: async () => {
      client.pageWaitCount += 1;
    },
    removeExposedFunction: async (name) => {
      client.removedBindings.push(name);
    },
  };
  client.on(injectionFailureEvent, (error) => client.failures.push(error));
  return client;
}
