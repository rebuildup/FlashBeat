---
description: "FlashBeat lean tasks — Issue 096"
---

# Tasks: Title/Result/Option/Opening の解像度対応 (Issue #96)

**Issue**: #96 | **Branch**: `w/spec-wingate` | **Spec**: [`spec.md`](spec.md) / [`plan.md`](plan.md)

## Tasks

- [x] **T1** v5 / v6 の解像度プロファイルと適用経路が `main` に実装されていることを確認する。
  — **done when**: 各プロファイルと適用箇所に file:line が付いていること
  — **実測**: `Assets/Game/Scripts/UI/V5/UISizeProfile.cs:87` `Fhd()` / `:98` `TwoK()` /
  `:112` `Mac()` / `:127` `FhdScaled(float)` / `:151` `Select(int width, int height)`
  (`:158` mac / `:162` 2K / `:164` FHD / `:166` FhdScaled) / `:217` `CanvasConfigurator.Configure(canvas)` /
  `Assets/Game/Scripts/UI/V6/Common/V6UiProfile.cs:71` `Known` / `:73` FHD 1920×1080 /
  `:74` 2K 2560×1440 / `:75` MacBook 2560×1664 / `:86` `For(screenW, screenH)` /
  `Assets/Game/Scripts/UI/V5/OpeningView.cs:39` `CanvasConfigurator.Configure(canvas)` /
  `Assets/Game/Scripts/UI/V5/V5GameUi.cs:24` /
  `Assets/Game/Scripts/UI/V6/Game/Playfield/V6PlayfieldRuntime.cs:61` /
  `design/ui-size-profiles.json` (正本)。

- [x] **T2** json ↔ C# の一致と PlayerSettings の全画面設定が EditMode テストで固定されている
  ことを確認する。
  — **done when**: テスト名と file:line が記録されていること
  — **実測**: `Assets/Game/Tests/FullscreenPolicyTests.cs` (10 テスト) —
  `:45` `ProjectSettings_FullScreenMode_IsFullScreenWindow` /
  `:52` `ProjectSettings_DefaultIsNativeResolution_IsTrue` /
  `:59` `ProjectSettings_ResizableWindow_IsFalse` /
  `:68` `GameScripts_HaveNoScreenSetResolution` /
  `:146` `JsonMacScreen_Is2560x1664` / `:155` `JsonFhdScreen_Is1920x1080` /
  `:163` `JsonTwoKScreen_Is2560x1440` / `:171` `FhdProfile_MatchesJson` /
  `:199` `TwoKProfile_MatchesJson` / `:227` `MacProfile_MatchesJson`。
  関連 `Assets/Tests/Editor/TitleOptionV5Tests.cs` / `ResultV5ViewTests.cs` / `V5UiTests.cs` /
  `Assets/Game/Tests/V6CanvasTests/V6UiProfileTests.cs`。
  **これらは設定値と座標の一致の固定であり、実描画の見切れ・重なりを保証しない。**

- [x] **T3** 既存の実機スクリーンショットの実解像度を `file` コマンドで実測し、
  解像度カバレッジの穴を特定する。
  — **done when**: 各 PNG の実解像度が記録され、不足が明記されていること
  — **実測**: `docs/evidence/win64-2026-11-06/*.png` = **7 枚すべて 2560×1440**
  (01-title / 02-select-no1 / 03-select-no2-manimani / 04-game-playing / 05-result /
  06-retry-game-reloaded / 07-result-run2-rostoreamblera) /
  `docs/evidence/win64-2026-11-06-issue13/*.png` = **11 枚すべて 1296×759**
  (ウィンドウ枠込み = クライアント 1280×720) /
  `docs/evidence/233-hud-nre/*.png` = **2560×1664** (Mac 実機) /
  `docs/evidence/mac-verify-run-2026-11-08/` = **2560×1664** (Title のみ)。
  **不足: 1024×768 と 1920×1080 の撮影が無い。**
  また **Issue 本文が挙げる `08_retry.png` は `docs/evidence/` に実在しない**
  (最後は `07-result-run2-rostoreamblera.png`)。

- [ ] **T4** macOS 実機 (2560×1664) で Title / Result / Option / Opening を撮影する。
  — **done when**: 4 画面分の実全画面スクリーンショットが残り、
  画像実解像度が 2560×1664 であること
  — **備考**: `docs/operations/three-resolution-fullscreen-verification.md` §3 に従う。
  `screencapture -x` を使い **-menubar / -window を付けない** (実全画面)。
  **Option と Opening の macOS 実機記録が現在無い** ので、ここでの差分を埋められる。
  **macOS は Win64 の代替にならない** (`docs/evidence/index.md` §1.6)。

- [ ] **T5** Windows 実機で 1920×1080 と 2560×1440 の実全画面スクリーンショットを、
  4 画面 (Title / Result / Option / Opening) 分取得する。
  — **done when**: 各解像度の 4 画面分のスクリーンショットと、
  Player.log の `[FullscreenDiagnostics]` 行 (screen= / fullScreenMode= / dpi= / displays=) が残ること
  — **備考**: **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**
  `FlashBeat.exe` は **引数なし** で起動。
  **擬似全画面 (ウィンドウのリサイズ) は禁止** — リサイズで得たスクリーンショットは証拠にならない
  (`docs/operations/exhibit-handoff-2026-11-08.md` §1)。
  2K モニタが確保できない場合は **「未証明」と記録**。

- [ ] **T6** 1024×768 の扱いを決める (撮影 or「未証明」と記録)。
  — **done when**: 撮影した場合はスクリーンショットと実効値、
  撮影できなかった場合は「未証明」の記載が残ること
  — **備考**: `UISizeProfile.Select(1024, 768)` は `FhdScaled(min(1, 768/1080))` =
  **FhdScaled(0.7111)** に落ちる (`UISizeProfile.cs:166`)。**1024×768 専用の profile を
  新設するかどうかは別判断** (オーナー)。
  **実機パネルが無い場合は「未証明」と記録** — 緑を作らない。

- [ ] **T7** 画面 × 解像度 のマトリクスで「問題なし / 具体的な崩れの記述」を判定する。
  — **done when**: 判定表が残り、崩れがあれば起票が切られていること
  — **備考**: **スクリーンショット 1 枚で「正常」と読まない**
  (`docs/audit/open-issues-audit.md` §0 の表)。
  Select / Game の崩れは **#88 の担当** だが、tour のために一緒に撮ってもよい
  (判定は #88 側で、#96 では Title / Result / Option / Opening だけを判定する)。

- [ ] **T8** 撮影物と判定表を `docs/evidence/` に置き、`docs/evidence/index.md` の該当行を更新する。
  — **done when**: evidence にスクリーンショットと判定表が残り、index.md が実態と一致すること
  — **備考**: **`docs/evidence/index.md` は他 Worker が所有**しているため、cc は
  **cc 側の統合 PR で** 適用する (本 spec の所有範囲は `specs/096-*/` のみ)。

- [ ] **T9** Issue 本文が挙げる `08_retry.png` の参照が実在しない点の扱いを cc に報告する。
  — **done when**: Issue 本文のファイル名と `docs/evidence/` の実ファイル名の差が報告されていること
  — **備考**: 実在最後は `docs/evidence/win64-2026-11-06/07-result-run2-rostoreamblera.png`。
  **Issue の更新は cc が行う** (本 spec は Issue を変更しない)。

- [ ] **T10** v5 / v6 の排他描画 (#233) の状態を確認し、スクリーンショットが
  どちらのスタックかを区別できる形で記録する。
  — **done when**: 各スクリーンショットに「v5 版 / v6 版」の別が併記されていること
  — **備考**: **排他そのものは #233 の領域**。本 Issue では区別の記録だけ。
  切替 (Tab キー等) が必要なら `docs/ui/issue-167-ui-acceptance.md` を参照。
