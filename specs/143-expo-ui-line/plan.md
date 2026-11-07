# plan: exhibition Unity UI line

## アプローチ
- `expo-ui` (= `origin/109-patch` 起点) に 5 ブランチを順に `git merge --no-ff`。
- 各マージ後に重要ファイル 5 つ (`RankingPanel.cs`, `KeyConfigUI.cs`, `KeyBindings.cs`,
  `RankingClient.cs`, `ResultRankingOverlay.cs`) を `git diff origin/109-patch -- <file>` で検証。
  旧実装に戻っていたら `git show origin/109-patch:<file>` で復元。
- コンフリクト解決方針:
  - Select/Result など画面本体は v5 側 (theirs)。
  - KeyBindings / KeyConfigUI は ours (109-patch) が上位互換 (テストフック・入力隔离) なため ours。
  - ただし 128-trunk の KeyConfigUI 追加機能 (DefaultLayout / IsDefaultLayout / IsCustomLayout /
    layoutLabel / public FindJapaneseFont / PanelHeight 900) は ours に統合 (手動マージ)。
  - `tools/update/Update.ps1` は 109-patch の更新結果ファイル機能 (issue #125) を維持。
- 検証: Unity batch compile (`error CS` grep) → EditMode tests。

## リスク
- v5 SelectView のナビゲーションは矢印キー直接読み (KeyBindings 非対応)。カスタム割り当てでの
  Select 移動は未対応 — 既知の残項目として報告。
- シーンファイルは全ブランチが旧 base から書き換えているが 109-patch は未変更のため
  3-way マージで競合しない想定。
