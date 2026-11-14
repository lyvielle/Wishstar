// Wishstar theme: Light, Dark or Auto (dark at night by local time, not by OS/browser setting).
// Loaded synchronously in <head> so the right theme is applied before the first paint (no light flash at night).
(() => {
    'use strict';

    const KEY = 'wishstar:theme';
    const NIGHT_STARTS = 20; // 20:00
    const NIGHT_ENDS = 7;    // 07:00
    const THEME_COLORS = { light: '#FE8069', dark: '#1B1218' };

    const read = () => {
        try {
            const value = localStorage.getItem(KEY);
            return value === 'light' || value === 'dark' ? value : 'auto';
        } catch {
            return 'auto';
        }
    };

    const isNight = (date = new Date()) => date.getHours() >= NIGHT_STARTS || date.getHours() < NIGHT_ENDS;
    const resolve = (preference) => preference === 'auto' ? (isNight() ? 'dark' : 'light') : preference;

    function apply() {
        const preference = read();
        const theme = resolve(preference);
        const root = document.documentElement;
        if (root.getAttribute('data-theme') !== theme) root.setAttribute('data-theme', theme);
        if (root.getAttribute('data-theme-preference') !== preference) root.setAttribute('data-theme-preference', preference);

        const meta = document.querySelector('meta[name="theme-color"]');
        if (meta && meta.getAttribute('content') !== THEME_COLORS[theme]) meta.setAttribute('content', THEME_COLORS[theme]);
    }

    apply();

    // Auto mode follows the clock: re-check every minute and whenever the app comes back to the foreground.
    setInterval(apply, 60_000);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) apply(); });
    document.addEventListener('DOMContentLoaded', apply);
    // Other tabs changed the setting.
    window.addEventListener('storage', (event) => { if (event.key === KEY) apply(); });
    // Page updates that rewrite <html> attributes (e.g. navigation to static pages) must not drop the theme.
    new MutationObserver(() => {
        if (document.documentElement.getAttribute('data-theme') !== resolve(read())) apply();
    }).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });

    window.wishstarTheme = {
        get: read,
        set(preference) {
            const value = preference === 'light' || preference === 'dark' ? preference : 'auto';
            try { localStorage.setItem(KEY, value); } catch { /* private mode: applies to this page only */ }
            apply();
            return value;
        },
        current: () => resolve(read()),
        nightHours: `${String(NIGHT_STARTS).padStart(2, '0')}:00–${String(NIGHT_ENDS).padStart(2, '0')}:00`
    };
})();
