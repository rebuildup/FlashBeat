# 展示 runbook — FlashBeat Win64（運営者向け）

対象 Issue: **#22**（リリースゲート）。承認記録: [`release-approval-checklist.md`](release-approval-checklist.md) / 証拠索引: [`../evidence/index.md`](../evidence/index.md)。

**起動は `FlashBeat.exe` の直接起動だけ**です。ランチャー / `Play.cmd` / `Update.cmd` / 起動時自動更新 /
ロールバックコマンドは **存在しません**（作らない方針。パッチ配布は #35、適用は #36 が担当）。

---

## 1. 今日使うビルド

| 用途 | フォルダ | `FlashBeat.exe` SHA-256 | 状態 |
|---|---|---|---|
| 展示用（**承認済み**） | 承認記録の「承認済み成果物」欄を参照 | — | 承認記録に 1 件も無い場合、**展示開始前に #22 へ連絡**する |
| 予備（last-known-good、外部保管） | `D:\4_apps\Unity\Build\FlashBeatBuild\Build0.1.0-win64-1c5137e` | `3031bb5e…c39a60` | S0。譜面→リザルト→リトライまで実証済み。**音は出ない**（S1 未証明） |

> 展示中に使うのは **承認済み成果物だけ**です。承認は
> [`release-approval-checklist.md`](release-approval-checklist.md) に記録されます。

## 2. 起動

1. 展示 PC でフォルダを開く（例 `D:\4_apps\Unity\Build\FlashBeatBuild\Build0.1.0-win64-1c5137e`）。
2. **`FlashBeat.exe` をダブルクリック**（ショートカットも `FlashBeat.exe` 直リンク）。
3. 起動後 10 秒以内にタイトル画面（`FlashBeat` ロゴ）が出れば正常。
   - 起動しない / 黒画面のまま → §6 の復旧、§7 のエスカレーション。
4. 運営者がログを取りたい場合（**起動パスを変えない**。`FlashBeat.exe` を直接起動したのと同じ）:

```powershell
# 既存ビルドで、ログだけ付けたいとき
& 'D:\4_apps\Unity\Build\FlashBeatBuild\Build0.1.0-win64-1c5137e\FlashBeat.exe' -logFile 'D:\4_apps\Unity\Build\FlashBeatEvidence\operator\Player.log'
```

## 3. 操作フロー（keyboard only）

| 画面 | 操作 | キー |
|---|---|---|
| Opening | 自動でタイトルへ（約 6 秒） | — |
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

**1 項目 1 行**。`NG` なら §6 へ。チェックリストは
[`release-approval-checklist.md`](release-approval-checklist.md) §C の承認版と同じ項目。

| # | 項目 | 判定 | 備考 |
|---|---|---|---|
| 1 | タイトル → 選曲 → ゲーム → リザルト → リトライが 1 曲ぶん通る | ☐ | リザルトで `R` で同じ曲に戻れる |
| 2 | 選曲/リザルトの曲名・アーティスト名・歌詞が `□` にならない | ☐ | `□` なら #27 のフォント修正ビルドを使う |
| 3 | 譜面が途中で落ちず、レーンとノーツ timing が崩れない | ☐ | 判定が `Miss` だけなら運用の問題ではなく検証の記録 |
| 4 | レーンキーが反応する（`Q` `W` `E` … を押してノーツ/判定が変わる） | ☐ | |
| 5 | 音が鳴る / 音量が適切 | ☐ | **S1 未証明のため現在は無音**。無音は不具合ではなく未証明項目として扱う |
| 6 | 音画同期（ノーツ落下と曲が合う） | ☐ | S1 承認後に有効 |
| 7 | ネットワークを切った時、固まらずエラー表示が出る | ☐ | 現状の仕様は「graceful に失敗してログに残る」まで |
| 8 | 起動直後 / 選曲中 / 曲中にフリーズしない | ☐ | 30 秒以上固まったら §6 |
| 9 | 1280×720 で文字が切れていない | ☐ | 曲名・歌詞・判定 |
| 10 | ウィンドウが 1280×720 で表示される | ☐ | 検証は windowed 1280×720 で実施（`-screen-width 1280 -screen-height 720 -screen-fullscreen 0`）。実機 1280×720 パネルでの fullscreen は **未証明** |

## 5. パッチ（更新）を適用する手順

適用は **運営者が明示的に 1 回だけ** 実行します（#36 の `Update.ps1`）。**訪問者セッション中の強制更新はしません。**

1. ゲームを **終了** させる（Result 画面の `Space` → 選曲 → `Esc` → タイトルで `E`）。実行中の `FlashBeat.exe` が
   残っている場合、#36 の手順は「実行中なら中止して促す」動作になります。**強制 kill はしない。**
2. パッチを **1 回だけ** 適用する（#36 の `Update.ps1` / ZIP の手動差し替え。手順の詳細は #36 のドキュメント）。
3. 適用が成功したら **`FlashBeat.exe` を直接起動** し直して、§4 の 1〜4 だけ確認する。

* 通信エラーや検査エラーで失敗した場合、**現在の配置はそのまま**残ります（更新の失敗で現状が壊れないこと）。
* 不具合が見つかった場合は **新しいパッチを別途配布** して直します（#35 が Release を作成 → #36 が適用）。
  過去版への切替・自動ロールバック・ダウングレード手段は **用意しません**。

## 6. 異常時の復旧（訪問者対応）

| 症状 | 対応 |
|---|---|
| フリーズ（画面が 30 秒以上変化しない） | `Alt` + `F4` でウィンドウを閉じる → `FlashBeat.exe` を再起動。上司/ ISSUE 担当へ §7 の形式で連絡 |
| 起動直後に黒画面 | `FlashBeat.exe` を再起動。再起動してもなおらない場合は §7 |
| 音が鳴らない | 既知の未証明項目（S1 音）。**そのまま続けてよい**が §7 へ一言 |
| ネット断で曲が始まらない | ログに yt-dlp / Invidious の失敗が残る。`Space` で別曲へ。§7 へ |
| PC ごと再起動 | 承認済みビルドのフォルダを再展開 → `FlashBeat.exe` を直接起動 |

## 7. エスカレーション（最低限）

連絡は **GitHub Issue** にコメントとして残す（口頭だけが証拠にならないため）。

| 対象 | Issue | 貼るもの |
|---|---|---|
| ビルドが起動しない / 固まる / 譜面の不整合 | [#11](https://github.com/rebuildup/FlashBeat/issues/11)（S0 baseline） | プレイヤーログ + スクリーンショット + 使用した `FlashBeat.exe` の SHA-256 |
| 音が出ない / ストリームが再生されない | [#13](https://github.com/rebuildup/FlashBeat/issues/13)（S1 yt-dlp 再生） | プレイヤーログ（`[MusicManager]` 行）+ 曲名 |
| 日本語が `□` になる | [#27](https://github.com/rebuildup/FlashBeat/issues/27)（フォント） | select / game / result のスクリーンショット |
| パッチ配布 / GitHub Release | [#35](https://github.com/rebuildup/FlashBeat/issues/35) | 必要な SHA / 検証結果 |
| パッチ適用 | [#36](https://github.com/rebuildup/FlashBeat/issues/36) | 適用ログ、適用前後の `FlashBeat.exe` SHA-256 |
| リリース判定・証跡の矛盾 | [#22](https://github.com/rebuildup/FlashBeat/issues/22) | どの成果物・どの SHA についての判断かを明記 |

ログの入手方法:

* 起動時に `-logFile <path>` を付けて起動した場合 → そのファイル。
* 付けずに起動した場合 → `%USERPROFILE%\AppData\LocalLow\rebuildup\FlashBeat\Player.log`（Unity の既定位置）。

**ログを Issue に貼る前に必ずマスク** してください（署名付き一時 URL は公開NG）。

```powershell
# マスク済みログを作る（署名 URL・video id を置換）
powershell -NoProfile -ExecutionPolicy Bypass -File release/check/redact-log.ps1 `
    -InPath 'C:\path\to\Player.log' -OutPath 'C:\path\to\Player.redacted.log'
# マスク漏れチェック
powershell -NoProfile -ExecutionPolicy Bypass -File release/check/redact-log.ps1 `
    -InPath 'C:\path\to\Player.redacted.log' -Check
```

## 8. 成果物・証跡の置き場所

| もの | 場所 |
|---|---|
| 承認済み Win64 成果物（Git の外） | `D:\4_apps\Unity\Build\FlashBeatBuild\<バージョン>-win64-<sha>` |
| スモークのログ / スクリーンショット（Git の外） | `D:\4_apps\Unity\Build\FlashBeatEvidence\<日付>-<ステージ>\` |
| 証跡索引（SHA 付き） | [`docs/evidence/index.md`](../evidence/index.md) |
| リリース判定 | [`docs/operations/release-approval-checklist.md`](release-approval-checklist.md) |

既存成果物の**上書き・削除はしない**。新しいビルドは同じ名前を作らず別名（バージョン + 短い SHA）で保管します。
