# Plan: 106 — v5 UI 基盤

設計ソース: design/flashbeat-ui.pen v5 (pen CLI `GetVariables()` / `Prefab/*` ノード実測) +
design/ui-size-profiles.json。LFS PNG は本 worktree に実体がないため、数値はすべて
pen CLI と json から取得した。

## 構成

- `Assets/Game/Scripts/UI/V5/UISizeProfile.cs` — ui-size-profiles.json の 3 プロファイルを
  不変クラス化。`Select(w,h)` は json の select 規則そのまま。fhd 未満の高さでは
  `min(1, h/1080)` 倍の派生プロファイル (FhdScaled) を返しクリップを防ぐ。
  `CanvasConfigurator` が Constant Pixel Size x1 を適用し、Update で解像度変化を検知して再適用。
- `Assets/Game/Scripts/UI/V5/V5Tokens.cs` — Design System v5 の変数
  (bg/text/accent/danger/warn/ok + tint/light, grays g1-19, raised u1-5, recessed d1-4,
  keycap k0-6) を static readonly Color 化。角丸は Components v5 実測
  (KeyCap/Tag/Thumb = 4, Button/Row/SearchField = 8)。
- `Assets/Game/Scripts/UI/V5/V5Ui.cs` — ビルダ。先頭コメントに公開 API 一覧。
  角丸 9-slice スプライトは実行時生成 (距離ベース AA, border = r+2)。
  フォントは TMP_Settings.defaultFontAsset → Resources フォールバック。
- `Assets/Tests/Editor/V5UiTests.cs` — EditMode テスト 13 件。

## 決定事項

- 1024x768 / 1280x720 は fhd を `h/1080` 倍に縮小 (spec.md 参照)。mac は 900px 高設計済みのため縮小しない。
- 行の高さなどサイズは ui-size-profiles.json を優先 (SongRow は pen の 72 ではなく row_h=78)。
  色/角丸/ラベルサイズは pen 実測を優先。
- ScriptableObject は使わない (要件どおり纯 C# + static ファクトリ)。

## 検証

Unity 実行は不可 (Supervisor が単一 Windows Editor を保有)。コンパイルは C# コンパイラで
構文チェックするのみ (Unity API のリンクは不可 → 目視レビュー併用)。実行確認は未検証として報告。
