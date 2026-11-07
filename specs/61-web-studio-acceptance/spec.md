# Spec: Web Studio 受け入れ監査 (実操作ベース)

**Issue**: #61 | **Branch**: `w-61` | **PR base**: `main` | **Date**: 2026-11-08 | **Status**: Draft

**Input**: Web (https://flashbeat.rebuildup.dev) を展示で「ブラウザだけで譜面を作って公開する」
導線として使う。画面上のボタン・タブ・設定の実機能が未確認で、「動かない見せかけの UI」が
残っていれば展示で信用を失う。実ブラウザ (Playwright) で全画面・全タブ・全ボタンを実際に
操作し、壊れているものを洗い出して直す。

> Lean rules: one Issue = one branch = one worktree = one Draft PR; ≤5 tasks;
> no retrospective re-specification of the legacy game.

## Objective

Studio の全ルート (`/`, `/chart`, `/publish`, `/new-song`) の全操作要素が実機能を持つこと。
Playwright による実操作監査 (3 解像度) で証跡を残し、見つかった不具合のうち
自分の所有ファイル範囲内のものは実装で直す。

## Owned Paths

- `web/src/app/**` — シェル・Catalog・topbar・drawer・panel
- `web/src/components/ui/**` — Park UI コンポーネント層
- `web/src/components/song-publisher/**` — Publish 画面コンポーネント
- `web/src/components/catalog/**` — 存在する場合のみ (現状なし、Catalog は `app/` 配下)
- `docs/evidence/web-acceptance/**` — 監査の操作ログ・スクリーンショット・README
- `specs/61-web-studio-acceptance/**`

## Exclusions (explicitly out of scope)

- `web/src/components/chart-editor/**`, `web/src/lib/chart/**` — 他 Worker 排他。触らない。
- 本番 API への書き込み (Publish) / Cloudflare デプロイ / DB 書き込み — 禁止。
  読み取り (Catalog 取得) は可。
- Unity 側 (`Assets/**`) — 対象外。

## User Scenarios & Testing

### Scenario 1 — 実操作監査の完走 (P1)

**Given** ローカル dev server が seed 済み D1 で起動している, **When** Playwright で
4 ルートの全ボタン・タブ・設定 (検索 / レベルフィルタ / ソート / スナップ / タブ切替 /
ダイアログ開閉 / フォーム入力) を 1920×1080 / 2560×1440 / 2560×1664 の 3 解像度で
実際に操作する, **Then** 「押せるのに何もしない」「リンクが死んでいる」「エラーが出る」
要素が一覧化され、操作ログとスクリーンショットが `docs/evidence/web-acceptance/` に残る。

**Independent Test**: `cd web && void db migrate && void db seed && vp dev` +
`E2E_BASE_URL=... npx playwright test -c tests/e2e/playwright.config.ts` (監査 spec)。

### Scenario 2 — 壊れた UI の実修正 (P1)

**Given** 監査で「動かない UI」が見つかった, **When** その原因が Owned Paths 内なら
実装で直す (受け皿だけのダミー表示ではなく実動作に繋ぐ), **Then** 最低 1 つの実修正が
本 PR の差分に入り、監査が再実行されて當該項目が解消される。

**Independent Test**: 修正対象に応じた unit test (vitest) + 監査 spec の再実行。

### Scenario 3 — 導線の実 API 完走 (P1)

**Given** ローカル dev server に対して, **When** Catalog → 譜面編集 → 検証 → Publish →
再取得を操作する, **Then** 実 API (ローカル D1) で完走する。本番への書き込みはしない。

**Independent Test**: 既存 `web/tests/e2e/studio-flow.spec.ts` (Catalog→Editor→Publish→
Catalog 戻り) をローカル dev server に対して実行。

### Scenario 4 — 3 解像度のレイアウト (P2)

**Given** 1920×1080 / 2560×1440 / 2560×1664, **When** 4 ルートを開く, **Then**
横スクロール・レイアウト崩れがない。

**Independent Test**: 監査 spec の解像度ループ + スクリーンショット。

## Requirements

- **R1**: 監査は実ブラウザ操作 (Playwright) で行い、解像度 3 種 × 4 ルートの
  スクリーンショットと操作ログを `docs/evidence/web-acceptance/` に残す。
- **R2**: 見つかった「動かない UI」のうち Owned Paths 内のものは実装で直す
  (最低 1 修正)。Owned Paths 外のものは `specs/61-web-studio-acceptance/spec.md` の
  Known Issues に記録して報告する。
- **R3**: `web/` の `npm test` / `npm run build` を green に維持する。
- **R4**: 本番 API への書き込みをしない。監査・検証はローカル dev server に対して行う。

## Known Issues

- 監査で見つかり本 PR で修正: Catalog テーブル行の旧 `rowBody` 残留
  (#156 マージ由来、タイトル/アーティストの二重描画 + 列ズレ)。
  `web/src/app/catalog-panel.tsx` を修正し、回帰テスト
  (`web/src/app/catalog-panel.test.tsx`, 5 件) を追加。
- 監査で見つかり本 PR で修正: `tests/e2e/studio-flow.spec.ts` の行リンク locator が
  md+ で非表示のカードビューを掴み spec が壊れていた (可視側テーブルに限定)。
- Owned Paths 外のため修正せず記録のみ: なし
  (chart-editor / lib/chart 側の UI はシェルレベルの死活監査のみ実施し、
  不具合は検出されなかった)。
- 環境メモ: `vp dev` 起動直後に `/publish`・`/new-song` が一時的に空になる状態を
  1 回観測 (再読込で解消、本番ビルドでは非再現)。アプリコードの不具合とは判断しない。

## Dependencies & Gate

- **Depends on**: なし (base `origin/fix/main-sync-014` = release-0-1-14 + #154 修復 +
  Web テスト同期 の上で完結)。chart-editor / lib/chart は他 Worker — 呼ぶだけ。
- **Blocks**: なし。
- **Gate**: `npm test` / `npm run build` が緑。監査 spec が 3 解像度で完走し、
  証跡が `docs/evidence/web-acceptance/` に残る。

## Verification Evidence

| # | Check | Command / Method | Expected result |
|---|-------|-------------------|-----------------|
| V1 | 単体テスト | `cd web && npm test` | 全緑 |
| V2 | ビルド | `cd web && npm run build` | 成功 |
| V3 | 実操作監査 | Playwright 監査 spec (3 解像度 × 4 ルート) | 完走、証跡残存 |
| V4 | 導線 E2E | `npx playwright test -c tests/e2e/playwright.config.ts` | 全緑 |
| V5 | 型検査 | `cd web && npx tsc --noEmit` | エラー 0 |

## Rollback

PR の revert。Owned Paths と evidence/spec 配下のみに触れるため他 Issue に影響しない。

## Success Criteria

- **SC1**: 3 解像度 × 4 ルートの実操作監査が完走し、証跡が残る。
- **SC2**: 見つかった「動かない UI」のうち最低 1 つが実修正されている。
- **SC3**: V1–V2 が緑。
