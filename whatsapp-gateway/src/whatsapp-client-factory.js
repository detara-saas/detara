import { rmSync } from 'node:fs';
import path from 'node:path';
import whatsappWeb from 'whatsapp-web.js';

const { Client, LocalAuth } = whatsappWeb;

export class WhatsAppClientFactory {
  constructor({ sessionsPath, chromiumExecutablePath }) {
    this.sessionsPath = sessionsPath;
    this.chromiumExecutablePath = chromiumExecutablePath;
  }

  create(sessionKey) {
    removeStaleChromiumLocks(this.sessionsPath, sessionKey);
    return new Client({
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
