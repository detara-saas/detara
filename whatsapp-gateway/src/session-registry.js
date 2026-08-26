import path from 'node:path';
import { JsonStore } from './json-store.js';
import { normalizeTenantId } from './validation.js';

const validStatuses = new Set([
  'Disconnected',
  'Connecting',
  'WaitingQRCode',
  'Connected',
  'Error',
  'Reconnecting',
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
        this.sessions.set(empresaId, Object.freeze({
          ...session,
          empresaId,
          phoneNumber: normalizeStoredPhone(session?.phoneNumber),
        }));
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
    const normalized = Object.freeze({
      ...session,
      phoneNumber: normalizeStoredPhone(session?.phoneNumber),
    });
    this.sessions.set(normalized.empresaId, normalized);
    await this.store.write({ version: 1, sessions: this.list() });
    return normalized;
  }

  async remove(empresaId) {
    this.sessions.delete(empresaId);
    await this.store.write({ version: 1, sessions: this.list() });
  }
}

export function createSessionKey(empresaId) {
  return `tenant-${empresaId.replaceAll('-', '')}`;
}

function normalizeStoredPhone(value) {
  if (typeof value !== 'string') return null;
  const digits = value.replace(/\D/g, '');
  return /^\d{8,15}$/.test(digits) ? digits : null;
}
