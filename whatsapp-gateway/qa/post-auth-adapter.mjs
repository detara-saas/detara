import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { mkdtemp, rm, access, readdir, readFile } from 'node:fs/promises';
import { test } from 'node:test';
import { WhatsAppClientFactory, injectionFailureEvent } from '../src/whatsapp-client-factory.js';
import { cleanupClient } from '../src/client-cleanup.js';
import { postAuthStageEvent } from '../src/post-auth.js';
import { installSyntheticModules } from './synthetic-wa.mjs';
import { readPostAuthProbe } from './post-auth-probe.mjs';
const require = createRequire(import.meta.url);
const puppeteer = require('puppeteer');
const { LoadUtils } = require('whatsapp-web.js/src/util/Injected/Utils');
const { ExposeAuthStore } = require('whatsapp-web.js/src/util/Injected/AuthStore/AuthStore');
const logger = { info() {}, warn() {}, error() {} };

async function fixture(t) {
  const root = await mkdtemp('/tmp/detara-adapter-auth-');
  const client = new WhatsAppClientFactory({ sessionsPath: root,
    chromiumExecutablePath: '/usr/bin/chromium' }).create('tenant-11111111111141118111111111111111');
  await client.authStrategy.beforeBrowserInitialized();
  const browser = await puppeteer.launch(client.options.puppeteer);
  client.ownedBrowser = browser;
  client.pupBrowser = browser;
  client.pupPage = await browser.newPage();
  const page = client.pupPage;
  t.after(async () => { await cleanupClient(client, { logger }); await rm(root, { recursive: true, force: true }); });
  await installSyntheticModules(page);
  client.currentIndexHtml = '<html>Synthetic</html>';
  const failures = [];
  let ready = 0;
  client.on(injectionFailureEvent, (error) => failures.push(error));
  client.on('ready', () => { ready += 1; });
  await client.inject();
  return { client, page, failures, ready: () => ready };
}

test('adapter real: read-only, duas chamadas hasSynced, ready único e listeners upstream', async (t) => {
  const h = await fixture(t);
  const stages = [];
  h.client.on(postAuthStageEvent, (stage) => stages.push(stage));
  await h.page.evaluate(() => Promise.all([window.onAppStateHasSyncedEvent(), window.onAppStateHasSyncedEvent()]));
  assert.equal(h.ready(), 1);
  assert.equal(h.failures.length, 0);
  assert.ok(stages.includes('POST_AUTH_LISTENERS_DONE'));
  assert.equal(await h.page.evaluate(() => typeof window.onIncomingCall), 'function');
  const probe = await readPostAuthProbe(h.page);
  assert.equal(probe.hasWWebJS, true);
  assert.equal(probe.bindings.onAddMessageEvent, true);
  assert.equal(probe.page, 'local-or-other');
  assert.doesNotMatch(JSON.stringify(probe), /wid|cookie|phone|token|title/i);
  let browserCount = 0;
  for (const pid of await readdir('/proc')) {
    if (!/^\d+$/.test(pid)) continue;
    try {
      const args = (await readFile(`/proc/${pid}/cmdline`, 'utf8')).split('\0');
      if (args.includes(`--user-data-dir=${h.client.authStrategy.userDataDir}`)
        && !args.some((arg) => arg.startsWith('--type='))) browserCount += 1;
    } catch (error) {
      if (!['ENOENT', 'ESRCH'].includes(error.code)) throw error;
    }
  }
  assert.equal(browserCount, 1);
  const memoryEvents = await readFile('/sys/fs/cgroup/memory.events', 'utf8');
  assert.match(memoryEvents, /^oom 0$/m);
  assert.match(memoryEvents, /^oom_kill 0$/m);
  console.log(JSON.stringify({ browserCount, memoryEvents,
    memoryCurrent: (await readFile('/sys/fs/cgroup/memory.current', 'utf8')).trim() }));
  await h.client.inject();
  await h.page.evaluate(() => window.onAppStateHasSyncedEvent());
  assert.equal(h.ready(), 1);
});

test('adapter real: contexto destruído durante evaluate recupera no mesmo browser', async (t) => {
  const h = await fixture(t);
  const browser = h.client.ownedBrowser;
  const evaluate = h.page.evaluate.bind(h.page);
  let first = true;
  h.page.evaluate = async (fn, ...args) => {
    if (fn === LoadUtils && first) {
      first = false;
      const pending = evaluate(() => new Promise(() => {}));
      void pending.catch(() => {});
      await h.page.goto('about:blank');
      await installSyntheticModules(h.page);
      await evaluate(ExposeAuthStore);
      return pending;
    }
    return evaluate(fn, ...args);
  };
  // Invoke Node callback directly: the browser call's own document is destroyed.
  await h.client.postAuth.run();
  assert.equal(h.ready(), 1);
  assert.equal(h.failures.length, 0);
  assert.equal(h.client.ownedBrowser, browser);
});

test('adapter real: navegação após ready prepara novo documento sem reutilizar listeners antigos', async (t) => {
  const h = await fixture(t);
  await h.client.postAuth.run();
  await h.page.goto('about:blank');
  await installSyntheticModules(h.page);
  await h.client.inject();
  await h.page.evaluate(() => window.onAppStateHasSyncedEvent());
  await h.page.evaluate(() => window.onAppStateHasSyncedEvent());
  assert.equal(h.ready(), 2); // Once for each prepared document.
  assert.equal(h.failures.length, 0);
  assert.equal((await readPostAuthProbe(h.page)).bindings.onAddMessageEvent, true);
});

test('adapter real: navegação depois de listeners não publica ready antigo', async (t) => {
  const h = await fixture(t);
  const attach = h.client.attachEventListeners.bind(h.client);
  h.client.attachEventListeners = async () => { await attach(); await h.page.goto('about:blank'); };
  await h.client.postAuth.run();
  assert.equal(h.ready(), 0);
  assert.equal(h.failures[0].name, 'ContextChanged');
  await access(h.client.authStrategy.userDataDir);
});

test('adapter real: falha LoadUtils não escapa como pageerror e preserva profile', async (t) => {
  const h = await fixture(t);
  const evaluate = h.page.evaluate.bind(h.page);
  h.page.evaluate = (fn, ...args) => fn === LoadUtils
    ? Promise.reject(new Error('synthetic LoadUtils failure')) : evaluate(fn, ...args);
  let pageErrors = 0;
  h.page.on('pageerror', () => { pageErrors += 1; });
  await h.page.evaluate(() => window.onAppStateHasSyncedEvent());
  assert.equal(h.ready(), 0);
  assert.equal(h.failures[0].postAuthStage, 'POST_AUTH_LOAD_UTILS_STARTED');
  assert.equal(pageErrors, 0);
  await access(h.client.authStrategy.userDataDir);
});

test('adapter real: reparo de binding na mesma Page mantém boundary pós-auth', async (t) => {
  const h = await fixture(t);
  await h.page.removeExposedFunction('onAppStateHasSyncedEvent');
  await h.client.inject();
  const evaluate = h.page.evaluate.bind(h.page);
  h.page.evaluate = (fn, ...args) => fn === LoadUtils
    ? Promise.reject(new Error('synthetic LoadUtils failure')) : evaluate(fn, ...args);
  await h.page.evaluate(() => window.onAppStateHasSyncedEvent());
  assert.equal(h.ready(), 0);
  assert.equal(h.failures[0].postAuthStage, 'POST_AUTH_LOAD_UTILS_STARTED');
});
