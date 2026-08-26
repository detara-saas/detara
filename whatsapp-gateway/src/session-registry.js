import path from 'node:path';
import { JsonStore } from './json-store.js';
import { normalizeTenantId } from './validation.js';

const validStatuses = new Set([
  'Disconnected',
  'WaitingQRCode',
  'Connected',
  'Error',
]);

export class SessionRegistry {
  constructor(sessionsPath) {
    this.store = new JsonStore(path.join(sessionsPath, 'registry.json'), {
      version: 1,
      sessions: [],
    });
    this.sessions = new Map();
  }

  async load() {
    const content = await this.store.read();
    const sessions = Array.isArray(content?.sessions) ? content.sessions : [];
    this.sessions.clear();
    for (const session of sessions) {
      let empresaId;
      try {
        empresaId = normalizeTenantId(session?.empresaId);
      } catch {
        continue;
      }
      if (
        typeof session?.sessionKey === 'string' &&
        session.sessionKey === createSessionKey(empresaId) &&
        validStatuses.has(session?.status)
      ) {
        this.sessions.set(empresaId, Object.freeze({ ...session, empresaId }));
      }
    }
    return this.list();
  }

  get(empresaId) {
    return this.sessions.get(empresaId) ?? null;
  }

  list() {
    return [...this.sessions.values()];
  }

  async upsert(session) {
    const normalized = Object.freeze({ ...session });
    this.sessions.set(normalized.empresaId, normalized);
    await this.store.write({ version: 1, sessions: this.list() });
    return normalized;
  }
}

export function createSessionKey(empresaId) {
  return `tenant-${empresaId.replaceAll('-', '')}`;
}
