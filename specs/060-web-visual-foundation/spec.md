---
description: "FlashBeat lean per-issue specification template (project-local override of the Spec Kit core spec template)"
---

# Spec: Pen v5 tokens → Panda/Park mapping (web visual foundation)

**Issue**: #60 | **Branch**: `60` | **PR base**: `release-0-1-3` | **Date**: 2026-11-07 | **Status**: Merged

**PR**: [#90](https://github.com/rebuildup/FlashBeat/pull/90)
**実行 SHA（証跡撮影・検証の 기준）**: `2bd7302aac0419d80d662eaa4315253a2b36707f`
**マージコミット**: `5e4e43f9e4a482a79cf1ff5ed7f686c930c8bff5`
**before SHA（#60 適用直前）**: `54505c2998fb4e8c756e632ea48c396bd36dad88`（= `2bd7302^`）
**証跡**: [`docs/evidence/60-web-visual-foundation/README.md`](../../docs/evidence/60-web-visual-foundation/README.md)
（この証跡の出典・コマンド・画素サンプル・残存課題を記載）

**Input**: #60 + #61 (product spec, visual reference authority)

> Lean rules (enforced by `docs/agent/spec-kit-workflow.md`):
> one Issue = one branch = one worktree = one Draft PR; ≤5 tasks; no retrospective
> re-specification of the legacy game; no second authority for project governance.

## Objective

Every semantic color, surface depth and typography role rendered by the Web Studio
follows a documented translation of Pen v5 (`design/flashbeat-ui.pen` variables and
component layers) through Panda CSS / Park UI token roles. The independent Slate/Teal
brand (`#12A594`) no longer exists in the token system; accent is Pen `#3FA9F5`,
neutrals are the Pen gray ramp, status colors are Pen danger/warn/ok.

## Owned Paths

- `docs/design/web-token-mapping.md` — new; the primitive → semantic → component translation record.
- `web/panda.config.ts` — palette include list, semantic tokens (fg/border/canvas/status), font tokens.
- `web/src/theme/**` — palette files (`gray`, `blue` replacing `teal`, `amber` new, `red`, `green` anchors), global css (font stack).
- `specs/060-web-visual-foundation/**` — spec/plan/tasks/evidence.
- Mechanical token-rename only (`colorPalette: "teal"` → `"blue"`): `web/src/app/catalog-panel.tsx`, `web/src/components/chart-editor/editor-toolbar.tsx`, `web/src/components/song-publisher/{publish-dialog,song-publisher}.tsx`.

## Exclusions (explicitly out of scope)

- Screen layout / information architecture (`web/src/app/**` beyond the mechanical rename) — #69/#70.
- Brand mark / favicon / `web/public/**` — #67.
- Chart editor layout and publish flow behaviour — #72/#73.
- Unity scenes, Pen file edits, new component kits (no Tailwind/shadcn), light-mode redesign
  (the studio is dark-first; light values stay defined but vestigial).

## User Scenarios & Testing *(mandatory)*

### Scenario 1 — Token system renders Pen surfaces (P1)

**Given** the studio shell renders with the new theme, **When** any Park UI button, input,
select, list surface or focus ring renders, **Then** accent is `#3FA9F5`, canvas is `#1B1B1B`,
recessed surfaces use the Pen well ramp, raised controls use the Pen base ramp, and no
`#12A594` teal remains in rendered CSS.

### Scenario 2 — Typography role (P2)

**Given** Japanese and English text render in the studio, **When** any UI text renders,
**Then** the UI font stack leads with Noto Sans JP (self-hosted variable font) with system
Japanese fallbacks, and text hierarchy (default `#FFFFFF` / muted `#B0B0B0` / dim `#8C8C8C`)
matches Pen text roles.

### Scenario 3 — Verified rendering (P1)

**Given** the dev server runs, **When** the catalog screen is captured in a real browser at
1440×900 and 1280×720 before and after, **Then** after-images show the Pen palette applied
and evidence is committed under `specs/060-web-visual-foundation/evidence/`.

## Functional Requirements

1. `docs/design/web-token-mapping.md` records every Pen variable consumed, its Panda
   semantic role, and Pen variables deliberately not translated (pen-only layout tokens).
2. Panda semantic palettes: `blue` (accent `#3FA9F5`, anchor steps 9/10/11/12 from Pen
   `accent`/`accent-light`), `gray` (Pen neutral ramp incl. `well #131313`, `bg #1B1B1B`,
   `base #2D2D2D`), `red`/`green`/`amber` anchored to Pen `danger`/`ok`/`warn` incl.
   tint/light anchors. Park UI component APIs (solid/subtle/surface/outline/plain) unchanged.
3. Status meaning is not color-only at token level: focus ring, subtle/solid fg pairs keep
   ≥3:1 contrast on their dark surfaces (verified values recorded in the mapping doc).
4. `panda codegen`, `npx tsc --noEmit`, `npm test`, `vp build` pass after the change.

## Stakeholder Judgement Calls

- Accent palette name: `blue` (Pen accent hue ≈ 205°). Teal palette file is deleted, not
  recolored, so the name cannot silently return.
- Mechanical `colorPalette` rename touches 4 files owned by #70/#72/#73 — recorded as the
  handoff contract in the PR description.

## Rollback

Single revert of the PR merge commit restores the Slate/Teal theme.
