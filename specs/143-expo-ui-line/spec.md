# spec: exhibition Unity UI line (v5 on the release line)

## 目的
展示用リリースライン `109-patch` の上に、v5 UI 各画面を載せた単一ブランチ `expo-ui` を作る。
Supervisor が 1 つの exhibition player をビルド・検証できるようにする。

## 対象マージ (順序守る)
1. `origin/121` — Select v5 (list / ranking panel / typing filter / History) — PR #124
2. `origin/123` — Result / Result-Register v5 — PR #126
3. `origin/122` — Game HUD + Pause v5 — PR #130
4. `origin/120` — Title / Opening / Option v5 — PR #131
5. `origin/128-trunk` — Option 画面の一人前の「キー設定」項目 — PR #132

## 不変条件
- `Assets/Game/Scripts/UI/RankingPanel.cs`: 109-patch の backdrop 修正
  (BuildBackdrop が backdrop ルートを返す / `boardRoot = body;` は旧実装) を維持。
- `Assets/Game/Scripts/UI/KeyConfigUI.cs`: モーダルは `panel.gameObject.SetActive(false)` で隠す。
- `Assets/Game/Scripts/Gameplay/KeyBindings.cs` + `KeyConfigUI.cs`: カスタムキー割り当てが動作し続けること。
- `web/`, `contracts/` は触らない。player build / deploy はしない。

## 完了条件
- 5 ブランチのマージ完了、Draft PR (#143, base `109-patch`) オープン。
- Unity batch mode で `error CS` 0 件。
- EditMode テスト実行し結果を報告 (既知の既存失敗 `JapaneseFontCoverageTests` は除外対象外として報告のみ)。
