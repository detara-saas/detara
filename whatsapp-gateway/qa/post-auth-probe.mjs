// QA helper for an already-owned local Page. No discovery/CDP port or HTTP route.
// Never returns title, query strings, HTML, WIDs, cookies or business data.
export async function readPostAuthProbe(page) {
  return page.evaluate(() => {
    const safe = (operation) => { try { return operation(); } catch { return undefined; } };
    const socket = safe(() => window.require('WAWebSocketModel').Socket);
    const state = socket?.state;
    const version = String(window.Debug?.VERSION ?? '');
    const progress = safe(() => window.AuthStore.OfflineMessageHandler.getOfflineDeliveryProgress());
    return {
      documentReadyState: ['loading', 'interactive', 'complete'].includes(document.readyState) ? document.readyState : 'unknown',
      webVersion: /^[0-9.]+(?:-alpha)?$/.test(version) ? version : null,
      page: window.location.origin === 'https://web.whatsapp.com' ? 'https://web.whatsapp.com/' : 'local-or-other',
      socketState: ['CONNECTED', 'OPENING', 'PAIRING', 'TIMEOUT', 'UNPAIRED', 'UNPAIRED_IDLE'].includes(state) ? state : 'unknown',
      hasSynced: socket?.hasSynced === true,
      offlineProgress: typeof progress === 'number' && progress >= 0 && progress <= 100 ? progress : null,
      hasAuthStore: Boolean(window.AuthStore),
      hasWWebJS: Boolean(window.WWebJS),
      hasSendMessage: typeof window.WWebJS?.sendMessage === 'function',
      bindings: Object.fromEntries(['onAppStateHasSyncedEvent', 'onAddMessageEvent',
        'onAppStateChangedEvent', 'onIncomingCall'].map((name) => [name, typeof window[name] === 'function'])),
    };
  });
}
