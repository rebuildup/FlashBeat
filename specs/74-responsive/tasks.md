# Tasks: Responsive Layout & Keyboard Navigation (#74)

## Task list (max 5 per spec)

| ID | Description | Files | Test |
|----|-------------|-------|------|
| **T1** | **Identify stress points** — done: `spec.md` §2 with `file:line` of the baseline `94a4103`. | `specs/74-responsive/spec.md` | — |
| **T2** | **Reflow / disclosure** — scroll ownership, sticky sidebar, fluid toolbars, catalog card list, inspector drawers, focusable note grid. | `web/src/styles/app.css`, `web/src/app/app-shell.tsx`, `web/src/app/sidebar.tsx`, `web/src/app/topbar.tsx`, `web/src/app/catalog-panel.tsx`, `web/src/app/panel.tsx`, `web/src/app/drawer.tsx`, `web/src/components/chart-editor/chart-editor.tsx`, `web/src/components/chart-editor/editor-toolbar.tsx`, `web/src/components/chart-editor/note-grid.tsx`, `web/src/components/chart-editor/validation-panel.tsx`, `web/src/components/chart-editor/youtube-preview.tsx`, `web/src/components/song-publisher/song-publisher.tsx` | static review + `npx tsc --noEmit`; **visual check open (#75)** |
| **T3** | **Keyboard: Tab / Enter / Escape / focus restore** — global focus ring, drawer trap + restore, focusable note grid, `aria-expanded` on the publisher toggle. | `web/src/styles/app.css`, `web/src/app/drawer.tsx`, `web/src/app/drawer.test.ts`, `web/src/components/chart-editor/chart-editor.tsx`, `web/src/components/chart-editor/chart-editor.test.ts`, `web/src/components/chart-editor/note-grid.tsx`, `web/src/components/song-publisher/song-publisher.tsx`, `web/src/components/song-publisher/song-publisher.test.ts` | `web/src/app/drawer.test.ts`, `web/src/components/chart-editor/chart-editor.test.ts`, `web/src/components/song-publisher/song-publisher.test.ts` (vitest) |
| **T4** | **Long JP titles / loading / error states** — 2 line clamp in table and card, breakable error text, validation list and preview bounded. | `web/src/app/catalog-panel.tsx`, `web/src/app/panel.tsx`, `web/src/components/chart-editor/validation-panel.tsx`, `web/src/components/chart-editor/youtube-preview.tsx` | static review; **visual check open (#75)** |
| **T5** | **Verification evidence** — type check, tests, lint, dev server start; screenshots. | — | `npx tsc --noEmit`, `npm test`, `npx vp check`; screenshots **open (#75)** |

## Detailed breakdown

### T1 — stress points

- [x] Every fixed width, `100dvh` shell and `overflow: hidden` of the owned
      paths is listed with `file:line` in `spec.md` §2.
- [x] Out of scope paths (`web/pages/**`, `web/src/components/new-song/**`)
      are listed as known stress points, not fixed here.

### T2 — reflow and disclosure

- [x] `web/src/styles/app.css`: `body { overflow: auto }`, global
      `:focus-visible` ring.
- [x] `web/src/app/app-shell.tsx`: `minHeight: 100dvh`, block layout below
      `md` so the navigation can stick.
- [x] `web/src/app/sidebar.tsx`: horizontal bar with `position: sticky` below
      `md`, rail from `md`.
- [x] `web/src/app/topbar.tsx`: fluid search field (`flex: 1 1 auto`,
      `minWidth`/`maxWidth`), selects keep their width.
- [x] `web/src/app/catalog-panel.tsx`: card list below `md`, 2 line clamp
      for the title, actions row in the card.
- [x] `web/src/app/panel.tsx`: the panel frame scrolls, error text breaks.
- [x] `web/src/app/drawer.tsx` (new): shared backdrop and focus trap.
- [x] `web/src/components/chart-editor/chart-editor.tsx`: the inspector is
      one element — persistent column from `lg`, drawer below.
- [x] `web/src/components/chart-editor/editor-toolbar.tsx`: fluid fields,
      toggle button below `lg`.
- [x] `web/src/components/chart-editor/note-grid.tsx`: the grid is
      focusable and shows a focus outline; the horizontal scroll of the
      stage is not clipped any more.
- [x] `web/src/components/song-publisher/song-publisher.tsx`: the inspector
      is one element — persistent column from `lg`, drawer below.

### T3 — keyboard and focus

- [x] Global `:focus-visible` ring.
- [x] `useDrawerFocus`: focus in, focus trapped, `Escape` closes, focus
      restored to the trigger.
- [x] Focus trap logic unit tested (`nextFocusTarget`).
- [x] The inspector is mounted once — render tests assert a single
      metadata form / preview in the markup
      (`chart-editor.test.ts`, `song-publisher.test.ts`).
- [x] Note grid focusable (`tabIndex`, `role`, `aria-label`).
- [x] Publisher toggle: `aria-expanded`, state dependent `aria-label`,
      the broken `sidebar-toggle` class name removed.
- [x] Real browser tab order walk — evidence:
      `specs/74-responsive/evidence/2026-11-10/tab-walk.json` (+ `tabwalk_*.png`).
      Tab / Shift+Tab を実 Chrome で全 route × 2 viewport (1440×900 / 390×844) で走査。
      ドロワー (publish sidebar) はフォーカストラップが保持され、`Escape` で閉じ、
      トリガーにフォーカスが復帰。所見: 末尾 wrap 時に 1 step だけ `<body>` を経過
      (headless Chrome の通常挙動)、YouTube iframe にはグローバル focus ring が
      出ない (別 Issue 提案、README 参照)。

### T4 — content overflow

- [x] Table and card titles wrap and clamp to two lines.
- [x] Error and validation messages break inside their container.
- [x] The YouTube preview stays inside its panel.
- [x] Rendered check at 390px — evidence:
      `specs/74-responsive/evidence/2026-11-10/visual-index.json` の
      `home/chart/publish@390x844`。実 Chrome 描画で
      `document.scrollWidth == clientWidth` (横スクロール無し)、
      フォーカス可能要素は全 step で viewport 内
      (`tab-walk.json` の `inViewport` 全 true)。`clipped` 4 件は
      ノートグリッド stage (8 レーン × 56px = 448px) の**設計どおりの
      内部横スクロール**で、document は溢れない。

### T5 — verification

- [x] `npx tsc --noEmit` — clean.
- [x] `npm test` — `node --test tests/` and `vitest run`.
- [x] `npx vp check` — format and lint.
- [x] Dev server started locally with `FLASHBEAT_ADMIN_TOKEN=devtoken` to
      prove the application still boots (URL and port in the PR body).
- [x] Screenshots of the five viewports — evidence:
      `specs/74-responsive/evidence/2026-11-10/` の `*.png` (1440×900 /
      1280×720 / 1024×768 / 390×844 / 200% zoom 相当 720×450 × 3 route) と
      `visual-index.json`。`npm run visual` は **PASS (exit 0)**:
      horizontal overflow 無し、console error 無し。

## Files in this PR

| Path | Change |
|------|--------|
| `web/src/app/app-shell.tsx` | scroll ownership, block layout below `md` |
| `web/src/app/catalog-panel.tsx` | card list below `md`, wrapping titles |
| `web/src/app/drawer.tsx` | **new**: shared drawer backdrop + focus trap |
| `web/src/app/drawer.test.ts` | **new**: unit test of the focus trap |
| `web/src/app/panel.tsx` | scrollable frame, breakable error text |
| `web/src/app/sidebar.tsx` | sticky horizontal bar below `md` |
| `web/src/app/topbar.tsx` | fluid tool fields |
| `web/src/components/chart-editor/chart-editor.tsx` | inspector drawer, single instance |
| `web/src/components/chart-editor/chart-editor.test.ts` | **new**: the inspector is mounted once |
| `web/src/components/chart-editor/editor-toolbar.tsx` | fluid fields, drawer toggle |
| `web/src/components/chart-editor/note-grid.tsx` | focusable grid |
| `web/src/components/chart-editor/validation-panel.tsx` | bounded list |
| `web/src/components/chart-editor/youtube-preview.tsx` | bounded preview |
| `web/src/components/song-publisher/song-publisher.tsx` | inspector drawer, single instance |
| `web/src/components/song-publisher/song-publisher.test.ts` | **new**: the metadata form is mounted once |
| `web/src/styles/app.css` | document scroll, focus ring |
| `specs/74-responsive/**` | spec, plan, tasks |
