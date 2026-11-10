# #233 再取得 run — 2026-11-10 (main 直後の macOS 実機)

**実施日**: 2026-11-10
**対象 commit**: `origin/main` = `e45c494` (PR #291 merge 直後。#295 / #296 は `web/` と
ドキュメントのみの変更で **ゲーム本体のバイナリには影響しない**)
**環境**: **Mac** (Unity 6000.5.6f1 / macOS スタンドアロン / **実ディスプレイ 2560×1664 全画面**)
**Windows 展示機には一切アクセスしていない** (指示どおり)。

この run は **PR #263 の修正 (3D プレイフィールド) が、HOLD 実機検証 (PR #291) を入れた
`main` でも崩れていないこと** を再確認するためのもの。**Windows 実機の受入ではない。**

---

## 1. 実行手順 (再現可能)

```
# 1) 実機ビルド (main から)
script/mac-verify build origin/main
# -> /Users/rebui/work/FlashBeat.macverify/Builds/macOS/FlashBeat.app

# 2) 実ディスプレイ全画面でハーネスを走らせる
script/mac-playfield-evidence.sh \
  /Users/rebui/work/FlashBeat.macverify/Builds/macOS/FlashBeat.app \
  <出力先>
```

`script/mac-playfield-evidence.sh` は

- 実行ファイル直叩きで起動し、**自分の子プロセスだけを** kill する
  (ユーザーが手元で起動した FlashBeat には触らない)
- 実ディスプレイ全画面 (2560×1664) で起動する
  (窓リサイズによる擬似全画面は使っていない)
- `autotest-v6-result.txt` が出るまで待ち、**結果ファイルの中身で終了コードを返す**
  (0 = fail=0 / 1 = 結果なし・fail≠0・ログに FAIL 行)

## 2. 実測結果

`autotest-v6-result.txt`:

```
[AutoTestV6] RESULT pass=29 fail=0
```

ハーネスの 29 項目が **すべて成功**。FAIL 行は 1 件もない。

Player.log の `[FullscreenDiagnostics]` (実効値):

```
[FullscreenDiagnostics] event=startup, screen=2560x1664,
  currentResolution=2560x1664@60.0024000960038Hz, fullScreen=True,
  fullScreenMode=FullScreenWindow, dpi=299, safeArea=0,0,2560x1664,
  displays=1, display0=system:3420x2224,rendering:2560x1664,active:True
```

→ **2560×1664 の実ディスプレイ上で FullScreenWindow**。擬似全画面ではない。

### 2.1 合格した項目のうち「描画まわり」のもの

| 項目 | 結果 |
|---|---|
| `no legacy graphics are visible` | PASS (旧 v5 UI が v6 と混ざっていない) |
| `no legacy lyric objects are active` | PASS (歌詞の二重表示なし) |
| `the legacy loading overlay is not drawn` | PASS (旧ロード表示が二重に出ていない) |
| `the original 3D lanes are kept` | PASS (3D レーンが保持されている) |
| `the media is ready` / `the playback started` | PASS (映像の準備と再生開始) |
| `the media clock advances` | PASS (メディアクロックが進んでいる = 映像が流れている) |

## 3. スクリーンショット

| ファイル | 対応するキャプチャ | 中身 |
|---|---|---|
| `autotest-v6-1.png` | 01-select | 選曲画面 |
| `autotest-v6-2.png` | 02-select-selected | 選曲画面 (曲を選択した状態) |
| `autotest-v6-3.png` | 03-game | ゲーム画面 (開始直後) |
| `autotest-v6-4.png` | 04-media-ready | ゲーム画面 (メディア準備完了)。**`autotest-v6-3.png` とバイト単位で同一** (同じフレームの撮影) |
| `autotest-v6-5.png` | 05-gameplay | ゲーム画面 (再生中)。**映像 / レーン境界 8 本 / 判定ライン / ノーツ / 歌詞パネル** が見えている |
| `autotest-v6-6.png` | 05a-gameplay-dense | 密度の高い領域の 1 枚目 |
| `autotest-v6-7.png` | 05b-gameplay-dense | 密度の高い領域の 2 枚目 (ノーツ 2 本が画面内にある) |

`autotest-v6-5.png` / `-7.png` を **画像として目視**した結果:

- **映像プレーン**: 動画 (アニメの 1 フレーム) が背景として全面に描かれている
- **レーン**: 判定ラインから上方へ収束する **8 本のレーン境界線** が見える
- **判定ライン**: 画面下部に白い水平線
- **ノーツ**: 白い横長のブロックがレーン上に 1〜2 本見えている
- **歌詞**: 中央上部に歌詞パネル (日本語 + ローマ字)

## 4. 判定の限界 (正直に)

1. **Windows 実機では未検証。** ここにあるのは macOS の実機観測である。
   Media Foundation の URL 読み込み失敗 (#13) の性質上、macOS で通ったことは
   Windows の挙動を保証しない。**#233 の「Windows 実機でのノーツ・動画の視認」は
   引き続き未解決**。
2. **密度の高い区間を捉えたかの判定は SOFT PASS。** ログの
   `SOFT PASS the capture lands in a dense chart region (soft)` と
   `SOFT PASS the 2nd capture has more notes than the 1st (soft)` は
   **強制条件ではない**ため、「05a / 05b が本当に密度の高い区間だった」ことは
   保証されない。実際 05b には 2 本しか写っていない。
   → **「密度の高い区間を捉えた撮影」自体は未証明**。
   「描画が存在する」ことは証明できるが、
   「密度の高い区間でどう描画されるか」は本 run では証明できていない。
3. **判定色は見ていない。** ハーネスは `-autotest v6` でキー入力を行わないため
   画面には `Miss` が出る。これは自動操作の結果であり描画の不具合ではない。
4. **A/V 同期 (音と映像の一致) は見ていない。** 観測したのは映像プレーンの
   描画とメディアクロックの進行まで。

## 5. 証跡

| ファイル | 内容 |
|---|---|
| `autotest-v6-result.txt` | ハーネスの結果 (`pass=29 fail=0`) |
| `Player.log` | 実行時の全ログ (`FullscreenDiagnostics` / 29 個の PASS / 映像準備) |
| `autotest-v6-1.png` 〜 `-7.png` | 実ディスプレイ全画面でのキャプチャ 7 枚 |
| `script/mac-playfield-evidence.sh` | 起動スクリプト (リポジトリ側) |
