# Evidence: 233 — 旧 UI 排他制御 (コード側)

- 日付: 2026-11-08
- Branch: `w/e233`
- Issue: #233 ([P0 BLOCKER/Owner real PC] ゲーム画面崩壊・プレイフィールド不可視・HUD重複)
- 範囲: **コード側の排他制御のみ**。実 Win64 での画面確認は Owner 作業 (未実施、下記「残存リスク」)。

## 実装

### 新規: `Assets/Game/Scripts/UI/V5/GameUiExclusivity.cs`

表示の単一正本。Unity API に依存しない純粋関数 + 冪等な適用:

- `GameUiPhase` = Loading / Ready / Playing / Paused / Failed
- `ResolvePhase(HudPlayPhase, playbackStarted, pauseOpen)` — 既存の観測値
  (`HudPlayState.Evaluate` #93、`GManager.played`、`PauseMenuView.IsOpen`) のみを入力にする。
  新しい通知経路は導入していない。
- `Resolve(phase, V6UiFlags, pauseOpen)` — 決定表:
  - v5 モード (`V6UiMode.GameHud == false`): Loading→Loading / Ready→Ready /
    Playing→Hud / Paused→Hud+Pause / Failed→Error。Ready 中は **HUD を出さない**
    (Issue #233 の「HUD と Ready が同時表示」の是正)。ポーズは HUD / Ready の上に
    重ねる (別 Canvas のため両方可視が正)。Loading・Failed 中はポーズを閉じる。
  - v6 モード (`V6UiMode.GameHud == true`): v5 の Ready / HUD / Loading / Error /
    Pause を全フェーズで非表示。例外は v6 ポーズ無効時 (`V6PauseView.Enabled == false`)
    の v5 ポーズのみ。
- `Apply(vis, ...)` — 各ビューへの冪等な適用。未生成のビューは null 許容
  (v6 モードでは GameHudView を作らない)。

### 変更: `GameHudRuntime.cs` (GameHudController)

- **Issue #233 の根本原因への対処**: `HideLegacyHud` (名前ベース `SetActive(false)`) を
  Start の 1 回きりから 0.5 秒間隔の再適用 (`ReapplyLegacyHud`) に変更。
  後から生成 / 再有効化された旧 UI を消し続ける。
- `Update` は毎フレーム `ResolvePhase` → `Resolve` → `Apply` で状態から再適用する
  (従来の if/else による error / loading / pause の個別操作を置き換え)。
- `GameHudController.LastPhase` (v6 のローディング表示が読む) は維持。

### 変更: `GameReadyView.cs` / `GameHudView.cs`

- `SetVisible(bool)` を追加 (Canvas 単位で Stop/再開)。
- `GameReadyView.Update` が自前で行っていた `root.SetActive(ready)` を廃止。
  可視の決定は `GameUiExclusivity` に一元化され、隠れている間は Update / コルーチンも
  停止する (内容の更新は表示中のみ)。

### v6 側 (`V6GameRuntime.cs`)

変更なし。`LateUpdate` での v5 Canvas 非表示は同じ方向に作用する防御層として残す
(フェーズ正本と競合しない)。

### テスト前提の更新

- `Assets/Game/Scripts/Common/AutoTestV6.cs`: v6 モードでは全フェーズで
  v5 Canvas 非表示が正になったため、「Ready 中の V5ReadyCanvas は正当」という
  免除を削除。V5ReadyCanvas が可視で残ったら fail になる。
- `AutoTestV6Screens.cs` は所有パス外のため未変更 (同種の免除が残るが緩い側の検査)。

## 検証 (実施済み)

| 項目 | 結果 |
| --- | --- |
| `script/compile-check.sh --assembly flashbeat` (Assets/Game/Scripts, 139 ソース) | **Build succeeded** |
| `script/compile-check.sh --assembly tests` (Assets/Game/Tests) | **Build succeeded** |
| FlashBeat.Tests (`Assets/Tests/Editor`, 61 ソース) — script 対象外のため同一手順 (`dotnet build` + Unity 生成 csproj を雛形に Compile 項目をディスクから再生成、参照は Unity ビルド済み DLL) で検証 | **0 error** (新規 `GameUiExclusivityTests.cs` を含む) |

備考: FlashBeat.Tests の検証では `Assets/Editor` の `GManagerParallelArrayUpdater` を
参照する既存テスト (`YouTubeImportTests`) のために `FlashBeat.Editor.dll` の参照を、
`MigrateLegacyTextJsonTests` のために `NoteEditor.Editor.dll` の参照を
それぞれ追加した (いずれも既存コードの依存であり、今回の変更とは無関係)。

新規テスト `Assets/Tests/Editor/GameUiExclusivityTests.cs` (12 テスト):
- `ResolvePhase` の 5 分岐 (Failed / Loading / Ready / Playing / Paused)
- v5 モードの表示行列 (Ready=Ready のみ、Playing=Hud+Pause、Paused=Hud+Pause、
  Loading/Failed は単独オーバーレイ + ポーズ封鎖)
- 全フェーズで主表示 (Ready/Hud/Loading/Error) が高々 1 枚の不変条件
- v6 モードで全フェーズ・全 v5 要素が非表示 / v6 ポーズ無効時の v5 ポーズ フォールバック
- 実際に v5 View 群を EditMode で生成した `Apply` の検証:
  Ready→Playing で Ready が隠れ HUD が出る、Paused で HUD を出したままポーズを重ねる、
  同一決定の再適用で状態不変 (冪等)、null ビューが許される

## 残存リスク / 未実施

- **実 Win64 での目視確認は未実施 (Owner 作業)**。Issue #233 の Done 条件
  (「一曲が実 Windows で開始し、映像とノーツが表示され、キーで判定され、スコアが動き、
  結果へ進める」) は本タスクでは満たせない。画面崩壊にはプレイフィールド不可視・
  動画非表示・フルスクリーン等の別要因が含まれる可能性があり、それらは本タスクの
  スコープ外 (Issue #233 の別タスク / TASK 2 PLAYABLE-FIRST) 。
- **実 EditMode テスト実行は未実施** (Unity Editor を起動しない作業方針)。
  compile-check は通ったが、テストの実行結果 (特に EditMode での実 View 生成まわり:
  `GameReadyView.Build` のリソース読み込み、`PauseMenuView.Build`) は Supervisor の
  実行で確認されるべき。`V5UiWiringTests` が同種の View 生成を EditMode で既に行っており、
  同じパターンなのでリスクは低いと判断。
- v5 モードでの挙動変化: Ready 中に HUD (スコア / コンボ) が見えなくなる。
  これは Issue #233 の要求 (「Ready 中は Ready 専用表示」) そのものだが、
  v5 UI の従来見た目からの意図的な変更である。
- レガシー非表示の再適用間隔は 0.5 秒 (`LegacyHideInterval`)。再表示されてから
  最大 0.5 秒見える可能性は残る (毎フレーム化はシーン全体走査のコストとトレードオフ)。
