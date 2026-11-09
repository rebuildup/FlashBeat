---
description: "FlashBeat lean spec — Issue 219"
---

# Spec: 8 レーン遠近法プレイフィールド・ノーツ・判定ラインを移植

**Issue**: #219 | **Branch**: `w/spec-gameui` | **Date**: 2026-11-10 | **Status**: Draft

## Input

`[P0/v6 Game] 8レーン遠近法プレイフィールド・ノーツ・判定ラインを移植`

## Objective

pencil-ui の frozen デザイン (`19f08b31` / node `nrfw8` 07 Gameプレイフィールド) に定義された
**8 レーンの遠近法プレイフィールド・遠近法ノーツ・判定ライン (y=792, 高さ 4) / Judge Glow (y=796)**
を、**実のプレイデータに投影する描画**として Unity に持ち込む。

**描画コードと EditMode テストは main に既にある** (`V6PlayfieldView` / `V6PlayfieldRuntime` /
`V6LaneGeometry` / `V6PlayfieldGeometryTests`)。残るのは:

- (A) **`V6UiMode.Playfield = false`** — 描画が丸ごと無効化されている (owner rollback 2026-11-08)。
  再承認は **オーナー判断**。
- (B) **`GameScene.unity` の `Lane` が `m_IsActive: 0`** — フォールバック先の 3D レーンが無く、
  どちらの描画も出ない。**シーン (.unity) の改変は統合 owner との排他的合意が必要**。
- (C) **実機での 8 レーン / ノーツ / 判定同期 / 動画背景の観測** (Windows 実機必須)。

本 spec は (A)〜(C) を、そして **「誰が統合 owner で、排他的合意の要件が何か」を明記する** ことを主眼とする。

## Decisions (凍結する設計)

| 項目 | 決定 |
| --- | --- |
| **排他的合意が前提** | Issue 本文の Owned Paths に明記がある通り、**既存 `NotesManager` / `Notes` / `Judge` / `MediaTimeline` / シーン (.unity) の改変は「統合 owner と排他的な合意」の後のみ**。**合意なしには `Assets/Game/Scenes/GameScene.unity` と `NotesManager` / `Notes` / `Judge` / `MediaTimeline` を改変してはならない**。本 spec のタスクはこの前提を守る |
| 描画の実装場所 | 新規 `Assets/Game/Scripts/UI/V6/GamePlayfieldV6.cs` および `Assets/Game/Scripts/UI/V6/Game/Playfield/` 配下の専用描画子・テスト。**HUD / Select 関連ファイルは変更しない** (Issue 本文) |
| 静的な貼り絵の禁止 | 8 レーンとノーツは **実プレイデータ (Time / NoteIndex / Judge) の投影** でなければならない。**静止の譜面を背景に貼って誤魔化さない** (Issue 本文) |
| 判定の実装 | **既存判定・HOLD は再実装しない**。`Judge` / `JudgeQueue` / `LongNote` の契約は読み取りのみ |
| 入力キー | **入力キーの意味を勝手に変更しない** (`Judge.LaneKeys` → `KeyBindings.DefaultKeys` の単一正本) |
| 有効化フラグ | `Assets/Game/Scripts/UI/V6/V6UiMode.cs:27` の `public static bool Playfield = false;` を `true` にするのは **owner rollback の再承認 (= オーナー判断) が前提**。**cc は勝手に変更しない** |
| シーンの `Lane` | `Assets/Game/Scenes/GameScene.unity:2832` `m_Name: Lane` / `:2837` `m_IsActive: 0`。
  **シーン (.unity) の改変は統合 owner との排他的合意が必要**。コード側から active にする enabler を
  追加する案もあるが、**これも `NotesManager` 側の改変を伴うため同じ合意が必要** |
| legacy 排除 | `Assets/Game/Scripts/UI/V6/V6GameRuntime.cs:370-374` の `HideLegacyPlayfield()` は
  `V6UiMode.Playfield` が true のときだけ `HideByName("Lane")` / `"JudgeLine"` / `"LaneLights"` を隠す。
  **Playfield が false のままだと旧 v5 レーンも隠れない** = (A) と (B) は同時に必要 |
| フォールバック | `Assets/Game/Scripts/Gameplay/Notes.cs:25,111` は `V6PlayfieldView.Current == null` のとき
  3D 描画へフォールバックする設計。**fallback 先の `Lane` が非 active なので、どちらの描画も出ない** |
| 未マージ PR との衝突 | 未マージ PR #204 との衝突に注意 (Issue 本文)。`origin/134-patch` の `615db66` / `5067329` は
  `git log origin/main..origin/134-patch` が空で内容は main にある (=#134 の移植とは別件) |
| Unity Editor の単独所有 | **Unity Editor / シーンの単独所有は統合担当** (Issue 本文)。cc はシーンを編集しない |
| 証跡 | `docs/evidence/219-v6-playfield/` に集める (新規)。`docs/evidence/index.md` /
  `docs/audit/open-issues-audit.md` は **他 Worker 所有** なので触らない |

## 非目標 (Out of scope)

- `NotesManager` / `Notes` / `Judge` / `JudgeQueue` / `LongNote` / `MediaTimeline` の
  **改変** (排他的合意なしに行えない)。
- `Assets/Game/Scenes/GameScene.unity` の **改変** (同上)。
- HUD / Select 関連ファイルの変更 (Issue 本文)。
- 判定・HOLD の再実装。
- 入力キー表の変更。
- Windows 実機での操作 (展示 PC は cc が自発的に触らない)。

## Acceptance criteria

1. `V6UiMode.Playfield` の有効化が **owner rollback の再承認 (= オーナー判断) として記録されている** (未承認のまま変更しない)。
2. 統合 owner との **排他的合意の記録** が存在し、合意の対象ファイル
   (`GameScene.unity` / `NotesManager` / `Notes` / `Judge` / `MediaTimeline`) と
   変更内容が明示されていること。**合意なしの変更は 0**。
3. 8 レーンの遠近法形状・判定ライン (y=792 / h=4) / Judge Glow (y=796) が
   デザイン frozen (`19f08b31`) と一致して描画されること。
4. ノーツが **実プレイデータに投影** されている (静止の貼り絵でない) こと。
   通常ノーツ / HOLD の体 / 同時押し / Miss / レーン別判定エフェクトが
   現行の Time / NoteIndex / Judge と一致すること。
5. 動画がプレイフィールドの **背景として** 再生されること (前後関係 / レイヤー順序が正しいこと)。
6. レーン描画 / 時間進行 / リサイズの EditMode ロジックテストが緑であること
   (`Assets/Game/Tests/V6PlayfieldGeometryTests.cs`)。
7. 実機 (FHD / 2K / Mac) でフレーム切れ・レーンのズレ・上下反転がないことを確認できること。
8. 統合 Worker が GameScene へ一度で導入できるよう、**公開 API / 接続手順 / 所有オブジェクト /
   必要な Unity アセット** が PR に明記されていること。
9. 既存プレイ完走と音 / 動画同期を退化させていないこと。
10. 完了していなくても **独立 PR で進捗を残せる** 状態であること (Issue 本文)。

## 現在の実装状況 (2026-11-10 時点)

**充足済み (コード・テスト)**

| 受入条件 | 判定 | 根拠 (file:line) |
|---|---|---|
| 1. 既存 GameScene / NotesManager / Judge / 動画背景 / レーン描画の追跡、判定・HOLD は再実装しない | 充足 | `Assets/Game/Scripts/UI/V6/Game/Playfield/V6LaneGeometry.cs:48` (`LaneCount = 8`) が `Judge.LaneKeys` と同一定義を参照 |
| 2. v6 の 8 レーン遠近形状・判定ライン・ノーツを実プレイデータに投影する描画 | 充足 (コード) | `Assets/Game/Scripts/UI/V6/Game/Playfield/V6PlayfieldView.cs:38,118-124` が `NoteLayer` (uGUI "Notes" RectTransform) を提供し、`Assets/Game/Scripts/Gameplay/Notes.cs:110-129,158-165` が head/body の Image を `NoteLayer` に生成 (`V6LaneGeometry.NoteColor` / `LaneCount` を使う)。`Notes.cs:181` がフレームごとに `V6PlayfieldView.Current` を参照 |
| 3. 通常/ロング/同時押し/Miss/レーン別判定エフェクトが Time / NoteIndex / Judge と一致 | 充足 (コード) | `Notes.cs:129` (HOLD の body Image)、`Assets/Game/Scripts/UI/V6/Game/Playfield/V6PlayfieldView.cs:155-165` (レーンフラッシュは `KeyBindings.IsLaneKeyDown(lane)` 駆動) |
| 4. レーン描画 / 時間進行 / リサイズの EditMode ロジックテスト | 充足 (EditMode) | `Assets/Game/Tests/V6PlayfieldGeometryTests.cs` |
| 5. 統合 Worker 向けの公開 API / 接続手順 | 充足 | `Assets/Game/Scripts/UI/V6/Game/Playfield/V6PlayfieldRuntime.cs` (Canvas 生成、`Ensure()`)、`V6PlayfieldView` (`NoteLayer` / レーンフラッシュ) |
| 判定ライン / Glow の数値 | 充足 | `V6LaneGeometry.cs:45` (`JudgeLineY = 792f`)、`:66` (`JudgeLineHeightDesignPx = 4f`)、`:72` (`JudgeGlowY = JudgeLineY + JudgeLineHeightDesignPx` = 796)、`:84` (`ExitT01`) |

**未充足**

| 受入条件 | 判定 | 未充足の理由 |
|---|---|---|
| 1. `V6UiMode.Playfield` の有効化 | **未充足 (オーナー判断待ち)** | `Assets/Game/Scripts/UI/V6/V6UiMode.cs:27` が `public static bool Playfield = false;` (owner rollback 2026-11-08)。`Assets/Game/Scripts/UI/V6/Game/Playfield/V6PlayfieldRuntime.cs:52` の `if (!FlashBeat.UI.V6.V6UiMode.Playfield) return;` により **Canvas 自体が作られない** |
| 6. 実機での 8 レーン / ノーツ / 判定同期 / 動画背景の観測 | **未充足 (Windows 実機必須)** | フラグで無効化されているので描画自体が出ない。**EditMode 緑は描画上の問題を検出しない** |
| 実機でのフレーム切れ / レーンのズレ / 上下反転の画像比較 | **未充足 (Windows 実機必須)** | FHD / 2K / Mac の実画像比較の記録が無い。**Windows 実機は展示 PC のため cc は実行しない。オーナー実施** |
| 統合 owner との排他的合意の記録 | **未充足 (合意未取得)** | Issue 本文の Owned Paths が「**統合 owner と排他的な合意後のみ**」を要求しているが、合意の記録が無い |
| `GameScene.unity` の `Lane` の active 化 | **未充足 (合意が前提)** | `Assets/Game/Scenes/GameScene.unity:2832` `m_Name: Lane` / `:2837` `m_IsActive: 0`。enabler をコードから足す案も `NotesManager` 側の改変を伴う |
| legacy レーンの排除 | **未充足 (1 と同時)** | `Assets/Game/Scripts/UI/V6/V6GameRuntime.cs:370-374` `HideLegacyPlayfield()` は `Playfield` が true のときだけ `HideByName("Lane")` を呼ぶ。**false のままだと旧 v5 レーンも隠れない** |
