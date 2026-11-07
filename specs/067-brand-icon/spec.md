---
description: "FlashBeat lean per-issue specification template (project-local override of the Spec Kit core spec template)"
---

# Spec: Real FlashBeat app icon / brand mark / favicon

**Issue**: #67 | **Branch**: `67` (from `60`) | **PR base**: `60` | **Date**: 2026-11-07 | **Status**: Draft

## Objective

The browser tab, brand header and brand assets of the Web Studio show the real
Unity app icon (`Assets/Game/Art/Images/flashIcon.png`, LFS oid `377707ea…`,
1340×1340 RGBA). The placeholder `FB` tile and the placeholder teal `favicon.svg`
are gone; no invented brand badge exists anywhere.

## Owned Paths

- `web/public/flashbeat-icon-{32,64,180,192,512}.png` — new; real icon resizes.
- `web/public/favicon.svg` — deleted (was a placeholder teal FB tile).
- `web/src/components/brand/**` — new; `BrandHeader` + `brandIconSrc` contract.
- `web/void.config.ts` — `head.link` favicon wiring only.
- `web/src/app/sidebar.tsx` — placeholder tile → `BrandHeader` (nav owner #69 is
  merged; this is the stacked continuation, contract documented in brand-header.tsx).
- `specs/067-brand-icon/**`

## Exclusions (explicitly out of scope)

- Nav structure/routes (#69, merged), token values (#60), Unity assets, marketing
  assets, iOS/Android app icons, Cloudflare deploy.

## User Scenarios & Testing *(mandatory)*

### Scenario 1 — Tab icon (P1)

**Given** the studio is opened in a browser, **When** the tab renders, **Then** the
favicon is the real FlashBeat icon (`/flashbeat-icon-32.png`, `192`, apple-touch
`180`) and `favicon.svg` no longer exists.

### Scenario 2 — Brand header (P1)

**Given** the studio shell renders, **When** the sidebar brand slot renders,
**Then** it shows the real icon image at 32px (64px asset, 2× crisp) with the
"FlashBeat / Web Studio" wordmark — no `FB` text tile.

### Scenario 3 — Asset integrity (P1)

**Given** the resized assets, **When** inspected at 32/64/192 px on dark and light
backgrounds, **Then** transparency is preserved (no white/black halo), aspect ratio
is 1:1, and the mark is legible at 32px.

## Functional Requirements

1. Assets derive from the LFS object (sha256 `377707ea…`), never redrawn/reinvented.
2. `BrandHeader` never recolors the mark; missing asset renders as broken image.
3. `npx tsc --noEmit`, `npm test`, `vp build` pass.

## Rollback

Single revert of the PR merge commit.
