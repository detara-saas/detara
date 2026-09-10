import { lstat, readFile, readdir, unlink } from 'node:fs/promises';
import path from 'node:path';

const singletonFiles = Object.freeze([
  'SingletonLock',
  'SingletonCookie',
  'SingletonSocket',
]);
const chromiumProcessName = /(?:^|\/)(?:chrome|chromium)(?:-browser)?$/i;

export async function cleanupStaleChromiumSingletons({
  profileDirectory,
  empresaId,
  logger,
  processRoot = '/proc',
}) {
  const profile = path.resolve(profileDirectory);
  const profileStat = await lstat(profile);
  if (!profileStat.isDirectory() || profileStat.isSymbolicLink()) {
    throw new Error('Chromium profile directory is invalid.');
  }

  const inspection = await inspectChromiumProfileUsage(profile, processRoot);
  if (inspection.active) {
    logger?.warn(
      'Chromium profile is currently active; stale singleton cleanup skipped.',
      { empresaId },
    );
    return { removed: [], skipped: 'active' };
  }
  if (!inspection.complete) {
    logger?.warn(
      'Chromium process inspection incomplete; stale singleton cleanup skipped.',
      { empresaId },
    );
    return { removed: [], skipped: 'inspection-incomplete' };
  }

  const removed = [];
  for (const file of singletonFiles) {
    const entry = path.join(profile, file);
    try {
      const stat = await lstat(entry);
      if (stat.isDirectory() && !stat.isSymbolicLink()) {
        throw new Error('Chromium singleton entry has unexpected type.');
      }
      await unlink(entry);
      removed.push(file);
      logger?.info('Stale Chromium singleton removed.', { empresaId, file });
    } catch (error) {
      if (error?.code !== 'ENOENT') throw error;
    }
  }
  return { removed, skipped: null };
}

export async function inspectChromiumProfileUsage(profileDirectory, processRoot = '/proc') {
  const profile = path.resolve(profileDirectory);
  let entries;
  try {
    entries = await readdir(processRoot, { withFileTypes: true });
  } catch {
    return { active: false, complete: false };
  }

  let complete = true;
  for (const entry of entries) {
    if (!entry.isDirectory() || !/^\d+$/.test(entry.name)) continue;
    const processDirectory = path.join(processRoot, entry.name);
    try {
      const comm = await readFile(path.join(processDirectory, 'comm'), 'utf8');
      if (!chromiumProcessName.test(comm.trim())) continue;
      const rawCommandLine = await readFile(path.join(processDirectory, 'cmdline'));
      const args = rawCommandLine.toString('utf8').split('\0').filter(Boolean);
      const executable = path.basename(args[0] ?? comm.trim());
      if (!chromiumProcessName.test(executable)) continue;
      const userDataDirectory = readUserDataDirectory(args);
      if (userDataDirectory && path.resolve(userDataDirectory) === profile) {
        return { active: true, complete: true };
      }
    } catch (error) {
      if (!['ENOENT', 'ESRCH'].includes(error?.code)) complete = false;
    }
  }
  return { active: false, complete };
}

function readUserDataDirectory(args) {
  for (let index = 0; index < args.length; index += 1) {
    const argument = args[index];
    if (argument.startsWith('--user-data-dir=')) {
      return argument.slice('--user-data-dir='.length);
    }
    if (argument === '--user-data-dir') return args[index + 1] ?? null;
  }
  return null;
}
