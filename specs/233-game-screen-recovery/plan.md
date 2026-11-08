# Plan: 233 — ゲーム画面復旧 (旧 UI 排他制御 / コード側)

Issue: #233 — branch `w/e233`。spec: [spec.md](spec.md)。

## 設計

### 状態の単一正本: `GameUiExclusivity` (新規, 純ロジック)

`Assets/Game/Scripts/UI/V5/GameUiExclusivity.cs`。Unity API に依存しないので
EditMode テストから直接呼べる。

- `enum GameUiPhase { Loading, Ready, Playing, Paused, Failed }`
- `struct GameUiVisibility { Ready, Hud, Loading, Error, Pause }`
- `struct V6UiFlags { GameHud, PauseEnabled }` — v6 の静的フラグ
  (`V6UiMode.GameHud` / `V6PauseView.Enabled`) は呼び出し側が渡す
  (純粋関数から v6 の静的状態を直接読まない。テストで両モードを再現できるように)。
- `ResolvePhase(HudPlayPhase, playbackStarted, pauseOpen)` — 既存の
  `HudPlayState.Evaluate` (#93) の結果 + `GManager.played` + ポーズ開閉から
  フェーズを決める。新しい通知経路は作らない。
- `Resolve(phase, v6, pauseOpen)` — 表示 / 非表示の決定表:
  - v5 モード: Loading→Loading / Ready→Ready / Playing→Hud / Paused→Hud+Pause /
    Failed→Error。Ready・Playing 中はポーズ開なら重ねて表示 (別 Canvas)。
    Loading・Failed 中はポーズを閉じる (既存の Esc=選曲導線と同一規約)。
  - v6 モード: v5 の全描画要素を非表示。v6 ポーズ無効時のみ v5 ポーズが役割を持つ。
- `Apply(vis, ready, hud, loading, error, pause)` — 冪等な適用。
  未生成のビューは null を許す (v6 モードでは GameHudView を作らない)。

### 配線: `GameHudController` (GameHudRuntime.cs)

- `Update` ごとに `ResolvePhase` → `Resolve` → `Apply` (毎フレーム再適用)。
  従来の if/else による error/loading/pause の個別操作は置き換え。
- `GameReadyView.Update` が自前で行っていた `root.SetActive(ready)` を廃止し、
  可視は `SetVisible` (Canvas ごと) に集約。隠れている間は Update / コルーチンも止まる。
- `GameHudView` / `GameReadyView` に `SetVisible(bool)` を追加 (Canvas 単位)。
- レガシー HUD の名前ベース非表示 (`HideLegacyHud`) は Start の 1 回から
  0.5 秒間隔の再適用 (`ReapplyLegacyHud`) に変更。後から再有効化された
  旧 UI を消し続ける (Issue #233 の根本原因への対処)。
- `GameHudController.LastPhase` (v6 のローディング表示が読む) は従来どおり維持。

### v6 側 (`V6GameRuntime`)

変更なし。`LateUpdate` での v5 Canvas 非表示は防御層として残す
(フェーズ正本と同じ方向に作用するため競合しない)。

### 自動テストの前提更新

- `AutoTestV6`: v6 モードでは全フェーズで v5 Canvas 非表示が正になったので、
  「Ready 中の V5ReadyCanvas は正当」という免除を削除。
- `AutoTestV6Screens` は所有パス外のため未変更 (同種の免除が残るが、
  緩い側の検査であり誤検知を生まない)。

## テスト計画 (EditMode: `Assets/Tests/Editor/GameUiExclusivityTests.cs`)

1. `ResolvePhase`: Failed / Preparing / Hidden+(未再生|再生済|再生済+ポーズ) の 5 分岐。
2. `Resolve` (v5): Ready=Ready のみ / Playing=Hud(+Pause) / Paused=Hud+Pause /
   Loading・Paused・Failed で主表示が高々 1 枚 (排他の不変条件)。
3. `Resolve` (v6): 全フェーズで v5 の 5 要素すべて非表示。v6 ポーズ無効時は
   v5 ポーズにフォールバック (Loading・Failed 中は除く)。
4. `Apply`: 実際に v5 View 群 (GameReadyView / GameHudView / GameLoadingView /
   GameErrorView / PauseMenuView) を EditMode で生成し、
   Ready→Playing→Paused→Playing の遷移で表示が入れ替わること、
   同じ決定の再適用で状態が不変 (冪等) であること、null ビューが許されること。

## 検証

- `script/compile-check.sh --assembly flashbeat` (Assets/Game/Scripts, 139 ソース)。
- `script/compile-check.sh --assembly tests` (Assets/Game/Tests)。
- `Assets/Tests/Editor` (FlashBeat.Tests) は script の対象外のため、
  同一手順 (`dotnet build` + Unity 生成 csproj を雛形に Compile 項目をディスクから再生成) で
  別途ビルド検証した (61 ソース, 0 error)。
- 実 EditMode 実行と実機確認は Supervisor / Owner (本タスクでは未実施、evidence 参照)。
