# Issue #226: 曲終了の受入条件テスト (2026-11-10)

Issue #226 (`[Gameplay/P1] 曲終了をdurationだけで決めるため末尾ノーツが未判定で確定し、動画終端でもResultへ進めない`)
の最後の受入条件

> duration端点のTap/HOLD、正負noteTiming、動画長<duration、通常終端、フレーム落ちを含む回帰テストと
> 実機完走・ランキング送信テストを追加する。

を埋めるために追加した EditMode テストの記録。

## 結果

| 項目 | 値 |
|---|---|
| ブランチ | `w/w226-acceptance` (origin/main から作業ツリーを分岐) |
| 追加したテスト | 20 件 (5 ファイル + 共有リグ 1 ファイル) |
| EditMode 全体 | **1235** 件 / 失敗 **0** 件 (自分の追加分 20 件はすべて成功) |
| 本番コードの変更 | **なし** (`SongEndStateMachine.cs` も変更していない) |

## 追加したテスト

### 1. duration 端点の Tap / HOLD — `Assets/Tests/Editor/SongEndDurationEndpointTests.cs`

| テスト | 固定する契約 |
|---|---|
| `TapAtDurationEndpoint_IsJudged_AndTheSongWaitsForIt` | duration ちょうど (10.0s) の Tap が **判定される**。終端条件が duration-1 秒で成立しても、判定猶予が終わる前に曲が終わらない。判定期限は本番の `MusicManager.ComputeSettleDeadline` (= duration + BAD_WINDOW) |
| `TapAtDurationEndpoint_NotPressed_SettlesAsMissExactlyOnce` | 押さなかった端点 Tap も **1 回だけ** Miss として確定する (判定合計が noteCount に届く) |
| `HoldEndingAtDurationEndpoint_IsCleared_NotBroken` | 終端が duration 端点の HOLD は、終端ちょうどで離しても **中断 (BAD) にならない**。判定合計は noteCount のまま |
| `HoldEndingAtDurationEndpoint_HeldPastTheEnd_SettlesOnce` | 押し続けたまま終端を越えた HOLD は期限切れ (成功) として片付く (二重確定しない) |
| `HoldStartingAtDurationEndpoint_HeadIsJudged_AndTheSongWaitsForIt` | **開始** が duration 端点の HOLD の先頭も判定され、確定するまで曲が終わらない |

### 2. 動画長 < duration — `Assets/Tests/Editor/SongEndMediaShorterThanDurationTests.cs`

| テスト | 固定する契約 |
|---|---|
| `TheDurationAloneDoesNotEndTheSong` | duration だけ (60s / 120s) では終端条件が成立しない |
| `TheMediaEndIsRecorded_ByTheProductionPath` | 本番の `MusicManager.OnSongVideoLoopReached` が終端を記録する (メタデータが長い場合でも) |
| `TheSongCompletesAtTheMediaEnd_WithoutWaitingForTheCatalogDuration` | メディア終端で、確定済みなら壁時計を待たずに曲が終わる |
| `TheSongWaitsForTheTailNotesAfterTheMediaEnd` | メディア終端後も末尾ノーツの判定猶予を待つ |
| `AMediaEndWithUnplayedNotes_DoesNotLookLikeASuccessfulCompletion` | 未再生ノーツがある強制完了は **印 (ForcedByDeadline) が付く** = 成功扱いではない |

### 3. 通常終端 — `Assets/Tests/Editor/SongEndOrdinaryEndTests.cs`

| テスト | 固定する契約 |
|---|---|
| `TheOrdinaryEnd_CompletesWithEveryNoteJudged` | 全ノーツ判定 + 終端条件成立 → **期限切れ強制完了ではなく**普通に終わる。判定合計 = noteCount、スコア = 満点 |
| `TheOrdinaryEnd_WaitsForTheMediaEnd` | ノーツが全部確定していても、終端条件が成立するまでは終わらない |

### 4. フレーム落ち — `Assets/Tests/Editor/SongEndFrameDropAndStallTests.cs`

| テスト | 固定する契約 |
|---|---|
| `FrameDrop_SettlesEveryNoteExactlyOnce_AndEndsCleanly` | 判定時計が 0.6s → 3.5s と飛んでも **1 ノーツ = 1 判定**が保たれ、強制完了にならない |
| `AClockJumpAloneDoesNotEndTheSong` | フレーム落ちだけでは終端条件にならない (Playing のまま) |
| `AStalledMediaClock_FailsTheSong_InsteadOfCompletingIt` | 本番の `MusicManager.Update` がメディア時計 30 秒停止を検出 → **Failed** (成功偽装の完走にしない)。Result へ遷移しない |
| `AFailedSongNeverCompletesAfterwards` | Failed になった曲が後から Completed にならない |

### 5. ランキング送信 — `Assets/Tests/Editor/RankingSubmissionJudgementSumTests.cs`

API 側 (`web/routes/api/v1/_score-model.ts` の `scoreSemanticsDiagnostics`) は
「perfect + great + bad + miss == 譜面の noteCount」でなければ **422** で拒否する
(契約: `contracts/openapi.v1.yaml` / サーバのテスト: `web/tests/score-model.test.ts`)。
クライアント側には合計の判定が無く、判定数をそのまま送る。ここは **クライアントが
送るもの** を固定する (本番の挙動は変更していない)。

| テスト | 固定する契約 |
|---|---|
| `ACompleteRun_SumsToTheNoteCount_AndIsAccepted` | 全ノーツ判定済みのプレイは送信する判定数の合計 = noteCount。score は API の再計算と一致。201 → **Success** |
| `ARunWithUnsettledNotes_IsRejected_NotAccepted` | 未判定ノーツが残ったプレイは合計が noteCount に **届かない** (クライアントは水増ししない)。422 → **Rejected** (Success ではない) |
| `TheRejectedOutcomeIsDistinguishedFromOfflineAndDuplicate` | 422 / オフライン / 429 が区別される |
| `OnlyRemoteSongsWithAChartVersionAreSubmittable` | ローカル曲 / remoteId なし / 譜面版なしは送信対象外 |

## 実機 (macOS) へのランキング断言の追加

`Assets/Game/Scripts/Common/AutoTestV6SongEnd.cs` (PR #258 の実機完走ハーネス) に、
**ネットワーク不要**のランキング受理条件の断言を追加した:

- `the judgement total equals the chart note count (the ranking acceptance condition)`
  (perfect + great + bad + miss == 譜面のノーツ数 = API が受理する条件)
- `the ranking registration mode is resolved` (オンライン送信 / 端末内保存の別)

結果ファイル (`autotest-v6songend-result.txt`) にも `rankingMode=` と
`judgedMatchesChart=` を追記する。

API への **実送信** (201 / 422) はネットワークと認証が要るため行っていない。
サーバ側の規則そのものは `web/tests/score-model.test.ts` が担保している。

## 既に入っていたもの (重複実装しない)

| 受入条件 | 既存の担保 |
|---|---|
| 曲終了の状態機械そのもの | `Assets/Tests/Editor/SongEndStateMachineTests.cs` (17 件) |
| 正負 noteTiming | PR #272 `Assets/Tests/Editor/NoteTimingRegressionTests.cs` (7 件) |
| 実機完走 (macOS) | PR #258 `docs/evidence/233-song-end-harness.md` |

## 未検証 (open)

- **Windows 実機完走・ランキング送信** は未検証のまま (Issue #226 の受入条件のうち
  実機部分は macOS でのみ確認している。Windows は Issue #226 の open 項目として残る)。
- API への実ネットワーク送信 (201 / 422 の実応答) は未実施。
