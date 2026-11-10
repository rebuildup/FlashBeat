---
description: "FlashBeat lean tasks — Issue 059"
---

# Tasks: #30 各機能の受入確認 (Issue #59)

**Issue**: #59 | **Branch**: `w/spec-gameui` | **Spec**: [`spec.md`](spec.md) / [`plan.md`](plan.md)

## Tasks

- [x] **T1** 4 機能 (歌詞同期 / キーガイド / HOLD / HUD) の実装と EditMode テストが main に揃っていることの確認。
  — **done when**: 各機能の実装ファイルとテストファイル名・件数が file:line 付きで記録されていること
  — **実測**: 歌詞同期 = `Assets/Game/Scripts/UI/LyricsTimeline.cs` +
  `Assets/Tests/Editor/LyricsTimelineTests.cs` (18 件) + `ChartLyricsTimingTests.cs` (1) +
  `EditDataSerializerLyricsTests.cs` (6)。キーガイド = `Assets/Game/Scripts/UI/KeyGuideUI.cs` +
  `KeyGuideTests.cs` (11) + `KeyBindingsTrunkTests.cs` (3)。
  HOLD = `Assets/Game/Scripts/Gameplay/LongNote.cs:26,29` + `JudgeQueue.cs:165,183` +
  `Judge.cs:178` + `LongNoteTests.cs` (23) + `JudgeQueueTests.cs` (HOLD 2 件)、
  仕様は `specs/164-long-note/spec.md` に凍結済み (PR #265)。
  HUD = `Assets/Game/Scripts/UI/V5/GameHudView.cs` + `Assets/Game/Scripts/UI/V6/Game/Hud/V6GameHudView.cs` +
  `V5GameHudTests.cs` / `V6GameHudViewTests.cs` / `V6GameHudLogicTests.cs` /
  `V6CanvasTests/V6GameHudContractTests.cs` / `HudPlayStateTests.cs` (9 件)

- [x] **T2** 受入記録 `docs/evidence/59-feature-acceptance.md` が作成され、4 機能に
  OK / NG / 未検証 の判定と根拠 (テスト出力・スクリーンショット) があること。
  — **done when**: 記録に 4 機能 × (コード・テスト判定 / 実機判定 / 証跡) の表があること
  — **実測**: PR #266 で merged。§1 に判定表 (4 機能とも「充足 (コード・テスト)」/ 実機は「未検証 (Win64)」「macOS では表示を確認」)、
  §2.1–2.4 に機能別の実装・テスト件数・macOS 実機・判定・残存、§3 に起票記録 (#241)、
  §4 に総合判定 (未充足 0 件 / Close 条件は Windows 実機) がある。
  テスト結果欄は **1181 / 1181 成功 / 失敗 0**

- [x] **T3** HOLD の実機演奏が #278 に切り出されたことの確認。
  — **done when**: #278 が起票済みで、#59 の HOLD 行が「実機演奏は #278 で受入」になっていること
  — **実測**: `docs/evidence/59-feature-acceptance.md` §2.3 の「残存」が #278 相当の切り出しを明記。
  `docs/audit/open-issues-audit.md` サマリ表 (#59 行) も「HOLD の実機演奏は #278 に切り出し」と記載。
  #278 自体は 2026-11-10 起票 (対策 A: テスト用 HOLD 譜面 + macOS 実機ハーネスの Worker が実行中)

- [x] **T4** macOS 実機 (実ディスプレイ全画面) での 4 機能の画面確認が記録に残っていること。
  — **done when**: 実画像が `docs/evidence/` に残り、画像の実効解像度が 2560×1664 であること
  — **実測**: `docs/evidence/59-feature-acceptance.md` §2.1–2.4 に 2560×1664 実ディスプレイ全画面での
  観測記録 (歌詞「ああ」/ ローマ字行「ala」/ 下部キー列 A S D F J K L / ランク C・SCORE・COMBO・判定ゲージ・Miss 表示)。
  実画像は `docs/evidence/233-hud-nre/` (`before-fix-gameplay-2560x1664.png` / `after-fix-gameplay-2560x1664.png`)。
  **Windows 実機の代替ではない**ことを同記録の冒頭注記が明記

- [ ] **T5** Windows 実機での 4 機能の目視確認 (歌詞同期 / キーガイド / HOLD / HUD)。
  スクリーンショットと `Player.log` を `docs/evidence/` に残す。
  — **done when**: 4 機能それぞれの実画像が `docs/evidence/` にあり、判定が「充足 (実機)」に更新されていること
  — **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**。
  手順は `docs/operations/win64-candidate-acceptance-2026-11-10.md` (SHA-256 照合 → 展開 → 実全画面 → 1 曲完走)

- [ ] **T6** HOLD の実機演奏 (開始・保持・終端の 3 段階) の観測。
  — **done when**: HOLD の判定数・スコア・コンボが Tap のみの場合と違うことが実測で記録されていること
  — **=#278 のスコープ**。対策 (A) の macOS 実機ハーネスが別 Worker によって実装中。
  本 Issue では #278 の完了を待つだけで、**#278 完了後**に結果を本 Issue の判定表へ反映する。
  Windows 実機での HOLD 演奏は #278 の受入条件 3 として別途残す

- [ ] **T7** NG が見つかった場合の別 Issue 起票 (修正はしない)。
  — **done when**: 起票した Issue 番号が `docs/evidence/59-feature-acceptance.md` §3 に載っていること
  (起票が不要なら「今回検査で NG は無し」と明記)
  — 既に 1 件あり: 譜面/設定 JSON の Resources 混在バグ → #241 → PR #264 で修正済み

- [ ] **T8** Issue 本文の 4 チェックボックスを、受入判定が確定した範囲でのみチェックする。
  — **done when**: 未検証の機能がチェックされていないこと
  — **cc は Issue へのコメント投稿を行わない**。チェックは cc 以外の者が行う
