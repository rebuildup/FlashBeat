# 0.1.24 リリース候補 — Win64 実機スモーク証跡 (2026-11-13)

Issue #250 / #233 / #242 のリリース収束に伴う、新候補 `0.1.24` (buildSha `9fd7c0403d086b71c418574253e7138bd151ba1d`) の実機確認記録。

- 実施場所: 展示機 (`device-windows`, Windows 11 / UUU\basic)
- 対象: `FlashBeat-0.1.24-win64-9fd7c040.zip` (SHA-256 `d4c65fce0c594d1e8013651ad9428646b16cbaca3440dd3e1dbfcbaa893b7025`、展開先 `C:\Users\rebui\Desktop\FlashBeat-0.1.24-win64-9fd7c040\`)
- 凍結候補 `0.1.23+b6860a80` (`FlashBeat-Exhibit`) は別フォルダ・別プロセスのまま無変更。検証は新規フォルダのみで実施。
- SSH セッションからの直接起動はセッション 0 相当で Unity が即終了するため、Interactive ログオンのスケジュールタスク経由で起動した (旧来の `FBInteractiveRun` と同じ方式)。

## 1. 成果物の同一性 (展開後に実機で確認)

| 項目 | 値 |
|---|---|
| ZIP SHA-256 | `d4c65fce…b7025` — sidecar・ローカル計算と一致 |
| `version.json` | `0.1.24` / `buildSha=9fd7c0403d08…` / tag=`v0.1.24-win64-9fd7c040` / platform=win64 / mediaPolicy=`song-free` |
| 更新チャンネル | `stable` (`FlashBeat_Data/StreamingAssets/update-channel.txt`) |
| 展開先 | `C:\Users\rebui\Desktop\FlashBeat-0.1.24-win64-9fd7c040\` (凍結候補とは別) |

## 2. 自動ハーネス結果 (2K 主画面 2560×1440 ネイティブ)

| ハーネス | 結果 | 証跡 |
|---|---|---|
| `-autotest v6songend` | **pass=31 fail=0** / songEndState=Completed / judged=130==chartNotes / score=992,307 | `songend-2k/` |
| `-autotest v6screens` | **pass=46 fail=0** / symbols=63 / screenshots=15 / resolution=2560x1440 | `v6screens-2k/` |
| `-autotest v6` (1920×1080 指定起動) | **pass=29 fail=0** | `fhd-1920x1080/` |
| `-autotest v6songend` (1920×1080 指定起動) | **pass=31 fail=0** | (result.txt は `fhd-1920x1080/` 参照、リモート `fb-0124-fhd-songend/`) |

音声: `MusicManager STATUS` 行で `audioMode=StreamCombined` かつ `rms=0.08〜0.54 / peak≈0.67` を確認 (信号レベル。人間の聴取ではない)。

## 3. FHD Select BEST 折返し (#379) の確認

旧候補 (`09-fhdreal-select.png` in `docs/evidence/233-win64-exhibit-2026-11-13/`) では物理 FHD で BEST `992,307` が `992,30 / 7` と 2 行に折り返していた。

今回、1920×1080 のウィンドウ指定起動 (`-screen-width 1920 -screen-height 1080`、全画面引数なし) で `v6songend` を実行し、Select 画面の BEST が **`992,307` を 1 行で描画** されることを OCR で確認した。

- 新: `fhd-1920x1080/select-best-992307-1920x1080.png` — BEST 値 `992,307` が単一テキスト行 (OCR 1 ボックス)
- 旧: `docs/evidence/233-win64-exhibit-2026-11-13/09-fhdreal-select.png` — BEST 値が `992,30` + 折返し行の 2 ボックス

注意: 今回の FHD 確認は **解像度 1920×1080 での描画検証** であり、物理 FHD モニターでの全画面受入ではない (物理 FHD での全画面起動はオーナー最終確認に残す)。折返しはレイアウト起因の不具合であり、描画解像度が同じなら再現条件は同等。

## 4. 未実施 (オーナー最終確認に残す項目)

- Result → Retry の実遷移 (既存ハーネスに Retry 経路が無い。0.1.23 受入時と同じくオーナー実機で 1 回確認)
- 人間による聴取 (cc は音を聞かない)
- 物理 FHD モニターでの引数なし全画面起動 (0.1.23 で実施済みの手順を 0.1.24 でも推奨)
- Option 設定の保存方向、オンライン登録 API 書き込み、ネットワーク不通時の挙動

## 5. 実行環境メモ

- SSH (session 0 相当) からの `Start-Process` では Unity プレイヤーが即終了し成果物を残さない。Interactive タスク (`Register-ScheduledTask -LogonType Interactive -UserId basic`) 経由で session 1 に起動する必要がある。
- 旧展示プロセス (PID 47092, `FlashBeat-Exhibit`) は検証中も動作継続。Player.log は同一 `KimuraYusuke/FlashBeat` 配下で共有されるため、証跡は `-autotest-out` の結果ファイルを正本とした。
