# Wishstar – notes for Claude

Blazor Server wishlist app (.NET), deployed with `docker compose up -d --build`. Build check: `dotnet build Wishstar`.

## Layout
- Pages: `Wishstar/Components/Pages/*.razor` + code-behind `*.razor.cs`; shared UI in `Components/Shared` (`ConfirmDialog`, `FormPage`, …).
- All client JS lives in `Wishstar/wwwroot/js/app.js`, exposed as `window.wishstar.{storage,scroll,nav,upload,paste,auth}`. `theme.js` handles Light/Auto/Dark.
- Styles: single `Wishstar/wwwroot/app.css` with theme tokens (dark × pink theme).
- Image uploads go through hidden `<input type="file">` + `wishstar.upload.*` (client downscale → `/api/icon/upload` or `/api/import/screenshot`).
- Pasting an image (Ctrl+V) on the wish form: `wishstar.paste` holds the file, the page asks "product photo" vs "import", then `paste.attach` puts it into the matching input and the existing handler runs.

## Git
- Commit and push as the user (git user `lyvielle`), directly to `main`, using `git`/`gh`.
- **No `Co-Authored-By` or other Claude attribution** in commits or PRs.
- Commit style: `feat: …` / `fix: …`.
