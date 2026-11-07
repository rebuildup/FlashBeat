# Tasks: Pen v5 tokens → Panda/Park mapping

**Issue**: #60 | **Branch**: `60` | **Base**: `release-0-1-3`
**Inputs**: [`spec.md`](spec.md), [`plan.md`](plan.md)

**Hard limit: at most 5 tasks per issue.**

## Tasks

- [ ] **T1** Write `docs/design/web-token-mapping.md` (Pen variable inventory → Panda
      semantic roles, derived-step rules, contrast checks) — done when every consumed Pen
      variable and every derived step is recorded.
- [ ] **T2** Palettes: `web/src/theme/colors/gray.ts` (Pen ramp), `blue.ts` (accent, teal.ts
      deleted), `amber.ts` (new), `red.ts`/`green.ts` (Pen status anchors);
      `web/panda.config.ts` (colorPalette include, semantic tokens, canvas/border, font
      stack via `web/src/theme/global-css.ts`, `@fontsource-variable/noto-sans-jp` in
      `web/package.json`) — done when `panda codegen` passes and no `teal` remains.
- [ ] **T3** Mechanical rename `colorPalette: "teal"` → `"blue"` in `catalog-panel.tsx`,
      `editor-toolbar.tsx`, `publish-dialog.tsx`, `song-publisher.tsx` — done when
      `npx tsc --noEmit` passes.
- [ ] **T4** Verify: `npm test`, `vp build`, before/after screenshots 1440×900 + 1280×720
      (+390) committed under `specs/060-web-visual-foundation/evidence/` — done when images
      show Pen palette and checks pass at the recorded SHA.
- [ ] **T5** Draft PR with mapping summary, evidence, handoff contract for #70/#72/#73 —
      done when PR URL recorded.

## Dependencies & Execution Order

1. T1 → T2 → T3 → T4 → T5 (T2/T3 both touch compile surface; keep ordered).

## Verification Evidence

| Task | Check | Command / Method | Expected result |
|------|-------|-------------------|-----------------|
| T2 | no teal left | `grep -ri teal web/src web/panda.config.ts` | 0 hits |
| T2 | codegen | `panda codegen` | exit 0 |
| T3 | types | `npx tsc --noEmit` | exit 0 |
| T4 | tests | `npm test` | all pass |
| T4 | build | `vp build` | exit 0 |
| T4 | pixels | Chrome headless 1440×900/1280×720/390×844 | Pen palette visible |

## Rollback

Revert the single PR merge commit.
