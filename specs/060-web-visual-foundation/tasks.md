# Tasks: Pen v5 tokens → Panda/Park mapping

**Issue**: #60 | **Branch**: `60` | **Base**: `release-0-1-3`
**Inputs**: [`spec.md`](spec.md), [`plan.md`](plan.md)

**PR**: [#90](https://github.com/rebuildup/FlashBeat/pull/90)
**実行 SHA**: `2bd7302aac0419d80d662eaa4315253a2b36707f`（PR #90 の head。検証・証跡撮影の基準）
**マージコミット**: `5e4e43f9e4a482a79cf1ff5ed7f686c930c8bff5`
**証跡**: [`docs/evidence/60-web-visual-foundation/README.md`](../../docs/evidence/60-web-visual-foundation/README.md)

**Hard limit: at most 5 tasks per issue.**

## Tasks

- [x] **T1** Write `docs/design/web-token-mapping.md` (Pen variable inventory → Panda
      semantic roles, derived-step rules, contrast checks) — done when every consumed Pen
      variable and every derived step is recorded.
      → §1 中性ランプ / §2 accent / §3 status / §4 タイポグラフィ / §5 コンポーネント層 /
      §6 残存差分 / 再生成スクリプト を収録済み。
- [x] **T2** Palettes: `web/src/theme/colors/gray.ts` (Pen ramp), `blue.ts` (accent, teal.ts
      deleted), `amber.ts` (new), `red.ts`/`green.ts` (Pen status anchors);
      `web/panda.config.ts` (colorPalette include, semantic tokens, canvas/border, font
      stack via `web/src/theme/global-css.ts`, `@fontsource-variable/noto-sans-jp` in
      `web/package.json`) — done when `panda codegen` passes and no `teal` remains.
      → `panda codegen` exit 0。`grep -rin teal src panda.config.ts` は 0 件
      （`src/theme/colors/` は gray/blue/amber/red/green のみ、teal.ts は削除済み）。
      **残存(撮影 SHA 時点)**: `src/components/chart-editor/note-grid.tsx:32` の
      `NOTE_FILL = "#12a594"` はハードコードのままであり、PR #90 の handoff contract で
      #72 のスコープと明記していた。**その後 `41d7da1` (#72) で `NOTE_FILL = "#ededed"` に
      差し替わり、`main` に teal ハードコードは残っていない。**
- [x] **T3** Mechanical rename `colorPalette: "teal"` → `"blue"` in `catalog-panel.tsx`,
      `editor-toolbar.tsx`, `publish-dialog.tsx`, `song-publisher.tsx` — done when
      `npx tsc --noEmit` passes.
      → 実行 SHA `2bd7302` で `npx tsc --noEmit` exit 0。
- [x] **T4** Verify: `npm test`, `vp build`, before/after screenshots 1440×900 + 1280×720
      (+390) committed under `specs/060-web-visual-foundation/evidence/` — done when images
      show Pen palette and checks pass at the recorded SHA.
      → 実行 SHA `2bd7302` で `npm test`（node --test 19 pass / vitest 11 files 93 tests 0 fail）、
      `npx vp build` exit 0。before/after 画素証跡は
      `specs/060-web-visual-foundation/evidence/pixel/{before-54505c2,after-2bd7302}/` に
      1440×900 / 1280×720 / 390×844 をコミット済み。画素サンプリングで
      `Export` / `Review publish` ボタンが `#12A594` → `#3FA9F5` に変わったことを確認。
      **注記**: 本 Issue 既存の `before-ready-*` / `after2-*` はどちらも Pen パレットで
      accent の差分を証明しないため、本 Issue で `54505c2`（旧 teal テーマ）と `2bd7302`
      の 2 つの SHA で実機 Chrome により再撮影した。手順は
      `docs/evidence/60-web-visual-foundation/README.md`。
- [x] **T5** Draft PR with mapping summary, evidence, handoff contract for #70/#72/#73 —
      done when PR URL recorded.
      → PR URL: https://github.com/rebuildup/FlashBeat/pull/90
      （head SHA `2bd7302aac0419d80d662eaa4315253a2b36707f`、マージコミット
      `5e4e43f9e4a482a79cf1ff5ed7f686c930c8bff5`、2026-11-06 マージ済み）。
      PR 本文にマッピング要約・証跡・handoff contract を記載済み。
      **注記**: PR #90 は governance 上は Draft PR の想定だったが、実際には Draft 状態を
      経由せず（`isDraft: false`）2026-11-06 にマージされている。done-when の
      「PR URL recorded」は URL の記録により充足。

## Dependencies & Execution Order

1. T1 → T2 → T3 → T4 → T5 (T2/T3 both touch compile surface; keep ordered).

## Verification Evidence

| Task | Check | Command / Method | Expected result | 実測 |
|------|-------|-------------------|-----------------|------|
| T2 | no teal left | `grep -ri teal web/src web/panda.config.ts` | 0 hits | 0 hits（`2bd7302` で実測） |
| T2 | codegen | `panda codegen` | exit 0 | exit 0 |
| T3 | types | `npx tsc --noEmit` | exit 0 | exit 0 |
| T4 | tests | `npm test` | all pass | node --test 19 pass / vitest 93 tests 0 fail |
| T4 | build | `vp build` | exit 0 | exit 0 |
| T4 | pixels | Chrome headless 1440×900/1280×720/390×844 | Pen palette visible | `evidence/pixel/` に before/after を収録。`#12A594` → `#3FA9F5` を確認 |
| T5 | PR URL recorded | `gh pr view 90 --json url,headRefOid,mergeCommit` | URL あり | https://github.com/rebuildup/FlashBeat/pull/90 |

## Rollback

Revert the single PR merge commit `5e4e43f9e4a482a79cf1ff5ed7f686c930c8bff5`.
