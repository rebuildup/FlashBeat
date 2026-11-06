# 展示 runbook — FlashBeat Win64（運営者向け）

対象 Issue: **#22**（展示版 smoke / パッチ配布ゲート）。
承認記録: [`release-approval-checklist.md`](release-approval-checklist.md) /
証跡索引: [`../evidence/index.md`](../evidence/index.md)。

**起動は `FlashBeat.exe` の直接起動だけ**です。ランチャー / `Play.cmd` / `Update.cmd` / 起動時自動更新 /
ロールバックコマンドは **存在しません**（作らない方針。パッチ配布は #35、適用は #36 が担当）。

---

## 1. 展示フォルダ（正確なパス）

```
D:\4_apps\Unity\Build\FlashBeatExhibit\
├─ FlashBeat.exe            ← 運営者はこれを直接起動する
├─ FlashBeat_Data\          （Managed DLL / resources を含む）
├─ MonoBleedingEdge\
├─ UnityPlayer.dll / UnityCrashHandler64.exe / DirectML.dll / dstorage.dll / D3D12\
├─ EXHIBIT-MANIFEST.txt    ← exe SHA-256 とフォルダ SHA-256
└─ logs\                   ← 展示中の診断ログ（運営者が明示的に書き出す場所）
```

* 展示中は **読み取り専用で参照** します。フォルダ内のファイルを書き換えてはいけません。
* 検証済みの成果物 1 式だけを入れます。古いビルドをこのフォルダに別名でのこさないでください
  （古い成果物は `D:\4_apps\Unity\Build\FlashBeatBuild\` に別の名前で保管されています）。
* 指纹（SHA-256）は `EXHIBIT-MANIFEST.txt` と [`../evidence/index.md`](../evidence/index.md) に記載。

## 2. 起動方法（`FlashBeat.exe` 直接実行）

1. ファイルエクスプローラーで `D:\4_apps\Unity\Build\FlashBeatExhibit\` を開く。
2. **`FlashBeat.exe` をダブルクリック**（ショートカットも `FlashBeat.exe` 直リンク）。
3. 起動からタイトル画面（`FlashBeat` ロゴ）まで **実測 60〜90 秒** かかることがあります（初回は
   杀毒ソフト / ディスクキャッシュでさらに遅い）。固まったように見える時間は、**そのまま待ってください**。
4. 起動しない / 黒画面のまま → §6 の復旧、§7 のエスカレーション。

運営者がログを取りたい場合（**起動経路は変えない**。`FlashBeat.exe` を直接起動するのと同じ）:

```powershell
# ログを展示フォルダの logs/ に出しながら起動する（読み取り専用の展示フォルダに書かない）
& 'D:\4_apps\Unity\Build\FlashBeatExhibit\FlashBeat.exe' -logFile 'D:\4_apps\Unity\Build\FlashBeatExhibit\logs\Player-<日付>-<時刻>.log'
```

## 3. 操作フロー（keyboard only）

| 画面 | 操作 | キー |
|---|---|---|
| Opening | 自動でタイトルへ | — |
| Title | 選曲へ / オプション / 終了 | `Space` / `O` / `E` |
| Select | 決定 / 次の曲 / 前の曲 / タイトルへ | `Space` / `J` `K` `L` `;` `→` / `F` `D` `S` `A` `←` / `Esc` |
| Game | 開始（8 秒のプリロール後） | `Space` |
| Game | ノーツ叩き | 下表 |
| Result | 選曲へ / リトライ | `Space` / `R` |

レーンキー（`Judge.LaneKeys`、8 レーン）:

| レーン | キー |
|---|---|
| 0 | `1` `Q` `A` `Z` |
| 1 | `2` `W` `S` `X` |
| 2 | `3` `E` `D` `C` |
| 3 | `4` `5` `R` `T` `F` `G` `V` `B` |
| 4 | `6` `7` `Y` `U` `H` `J` `N` `M` |
| 5 | `8` `I` `K` `,` `<` |
| 6 | `9` `O` `L` `.` `>` |
| 7 | `0` `-` `^` `\` `P` `@` `[` `;` `:` `]` `/` `_` `*` `` ` `` |

## 4. 展示前 / 展示中の 1280×720 スモークチェックリスト

**1 項目 1 行**。`NG` なら §6 へ。承認版は
[`release-approval-checklist.md`](release-approval-checklist.md) §D。

| # | 項目 | 判定 | 備考 |
|---|---|---|---|
| 1 | タイトル → 選曲 → ゲーム → リザルト → リトライが 1 曲ぶん通る | ☐ | リザルトで `R` で同じ曲に戻れる |
| 2 | 選曲/リザルトの曲名・アーティスト名・歌詞が `□` にならない | ☐ | `□` ならフォント修正が効いていない |
| 3 | 譜面が途中で落ちず、レーンとノーツ timing が崩れない | ☐ | 判定が `Miss` だけなら運用の問題ではなく検証の記録 |
| 4 | レーンキーが反応する（`Q` `W` `E` … を押してノーツ/判定が変わる） | ☐ | |
| 5 | 音が鳴る / 音量が適切 | ☐ | **Showcase ビルドで要確認**（§6 に現状の判定あり） |
| 6 | 音画同期（ノーツ落下と曲が合う） | ☐ | |
| 7 | ネットワークを切った時、固まらずエラー表示が出る | ☐ | 現状の仕様は「graceful に失敗してログに残る」まで |
| 8 | 起動直後 / 選曲中 / 曲中にフリーズしない | ☐ | 30 秒以上固まったら §6 |
| 9 | 1280×720 で文字が切れていない | ☐ | 曲名・歌詞・判定 |
| 10 | ウィンドウが 1280×720 で表示される | ☐ | 検証は windowed 1280×720 で実施（`-screen-width 1280 -screen-height 720 -screen-fullscreen 0`）。実機 1280×720 パネルでの fullscreen は **未検証** |

## 5. パッチ（更新）を適用する手順

* 展示フォルダ `D:\4_apps\Unity\Build\FlashBeatExhibit\` は **自動更新の対象外** です。
  `Update.ps1` は **別の既存配置（インストール先）** に対して実行します。
* 運営者が明示的に 1 回だけ適用します（**訪問者セッション中の強制更新はしません**）。

1. ゲームを **終了** させる（Result 画面の `Space` → 選曲 → `Esc` → タイトルで `E`）。
   実行中の `FlashBeat.exe` が残っている場合、#36 の手順は「実行中なら中止して促す」動作になります。
   **強制 kill はしません。**
2. パッチを **1 回だけ** 適用する（#36 の `Update.ps1`、または ZIP の手動差し替え）。
   * パッチ ZIP の取得先: **#35 の GitHub Release**（SHA-256 付き）。
   * 適用手順の詳細は #36 のドキュメント（`tools/update/**` の担当範囲）。
3. 適用が成功したら **インストール先の `FlashBeat.exe` を直接起動** し直して、§4 の 1〜4 だけ確認する。
4. 展示フォルダの中身は ** 바꾸えません**。展示でどのビルドを使うかは §1 と
   [`../evidence/index.md`](../evidence/index.md) の「承認済み成果物」欄で決まります。

* 通信エラーや検査エラーで失敗した場合、**現在の配置はそのまま**残ります。
* 不具合が見つかった場合は **新しいパッチを別途配布** して直します（#35 が Release を作成 → #36 が適用）。
  過去版への切替・自動ロールバック・ダウングレード手段は **用意しません**。

## 6. 異常時の復旧（訪問者対応・読み取り専用）

| 症状 | 対応（展示フォルダには書き込まない） |
|---|---|
| フリーズ（画面が 30 秒以上変化しない） | `Alt` + `F4` でウィンドウを閉じる → `FlashBeat.exe` を再起動。復旧後 §7 へ連絡 |
| 起動直後に黒画面 | `FlashBeat.exe` を再起動（初回は 60〜90 秒かかる）。なおらない場合は §7 |
| 音が鳴らない | ログの `[MusicManager]` 行を確認（§6.1）。既知の未証明項目の可能性があります |
| ネット断で曲が始まらない | ログに yt-dlp / Invidious の失敗が残る。`Space` で別曲へ。§7 へ |
| PC ごと再起動 | 展示フォルダを **書き換えず** に再起動 → `FlashBeat.exe` を直接起動 |

### 6.1 読み取り専用で原因を見る手順

| 確認したいこと | どこを見る（読むだけ） |
|---|---|
| 起動したか / どのビルドか | `D:\4_apps\Unity\Build\FlashBeatExhibit\EXHIBIT-MANIFEST.txt` |
| 直近のプレイヤーログ | `D:\4_apps\Unity\Build\FlashBeatExhibit\logs\`（運営者が `-logFile` を指定して書き出したもの）。無ければ `%USERPROFILE%\AppData\LocalLow\KimuraYusuke\FlashBeat\Player.log` |
| YouTube ストリームの成否 | ログの `[MusicManager] yt-dlp resolved videoId=…` / `[MusicManager] VideoPlayer failed to prepare within 30s` |
| 音声の成否 | ログの `AudioLevelProbe` / `VideoPlayer` 系行 + 実際に聞こえたか |
| 日本語グリフの崩れ | ログの `not found in the [...] font asset`（0 件なら正常） |
| 判定数と譜面の一致 | ログの `Miss` / `Perfect` / `Great` / `Bad` 行数（譜面ノーツ数と一致なら正常） |
| どこで固まったか | ログ末尾（最後に出力された行が怪しい処理） |

ログを Issue に貼る前に必ずマスクします（署名付き一時 URL は公開NG）。

```powershell
# マスク済みログを作る（署名 URL・video id を置換）
powershell -NoProfile -ExecutionPolicy Bypass -File release/check/redact-log.ps1 `
    -InPath 'D:\4_apps\Unity\Build\FlashBeatExhibit\logs\Player-2026-11-06-2300.log' `
    -OutPath 'C:\Users\rebui\Desktop\Player.redacted.log'
# マスク漏れチェック
powershell -NoProfile -ExecutionPolicy Bypass -File release/check/redact-log.ps1 `
    -InPath 'C:\Users\rebui\Desktop\Player.redacted.log' -Check
```

## 7. エスカレーション（最低限）

連絡は **GitHub Issue** にコメントとして残す（口頭だけが証拠にならないため）。

| 対象 | Issue | 貼るもの |
|---|---|---|
| ビルドが起動しない / 固まる / 譜面の不整合 | [#11](https://github.com/rebuildup/FlashBeat/issues/11)（S0 baseline） | プレイヤーログ + スクリーンショット + `EXHIBIT-MANIFEST.txt` の SHA-256 |
| 音が出ない / ストリームが再生されない | [#13](https://github.com/rebuildup/FlashBeat/issues/13)（S1 yt-dlp 再生） | プレイヤーログ（`[MusicManager]` 行）+ 曲名 |
| 日本語が `□` になる | [#27](https://github.com/rebuildup/FlashBeat/issues/27)（フォント） | select / game / result のスクリーンショット |
| パッチ配布 / GitHub Release | [#35](https://github.com/rebuildup/FlashBeat/issues/35) | 必要な SHA / 検証結果 |
| パッチ適用 | [#36](https://github.com/rebuildup/FlashBeat/issues/36) | 適用ログ、適用前後の `FlashBeat.exe` SHA-256 |
| リリース判定・証跡の矛盾 | [#22](https://github.com/rebuildup/FlashBeat/issues/22) | どの成果物・どの SHA についての判断かを明記 |

## 8. 成果物・証跡の置き場所

| もの | 場所 |
|---|---|
| 展示フォルダ（読み取り専用） | `D:\4_apps\Unity\Build\FlashBeatExhibit\` |
| 展示フォルダの診断ログ | `D:\4_apps\Unity\Build\FlashBeatExhibit\logs\` |
| 旧・実験ビルド（Git の外） | `D:\4_apps\Unity\Build\FlashBeatBuild\<バージョン>-win64-<sha>\` |
| スモークのログ / スクリーンショット（Git の外） | `D:\4_apps\Unity\Build\FlashBeatEvidence\<日付>-<ステージ>\` |
| 証跡索引（SHA 付き） | [`../evidence/index.md`](../evidence/index.md) |
| リリース判定 | [`release-approval-checklist.md`](release-approval-checklist.md) |

既存成果物の**上書き・削除はしない**。新しいビルドは同じ名前を作らず別名（バージョン + 短い SHA）で保管します。

## 9. 2026-11-06 深夜に **成立していない** Windows 作業（未検証）

以下は「今夜この PC で実証できていない」項目です。**green 表示はしません。**

| # | 未成立の作業 | 展示中の代替（読み取り専用で確認できるもの） |
|---|---|---|
| 1 | **統合ビルド（#13 + #27）の Win64 ビルド** — fresh Library のインポートで作業中。00:10 JST 時点で未完了なら **未検証**。完了しても展示フォルダの切替は運営者の明示操作 | `D:\4_apps\Unity\Build\FlashBeatExhibit\` の `EXHIBIT-MANIFEST.txt` で「いま展示している世代」を確認 |
| 2 | **統合ビルドでの 選曲 → YouTube ストリーム → 音 → リザルト** の実プレイ | 現状の展示世代は **音が出ない**（旧 resolver）。ログの `[MusicManager] yt-dlp resolver failed` / `YouTube prepare failed` 行で「ストリーム未再生」を確認できる |
| 3 | **実機 1280×720 ディスプレイでの fullscreen 描画** — この PC に 1280×720 パネルが無い | ウィンドウモードで `FlashBeat.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0` を起動し、表示が崩れないことを確認（§4 の 9/10） |
| 4 | **統合ビルドでの 1280×720 UX スモーク** | 上記と同じ（windowed 1280×720） |
| 5 | **展示 PC 実機でのパッチ適用（#36 の `Update.ps1`）** | #22 の検証範囲外。#36 の担当。展示フォルダは更新対象外（§5） |
| 6 | **S2 / S3（API 連携・公開譜面の再生）** | API 未デプロイ・成果物なし。展示はローカル譜面のみ |
| 7 | **#27 のログに残った生の YouTube video id のマスク** | `release/check/redact-log.ps1 -InPath <log> -Check` で機械的に確認できる。#27 側の対応待ち |
