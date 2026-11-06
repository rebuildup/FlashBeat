# Plan: Catalog information design / density / row actions

**Issue**: #70 | **Branch**: `70` | **Base**: `67` (`9ad25d7`) | **Date**: 2026-11-07
**Spec**: [`specs/070-catalog-ux/spec.md`](spec.md)

## Summary

Rebuild `catalog-panel.tsx` from a capped, display-only grid into a full-width list of
row links (Pen `SongRow` translation: title 500 weight + artist muted, radius,
hairline separators, recessed panel bg = Pen `well #131313`). Keep the existing
`loadCatalog`/`?state=` contract; add the filter-no-match state locally.

## Owned Paths

```text
web/src/app/catalog-panel.tsx   (rewritten)
web/src/app/panel.tsx           (panelFrame recessed bg, StateBlock unchanged API)
specs/070-catalog-ux/**
```

**Structure Decision**: no new catalog-* file — the panel is one cohesive component;
a second file would split row styles from state styles for no benefit.

## Dependencies & Gate

- **Depends on**: #68/#71 (real catalog + exact chart loading) — in trunk; #60/#67 stacked.
- **Blocks**: #72/#73 consume the row link target format (unchanged: `/chart?songId&version`).
- **Gate**: tsc/tests/build; density evidence with 12+ locally-published songs via the
  real admin API; before/after screenshots 1440×900/1280×720/390.

## Density test data

Local D1 only: publish 11 derived songs (`POST`/`PUT api/v1/admin/songs/<id>` with
`FLASHBEAT_ADMIN_TOKEN` dev token) — long JP titles, varied levels/BPM/notes. Never
touches remote D1.
