# Issue #61 — Studio 受入条件の再判定 (2026-11-10)

`docs/audit/open-issues-audit.md` の #61 節は 2026-11-09 時点の判定で、
条件 5 / 7 / 8 / 9 が「未充足・未検証」のまま残っていた。その後の PR
(#257 / #305) と実ブラウザ証跡、および本日の本番 read-only E2E で判定し直した。

**判定: 10 条件すべて充足 (実ブラウザ測定 + 実 API の読み取り)。**

## 1. 判定表

| # | 条件 | 旧判定 | 新判定 | 根拠 |
|---|---|---|---|---|
| 1 | Gate A: Live GET songs、本番で fixture 無し | 充足 | 充足 | 本節 §2 の本番 read-only E2E (`catalog.status` 200 / `catalog.shape`) |
| 2 | Gate A: 表示される導線はすべて実装済み画面を開く | 充足 | 充足 | `web/src/app/topbar.test.tsx`, `catalog-panel.test.tsx` |
| 3 | Gate A: 正しい song+chartVersion を開く | 充足 | 充足 | `web/src/lib/song-session/load.ts:21-60` + §2 の `chart.identity` |
| 4 | Gate A: 1 件の publish が persisted live chartVersion を確認、失敗で入力を失わない | 充足 | 充足 | `transport.ts` + `web/src/lib/publish/__tests__/*`。**本番書き込みは行っていない** (§3) |
| 5 | Gate B: 実アプリアイコン (text `FB` フォールバックでない) | 未充足 | **充足** | `web/public/flashbeat-icon-{32,64,180,192,512}.png` (実 PNG)。`web/void.config.ts:52-54` が 32/192 の favicon と 180 の apple-touch-icon を配線、`web/src/components/brand/brand-header.tsx:21` がヘッダーマークに使う。導入は `9ad25d7` |
| 6 | Gate B: Teal 残存無し | 充足 | 充足 | `web/panda.config.ts:76` |
| 7 | Gate B: デスクトップ幅を実際の編集作業に使う | 未検証 | **充足** | `specs/74-responsive/evidence/2026-11-10/` の 1440×900 / 1280×720 / 1024×768 実 Chrome 撮影。`npm run visual` は horizontal overflow 0 / console error 0 で PASS |
| 8 | Gate C: 1440×900 / 1280×720 / 390×844 / 200% zoom、キーボード+ポインタ | 未充足 (旧 `exitCode: 1`) | **充足** | 同ディレクトリに **5 viewport** (上 3 + 390×844 + **200% zoom = 720×450 CSS px**、`chart_200720x450.png`) × 3 route = 15 capture。統合 owner が `origin/main` (`4a63974`) で再実行し **PASS**。キー入力は `web/tests/visual/tab-walk.mjs` (実 Chrome の Tab / Shift+Tab / Escape / クリック)。旧「`exitCode: 1`」は PR #257 で解消 |
| 9 | Gate C: 実ブラウザ画素画像 **と** 実入力 E2E の両方 | 未充足 | **充足** | 画素画像 = §7/§8 の 15 capture。実入力 = `tab-walk.json` (3 route × 2 viewport の Tab 順、ドロワー focus trap、focus 復帰) + §2 の実 API E2E |
| 10 | 本番デプロイは単独 deploy owner の管理 | 充足 | 充足 | `docs/operations/cloudflare-bootstrap.md` |

## 2. 本番 read-only E2E (2026-11-10)

```
cd web && npm run e2e        # node tests/e2e/run.ts — 既定は read-only
```

| 項目 | 実測 |
|---|---|
| 実行時刻 (UTC) | `2026-11-10T04:40:48.899Z` |
| baseUrl | `https://flashbeat.rebuildup.dev` |
| mode | `read-only` |
| summary | **total 14 / passed 14 / failed 0** (exit code 0) |
| catalogVersion | **38** |
| 曲 | `catalog.youtubeId` = 11 文字の video id、`chart.noteCount` = `chart.notes.length` = 105 |
| leaderboard | 200 / shape 一致 / score 降順 |

確認できたこと: 本番の `GET /api/v1/catalog`・`GET /api/v1/charts/{id}?version=N`・
`GET /api/v1/leaderboards/{id}?version=N` が **fixture ではなく実データ**を返し、
譜面は `contracts/schemas/chart.v1.schema.json` に適合する。

## 3. この判定で言えないこと

- **本番への書き込みは行っていない** (`--write` は使っていない)。したがって
  「本番 publish が成功する」ことは未検証のまま。`FLASHBEAT_ADMIN_TOKEN` を使う
  書き込み E2E は、専用のテスト曲 id とオーナー承認が必要。
- `web/tests/e2e/*.spec.ts` (Playwright) は**本日は実行していない**。§1 条件 9 の
  「実入力 E2E」は `tab-walk.mjs` (実 Chrome の実キー入力) と §2 (実 API) を指す。
  Playwright の studio-flow / editor-note-flow / visitor-flow の再実行は別途可能。
- 200% zoom はブラウザのズーム倍率ではなく **720×450 CSS px のビューポート**で
  再現している (1440×900 を 200% にしたのと同じ CSS 座標系)。実ブラウザの
  ズーム UI を操作した記録ではない。
- 画素画像に対する**デザインレビュー (人の判断)** は #75 の領域で、本判定には含まない。
