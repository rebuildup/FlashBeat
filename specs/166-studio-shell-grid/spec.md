# Spec: Studio全画面のグリッド・共通要素・操作フロー再構成

**Issue**: #166 | **Branch**: `166` | **PR base**: `release-0-1-11` | **Date**: 2026-11-07 | **Status**: Draft

**Input**: Studio全画面 (Catalog / Chart / Publish / New song) の共通要素・余白/グリッド・操作フローを再構成し、全ルートで実操作が成立すること (親 #61)。

> Lean rules: one Issue = one branch = one worktree = one Draft PR; ≤5 tasks;
> no retrospective re-specification of the legacy game.

## Objective

4画面すべてが同一のシェルグリッド (サイドバーレール + 主領域 + 共通ヘッダー帯) を共有し、
Catalog → Editor → Publish → Catalog再読込の導線が実操作で途切れずに成立する。
画面ごとに独自実装だった余白・階層・CTA配置が共通設計に置き換わる。

## Owned Paths

- `web/src/app/app-shell.tsx` — 共通シェル (StudioShell) への抽出と Catalog 固有ロジックの保持
- `web/src/app/sidebar.tsx` — ナビレール (全画面で共有)
- `web/src/app/panel.tsx` — 共通サーフェス (ページヘッダー帯の追加)
- `web/src/app/drawer.tsx` — オフキャンバス inspector (既存・必要時のみ調整)
- `web/pages/{index,chart,new-song,publish}.tsx` — 各画面のシェルへの組み込み
- `web/src/theme/**` の新規トークン/レシピファイル (新規のみ、既存共通を破壊しない)
- `specs/166-studio-shell-grid/**`

## Exclusions (explicitly out of scope)

- `web/src/app/topbar.tsx` — Worker C 排他。編集しない。
- `web/src/components/chart-editor/**`, `web/src/lib/chart/**` — Worker D / E。
- `web/src/components/song-publisher/**`, `web/src/components/new-song/**`,
  `web/src/components/ui/{select,input,field}.tsx` — Worker C。
- `web/src/app/{catalog,catalog-panel,catalog-view}.ts(x)` — 読み取りのみ。
- `Assets/**/*.unity`, Unity Editor 起動, Cloudflare デプロイ — 排他資源。

## User Scenarios & Testing

### Scenario 1 — 全画面が同一シェルを共有する (P1)

**Given** Studio の任意の画面を開いている, **When** `/`, `/chart`, `/publish`, `/new-song` を順に移動する, **Then** 4画面すべてに同一のブランドレール (Sidebar) と同一幅の主領域が表示され、画面固有のヘッダー帯 (戻りリンク + 題名 + 主CTA) が同一の位置・高さに置かれる。全体スクロールは発生せず、各画面の作業領域が内側でスクロールする。

**Independent Test**: `cd web && npx tsc --noEmit && npx vp build` + 実ブラウザで4ルートを目視 (FHD 1920×1080)。

### Scenario 2 — Catalog → Editor → Publish → Catalog再読込 の導線 (P1)

**Given** Catalog に公開済み楽曲がある, **When** 行をクリックして Editor を開き、ノートを編集して Review & Publish を押し、Publish 画面で公開を完了する, **Then** Catalog に戻ると再読込された楽曲 (新しい noteCount / chartVersion) が表示される。Editor で未保存のまま戻るリンクを押すと、保存状態が失われることが明示される。

**Independent Test**: `cd web && npm run dev` + Playwright 仕様 (`web/tests/e2e/studio-flow.spec.ts`) をローカル dev server に対して実行。

### Scenario 3 — 状態の統一 (P2)

**Given** Catalog が loading / empty / error / offline のいずれか, **When** 該当の `?state=` で `/` を開く, **Then** 共通 StateBlock サーフェスで状態が表示され、レイアウトが ready と同じ枠内に収まる (飛び跳ねない)。

**Independent Test**: `/?state=empty` 等をブラウザで開いて目視 + 既存 `web/src/app/catalog-view.test.ts`。

### Edge Cases

- Chart Editor (`ssr=false`, Konva) は `overflow: hidden` の作業領域を必要とする — シェルは全体スクロールを強制しない。
- Publish 画面の loading/error 状態もシェル内に収まる (全画面ブランドレールを維持)。
- 390px 幅: レールは上部バーに折りたたまれる (既存 #70 の挙動を維持)。

## Requirements

- **R1**: `app-shell.tsx` が汎用 `StudioShell` (Sidebar + 主領域 + スクロール方針の指定) を公開し、Catalog 固有の状態 (フィルタ・catalog 読み込み) は `AppShell` 内に留まる。
- **R2**: `chart.tsx` / `publish.tsx` / `new-song.tsx` が `StudioShell` を使う。ハードコードされた hex 色 (`#1b1b1b` 等) を既存セマンティックトークンに置き換える。
- **R3**: ページヘッダー帯 (戻りリンク / 題名 / 主CTA) の共通スタイルを `panel.tsx` に置き、4画面で同一の位置・高さ・文言規則を使う。
- **R4**: 導線: Catalog 行 → `/chart?songId&version` → Review & Publish → `/publish?...` → 公開完了 → Catalog へ戻る → 再読込された catalog。保存状態と戻る挙動を明示する。

## Dependencies & Gate

- **Depends on**: なし (base `release-0-1-11` 上で完結)。topbar 改修 (Worker C) とは非競合: 本 Issue は topbar を呼ぶだけ。
- **Blocks**: なし。
- **Gate**: `npx tsc --noEmit` / `npx vp build` / `npm test` が緑。実ブラウザで Scenario 1–3 を目視 (FHD)。Playwright 仕様がリポジトリに存在すること。

## Verification Evidence

| # | Check | Command / Method | Expected result |
|---|-------|-------------------|-----------------|
| V1 | 型検査 | `cd web && npx tsc --noEmit` | エラー 0 |
| V2 | ビルド | `cd web && npx vp build` | 成功 |
| V3 | 単体テスト | `cd web && npm test` | 全緑 |
| V4 | 導線 E2E (コード) | `web/tests/e2e/studio-flow.spec.ts` (Playwright) | 仕様が存在し、dev server に対して実行可能 |
| V5 | 実ブラウザ目視 | FHD 1920×1080 / 2K 2560×1440 / Mac 2560×1664 | 4ルートで Scenario 1–3 成立 (スクリーンショットを specs 配下に記録) |

## Rollback

`revert` the merge commit of this PR, or branch 削除。Owned Paths のみに触れるため他 Issue の作業に影響しない。

## Success Criteria

- **SC1**: 4ルートすべてで Sidebar + 共通ヘッダー帯が表示される (hex 直書きの画面別シェルが消える)。
- **SC2**: Catalog→Editor→Publish→Catalog再読込 が実操作で完走する。
- **SC3**: V1–V3 が緑で、V4 のテストコードが残る。
