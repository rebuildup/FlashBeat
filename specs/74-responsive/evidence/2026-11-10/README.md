# 実ブラウザ証跡 — 5 viewport 撮影・390px 到達性・Tab 順 (2026-11-10)

Issue #74「[Web/Responsive] 画面サイズ・ズーム・キーボードで操作不能なUIをなくす」の
未検証項目 3 件 (tasks.md の T3 Tab 順 walk / T4 390px rendered check / T5 5 viewport
screenshots — いずれも #75 扱い) を **実 Chrome (headless) で測定**して埋めたもの。

- アプリのビルド SHA: `a2fefe14913869981ea5f3773adead79a68b226e`
  (branch `w/w74-evidence`, 基点 `origin/main` = `a2fefe1`)
- URL: `http://127.0.0.1:5210` (ローカル dev server, `void db seed` 済みのローカル D1)
- ハーネス: `web/tests/visual/capture.mjs` (puppeteer-core, システム Chrome) と
  本タスクで追加した `web/tests/visual/tab-walk.mjs`。
  `@playwright/test` は devDependency にあるが、既存の visual ゲートは
  puppeteer-core + システム Chrome を使うためそれに合わせた (ブラウザ DL 不要)。

## 1. 実行コマンドと exit code

| コマンド | exit code | 結果 |
|---|---|---|
| `cd web && npm ci` | 0 | 504 packages |
| `cd web && npx tsc --noEmit` | 0 | 診断なし |
| `cd web && npm test` | 0 | `node --test`: 73 pass / 0 fail (17 suites)、`vitest run`: 46 ファイル 426 tests pass |
| `cd web && npx vp build` | 0 | `✓ built in 353ms` |
| `cd web && npm run visual` | 0 | **PASS** — 15 capture (3 route × 5 viewport)、horizontal overflow 無し、console error 無し |
| `cd web && VISUAL_BASE_URL=… node tests/visual/tab-walk.mjs` | 0 | Tab walk 6 回 (3 route × 2 viewport) + ドロワー focus trap 検証 |

環境: Node v26.13.0 / puppeteer-core 25.16.0 / Google Chrome (headless, システム導入の
もの) / macOS (darwin, arm64)。

補足: 既定の管理ポート 5199 は別セッションの残置プロセスが掴んでいたため、
dev server を手動起動 (ポート 5210) し `VISUAL_BASE_URL` で指した。測定内容に差はない。

統合 owner による再実行 (2026-11-10 13:06-13:14 JST): ブランチを `origin/main` = `4a63974` へ
rebase した上で、上の表のコマンドを同じ手順で再実行し、同じ結果を得た
(`tsc` exit 0 / `npm test` 73 + 426 pass / `vp build` exit 0 / `npm run visual` PASS /
`tab-walk.mjs` 0 outsideViewport・ドロワー trap held=true・focusRestored=true)。
`a2fefe1..4a63974` の差分は `web/` に 1 行も入っていないため、本文のビルド SHA 表記は
そのまま有効 (rebase は証跡の内容を変えない)。

## 2. 5 viewport スクリーンショット (T5)

撮影条件: 各 route (home `/`、chart `/chart?songId=who&version=1`、publish `/publish`) ×
5 viewport。`deviceScaleFactor` は 390×844 と 200% zoom 相当が 2、他は 1。
200% zoom は「1440×900 を 200% 表示」= CSS ピクセル 720×450 の viewport として再現した
(ブラウザズームの CSS ピクセル挙動と同じ)。`deviceScaleFactor: 2` は描画密度の再現であり、
レイアウト判定 (scrollWidth 等) は CSS ピクセルで行われる。

| viewport | label | dsf |
|---|---|---|
| 1440×900 | `1440x900` | 1 |
| 1280×720 | `1280x720` | 1 |
| 1024×768 | `1024x768` | 1 |
| 390×844 (mobile emu) | `390x844` | 2 |
| 200% zoom 相当 (720×450) | `200pct-720x450` | 2 |

ファイル: `<route>_<label>.png` (viewport 実見) と `<route>_<label>.full.png`
(フルページ、折り返し下の確認用)。機械可読の全数値は `visual-index.json`。

### 判定表 (viewport × 項目)

| route | viewport | 横スクロール (document) | clipped | consoleErrors |
|---|---|---|---|---|
| home | 1440×900 | 無し | 0 | 0 |
| home | 1280×720 | 無し | 0 | 0 |
| home | 1024×768 | 無し | 6 (注 1) | 0 |
| home | 390×844 | 無し | 0 | 0 |
| home | 200% (720×450) | 無し | 0 | 0 |
| chart | 1440×900 | 無し | 0 | 0 |
| chart | 1280×720 | 無し | 0 | 0 |
| chart | 1024×768 | 無し | 0 | 0 |
| chart | 390×844 | 無し | 4 (注 2) | 0 |
| chart | 200% (720×450) | 無し | 0 | 0 |
| publish | 1440×900 | 無し | 0 | 0 |
| publish | 1280×720 | 無し | 0 | 0 |
| publish | 1024×768 | 無し | 0 | 0 |
| publish | 390×844 | 無し | 4 (注 2) | 0 |
| publish | 200% (720×450) | 無し | 0 | 0 |

**全 15 capture で `document.scrollWidth == clientWidth` (document レベルの横スクロール無し)。**

- 注 1: home@1024×768 の 6 件は catalog テーブルの actions 列
  (「公開はスタッフ（要トークン）」badge + YouTube link)。祖先の panel 内部スクローラー
  (`overflow-x: auto`, `scrollWidth 832 / clientWidth 782`) の内側にあるため
  **スクロールして到達可能**。document は溢れない。polish 候補として記録する
  (テーブルの md ブレークポイント必要幅 ≈ 27rem に対し 1024px では約 50px 足りない)。
- 注 2: chart/publish@390×844 の 4 件はノートグリッド stage の Konva canvas
  (8 レーン × `MIN_LANE_WIDTH 56px` = 448px)。spec §3.1 のとおり**ノートグリッドが
  横スクロールの所有者**で、stage 内部スクロールで全レーンに到達できる。
  document は溢れない。期待どおりの設計挙動。

## 3. 390×844 rendered check (T4)

`visual-index.json` の `home/chart/publish@390x844` と `tab-walk.json` の
同名 walk が根拠。

| 項目 | 判定 | 根拠 |
|---|---|---|
| 横スクロールが発生しない | **OK** | 全 route で `documentScrollWidth == clientWidth` (390) / `horizontalScroll: false` |
| 主要操作が画面内で到達可能 | **OK** | Tab walk 全 step で `inViewport: true` (chart 40 step / publish 40 step とも body 逸脱 0〜2 = 末尾 wrap のみ)。nav (sticky bar)・検索 (topbar)・publish toggle・editor toolbar・「Add line」等がすべて 390px 幅で focus 到達 |
| 見切れ (clipped) | **OK (設計どおり)** | 上記注 2 の stage 内部スクロールのみ。document は溢れない |
| 要素の重なり | **未機械判定** (§5) | ハーネスは視界外はみ出しのみ測定。目視用 PNG を残した |

## 4. Real browser tab order walk (T3)

`tab-walk.json` (全 step の tag / role / aria-label / text / rect / `inViewport` /
`:focus-visible` 合致) と `tabwalk_*.png` が根拠。各 route で `Tab` を
30〜40 回 (1 step ごとに `document.activeElement` を記録)、その後 `Shift+Tab` を
5 回 (逆順の記録あり)。

| route | viewport | Tab step 数 | `<body>` 逸脱 | ring 無し | 備考 |
|---|---|---|---|---|---|
| home | 1440×900 | 30 | 2 (tab 14, 28) | 0 | 逸脱は末尾 wrap (注 3) |
| home | 390×844 | 30 | 1 (tab 11) | 0 | 同上 |
| chart | 1440×900 | 40 | 1 (tab 27) | 5 (注 4) | tab 15–19 は YouTube iframe 内 |
| chart | 390×844 | 40 | 0 | 12 (注 4) | 同左 |
| publish | 1440×900 | 40 | 1 (tab 28) | 3 (注 4) | |
| publish | 390×844 | 40 | 2 (tab 18, 36) | 4 (注 4) | 逸脱はいずれも末尾 wrap |

- 注 3: `<body>` 逸脱は**すべてページの最終 focusable の直後** (home は最終行の
  “Who on YouTube”、chart は lyrics editor の “Add line”、publish は “Review publish”) で、
  次 Tab で先頭 (sidebar の「曲を選ぶ」) に戻る。headless Chrome には browser chrome
  (アドレスバー) が無いため wrap の 1 step が `<body>` として観測されるもの。
  フォーカスが滞留・消失する箇所は**無い**。実ブラウザではアドレスバーへ抜ける
  通常の wrap 挙動に相当する。
- 注 4: **YouTube 埋め込み iframe にグローバル focus ring が出ない** (不具合として記録)。
  focus が `iframe` 要素に乗ると `:focus-visible` が match せず
  (`app.css` の `*:focus-visible` ring が描画されない)、その後の Tab は
  埋め込みプレイヤー内部に連続して移動する (chart@1440 で tab 15–19、
  chart@390 で tab 11–17・36–40 が連続 `iframe`)。フォーカス位置が見えない期間がある。
  修正は本 Issue の範囲外のため**記録のみ**。
  **別 Issue 提案**: 「YouTube preview iframe にフォーカス時の可視インジケータを
  与える (iframe の `title` 付与と併せて)」。

### ドロワーの focus trap (publish sidebar, 390×844)

`tab-walk.json` の `drawerTrap`。`button[aria-label="Open the publish sidebar"]` を
focus (Tab 到達可能な要素) して `Enter` で開いた:

| 項目 | 判定 |
|---|---|
| 開いた直後の focus | panel 内の先頭 focusable (「✕ Close the publish sidebar」) — `useDrawerFocus` のとおり |
| Tab 12 回のトラップ | **全 step が panel 内** (`trapHeld: true`)、ring 付きで可視 |
| `Escape` | ドロワーが閉じる (`role="dialog"` の aside が消える、`drawerClosed: true`) |
| 閉じた後の focus 復帰 | **トリガーに戻る** (`aria-label="Open the publish sidebar"`, `aria-expanded="false"`, ring 可視) |

証跡 PNG: `tabwalk_publish_390x844_drawer.png` (開いた状態) /
`tabwalk_publish_390x844_after_escape.png` (閉じた後)。

## 5. できなかったこと (未検証)

- **ピクセルレベルのデザイン review** — `visual-index.json` の `needsHumanVisualReview`
  のとおり、spacing・視覚階層・密度の判定は本証跡の範囲外 (PNG を目視評価する別工程)。
- **要素同士の重なりの機械判定** — ハーネスは「viewport 外へのはみ出し (clipped)」のみを
  測る。重なりは目視用 PNG で判断するしかない。
- **実機・実ブラウザ chrome の挙動** — 物理スマホでのタッチ操作、アドレスバーへの
  フォーカス逸脱は headless では再現できない (注 3 の wrap はその代替観測)。
- **YouTube iframe 内部の操作性** — focus が iframe に入ることまでは観測したが、
  埋め込みプレイヤー内部の key 操作は headless では検証していない。
- **本番環境での描画** — すべてローカル dev server (seed 済み D1) での測定。
  publish 等の書き込み操作は行っていない (読み取り描画のみ)。

## 6. ハーネス側の変更 (本コミットに含む)

- `web/tests/visual/capture.mjs`: `VIEWPORTS` を 5 つに拡張 (1024×768 と
  200% zoom 相当 720×450 を追加)。判定ロジックは変更していない。
- `web/tests/visual/dev-server.mjs` (新規): capture.mjs から Chrome 探索と
  dev server 起動を切り出した共有ヘルパー。
- `web/tests/visual/tab-walk.mjs` (新規): 本証跡の Tab walk / focus trap 測定スクリプト。
