# Tasks: #142 KeyActions

1. [x] KeyActions.cs 追加 (enum/既定表/IsDown/Set 検証/Save/Load/Changed)
2. [x] Title/Result/GameScene/Select/MusicManager/Typing/Notes の inline 読みを KeyActions.IsDown へ
3. [x] KeyConfigUI 第 2 ページ『メニュー操作』 (タブ + ←→/PageUp/PageDown、トグル/クリア/R 初期化)
4. [x] ヘルプ表示の動的更新 (Title push hints、Typing Space to Start)
5. [x] KeyActionsTests.cs + AutoTestKeyConfig 第 2 ページ自動テスト

## 追補タスク (2026-08 → 2026-11-08, #146/#148 残差分)

- [x] 表示: SelectView/OptionView/PauseMenuView/ResultView/GameErrorView/GameReadyView の
      固定キーキャップを ActionKeyHint 経由に置換
- [x] 入力: SelectView/OptionView/PauseMenuView/ResultView/GameErrorView/
      SelectSceneManager(V6)/ResultRankingOverlay の Esc/Space/O 直読みを
      KeyActions.IsDown へ統一
- [x] PauseMenuView のアクションキャップを KeyConfigUI.GetKeysText から ActionKeyHint へ統一
- [x] MenuKeyActionWiringTests (追従・未割当・固定リテラル走査・直読み走査)
- [x] 検証: script/compile-check.sh (flashbeat / tests) + テスト asm 全体の one-off ビルド
