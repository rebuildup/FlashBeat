---
description: "FlashBeat lean spec — Issue 059"
---

# Spec: #30 各機能 (歌詞同期 / キーガイド / HOLD / HUD) の受入確認

**Issue**: #59 | **Branch**: `w/spec-gameui` | **Date**: 2026-11-10 | **Status**: Draft

## Input

`検証: Issue #30 の各機能 (歌詞同期 / キーガイド / HOLD / HUD) の受入確認`

## Objective

#59 は **実装ではなく受入確認** の Issue である。4 機能 (歌詞同期 / キーガイド / HOLD / HUD/UX) の
実装と EditMode テストは **すでに main に揃っている**。受入記録
`docs/evidence/59-feature-acceptance.md` (PR #266 merged) も作成済みで、**4 機能とも
「充足 (コード・テスト)」判定** 済みである。

残る受入条件は **Windows 実機での目視確認** のみ (HOLD の実機演奏は #278 に切り出されている)。
本 spec は **残りの受入条件をどの手順・どの観測点で満たすか** を定め、
既に main にある受入記録 (`docs/evidence/59-feature-acceptance.md`) と
仕様・決定記録 (`docs/ui/issue-30-spec-decisions.md` / `specs/164-long-note/spec.md`) を
どの受入条件へ紐付けるかの索引になる。

**参照ファイル**: `docs/evidence/59-feature-acceptance.md` / `docs/ui/lyrics-sync.md` / `docs/ui/issue-30-spec-decisions.md` / `docs/evidence/233-hud-nre/` / `docs/evidence/233-song-end-harness.md` / `specs/164-long-note/spec.md`

## Decisions (凍結する設計)

| 項目 | 決定 |
| --- | --- |
| 判定の 3 値 | **充足 (コード・テスト)** / **充足 (実機)** / **未検証** の 3 値で固定する。**「EditMode 緑」を「実機合格」に読み替えない** (`docs/evidence/59-feature-acceptance.md` の冒頭注記に従う) |
| HOLD の扱い | **HOLD の実機演奏は本 Issue の受入対象から外す** (=#278 へ切り出し済み)。本 Issue では HOLD の行を「充足 (コード・テスト) / 実機演奏は #278 で受入」と 2 値表記する |
| Issue 本文のチェックボックス | Issue 本文 189–192 行目の 4 つのチェックボックスは **実装済みでも自動でチェックしない**。チェックは受入判定が確定してから cc 以外の者が行う (cc は Issue へのコメント投稿を行わない) |
| スクリーンショットの置き場所 | Issue 本文は `docs/evidence/<feature>-<date>/` を要求するが、**既に `docs/evidence/59-feature-acceptance.md` に集約されている**。機能別ディレクトリを新たに作らずに **既存記録を Windows 実機の観察で更新する** 形に統一する |
| Windows 実機 | 展示 PC のため **cc は実行しない。オーナー実施**。判定は「未検証」のまま固定する |
| macOS の代替 | macOS 実機 (実ディスプレイ全画面 2560×1664) での観測は **「その環境で何が映ったか」の記録** であり、Win64 の代替ではない (`docs/evidence/index.md` §1.6 / §1.8 の扱い) |
| NG の扱い | NG が見つかった場合は **別 Issue として起票** し、修正は実装ワーカーへ差し戻す。**本 Issue では修正しない** (Issue 本文の制約) |
| `#30` の未確定事項 | #30 の HOLD 判定仕様は `specs/164-long-note/spec.md` で凍結済み (PR #265) だが、**D5 (`scoringVersion`) は未決** (`docs/ui/issue-30-spec-decisions.md` §3)。この未決は **#59 の 4 機能の判定を止めない** (HOLD はタップと同点) |

## 非目標 (Out of scope)

- 4 機能の実装変更 (`LyricsTimeline` / `KeyGuideUI` / `LongNote` / `GameHudView` / `V6GameHudView`)。
- HOLD 譜面の追加・HOLD の実機演奏 (=#278 のスコープ)。
- ランキング送信の実機確認 (=#134 / `#21` のスコープ)。
- Windows 実機の操作 (展示 PC は cc が自発的に触らない)。
- `docs/audit/open-issues-audit.md` / `docs/evidence/index.md` の編集 (他 Worker 所有)。

## Acceptance criteria

1. 歌詞同期について、歌詞が音 (動画時刻) に同期して表示・非表示できることの受入判定が「充足 (コード・テスト)」または「充足 (実機)」のいずれかで記録され、根拠 (テスト名・件数・証跡ファイル) が示されている。
2. キーガイドについて、8 レーンのキー配置が `Judge.LaneKeys` と一致して画面に出ることの受入判定が記録され、根拠が示されている。
3. HOLD について、保持・判定・表示の受入判定が記録され、**実機演奏が #278 に切り出されている** 旨が明記されている。
4. HUD/UX について、判定・コンボ・プログレスの導線が崩れていないことの受入判定が記録され、根拠が示されている。
5. 各機能の判定は OK / NG / 未検証 の 3 値を持ち、**未検証は未検証のまま** 記されている (NG を OK に読み替えない)。
6. NG が見つかった場合、それは **別 Issue として起票** され、番号が記録に載っている (無ければ「無し」と明記)。
7. Windows 実機での 4 機能の目視確認が未実施である場合、判定は **「未検証 (Windows 実機・オーナー実施)」** と明記され、実施手順が `docs/operations/win64-candidate-acceptance-2026-11-10.md` 経由で参照できる。
8. Issue 本文の 4 チェックボックスが、受入判定が確定した範囲でのみチェックされている (未検証の機能を勝手にチェックしない)。

## 現在の実装状況 (2026-11-10 時点)

**充足済み**

| 受入条件 | 判定 | 根拠 (file:line / 証跡) |
|---|---|---|
| 歌詞同期の実装 | 充足 (コード・テスト) | `Assets/Game/Scripts/UI/LyricsTimeline.cs`、テスト `Assets/Tests/Editor/LyricsTimelineTests.cs` (18 件)、`ChartLyricsTimingTests.cs` (1 件)、`EditDataSerializerLyricsTests.cs` (6 件)。`Assets/Game/Scripts/UI/V6/Game/Hud/V6GameHudLogic.cs` の `LyricsTimeline_Build` が HUD へ渡す |
| キーガイドの実装 | 充足 (コード・テスト) | `Assets/Game/Scripts/UI/KeyGuideUI.cs` (8 レーンのラベルとキーを `KeyBindings` の単一正本から生成)、テスト `Assets/Tests/Editor/KeyGuideTests.cs` (11 件)、`KeyBindingsTrunkTests.cs` (3 件) |
| HOLD の実装・仕様凍結 | 充足 (コード・テスト) | `Assets/Game/Scripts/Gameplay/LongNote.cs:26` (`TYPE_TAP = 1`), `:29` (`TYPE_HOLD = 2`)、`Assets/Game/Scripts/Gameplay/JudgeQueue.cs:165` (`BeginHold`), `:183` (`UpdateHold`)、`Assets/Game/Scripts/Gameplay/Judge.cs:178` (`DowngradeHoldToBad`)。仕様は `specs/164-long-note/spec.md` に凍結済み (PR #265) |
| HUD の実装 | 充足 (コード・テスト) | `Assets/Game/Scripts/UI/V5/GameHudView.cs`、テスト `Assets/Tests/Editor/V5GameHudTests.cs`、`V6GameHudViewTests.cs`、`Assets/Game/Tests/V6GameHudLogicTests.cs`、`Assets/Game/Tests/V6CanvasTests/V6GameHudContractTests.cs`、`Assets/Tests/Editor/HudPlayStateTests.cs` (9 件) |
| 受入記録そのもの | 充足 | `docs/evidence/59-feature-acceptance.md` (PR #266 merged)。§1 に 4 機能 × (コード・テスト判定 / 実機判定 / 証跡) の表がある |
| EditMode テスト結果 | 充足 | 同記録 §1: **1181 / 1181 成功 / 失敗 0** (macOS / Unity 6000.5.6f1) |
| macOS 実機での画面確認 | 充足 (macOS のみ) | 同記録 §2.1–2.4: 2560×1664 実ディスプレイ全画面の実画像で歌詞「ああ」/ ローマ字行「ala」/ 下部のキー列 (A S D F J K L) / ランク C・SCORE・COMBO・判定ゲージ・Miss 表示を確認。証跡 `docs/evidence/233-hud-nre/` |
| 不具合の起票記録 | 充足 | 同記録 §3: 譜面/設定 JSON の Resources 混在バグを #241 として起票 → PR #264 で修正済み |

**未充足**

| 受入条件 | 判定 | 未充足の理由 |
|---|---|---|
| 4 機能の Windows 実機での目視確認 | **未充足 (Windows 実機必須)** | `docs/evidence/59-feature-acceptance.md` §4 が「Close する条件|Windows 実機 (展示 PC) での 4 機能の確認が完了し、そのスクリーンショットと Player.log が `docs/evidence/` に残ること」と明記。**cc は展示 PC を自発的に操作しない** |
| HOLD の実機演奏 | **未充足 (=#278 に切り出し済み)** | 全 27 曲に `type=2` が 1 つも無いため **実行不能**。`docs/evidence/index.md` §7.3-4 も同旨 |
| Issue 本文の 4 チェックボックス | **未充足** | Issue 本文 189–192 行目のチェックボックスが未チェック (チェックは受入判定確定後、cc 以外の者が行う) |
| 機能別スクリーンショットディレクトリ | **再定義済 (充足扱い)** | Issue 本文は `docs/evidence/<feature>-<date>/` を要求するが、実体は `docs/evidence/59-feature-acceptance.md` + `docs/evidence/233-hud-nre/` + `docs/evidence/233-song-end-harness.md` に集約されている ( Decisions で再定義 ) |
| ストリーム音声 (m4a) の再生 | **未充足 (#13 / #4 のスコープ)** | `docs/evidence/index.md` §7.3-2 が「未証明」と明記 |
