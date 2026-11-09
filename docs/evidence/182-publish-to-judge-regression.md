# Issue #182 — Web 公開譜面 → Unity 判定 の回帰テスト

## 追加したもの

- `Assets/Tests/Editor/PublishedChartJudgeRegressionTests.cs` (新規, EditMode テスト 7 本)
- `Assets/Tests/Editor/PublishedChartJudgeRegressionTests.cs.meta` (新規 GUID `d8e740eab9de4485ac78ce532df3c6f3`)

`Assets/Tests/Editor/FlashBeat.Tests.asmdef` は変更していない。既に
`FlashBeat` / `NoteEditor` / `NoteEditor.Editor` を参照しているため、
テスト側で新しい参照を追加する必要はなかった。

## 埋め込んだ「Web が実際に publish する JSON」

テスト内の `PublishedChartJson` は、Web エディタが書き出す形そのものである。

根拠 (Web 側):

| 対象 | 場所 |
|---|---|
| `ChartNote` のフィールド (`type` / `num` / `block` / `LPB` / `endNum`) | `web/src/lib/chart/types.ts:35-45` |
| `Chart` のフィールド (`name` / `maxBlock` / `BPM` / `offset` / `notes`) | `web/src/lib/chart/types.ts:64-70` |
| 書き出しは `JSON.stringify(chart)` (未知のトップレベルフィールドもそのまま) | `web/src/lib/chart/export.ts:11-12` |
| publish は `chart` をそのまま `JSON.stringify` して D1 に保存 | `web/routes/api/v1/admin/songs/[songId].ts:102` |
| 読み取りは `{songId, chartVersion, chart}` の包み | `web/routes/api/v1/_read-model.ts:118-127` |
| 契約 (同じ形) | `contracts/schemas/chart.v1.schema.json` |

Unity 側が実際に受け取るのは `GET /api/v1/charts/{songId}` の包みなので、
テストは `PublishedChartResponseJson` (`{songId, chartVersion, chart}`) として
埋め込み、その中身を production のパース関数に読ませている。

## どの production パーサ / 判定コードを通るか

すべて **production のコードそのもの** を通す (テスト側の再実装なし)。

| 段 | production の場所 |
|---|---|
| 譜面 JSON → `Data` | `CloudCatalog.ParseChartResponse` — `Assets/Game/Scripts/Songs/CloudCatalog.cs:159` (`JsonUtility.FromJson<ChartResponse>` → `Data` / `Note` は `Assets/Game/Scripts/Gameplay/NotesManager.cs:9,19`) |
| ノーツ時刻 | `NotesManager.NoteTimeSeconds` — `Assets/Game/Scripts/Gameplay/NotesManager.cs:74` (`60 / BPM * num / LPB`) |
| HOLD の終端 | `LongNote.EndSeconds` — `Assets/Game/Scripts/Gameplay/LongNote.cs:91` |
| レーン別・時刻順のキュー | `JudgeQueue` — `Assets/Game/Scripts/Gameplay/JudgeQueue.cs` (`Add` の時刻順挿入、`TryPress` の最近傍選択、`BeginHold` / `UpdateHold` / `TryPeekMiss`) |
| スコアの集計 | `JudgeScoreboard` — `Assets/Game/Scripts/Gameplay/JudgeScoreboard.cs` |
| 判定幅 | `LongNote.JudgeHead` / `PERFECT_WINDOW` / `GREAT_WINDOW` / `BAD_WINDOW` — `Assets/Game/Scripts/Gameplay/LongNote.cs:111` |

### なぜ `Judge` (MonoBehaviour) 自体ではなく `JudgeQueue` を駆動するのか

`Judge` は MonoBehaviour で `Input.GetKeyDown` と `GManager.step_time` に依存
するため、EditMode テストからは呼べない (既存の
`Assets/Tests/Editor/JudgeQueueTests.cs` も同じ理由で `JudgeQueue` を直接
駆動している)。そこで本テストは `Judge` の**呼び出し順序と判定式をそのまま
再現した**薄いラッパー `ChartRunner` を用意し、
production の純粋クラスだけを駆動する:

- `Press()` = `Judge.PressLane` (`Assets/Game/Scripts/Gameplay/Judge.cs:154-181`)
  … `TryPress` → `LongNote.JudgeHead` → `board.ApplyHead` → `BeginHold` / `ConsumeTap`
- `Update()` = `Judge.Update` の中段以降 (`Assets/Game/Scripts/Gameplay/Judge.cs:110-141`)
  … `queue.UpdateHold` → `board.DowngradeHoldToBad`、`queue.TryPeekMiss` → `board.ApplyMiss`

 本番コード (production code) の再構成・変更は一切していない。

## テストが固定している内容

1. `PublishedChart_ProductionParserReadsWebShape` — Web 公開のフィールド名と
   意味が Unity の `Data` / `Note` にそのまま読める。`endNum` (HOLD) も読める。
   未知のトップレベルフィールド (`lyrics`) が混ざっていても壊れない。
2. `PublishedChart_NoteTimesMatchChartFormula` — ノーツ時刻が
   `60 / BPM * num / LPB` と一致する (HOLD の終端は `endNum` から)。
3. `PublishedChart_QueueIsLaneWiseAndTimeOrdered` — 譜面ファイルが
   「レーン 0 の 1.0 秒 → レーン 0 の 0.0 秒」の順でも、キューは
   レーン別・時刻順 (0.0 秒が先) になる。他レーンは独立。
4. `PublishedChart_NoteOnOtherLaneDoesNotBlockJudgement` — 旧実装
   (全体の先頭ノーツだけを見る) なら阻害されていた他レーンの入力が
   成立する。
5. `PublishedChart_AllNotesPressedPerfectly_YieldsExpectedSequence` —
   全ノーツ Perfect、HOLD は「先頭 1 判定 + 終端で離す」で 1 ノーツ =
   1 判定、コンボ 5 / スコア 25。
6. `PublishedChart_HoldBrokenRelease_CountsAsExactlyOneBadJudgement` —
   途中で離すと先頭の Perfect が BAD に差し替わる (加点しない)。
   押し直しで救済されない。
7. `PublishedChart_UnpressedNoteBecomesMiss_OtherLanesUnaffected` —
   押されなかったノーツだけ Miss / コンボ切れ。保持中の HOLD は Miss にならない。
8. `PublishedChart_LatePressIsGreat_TooLateIsNotJudged` — 0.12 秒遅れは
   Great、0.30 秒遅れは判定にならない (Bad 幅の外)。

## 実行したこととその出力

### 1) runtime アセンブリ (変更なしだが確認)

```
$ script/compile-check.sh --project /Users/rebui/work/FlashBeat.w182 --assembly flashbeat
compile-check: 142 sources -> /Users/rebui/work/FlashBeat.w182/FlashBeat.compilecheck.csproj
Build succeeded.
```

### 2) テストアセンブリ

`script/compile-check.sh --assembly tests` の `tests` は
`Assets/Game/Tests` (V5/V6 Canvas 系) を対象とするasmdef で、
`Assets/Tests/Editor` (`FlashBeat.Tests`) は対象外。因此、`Assets/Tests/Editor`
全 66 ファイル (新規ファイルを含む) を `FlashBeat.Tests` と同じ参照構成
(FlashBeat / FlashBeat.Editor / NoteEditor / NoteEditor.Editor / UniRx /
UnityEngine.TestRunner / UnityEditor.TestRunner / nunit.framework.dll) で
`dotnet build` した:

```
$ dotnet build FlashBeat.compilecheck.tests.csproj -p:OutputPath=Temp/bin/tests2
Build succeeded.
```

(エラー 0 / 警告 6。警告はすべて既存の `JapaneseFontCoverageTests.cs` と
`V5UiWiringTests.cs` の未使用フィールド・廃止 API で、今回の追加分ではない。
この csproj / Temp 出力は gitignore 対象で、リポジトリには残していない。)

> 注: Unity の Test Runner (EditMode) 自体は **この環境では起動していない**
> (Unity Editor を起動しない方針のため)。上の `dotnet build` は型検査までで、
> 実行 (NUnit の PASS/FAIL) は Unity Editor 上で行う必要がある。

## Web の形と Unity の形で一致しなかった点

调查结果 (コード変更はしていない):

1. **`lyrics` は Unity の `Data` には存在しない。**
   Web は `chart.lyrics` (startTime / endTime / furigana / mondai / romaji) を
   載せる (`web/src/lib/chart/types.ts:53-61`,
   `contracts/schemas/chart.v1.schema.json` の `lyrics`) が、Unity の
   `Assets/Game/Scripts/Gameplay/NotesManager.cs:9-16` の `Data` には
   `lyrics` が無い (`name` / `maxBlock` / `BPM` / `offset` / `notes` のみ)。
   `JsonUtility` は未知フィールドを黙って捨てるので**壊れない**が、
   歌詞は読まれない。歌詞は別ルート (`GameSceneManager.LoadTextData`,
   `Assets/Game/Scripts/UI/GameSceneManager.cs:119`) が
   `NoteEditor.DTO.MusicDTO.EditData` 経由で読む設計になっており、
   **クラウド publish 経路の歌詞は現状読まれない**。
   今回のテストは「壊れない」ことだけを固定している (歌詞の反映は別課題)。
2. **`NoteEditor` 側の DTO は HOLD の `endNum` を持たない。**
   `Assets/NoteEditor/Scripts/DTO/MusicDTO.cs` の `Note` は
   `LPB` / `num` / `block` / `type` / `notes` (ネストした子ノーツ) であり、
   Web / 契約の `endNum` とは別表現。Unity ランタイム側の
   `Assets/Game/Scripts/Gameplay/NotesManager.cs` の `Note` は
   `endNum` を持つので、**ランタイムの読み込みは Web の形と一致**する。
   (NoteEditor の保存形式と Web の公開形式は別形式であり、
   `Assets/NoteEditor/IMPORT.md` のとおり互いに非互換。)
3. **`offset` は両側とも「保存するが判定には使わない」。**
   Web は値を保存するだけ (`web/src/lib/chart/notes.ts:62-68`)、
   Unity は `GManager.noteTiming` (設定値) を使う
   (`Assets/Game/Scripts/Gameplay/NotesManager.cs:145,155`)。
   本テストはオフセットを 0 として同じ式をなぞっている (全ノーツに
   一様に足る量なので判定順序・判定幅に影響しない)。
4. **`maxBlock` は Unity 側で使われていない。**
   Web は 1〜8 を書き (`contracts/schemas/chart.v1.schema.json` の
   `maxBlock`) 8 レーン固定 (`Assets/Game/Scripts/Gameplay/Judge.cs` の
   `LaneCount = 8`)。本テストの譜面は 8 レーンの範囲に収めている。

## 未検証の点

- **実ブラウザからの publish → 実 Unity プレイの通し確認は未実施。**
  本テストは「Web が書き出す形」を埋め込んだ静的レグレッションであり、
  実際の Web UI を通した publish、API (`PUT /api/v1/admin/songs/{songId}` →
  `GET /api/v1/charts/{songId}`) の往復、実機でのキー入力と判定表示は
  検証していない。実機検証は Supervisor の担当。
- **Unity Test Runner での実行未実施** (この環境では Unity を起動しないため)。
  型検査 (dotnet build) まで。
- `Judge` (MonoBehaviour) を実キーで駆動する PlayMode 相当のテストは
  今回の範囲外 (既存方針どおり)。
