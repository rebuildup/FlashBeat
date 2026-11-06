# Plan: Pen v5 tokens → Panda/Park mapping

**Issue**: #60 | **Branch**: `60` | **Base**: `release-0-1-3` | **Date**: 2026-11-07
**Spec**: [`specs/060-web-visual-foundation/spec.md`](spec.md)

## Summary

Replace the Park UI Slate/Teal palettes with a Pen-derived token set while keeping the
Park UI palette API (12 steps + alpha steps + solid/subtle/surface/outline/plain semantic
pairs) intact. Pen v5 defines named colors (`bg #1B1B1B`, `accent #3FA9F5`, `text*`,
`well`, `base`), neutral ramps (`g1–g19`, `d1–d4`, `u1–u5`, `k0–k6`, `r1–r3`) and status
triples (danger/warn/ok + tint/light) — not 12-step ramps — so the mapping doc records
which Pen value anchors each Panda step and which steps are derived (hue-consistent
interpolation, documented, deterministic).

## Owned Paths

```text
docs/design/web-token-mapping.md
web/panda.config.ts
web/src/theme/colors/{gray,blue,amber,red,green}.ts  (teal.ts deleted)
web/src/theme/global-css.ts
web/package.json (+@fontsource-variable/noto-sans-jp), package-lock.json
web/src/app/catalog-panel.tsx                        (mechanical teal→blue rename only)
web/src/components/chart-editor/editor-toolbar.tsx   (mechanical teal→blue rename only)
web/src/components/song-publisher/{publish-dialog,song-publisher}.tsx (same)
specs/060-web-visual-foundation/**
```

**Structure Decision**: palettes stay Park-shaped (`defineSemanticTokens.colors`) so every
existing Park UI recipe keeps working; only values and the accent name change.

## Dependencies & Gate

- **Depends on**: none (foundation). Trunk `release-0-1-3` already carries #69/#71.
- **Blocks**: #67 (brand header uses token roles), #70 (catalog uses type/surface roles),
  #72/#73 (mechanical `teal`→`blue` rename is their handoff contract).
- **Gate**: `panda codegen` + `npx tsc --noEmit` + `npm test` + `vp build` pass; before/after
  screenshots at 1440×900/1280×720 committed; no `#12A594`/`teal` left in `web/src`.

## Exclusions

- Screen layout/density (#70), brand assets (#67), editor/publish behaviour (#72/#73),
  responsive pass (#74), production deploy.

## Constitution Check

- Authority docs unchanged.
- Mutable ownership: palettes + panda.config are #60-exclusive; the four `colorPalette`
  rename lines in other issues' files are mechanical, listed in the PR as handoff contract.
- No deploy; local dev only.

## Design decision record (short)

- Accent name `blue` (hue ≈205° from `#3FA9F5`); `teal.ts` deleted so the old brand cannot
  silently return.
- Pen anchors: `blue.9=#3FA9F5`, `blue.10` derived hover (lightness +6), `blue.11=#9AD3FA`
  (Pen `accent-light`, accent-as-text), `blue.12` derived near-white; subtle bg anchors on
  Pen `accent-tint #2B4457` (alpha-equivalent over canvas verified in mapping doc).
- `gray` maps Pen ramp: 1 `well #131313` (recessed), 2 `bg #1B1B1B` (canvas), 6 `base
  #2D2D2D` (raised controls), 10/11/12 = `text-dim #8C8C8C` / `text-muted #B0B0B0` /
  `text #FFFFFF`.
- Status: red.9 `#FF6B6B`, red.11 `#FFA8A8`; green.9 `#4CC38A`, green.11 `#8FE0B8`;
  amber.9 `#E9B949`, amber.11 `#F4D88A`; Pen `*-tint` values ≙ subtle bg (alpha ≈0.17 over
  canvas, verified in mapping doc).
- Typography: self-hosted Noto Sans JP variable font (`@fontsource-variable/noto-sans-jp`),
  global stack `"Noto Sans JP Variable", "Noto Sans JP", Hiragino Kaku Gothic ProN,
  "Yu Gothic", system-ui, sans-serif`; text colors from Pen text roles.
