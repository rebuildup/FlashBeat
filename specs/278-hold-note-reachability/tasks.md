---
description: "FlashBeat lean tasks — Issue 278"
---

# Tasks: HOLD ノーツの実機到達 (Issue #278)

**Issue**: #278 | **Branch**: `w/spec-gameui` | **Spec**: [`spec.md`](spec.md) / [`plan.md`](plan.md)

> **対策 (A) (テスト用 HOLD 譜面 + macOS 実機ハーネス) は別 Worker が実装中**。
> 本 Worker は **A を重複実装しない**。完了を待ち、結果の反映だけを行う。
> **対策 (B) (実曲へ HOLD 追加) は谱面内容の方針決定が要る = オーナー判断**。

## Tasks

- [x] **T1** 全譜面の `notes[].type` を実測し、HOLD (type=2) が 0 個であることの確認。
  — **done when**: 全 27 曲の実測値 (曲ごとのノーツ数と HOLD の個数) が記録され、
  `type=2` が 0 個であることが確認できること
  — **実測**: `Assets/Game/Resources/*.json` のうち `notes[]` を持つ 28 ファイル
  (27 曲 + `E2EProbeSong`) を走査。**全件で `type=2` の個数は 0**。
  曲ごとのノーツ数は Issue 本文の実測と一致
  (4th smile 105 / Calamity 111 / Chartreuse 343 / Dogbite 603 / GURU 295 / Poison 82 /
  Psyched 256 / Who 139 / まにまに 76 / イガク 196 / エウタナシア 243 / オーバーライド 250 /
  セルフィー 254 / チュートリアル 53 / ツイッターランド 130 /
  テレキャスタービーボーイ 163 / パノプティコン 319 / ビビビビ 184 / ロストアンブレラ 268 /
  一龠 191 / 春嵐 315 / 混沌ブギ 292 / 灰Φ倶楽部 275 / 狂喜蘭舞 360 / 終焉逃避行 296 /
  阿吽のビーツ 262 / ニセモノ 0)。`E2EProbeSong` も 0 notes

- [x] **T2** HOLD の実装・仕様・EditMode テストが main に揃っていることの確認。
  — **done when**: 実装・仕様・テストが file:line 付きで記録されていること
  — **実測**: `Assets/Game/Scripts/Gameplay/LongNote.cs:26` (`TYPE_TAP = 1`)、`:29` (`TYPE_HOLD = 2`)、
  `:71` (`IsHold`)。`Assets/Game/Scripts/Gameplay/JudgeQueue.cs:165` (`BeginHold`)、
  `:183` (`UpdateHold`)、`Assets/Game/Scripts/Gameplay/Judge.cs:178` (`DowngradeHoldToBad`)、
  `:230` (`queue.BeginHold(hit)`)。仕様は `specs/164-long-note/spec.md` に凍結済み (PR #265 merged /
  Issue #164 CLOSE)。テスト `Assets/Tests/Editor/LongNoteTests.cs` (23 件) +
  `Assets/Tests/Editor/JudgeQueueTests.cs` の HOLD 2 件
  (`Hold_ExpiryBoundary_MatchesLongNoteRule` / `Hold_RuntimeResult_MatchesLongNoteRules`)

- [ ] **T3** 対策 (A) (テスト用 HOLD 譜面 + macOS 実機ハーネス) の別 Worker の完了を待ち、
  使われた差し替えシームと観測点を記録する。
  — **done when**: 対策 (A) の PR が merged になり、差し替えシーム
  (`Assets/Game/Scripts/Songs/PlaySessionSnapshot.cs:25` / `:64` `Begin`) と
  観測点 (開始・保持・終端) が本 Issue の受入記録に反映されていること
  — **別 Worker が実装中** (`docs/audit/open-issues-audit.md` §4 U12:
  「対策 (A) テスト用 HOLD 譜面 + macOS 実機ハーネスを Worker が実装中
  (譜面は `PlaySessionSnapshot` の差し替えシームを使い、カタログは増やさない)」)。
  **本 Worker は A を重複実装しない**
  — カタログ (GManager の 7 並列配列 / `GManager.totalSong`) は増やさないこと

- [ ] **T4** 対策 (B) (実曲へ HOLD 追加) の採否を **オーナーに確認**する。
  — **done when**: 「A で足りるか / B も必要か」の判断が記録として残っていること
  — **オーナー判断事項** (Issue 本文: 「体験の设计上も意味があるが、谱面内容の方針決定が要る」)。
  **cc は実曲へ HOLD を追加しない**。指示があった場合のみ、
  `GManager` の 7 並列配列を増やさずに **譜面 JSON のみ** を編集する
  — 編集時は `LongNoteTests` の「既存 tap 譜面の最終 note 時刻が変わらない」を必ず確認する

- [ ] **T5** macOS 実機での HOLD の 3 段階 (開始・保持・終端) の観測。
  判定数・コンボが **Tap のみの場合と違う** ことを確認する。
  — **done when**: 実機ログとスクリーンショットが
  `docs/evidence/278-hold-note-reachability/` に残り、
  HOLD の判定数が Tap のみの場合と異なる (HOLD が 1 判定として数えられる) ことが確認できること
  — **macOS 実機 (実ディスプレイ全画面) で実行可能**。
  **HOLD はタップと同点なので、スコア差ではなく判定数とコンボで区別する**
  — 対象は対策 (A) の譜面 (T3 の完了が前提)

- [ ] **T6** HOLD の中断 (途中で離す → BAD、押し直し不可) の実機観測。
  — **done when**: 中断が BAD になり、押し直しで救済されないことの記録があること
  — **macOS 実機 (実ディスプレイ全画面) で実行可能**
  — 判定仕様は `specs/164-long-note/spec.md` に凍結済み (**再設計しない**)

- [ ] **T7** 既存曲の実測回帰: 追加後も既存 Tap 譜面の結果が変わっていないことの確認。
  — **done when**: 「ツイッターランド」130/130 確定 / `Completed` /
  `forcedByDeadline=false` / `ResultScene` 到達が保たれていることの記録があること
  — **macOS 実機 (実ディスプレイ全画面) で実行可能**。
  `script/mac-songend-harness.sh` を使用 (基準値:
  `docs/evidence/233-song-end-harness.md` / `docs/evidence/index.md` §7.1)

- [ ] **T8** Windows 実機での HOLD 演奏 (Issue 本文の受入条件 3)。
  — **done when**: 実画像と Player.log が `docs/evidence/278-hold-note-reachability/` に残ること
  — **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**。
  #59 の残りとも重なる項目

- [ ] **T9** 判定表 (受入条件 1–3 × OK / NG / 未検証) と、
  対策 (A) / (B) の採否判断を PR で報告する。
  — **done when**: 判定が OK / NG / 未検証 のいずれかで記録され、
  **未検証 (Windows 実機) が「合格」に書き換えられていない** こと
  — **Issue コメントの投稿は cc が行わない** (PR を作成して cc に報告する)
