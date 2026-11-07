# Plan: #125

- **Update.ps1**: `Write-UpdateResult` (best-effort・tmp + rename の原子的書き込み) と
  `Get-UpdateVersionFromText` を追加し、`Stop-WithError` / 成功 path / 外部 `catch` から呼ぶ。
  タグ・新版版数は ZIP 特定後に、現行版数はインストール先 exe の ProductVersion から記録。
  PowerShell 5.1 互換構造のみ (?? 演算子等は使わない)。
- **UpdateResultFile.cs** (新規, `FlashBeat.Update`): 読み取り (JsonUtility、必須キー `"result"` の
  事前確認で不正 JSON を排除)、`BuildNoticeText` (純粋)、`MarkShown` (リネーム、失敗時は削除)。
  パス明示 overload をテストに使う。Utf8 BOM 無しで書かれた JSON を前提とする。
- **UpdateFlowModel**: `LastResultNotice` + `SetLastResultNotice` (1 回だけ設定)。
- **UpdateNoticeUI**: 結果通知を Title の表示条件に追加、文言は結果通知 + 既存フェーズ文言の
  結合、Title で初めて表示したフレームで `UpdateResultFile.MarkShown()` (セッション中 1 回)。
- **UpdateChecker**: 起動時 coroutine の先頭で結果ファイルを読み Model へ設定。
  `IsStagedTagNewerThanCurrent` (純粋) を `DownloadAndStage` 冒頭と `TryLaunchUpdater` に追加。
  起動ログ文言を変更。
- **テスト**: `UpdateResultFileTests.cs` (解析 / 文言 / MarkShown / Model 反映) +
  `UpdateStagedVersionGuardTests` (版ガード)。一時ディレクトリに IO 注入。
- **パッチライン**: トランク (branch 118) のコミットを 109-patch に cherry-pick して push。
