---
description: "FlashBeat lean implementation plan — Issue 162"
---

# Plan: v0.1.13 の UI 受け入れ撤回 — 実機受入ゲート — Issue #162

**Issue**: #162 | **Branch**: `w/spec-gameui` | **Spec**: [`spec.md`](spec.md) | **Date**: 2026-11-10

## Architecture

```
[ 既に main にあるもの (変更しない) ]

ProjectSettings/ProjectSettings.asset
  fullscreenMode: 1 (FullScreenWindow) / resizableWindow: 0 / defaultIsNativeResolution: 1
  └─ Assets/Game/Tests/FullscreenPolicyTests.cs (11 テスト)
       FHD 1920x1080 / 2K 2560x1440 / Mac 2560x1664 のプロファイル一致まで固定

docs/ui/issue-162-ui-contract.md      6 シーン × 入力/ハンドラ/効果/根拠 の静的契約表
  F1–F18 / A-1..A-10 / B-1..B-11 / C-1..C-9 の defect 記録
docs/ui/pr204-playtest-checkpoints.md 実プレイの受入チェックポイント (手順のみ)
docs/ui/issue-167-handler-audit.md    F1–F7 の修正記録

specs/167-v5-ui-functional-wiring  … 子の実操作配線 (spec 済み・別所有)
specs/169-fullscreen-canvas-foundation … 子の全画面基盤 (spec 済み・別所有)

[ 親 Issue の残り = 本 spec の仕事 ]

  ① 残 defect の決着マップ
       契約表の A/B/C 各行 → 修正済み (file:line) | 別 Issue (番号) | 残存 (理由)
  ② 実 Win64 受入 (3 解像度)
       配布 ZIP (SHA-256 照合) → 起動引数なしで起動
         → Win32 window bounds/style, effective Screen.width/height,
           Screen.fullScreenMode, monitor resolution/scale/DPI を採取
         → 画面ピクセル被覆を測定 (displayCoverage >= 0.99)
  ③ 機能 E2E
       Title → Select → Game → Result → Retry の一巡
       各画面: クリック/キー → 状態変化 (入力ログ付き)
       「押したのに動かない」を 1 件残さない
  ④ 1 曲完走 (Windows 実機)
  ⑤ v0.1.13 → 候補 ZIP の安全な更新 (適用 → 起動確認)
  ⑥ EditMode 緑 ( alone では合格にしない )
  ⑦ 判定表 (OK / NG / 未検証)

[ 既に macOS で実証済み (Windows の代替にはならない) ]
docs/evidence/233-song-end-harness.md : 1 曲完走 / Result 到達 / 実効 2560x1664 全画面
docs/evidence/index.md §7.2            : Win64 配布候補 0.1.22+0bab549 (SHA-256 付き)
```

## 実装順序

1. **残 defect の決着マップを作る (実装なし)**
   - `docs/ui/issue-162-ui-contract.md` の A-1〜A-10 / B-1〜B-11 / C-1〜C-9 を 1 行ずつ読む。
   - 各行に「main で既に修正済み (file:line)」「#167 の残タスク」「別 Issue」「残存 (理由)」のラベルを付ける。
   - **コードを変更しない** (子 #167 / #169 の所有を尊重する)。
2. **Windows 実機の受入手順を固める (オーナー実施)**
   - `docs/evidence/index.md` §7.2 の配布 ZIP (`0.1.22+0bab549-win64-0bab549.zip`、SHA-256 `a23c8c4af6983ffc6b19b1c69329fd2ebab3137f9d77e3a01598ca9c831b1c5d`) を使う。
   - `docs/operations/win64-candidate-acceptance-2026-11-10.md` の手順 (SHA 照合 → 展開 → 3 解像度の実全画面 → 1 曲完走 7 項目) に沿う。
   - **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**。
3. **3 解像度の実全画面**
   - FHD 1920×1080 / 2K 2560×1440 / Mac 2560×1664 の 3 条件のみ (Issue 本文)。
   - 窓リサイズによる擬似全画面は **証拠にならない**。
   - 各解像度で effective `Screen.width/height` と `Screen.fullScreenMode` を採取する。
4. **機能 E2E**
   - Title → Select → Game → Result → Retry を 1 巡。
   - 各画面の見えている control について「クリック/キー → ハンドラ → 状態変化 → フィードバック」を 1 つずつ確認し、**入力ログを残す**。
   - **スクリーンショットだけでボタンが動くと判定しない** (Issue 本文)。
5. **1 曲完走 + 更新適用**
   - 実 Win64 で 1 曲完走 → Result 到達。
   - v0.1.13 からの更新 (更新通知 F3 → 適用 → 再起動 → 起動確認) を実施。
6. **判定表**
   - 8 つの受入条件を OK / NG / 未検証 で記録。**「CI 緑」「Release 公開済み」を合格の根拠にしない**。
7. **報告**
   - PR を作成し、cc が報告する。**Issue コメントの投稿は cc が行わない**。

## ブロッカー

| ブロッカー | 内容 | 対処 |
|---|---|---|
| **Windows 実機 (展示 PC)** | 全画面の成立・ピクセル被覆・E2E・1 曲完走・更新適用はすべて Windows 実機必須。**Windows 実機は展示 PC のため cc は実行しない。オーナー実施** | 手順書と候補 SHA を渡し、判定を「未検証」で固定 |
| **擬似全画面は不可** | macOS 側で証明できるのは実ディスプレイの 2560×1664 だけ。FHD / 2K は窓リサイズでは証明にならない | **macOS 実機 (実ディスプレイ全画面) で実行可能なのは 2560×1664 の観測のみ** |
| **Issue 本文の期限が経過** | 「2026-11-08 朝・展示開始前まで」は既に過ぎている | **cc は期限を独断で延長しない**。オーナーの判断事項 |
| **子 #167 / #169 の所有** | `specs/167-v5-ui-functional-wiring` / `specs/169-fullscreen-canvas-foundation` は別 Worker の所有 | **書き直さない・上書きしない**。本 spec は親ゲートのみ |
| **残り defect の修正** | 契約表の残 defect の修正は実装作業 | **本 Issue では修正しない**。#167 の残タスクへ回す |
| **`docs/evidence/index.md` の編集** | 他 Worker 所有 | 触らない |

## テスト戦略

| 種別 | 内容 | 備考 |
|---|---|---|
| EditMode (既存のみ) | `Assets/Game/Tests/FullscreenPolicyTests.cs` (11 テスト)、`Assets/Tests/Editor/WindowBaselineTests.cs`、`V5UiTests.cs`、`V5GameHudTests.cs`、`TitleOptionV5Tests.cs`、`V5UiWiringTests.cs` (8 テスト)、`GameUiExclusivityTests.cs` | **緑だけでは UI 合格ではない** (Issue 本文) |
| 実機 (Windows) | 3 解像度の実全画面 / ピクセル被覆 / 機能 E2E / 1 曲完走 / 更新適用 | **オーナー実施**。cc は実行しない |
| 実機 (macOS) | 実ディスプレイ 2560×1664 全画面での 1 曲完走 (済) と画面目視 | **Win64 の代替にならない** (`docs/evidence/index.md` §1.6 / §1.8) |
| 静的監査 | `docs/ui/issue-162-ui-contract.md` の defect マップ | コード読解であり実プレイ証拠ではない (同ファイル 5 行目) |
