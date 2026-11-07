---
description: "FlashBeat lean per-issue specification template (project-local override of the Spec Kit core spec template)"
---

# Spec: ベストスコアの保存キーを stable songId + 譜面版へ移行する (Issue #189)

**Issue**: #189 | **Branch**: `w-189-chart-score` | **PR base**: `main` | **Date**: 2026-11-08 | **Status**: Draft

**Input**: `[Persistence/P1] 曲追加でベストスコアが別曲へ移る：配列index保存をstable songId/譜面版へ移行する`

> Lean rules (enforced by `docs/agent/spec-kit-workflow.md`):
> one Issue = one branch = one worktree = one Draft PR; ≤5 tasks; no retrospective
> re-specification of the legacy game; no second authority for project governance.

## Objective

ベストスコアの保存キーを **カタログの並び順 (配列 index)** から
**安定 songId + 譜面版 (chartVersion)** へ移行する。曲を追加・並べ替え・削除しても、
他曲のベストスコアが移動しなくなる。旧形式 (index ベース CSV) のセーブデータは
**既知の対応表**で移行し、出所不明のスロットは別の曲へ推測で割り当てない。

## Owned Paths

- `Assets/Game/Scripts/Persistence/**` — 新規: stable key の解決、旧 CSV の移行、
  永続化 (PlayerPrefs) の読み書き
- `Assets/Game/Scripts/Songs/GManager.cs` — **Hiscore / 曲 ID 関連のみ**。
  `SongName` / `Musician` / `SongURL` / `SBPM` / `Slevel` / `Shit` / `SongLong`
  の 7 つの並列配列の並びと長さは **変更しない**
- `Assets/Tests/Editor/` — 新規: 保存 / カタログ整合の EditMode テスト
- `specs/189-score-attribution/{spec,plan,tasks}.md`

## Exclusions (explicitly out of scope)

- `Assets/Game/Scripts/Gameplay/**` — 別 Worker の排他所有
- `Assets/Game/Scripts/UI/**` — 別 Worker の排他所有
  (`ResultSceneManager` / `SelectView` / `PauseMenuView` / `GameHudView` が
  `GManager.Hiscore` の配列 index を読んでいる箇所は、**別 Issue の
  follow-up** として `spec.md` の Known Issues に記録する)
- `Assets/**/*.unity` / `Assets/Game/Resources/*.json` — 触らない
- `web/**` — Issue #191 の排他所有
- 本番 DB / Cloudflare デプロイ / 実機 Windows / Unity Editor 実行 —
  Supervisor の直列作業
- `GManagerParallelArrayUpdater.cs` (Editor ツール) — `Assets/Editor/**` は
  本 Issue の所有外。`Hiscore` の配列長を触る既存の挙動は Known Issues に記録

## User Scenarios & Testing *(mandatory)*

### Scenario 1 — 曲を追加しても他曲のベストスコアが動かない (Priority: P1)

**Given** 内蔵曲 A (id=1) と B (id=2) のベストスコアが保存されている
**When** 新しいクラウド曲 (辞書順で A より前) がカタログに追加される
**Then** A と B のベストスコアは **それぞれの stable songId** に紐づいたままで、
新曲には A の記録が継承されない

**Independent Test**:
`Unity -batchmode -quit -projectPath . -runTests -testPlatform editmode`
(Supervisor 実行。`ScoreStoreTests` の
`AddingASongBeforeAnExistingOneKeepsTheExistingBestScores` が通ることを要求)

### Scenario 2 — 45 曲を超えても記録を共有 / 喪失しない (Priority: P1)

**Given** 固定 45 要素の `Hiscore` 配列assumption だった時代の挙動
**When** 50 曲以上の曲でプレイして再起動する
**Then** 各曲在自己的 stable key に記録が残り、slot 0 を共有して記録が
失われる / 他曲へ移る 일이 없다

**Independent Test**:
`Unity -batchmode -quit -projectPath . -runTests -testPlatform editmode`
(Supervisor 実行。`ScoreStoreTests.TheBestScoreOfEverySongSurvivesMoreSongsThanTheOldTable`
が通ることを要求)

### Scenario 3 — 旧セーブデータから移行し、出所不明のスロットは移さない (Priority: P1)

**Given** 旧形式 (`PlayerPrefs` の `"Hiscore"` key に index 順の CSV) のセーブデータが
ある
**When** 新しいバージョンで読み込む
**Then** 内蔵曲 (固定 id → stable key の **既知の対応**) の記録は移行され、
**内蔵曲に対応しないスロット** (旧 remote slot) は **どの曲にも割り当てず**、
移行済み_FLAG と 함께保持 (黙って消さない / 推測で別曲に割り当てない)

**Independent Test**:
`Unity -batchmode -quit -projectPath . -runTests -testPlatform editmode`
(Supervisor 実行。`ScoreStoreTests.TheLegacyCsvIsMigratedForTheBuiltInSongs` と
`TheLegacyCsvDoesNotGuessWhichRemoteSongAnUnknownSlotBelongedTo` が通ることを要求)

### Edge Cases

- **旧 CSV の要素数が `Hiscore` 配列より多い/少ない**: 範囲外の要素は読み飛ばす
  (例外を出さない)
- **旧 CSV が壊れている (非数値要素)**: その要素だけ 0 として扱い、他の要素は移行する。
  全面リセットしない
- **新形式 (key 付き) が既に保存されている**: 新形式を優先し、旧 CSV の移行は
  **一度だけ** (移行済み FLAG) 行う。再起動で二重適用しない
- **chartVersion が 0 / 不明な曲** (cloud catalog 未取得時): 谱面版は
  `chartVersion = 0` (「版不明」) として扱う。同じ曲でも版が変わると
  **別の key** になり、ベストは **Guy 历来の値を引き継がない** (R4 のポリシー)
- **同名曲 / 同 YouTube ID の重複**: stable key の衝突は **後勝ち** で
  検出する (二重登録の検出ログ)
- **他 Issue の Worker が `GManager.Hiscore` を配列として読む**: 本 Issue は
  `GManager.Hiscore` を **削除しない**。既存の読み書き経路は壊さない
  (後続 Issue で移行する)

## Requirements

- **R1**: ベストスコアの保存キーは **stable songId** と **譜面版 (chartVersion)** の
  組で一意に決まる。カタログの並び順 (配列 index) は **保存キーに含まれない**
- **R2**: 内蔵曲には **並べ替えに依存しない stable songId** を与える
  (内蔵曲 index → stable key の対応表を持つ。7 つの並列配列は変更しない)
- **R3**: 保存は `PlayerPrefs` に **key → score** の **新形式** で書き出す。
  旧形式 (`"Hiscore"` key の index ベース CSV) は **読み書きの壊れた
  後方互換** として残す (旧 `"Hiscore"` key を **上書きしない**)
- **R4**: **譜面版更新時のベスト比較ポリシー** を明示する:
  譜面版が変わると key が変わるため、**新しい譜面版のベストは 0 から開始**する
  (別譜面の記録を混ぜない)。旧版Leaderは **履歴として保持** される (消さない)
- **R5**: 旧形式からの移行は **既知の対応** (内蔵曲) に対してのみ行い、
  出所不明のスロットは **どの曲にも割り当てない** (推測で別曲へ移さない)
- **R6**: 移行は **冪等** かつ **一度だけ** (移行済みフラグ) 実行される
- **R7**: 曲を追加・並べ替え・削除・API 失敗/復帰・再起動を跨いで、
  同じ曲 (同じ stable key) の記録が残る
- **R8**: 45 曲を超えても記録を共有 / 喪失しない (固定長配列の index を
  キーにしない)
- **R9**: 読み込み・保存は **壊れた入力で例外を投げない** (防御的にパースする)
- **R10**: `GManager.Hiscore` (index ベースの配列) の **公開 API は壊さない**
  (既存コードがコンパイル・動作しつ���)。新方式の正本は
  `Assets/Game/Scripts/Persistence/` の新クラスが保持する

## Known Issues (この Issue が解かないもの)

### K1 — UI 層は 아직 index ベースの `GManager.Hiscore` を読んでいる

`ResultSceneManager` / `SelectView` / `PauseMenuView` / `GameHudView` /
`SelectViewLogic` は `GManager.Hiscore[arrayIndex]` を直接読んでいて、
`index >= Hiscore.Length` のときは 0 番へ丸める (slot 0 を共有)。
本 Issue は **保存モデル** を新しいものにするが、**UI 側の読み取り経路** は
`Assets/Game/Scripts/UI/**` の排他所有のため触っていない。

→ **follow-up Issue が必要**: 「UI 層的最佳スコア参照を stable key 経由に
置き換える」。§1 の「曲追加で他曲へ移る」現象は **保存モデル** が直ると
再起動後の永続化分は直るが、**同一セッション内の remote 曲** は
UI 側がまだ index 経由のため **暫く残存** する (Known Issue として明示)。

### K2 — `Assets/Editor/GManagerParallelArrayUpdater.cs` は `Hiscore` の配列長を延ばす

この Editor ツールは `public static readonly int[] Hiscore = new int[N];` の
`N` を曲追加のたびに +1 する。新方式では配列長に依存しないので
この拡張は不要になるが、`Assets/Editor/**` は本 Issue の所有外。
→ follow-up で削除する (新方式では無害)。

### K3 — 旧 `"Hiscore"` key を消さない

旧 key を **上書きしない** 方針 (R3) のため、`PlayerPrefs` に旧 key が残る。
読み込みは毎回「新形式 → 旧形式 (移行済みならスキップ)」の順で判定する。
`OptionView` の `PlayerPrefs.DeleteKey("Hiscore")` (hibcore リセット) は
**新形式 key も消す** 必要があり、`Assets/Game/Scripts/UI/**` の排他所有。
→ follow-up Issue で「hibcore リセットが新形式も消す」ようにする。

### K4 — Unity Editor / 実機での確認は未実施

本 Issue は EditMode テストコードまでを書く。Unity EditMode テストの実行と
実機 (Windows ビルド) での確認は **Supervisor の直列作業**。

## Dependencies & Gate

- **Depends on**: なし (Issue #189 は自立している)
- **Blocks**: 「UI 層を stable key 経由に置き換える」follow-up Issue
- **Gate**:
  1. `Unity -batchmode -quit -projectPath . -runTests -testPlatform editmode`
     が green (**Supervisor 実行**)
  2. 既存 EditMode テスト (`SongDataTests` / `CatalogMergeTests` /
     `CloudCatalogTests` / `ChartDataIntegrityTests`) に regression がない
  3. `git diff --stat <base> -- Assets/Game/Resources/` が空
  4. `GManager.cs` の 7 つの並列配列が **バイト単位で不変** である

## Verification Evidence

| # | Check | Command / Method | Expected result |
|---|-------|-------------------|-----------------|
| V1 | 新規 EditMode テスト | `Unity -batchmode -quit -projectPath . -runTests -testPlatform editmode -testResults TestResults.xml` | `ScoreStoreTests` 全 pass (**Supervisor 実行**) |
| V2 | 既存テストの回帰 | 同上 | 既存 suite に regression なし (**Supervisor 実行**) |
| V3 | 並列配列の不変 | `git diff <base> -- Assets/Game/Scripts/Songs/GManager.cs` | 7 配列の並び・長さが不変 |
| V4 | 実譜面データ無変更 | `git diff --stat <base> -- Assets/Game/Resources/` | 空 |
| V5 | 型/コンパイル | Unity のコンパイルログ | エラー 0 (**Supervisor 実行**) |

## Rollback

本 Issue の差分は (a) `Assets/Game/Scripts/Persistence/**` の新規ファイル、
(b) `GManager.cs` の Hiscore 関連、`specs/**` のドキュメント、
(c) 新規 EditMode テストの 3 つに分割できる。新形式 key は
**旧 `"Hiscore"` key を上書きしない** ので、revert しても旧形式のセーブデータは
失われない (新形式 key が残るだけ。 Orphan になるが害はない)。
全体を戻す場合はブランチ `w-189-chart-score` の merge commit を revert する。

## Success Criteria

- **SC1**: 曲を追加・並べ替え・削除しても、**他曲** のベストスコアが動かない
  (EditMode テストで証明)
- **SC2**: 45 曲を超えても記録を共有 / 喪失しない (EditMode テストで証明)
- **SC3**: 旧 CSV の移行が **内蔵曲についてのみ** 行われ、出所不明のスロットは
  推測で割り当てられない (EditMode テストで証明)
- **SC4**: 既存の `GManager.Hiscore` 配列 API は **壊れていない** (コンパイル +
  既存テストの回帰なし)
