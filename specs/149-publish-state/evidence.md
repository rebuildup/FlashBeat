# Evidence: Publish loading / error states (#149)

**Issue**: #149 | **Candidate SHA**: `fbe58104209549565b959e7dfab561ae68f892e0` (`origin/main`)
**Reviewed**: 2026-11-08

The implementation of T1–T3 already lives in `web/pages/publish.tsx` on `main`
(`PublishState` component with `kind="loading" | "error"`). This document records
the verification evidence for T4 (web checks) and T5 (browser pixels).

## T4 — Web checks

Run in `web/` on the candidate SHA:

| Command | Result |
|---|---|
| `npx tsc --noEmit` | exit 0 |
| `npx vp build` | exit 0 (`✓ built in 2.56s`, `.cloudflare/output/v0/workers/default`) |
| `npx vitest run` | exit 0 — **28 files / 220 tests passed** |

## T5 — Browser pixels

Captured with Chrome DevTools MCP against the local dev server
(`FLASHBEAT_ADMIN_TOKEN=dummy npx vp dev --port 4311`) at
`http://localhost:4311/publish?songId=does-not-exist&version=1`, i.e. a chart the
Catalog does not know.

| File | Viewport | SHA-256 |
|---|---|---|
| `screenshots/publish-loading-1440x900.png` | 1440×900 | `a35ebdddc831888cc161c8de23ac153f38ea17c7cdf0284416d718ee15b9182a` |
| `screenshots/publish-loading-390x844.png` | 390×844 | `e4f0f4b2b7654f3ef9283fe939b3d8132b3a11891d21095d21a0da3696780f68` |
| `screenshots/publish-error-1440x900.png` | 1440×900 | `8fbcca29197254cf1a720805c936fa06174aae173b21530668d14668ac28b1c0` |
| `screenshots/publish-error-1280x720.png` | 1280×720 | `30feb06db0b2082c887333f6656a7c16020ef6092b5d5245b6f7b9e76cc105a9` |
| `screenshots/publish-error-390x844.png` | 390×844 | `4f5978636a74ccd72bdefa898f0b69d7e9a953035b7a8b65f843fd9c653f9e87` |

### Accessibility tree (a11y snapshot, same URL)

Loading state:

- container `role="status"` `aria-busy="true"`
- heading 「譜面を読み込み中」
- body 「公開対象を確認しています」 / 「公開対象の楽曲と譜面バージョンを確認しています。」

Error state:

- container `role="alert"`
- heading 「譜面を読み込めませんでした」
- body 「読み込みに失敗しました」
- message `Song no longer available in Catalog.`
- button 「再試行」
- link 「← Catalog に戻る」

### Review against the design skill

- Both states render **inside the product Studio shell** (BrandHeader + navigation),
  not as raw unstyled HTML.
- The panel uses the v5 surface tokens; no raw-HTML fallback, no horizontal
  overflow at 390×844 (the header navigation collapses to a single scrollable row).
- The error path offers exactly two exits: 「再試行」 (button) and
  「← Catalog に戻る」 (link) — both keyboard reachable, neither is a trap.

## Handoff to #75 (visual gate)

The visual gate for the Web exhibition remains open in #75. This PR only closes
the `#149` sub-path: the publish loading/error states are implemented, checked and
captured. The screenshots above are the pixels the #75 gate should compare against
for the publish route.

## Caveat

The screenshots come from the **local dev server** (`vp dev --port 4311`) on the
candidate SHA, not from the deployed exhibition environment. The deployed release
still needs its own capture before #75 can be closed.
