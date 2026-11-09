# Issue #182 — EditMode テスト結果サマリ

`Assets/Tests/Editor` の EditMode スイート全件 (Unity 6000.5.6f1, `-batchmode -runTests -testPlatform editmode`)。

基準: `origin/main` @ `d8c1b39` 時点で 1073 件が Pass していた。
本作業で EditMode テストを追加し、1073 + 12 = 1085 件。

- ブランチ: `w/w182-close`
- 検証したコミット: `ff7fa33` (`test(#182): 判定確定数のテストを production の経路で検証する`)
- 日付: 2026-11-09
- ワークツリー: `/Users/rebui/work/FlashBeat.w182b`

## 修正後 (緑)

### 緑 (`JudgeScoreboard.Settled` で「確定したノーツ数」を数える修正 + PR #256 のレビュー対応入り)
- source: `TestResults-256.xml`
- total=1085 passed=1085 failed=0 skipped=0 inconclusive=0
- start-time=2026-11-09 14:44:09Z end-time=2026-11-09 14:44:12Z duration=2.7202776s
- failed: なし

## 修正前 (赤 demonstration)

HOLD 中断時の「確定したノーツ数」の二重計数を再現するため、
`JudgeScoreboard.Settled` の加算と `Judge.FlushScore` の `GManager.nextNotes` 書き込みを
修正前の形 (`message()` での `GManager.nextNotes++` = 判定イベント数のカウント) に戻して走らせた結果。

### 修正前 (判定イベント数を数える旧実装)
- source: `TestResults-red.xml`
- total=1080 passed=1078 failed=2 skipped=0 inconclusive=0
- start-time=2026-11-09 04:50:28Z end-time=2026-11-09 04:50:31Z duration=3.1302048s
- failed:
  - `JudgeSettlementInvariantTests.BrokenHold_SettlesExactlyOneNote_NotTwo`: HOLD 中断で確定ノーツ数が 2 になった /   Expected: 1 /   But was:  2
  - `JudgeSettlementInvariantTests.FrameDrop_ManyNotesExpireAtOnce_EveryNoteSettledExactlyOnce`: GManager.settledNoteCount が noteCount と一致しない (HOLD 中断で二重に数えていないか) /   Expected: 12 /   But was:  13

## 負の制御 (PR #256 の S3)

`Judge.Update` が `JudgeStep` に渡す時刻を `GManager.step_time` から
`Time.time` に **一時的に** 変更して走らせた結果。
`Update_PassesGManagerStepTimeToJudgeStep` だけが赤になることを確認した
(＝このテストは実際に検証している。`Assert.Pass` だけの「緑」ではない)。

- source: `TestResults-neg.xml`
- total=1085 passed=1084 failed=1 skipped=0 inconclusive=0
- start-time=2026-11-09 14:44:34Z end-time=2026-11-09 14:44:39Z duration=4.4725214s
- failed:
  - `JudgeSettlementInvariantTests.Update_PassesGManagerStepTimeToJudgeStep`: Judge.Update が JudgeStep に渡した時刻が GManager.step_time と一致しない /   Expected: 123.5d +/- 9.9999997473787516E-05d /   But was:  0.0d

→ 変更は revert 済み。revert 後は 1085 / 1085 Pass (§「修正後 (緑)」)。

## 備考

- どちらの実行も `-quit` を付けずに走らせている (`-quit` を付けると 1 件も走らない)。
- `TestResults*.xml` / `unity-*.log` は `.gitignore` 対象のためリポジトリには残していない
  (このサマリだけを残す)。
