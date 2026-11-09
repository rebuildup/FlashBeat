# Tasks: 233 — ゲーム画面復旧 (旧 UI 排他制御 / コード側)

- [x] 1. `Assets/Game/Scripts/UI/V5/GameUiExclusivity.cs` (新規) — `GameUiPhase` /
      `GameUiVisibility` / `V6UiFlags` / `ResolvePhase` / `Resolve` / `Apply`。
      表示の単一正本 (純粋関数 + 冪等な適用)。
- [x] 2. `GameReadyView.cs` — `SetVisible` 追加、`Update` の自己管理の可視切替を廃止
      (内容の更新のみに縮小)。
- [x] 3. `GameHudView.cs` — `SetVisible` 追加 (Canvas 単位)。
- [x] 4. `GameHudRuntime.cs` (GameHudController) — 毎フレーム `ResolvePhase` →
      `Resolve` → `Apply` に置き換え。レガシー HUD 非表示を 0.5 秒間隔の再適用に変更
      (Start の 1 回廃止)。
- [x] 5. `AutoTestV6.cs` — v6 モードでの V5ReadyCanvas 免除を削除 (前提更新)。
- [x] 6. `Assets/Tests/Editor/GameUiExclusivityTests.cs` (新規) — ResolvePhase 5 分岐 /
      v5・v6 の表示行列 / 主表示高々 1 枚の不変条件 / 実 View を使った Apply の
      遷移・冪等性 / null ビュー。
- [x] 7. compile-check: `--assembly flashbeat` / `--assembly tests` + FlashBeat.Tests
      (Assets/Tests/Editor) の手動ビルド検証。
- [x] 8. `docs/evidence/233-legacy-ui-exclusivity.md` — 実装・検証・残存リスクの記録。

## 未実施 (スコープ外 / Owner 作業)

- [ ] 実 EditMode テスト実行 (Unity Editor 起動が必要 — Supervisor が実施)。
- [ ] 実 Win64 での画面確認・1 曲完走 (Owner 実機 — Issue #233 の本体ゲート)。
