// Development always uses the network. Offline caching is enabled only in published builds.
// A previously published worker may still control localhost, so the explicit update action
// must be able to activate this network-only worker and discard the obsolete app shell.
const cacheNamePrefix = 'detara-app-shell-';

self.addEventListener('activate', event => event.waitUntil(onActivate()));
self.addEventListener('fetch', () => { });
self.addEventListener('message', event => {
    if (event.data?.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});

async function onActivate() {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix))
        .map(key => caches.delete(key)));
    await self.clients.claim();
}
