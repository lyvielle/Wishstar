// Wishstar client script: Blazor startup & reconnection, scroll/filter persistence, image uploads,
// login/logout and service worker registration.
(() => {
    'use strict';

    // ------------------------------------------------------------------ storage
    const storage = {
        get(key) {
            try { return sessionStorage.getItem('wishstar:' + key); } catch { return null; }
        },
        set(key, value) {
            try {
                if (value === null || value === undefined) sessionStorage.removeItem('wishstar:' + key);
                else sessionStorage.setItem('wishstar:' + key, value);
            } catch { /* private mode / quota: not critical */ }
        }
    };

    // ------------------------------------------------------------------ scroll position
    // The browser can't restore the list position on its own (the list is rendered by Blazor after
    // navigation), so it is saved when leaving the list and restored once the list has rendered.
    if ('scrollRestoration' in history) history.scrollRestoration = 'manual';

    const isListPage = () => location.pathname === '/' || location.pathname === '/wishlist';

    const scroll = {
        save() {
            if (isListPage()) storage.set('list-scroll', String(Math.round(window.scrollY)));
        },
        restore() {
            const y = parseInt(storage.get('list-scroll') || '0', 10);
            if (!(y > 0)) return;
            // Wait for layout, then jump without animation. Retry briefly in case the list is still growing.
            let attempts = 0;
            const apply = () => {
                window.scrollTo({ top: y, behavior: 'instant' });
                if (Math.abs(window.scrollY - y) > 2 && ++attempts < 10) setTimeout(apply, 50);
            };
            requestAnimationFrame(() => requestAnimationFrame(apply));
        }
    };

    // A tab that is hidden may be discarded or reloaded; remember where the list was.
    document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'hidden') scroll.save(); });
    window.addEventListener('pagehide', () => scroll.save());

    // ------------------------------------------------------------------ in-app navigation
    // Forms return with history.back() when they were opened from inside the app, which keeps the history
    // clean and returns to the exact previous state. A small stack (per tab) remembers which entries we pushed.
    const currentUrl = () => location.pathname + location.search;
    const readStack = () => { try { return JSON.parse(storage.get('nav-stack') || '[]'); } catch { return []; } };
    const writeStack = (stack) => storage.set('nav-stack', JSON.stringify(stack.slice(-20)));

    const nav = {
        go(url) {
            if (!window.Blazor || typeof Blazor.navigateTo !== 'function') return false;
            scroll.save();
            const target = new URL(url, location.origin);
            const stack = readStack();
            stack.push(target.pathname + target.search);
            writeStack(stack);
            Blazor.navigateTo(target.pathname + target.search);
            return true;
        },
        back() {
            const stack = readStack();
            if (stack.length > 0 && stack[stack.length - 1] === currentUrl() && history.length > 1) {
                stack.pop();
                writeStack(stack);
                history.back();
                return true;
            }
            return false;
        }
    };

    // ------------------------------------------------------------------ image upload
    const MAX_DIMENSION = 2048;

    async function downscale(file) {
        if (typeof createImageBitmap !== 'function') return file;
        let bitmap;
        try {
            bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
        } catch {
            return file; // Let the server try (e.g. formats the browser can't decode).
        }

        const scale = Math.min(1, MAX_DIMENSION / Math.max(bitmap.width, bitmap.height));
        if (scale === 1 && file.size <= 2_500_000) {
            bitmap.close();
            return file;
        }

        const canvas = document.createElement('canvas');
        canvas.width = Math.round(bitmap.width * scale);
        canvas.height = Math.round(bitmap.height * scale);
        const ctx = canvas.getContext('2d');
        ctx.imageSmoothingQuality = 'high';
        ctx.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
        bitmap.close();

        // Keep transparency for PNG/WebP/GIF sources, otherwise JPEG is smallest and universally supported.
        const keepAlpha = /png|webp|gif/i.test(file.type);
        const type = keepAlpha ? 'image/webp' : 'image/jpeg';
        const blob = await new Promise(resolve => canvas.toBlob(resolve, type, 0.9));
        if (!blob || (keepAlpha && blob.type !== 'image/webp')) {
            const png = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
            return png && png.size < file.size ? png : file;
        }
        return blob.size < file.size ? blob : file;
    }

    const upload = {
        async fromInput(input) {
            const file = input && input.files && input.files[0];
            if (!file) return { ok: false, fileName: null, error: null };
            if (file.type && !file.type.startsWith('image/')) {
                input.value = '';
                return { ok: false, fileName: null, error: 'Please choose an image file.' };
            }

            try {
                const blob = await downscale(file);
                const form = new FormData();
                form.append('file', blob, file.name || 'photo');
                const response = await fetch('/api/icon/upload', { method: 'POST', body: form, credentials: 'same-origin' });
                if (!response.ok) {
                    const message = response.status === 401
                        ? 'Your session has expired. Please log in again.'
                        : (await response.text()) || 'The photo could not be uploaded.';
                    return { ok: false, fileName: null, error: message.replace(/^"|"$/g, '') };
                }
                const data = await response.json();
                return { ok: true, fileName: data.fileName, error: null };
            } catch {
                return { ok: false, fileName: null, error: 'The photo could not be uploaded. Check your connection and try again.' };
            } finally {
                input.value = '';
            }
        }
    };

    // ------------------------------------------------------------------ auth
    const IMAGE_CACHE_PREFIX = 'wishstar-img';

    const auth = {
        async logout() {
            try { await fetch('/api/logout', { method: 'POST', credentials: 'same-origin' }); } catch { /* ignore */ }
            // Private images may be in the offline cache: drop them on logout.
            if ('caches' in window) {
                const keys = await caches.keys();
                await Promise.all(keys.filter(k => k.startsWith(IMAGE_CACHE_PREFIX)).map(k => caches.delete(k)));
            }
            location.replace('/wishlist');
        }
    };

    // The login page is static (no Blazor circuit), so its form is handled here.
    document.addEventListener('submit', async (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return;

        // Blazor forms must never fall back to a native submit before the circuit is ready.
        if (form.hasAttribute('data-no-native-submit')) {
            event.preventDefault();
            return;
        }

        if (!form.hasAttribute('data-login-form')) return;
        event.preventDefault();

        const button = form.querySelector('button[type="submit"]');
        const error = form.querySelector('[data-login-error]');
        const original = button.innerHTML;
        button.disabled = true;
        button.innerHTML = '<i class="fas fa-circle-notch fa-spin" aria-hidden="true"></i> <span>Signing in…</span>';
        error.hidden = true;

        try {
            const response = await fetch('/api/login', {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ email: form.email.value.trim(), password: form.password.value })
            });

            if (response.ok) {
                location.replace(form.dataset.returnUrl || '/wishlist');
                return;
            }

            error.textContent = response.status === 429
                ? 'Too many attempts. Please wait a few minutes and try again.'
                : 'Invalid email or password.';
        } catch {
            error.textContent = 'Could not reach the server. Check your connection and try again.';
        }

        error.hidden = false;
        button.disabled = false;
        button.innerHTML = original;
    }, true);

    // ------------------------------------------------------------------ reconnection
    // Blazor Server keeps the UI state on the server and talks to it over a WebSocket. Mobile browsers and
    // background tabs drop that socket regularly. Instead of the default blocking "Reconnecting…" overlay:
    //  * reconnect immediately when the tab becomes visible again,
    //  * show a small, delayed toast only if reconnecting actually takes a moment,
    //  * if the server has already discarded the session, silently reload (scroll position is kept).
    const reconnectToast = document.getElementById('components-reconnect-modal');
    let reconnectState = 'hide';

    async function reconnectNow() {
        if (!window.Blazor || typeof Blazor.reconnect !== 'function') return;
        try {
            const reconnected = await Blazor.reconnect();
            if (!reconnected) {
                // The server no longer knows this session: start a fresh one.
                scroll.save();
                location.reload();
            }
        } catch {
            /* Server unreachable (offline); retried on the next visibility change. */
        }
    }

    if (reconnectToast) {
        reconnectToast.addEventListener('components-reconnect-state-changed', (event) => {
            reconnectState = event.detail.state;
            reconnectToast.classList.toggle('is-visible', reconnectState === 'show' || reconnectState === 'retrying' || reconnectState === 'failed');
            reconnectToast.classList.toggle('is-failed', reconnectState === 'failed');
            const text = reconnectToast.querySelector('.reconnect-text');
            if (text) text.textContent = reconnectState === 'failed' ? 'Connection lost – tap to retry' : 'Reconnecting…';

            if (reconnectState === 'rejected') {
                scroll.save();
                location.reload();
            }
        });

        reconnectToast.addEventListener('click', () => { if (reconnectState === 'failed') reconnectNow(); });
    }

    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible' && reconnectState === 'failed') reconnectNow();
    });
    window.addEventListener('online', () => { if (reconnectState === 'failed') reconnectNow(); });

    // ------------------------------------------------------------------ start
    window.wishstar = { storage, scroll, nav, upload, auth };

    if (!window.Blazor) {
        console.error('Wishstar: _framework/blazor.web.js failed to load; interactive features are unavailable.');
        return;
    }

    Blazor.start({
        circuit: {
            configureSignalR(builder) {
                // Must stay above the server's keep-alive interval (15s); generous because browsers throttle
                // timers in background tabs.
                builder.withServerTimeout(120000).withKeepAliveInterval(15000);
            },
            reconnectionOptions: {
                maxRetries: 60,
                // Retry quickly at first (most drops are brief), then back off.
                retryIntervalMilliseconds: (previousAttempts) =>
                    previousAttempts < 3 ? 500 : previousAttempts < 10 ? 2000 : 5000
            }
        }
    });

    // ------------------------------------------------------------------ PWA
    if ('serviceWorker' in navigator && window.isSecureContext) {
        window.addEventListener('load', () => {
            navigator.serviceWorker.register('/service-worker.js', { scope: '/' }).catch(() => { /* not critical */ });
        });
    }
})();
