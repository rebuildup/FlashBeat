---
description: "FlashBeat lean implementation plan — Issue 219"
---

# Plan: 8 レーン遠近法プレイフィールドの移植 — Issue #219

**Issue**: #219 | **Branch**: `w/spec-gameui` | **Spec**: [`spec.md`](spec.md) | **Date**: 2026-11-10

## Architecture

```
[ 前提 — 排他的合意 ]
  統合 owner と排他的な合意が必要。
  合意なしに行えないもの:
    Assets/Game/Scenes/GameScene.unity
    Assets/Game/Scripts/Gameplay/NotesManager.cs
    Assets/Game/Scripts/Gameplay/Notes.cs
    Assets/Game/Scripts/Gameplay/Judge.cs
    Assets/Game/Scripts/Gameplay/MediaTimeline.cs
  → 本 Worker は「合意の取得と記録」までをタスクとする。合意取得前の改変は行わない。

[ 既に main にあるもの (変更しない) ]

V6UiMode.cs:27           public static bool Playfield = false;   ← owner rollback (再承認待ち)
  │
  ├─ V6PlayfieldRuntime.cs:52   if (!V6UiMode.Playfield) return;  ← Canvas 自体が作られない
  │     └─ :57+ Canvas (ScreenSpaceOverlay) + CanvasScaler を生成
  │
  ├─ V6GameRuntime.cs:370-374   HideLegacyPlayfield()
  │     └─ Playfield が true のときだけ HideByName("Lane"/"JudgeLine"/"LaneLights"/LaneLight1..8)
  │
  ├─ V6PlayfieldView.cs:38,118-124  NoteLayer (uGUI "Notes" RectTransform) を提供
  │     :155-165 レーンフラッシュ (KeyBindings.IsLaneKeyDown(lane) 駆動)
  │
  └─ Notes.cs:25,110-129,158-165,181
        V6PlayfieldView.Current == null → 3D transform にフォールバック
        存在するとき → head/body の Image を NoteLayer に生成、3D メッシュを隠す

V6LaneGeometry.cs:45  JudgeLineY = 792f
                  :48  LaneCount = 8 (Judge.LaneKeys と同一定義を参照)
                  :66  JudgeLineHeightDesignPx = 4f
                  :72  JudgeGlowY = 796f
                  :84  ExitT01
                  :125 遠近法 positions

GameScene.unity:2832  m_Name: Lane
                :2837  m_IsActive: 0        ← 3D フォールバック先が無い

[ 未充足 の 3 原因 ]
  (A) V6UiMode.Playfield = false        → owner rollback の再承認 (オーナー判断)
  (B) GameScene.unity の Lane が inactive → シーン改変 = 統合 owner との排他的合意が必要
  (C) 実機での 8 レーン / ノーツ / 判定同期 / 動画背景の観測 → Windows 実機 (オーナー実施)

[ 出力 ]
  ① 合意記録 (対象ファイルと変更内容)
  ② V6UiMode.Playfield の再承認記録
  ③ GameScene の Lane 復帰 or コード側 enabler (合意後)
  ④ 公開 API / 接続手順 / 所有オブジェクト / 必要な Unity アセット の PR 記載
  ⑤ 実機画像比較 (FHD / 2K / Mac)
```

## 実装順序

1. **統合 owner との排他的合意を取得する (最優先・他のすべてが前提)**
   - 合意の対象: `Assets/Game/Scenes/GameScene.unity` (Lane の active 化)、
     `Assets/Game/Scripts/Gameplay/NotesManager.cs`、
     `Assets/Game/Scripts/Gameplay/Notes.cs`、
     `Assets/Game/Scripts/Gameplay/Judge.cs`、
     `Assets/Game/Scripts/Gameplay/MediaTimeline.cs`。
   - 合意の記録 (日時 / 合意者 / 対象ファイル / 変更内容) を PR に残す。
   - **合意が得られるまで、5 ファイルとシーンには一切手を触れない。**
2. **`V6UiMode.Playfield` の再承認を请求する (オーナー判断)**
   - `Assets/Game/Scripts/UI/V6/V6UiMode.cs:27` を `true` にする承認を得る。
   - owner rollback (2026-11-08) の経緯を PR に引用する。
3. **描画コードの現状確認 (読み取り)**
   - `V6PlayfieldView` / `V6PlayfieldRuntime` / `V6LaneGeometry` の公開 API を一覧化する。
   - frozen デザイン `19f08b31` (node `nrfw8` 07 Gameプレイフィールド、1728×972) の
     8 path (Lane 0–7) / Judge Line y792 h4 / Judge Glow y796 / ノーツ遠近サイズ差 と
     実装の数値を突き合わせる。差分は **PR に記録** する。
4. **フォールバックの解決 (合意後)**
   - 案 A: `GameScene.unity:2837` の `m_IsActive: 0` を `1` に戻す (シーン改変)。
   - 案 B: コード側から `Lane` を active にする enabler を足す (`NotesManager` 側の改変)。
   - **どちらかを合意の範囲で選び、1 回で導入できる形で PR にまとめる。**
5. **動画背景との前後関係の確認 (合意後)**
   - 映像プレーンがプレイフィールドより **後ろ** にあり、prepare 前は白板のままにならないことを確認する
     (`#13` / PR #263 のブロッカー 1 の lessons)。
   - **レイヤー順序 / sortingOrder / 平面的配置 (z / sortingOrder) を記録**。
6. **EditMode テスト**
   - `Assets/Game/Tests/V6PlayfieldGeometryTests.cs` を実行して緑を確認する。
   - レーン描画 / 時間進行 / リサイズの境界ケースを既存テストで固定済みか確認し、
     不足分だけ追加する。
7. **公開 API と接続手順を PR に明記**
   - 統合 Worker が GameScene へ一度で導入できるよう、
     公開 API / 接続手順 / 所有オブジェクト / 必要な Unity アセット を書く。
8. **実機での画像比較 (オーナー実施)**
   - FHD 1920×1080 / 2K 2560×1440 / Mac 2560×1664 でフレーム切れ・レーンのズレ・上下反転を確認。
   - **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**。

## ブロッカー

| ブロッカー | 内容 | 対処 |
|---|---|---|
| **統合 owner との排他的合意** | `GameScene.unity` / `NotesManager` / `Notes` / `Judge` / `MediaTimeline` の改変は **合意なしに行えない**。 Issue 本文の Owned Paths が明文で要求 | **タスク 1 が最優先**。合意記録の提出まで、対象 5 ファイル + シーンには触れない |
| **`V6UiMode.Playfield` の再承認** | owner rollback (2026-11-08) により `false`。**オーナー判断** | 承認記録が取れるまで `V6UiMode.cs` を変更しない |
| **Unity Editor / シーンの単独所有** | Issue 本文 「Unity Editor単独所有は統合担当」 | cc はシーンを編集しない |
| **Windows 実機** | 実プレイでの 8 レーン / 判定同期 / 動画背景の観測は実機必須 | **オーナー実施**。macOS は実ディスプレイ 2560×1664 のみ証明可能 |
| **未マージ PR #204 との衝突** | Issue 本文 が注意喚起 | 取り込み前に差分を確認。衝突したら **通知して統合 owner と調整** |
| **`docs/evidence/index.md` の編集** | 他 Worker 所有 | 触らない |

## テスト戦略

| 種別 | 内容 | 備考 |
|---|---|---|
| EditMode (既存) | `Assets/Game/Tests/V6PlayfieldGeometryTests.cs` (レーン幾何 / 判定ライン / Glow / 遠近法 positions) | 緑でも描画上の問題は検出しない (フラグで無効化されている箇所は純論理的テストを通る) |
| EditMode (追加) | レーン描画 / 時間進行 / リサイズの境界ケース (既存テストで不足分のみ) | 判定・HOLD の再実装はしない |
| 実機 (Windows) | 8 レーン / ノーツの投影 / 判定同期 / 動画背景 / 3 解像度のフレーム比較 | **オーナー実施**。cc は実行しない |
| 実機 (macOS) | 実ディスプレイ 2560×1664 全画面での 1 曲プレイ確認 | **Win64 の代替にならない** |
| 回帰 | 既存プレイ完走と音 / 動画同期の非退化 (=#233 / `#226` の成果物) | 退化したら本 Issue の NG |
