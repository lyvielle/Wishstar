// Wishstar service worker: makes the app installable, speeds up repeat visits and shows an offline page.
// Pages themselves are always fetched from the network (Blazor Server needs the live connection);
// static assets and wish images are served from cache.
const VERSION = 'v3';
const SHELL_CACHE = `wishstar-shell-${VERSION}`;
const STATIC_CACHE = `wishstar-static-${VERSION}`;
const IMAGE_CACHE = `wishstar-img-${VERSION}`;
const MAX_IMAGES = 400;

const SHELL_ASSETS = [
    '/offline.html',
    '/manifest.webmanifest',
    '/resources/favicon.svg',
    '/resources/favicon-96x96.png',
    '/resources/web-app-manifest-192x192.png',
    '/resources/web-app-manifest-512x512.png'
];

self.addEventListener('install', (event) => {
    event.waitUntil(caches.open(SHELL_CACHE).then(cache => cache.addAll(SHELL_ASSETS)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', (event) => {
    event.waitUntil((async () => {
        const keep = new Set([SHELL_CACHE, STATIC_CACHE, IMAGE_CACHE]);
        const keys = await caches.keys();
        await Promise.all(keys.filter(k => k.startsWith('wishstar-') && !keep.has(k)).map(k => caches.delete(k)));
        if (self.registration.navigationPreload) await self.registration.navigationPreload.enable();
        await self.clients.claim();
    })());
});

self.addEventListener('fetch', (event) => {
    const request = event.request;
    if (request.method !== 'GET') return;

    const url = new URL(request.url);

    // Cross-origin requests (Font Awesome CDN) are left to the browser and its HTTP cache: re-fetching them
    // from the worker would be blocked by the worker's CSP (connect-src 'self') and breaks SRI.
    if (url.origin !== self.location.origin) return;

    // Live connection, API and auth-dependent endpoints: always network.
    if (url.pathname.startsWith('/_blazor') || url.pathname.startsWith('/api/')) return;

    if (request.mode === 'navigate') {
        event.respondWith(networkWithOfflineFallback(event));
        return;
    }

    if (url.pathname.startsWith('/img/') || url.pathname === '/icon') {
        event.respondWith(cacheFirst(request, IMAGE_CACHE, MAX_IMAGES));
        return;
    }

    if (/\.(css|js|woff2?|ttf|png|svg|ico|webp|webmanifest)$/i.test(url.pathname)) {
        event.respondWith(staleWhileRevalidate(request, STATIC_CACHE));
    }
});

async function networkWithOfflineFallback(event) {
    try {
        const preloaded = await event.preloadResponse;
        if (preloaded) return preloaded;
        return await fetch(event.request);
    } catch {
        return (await caches.match('/offline.html')) || Response.error();
    }
}

async function cacheFirst(request, cacheName, maxEntries) {
    const cache = await caches.open(cacheName);
    const cached = await cache.match(request);
    if (cached) return cached;

    const response = await fetch(request);
    if (response.ok) {
        await cache.put(request, response.clone());
        trimCache(cache, maxEntries);
    }
    return response;
}

async function staleWhileRevalidate(request, cacheName) {
    const cache = await caches.open(cacheName);
    const cached = await cache.match(request);
    const network = fetch(request).then(response => {
        if (response.ok || response.type === 'opaque') cache.put(request, response.clone());
        return response;
    }).catch(() => cached);
    return cached || network;
}

async function trimCache(cache, maxEntries) {
    const keys = await cache.keys();
    for (let i = 0; i < keys.length - maxEntries; i++) await cache.delete(keys[i]);
}
