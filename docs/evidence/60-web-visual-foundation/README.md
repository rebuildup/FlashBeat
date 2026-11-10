# Issue #60 証跡 — Web visual foundation（Pen v5 → Panda/Park トークン写像）

本フォルダは Issue #60 の受入条件 **T5**（「before/after の画素証跡・実行 SHA・未解決差分を
Draft PR に記録」）を満たすために記録した証跡である。

**本フォルダに記録しているのは実際に再現した証跡である。既存の
`specs/060-web-visual-foundation/evidence/` の画像は本 Issue の差分を証明していない
（理由は末尾「既存証跡の位置づけ」を参照）。**

---

## 1. 記録した識別子

| 項目 | 値 | 確認方法 |
|---|---|---|
| Issue | [#60](https://github.com/rebuildup/FlashBeat/issues/60) | — |
| 実装 PR | [#90](https://github.com/rebuildup/FlashBeat/pull/90) | `gh pr view 90 --json url,headRefOid,mergeCommit` |
| PR の head SHA（= 証跡撮影 SHA） | `2bd7302aac0419d80d662eaa4315253a2b36707f` | `gh pr view 90 --json headRefOid` / `git rev-parse 2bd7302` |
| PR の merge commit | `5e4e43f9e4a482a79cf1ff5ed7f686c930c8bff5` | `gh pr view 90 --json mergeCommit` |
| before SHA（#60 適用直前の基底） | `54505c2998fb4e8c756e632ea48c396bd36dad88` | `git rev-parse 2bd7302^`（= `54505c2` "Merge pull request #85 from rebuildup/69"） |
| 撮影日 | 2026-11-10 | — |
| 撮影環境 | macOS / 実機 Google Chrome (`/Applications/Google Chrome.app/Contents/MacOS/Google Chrome`) headless、`puppeteer-core` | `web/tests/visual/capture.mjs` |
| 撮影ツール | `web/tests/visual/capture.mjs`（`origin/main` 版 blob `94b4ebdf4fab6ac0d4aa571012d62de06a0f6699`、追加コミット `3ad9947` / #75） | `git rev-parse origin/main:web/tests/visual/capture.mjs` |

### 撮影ツールが #60 の 2 つの SHA に存在しない点について（重要）

`web/tests/visual/capture.mjs` と `package.json` の `visual` スクリプトは **#75（`3ad9947`）で
追加された** ため、#60 の実装 SHA（`2bd7302`）と before SHA（`54505c2`）には存在しない。
本証跡では **撮影ツールだけを `origin/main` から両作業ツリーに同一のものをコピーして使用した**
（アプリ本体のソース・設定は変更していない）。また `2bd7302` の `package.json` には `visual`
スクリプトが無いため、`npm run visual` ではなく `node tests/visual/capture.mjs` を直接実行している。

---

## 2. 証跡ファイル

画像の実体は `specs/060-web-visual-foundation/evidence/pixel/` にあり、本フォルダから相対参照する。

| パス | 内容 |
|---|---|
| `specs/060-web-visual-foundation/evidence/pixel/before-54505c2/` | before 撮影（SHA `54505c2`）の `{home,chart,publish}_{1440x900,1280x720,390x844}.png` と `index.json` |
| `specs/060-web-visual-foundation/evidence/pixel/after-2bd7302/` | after 撮影（SHA `2bd7302`）の同構成 |
| `results.json` | 上記の機械可読サマリ（SHA・PR・コマンド・画素サンプル・残存課題） |
| `specs/060-web-visual-foundation/evidence/README.md` | 既存の `before-ready-*` / `after2-*` 画像が何を証明し / しないかの説明 |

### 各画像が証明できること / できないこと

| 画像 | 証明できること | 証明できないこと |
|---|---|---|
| `pixel/before-54505c2/publish_1440x900.png` | 旧 Slate/Teal テーマの実描画（`Export` / `Review publish` ボタンが teal `#12A594`、canvas が `#111113`） | — |
| `pixel/after-2bd7302/publish_1440x900.png` | Pen パレット適用後の実描画（同名ボタンが Pen accent `#3FA9F5`、canvas が `#1B1B1B` 系） | ノートブロックの色（ハードコード teal の残存。4 節を参照） |
| `pixel/*/chart_1440x900.png` | チャート編集画面の `Export` ボタンが teal から Pen accent に変わったこと | — |
| `pixel/*/home_1440x900.png` | catalog 一覧の canvas が `#111113` から `#1B1B1B` 系になったこと | accent の変化（catalog 一覧には token 駆動の accent 要素がほぼ無い） |
| `pixel/*/index.json` | 各撮影の測定値（横スクロール無し・console error 無し・`smallFontElements` 件数）と撮影条件 | ピクセル平坦性の主観評価（`needsHumanVisualReview` は本来人が判断する項目） |

---

## 3. 画素サンプリング（実測値）

`publish_1440x900.png` / `chart_1440x900.png` の指定座標を PNG から直接デコードして採取した値。

| 採取点 | before (`54505c2`) | after (`2bd7302`) | 判定 |
|---|---|---|---|
| `publish` — `Export` ボタン塗り | `#12A594` | `#3FA9F5` | **teal から Pen accent へ** |
| `publish` — `Review publish` ボタン塗り | `#12A594` | `#3FA9F5` | **teal から Pen accent へ** |
| `publish` — canvas 地色 | `#111113` | `#1C1C1C` | Park 既定 gray から Pen `bg #1B1B1B` へ（実測は 1 段階の明度差） |
| `chart` — `Export` ボタン塗り | `#12A594` | `#3FA9F5` | **teal から Pen accent へ** |
| `publish` — ノートブロック塗り | `#12A594` | `#12A594` | **差なし** = 既知の残存（4 節を参照） |

座標と値は `results.json` の `pixelSamples` に機械可読で記録してある。

---

## 4. 未解決・残存（この Issue の範囲外）

1. **ノートブロックの teal ハードコード残存** —
   `web/src/components/chart-editor/note-grid.tsx:32` の `const NOTE_FILL = "#12a594";`。
   トークン駆動ではないため #60 の変更では書き換わらず、after 撮影でもノートは teal のまま
   （3 節の表のとおり）。PR #90 の handoff contract でも「#72 のスコープ」と明記している残存。
2. **`2xs` = 8px の可読性** — 実測 `smallFontElements` は before/after いずれも
   home 9 / chart 4 / publish 9 件。トークンマッピング表 §4/§6 に残差として記録済み。
3. **フォーカスリングのコントラスト 2.5:1** — accent が Pen 固定値のため。#74 で再監査。
4. **390px のサイドバーが折りたたまれない** — レスポンシブは #74 のスコープ。

---

## 5. 再現手順（実測したコマンド）

`web/node_modules` は Panda のキャッシュ（`node_modules/.panda`）を共有するため、
**before と after で物理的に別作業ツリーを使い、それぞれの `node_modules` を独立に用意した**
（同一 `node_modules` を共有すると Panda キャッシュが上書きされ、before 撮影まで Pen パレットで
描画されてしまう）。再現する際はこの点に注意する。

```sh
# ---- 0. 作業ツリー（before / after を別々に作る）
git worktree add --detach /tmp/w60b 54505c2998fb4e8c756e632ea48c396bd36dad88   # before
git worktree add --detach /tmp/w60a 2bd7302aac0419d80d662eaa4315253a2b36707f   # after

# ---- 1. 各ツリーで依存関係の導入とコード生成（node_modules は物理コピー。共有 symlink にしない）
cd /tmp/w60b/web
cp -a /path/to/repo/web/node_modules ./node_modules       # Panda キャッシュを隔離するため物理コピー
find node_modules/.vite -delete; find node_modules/.panda -delete
npx void prepare
npx panda codegen
FLASHBEAT_ADMIN_TOKEN=local-dev-only npx void db migrate
FLASHBEAT_ADMIN_TOKEN=local-dev-only npx void db seed

# ---- 2. 撮影ツールを配置（#60 の SHA には存在しないため origin/main から同一 blob をコピー）
mkdir -p tests/visual
git -C /path/to/repo show origin/main:web/tests/visual/capture.mjs > tests/visual/capture.mjs

# ---- 3. ローカル dev サーバ（シークレット無し・外部デプロイ無し）
FLASHBEAT_ADMIN_TOKEN=local-dev-only npx vp dev --port 5312 --host 127.0.0.1 &
# http://127.0.0.1:5312/api/v1/catalog が 200 になるまで待つ

# ---- 4. 撮影（実 Chrome / headless）
cd /tmp/w60b/web
VISUAL_BASE_URL=http://127.0.0.1:5312 node tests/visual/capture.mjs
# → web/tests/visual/out/<timestamp>/ に PNG と index.json が出力される
# after ツリーも同じ手順（ポート 5311）で実施する
```

撮影時の出力（before ツリー）:

```
[visual] using provided base URL http://127.0.0.1:5312
[visual] chrome: /Applications/Google Chrome.app/Contents/MacOS/Google Chrome
[visual] home @ 1440x900: hScroll=false clipped=0 smallFonts=9 smallTargets=1 consoleErrors=0
[visual] home @ 1280x720: hScroll=false clipped=0 smallFonts=9 smallTargets=1 consoleErrors=0
[visual] home @ 390x844: hScroll=false clipped=18 smallFonts=8 smallTargets=1 consoleErrors=0
[visual] chart @ 1440x900: hScroll=false clipped=0 smallFonts=4 smallTargets=1 consoleErrors=0
[visual] chart @ 1280x720: hScroll=false clipped=0 smallFonts=4 smallTargets=1 consoleErrors=0
[visual] chart @ 390x844: hScroll=false clipped=2 smallFonts=4 smallTargets=1 consoleErrors=0
[visual] publish @ 1440x900: hScroll=false clipped=0 smallFonts=9 smallTargets=1 consoleErrors=0
[visual] publish @ 1280x720: hScroll=false clipped=0 smallFonts=9 smallTargets=1 consoleErrors=0
[visual] publish @ 390x844: hScroll=false clipped=2 smallFonts=9 smallTargets=1 consoleErrors=0
[visual] horizontal overflow: none
[visual] console errors: none
[visual] PASS
```

after ツリーの撮影出力も `horizontal overflow: none` / `console errors: none` / `PASS` で、
`smallFonts` の件数も before と同値（home 9 / chart 4 / publish 9）だった。

> ポート 5199 は本環境で他プロセスが使用済みのため 5311 / 5312 を使用した
> （`capture.mjs` は `VISUAL_BASE_URL` 指定時は自前の dev サーバを立てない）。

### 同一 SHA での検証コマンド（after = `2bd7302`）

```sh
cd /tmp/w60a/web
npx tsc --noEmit    # exit 0
npm test            # node --test: 19 pass / vitest: 11 files, 93 tests, 0 fail
npx vp build        # exit 0（"✓ built in 402ms"）
```

---

## 6. 既存証跡（`specs/060-web-visual-foundation/evidence/*.png`）の位置づけ

`before-ready-{1440,900;1280,720;390,844}.png` と `after2-*` は **どちらも Pen パレット**
（青 accent、teal なし）で描画されており、**teal から青への変化を証明していない**。
実測（catalog 一覧 1440×900）:

| 採取点 | `before-ready-*` | `after2-*` |
|---|---|---|
| サイドバー選択背景 | `#2B4457` | `#2B4457`（差なし = Pen `accent-tint`） |
| canvas 地色 | `#111113` | `#1C1C1C`（Pen `bg #1B1B1B` 系） |

つまり既存 2 組は **canvas（中性色）の変化しか示さない**。サイドバーの選択色は #60 の
実装前から `web/src/app/sidebar.tsx` に Pen 値がハードコードされていたため、token を
teal から blue へ差し替えても描画が変化しない。A グレード監査（issue #60 コメント
`6744105927`）が「差分の証明にならない」と判定した根拠はここ。
本 Issue で撮影した `evidence/pixel/before-54505c2/` が、その正しい before 実物である
（`publish` / `chart` 画面には `colorPalette` 駆動のボタンがあり、teal → `#3FA9F5` が
画素差として現れる）。
