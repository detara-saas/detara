import { randomUUID } from 'node:crypto';
import { ConflictError } from './errors.js';
import { createSessionKey } from './session-registry.js';

const disconnectedStatus = Object.freeze({
  status: 'Disconnected',
  qrCode: null,
  createdAt: null,
  updatedAt: null,
  lastConnectedAt: null,
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
  }

  async start() {
    const sessions = await this.registry.load();
    await this.deliveryStore.load();
    for (const session of sessions) {
      const restoring = await this.registry.upsert({
        ...session,
        status: 'Disconnected',
        updatedAt: this.now(),
      });
      const context = this.ensureContext(session.empresaId, restoring);
      this.initialize(context);
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
      });
      this.logger.info('Conexão WhatsApp criada.', { empresaId });
    }

    const context = this.ensureContext(empresaId, metadata);
    if (context.metadata.status === 'Connected') {
      return this.toPublicStatus(context);
    }

    const statusChanged = this.waitForStatus(context);
    this.initialize(context);
    await Promise.race([
      statusChanged,
      new Promise((resolve) => setTimeout(resolve, this.connectWaitMs)),
    ]);
    return this.toPublicStatus(context);
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
    const restored = this.ensureContext(empresaId, metadata);
    this.initialize(restored);
    return this.toPublicStatus(restored);
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
    await Promise.allSettled(
      [...this.contexts.values()].map((context) => context.client.destroy()),
    );
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
      client: this.clientFactory.create(metadata.sessionKey),
      qrCode: null,
      initializationPromise: null,
      waiters: new Set(),
    };
    this.bindEvents(context);
    this.contexts.set(empresaId, context);
    return context;
  }

  bindEvents(context) {
    context.client.on('qr', (qr) => this.trackEvent(async () => {
      try {
        context.qrCode = await this.qrEncoder(qr, {
          errorCorrectionLevel: 'M',
          margin: 2,
          width: 320,
        });
        await this.updateStatus(context, 'WaitingQRCode');
        this.logger.info('QR Code WhatsApp gerado.', {
          empresaId: context.empresaId,
        });
      } catch (error) {
        await this.updateStatus(context, 'Error');
        this.logger.error('Falha ao gerar QR Code WhatsApp.', {
          empresaId: context.empresaId,
          errorType: error?.name ?? 'Error',
        });
      }
    }));
    context.client.on('authenticated', () => {
      this.logger.info('Sessão WhatsApp autenticada.', {
        empresaId: context.empresaId,
      });
    });
    context.client.on('ready', () => this.trackEvent(async () => {
      context.qrCode = null;
      await this.updateStatus(context, 'Connected', this.now());
      this.logger.info('Sessão WhatsApp conectada.', {
        empresaId: context.empresaId,
      });
    }));
    context.client.on('auth_failure', () => this.trackEvent(async () => {
      context.qrCode = null;
      await this.updateStatus(context, 'Error');
      this.logger.error('Falha de autenticação da sessão WhatsApp.', {
        empresaId: context.empresaId,
      });
    }));
    context.client.on('disconnected', () => this.trackEvent(async () => {
      context.qrCode = null;
      await this.updateStatus(context, 'Disconnected');
      this.logger.warn('Sessão WhatsApp desconectada.', {
        empresaId: context.empresaId,
      });
    }));
  }

  initialize(context) {
    if (context.initializationPromise) {
      return context.initializationPromise;
    }
    context.initializationPromise = Promise.resolve(context.client.initialize())
      .catch(async (error) => {
        await this.updateStatus(context, 'Error');
        this.logger.error('Falha ao inicializar sessão WhatsApp.', {
          empresaId: context.empresaId,
          errorType: error?.name ?? 'Error',
        });
      })
      .finally(() => {
        context.initializationPromise = null;
      });
    return context.initializationPromise;
  }

  async updateStatus(context, status, lastConnectedAt = undefined) {
    context.metadata = await this.registry.upsert({
      ...context.metadata,
      status,
      updatedAt: this.now(),
      lastConnectedAt:
        lastConnectedAt === undefined
          ? context.metadata.lastConnectedAt
          : lastConnectedAt,
    });
    for (const resolve of context.waiters) {
      resolve();
    }
    context.waiters.clear();
  }

  waitForStatus(context) {
    return new Promise((resolve) => context.waiters.add(resolve));
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
    };
  }
}
