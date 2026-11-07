# Plan: 169 — 全画面・画面サイズ・Canvas/EventSystem 基盤の基準化

Issue: #169 (親 #162) — branch `169`, base `release-0-1-11` @ `2f2b6ba`。担当 Worker A。

## 目標

「全画面にならない」実機観測に対し、(1) 設定と実挙動の矛盾をコードから特定して記録し、
(2) 画面サイズ・Canvas・EventSystem を単一契約に整理し、(3) 実行時に測定できる診断手段を
作る。ProjectSettings の値変更だけ「修正済み」としない。

## 技術判断

### 画面契約 (T2)

- プロファイル選択は `UISizeProfile.Select(w,h)` に一元化し、Issue #162 指示の規則を採用:
  1. 縦横比 ~1.538 (±0.03, Mac ネイティブ 2560x1664) → mac
  2. 縦横比 ~1.778 (±0.03) かつ height>=1440 → 2k
  3. height>=1080 → fhd
  4. else → FhdScaled(min(1, height/1080))
  旧 1440x900 (縦横比 1.6) 用の規則は廃止。
- **mac プロファイルは 2560x1664 実ピクセル用に再構築** (旧 1440x900 設計は無効)。
  `design/ui-size-profiles.json` (profiles.mac / grid.mac / controls_v3.mac) を
  1440→2560 倍率 (16/9) で再スケールし、`UISizeProfile.Mac()` に同じ値を写す。
  json↔C# の値一致は検証スクリプトで確認。
- 3解像度 (1920x1080 / 2560x1440 / 2560x1664) で代表寸法が画面内に収まることを
  EditMode テストで担保する。旧 1440x900 は fhd 縮小 (x0.833) に流れる。
- `CanvasConfigurator` (Constant Pixel Size x1) を唯一の CanvasScaler 設定経路とする。
  Select 画面独自の u 倍計算は SelectView.cs (Worker B 所有) にあるため本タスクでは
  変更せず Known Issues に記録する。

### フルスクリーン方針 (Issue #162 オーナー指示)

- 実フルスクリーンのみ: `fullscreenMode: 1` (FullScreenWindow) +
  `defaultIsNativeResolution: 1` (ネイティブ解像度で起動)。
- `Screen.SetResolution` によるウィンドウリサイズでの擬似全画面は禁止。
  実装 0 件を確認済み (`grep -rn "SetResolution" Assets/Game/Scripts`)。

### Canvas / EventSystem / Raycaster (T3)

- `CanvasConfigurator.Configure(Canvas)` を冪等にする:
  - `CanvasScaler` が無ければ付与、Constant Pixel Size x1 を設定。
  - `GraphicRaycaster` が無ければ付与 (重複は生成しない)。
  - `CanvasConfigurator` 自身も重複 AddComponent しない
    ([DisallowMultipleComponent] のため 2 回目の AddComponent はエラーになる)。
  - `EventSystem` がシーンに 1 つも無ければ `EventSystem` + `StandaloneInputModule`
    を生成 (再生中は DontDestroyOnLoad)。既にあれば生成しない。
- 旧 Canvas を `SetActive(false)` にする方式と v5 動的 Canvas の併用でも
  EventSystem が重複しないことをテストで担保する。

### 診断 (T4)

- `Assets/Game/Scripts/UI/V5/FullscreenDiagnostics.cs` を新規追加。
  `[RuntimeInitializeOnLoadMethod]` で起動時 1 回、Screen.width/height,
  currentResolution, fullScreenMode, fullScreen, dpi, Display 情報を 1 行ログ出力。
  Development ビルド / エディタ限定で F3 再出力 + スクリーンショット保存。
  実機の全画面ピクセル被覆を客観測定するための手段であり、設定値の断定はしない。

### PlayerSettings (T1 の最小対処)

- `ProjectSettings/ProjectSettings.asset` の `defaultIsNativeResolution: 0 → 1` のみ変更。
  他の行 (fullscreenMode / resizableWindow / defaultScreenWidth/Height) は現状維持。
  効果の判断は FullscreenDiagnostics の実機ログで行う (Supervisor)。

### テスト (T5)

- 新規 `Assets/Game/Tests/` (asmdef `FlashBeat.V5CanvasTests`, EditMode のみ)。
  既存 `Assets/Tests/Editor/FlashBeat.Tests.asmdef` は他 Worker と共有のため触らず、
  新しい asmdef を作る。参照: FlashBeat, UnityEngine.UI, nunit, TestRunner。
  例外: 旧 mac 規則を前提とした `Assets/Tests/Editor/V5UiTests.cs` の
  `Select_1440x900_ReturnsMac` のみ規則改訂に伴い期待値を更新。

## リスク

- EditMode での `DontDestroyOnLoad` は無意味なため `Application.isPlaying` でガードする。
- `FindObjectOfType<EventSystem>` はシーン上の非アクティブ含めて検索しない (既定動作)。
  旧 Canvas が EventSystem を持つケースはアクティブなものだけ数える方針をテストで明示。
- Unity Editor は起動しない (排他)。テスト実行は Supervisor が行う。
