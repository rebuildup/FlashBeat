---
description: "FlashBeat lean implementation plan — Issue 059"
---

# Plan: #30 各機能の受入確認 — Issue #59

**Issue**: #59 | **Branch**: `w/spec-gameui` | **Spec**: [`spec.md`](spec.md) | **Date**: 2026-11-10

## Architecture

```
[ 実装 (main 済み・変更しない) ]                [ 受入確認の残作業 ]

歌詞同期   LyricsTimeline.cs                        ① macOS 実機での 4 機能画面確認 (継続)
           + LyricsTimelineTests.cs (18)            実ディスプレイ 2560x1664 全画面
           + ChartLyricsTimingTests.cs (1)          → 既に 233-hud-nre/ に画像あり
           + EditDataSerializerLyricsTests.cs (6)   追加観測: 歌詞の開始/終了境界
                                                  ② Windows 実機での 4 機能目視
キーガイド  KeyGuideUI.cs (KeyBindings 単一正本)     展示 PC → cc は実行しない
           + KeyGuideTests.cs (11)                  判定は「未検証 (オーナー実施)」
           + KeyBindingsTrunkTests.cs (3)          ③ HOLD の実機演奏 (= #278 へ)
HOLD      LongNote.cs:26 TYPE_TAP / :29 TYPE_HOLD   #278 の Worker が実装中
           + JudgeQueue.cs:165 BeginHold           macOS 実機ハーネスで 3 段階観測
           + JudgeQueue.cs:183 UpdateHold          ④ NG があれば別 Issue として起票
           + Judge.cs:178 DowngradeHoldToBad
           + specs/164-long-note/spec.md (凍結)
HUD       V5/GameHudView.cs + V6/Game/Hud/V6GameHudView.cs
           + V5GameHudTests / V6GameHudViewTests
           + V6GameHudLogicTests / V6CanvasTests
           + HudPlayStateTests.cs (9)
                                                  ⑤ 判定表を既存記録に反映

[ 既に main にある受入記録 ]
docs/evidence/59-feature-acceptance.md … §1 判定表 / §2 機能別 / §3 起票 / §4 総合判定
docs/evidence/233-hud-nre/*.png          … macOS 実機の 4 機能実画像
docs/evidence/233-song-end-harness.md    … macOS 実機の 1 曲完走
```

## 実装順序

1. **既存記録の確認 (変更なし)**
   - `docs/evidence/59-feature-acceptance.md` を開き、4 機能の判定が「充足 (コード・テスト) / 未検証 (実機)」の 2 値で記録されていることを確認する。
   - HOLD 行が「実機演奏は #278 で受入」と切り出されていることを確認する。
2. **macOS 実機での追加観測 (可能なものだけ)**
   - macOS ビルド (`script/mac-verify build`) → 起動 → **実ディスプレイ 2560×1664 全画面**。
   - 歌詞同期: Verse 付近で歌詞行が切り替わること (開始/終了境界)。
   - キーガイド: 下部のキー列が `Judge.LaneKeys` と一致すること。
   - HUD: 判定・コンボ・プログレスの導線が崩れていないこと。
   - スクリーンショットを `docs/evidence/233-hud-nre/` に追記する。
   - **macOS の観測は Win64 の代替ではない** (`docs/evidence/index.md` §1.6 / §1.8)。
3. **Windows 実機 (オーナー実施)**
   - `docs/operations/win64-candidate-acceptance-2026-11-10.md` の手順 (SHA-256 照合 → 展開 → 実全画面) を渡す。
   - 4 機能それぞれのスクリーンショット + `Player.log` を `docs/evidence/` に残してもらう。
   - **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**。
4. **HOLD の実機演奏**
   - **本 Issue では実施しない**。#278 の Worker が対策 (A) (テスト用 HOLD 譜面 + macOS 実機ハーネス) を実装中。
   - #278 が完了したら、その結果を本 Issue の HOLD 行に反映する (本 Issue では起票だけする)。
5. **判定表の更新**
   - `docs/evidence/59-feature-acceptance.md` の §1 / §4 を更新する。**未検証は未検証のまま**。
   - Issue 本文の 4 チェックボックスは、受入判定が確定した範囲でのみ cc 以外の者がチェックする。

## ブロッカー

| ブロッカー | 内容 | 対処 |
|---|---|---|
| **Windows 実機 (展示 PC)** | 4 機能の目視確認は Windows 実機必須。**Windows 実機は展示 PC のため cc は実行しない。オーナー実施** | 手順書を渡し、判定を「未検証」で固定 |
| **HOLD の実機演奏** | 全 27 曲に `type=2` が 1 つも無い = 到達不能 | **#278 の Worker に依存**。本 Issue では起票のみ |
| **ストリーム音声 (m4a)** | `#13` / `#4` の Windows Media Foundation 制約 | 本 Issue のスコープ外。「未証明」のまま |
| **`#30` の D5 (`scoringVersion`) 未決** | `docs/ui/issue-30-spec-decisions.md` §3 | **#59 の 4 機能を止めない** (HOLD はタップと同点)。オーナー決定事項 |
| **`docs/evidence/index.md` の編集** | 他 Worker 所有 | 触らない |

## テスト戦略

| 種別 | 内容 | 備考 |
|---|---|---|
| EditMode (既存のみ) | `LyricsTimelineTests` / `ChartLyricsTimingTests` / `EditDataSerializerLyricsTests` / `KeyGuideTests` / `KeyBindingsTrunkTests` / `LongNoteTests` / `JudgeQueueTests` (HOLD 2 件) / `V5GameHudTests` / `V6GameHudViewTests` / `V6GameHudLogicTests` / `V6CanvasTests/V6GameHudContractTests` / `HudPlayStateTests` | **新規テストは書かない** (本 Issue は検証 Issue) |
| EditMode 実行 | Unity batchmode (`-runTests -testPlatform editmode`、`-quit` を付けない) | 直近 1181/1181 が記録済み |
| 実機 (macOS) | 実ディスプレイ 2560×1664 全画面で 4 機能を画面確認 | **実ディスプレイ全画面**。窓リサイズによる擬似全画面は不可 |
| 実機 (Windows) | 未実施 | **オーナー実施**。判定は「未検証」 |
| HOLD | #278 の macOS 実機ハーネス (`script/mac-songend-harness.sh` 系) | 本 Issue のスコープ外 |
