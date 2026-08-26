import path from 'node:path';
import { JsonStore } from './json-store.js';

const maximumEntries = 10_000;

export class DeliveryStore {
  constructor(sessionsPath) {
    this.store = new JsonStore(path.join(sessionsPath, 'deliveries.json'), {
      version: 1,
      deliveries: [],
    });
    this.deliveries = new Map();
  }

  async load() {
    const content = await this.store.read();
    const deliveries = Array.isArray(content?.deliveries)
      ? content.deliveries
      : [];
    this.deliveries.clear();
    for (const delivery of deliveries.slice(-maximumEntries)) {
      if (
        typeof delivery?.key === 'string' &&
        typeof delivery?.empresaId === 'string' &&
        ['InProgress', 'Sent'].includes(delivery?.status)
      ) {
        this.deliveries.set(this.composite(delivery.empresaId, delivery.key), {
          ...delivery,
        });
      }
    }
  }

  get(empresaId, key) {
    return this.deliveries.get(this.composite(empresaId, key)) ?? null;
  }

  async begin(empresaId, key, now) {
    const existing = this.get(empresaId, key);
    if (existing) {
      return existing;
    }
    const entry = {
      empresaId,
      key,
      status: 'InProgress',
      createdAt: now,
      sentAt: null,
      messageId: null,
    };
    this.deliveries.set(this.composite(empresaId, key), entry);
    await this.persist();
    return entry;
  }

  async complete(empresaId, key, messageId, now) {
    const entry = this.get(empresaId, key);
    if (!entry) {
      throw new Error('Registro de idempotência não encontrado.');
    }
    entry.status = 'Sent';
    entry.sentAt = now;
    entry.messageId = messageId;
    await this.persist();
    return { ...entry };
  }

  async persist() {
    const values = [...this.deliveries.values()]
      .sort((left, right) => left.createdAt.localeCompare(right.createdAt))
      .slice(-maximumEntries);
    this.deliveries = new Map(
      values.map((entry) => [this.composite(entry.empresaId, entry.key), entry]),
    );
    await this.store.write({ version: 1, deliveries: values });
  }

  composite(empresaId, key) {
    return `${empresaId}:${key}`;
  }
}
