import { rmSync } from 'node:fs';
import path from 'node:path';
import whatsappWeb from 'whatsapp-web.js';

const { Client, LocalAuth } = whatsappWeb;

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
  }

  inject() {
    return this.injectionCoordinator.run(this, () => super.inject());
  }
}

export class WhatsAppClientFactory {
  constructor({ sessionsPath, chromiumExecutablePath }) {
    this.sessionsPath = sessionsPath;
    this.chromiumExecutablePath = chromiumExecutablePath;
  }

  create(sessionKey) {
    removeStaleChromiumLocks(this.sessionsPath, sessionKey);
    return new DetaraWhatsAppClient({
      authStrategy: new LocalAuth({
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

export function removeStaleChromiumLocks(sessionsPath, sessionKey) {
  if (!/^tenant-[0-9a-f]{32}$/.test(sessionKey)) {
    throw new Error('Chave de sessão WhatsApp inválida.');
  }
  const sessionDirectory = path.join(sessionsPath, `session-${sessionKey}`);
  for (const fileName of ['SingletonLock', 'SingletonSocket', 'SingletonCookie']) {
    rmSync(path.join(sessionDirectory, fileName), {
      force: true,
      maxRetries: 2,
      recursive: false,
      retryDelay: 50,
    });
  }
}
