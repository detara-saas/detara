// Deliberately minimal, no accounts/identifiers/messages; not a WA emulator.
export async function installSyntheticModules(page) {
  await page.evaluate(() => {
    window.Debug = { VERSION: '2.3000.1047198186' };
    const bus = () => ({ on() {}, off() {} });
    const socket = { ...bus(), state: 'CONNECTED', hasSynced: true };
    const modules = {
      WAWebSocketModel: { Socket: socket }, WAWebCmd: { Cmd: bus() },
      WAWebConnModel: { Conn: { ...bus(), serialize: () => ({}) } },
      WAWebOfflineHandler: { OfflineMessageHandler: { getOfflineDeliveryProgress: () => 100 } },
      WAWebUserPrefsMeUser: { getMaybeMePnUser: () => null, getMaybeMeLidUser: () => null },
      WAWebCollections: { Msg: bus(), Chat: bus() },
      WAWebSyncGatingUtils: {}, WAWebCallCollection: {},
      WAWebAddonReactionTableMode: { reactionTableMode: { bulkUpsert() {} } },
      WAWebAddonPollVoteTableMode: { pollVoteTableMode: { bulkUpsert() {} } },
    };
    window.require = (name) => modules[name] ?? {};
  });
}
