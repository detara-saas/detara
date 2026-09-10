// Synthetic WA module surface: verifies upstream control flow, NOT real WA support.
// Run in an isolated image with --init --network none --read-only.
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { mkdtemp, rm, readFile } from 'node:fs/promises';
import { test } from 'node:test';
import { installSyntheticModules } from './synthetic-wa.mjs';
const require = createRequire(process.env.QA_PACKAGE_ROOT || import.meta.url);
const { Client, LocalAuth } = require('whatsapp-web.js');
const puppeteer = require('puppeteer');
const { LoadUtils } = require('whatsapp-web.js/src/util/Injected/Utils');

async function fixture(t, cache, nativeUa = false) {
  const root = await mkdtemp('/tmp/detara-post-auth-');
  const client = new Client({ authStrategy: new LocalAuth({ dataPath: root }),
    webVersionCache: { type: cache },
    puppeteer: { executablePath: process.env.QA_BROWSER || '/usr/bin/chromium',
      args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-dev-shm-usage', '--disable-gpu'] } });
  await client.authStrategy.beforeBrowserInitialized();
  const browser = await puppeteer.launch(client.options.puppeteer);
  t.after(async () => { await browser.close(); await rm(root, { recursive: true, force: true }); });
  const page = await browser.newPage();
  await page.setUserAgent(nativeUa ? await browser.userAgent() : client.options.userAgent);
  client.pupPage = page;
  client.pupBrowser = browser;
  client.currentIndexHtml = '<!doctype html><title>Synthetic test document</title>';
  await installSyntheticModules(page);
  return { client, page, browser };
}

for (const nativeUa of [false, true]) {
  test(`upstream read-only cache fails before LoadUtils; nativeUA=${nativeUa}`, async (t) => {
    const { client, page } = await fixture(t, 'local', nativeUa);
    let authenticated = 0;
    let ready = 0;
    client.on('authenticated', () => { authenticated += 1; });
    client.on('ready', () => { ready += 1; });
    await client.inject(); // Resolves BEFORE page calls the exposed callback.
    await assert.rejects(page.evaluate(() => window.onAppStateHasSyncedEvent()), /EROFS|EACCES|ENOENT/);
    assert.equal(authenticated, 1);
    assert.equal(ready, 0);
    assert.equal(await page.evaluate(() => typeof window.WWebJS), 'undefined');
  });
  test(`upstream cache none reaches real LoadUtils and listeners; nativeUA=${nativeUa}`, async (t) => {
    const { client, page } = await fixture(t, 'none', nativeUa);
    let ready = 0;
    client.on('ready', () => { ready += 1; });
    await client.inject();
    await page.evaluate(() => window.onAppStateHasSyncedEvent());
    assert.equal(ready, 1);
    assert.ok(client.info);
    assert.deepEqual(await page.evaluate(() => [typeof window.WWebJS.sendMessage,
      typeof window.onAddMessageEvent, typeof window.onIncomingCall]), ['function', 'function', 'function']);
    console.log(JSON.stringify({ browser: await client.pupBrowser.version(),
      nativeUa, cache: 'none', memoryCurrent: (await readFile('/sys/fs/cgroup/memory.current', 'utf8')).trim() }));
  });
}

test('LoadUtils does not require WA modules immediately; navigation replaces WWebJS', async (t) => {
  const { page } = await fixture(t, 'none');
  await page.evaluate(() => { window.require = () => { throw new Error('unexpected immediate require'); }; });
  await page.evaluate(LoadUtils);
  assert.equal(await page.evaluate(() => typeof window.WWebJS.sendMessage), 'function');
  await page.goto('about:blank');
  assert.equal(await page.evaluate(() => typeof window.WWebJS), 'undefined');
});

test('unawaited exposed rejection goes to pageerror, not resolved inject promise', async (t) => {
  const { client, page } = await fixture(t, 'local');
  let pageError;
  page.on('pageerror', (error) => { pageError = error; });
  await client.inject();
  await page.evaluate(() => { window.onAppStateHasSyncedEvent(); });
  const deadline = Date.now() + 3_000;
  while (!pageError && Date.now() < deadline) await new Promise((resolve) => setTimeout(resolve, 20));
  assert.match(pageError?.message ?? '', /EROFS|EACCES|ENOENT/);
  console.log('Observed post-auth error:', pageError.message);
});

test('writable temporary HTML cache also reaches ready without runtime changes', async (t) => {
  const { client, page } = await fixture(t, 'local');
  const cache = await mkdtemp('/tmp/detara-html-cache-');
  t.after(() => rm(cache, { recursive: true, force: true }));
  client.options.webVersionCache.path = cache;
  let ready = false;
  client.on('ready', () => { ready = true; });
  await client.inject();
  await page.evaluate(() => window.onAppStateHasSyncedEvent());
  assert.equal(ready, true);
  assert.equal(await readFile(`${cache}/2.3000.1047198186.html`, 'utf8'), client.currentIndexHtml);
});
