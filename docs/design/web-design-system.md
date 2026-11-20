# Web Studio design system (issue #802)

The visual contract of `web/` — grid, typography, tokens, primitives and the
mechanisms that keep them true. Pen v5 remains the visual source of truth
(`design/flashbeat-ui.pen`, mapping in `web-token-mapping.md`); this document
records how the *web app* layers its own system on top.

## 1. Grid and layout

The studio is a two-region shell: a navigation rail on the start side and a
work area next to it.

| Region | Width | Notes |
|---|---|---|
| Rail (sidebar) | `13rem` (`208px`) fixed track | `gridTemplateColumns: "13rem minmax(0, 1fr)"` on `md+` in both `app-shell.tsx` and `studio-layout.tsx` |
| Work area | `minmax(0, 1fr)` | owns its own vertical scroll (`overflow-y: auto`); the document does not scroll on `md+` |
| Narrow (`< md`) | single column | the rail becomes a sticky top bar / disclosure; content padding `p-4` |

Breakpoints are Panda's defaults (`sm 640 / md 768 / lg 1024 / xl 1280 /
2xl 1536`). The catalog list switches from cards to the table at `xl`
(~832px of fixed tracks + padding must fit beside the rail — issue #515).

**Enforced**: `src/theme/design-system.test.ts` fails if either shell stops
using the shared `13rem minmax(0, 1fr)` rail track.

## 2. Semantic color tokens

`src/theme/tokens/semantic-colors.ts` is the role layer; `src/theme/colors/**`
(Park UI palettes, Pen-anchored) is the hue layer. Reach for roles first:

| Token | Role |
|---|---|
| `fg.default` / `fg.muted` / `fg.subtle` | primary / secondary / tertiary text (Pen `text` / `text-muted` / `text-dim`) |
| `border` | hairlines (dark `gray.6`, light `gray.4`) |
| `canvas` | application background (Pen `bg #1B1B1B`) |
| `error` | error accent (`red.9`) |
| `cta.bg` / `cta.fg` | the one solid CTA class (Pen `Prefab/Button Primary` `#EDEDED`/`#1B1B1B`) |

Palette steps (`gray.8`, `blue.11`, `red.9`, `amber.9`, …) are for cases no
role covers — chip fills, hover washes, accent text. Accent blue is never a
filled button.

**Enforced**: the guard test rejects `#rrggbb`/`#rrggbbaa` literals in
`src/app`, `pages` and the feature components (`new-song`, `song-publisher`,
`welcome`, `brand`). `chart-editor` (Konva canvas paint) and generated
`components/ui` are exempt.

## 3. Typography scale

Two layers (`src/theme/text-styles.ts`):

- **Steps** `2xs`–`7xl` (Panda scale) for one-off sizes.
- **Semantic styles** for the combinations screens actually use:

| Style | Use |
|---|---|
| `pageTitle` | page heading (`PageHeader`, catalog topbar) — `xl` semibold |
| `sectionTitle` | hero/section heading — `lg` semibold |
| `panelTitle` | panel/card titles, compact page-header band, state blocks — `sm` semibold |
| `rowTitle` | primary line of a dense list row — `md` medium (**16px floor**, per the v0.2.3 "text too small" finding) |
| `rowMeta` | secondary line / page subtitle — `sm` |
| `caption` | smallest readable meta (timestamps, hints, badges) — `xs` |
| `numeric` | `tabular-nums` for aligned numeric columns |

Applied via `css({ textStyle: "…" })` or the `textStyle` JSX prop.

**Enforced**: the guard test rejects `fontSize` values that are not scale
steps (raw `px`/`rem`/numbers) in the guarded directories.

## 4. Spacing, density, radii

- Spacing uses Panda's numeric scale (`p-4` = `1rem`); component padding is
  `2`–`6`, section gaps `3`–`5`.
- Radii: `l1` (chips, small controls), `l2` (panels, buttons, inputs),
  `l3`, `full` (pills).
- Density: list rows are `min-h-14` (catalog, 3rem thumbnail) / `min-h-12`+;
  numeric columns are right-aligned `tabular-nums`, `sm`.

## 5. Component primitives

| Module | Owns |
|---|---|
| `src/app/panel.tsx` | surfaces: `panelFrame`, `panelHeader/Title/Subtitle`, `panelBody(Flush)`, `StateBlock`, page-header band, `primaryCta` |
| `src/app/list.tsx` | list atoms: `Badge` (neutral/muted/outline), `num`/`numWide` cells, `metaLine`/`metaText`, `rowActions`/`actionLink(Subtle)`, `keyCap`, `visuallyHidden` |
| `src/app/page-header.tsx`, `topbar.tsx` | page heading band / catalog toolbar |
| `src/components/ui/**` | Park UI primitives (Button, Field, Input, Select, Dialog, Tabs, Toast) — **use these before writing custom controls** |

Rules: no per-screen token forks, no near-duplicate components with unclear
names, no ad-hoc styling where a primitive exists. Badge tone classes are
complete variants — never `cx` two classes that set the same property
(atomic order is stylesheet order, not class order).

## 6. Song lists

Both lists share `list.tsx` atoms:

- **Catalog** (`catalog-panel.tsx`): cards below `xl`, table at `xl+`.
  Columns: jacket (16:9, 3rem) · title (`rowTitle`) · artist (`metaText`) ·
  Lv chip · BPM · time (`lg+`) · notes · version · actions. Whole row is the
  primary action (stretched link → `/chart`); secondary actions are ghost
  links above it. Numeric columns right-aligned tabular; below `xl` the
  values fold into a meta line instead of shrinking the table.
- **Songs** (`songs-panel.tsx`, staff archive): same row grammar plus a
  labeled header row (曲 / プレイ / 最高スコア / Ver); archived state is a
  muted `Badge`; archive/restore stay Park UI `Button`s.

States (loading / empty / error / offline / no-filter-match) all render
through `StateBlock` inside the same `panelFrame`, so layout does not jump.

## 7. Image review as verification

`npm run visual` (`tests/visual/capture.mjs`) drives real headless Chromium
over the studio routes at 1920/1440/1280/1024/768/390(+200% zoom) and writes
screenshots + measured DOM facts. DOM PASS is **not** a visual PASS — the
PNGs must be inspected. Evidence for this issue lives in
`docs/evidence/web-802/` (before = production `flashbeat.rebuildup.dev`,
after = local dev server).
