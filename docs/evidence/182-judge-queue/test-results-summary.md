# Issue #182 — EditMode テスト結果サマリ

`Assets/Tests/Editor` の EditMode スイート全件 (Unity 6000.5.6f1, `-batchmode -runTests -testPlatform editmode`)。

基準: `origin/main` @ `d8c1b39` 時点で 1073 件が Pass していた。
本作業で EditMode テストを 7 件追加し、1073 + 7 = 1080 件。

## 修正後 (緑)

### 修正後 (`JudgeScoreboard.Settled` で「確定したノーツ数」を数える修正入り)
- source: `TestResults.xml`
- total=1080 passed=1080 failed=0 skipped=0 inconclusive=0
- start-time=2026-11-09 04:51:31Z end-time=2026-11-09 04:51:36Z duration=5.6081686s
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
  - `JudgeSettlementInvariantTests.FrameDrop_ManyNotesExpireAtOnce_EveryNoteSettledExactlyOnce`: GManager.nextNotes - 1 が noteCount と一致しない (HOLD 中断で二重に数えていないか) /   Expected: 12 /   But was:  13

## 備考

- どちらの実行も `-quit` を付けずに走らせている (`-quit` を付けると 1 件も走らない)。
- `TestResults.xml` / `TestResults-red.xml` は `.gitignore` 対象のためリポジトリには残していない
  (このサマリだけを残す)。
