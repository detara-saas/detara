import { lstat, realpath } from 'node:fs/promises';
import { createRequire } from 'node:module';
import path from 'node:path';
import whatsappWeb from 'whatsapp-web.js';

const require = createRequire(import.meta.url);
// Use the exact Puppeteer resolved by whatsapp-web.js, including its pinned override.
const puppeteer = createRequire(require.resolve('whatsapp-web.js'))('puppeteer');

const { Client, LocalAuth } = whatsappWeb;

// Upstream frame-navigation callbacks call logout/beforeBrowserInitialized outside
// initialize(). They must not remove or recreate a profile after its client retires.
class DetaraLocalAuth extends LocalAuth {
  async beforeBrowserInitialized() {
    if (this.client.stopping) return;
    await super.beforeBrowserInitialized();
  }

  async logout() {
    // A remote event or upstream Client.logout is not our physical-cleanup barrier.
    // Persistent removal belongs only to explicit DELETE after browser exit.
  }

  async removeProfile() {
    await super.logout();
  }
}

export const injectionFailureEvent = 'detara_injection_failure';

const duplicateBindingPattern =
  /Failed to add page binding with name ([A-Za-z0-9_]+):/;

export class InjectionCoordinator {
  constructor(maxAttempts = 3) {
    this.maxAttempts = maxAttempts;
    this.pending = null;
  }

  run(client, inject) {
    if (this.pending) {
      return this.pending;
    }

    const pending = this.runWithRecovery(client, inject).finally(() => {
      if (this.pending === pending) {
        this.pending = null;
      }
    });
    this.pending = pending;
    return pending;
  }

  async runWithRecovery(client, inject) {
    for (let attempt = 1; attempt <= this.maxAttempts; attempt += 1) {
      try {
        return await inject();
      } catch (error) {
        const recovery = classifyInjectionFailure(error);
        if (!recovery) {
          client.emit(injectionFailureEvent, error);
          return undefined;
        }

        if (attempt === this.maxAttempts) {
          client.emit(injectionFailureEvent, error);
          return undefined;
        }

        try {
          await recoverInjection(client, recovery);
        } catch (recoveryError) {
          client.emit(injectionFailureEvent, recoveryError);
          return undefined;
        }
      }
    }

    return undefined;
  }
}

// v1.34.7 may call inject concurrently from its async frame navigation listener.
// Keep the upstream package intact and contain that race at the client boundary.
class DetaraWhatsAppClient extends Client {
  constructor(options) {
    super(options);
    this.injectionCoordinator = new InjectionCoordinator();
    this.stopping = false;
  }

  initialize() {
    this.initializationTask ??= this.initializeOwnedBrowser();
    return this.initializationTask;
  }

  async initializeOwnedBrowser() {
    await validatedProfile(this.authStrategy.dataPath, this.authStrategy.clientId);
    await this.authStrategy.beforeBrowserInitialized();
    if (this.stopping) return;
    const options = this.options.puppeteer;
    this.launchPromise = puppeteer.launch({
      ...options,
      args: [...options.args, `--user-agent=${this.options.userAgent}`,
        '--disable-blink-features=AutomationControlled'],
    }).then((browser) => {
      this.ownedBrowser = browser;
      return browser;
    });
    const browser = await this.launchPromise;
    if (this.stopping) return;
    // Upstream connects to our browser instead of launching an untracked one.
    // This creates a CDP connection/page, not another Chromium process/profile.
    this.options.puppeteer = { ...options, browserWSEndpoint: browser.wsEndpoint() };
    await super.initialize();
  }

  stopInitialization() {
    this.stopping = true;
  }

  async removeLocalAuth() {
    const strategy = this.authStrategy;
    const directory = await validatedProfile(strategy.dataPath, strategy.clientId);
    if (strategy.userDataDir && path.resolve(strategy.userDataDir) !== directory) {
      throw new Error('Profile de sessão inválido.');
    }
    strategy.userDataDir = directory;
    await strategy.removeProfile();
    try {
      await lstat(directory);
    } catch (error) {
      if (error.code === 'ENOENT') return;
      throw error;
    }
    throw new Error('Profile de sessão não foi removido.');
  }

  inject() {
    if (this.stopping) return Promise.resolve();
    return this.injectionCoordinator.run(this, () => super.inject());
  }
}

export class WhatsAppClientFactory {
  constructor({ sessionsPath, chromiumExecutablePath }) {
    this.sessionsPath = sessionsPath;
    this.chromiumExecutablePath = chromiumExecutablePath;
  }

  create(sessionKey) {
    validateSessionKey(sessionKey);
    return new DetaraWhatsAppClient({
      authStrategy: new DetaraLocalAuth({
        clientId: sessionKey,
        dataPath: this.sessionsPath,
      }),
      puppeteer: {
        headless: true,
        executablePath: this.chromiumExecutablePath,
        args: [
          '--no-sandbox',
          '--disable-setuid-sandbox',
          '--disable-dev-shm-usage',
          '--disable-gpu',
        ],
      },
    });
  }
}

function classifyInjectionFailure(error) {
  const message = String(error?.message ?? error ?? '');
  if (message.includes('Execution context was destroyed')) {
    return { type: 'navigation' };
  }

  const bindingMatch = message.match(duplicateBindingPattern);
  if (bindingMatch) {
    return { type: 'duplicate-binding', bindingName: bindingMatch[1] };
  }

  return null;
}

async function recoverInjection(client, recovery) {
  const page = client.pupPage;
  if (!page || page.isClosed?.()) {
    throw new Error('Página do WhatsApp indisponível durante a recuperação.');
  }

  if (recovery.type === 'duplicate-binding') {
    await page.removeExposedFunction(recovery.bindingName);
    return;
  }

  await page.waitForFunction('window.Debug?.VERSION != undefined', {
    timeout: client.options?.authTimeoutMs,
  });
}

function validateSessionKey(sessionKey) {
  if (!/^tenant-[0-9a-f]{32}$/.test(sessionKey)) {
    throw new Error('Chave de sessão WhatsApp inválida.');
  }
}

async function validatedProfile(sessionsPath, sessionKey) {
  validateSessionKey(sessionKey);
  const root = path.resolve(sessionsPath);
  const directory = path.join(root, `session-${sessionKey}`);
  try {
    const stat = await lstat(directory);
    if (stat.isSymbolicLink()) throw new Error('Profile de sessão inválido.');
    const resolvedRoot = await realpath(root);
    if (path.dirname(await realpath(directory)) !== resolvedRoot) {
      throw new Error('Profile fora da raiz de sessões.');
    }
  } catch (error) {
    if (error.code !== 'ENOENT') throw error;
  }
  return directory;
}
