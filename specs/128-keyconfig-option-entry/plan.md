# Plan: Issue #128

1. `git merge --no-edit origin/109-patch` (KeyConfigUI の入力分離修正を取り込む)。
2. KeyConfigUI.cs: 文字列をアトラス被覆グリフのみに書き直し、
   `DefaultLayout` / `IsDefaultLayout` / `IsCustomLayout` とヘッダー下の状態表示を追加、
   `FindJapaneseFont` を public 化。
3. KeyConfigOptionButton.cs (新規): sceneLoaded フックで OptionScene にボタン + キャプションを生成。
4. KeyConfigLayoutTests.cs (新規): 判定ロジックと既定テーブルのずれ検出テスト。
5. 検証: グリフ被覆スクリプト、全差分目視再読。Unity 実行は不可 (unverified と報告)。
