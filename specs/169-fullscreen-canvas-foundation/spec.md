# Spec: 169 — 全画面・画面サイズ・Canvas/EventSystem 基盤の基準化

Issue: #169 (親 #162) — branch `169`, base `release-0-1-11` @ `2f2b6ba`.
担当: Worker A (実装) / Supervisor (Unity Editor 起ち上げ・EditMode テスト実行・実機検証)。

## 目的

「全画面にならない」実機観測に対し、(1) 設定と実挙動の矛盾をコードから特定して記録し、
(2) 画面サイズ・Canvas・EventSystem を単一契約に整理し、(3) 実行時に測定できる診断手段を
作る。**ProjectSettings の値変更だけで「修正済み」とはしない** — 実機での客観測定
(FullscreenDiagnostics のログ) で初めて判断する。

## T1: 現状の矛盾の特定と記録

`ProjectSettings/ProjectSettings.asset` の該当行 (実測):

```yaml
defaultScreenWidth: 1920          # L48
defaultScreenHeight: 1080         # L49
defaultIsNativeResolution: 0      # L92  ← ネイティブ解像度を使わない
resizableWindow: 1                # L107
fullscreenMode: 1                 # L119 (FullScreenWindow)
```

矛盾の説明 (コードから):

1. `fullscreenMode: 1` は起動時の **希望** を示すだけ。`defaultIsNativeResolution: 0` の
   場合 Unity は defaultScreenWidth/Height (1920x1080) でウィンドウを作り、OS 側の
   DPI スケーリングやディスプレイ構成によっては的实际解像度が一致しない。
   Windows 実機で「全画面にならない」ように見える一因はここ。
2. 実行中に `Screen.SetResolution` を呼ぶコードは存在しない
   (`grep -rn "SetResolution" Assets/Game/Scripts` → 0件)。起動時設定のみに依存。
3. 実行時の実際のモード (FullScreenWindow / ExclusiveFullscreen / Windowed) と
   実効 DPI・アスペクトはどこにも記録されず、「設定値 ≠ 実挙動」を検証する手段が無かった。
4. macOS の Retina (2560x1664, 16:10) では `UISizeProfile.Select` の mac 分岐
   (|w/h-1.6|<0.05) で選ばれるが、単純画素数比較 (height>=1440 → 2k) と混同すると
   誤選択する。画素数だけで 16:10 を除外しないこと。

## T2: 画面・解像度の単一契約 (UISizeProfile)

- プロファイル選択規則は `UISizeProfile.Select(w,h)` に一元化 (Issue #162 指示で改訂):
  1. 縦横比 ~1.538 (±0.03, Mac ネイティブ 2560x1664) → mac
  2. 縦横比 ~1.778 (±0.03) かつ height>=1440 → 2k
  3. height>=1080 → fhd
  4. else → FhdScaled(min(1, height/1080))
- **mac プロファイルは 2560x1664 実ピクセル用に再構築** (旧 1440x900 設計は無効)。
  `design/ui-size-profiles.json` の profiles.mac / grid.mac / controls_v3.mac を
  1440→2560 倍率 (16/9) で再スケールし、`UISizeProfile.Mac()` が同じ値を写す
  (json と C# の値の一致をスクリプトで検証済み)。
- 3解像度での破綻がないことを EditMode テストで明示:
  FHD 1920x1080 / 2K 2560x1440 / Mac 2560x1664。旧 1440x900 (16:10) は mac に
  選ばれず fhd 縮小 (x0.833) に流れる。
- `CanvasConfigurator` を唯一の CanvasScaler 設定経路とする
  (Constant Pixel Size ×1、解像度変化で再適用)。既存の `UISizeProfile.Current` キャッシュは温存。

## T3: EventSystem / GraphicRaycaster の単一系統化

- `CanvasConfigurator.Configure(Canvas)` が (a) CanvasScaler 設定に加え、
  (b) **EventSystem が 1 つも無ければ生成** (`EventSystem` + `StandaloneInputModule`,
  DontDestroyOnLoad)、既にあれば生成しない (重複排除)。
- (c) Canvas に `GraphicRayCanvas` が無ければ付与 — raycast の死に Canvas を排除。
- 旧 Canvas をSetActive(false)にする方式と v5 動的 Canvas の併用でも、
  EventSystem が重複生成されないことをテストで担保。

## T4: 診断手段 (FullscreenDiagnostics)

新規 `Assets/Game/Scripts/UI/V5/FullscreenDiagnostics.cs`:

- `[RuntimeInitializeowOnLoad]` で起動時に 1 回、以下を `Debug.Log` 1 行 JSON 風に出力:
  `Screen.width/height`, `Screen.currentResolution`, `Screen.fullScreenMode`,
  `Screen.fullScreen`, `Screen.dpi`, `Display.displays.Length`, 各 display の
  `systemWidth/Height`, `renderingWidth/Height`。
- Development ビルド限定で `F3` キー押下時に同内容を再出力
  (`#if DEVELOPMENT_BUILD || UNITY_EDITOR`)。
- PlayerSettings を変えずに **実機で客観測定** できるのが目的。設定値の断定はしない。

## T5: EditMode テスト (Assets/Game/Tests/)

新規 `Assets/Game/Tests/` (EditMode のみ, asmdef `FlashBeat.V5CanvasTests`):

1. `CanvasConfigurator_Configure_SetsConstantPixelSize` — Configure 後
   `CanvasScaler.uiScaleMode == ConstantPixelSize && scaleFactor == 1`。
2. `CanvasConfigurator_Configure_AddsGraphicRaycaster` — Raycaster 無し Canvas に付与される。
3. `CanvasConfigurator_EnsureEventSystem_CreatesExactlyOne` — Canvas を 2 回 Configureしても
   EventSystem は 1 つ。
4. `ProfileSelect_ThreeResolutions` — Select(1920,1080)=fhd /
   Select(2560,1440)=2k / Select(2560,1664)=mac、かつ各プロファイルの代表寸法が
   自画面内に収まる (Btn/Card/Drawer の代表値で screen 以下)。
5. `ProfileSelect_MacByAspect_Before2kPixelRule` — 2560x1664 (縦横比 1.538) が
   height>=1440 の 2k 規則より先に mac に選ばれる。
6. `ProfileSelect_1440x900_NoLongerMac` / `ProfileSelect_1280x720_FhdScaled` —
   旧 mac 規則廃止後は fhd 縮小 (x0.833 / x0.667) に流れる。

## Known Issues / 非所有ファイル

- **フルスクリーン方針 (Issue #162 オーナー指示)**: `fullscreenMode: 1`
  (FullScreenWindow) + `defaultIsNativeResolution: 1` の実フルスクリーンのみ。
  `Screen.SetResolution` によるウィンドウリサイズでの擬似全画面は禁止
  (実装 0 件を確認済み、今後も追加しない)。
- 実機での全画面確認 (Windows) は Supervisor が FullscreenDiagnostics のログで実施。
- `defaultIsNativeResolution: 0 → 1` の最小変更のみ ProjectSettings に施す
  (fullscreenMode / resizableWindow / defaultScreenWidth / defaultScreenHeight は現状維持)。
- Worker B 所有の View / SceneManager / .unity には触れない。
  (例外: 旧 mac 規則を前提とした既存テスト `Assets/Tests/Editor/V5UiTests.cs` の
  `Select_1440x900_ReturnsMac` 1 件のみ、規則改訂に伴い期待値を更新。)
