import { randomUUID } from 'node:crypto';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const { LoadUtils } = require('whatsapp-web.js/src/util/Injected/Utils');
const ClientInfo = require('whatsapp-web.js/src/structures/ClientInfo');
const InterfaceController = require('whatsapp-web.js/src/util/InterfaceController');

export const postAuthStageEvent = 'detara_post_auth_stage';

function contextChanged() {
  const error = new Error('Documento pós-autenticação substituído.');
  error.name = 'PostAuthDocumentChanged';
  return error;
}

export function classifyPostAuthError(error) {
  const message = String(error?.message ?? error ?? '');
  if (error?.name === 'PostAuthDocumentChanged' || /Execution context was destroyed|Cannot find context with specified id/.test(message)) return 'ContextChanged';
  if (error?.name === 'PostAuthTimeout') return 'Timeout';
  if (/Target closed|Session closed/.test(message)) return 'TargetClosed';
  if (/binding.*already exists|Failed to add page binding/.test(message)) return 'BindingError';
  if (error?.name === 'ProtocolError') return 'ProtocolError';
  return 'Error';
}

// Boundary for the exact 1.34.7 hasSynced callback, not a copy of Client.inject().
// Cache is explicitly none. Keep upstream LoadUtils/ClientInfo/listeners intact.
// Remove/review when upstream catches this exposed callback and guards readiness.
// Evidence: docs/qa-whatsapp-post-auth-ready.md, upstream issues #127084/#201821.
export class PostAuthCoordinator {
  constructor(client, onFailure, { timeoutMs = 30_000 } = {}) {
    this.client = client;
    this.onFailure = onFailure;
    this.timeoutMs = timeoutMs;
    this.pending = null;
    this.finished = false;
    this.stopped = false;
    this.stage = 'POST_AUTH_STARTED';
  }

  stop() {
    this.stopped = true;
    this.cancel?.();
  }

  run() {
    if (this.stopped || this.client.stopping) return Promise.resolve();
    if (this.pending) return this.pending;
    this.pending = this.execute().finally(() => { this.pending = null; });
    return this.pending;
  }

  async execute() {
    let timer;
    try {
      await Promise.race([
        this.prepare(),
        new Promise((resolve) => { this.cancel = resolve; }),
        new Promise((_, reject) => {
          timer = setTimeout(() => {
            const error = new Error('Prazo de preparação pós-autenticação excedido.');
            error.name = 'PostAuthTimeout';
            reject(error);
          }, this.timeoutMs);
        }),
      ]);
    } catch (error) {
      if (!this.stopped && !this.client.stopping) {
        this.stopped = true; // Timeout is not cancellation: late work cannot emit ready.
        const safe = new Error('Falha na preparação pós-autenticação WhatsApp.');
        safe.name = classifyPostAuthError(error);
        safe.postAuthStage = this.stage;
        this.onFailure(safe);
      }
    } finally {
      clearTimeout(timer);
      this.cancel = null;
    }
  }

  guard() {
    if (this.stopped || this.client.stopping) throw new Error('Client encerrando.');
  }

  mark(stage) {
    this.guard();
    this.stage = stage;
    this.client.emit(postAuthStageEvent, stage);
  }

  async current(page, documentId) {
    this.guard();
    if (page !== this.client.pupPage || page.isClosed()) throw contextChanged();
    const valid = await page.evaluate((id) => window.__detaraPostAuthDocument === id, documentId);
    this.guard();
    if (!valid) throw contextChanged();
  }

  async prepare() {
    this.guard();
    if (this.finished) {
      const sameDocument = await this.client.pupPage.evaluate(
        (id) => window.__detaraPostAuthDocument === id, this.completedDocumentId);
      this.guard();
      if (sameDocument) return;
      // Exposed functions survive navigation, operational listeners do not.
      // A fresh document must complete preparation again, not reuse old ready.
      this.finished = false;
    }
    this.mark('POST_AUTH_STARTED');
    const payload = await this.client.authStrategy.getAuthEventPayload();
    this.guard();
    this.client.emit('authenticated', payload);
    for (let attempt = 1; attempt <= 2; attempt += 1) {
      let listenersStarted = false;
      const page = this.client.pupPage;
      try {
        this.guard();
        const documentId = await page.evaluate((id) => {
          window.__detaraPostAuthDocument ??= id;
          return window.__detaraPostAuthDocument;
        }, randomUUID());
        await this.current(page, documentId);
        // No HTML write after authentication; LocalAuth remains independently persistent.
        this.mark('POST_AUTH_CACHE_SKIPPED');
        this.mark('POST_AUTH_LOAD_UTILS_STARTED');
        await page.evaluate(LoadUtils);
        await this.current(page, documentId);
        this.mark('POST_AUTH_LOAD_UTILS_DONE');
        const usable = await page.evaluate(() => Boolean(window.AuthStore
          && window.require('WAWebSocketModel').Socket.state === 'CONNECTED'
          && window.WWebJS && typeof window.WWebJS.sendMessage === 'function'));
        await this.current(page, documentId);
        if (!usable) throw new Error('Utilitários pós-autenticação indisponíveis.');
        this.mark('POST_AUTH_WWEBJS_READY');
        const info = await page.evaluate(() => ({
          ...window.require('WAWebConnModel').Conn.serialize(),
          wid: window.require('WAWebUserPrefsMeUser').getMaybeMePnUser()
            || window.require('WAWebUserPrefsMeUser').getMaybeMeLidUser(),
        }));
        await this.current(page, documentId);
        this.client.info = new ClientInfo(this.client, info);
        this.client.interface = new InterfaceController(this.client);
        this.mark('POST_AUTH_CLIENT_INFO_DONE');
        listenersStarted = true;
        this.mark('POST_AUTH_LISTENERS_STARTED');
        await this.client.attachEventListeners();
        await this.current(page, documentId);
        const listenersReady = await page.evaluate(() => Boolean(window.WWebJS
          && typeof window.onAddMessageEvent === 'function'
          && typeof window.onAppStateChangedEvent === 'function'
          && window.require('WAWebSocketModel').Socket.state === 'CONNECTED'));
        await this.current(page, documentId);
        if (!listenersReady) throw new Error('Listeners pós-autenticação indisponíveis.');
        this.mark('POST_AUTH_LISTENERS_DONE');
        this.completedDocumentId = documentId;
        this.finished = true;
        this.mark('READY_EMITTED');
        this.client.emit('ready');
        await this.client.authStrategy.afterAuthReady();
        return;
      } catch (error) {
        this.guard();
        // Never repeat partial listener attachment in the same document. Retire safely.
        if (classifyPostAuthError(error) !== 'ContextChanged' || listenersStarted || attempt === 2) throw error;
        this.mark('POST_AUTH_CONTEXT_RETRY');
        await page.waitForFunction('window.Debug?.VERSION != undefined && !!window.require && !!window.AuthStore', { timeout: this.timeoutMs });
        this.guard();
      }
    }
  }
}
