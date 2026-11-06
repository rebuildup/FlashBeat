---
description: "FlashBeat lean per-issue specification template (project-local override of the Spec Kit core spec template)"
---

# Spec: Catalog information design, density, row actions, empty states

**Issue**: #70 | **Branch**: `70` (from `67`) | **PR base**: `67` | **Date**: 2026-11-07 | **Status**: Draft

## Objective

The catalog is a full-width working list: with 12+ songs an operator can scan
title/artist/level/BPM/notes/version in one gaze, reach any row by keyboard, and open
the real chart with one click. The 52rem non-interactive table, 2xs type and dead
right half of the screen are gone. Meaningless filler cards are not added.

## Owned Paths

- `web/src/app/catalog-panel.tsx` — rebuilt: full-width row-link table, states.
- `web/src/app/panel.tsx` — shared frame (recessed surface per Pen `well`).
- `web/src/app/catalog-*.tsx` new files if needed.
- `specs/070-catalog-ux/**`

Topbar/shell stay #nav-owned (merged; only consumed). `?state=` preview contract unchanged.

## Exclusions (explicitly out of scope)

- New-chart creation flow (#72/#73), publish flow, API shapes (`SongSummary` unchanged),
  responsive overhaul (#74 — narrow viewport must not regress below current, but the
  390px layout rework is #74), Unity.

## User Scenarios & Testing *(mandatory)*

### Scenario 1 — Scan and open (P1)

**Given** 12+ published songs (seeded locally through the real publish API), **When**
the catalog renders at 1440×900, **Then** every row shows title (JP-safe), artist,
level, BPM, note count, version in aligned tabular numerals across the content width,
one row is one link (`/chart?songId=…&version=…`), hover/focus feedback marks the row,
and Enter opens the chart.

### Scenario 2 — States (P1)

**Given** the `?state=loading|empty|error` previews and live filters, **When** each
renders, **Then** loading, empty (nothing published), error (retry preserves), and
filter-no-match (query/level with 0 hits, offering filter reset) are visually distinct
and honest; no fake success.

### Scenario 3 — Density (P2)

**Given** the same list at 1440×900 and 1280×720, **Then** rows keep a readable
56px-class rhythm, no clipped columns at either width, and the level chip is legible
(token-level `2xs` audit applied to this screen).

## Functional Requirements

1. Row click area = whole row; keyboard: rows are anchors (`Tab`, `Enter`), visual
   focus ring per #60 tokens.
2. Numbers tabular-nums; column alignment consistent; no truncated numeric columns.
3. Long Japanese titles/artists ellipsize without breaking the row grid.
4. Header labels sentence case (no uppercase wide-tracking slop), fg.subtle.
