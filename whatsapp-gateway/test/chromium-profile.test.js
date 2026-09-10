import assert from 'node:assert/strict';
import { access, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { afterEach, test } from 'node:test';
import { cleanupStaleChromiumSingletons } from '../src/chromium-profile.js';
import { createLogger } from '../src/logger.js';

const empresaId = '11111111-1111-4111-8111-111111111111';
const singletonFiles = ['SingletonLock', 'SingletonCookie', 'SingletonSocket'];
const cleanups = [];

afterEach(async () => {
  await Promise.allSettled(cleanups.splice(0).map((cleanup) => cleanup()));
});

test('restore remove somente singletons obsoletos e preserva LocalAuth e stores', async () => {
  const fixture = await profileFixture();
  await addSyntheticChromiumProcess(
    fixture.processRoot,
    '41',
    path.join(fixture.root, 'outro-profile'),
  );

  const result = await cleanupStaleChromiumSingletons({
    profileDirectory: fixture.profile,
    empresaId,
    logger: fixture.logger,
    processRoot: fixture.processRoot,
  });

  assert.deepEqual(result, { removed: singletonFiles, skipped: null });
  for (const file of singletonFiles) {
    await assert.rejects(access(path.join(fixture.profile, file)), { code: 'ENOENT' });
  }
  assert.equal(
    await readFile(path.join(fixture.profile, 'Default', 'Preferences'), 'utf8'),
    'local-auth-preservado',
  );
  assert.equal(await readFile(path.join(fixture.root, 'registry.json'), 'utf8'), 'registry');
  assert.equal(await readFile(path.join(fixture.root, 'deliveries.json'), 'utf8'), 'deliveries');
  assert.deepEqual(
    fixture.entries.filter((entry) => entry.message === 'Stale Chromium singleton removed.')
      .map((entry) => entry.file),
    singletonFiles,
  );

  const second = await cleanupStaleChromiumSingletons({
    profileDirectory: fixture.profile,
    empresaId,
    logger: fixture.logger,
    processRoot: fixture.processRoot,
  });
  assert.deepEqual(second, { removed: [], skipped: null });
});

test('processo Chromium ativo no profile exato impede remoção dos singletons', async () => {
  const fixture = await profileFixture();
  await addSyntheticChromiumProcess(fixture.processRoot, '42', fixture.profile);

  const result = await cleanupStaleChromiumSingletons({
    profileDirectory: fixture.profile,
    empresaId,
    logger: fixture.logger,
    processRoot: fixture.processRoot,
  });

  assert.deepEqual(result, { removed: [], skipped: 'active' });
  for (const file of singletonFiles) await access(path.join(fixture.profile, file));
  await access(path.join(fixture.profile, 'Default', 'Preferences'));
  assert.ok(fixture.entries.some((entry) =>
    entry.message
      === 'Chromium profile is currently active; stale singleton cleanup skipped.'
    && entry.empresaId === empresaId));
});

async function profileFixture() {
  const root = await mkdtemp(path.join(os.tmpdir(), 'detara-profile-restore-'));
  cleanups.push(() => rm(root, { recursive: true, force: true }));
  const profile = path.join(root, 'session-tenant-11111111111141118111111111111111');
  const processRoot = path.join(root, 'proc');
  await mkdir(path.join(profile, 'Default'), { recursive: true });
  await mkdir(processRoot);
  await Promise.all([
    writeFile(path.join(profile, 'SingletonLock'), 'old-container-20'),
    writeFile(path.join(profile, 'SingletonCookie'), 'stale-cookie-socket-name'),
    writeFile(path.join(profile, 'SingletonSocket'), 'stale-socket'),
    writeFile(path.join(profile, 'Default', 'Preferences'), 'local-auth-preservado'),
    writeFile(path.join(root, 'registry.json'), 'registry'),
    writeFile(path.join(root, 'deliveries.json'), 'deliveries'),
  ]);
  const entries = [];
  const logger = createLogger({
    log: (entry) => entries.push(JSON.parse(entry)),
    warn: (entry) => entries.push(JSON.parse(entry)),
    error: (entry) => entries.push(JSON.parse(entry)),
  });
  return { root, profile, processRoot, logger, entries };
}

async function addSyntheticChromiumProcess(processRoot, pid, profile) {
  const processDirectory = path.join(processRoot, pid);
  await mkdir(processDirectory);
  await writeFile(path.join(processDirectory, 'comm'), 'chromium\n');
  await writeFile(
    path.join(processDirectory, 'cmdline'),
    Buffer.from(`/usr/bin/chromium\0--user-data-dir=${profile}\0--headless\0`),
  );
}
