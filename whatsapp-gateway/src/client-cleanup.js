import { GatewayError } from './errors.js';

// Keep timed-out destructive operations attached to the old client across DELETE
// retries. A timeout is not cancellation and must never release a newer profile.
const pendingLogouts = new WeakMap();
const pendingRemovals = new WeakMap();

export function cleanupError() {
  return new GatewayError(503, 'whatsapp_cleanup_pendente',
    'Não foi possível concluir a desconexão. Tente novamente.');
}

export async function bounded(operation, timeoutMs = 5_000) {
  let timer;
  try {
    return await Promise.race([
      Promise.resolve().then(operation),
      new Promise((_, reject) => {
        timer = setTimeout(() => reject(cleanupError()), timeoutMs);
      }),
    ]);
  } finally {
    clearTimeout(timer);
  }
}

function hasExited(child) {
  return child.exitCode !== null || child.signalCode !== null;
}

export function browserConnected(browser) {
  if (!browser) return false;
  // Puppeteer 25 removed isConnected(); whatsapp-web.js 1.34.7 still calls it.
  return typeof browser.isConnected === 'function'
    ? browser.isConnected() : browser.connected;
}

async function waitForExit(child, timeoutMs) {
  if (hasExited(child)) return;
  let listener;
  try {
    await bounded(() => new Promise((resolve) => {
      listener = resolve;
      child.once('exit', listener);
      if (hasExited(child)) resolve();
    }), timeoutMs);
  } finally {
    if (listener) child.off('exit', listener);
  }
}

// The owned handle is captured immediately after launch, before upstream page setup.
// Never find processes by tenant, executable name or a global process scan.
export async function cleanupClient(client, {
  empresaId, logger, logout = false, timeoutMs = 5_000,
}) {
  const warn = (stage, error) => logger.warn('Fallback de cleanup WhatsApp utilizado.', {
    empresaId, stage, errorType: error?.name ?? 'Error',
  });
  logger.info('Cleanup de Client WhatsApp iniciado.', { empresaId });
  client.stopInitialization?.();

  // A late launch must settle before we inspect its handle or release the profile.
  if (client.launchPromise) {
    await bounded(() => client.launchPromise.catch(() => {}), timeoutMs);
  }
  const browser = client.ownedBrowser ?? client.pupBrowser;
  const child = browser?.process?.();
  let logoutCompleted = false;
  let logoutTask;
  if (logout) {
    try {
      logoutTask = pendingLogouts.get(client);
      if (!logoutTask) {
        logoutTask = Promise.resolve().then(() => client.logout());
        pendingLogouts.set(client, logoutTask);
      }
      await bounded(() => logoutTask, timeoutMs);
      logoutCompleted = true;
    } catch (error) {
      warn('client.logout', error);
    }
  }
  try {
    await bounded(() => client.destroy(), timeoutMs);
  } catch (error) {
    warn('client.destroy', error);
  }

  if (browser && ((child && !hasExited(child)) || browserConnected(browser))) {
    try {
      await bounded(() => browser.close(), timeoutMs);
    } catch (error) {
      warn('browser.close', error);
    }
  }
  if (child && !hasExited(child)) {
    warn('process.SIGTERM', new Error());
    child.kill('SIGTERM');
    try {
      await waitForExit(child, timeoutMs);
    } catch {
      warn('process.SIGKILL', new Error());
      // Puppeteer launches owned Chromium in a detached process group on Linux.
      // Only that captured group is eligible; foreign/connected browser PIDs are not.
      if (!hasExited(child)) {
        try {
          if (process.platform === 'linux' && browser === client.ownedBrowser) {
            process.kill(-child.pid, 'SIGKILL');
          } else {
            child.kill('SIGKILL');
          }
        } catch (error) {
          // Exit can race the signal. Still require the captured exit below.
          if (error.code !== 'ESRCH') throw error;
        }
      }
      await waitForExit(child, timeoutMs);
    }
  }
  if (child) await waitForExit(child, timeoutMs);
  if (browserConnected(browser)) await bounded(() => browser.disconnect(), timeoutMs);
  if (browserConnected(browser) || (child && !hasExited(child))) throw cleanupError();

  // Upstream initialize may still be unwinding after its page/browser was closed.
  if (client.initializationTask) {
    await bounded(() => client.initializationTask.catch(() => {}), timeoutMs);
  }
  if (logout) {
    // A timed-out logout must not delete the profile of a subsequent generation.
    if (logoutTask) await bounded(() => logoutTask.catch(() => {}), timeoutMs);
    if (client.removeLocalAuth) {
      let removal = pendingRemovals.get(client);
      if (!removal) {
        removal = Promise.resolve().then(() => client.removeLocalAuth());
        pendingRemovals.set(client, removal);
        void removal.catch(() => {
          if (pendingRemovals.get(client) === removal) pendingRemovals.delete(client);
        });
      }
      await bounded(() => removal, timeoutMs);
    } else if (!logoutCompleted) {
      throw cleanupError();
    }
    logger.info('LocalAuth removido por logout explícito.', { empresaId });
  }
  logger.info('Cleanup de Client WhatsApp concluído.', { empresaId });
}
