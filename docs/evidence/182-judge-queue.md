# Issue #182 — 判定キューをレーン別・時刻順に再設計 (クローズ証跡)

- Issue: [#182](https://github.com/rebuildup/FlashBeat/issues/182) 「[Game/P1] 判定キューをレーン別・時刻順に再設計：Web編集順と他レーンの未処理ノーツが正常入力を阻害」
- 日付: 2026-11-09 (PR #256 の独立レビュー指摘を反映するまで同日)
- ブランチ: `w/w182-close` (base: `origin/main` @ `d8c1b39`)
- ワークツリー: `/Users/rebui/work/FlashBeat.w182b`
- 検証したコミット: `ff7fa33` (`test(#182): 判定確定数のテストを production の経路で検証する`)
  — EditMode スイート 1085 / 1085 Pass のときの状態
- 先行資料: `docs/evidence/182-publish-to-judge-regression.md` (前回の Worker が追加した Web 公開譜面 → Unity 判定の回帰テスト)

## 0. 結論

Issue #182 の実装 (`ad20d21` / `6d9a4ae` / `f953eea`) は `origin/main` に既に入っていたが、
受入条件のうち **「最終判定数 = noteCount」「スコア API との整合」は裏付けされるテストが無かった**。

本作業で

1. 受入条件 1 件ずつを main の実コード + テスト名つきで照合し、
2. 未裏付けだった 2 条件を **EditMode テストで新規に固定**し、
3. その過程で **実バグ 1 件** (HOLD 中断時に「確定したノーツ数」が二重に数えられる) を
   テストが赤くなる形で発見・修正し、

受入条件 (Issue 本文の箇条書き 5 項目。うち「最終判定数 = noteCount」と
「スコア API との整合」は本作業で初めて裏付けられた) すべてを Pass できる状態にした。
下表の行 4 と行 5 は Issue 本文の 4 番目の箇条書きを 2 つに分けて書いてある。

さらに PR #256 の独立レビュー指摘 (B1 / B2 / B3 / S1 / S2 / S3 / S4) を反映し、
FIXME だったテスト (アサーションしない `Assert.Pass` だけのテスト) を
実際に検証するテストに置き換え、あわせて API の明確化とガードを追加した
(§7)。EditMode スイートは **1085 / 1085 Pass**
(追加前 1073 件 → 追加 12 件: `JudgeSettlementInvariantTests` 10 本)。

---

## 1. 受入条件 × 根拠 × 実測結果

| # | 受入条件 (Issue 原文) | 判定 | 根拠 (実装) | 根拠 (テスト) | 実測 |
|---|---|---|---|---|---|
| 1 | 後半→前半の追加、ドラッグで順序が逆転、異なる LPB の混在を経た Web 公開譜面を Unity で正しく判定できる | **Pass** | `Assets/Game/Scripts/Gameplay/JudgeQueue.cs:98-119` — `Add` はレーン内で時刻順に挿入する (同一時刻は追加順)。`Assets/Game/Scripts/Gameplay/Judge.cs:199-227` — `EnsureQueue` は譜面ファイルの順に積むだけで、並び替えはキュー側が行う | `JudgeQueueTests.EditOrderDiffersFromTimeOrder_NoNoteIsSkipped` / `PublishedChartJudgeRegressionTests.PublishedChart_QueueIsLaneWiseAndTimeOrdered` / `PublishedChartJudgeRegressionTests.PublishedChart_NoteTimesMatchChartFormula` / **新規** `JudgeSettlementInvariantTests.AllNotesPressed_ExactlyOnce_TotalJudgementsEqualNoteCount` (LPB 4 と 8 混在 + 8 レーン + HOLD 2 本 + 編集順≠時刻順 の 12 ノーツ譜面) | 12/12 ノーツが Perfect。合計判定数 = 12 = noteCount。キュー残 0。`GManager.settledNoteCount` = 12 |
| 2 | 他レーンの未処理ノーツは、押したレーンの判定幅内入力を妨げない | **Pass** | `Assets/Game/Scripts/Gameplay/JudgeQueue.cs:124-152` — `TryPress` は **そのレーンだけ** を，符合幅内で最近傍の 1 ノーツを探す。他レーンの未処理ノーツは参照しない | `JudgeQueueTests.UnprocessedNoteOnOtherLane_DoesNotBlockHit` / `PublishedChartJudgeRegressionTests.PublishedChart_NoteOnOtherLaneDoesNotBlockJudgement` / **新規** `JudgeSettlementInvariantTests.AllNotesPressed_ExactlyOnce_TotalJudgementsEqualNoteCount` | 他レーンに未処理ノーツが残っている状態で他レーンのノーツが全て Perfect (12/12) |
| 3 | 同時押し、HOLD 保持中の他レーン Tap、フレーム落ちで複数 Miss が溜まるケースでも、各ノーツが一度だけ確定する | **Pass** (本作業で 1 件のバグを修正) | `Assets/Game/Scripts/Gameplay/JudgeQueue.cs:124-152` (押下は 1 レーン 1 ノーツ) / `:154-171` (`ConsumeTap`・`BeginHold` でキューから除去) / `Assets/Game/Scripts/Gameplay/Judge.cs:161-190` (HOLD 中断は `DowngradeHoldToBad` で **差し替え**。追加加点しない) / `Assets/Game/Scripts/Gameplay/JudgeScoreboard.cs` (`Settled` は押下と Miss だけを数える) | `JudgeQueueTests.SimultaneousPress_TwoLanes_BothJudged` / `JudgeQueueTests.Hold_WhileHolding_IsNeverMissed` / **新規** `JudgeSettlementInvariantTests.SameLaneSameTime_TwoPresses_SettleBothNotes` (同時押し) / **新規** `JudgeSettlementInvariantTests.TapOnOtherLane_WhileHolding_IsJudged` (HOLD 保持中の他レーン Tap) / **新規** `JudgeSettlementInvariantTests.FrameDrop_ManyNotesExpireAtOnce_EveryNoteSettledExactlyOnce` (1.0 秒 → 3.5 秒のフレーム落ちで複数 Miss が溜まる) | 3 シナリオとも 合計判定数 = noteCount、キュー残 0、HOLD 中断で確定数が 2 に増えない |
| 4 | **最終判定数 = noteCount** | **Pass** (本作業で新規に固定) | `Assets/Game/Scripts/Gameplay/JudgeScoreboard.cs` — `ApplyHead` (タップ / HOLD の先頭) と `ApplyMiss` が 1 ノーツ 1 判定、`DowngradeHoldToBad` は差し替えのみ。`Assets/Game/Scripts/Gameplay/Judge.cs` — `PressLane` / `UpdateHold` / `TryPeekMiss` がこの方針で素直に 1 判定 1 ノーツにする | **新規** `JudgeSettlementInvariantTests.AllNotesPressed_ExactlyOnce_TotalJudgementsEqualNoteCount` / `…FrameDrop_ManyNotesExpireAtOnce_EveryNoteSettledExactlyOnce` / `…SameLaneSameTime_TwoPresses_SettleBothNotes` | 12 ノーツ譜面で `Perfect + Great + Bad + Miss` = 12 = noteCount (3 シナリオとも)。キュー残 0 |
| 5 | **スコア API との整合** | **Pass** (本作業で新規に固定 + バグ 1 件修正) | `Assets/Game/Scripts/Gameplay/JudgeScoreboard.cs` (配点) / `Assets/Game/Scripts/Gameplay/Judge.cs:307-331` (`FlushScore` が `GManager.ratioScore/perfect/great/bad/miss/combo/maxCombo` を書き、`Judge.ComputeScore` で `GManager.score`) / `Assets/Game/Scripts/Gameplay/NotesManager.cs:134` (`GManager.maxScore = total * 5` — 本作業のテストは本番の `LoadRoutine` を通すので、この行が production のものだと検証できる) / `Assets/Game/Scripts/Gameplay/MusicManager.cs:990` (`settledNotes = GManager.settledNoteCount` → `SongEndStateMachine.AllSettled` の曲終了判定) | **新規** `JudgeSettlementInvariantTests.ScoreApi_MatchesJudgements_AndResultScreenFields` / `…AllPerfect_ScoreIsExactlyOneMillion` / `…BrokenHold_SettlesExactlyOneNote_NotTwo` | `ratioScore` = 5×perfect + 3×great + 1×bad、`maxScore` = noteCount×5 = 60、`score` = `Judge.ComputeScore(ratioScore, maxScore)`、全 Perfect で 1,000,000、`settledNoteCount` = 確定ノーツ数 = 12 |
| 6 | Web 編集→Publish→取得→Unity 判定をつなぐ回帰ケースを追加する | **Pass** (前回 Worker の成果を引き継ぐ + 本作業で `Judge` 本体まで通す) | `Assets/Game/Scripts/Songs/CloudCatalog.cs:159` — `ParseChartResponse` は `GET /api/v1/charts/{songId}` の包みを production のパーサで読む。`Assets/Game/Scripts/Gameplay/NotesManager.cs:74` — `NoteTimeSeconds` (60/BPM×num/LPB) | `PublishedChartJudgeRegressionTests` (8 本、既存) / **新規** `JudgeSettlementInvariantTests` (10 本。同じ production パーサ・同じ時刻式で Web 公開形の譜面を読み、`Judge` 本体まで駆動) | 既存 8 本 + 新規 10 本がすべて Pass |

---

## 2. 本作業で発見・修正した実バグ

### 2.1 HOLD 中断で「確定したノーツ数」が二重に数えられていた

**症状**: `GManager.nextNotes` は「判定イベント数」を数えていた (`Judge.message()` が呼ばれるたび `+1`)。
HOLD を途中で離して BAD に差し替えると、同じノーツについて

- 先頭の押下 (1 回目)
- 中断による BAD 差し替え (2 回目)

の **2 回分** として数えられていた。

**なぜ重大か**: `Assets/Game/Scripts/Gameplay/MusicManager.cs` は

```
int settledNotes = GManager.settledNoteCount;   // 「確定済みノーツ数」
int pendingNotes = totalNotes - settledNotes;
```

を `SongEndStateMachine` に渡し、`Assets/Game/Scripts/Gameplay/SongEndStateMachine.cs:156-159` の
`AllSettled` (`PendingNotes <= 0`) で **曲終了を確定する**。
HOLD 中断が 1 つあるだけで「残りノーツ数」が 1 少なく見積もられ、
未判定のノーツが残っていても曲終了が先に起こりうる
(＝受入条件「各ノーツが一度だけ確定する」が破れる)。

**修正**: 「確定したノーツ数」を `JudgeScoreboard.Settled` として明示的に持ち、
`Judge.FlushScore` で `GManager.settledNoteCount = board.Settled` を書くようにした
(旧 API の `GManager.nextNotes` は「1 + 確定数」の互換値として残す)。
`ApplyHead` (タップ / HOLD の先頭) と `ApplyMiss` が 1 ずつ数え、
`DowngradeHoldToBad` は差し替えなので数えない (= `Settled == Perfect + Great + Bad + Miss`)。

「`1 + N` を書いて `N` を読む」という規約は読み手に誤解を招くため、
PR #256 のレビュー (S1) を受けて **正は `GManager.settledNoteCount`** にし、
`MusicManager` はこの値を直接読むようにした。

**赤くなる形の証拠** (詳細は `docs/evidence/182-judge-queue/test-results-summary.md`):

| テスト | 修正前 | 修正後 |
|---|---|---|
| `BrokenHold_SettlesExactlyOneNote_NotTwo` | Fail (期待 1 / 実際 2) | Pass |
| `FrameDrop_ManyNotesExpireAtOnce_EveryNoteSettledExactlyOnce` | Fail (期待 12 / 実際 13) | Pass |

スイート全体では 修正前 1078 passed / 2 failed → 修正後 **1080 passed / 0 failed**
(PR #256 のレビュー対応でテストを追加した現在の値は **1085 passed / 0 failed**)。

### 2.2 `Judge` を EditMode テストから駆動できなかった (テストの穴 / false green の温床)

`Judge` は `MonoBehaviour` で `Input.GetKeyDown` と `GManager.step_time` に依存するため、
既存テスト (`JudgeQueueTests` / `PublishedChartJudgeRegressionTests`) は
**`Judge` の呼び出し順序をテスト側で再実装**していた
(前者は `JudgeQueue` のみ、後者は `ChartRunner` ラッパー)。
つまり `Judge` 本体 (キュー組み立て / スコア反映 / `GManager` への書き戻し) に
バグがあってもテストは緑のまま (false green)。

そこで `Judge.Update` の判定 1 ステップを

```csharp
public virtual void JudgeStep(float mediaTime, Func<int, bool> lanePressed, Func<int, bool> laneDown)
```

として切り出し、`Update` からは `Input` 由来の述語 (`IsLaneKeyPressed` / `IsLaneKeyDown`) を
渡して呼ぶようにした (**実挙動の変更はない**: 呼び出し順・判定式はそのまま)。
これにより EditMode テストから **実機と同じ経路** を検証できる。

---

## 3. 既存テストの true-green チェック

| 既存テスト | 判定 | 備考 |
|---|---|---|
| `Assets/Tests/Editor/JudgeQueueTests.cs` (18 本) | 本物の production コード (`JudgeQueue` / `JudgeScoreboard`) を駆動 | 問題なし。ただし `Judge` 本体 (MonoBehaviour) は通っていない |
| `Assets/Tests/Editor/PublishedChartJudgeRegressionTests.cs` (8 本) | `ChartRunner` が **`Judge` の呼び出し順序をテスト側で再実装** | **false green の余地あり**: `Judge` 本体の `FlushScore` / `GManager.settledNoteCount` / 譜面ファイル読み込み経路は検証対象外。→ 本作業の新規テスト (`JudgeSettlementInvariantTests`) が `Judge` 本体を駆動して穴を埋めた |
| `Assets/Tests/Editor/JudgeLogicTests.cs` (`ComputeScore`) | 純関数を直接検証 | 問題なし |
| `Assets/Tests/Editor/LongNoteTests.cs` | 純関数を直接検証 | 問題なし |

「早期 return して更新されていない」まで見る (＝偽陽性を潰す) 指示に沿い、
新規テストは **「合計数」「内訳」「キューの残数」「GManager への反映」の 4 つ**を
必ず同時に見る形にした。早期 return で何も起きない =
合計が noteCount にならない ので必ず落ちる。

---

## 4. 追加・変更したファイル

| ファイル | 種別 | 内容 |
|---|---|---|
| `Assets/Tests/Editor/JudgeSettlementInvariantTests.cs` | 新規 | EditMode テスト 10 本 (§4.1) |
| `Assets/Game/Scripts/Gameplay/Judge.cs` | 変更 | `JudgeStep` の切り出し / `GManager.settledNoteCount` を「確定したノーツ数」方式へ / `message()` のイベント数カウントを廃止 / `holdHead` の初期値を `None` に / `JudgeStep` の virtual 化と `LastJudgeMediaTime` (テスト用の観測点) |
| `Assets/Game/Scripts/Gameplay/JudgeScoreboard.cs` | 変更 | `Settled` (確定したノーツ数) の追加 / `DowngradeHoldToBad` の `None` ガード |
| `Assets/Game/Scripts/Gameplay/MusicManager.cs` | 変更 | 曲終了判定を `GManager.settledNoteCount` から読む |
| `Assets/Game/Scripts/Songs/GManager.cs` | 変更 | `settledNoteCount` の追加 / `nextNotes` の互換値のドキュメント |
| `docs/evidence/182-judge-queue.md` | 新規 | 本書 |
| `docs/evidence/182-judge-queue/test-results-summary.md` | 新規 | EditMode 実行結果サマリ (緑 / 赤 demonstration) |

新規ファイルは `Assets/Tests/Editor/` と `docs/` 配下のみ
(`.meta` は Unity が生成)。
**変更した `Assets/Game/Scripts/` のファイルは判定の集計に関する 4 つだけ**
(`Judge.cs` / `JudgeScoreboard.cs` / `MusicManager.cs` / `GManager.cs`、
いずれも判定の確定数に関するもので、他の担当領域には触っていない)。

### 4.1 追加したテスト (10 本)

| テスト名 | 内容 |
|---|---|
| `AllNotesPressed_ExactlyOnce_TotalJudgementsEqualNoteCount` | Web 公開形の混在譜面 (8 レーン / LPB 4 と 8 / HOLD 2 本 / 編集順≠時刻順 / 12 ノーツ) を全ノーツ押下 → 合計判定数 = noteCount、キュー残 0、確定ノーツ数 = noteCount |
| `FrameDrop_ManyNotesExpireAtOnce_EveryNoteSettledExactlyOnce` | 1.0 秒 → 3.5 秒へフレームを飛ばして複数 Miss を溜める → 合計判定数 = noteCount、Perfect 3 / Bad 1 (HOLD 中断は差し替えで数が増えない) / Miss 8 |
| `BrokenHold_SettlesExactlyOneNote_NotTwo` | HOLD 1 本だけの譜面。途中で離して BAD に差し替えても **確定ノーツ数は 1** |
| `SameLaneSameTime_TwoPresses_SettleBothNotes` | 同じ時刻・同じレーンに 2 ノーツ → 2 回の入力で 2 ノーツが確定 |
| `TapOnOtherLane_WhileHolding_IsJudged` | HOLD 保持中に他レーンの Tap が判定される (受入条件 3) |
| `ScoreApi_MatchesJudgements_AndResultScreenFields` | ratioScore / maxScore / score / settledNoteCount と判定内訳の整合 (maxScore は本番の `NotesManager.LoadRoutine` が書いた値) |
| `AllPerfect_ScoreIsExactlyOneMillion` | 全 Perfect で 1,000,000 点 |
| `SongEnd_CompletedOnlyWhenAllNotesSettled` | 判定 → pendingNotes → `SongEndMachine.Completed` の結合 |
| `Update_PassesGManagerStepTimeToJudgeStep` | `Judge.Update` が `JudgeStep` に渡す時刻が `GManager.step_time` であること (PR #256 の S3。旧版は `Assert.Pass` だけで検証していないので作り直した) |
| `DowngradeHoldToBad_WithNoHeadJudgement_DoesNotInflateSettledCount` | 先頭判定が `None` のとき中断処理が確定数を水増ししないこと (PR #256 の S2) |
| `Judge_HoldHeadStartsAsNone` | `Judge.holdHead` の初期値が `None` であること (PR #256 の S2) |
| `NextNotes_StaysCompatibleWithSettledNoteCount` | 旧 API `nextNotes` が互換値「1 + 確定数」を保つこと (PR #256 の S1) |

### 4.2 テストが使う譜面と通す production コード

`Assets/Tests/Editor/JudgeSettlementInvariantTests.cs` の `MixedChartJson` /
`SameLaneSameTimeChartJson` / `HoldOnlyChartJson`。

| 段 | 通す production コード |
|---|---|
| Web 公開 JSON → 譜面 | `CloudCatalog.ParseChartResponse` (`Assets/Game/Scripts/Songs/CloudCatalog.cs:159`) |
| ノーツ時刻 | `NotesManager.NoteTimeSeconds` (`Assets/Game/Scripts/Gameplay/NotesManager.cs:74`) |
| HOLD の終端 | `LongNote.EndSeconds` (`Assets/Game/Scripts/Gameplay/LongNote.cs:91`) |
| 譜面 → 平行配列 (番兵 10 本込み) と `GManager.maxScore` | **本番の** `NotesManager.LoadRoutine` を直接呼んで読み込ませる (`Assets/Game/Scripts/Gameplay/NotesManager.cs:113-224`) |
| キュー組み立て | `Judge.EnsureQueue` (`Assets/Game/Scripts/Gameplay/Judge.cs:247-286`) |
| 判定 | `Judge.JudgeStep` (押下 → HOLD 保持 → Miss) (`Assets/Game/Scripts/Gameplay/Judge.cs:147-215`) |
| 集計 | `JudgeScoreboard` / `Judge.FlushScore` → `GManager` (`Assets/Game/Scripts/Gameplay/JudgeScoreboard.cs`, `Assets/Game/Scripts/Gameplay/Judge.cs:307-331`) |

---

## 5. 実行したこと

### 5.1 EditMode スイート (全件)

```
/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity -batchmode \
  -projectPath /Users/rebui/work/FlashBeat.w182b -runTests -testPlatform editmode \
  -testResults /Users/rebui/work/FlashBeat.w182b/TestResults-256.xml \
  -logFile /Users/rebui/work/FlashBeat.w182b/unity-256.log
```

(`-quit` は付けない。付けると 1 件も走らない)

PR #256 のレビュー対応まで終わった状態 (コミット `ff7fa33`、2026-11-09) の結果:

```
total=1085 passed=1085 failed=0 skipped=0 inconclusive=0
duration=2.72s   (start-time=2026-11-09 14:44:09Z / end-time=2026-11-09 14:44:12Z)
```

### 5.2 修正前 (赤 demonstration)

`JudgeScoreboard.Settled` の加算と `Judge.FlushScore` の `GManager.nextNotes` 書き込みを
修正前の形 (`message()` での `GManager.nextNotes++` = 判定イベント数のカウント) に戻した状態で
同スイートを走らせた結果:

```
total=1080 passed=1078 failed=2
  - JudgeSettlementInvariantTests.BrokenHold_SettlesExactlyOneNote_NotTwo
      HOLD 中断で確定ノーツ数が 2 になった  (Expected: 1, But was: 2)
  - JudgeSettlementInvariantTests.FrameDrop_ManyNotesExpireAtOnce_EveryNoteSettledExactlyOnce
      GManager.settledNoteCount が noteCount と一致しない  (Expected: 12, But was: 13)
```

→ 同じ 2 本が修正後に Pass することを確認済み (緑: 1080/1080)。
詳細は `docs/evidence/182-judge-queue/test-results-summary.md`。

### 5.3 負の制御 (PR #256 の S3)

`Judge.Update` が `GManager.step_time` ではなく `Time.time` を
`JudgeStep` に渡すよう **一時的に** production コードを変更して走らせた結果:

```
total=1085 passed=1084 failed=1
  - JudgeSettlementInvariantTests.Update_PassesGManagerStepTimeToJudgeStep
      Judge.Update が JudgeStep に渡した時刻が GManager.step_time と一致しない
      (Expected: 123.5, But was: 0.0)
```

→ テストはJudge の時刻の受け渡しを実際に検証できている (変更は reverted)。

---

## 6. 未検証 / 残存リスク

- **実機 (Windows / macOS プレイヤー) での通し再生は未実施。**
  本テストは `Judge` の 1 ステップ (判定 → スコア反映 → `GManager` 書き戻し) までを再現し、
  譜面の読み込みも本番の `NotesManager.LoadRoutine` を通す (固定 JSON 用)。
  ただし `Input` / `VideoPlayer` / `AudioSource` / `Resources` からの実譜面読み込み
  (曲ID ごとの `Resources/<曲名>.json` 読み込み) は通っていない。
- **Web UI を通した publish → 実機プレイの E2E は未実施** (前回 Worker と同じ制約)。
  本テストは「Web が書き出す形」を production のパーサで読んだ静的レグレッション。
- キー入力は EditMode テストから起こせないため、テスト側の述語 (ラッパ) による代用。
  実キーの押下_edge 検出 (`Input.GetKeyDown`) 自体は未検証。
- `GManager.nextNotes` の意味が変わった (判定イベント数 → 確定したノーツ数)。
  読み手 (`MusicManager` のみ) には影響しないことを確認済み。
  PR #256 のレビュー (S1) で **正は `GManager.settledNoteCount`** に統一し、
  `nextNotes` は「1 + 確定数」の互換値として残した。
  今後 `nextNotes` を UI に出す経路が増えたときは
  「確定したノーツ数」と「判定イベント数」を区別すること。
- `GManager.maxScore = noteCount * 5` は、本番の `NotesManager.LoadRoutine` を
  テストから直接呼んで検証している (PR #256 のレビュー S3 で、
  テスト側が maxScore を書いていた自己充足的アサーションをやめた)。
  ただし譜面ファイル (Resources) からの実読み込みは通っていない。

---

## 7. PR #256 の独立レビュー指摘への対応

PR #256 に出た独立レビュー指摘に対する対応と、その根拠。
各項目は 1 コミットに分けている。

| ID | 指摘 | 対応 | コミット |
|---|---|---|---|
| B1 | `unity-cc.log` (4487 行、絶対パス入り) が PR に含まれていた | `git rm --cached` で追跡対象から外し、`.gitignore` の build/test ログ枠に `unity-*.log` / `unity-*.out` を追加 | `b9f5f52` |
| B2 | 日本語フォント SDF アセットの無関係な LFS 差分 (+16 bytes) が含まれていた | `origin/main` の版に復元 (アセットの再生成はしない) | `8ee273a` |
| S1 | 「`nextNotes = 1 + N` / `nextNotes - 1`」の規約が未記載で、`JudgeScoreboard.cs:42` が存在しない `GManager.SettledNoteCount` を参照 | `GManager.settledNoteCount` を追加し、`Judge.FlushScore` が書き、`MusicManager` が直接読む。`nextNotes` は互換値として残す | `7e1c095` |
| S2 | `Judge.holdHead` の初期値が `Perfect` (0) で、`DowngradeHoldToBad` に `None` ガードがない | `holdHead` を `None` 初期化 (生成時 + `Start()`)、`DowngradeHoldToBad` に `None` の早期 return を追加 | `88c3a9c` |
| B3 / S3 | `Update_PassesGManagerStepTimeToJudgeStep_SourceStructure` が IL を読むだけで何も検証せず `Assert.Pass` で終わっていた | 実際に検証するテストに置き換え (`Judge.JudgeStep` を virtual 化 + `Judge.LastJudgeMediaTime` の観測点を追加) | `ff7fa33` |
| S3 | `maxScore == noteCount * 5` のアサーションがテスト側で `maxScore` を書いていたため自己充足だった | 譜面読み込みを本番の `NotesManager.LoadRoutine` に通すようにし、配点上限の書き込みを production が担うことを検証 | `ff7fa33` |
| S4 | 証跡のテスト数が古い SHA 時点のもののまま | 本書と `test-results-summary.md` を 1085 / 1085・検証コミット `ff7fa33`・日付 2026-11-09 で更新 | (本書) |

### 7.1 B3 のテストが「実際に検証している」ことの確認 (負の制御)

`Judge.Update` の `JudgeStep` 呼び出しを **一時的に** `GManager.step_time` から
`Time.time` に変更して EditMode スイートを走らせたところ、
`Update_PassesGManagerStepTimeToJudgeStep` だけが赤になった
(1085 件中 1084 passed / 1 failed)。
つまりこのテストは `Judge` の時刻の受け渡しを本当に検証していて、
`Assert.Pass` だけの「緑」ではない。変更は revert 済み (§5.3)。

なお、`Judge` の派生クラスで `JudgeStep` を差し替える方式も試したが、
テストが `Assets/Tests/Editor/` 配下にあるため
「Can't add script behaviour 'TestJudge' because it is an editor script」で
`AddComponent` できない。観測点 (`LastJudgeMediaTime`) を本体に持たせる方式を採った。
