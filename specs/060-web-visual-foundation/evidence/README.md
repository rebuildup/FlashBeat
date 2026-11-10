# #60 evidence — before/after 画素証跡

## 既存画像（`before-ready-*` / `after2-*`）が証明していないこと

このフォルダ直下の `before-ready-{1440,900;1280,720;390,844}.png` と `after2-*` は
**どちらも Pen パレット**（canvas `#1B1B1B` 系・青い accent）で描画されており、
**teal → 青の変化を証明していない**。実測値（catalog 一覧 1440×900）:

| 採取点 | `before-ready-1440,900.png` | `after2-1440,900.png` |
|---|---|---|
| サイドバー選択背景（nav 選択状態） | `#2B4457` | `#2B4457`（差なし = Pen `accent-tint`） |
| canvas 地色 | `#111113`（旧 Park gray） | `#1C1C1C`（Pen `bg #1B1B1B` 系） |

つまり既存 2 組は **canvas（中性色）の変化しか示しておらず、accent の変化は示していない**。
理由: サイドバーの選択色（`#2B4457` / `#9AD3FA`）は #60 の実装前から `web/src/app/sidebar.tsx`
に Pen 値がハードコードされていたため、token を teal から blue へ変えても描画が変化しない。
A グレード監査（issue #60 コメント `6744105927`）が「差分の証明にならない」と判定した根拠はここ。

## 正しい before/after 証跡

`pixel/` 以下の再撮影が、#60 の差分を証明する実物である。

| フォルダ | 撮影 SHA | 内容 |
|---|---|---|
| `pixel/before-54505c2/` | `54505c2998fb4e8c756e632ea48c396bd36dad88`（= `2bd7302^`、旧 teal テーマ） | `{home,chart,publish}_{1440x900,1280x720,390x844}.png` + `index.json` |
| `pixel/after-2bd7302/` | `2bd7302aac0419d80d662eaa4315253a2b36707f`（= PR #90 の head、Pen パレット適用済み） | 同構成 |

画素サンプリング（`publish_1440x900.png`）:

| 採取点 | before | after |
|---|---|---|
| `Export` ボタン塗り | `#12A594` | `#3FA9F5` |
| `Review publish` ボタン塗り | `#12A594` | `#3FA9F5` |
| canvas 地色 | `#111113` | `#1C1C1C` |
| ノートブロック塗り | `#12A594` | `#12A594`（ハードコード残存・#72 スコープ） |

`publish` / `chart` 画面には `colorPalette` 駆動のボタンがあるため、token 差し替えが
そのまま画素差として現れる。catalog 一覧（`home`）には token 駆動の accent 要素が無く、
canvas のみが変わる。

撮影手順・コマンド・環境・残存課題は
[`docs/evidence/60-web-visual-foundation/README.md`](../../../docs/evidence/60-web-visual-foundation/README.md)
と `docs/evidence/60-web-visual-foundation/results.json` に記録してある。
