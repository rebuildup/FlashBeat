# Plan: Real FlashBeat app icon / brand mark / favicon

**Issue**: #67 | **Branch**: `67` | **Base**: `60` (`2bd7302`) | **Date**: 2026-11-07
**Spec**: [`specs/067-brand-icon/spec.md`](spec.md)

## Summary

Fetch the real `flashIcon.png` LFS object (targeted `git lfs fetch --include`, no
repo-wide pull), resize with `sips` (alpha-preserving) into `web/public/`, delete the
placeholder favicon.svg, expose a `BrandHeader` component under
`web/src/components/brand/`, and swap the sidebar's fake `FB` tile for it.

## Owned Paths

```text
web/public/flashbeat-icon-{32,64,180,192,512}.png   (new)
web/void.config.ts                                  (head.link only)
web/src/components/brand/{brand-header.tsx,index.ts} (new)
web/src/app/sidebar.tsx                             (brand slot + dead style removal)
specs/067-brand-icon/**
```

**Structure Decision**: assets are plain public PNGs (browser caches, no bundler
processing); the brand component owns display size and never recolors the mark.

## Dependencies & Gate

- **Depends on**: #60 (token roles for header text colors), stacked at `2bd7302`.
- **Blocks**: nothing asset-wise; #69 sidebar contract already merged — `BrandHeader`
  is the documented import for any future nav rework.
- **Gate**: tsc/tests/build pass; favicon links present in served HTML; pixel evidence.

## Constitution Check

- Authority docs unchanged; only listed paths mutated; no deploy.

## Notes

- `favicon-32.png` intermediate was removed; the canonical set is `flashbeat-icon-*`.
- Icon hue (Unity blue) intentionally differs from Pen accent `#3FA9F5` — the mark is
  brand art, not a UI token; it is not recolored to match (per issue: respect the
  brand expression of the Pen/Unity icon).
