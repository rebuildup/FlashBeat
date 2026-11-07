# Tasks: 169 — 全画面・画面サイズ・Canvas/EventSystem 基盤の基準化

Issue: #169 — branch `169`。担当 Worker A。実行順に依存あり (1 → 2 → 3/4 → 5)。

## 1. T1: 現状の矛盾の特定と記録

- 対象: `specs/169-fullscreen-canvas-foundation/spec.md` (既存・編集せず確認のみ),
  `ProjectSettings/ProjectSettings.asset` (L48/L49/L92/L107/L119 を引用)。
- 内容: `fullscreenMode:1` が起動時の希望を示すだけであること、実行時の
  `Screen.SetResolution` 呼び出しが 0 件であること (`grep -rn "SetResolution" Assets/Game/Scripts`)、
  `defaultIsNativeResolution:0` と Windows 実機観測の矛盾を spec.md に記録済みであることを確認する。
- 完了条件: spec.md の T1 節が PlayerSettings の実測行を引用し、矛盾を説明している。
  `defaultIsNativeResolution` を 0 → 1 に変更済み (他行は無変更)。

## 2. T2: 画面・解像度の単一契約 (UISizeProfile)

- 対象: `Assets/Game/Scripts/UI/V5/UISizeProfile.cs`。
- 内容: `UISizeProfile.Select(w,h)` を唯一の選択経路とし、既存規則
  (height>=1440 → 2k / |w/h-1.6|<0.05 → mac / height>=1080 → fhd / else FhdScaled) を維持。
  `CanvasConfigurator` を唯一の CanvasScaler 設定経路 (Constant Pixel Size x1) とする。
- 完了条件: Select 画面独自の計算 (SelectView.cs) には触れず Known Issues に記録。
  タスク 5 の 3 解像度テストが概念上通る形になっている。

## 3. T3: EventSystem / GraphicRaycaster の単一系統化

- 対象: `Assets/Game/Scripts/UI/V5/UISizeProfile.cs` (`CanvasConfigurator`)。
- 内容: `Configure(Canvas)` を冪等化 — CanvasScaler / GraphicRaycaster /
  CanvasConfigurator の重複付与を排除し、`EventSystem` が無い場合のみ
  `EventSystem` + `StandaloneInputModule` を生成する `EnsureEventSystem()` を追加。
- 完了条件: 同一 Canvas への 2 回 `Configure` で EventSystem が 1 個、
  GraphicRaycaster が 1 個であることをタスク 5 のテストで検証。

## 4. T4: 診断手段 (FullscreenDiagnostics)

- 対象: `Assets/Game/Scripts/UI/V5/FullscreenDiagnostics.cs` (新規)。
- 内容: `[RuntimeInitializeOnLoadMethod]` で起動時に Screen.width/height,
  currentResolution, fullScreenMode, fullScreen, dpi, Display.displays の
  systemWidth/Height を 1 行ログ出力。Development ビルド / エディタ限定で
  F3 押下時に再出力 + `Application.CaptureScreenshot` で保存。
- 完了条件: コンパイル対象 (FlashBeat asmdef 内) であり、実機ログで
  全画面ピクセル被覆を客観確認できる形式になっている。実行検証は Supervisor。

## 5. T5: EditMode テスト

- 対象: `Assets/Game/Tests/` (新規: `FlashBeat.V5CanvasTests.asmdef` + `V5CanvasFoundationTests.cs`)。
- 内容:
  1. `Configure` 後 `CanvasScaler.uiScaleMode == ConstantPixelSize && scaleFactor == 1`。
  2. Raycaster 無し Canvas に `GraphicRaycaster` が付与される。
  3. 2 回 `Configure` しても `EventSystem` が 1 個 (重複生成されない)。
  4. `Select(1920,1080)=fhd` / `Select(2560,1440)=2k` / `Select(2560,1664)=2k` で
     代表寸法 (Btn / CardMid.x / RowH) が各画面寸法内に収まる。
  5. `Select(1440,900)=mac` — 16:10 がアスペクト比で選ばれる (画素数ではない)。
- 完了条件: テストコードと期待値がコード上で明示されている。
  実行は Supervisor が EditMode で行う (Worker A は Unity Editor を起動しない)。
