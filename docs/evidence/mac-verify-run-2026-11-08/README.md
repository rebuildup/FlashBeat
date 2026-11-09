# mac-verify run — 2026-11-08 (macOS ネイティブ実機の証跡)

Issue #115 (mac-verify `run` サブコマンド) / #96 / #162 の **Mac 側** 受入証拠。

## 実行内容

```
script/mac-verify run integration/v6-followup 45
```

- 隔離 worktree: `/Users/rebui/work/FlashBeat.macverify`
- Unity: 6000.5.6f1 (canonical)
- 手順: build → launch → 待機 45s → OS スクリーンショット → Player.log 検査 → 終了

## 結果

| 項目 | 実測 |
|---|---|
| ビルド | **成功** (`Builds/macOS/FlashBeat.app`) |
| 起動 | 成功、プロセスは生存 (`process alive: yes`) |
| Player.log の例外/エラー行 | **0** |
| シーン遷移 | `opening scene start` → `scene load end TitleScene 55ms` → `scene has been loaded, and fading in has just ended` |
| 実解像度 | **2560×1664** (MacBook Air のネイティブ解像度) |

## 実機スクリーンショット

| ファイル | 画面 |
|---|---|
| `shot-00-Opening.png` | オープニング |
| `shot-01-TitleScene.png` | タイトル (v6) |
| `shot-02-TitleScene.png` | タイトル (v6) — 背景動画が再生中のフレーム |

v6 タイトル画面 (2560×1664) で確認できたもの:

- **ロゴが正しく描画される** (スプライト取得成功。白クアッドが出ていない)
- 背景動画が再生されている (音uay användでアニメーションが進む)
- 更新通知「更新して再起動 / F3」が正しい位置に出ており、**Version が `v0.1.0+aac497b` → `v0.1.14`** と比較表示されている
- 自己ベスト `55,263` と直近の記録曲 `まにまに / r-906` が実データ
- 下部に「設定 0」と「START / Space」

## 未証明 (Windows 実機が必要なもの)

- FHD 1920×1080 / 2K 2560×1440 の実ディスプレイ全画面 (展示 Win64) — Issue #242 のゲート
- 選曲 → ゲーム → リザルト の 1 曲完走 (YouTube ストリーム再生) — Issue #233 のゲート
- 2K モニタの確保可否 — オーナー判断

## 注意

`Player.log` にはトークン・シークレット・認証情報は含まれません (検査済み)。
