# Tasks: Real FlashBeat app icon / brand mark / favicon

**Issue**: #67 | **Branch**: `67` | **Base**: `60` (`2bd7302`)
**Inputs**: [`spec.md`](spec.md), [`plan.md`](plan.md)

## Tasks

- [ ] **T1** Verify LFS object (sha256 `377707ea…`, 1340×1340 RGBA) and resize to
      `web/public/flashbeat-icon-{32,64,180,192,512}.png`; delete placeholder
      `web/public/favicon.svg` — done when `sips -g` reports exact sizes + alpha.
- [ ] **T2** Wire favicons in `web/void.config.ts` head.link (32/192/apple-touch-180)
      — done when served HTML carries the links and assets resolve 200.
- [ ] **T3** `web/src/components/brand/**` (`BrandHeader`, `brandIconSrc`) and
      sidebar placeholder tile replacement — done when shell renders the real mark.
- [ ] **T4** Pixel evidence: studio screenshots 1440×900 + icon strip 32/64/192 on
      dark/light → `specs/067-brand-icon/evidence/` — done when legible at 32px.
- [ ] **T5** `npx tsc --noEmit` + `npm test` + `vp build` at recorded SHA; Draft PR.

## Verification Evidence

| Task | Check | Command | Expected |
|------|-------|---------|----------|
| T1 | asset sizes | `sips -g pixelWidth -g hasAlpha web/public/flashbeat-icon-*.png` | exact px, alpha yes |
| T2 | favicon links | `curl -s localhost:5174/ \| grep flashbeat-icon` | 3 links |
| T3 | types | `npx tsc --noEmit` | exit 0 |
| T5 | tests/build | `npm test` / `vp build` | 19+93 pass / build ok |

## Rollback

Revert the PR merge commit.
