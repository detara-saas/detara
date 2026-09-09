import { randomUUID } from 'node:crypto';
import { ConflictError } from './errors.js';
import { createSessionKey } from './session-registry.js';
import { injectionFailureEvent } from './whatsapp-client-factory.js';

const disconnectedStatus = Object.freeze({
  status: 'Disconnected',
  qrCode: null,
  createdAt: null,
  updatedAt: null,
  lastConnectedAt: null,
  phoneNumber: null,
});

export class WhatsAppGatewayService {
  constructor({
    clientFactory,
    qrEncoder,
    registry,
    deliveryStore,
    logger,
    connectWaitMs,
    now = () => new Date().toISOString(),
  }) {
    this.clientFactory = clientFactory;
    this.qrEncoder = qrEncoder;
    this.registry = registry;
    this.deliveryStore = deliveryStore;
    this.logger = logger;
    this.connectWaitMs = connectWaitMs;
    this.now = now;
    this.contexts = new Map();
    this.pendingEvents = new Set();
    this.destroyedClients = new WeakSet();
  }

  async start() {
    const sessions = await this.registry.load();
    await this.deliveryStore.load();
    for (const session of sessions) {
      const restoring = await this.registry.upsert({
        ...session,
        status: 'Reconnecting',
        updatedAt: this.now(),
      });
      const context = this.ensureContext(session.empresaId, restoring);
      void this.initialize(context);
    }
  }

  async connect(empresaId) {
    let metadata = this.registry.get(empresaId);
    if (!metadata) {
      const timestamp = this.now();
      metadata = await this.registry.upsert({
        id: randomUUID(),
        empresaId,
        sessionKey: createSessionKey(empresaId),
        status: 'Disconnected',
        createdAt: timestamp,
        updatedAt: timestamp,
        lastConnectedAt: null,
        phoneNumber: null,
      });
      this.logger.info('Conexão WhatsApp criada.', { empresaId });
    }

    const context = this.ensureContext(empresaId, metadata);
    if (context.metadata.status === 'Connected') {
      return this.toPublicStatus(context);
    }

    if (context.metadata.status === 'WaitingQRCode') {
      return this.toPublicStatus(context);
    }

    const client = context.client;
    const generation = context.generation;
    if (!['Connecting', 'Reconnecting'].includes(context.metadata.status)) {
      await this.updateStatus(
        context,
        'Connecting',
        undefined,
        undefined,
        client,
        generation,
      );
    }
    const statusChanged = this.waitForStatus(context);
    void this.initialize(context);
    await statusChanged;
    return this.toPublicStatus(context);
  }

  async disconnect(empresaId) {
    const context = this.contexts.get(empresaId);
    if (context) {
      context.closing = true;
      this.contexts.delete(empresaId);
      this.settleInitialization(context);
      this.resolveWaiters(context);
      const client = context.client;
      context.client = null;
      context.generation += 1;
      this.unbindEvents(context);
      await context.cleanupPromise;
      if (client) {
        await this.destroyClient(client, empresaId, true);
      }
    }
    await this.registry.remove(empresaId);
    this.logger.info('Sessão WhatsApp desconectada por solicitação.', { empresaId });
    return disconnectedStatus;
  }

  async getStatus(empresaId) {
    const context = this.contexts.get(empresaId);
    if (context) {
      return this.toPublicStatus(context);
    }
    const metadata = this.registry.get(empresaId);
    if (!metadata) {
      return disconnectedStatus;
    }
    return toPublicMetadata(metadata);
  }

  async sendMessage({ empresaId, phone, message, idempotencyKey }) {
    const context = this.contexts.get(empresaId);
    if (!context || context.metadata.status !== 'Connected') {
      throw new ConflictError(
        'whatsapp_nao_conectado',
        'O WhatsApp da empresa não está conectado.',
      );
    }

    const existing = this.deliveryStore.get(empresaId, idempotencyKey);
    if (existing?.status === 'Sent') {
      return {
        messageId: existing.messageId,
        sentAt: existing.sentAt,
        reused: true,
      };
    }
    if (existing?.status === 'InProgress') {
      throw new ConflictError(
        'envio_estado_incerto',
        'O envio anterior possui estado incerto e não será repetido automaticamente.',
      );
    }

    const registeredId = await context.client.getNumberId(phone);
    const chatId = registeredId?._serialized;
    if (typeof chatId !== 'string' || !/^\d+@(c\.us|lid)$/.test(chatId)) {
      throw new ConflictError(
        'telefone_nao_registrado',
        'O telefone informado não possui uma conta WhatsApp válida.',
      );
    }

    await this.deliveryStore.begin(empresaId, idempotencyKey, this.now());
    this.logger.info('Envio WhatsApp iniciado.', { empresaId });
    try {
      const sent = await context.client.sendMessage(chatId, message, {
        sendSeen: false,
      });
      const messageId =
        sent?.id?._serialized ?? sent?.id?.id ?? `accepted-${randomUUID()}`;
      const completed = await this.deliveryStore.complete(
        empresaId,
        idempotencyKey,
        messageId,
        this.now(),
      );
      this.logger.info('Envio WhatsApp concluído.', { empresaId });
      return {
        messageId: completed.messageId,
        sentAt: completed.sentAt,
        reused: false,
      };
    } catch (error) {
      this.logger.error('Falha no envio WhatsApp; reenvio automático bloqueado.', {
        empresaId,
        errorType: error?.name ?? 'Error',
      });
      throw new ConflictError(
        'envio_estado_incerto',
        'Não foi possível confirmar o envio. A mensagem não será repetida automaticamente.',
      );
    }
  }

  async shutdown() {
    const contexts = [...this.contexts.values()];
    for (const context of contexts) {
      context.closing = true;
      this.settleInitialization(context);
      this.resolveWaiters(context);
      context.generation += 1;
      this.unbindEvents(context);
    }
    await Promise.allSettled(contexts.map(async (context) => {
      await context.cleanupPromise;
      if (context.client) {
        await this.destroyClient(context.client, context.empresaId, false);
      }
    }));
    await Promise.allSettled([...this.pendingEvents]);
    this.contexts.clear();
  }

  ensureContext(empresaId, metadata) {
    const existing = this.contexts.get(empresaId);
    if (existing) {
      return existing;
    }

    const context = {
      empresaId,
      metadata,
      client: null,
      generation: 0,
      qrCode: null,
      initializationPromise: null,
      settleInitialization: null,
      cleanupPromise: Promise.resolve(),
      statusPromise: Promise.resolve(),
      listeners: [],
      waiters: new Set(),
      authenticated: false,
      closing: false,
    };
    this.contexts.set(empresaId, context);
    return context;
  }

  async prepareClient(context) {
    await context.cleanupPromise;
    if (context.closing || this.contexts.get(context.empresaId) !== context) {
      return { client: null, generation: context.generation };
    }
    if (!context.client) {
      context.client = this.clientFactory.create(context.metadata.sessionKey);
      context.generation += 1;
      context.authenticated = false;
      this.bindEvents(context, context.client, context.generation);
    }
    return { client: context.client, generation: context.generation };
  }

  bindEvents(context, client, generation) {
    const on = (event, listener) => {
      client.on(event, listener);
      context.listeners.push({ client, event, listener });
    };

    on('qr', (qr) => this.trackClientEvent(context, client, generation, async () => {
      try {
        const qrCode = await this.qrEncoder(qr, {
          errorCorrectionLevel: 'M',
          margin: 2,
          width: 320,
        });
        if (!this.isCurrent(context, client, generation)) return;
        context.qrCode = qrCode;
        context.authenticated = false;
        const updated = await this.updateStatus(
          context,
          'WaitingQRCode',
          undefined,
          undefined,
          client,
          generation,
        );
        if (!updated) return;
        this.logger.info('QR Code WhatsApp gerado.', {
          empresaId: context.empresaId,
        });
      } catch (error) {
        if (!this.isCurrent(context, client, generation)) return;
        context.qrCode = null;
        const updated = await this.updateStatus(
          context,
          'Error',
          undefined,
          undefined,
          client,
          generation,
        );
        if (!updated) return;
        this.logger.error('Falha ao gerar QR Code WhatsApp.', {
          empresaId: context.empresaId,
          errorType: error?.name ?? 'Error',
        });
        await this.retireClient(context, client, generation);
      }
    }));
    on('authenticated', () => {
      if (!this.isCurrent(context, client, generation) || context.authenticated) {
        return;
      }
      context.authenticated = true;
      this.logger.info('Sessão WhatsApp autenticada.', {
        empresaId: context.empresaId,
      });
    });
    on('ready', () => this.trackClientEvent(context, client, generation, async () => {
      context.qrCode = null;
      const phoneNumber = normalizeConnectedPhone(client.info?.wid);
      const updated = await this.updateStatus(
        context,
        'Connected',
        this.now(),
        phoneNumber,
        client,
        generation,
      );
      if (!updated) return;
      this.settleInitialization(context);
      this.logger.info('Sessão WhatsApp conectada.', {
        empresaId: context.empresaId,
      });
    }));
    on('auth_failure', () => this.trackClientEvent(context, client, generation, async () => {
      context.qrCode = null;
      const updated = await this.updateStatus(
        context,
        'Error',
        undefined,
        undefined,
        client,
        generation,
      );
      if (!updated) return;
      this.logger.error('Falha de autenticação da sessão WhatsApp.', {
        empresaId: context.empresaId,
      });
      await this.retireClient(context, client, generation);
    }));
    on('disconnected', () => this.trackClientEvent(context, client, generation, async () => {
      context.qrCode = null;
      const updated = await this.updateStatus(
        context,
        'Disconnected',
        undefined,
        undefined,
        client,
        generation,
      );
      if (!updated) return;
      this.logger.warn('Sessão WhatsApp desconectada.', {
        empresaId: context.empresaId,
      });
      await this.retireClient(context, client, generation);
    }));
    on(injectionFailureEvent, (error) => this.trackClientEvent(
      context,
      client,
      generation,
      async () => {
        context.qrCode = null;
        const updated = await this.updateStatus(
          context,
          'Error',
          undefined,
          undefined,
          client,
          generation,
        );
        if (!updated) return;
        this.logger.error('Falha durante a reinjeção WhatsApp; cliente será descartado.', {
          empresaId: context.empresaId,
          errorType: error?.name ?? 'Error',
        });
        await this.retireClient(context, client, generation);
      },
    ));
  }

  initialize(context) {
    if (context.initializationPromise) {
      return context.initializationPromise;
    }

    context.initializationPromise = new Promise((resolve) => {
      context.settleInitialization = resolve;
    });
    void this.runInitialization(context).catch((error) => {
      this.settleInitialization(context);
      this.resolveWaiters(context);
      this.logger.error('Falha no ciclo de vida da sessão WhatsApp.', {
        empresaId: context.empresaId,
        errorType: error?.name ?? 'Error',
      });
    });
    return context.initializationPromise;
  }

  async runInitialization(context) {
    let client = null;
    let generation = context.generation;
    try {
      ({ client, generation } = await this.prepareClient(context));
      if (!client) {
        this.settleInitialization(context);
        return;
      }
      this.logger.info('Inicialização da sessão WhatsApp iniciada.', {
        empresaId: context.empresaId,
      });
      await Promise.resolve().then(() => client.initialize());
    } catch (error) {
      if (!this.canUpdateStatus(context, client, generation)) return;
      const updated = await this.updateStatus(
        context,
        'Error',
        undefined,
        undefined,
        client,
        generation,
      );
      if (!updated) return;
      this.logger.error('Falha ao inicializar sessão WhatsApp.', {
        empresaId: context.empresaId,
        errorType: error?.name ?? 'Error',
      });
      if (client) {
        await this.retireClient(context, client, generation);
      } else {
        this.settleInitialization(context);
      }
    }
  }

  updateStatus(
    context,
    status,
    lastConnectedAt = undefined,
    phoneNumber = undefined,
    client = context.client,
    generation = context.generation,
  ) {
    const operation = context.statusPromise.then(async () => {
      if (!this.canUpdateStatus(context, client, generation)) return false;
      if (
        status === 'Connecting'
        && ['Connecting', 'Reconnecting', 'WaitingQRCode', 'Connected']
          .includes(context.metadata.status)
      ) {
        return false;
      }
      context.metadata = await this.registry.upsert({
        ...context.metadata,
        status,
        updatedAt: this.now(),
        lastConnectedAt:
          lastConnectedAt === undefined
            ? context.metadata.lastConnectedAt
            : lastConnectedAt,
        phoneNumber:
          phoneNumber === undefined
            ? context.metadata.phoneNumber
            : phoneNumber,
      });
      this.resolveWaiters(context);
      return true;
    });
    context.statusPromise = operation.catch(() => {});
    return operation;
  }

  waitForStatus(context) {
    return new Promise((resolve) => {
      const complete = () => {
        clearTimeout(timeout);
        context.waiters.delete(complete);
        resolve();
      };
      const timeout = setTimeout(complete, this.connectWaitMs);
      context.waiters.add(complete);
    });
  }

  resolveWaiters(context) {
    for (const resolve of [...context.waiters]) {
      resolve();
    }
  }

  settleInitialization(context) {
    const settle = context.settleInitialization;
    context.initializationPromise = null;
    context.settleInitialization = null;
    settle?.();
  }

  async retireClient(context, client, generation) {
    if (!this.isCurrent(context, client, generation)) return;
    context.client = null;
    context.generation += 1;
    context.authenticated = false;
    this.settleInitialization(context);
    this.unbindEvents(context, client);
    context.cleanupPromise = context.cleanupPromise.then(() =>
      this.destroyClient(client, context.empresaId, false));
    await context.cleanupPromise;
  }

  async destroyClient(client, empresaId, logout) {
    if (this.destroyedClients.has(client)) return;
    this.destroyedClients.add(client);
    if (logout) {
      try {
        await client.logout();
      } catch (error) {
        this.logger.warn('Falha ao encerrar sessão no cliente WhatsApp.', {
          empresaId,
          errorType: error?.name ?? 'Error',
        });
      }
    }
    try {
      await client.destroy();
    } catch (error) {
      this.logger.warn('Falha ao destruir cliente WhatsApp.', {
        empresaId,
        errorType: error?.name ?? 'Error',
      });
    }
  }

  unbindEvents(context, targetClient = undefined) {
    for (const binding of context.listeners) {
      if (!targetClient || binding.client === targetClient) {
        binding.client.off(binding.event, binding.listener);
      }
    }
    context.listeners = targetClient
      ? context.listeners.filter((binding) => binding.client !== targetClient)
      : [];
  }

  isCurrent(context, client, generation) {
    return Boolean(
      client
      && !context.closing
      && this.contexts.get(context.empresaId) === context
      && context.client === client
      && context.generation === generation,
    );
  }

  canUpdateStatus(context, client, generation) {
    if (
      context.closing
      || this.contexts.get(context.empresaId) !== context
      || context.generation !== generation
    ) {
      return false;
    }
    return client ? context.client === client : context.client === null;
  }

  trackClientEvent(context, client, generation, action) {
    if (!this.isCurrent(context, client, generation)) {
      return Promise.resolve();
    }
    return this.trackEvent(async () => {
      if (!this.isCurrent(context, client, generation)) return;
      await action();
    });
  }

  trackEvent(action) {
    const pending = Promise.resolve()
      .then(action)
      .catch((error) => {
        this.logger.error('Falha ao persistir evento de sessão WhatsApp.', {
          errorType: error?.name ?? 'Error',
        });
      });
    this.pendingEvents.add(pending);
    void pending.finally(() => this.pendingEvents.delete(pending));
    return pending;
  }

  toPublicStatus(context) {
    return {
      status: context.metadata.status,
      qrCode:
        context.metadata.status === 'WaitingQRCode' ? context.qrCode : null,
      createdAt: context.metadata.createdAt,
      updatedAt: context.metadata.updatedAt,
      lastConnectedAt: context.metadata.lastConnectedAt,
      phoneNumber: context.metadata.phoneNumber ?? null,
    };
  }
}

function normalizeConnectedPhone(wid) {
  const raw = wid?.user ?? wid?._serialized?.split('@')[0];
  if (typeof raw !== 'string') return null;
  const digits = raw.replace(/\D/g, '');
  return /^\d{8,15}$/.test(digits) ? digits : null;
}

function toPublicMetadata(metadata) {
  return {
    status: metadata.status,
    qrCode: null,
    createdAt: metadata.createdAt,
    updatedAt: metadata.updatedAt,
    lastConnectedAt: metadata.lastConnectedAt,
    phoneNumber: metadata.phoneNumber ?? null,
  };
}
