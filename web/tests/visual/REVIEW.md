# Visual review checklist (issue #802)

`index.json` measures the DOM; it is **not** a visual PASS. A reviewer —
human or model — applies this checklist to the PNGs in each capture
directory (`out/<timestamp>/`, `*.png` viewport + `*.full.png` below-fold).
DOM-clean output with an unreviewed screenshot is not evidence.

## Required coverage

- Widths: **390, 768, 1440, 1920** at minimum (the capture set also covers
  1280, 1024 and a 200%-zoom equivalent).
- Routes: `/` (catalog), `/chart`, `/new-song`, `/publish`, `/dashboard`,
  `/songs`.
- Before/after pairs live in `docs/evidence/web-802/`.

## Per screenshot

1. **Layout** — rail/content relationship intact; no element clipped or
   overflowing the viewport; cards↔table switch happens at `xl` without a
   broken intermediate state (check 1024/1280 vs 1440).
2. **Typography** — row titles are the `rowTitle` step (16px medium), meta
   lines readable, no text under 12px except intentional `2xs` badges;
   numeric columns right-aligned and tabular.
3. **Spacing & density** — row heights ~4rem, consistent gutters, panels
   sit in the recessed well (`gray.1`) on the canvas.
4. **Color & contrast** — fg/border/canvas roles only; no stray hues; CTA
   is the single light `cta` class; accent blue only on links/focus/selected.
5. **States** — loading/empty/error/hover/focus-visible render through the
   shared primitives (`StateBlock`, `Badge`, `actionLink`); focus ring
   visible (`focus1`/`focus3` shots at 1440).
6. **Operations** — primary action per row is obvious (stretched link);
   secondary actions are visually quieter; mobile shows the card layout,
   not a shrunken table.

## Recording the review

For each reviewed capture note: file name, verdict (pass / fix needed),
and the concrete defect. Fixes land in the same PR; the after-shots are
re-captured and the diff is listed in `docs/evidence/web-802/README.md`.
