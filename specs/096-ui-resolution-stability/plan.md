---
description: "FlashBeat lean implementation plan — Issue 096"
---

# Plan: Title/Result/Option/Opening の解像度対応 (Issue #96)

**Issue**: #96 | **Branch**: `w/spec-wingate` | **Spec**: [`spec.md`](spec.md) | **Date**: 2026-11-10

## Architecture

本 Issue の実体は **コードではなく「実機での目視観測と記録」** である。
main の既存構成 (変更しない) と、誰が何を観測するかを以下に示す。

```
[既存の解像度制御 — main に実装済 / 変更しない]
  v5 スタック
    UISizeProfile.cs:151  Select(width, height)
      ├─ aspect ~1.538 → Mac()        (2560×1664 ネイティブ)
      ├─ aspect ~1.778 && h>=1440 → TwoK()  (2560×1440)
      ├─ h >= 1080 → Fhd()            (1920×1080)
      └─ それ以外 → FhdScaled(min(1, h/1080))   ← 1024×768 は 0.7111 倍に落ちる
    UISizeProfile.cs:217  CanvasConfigurator.Configure(canvas)
      Constant Pixel Size x1 / 解像度変化を検知して再適用
    design/ui-size-profiles.json  ← 正本 (json ↔ C# は FullscreenPolicyTests が固定)

  v6 スタック
    V6UiProfile.cs:73-75  Known = FHD 1920×1080 / 2K 2560×1440 / MacBook 2560×1664
    V6UiProfile.cs:86  For(screenW, screenH)
      完全一致 → そのプロファイル / 未登録 → 最も近いプロファイル + origin のみ中央配置
      FrameW/FrameH は 1728×972 のまま (スケールしない)

[観測 — macOS 側 (cc)]
  docs/operations/three-resolution-fullscreen-verification.md §3
    screencapture -x /tmp/flashbeat-mac-2560x1664.png
    → 画像サイズが 2560×1664 であること (-menubar/-window を付けない全画面)
  既存: docs/evidence/mac-verify-run-2026-11-08/  (2560×1664 / Title のみ)

[観測 — Windows 実機 (展示 PC) — オーナー実施]
  FlashBeat.exe (引数なし・直接起動)
    ├─ 実ディスプレイで全画面 → 撮影 (枠・タスクバー・黒帯なし)
    ├─ Player.log の [FullscreenDiagnostics] 行 (screen= / fullScreenMode= / dpi= / displays=)
    ├─ Title / Select / Result / Option / Opening を順に撮影
    │     ※ Select / Game の崩れ対応は #88 の担当だが、
    │       画面 tour のために併せて撮るのは可 (判定は #88 側)
    └─ 3 解像度 (1920×1080 / 2560×1440 / 2560×1664) で繰り返す

  禁止: ウィンドウのリサイズで「そのサイズに見せかける」擬似全画面
        → リサイズで得たスクリーンショットは 証拠にならない
          (exhibit-handoff-2026-11-08.md §1 / three-resolution-fullscreen-verification.md §1)

[判定]
  見切れ / 重なり / 文字切れ の有無を 画面 × 解像度 で記録
  問題あり → 起票 (この spec の直接の成果物は「記録」であり、
             崩れの修正は #167 / #88 / #233 の領域)
```

## 実装順序

本 Issue に **新規コードは不要** (1024×768 の profile 新設はオーナー判断。Options にはしない)。順序は「撮影 → 判定 → 記録」。

1. **撮影対象の一覧化** (cc 側, macOS):
   - 画面 = Title / Result / Option / Opening (Issue #96 の担当) + Select / Game (参考、判定は #88)。
   - 解像度 = FHD 1920×1080 / 2K 2560×1440 / MAC 2560×1664。
   - 既存 PNG の実解像度を `file` コマンドで再確認し、**どこが欠けているか** を確定する
     (現状: 2560×1440 と 1280×720 相当の 2 解像度のみ)。
2. **macOS 実機での撮影** (cc 側, macOS):
   - `docs/operations/three-resolution-fullscreen-verification.md` §3 に従い、2560×1664 で
   Title / Result / Option / Opening を撮る。**Option と Opening の macOS 実機記録が無い**ため、
   ここでの差分を埋められる。
   - 撮影画像は **実全画面** (`screencapture -x`、-menubar / -window を付けない)。
3. **Windows 実機での撮影** (オーナー実施):
   - 手順は `docs/operations/win64-candidate-acceptance-2026-11-10.md` §3.3。
   - 各解像度で `FlashBeat.exe` を **引数なし** で起動 → 4 画面を撮影 →
   Player.log の `[FullscreenDiagnostics]` 行を回収。
   - **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**
4. **1024×768 の扱い** (オーナー判断):
   - 実機パネルが確保できたなら同手順で撮影する。
   - 確保できなかったなら **「未証明」と記録** する (緑を作らない)。
   - **1024×768 の profile を新設するかどうかは別判断** (`UISizeProfile.Select` は
   FhdScaled(0.711) で対応済み)。
5. **目視判定と記録** (cc 側):
   - 画面 × 解像度 のマトリクスで「問題なし / 具体的な崩れの記述」を記録。
   - 崩れがあれば起票し、本 Issue の受入には **「問題なし or 起票済み」** のどちらかが残るようにする。
6. **EditMode テストの再確認** (cc 側):
   - `Assets/Game/Tests/FullscreenPolicyTests.cs` の 10 テストが緑であること
   (設定値の一致の固定。**実機受入の代替ではない**)。
7. **記録の反映** (cc 側, 統合 PR):
   - スクリーンショットと判定表を `docs/evidence/` に置く。
   - `docs/evidence/index.md` の該当行を更新 (**別 Worker / cc の統合 PR**)。

## ブロッカー

| # | ブロッカー | 影響 | 解除方法 |
| --- | --- | --- | --- |
| B1 | **Windows 実機 (展示 PC)** | FHD / 2K の撮影がブロック | **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。** `docs/operations/win64-candidate-acceptance-2026-11-10.md` §3 |
| B2 | **1920×1080 / 2560×1440 の実機パネル** | 該当解像度の撮影がブロック | 展示 PC のパネル構成の確認。**2K が確保できない場合は「未証明」と記録** (`three-resolution-fullscreen-verification.md` §0) |
| B3 | **1024×768 の実機パネル** | 下限解像度の撮影がブロック | 存在しない可能性が高い。**「未証明」と記録** (緑を作らない) |
| B4 | **Option / Opening の実機記録の少なさ** | 4 画面のうち 2 画面のカバレッジが薄い | macOS 実機 (cc 側) と Windows 実機 (オーナー) の両方で埋める |
| B5 | **v5 / v6 の排他描画** (#233) | スクリーンショットがどちらのスタックか紛らわしい | 撮影時に「v5 版 / v6 版」と併記する。排他そのものは #233 の領域 |
| B6 | **Issue 本文の `08_retry.png` が実在しない** | Issue 側の参照が古い | `docs/evidence/` の実ファイルは `07-result-run2-rostoreamblera.png` が最後。**Issue の更新は cc が行う** |

## テスト戦略

| 層 | 手段 | 本 Issue で可否 |
| --- | --- | --- |
| 設定値固定 | `Assets/Game/Tests/FullscreenPolicyTests.cs` (10 テスト) | **可** (cc 側)。**実描画の見切れ・重なりを保証しない** |
| v5 画面の EditMode | `Assets/Tests/Editor/TitleOptionV5Tests.cs` / `ResultV5ViewTests.cs` / `V5UiTests.cs` | 既存。**実機受入の代替ではない** |
| v6 プロファイル | `Assets/Game/Tests/V6CanvasTests/V6UiProfileTests.cs` | 既存。**実機受入の代替ではない** |
| 実機目視 (macOS) | 2560×1664 の実全画面撮影 + 目視 | **可** (cc 側)。**ただし Win64 の代替にならない** |
| 実機目視 (Windows) | 1920×1080 / 2560×1440 の実全画面撮影 + 目視 | **本 Issue の受入そのもの**。オーナー実施 |

**「プロファイル座標の EditMode 検証は実描画の見切れ・重なりを保証しない」**
(`docs/audit/open-issues-audit.md` §3.2 #96「未検証の区別」) を厳守する。
**スクリーンショット 1 枚は正常と誤読しうる** (同 §0 の表)。
