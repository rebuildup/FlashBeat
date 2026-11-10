---
description: "FlashBeat lean spec — Issue 096"
---

# Spec: Title/Result/Option/Opening の UI を 1024x768〜2560x1440 で崩れなくする

**Issue**: #96 | **Branch**: `w/spec-wingate` | **Date**: 2026-11-10 | **Status**: Draft

**Input**: `[Unity/P1] Title/Result/Option/Opening の UI を 1024x768〜2560x1440 で崩れなくする`

## Objective

Issue #96 は「Title / Result / Option / Opening の 4 画面が 1024×768〜2560×1440 の範囲で
崩れないようにする」ことを求める。実機証拠として
`branch evidence/win-699b461` の `01_title.png` / `07_result.png` / `08_retry.png` を挙げている。

`main` には **v5 の `UISizeProfile` 体系 (FHD / 2K / Mac / FhdScaled)** と
**v6 の `V6UiProfile` (1920×1080 / 2560×1440 / 2560×1664)** が既に実装され、
json ↔ C# の一致を EditMode テストが固定している。

本 spec の目的は、**残った未充足条件** —

1. **複数解像度での実機目視 (スクリーンショット) が取得されていない** —
   を 1 つの作業単位に固定し、**「プロファイル座標の EditMode 検証は実描画の見切れ・重なりを
   保証しない」** という区別を明示することにある。

**加えて、Issue の要求する解像度範囲 (1024×768〜2560×1440) と、
`docs/operations/three-resolution-fullscreen-verification.md` §0 が定める
検証対象 (FHD 1920×1080 / 2K 2560×1440 / MAC 2560×1664、**FHD 未満は対象外**) が
**一致していない** 点は、オーナー判断が必要な論点として本 spec に記録する。

## Decisions (凍結する設計)

| 項目 | 決定 |
| --- | --- |
| 担当範囲 | **Title / Result / Option / Opening** の 4 画面のみ。**Select と Game は #88 の担当** (Issue 本文 / 監査 §3.2 #96 条件 3)。本 spec は Select / Game に触れない |
| v5 の既存 UI | `Assets/Game/Scripts/UI/V5/UISizeProfile.cs` の `UISizeProfile.Fhd()` (`:87`) / `.TwoK()` (`:98`) / `.Mac()` (`:112`) / `.FhdScaled(float)` (`:127`) / `.Select(width, height)` (`:151`) と `CanvasConfigurator.Configure(canvas)` (`:217`) を **そのまま使う**。新しいプロファイルは作らない |
| v6 の既存 UI | `Assets/Game/Scripts/UI/V6/Common/V6UiProfile.cs:71` `Known` (`:73` FHD 1920×1080 / `:74` 2K 2560×1440 / `:75` MacBook 2560×1664) と `:86` `For(screenW, screenH)` (未登録解像度は最も近いプロファイル + 中央配置の origin) を **そのまま使う**。**スケールは変えない** (UI を引き伸ばして崩れる根源を型のレベルで断つ) |
| 1024×768 の扱い | **現行の `UISizeProfile.Select` は 1024×768 を明示的なプロファイルとして持たない**。`:164` の `height >= 1080` に当たらず、`:166` の `FhdScaled(min(1, height/1080))` = **FhdScaled(0.711)** に落ちる (= 1080 相当を縦方向に 71.1% 縮小)。**縮小で clip しないことが設計上の想定** (`design/ui-size-profiles.json` の `select` 記載も同様)。**1024×768 の profile を新設するか否かはオーナー判断** (本 spec では新設しない) |
| 解像度検証の優先順 | `docs/operations/three-resolution-fullscreen-verification.md` §0 の **FHD 1920×1080 / 2K 2560×1440 / MAC 2560×1664** が **実務上の検証対象**。Issue 本文の下限 1024×768 は **未証明のまま残す** (実機パネルが無い場合は「未証明」と記録) |
| 擬似全画面の禁止 | **ウィンドウのドラッグ / リサイズで「そのサイズに見せかける」擬似再現は禁止。** リサイズで得たスクリーンショットは **証拠にならない** (`docs/operations/exhibit-handoff-2026-11-08.md` §1 / `docs/operations/three-resolution-fullscreen-verification.md` §1) |
| 実機操作 | **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。** macOS 実機 (2560×1664) での Title 描画は **既に証明済** (`docs/evidence/mac-verify-run-2026-11-08/`)。**Select / Game は #88** |
| EditMode テストの位置づけ | `Assets/Game/Tests/FullscreenPolicyTests.cs` の 10 テストは **設定値と json↔C# の一致**を固定するのみ。**実描画の見切れ・重なりは保証しない**。**EditMode 緑 ≠ 実機合格** |
| 実装の境界 | 本 spec はドキュメントのみ。**UI コードは main に実装済**。未充足分は **実機目視の観測と記録** |

## 非目標 (Out of scope)

- **Select / Game 画面の全画面・崩れ対応** (#88 の担当)。
- v6 UI の Pencil デザイン移植 (#217 / #219 / #221 / #239 / #240 / #241 の担当)。
- 新 resolutions プロファイルの追加 (オーナー判断事項として記録するだけ)。
- フォントの豆腐 (□) 問題の根本対策 (#140 の担当)。
- **`Assets/**` / `docs/ui/**` のコード・文書の編集** (本 spec はドキュメントのみ。

## Acceptance criteria

1. Title / Result / Option / Opening の **4 画面すべて**について、実機スクリーンショットが取得され、
   **見切れ・重なり・文字切れが無い** ことが目視で確認されていること。
2. 取得するスクリーンショットは **実ディスプレイの全画面** で撮影されたものであり、
   ウィンドウ枠 / タスクバー / 黒帯が 1px でも写っていないこと。
3. 撮影は **1920×1080 (FHD) / 2560×1440 (2K) / 2560×1664 (Mac)** の実機で確認され、
   各解像度の実効値が `FullscreenDiagnostics` の Player.log 行で確認できること。
4. **1024×768** については、**実機パネルが確保できた場合は** 同様に撮影し、
   **確保できなかった場合は「未証明」と明記**すること (緑を作らない)。
5. Issue 本文が挙げる `01_title.png` / `07_result.png` / `08_retry.png` に相当する
   4 画面分のスクリーンショットが、**どの解像度で撮ったかが分かる形で** 記録に残ること。
6. `FullscreenPolicyTests.cs` の 10 テストが引き続き緑で、json ↔ C# の一致が保たれていること
   (**これは実機受入の代替ではない**)。
7. 既存 27 曲と v5 / v6 の両スタックが同時に正しく描画すること (v5 / v6 排他は #233 の領域)。
8. 撮影・目視の結果は **「問題なし」か「具体的な崩れ的内容」** のどちらかで記録され、
   崩れがあれば起票が切られていること。

## 現在の実装状況 (2026-11-10 時点)

`origin/main` = `405555c` を実測した結果。

### 充足 (コード)

| 条件 | 根拠 (file:line) | 判定 |
| --- | --- | --- |
| 1: Title / Result / Option / Opening が 1024×768〜2560×1440 で崩れない (コード) | `Assets/Game/Scripts/UI/V5/UISizeProfile.cs:87` `Fhd()` / `:98` `TwoK()` / `:112` `Mac()` / `:127` `FhdScaled(float)` / `:151` `Select(int width, int height)` (`:158` mac / `:162` 2K / `:164` FHD / `:166` FhdScaled) / `:217` `CanvasConfigurator.Configure(canvas)` (Constant Pixel Size x1、解像度変化で再適用) / `Assets/Game/Scripts/UI/V6/Common/V6UiProfile.cs:73-75` (FHD / 2K / MacBook) / `:86` `For(screenW, screenH)` | 充足 (コード) |
| json ↔ C# の一致が固定されている | `Assets/Game/Tests/FullscreenPolicyTests.cs` — `:45` `ProjectSettings_FullScreenMode_IsFullScreenWindow` / `:52` `ProjectSettings_DefaultIsNativeResolution_IsTrue` / `:59` `ProjectSettings_ResizableWindow_IsFalse` / `:68` `GameScripts_HaveNoScreenSetResolution` / `:146` `JsonMacScreen_Is2560x1664` / `:155` `JsonFhdScreen_Is1920x1080` / `:163` `JsonTwoKScreen_Is2560x1440` / `:171` `FhdProfile_MatchesJson` / `:199` `TwoKProfile_MatchesJson` / `:227` `MacProfile_MatchesJson` (計 10 テスト) / `design/ui-size-profiles.json` | 充足 |
| 4 画面への適用が既存実装でされている | `Assets/Game/Scripts/UI/V5/OpeningView.cs:39` `CanvasConfigurator.Configure(canvas)` / `Assets/Game/Scripts/UI/V5/V5GameUi.cs:24` / `Assets/Game/Scripts/UI/V6/Game/Playfield/V6PlayfieldRuntime.cs:61` | 充足 (一部) |
| Select / Game は #88 が担当 | Issue 本文 / 監査 §3.2 #96 条件 3 | スコープ外 |

### 未充足 / 未検証

| # | 条件 | 判定 | 根拠 |
| --- | --- | --- | --- |
| 1 | 実機スクリーンショット (01_title.png / 07_result.png / 08_retry.png) での確認 | **未充足 (解像度カバレッジ不足)** | 実在する PNG の実解像度を `file` コマンドで実測した結果: `docs/evidence/win64-2026-11-06/*.png` は **7 枚すべて 2560×1440** (01-title / 02-select-no1 / 03-select-no2-manimani / 04-game-playing / 05-result / 06-retry-game-reloaded / 07-result-run2-rostoreamblera) / `docs/evidence/win64-2026-11-06-issue13/*.png` は **11 枚すべて 1296×759** (ウィンドウ枠込み = クライアント 1280×720) / `docs/evidence/233-hud-nre/*.png` は **2560×1664** (Mac 実機)。**すなわち 2 解像度 (2560×1440 / 1280×720 相当) のみ。1024×768 と 1920×1080 の撮影が無い。** また Issue 本文が挙げる `08_retry.png` は **`docs/evidence/` に実在しない** (最後は `07-result-run2-rostoreamblera.png`) |
| 2 | Title の実機描画 | **充足 (1 解像度のみ)** | `docs/evidence/mac-verify-run-2026-11-08/` に **2560×1664** の実機スクリーンショット (v6 Title の描画: ロゴ / 背景動画 / 自己ベスト / 更新通知)。`docs/evidence/win64-2026-11-06/01-title.png` が 2560×1440。**1920×1080 と 1024×768 は未撮影** |
| 3 | Result / Option / Opening の実機描画 | **部分充足** | Result は `docs/evidence/win64-2026-11-06/05-result.png` (2560×1440) と `07-result-run2-rostoreamblera.png` / `win64-2026-11-06-issue13/10-result.png` (1296×759)。Option は `win64-2026-11-06-issue13/12-option-cache-panel.png` (1296×759) のみ。**Opening は 4 画面の中で実機スクリーンショットが最も少ない** |
| 4 | 複数解像度の切替 (実行中の解像度変更) での挙動 | **未検証** | `CanvasConfigurator` は解像度変化を検知して再適用する (`UISizeProfile.cs:175` コメント)。**実機での解像度変更操作の記録が無い** |
| 5 | 1024×768 での挙動 | **未検証 (コードは充足)** | `UISizeProfile.Select(1024, 768)` は `FhdScaled(min(1, 768/1080))` = **FhdScaled(0.7111)** に落ちる (`UISizeProfile.cs:166`)。**この縮小が 4 画面で見切れ・重なり-less である保証はコードのみからは出ない**。**1024×768 の実機パネルが展示 PC に無い** |
| 6 | 4 画面すべて (Option / Opening を含む) の v6 版描画 | **未検証** | v6 の Title / Result / Option は `V6TitleView` / `V6ResultView` / `V6OptionView` に実装されているが、**実機スクリーンショットの記録は Title のみ**。Option は v5 のスクリーンショットのみ |

### ブロッカー

- **Windows 実機 (展示 PC)** が要る。cc は展示 PC を操作しない。
- **1920×1080 の実機パネル** — 展示 PC のディスプレイは 2560×1440 / 1920×1080 / 1280×720 を持つとされるが
  **「どのパネルが今利用可能か」は PC の管理状況次第** (`docs/evidence/index.md` §1.6 の記載を参照)。
- **1024×768 の実機パネル** — 存在しない可能性が高い。**確保できない場合は「未証明」と記録**。
- **Option / Opening の実機描画の少なさ** — 撮影対象を増やす必要がある。
- **v5 / v6 の排他描画** (#233) が未解決のまま、Option / Opening のスクリーンショットが
  どちらのスタックのものかを区別しにくい可能性がある。

### 参考

- `docs/audit/open-issues-audit.md` §3.2 の #96 節 (充足 2 / 未充足 1 / 未検証 2) が本 spec の根拠。
- `docs/audit/open-issues-audit.md` §4 **U7** (Win64 実機解像度スイープ) が #96 と #88 を束ねる作業単位を指している。
- macOS 側の 3 解像度手順は `docs/operations/three-resolution-fullscreen-verification.md` §3。
