# Spec: #125 更新結果の見える化 (無言の失敗の排除)

実機で v0.1.2 → v0.1.3 の自動更新が Update.ps1 の包含判定バグで終了コード 1 で中止されたが、
アプリは『更新スクリプトを起動しました』と成功のように記録するだけで、利用者・運営者に失敗が
伝わらなかった。失敗を Loud かつ検証可能にする。

## 要件

1. `tools/update/Update.ps1` は**すべての終了経路**で
   `%LOCALAPPDATA%\FlashBeat\update\last-result.json` を書く
   (`{ result: success|failed|aborted, exitCode, fromVersion, toVersion, tag, message, timeUtc }`)。
   - `success` は切替 + 配置後検査が完了したあとにだけ書く。
   - `aborted` = 終了コード 1 (前提不備) / 6 (実行中)。`failed` = それ以外の失敗・想定外エラー。
   - `message` は FlashBeat-update.log と同じ日本語の理由。
   - 既存の挙動・パラメータは変えない (結果ファイルの追記のみ)。
2. アプリ (UpdateChecker / UpdateNoticeUI) は次回起動時に結果ファイルを読む:
   - `failed` / `aborted` → Title で『前回の更新に失敗しました: \<message\> - 現在の版 vX を使用中 / F3 で再試行』
   - `success` → Title で『更新しました vX』を 1 回
   - 表示した時点で mark-as-shown (last-result-shown.json へリネーム)。ゲームはブロックしない。
   - スクリプト起動後のログを `update script started (result unknown until next start)` に変える。
3. staging / 適用の直前に、対象 ZIP の版が現行より新しいことを再確認する (旧版・同版別ビルドは拒否)。
4. 純粋ロジックの EditMode テスト (解析・文言選択・one-shot・版ガード、IO 注入)。
5. `docs/operations/update-release.md` に結果ファイルと失敗時挙動を追記。
