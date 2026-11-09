using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;

/// <summary>
/// 「Web が公開した譜面 → Unity がその譜面を読み込んで判定する」の回帰テスト
/// (Issue #182 のループを閉じるもの)。
///
/// 公開側 (Web) が書き出す JSON はそのまま D1 の chart_versions に保存され
/// (web/routes/api/v1/admin/songs/[songId].ts:102 の
/// <c>JSON.stringify(request.chart)</c>)、読み取り側の
/// <c>GET /api/v1/charts/{songId}</c> は
/// <c>{songId, chartVersion, chart}</c> という包みを返す
/// (web/routes/api/v1/_read-model.ts:118-127 の buildChartResponse)。
/// Unity はその包みを <see cref="CloudCatalog.ParseChartResponse"/> で
/// 譜面 (Data) に落とし (Assets/Game/Scripts/Songs/CloudCatalog.cs:159)、
/// <see cref="NotesManager.NoteTimeSeconds"/> でノーツの時刻を計算し
/// (Assets/Game/Scripts/Gameplay/NotesManager.cs:74)、
/// <see cref="Judge"/> が判定キューへ積んで (Assets/Game/Scripts/Gameplay/Judge.cs:192)
/// <see cref="JudgeQueue"/> がレーン別・時刻順に判定する。
///
/// このテストが固定する要点:
///
///   1. Web 公開のフィールド名 (name / maxBlock / BPM / offset / notes[].type,
///      num, block, LPB, endNum) が Unity の <c>Data</c> / <c>Note</c>
///      (Assets/Game/Scripts/Gameplay/NotesManager.cs:9,19) にそのまま
///      読めること。未知のフィールド (lyrics / videoId) が混ざっていても
///      壊れないこと。
///   2. その譜面のノーツが「レーン別・時刻順」に判定されること
///      (譜面ファイルの並び順と時刻順が違っても正しいノーツに当たる)。
///   3. HOLD が「先頭 1 判定 + 終端での離し」で 1 ノーツ = 1 判定になること。
///   4. 判定の並び順 (Perfect / Great / Bad / Miss / コンボ / スコア) が
///      期待どおりであること。
///
/// <see cref="Judge"/> 自体は MonoBehaviour で Input と GManager.step_time に
/// 依存するため EditMode テストからは呼べない。よってこのテストは
/// <see cref="Judge"/> の呼び出し順序 (PressLane → UpdateHold → TryPeekMiss)
/// をそのまま再現して、公開可能な純粋クラス
/// (<see cref="JudgeQueue"/> / <see cref="JudgeScoreboard"/> /
/// <see cref="LongNote"/>) を駆動する。
/// </summary>
public class PublishedChartJudgeRegressionTests
{
    private const int Lanes = 8;

    /// <summary>
    /// Web が公開した譜面 1 曲分 (実際にpublish されたのと同じ形)。
    ///
    /// 根拠:
    ///   - 譜面の型: web/src/lib/chart/types.ts:35-45 (ChartNote:
    ///     type / num / block / LPB / endNum)、同 64-70 (Chart:
    ///     name / maxBlock / BPM / offset / notes)。
    ///   - 書き出し: web/src/lib/chart/export.ts:11-12 の exportChartJson は
    ///     <c>JSON.stringify(chart)</c> そのまま (未知のトップレベル
    ///     フィールドもそのまま持つ)。
    ///   - 契約: contracts/schemas/chart.v1.schema.json。
    ///
    /// ポイント: notes[] の並びは Web エディタの編集順であり、時刻順とは
    /// 限らない。ここではレーン 0 のノーツが「後の時刻 → 前の時刻」の順で
    /// 並んでいる (num 8 のあとに num 0)。これが Issue #182 の対象そのもの。
    /// BPM 120 / LPB 4 なので 1 行 = 0.125 秒 (num 0 = 0.0s, num 4 = 0.5s,
    /// num 8 = 1.0s, num 12 = 1.5s, num 16 = 2.0s)。
    /// </summary>
    private const string PublishedChartJson =
        "{\"name\":\"Web公開テスト曲\",\"maxBlock\":8,\"BPM\":120,\"offset\":0," +
        "\"notes\":[" +
        "{\"type\":1,\"num\":8,\"block\":0,\"LPB\":4}," +
        "{\"type\":1,\"num\":0,\"block\":0,\"LPB\":4}," +
        "{\"type\":1,\"num\":4,\"block\":2,\"LPB\":4}," +
        "{\"type\":2,\"num\":4,\"endNum\":16,\"block\":3,\"LPB\":4}," +
        "{\"type\":1,\"num\":12,\"block\":5,\"LPB\":4}" +
        "]," +
        "\"lyrics\":{\"startTime\":[],\"endTime\":[],\"furigana\":[],\"mondai\":[],\"romaji\":[]}}";

    /// <summary>
    /// <c>GET /api/v1/charts/{songId}?version=N</c> のレスポンス (読み取り側の
    /// 包みを再現したもの。包む譜面は上で定義した公開 JSON そのもの)。
    /// </summary>
    private const string PublishedChartResponseJson =
        "{\"songId\":\"web-published-test\",\"chartVersion\":1,\"chart\":" + PublishedChartJson + "}";

    /// <summary>Offsets shared by every node of the chart (seconds).</summary>
    private const float Lane0First = 0f;
    private const float Lane0Second = 1f;
    private const float Lane2Tap = 0.5f;
    private const float Lane3HoldStart = 0.5f;
    private const float Lane3HoldEnd = 2f;
    private const float Lane5Tap = 1.5f;

    // ------------------------------------------------------------------
    // 1) Web が公開した JSON を Unity の読み込み口で読む
    // ------------------------------------------------------------------

    [Test]
    public void PublishedChart_ProductionParserReadsWebShape()
    {
        // 本物の読み込み口: CloudCatalog.ParseChartResponse (CloudCatalog.cs:159)。
        // NotesManager.ParseChart (NotesManager.cs:232) もクラウド取得の譜面
        // ではこの同じ関数を呼んでいる。
        Data chart = CloudCatalog.ParseChartResponse(PublishedChartResponseJson);

        Assert.IsNotNull(chart, "Web 公開の譜面が読めない");
        Assert.AreEqual("Web公開テスト曲", chart.name);
        Assert.AreEqual(8, chart.maxBlock);
        Assert.AreEqual(120, chart.BPM);
        Assert.AreEqual(0, chart.offset);
        Assert.AreEqual(5, chart.notes.Length);

        // notes[] の各フィールドが Web の名前 (type / num / block / LPB) のまま
        // 読まれていること。
        Note first = chart.notes[0];
        Assert.AreEqual(LongNote.TYPE_TAP, first.type);
        Assert.AreEqual(8, first.num);
        Assert.AreEqual(0, first.block);
        Assert.AreEqual(4, first.LPB);

        // HOLD の endNum も読まれている (Web は type 2 に endNum を書く)。
        Note hold = chart.notes[3];
        Assert.AreEqual(LongNote.TYPE_HOLD, hold.type);
        Assert.AreEqual(4, hold.num);
        Assert.AreEqual(16, hold.endNum);
        Assert.AreEqual(3, hold.block);

        // Web は未知のトップレベルフィールド (lyrics) をそのまま載せる。
        // Unity の Data には lyrics が無いので捨てられるが、壊れてはいない。
        Assert.IsNotNull(chart.notes);
    }

    [Test]
    public void PublishedChart_NoteTimesMatchChartFormula()
    {
        Data chart = CloudCatalog.ParseChartResponse(PublishedChartResponseJson);
        Assert.IsNotNull(chart);

        // 時刻は NotesManager.NoteTimeSeconds (NotesManager.cs:74) の式:
        // 60 / BPM * num / LPB。
        Assert.AreEqual(Lane0Second, NotesManager.NoteTimeSeconds(chart.BPM, chart.notes[0]), 0.0001f); // num 8
        Assert.AreEqual(Lane0First, NotesManager.NoteTimeSeconds(chart.BPM, chart.notes[1]), 0.0001f);  // num 0
        Assert.AreEqual(Lane2Tap, NotesManager.NoteTimeSeconds(chart.BPM, chart.notes[2]), 0.0001f);    // num 4
        Assert.AreEqual(Lane3HoldStart, NotesManager.NoteTimeSeconds(chart.BPM, chart.notes[3]), 0.0001f); // num 4
        Assert.AreEqual(Lane5Tap, NotesManager.NoteTimeSeconds(chart.BPM, chart.notes[4]), 0.0001f);   // num 12

        // HOLD の終端は endNum から (LongNote.EndSeconds, LongNote.cs:91)。
        Assert.AreEqual(Lane3HoldEnd, LongNote.EndSeconds(chart.BPM, chart.notes[3]), 0.0001f);
    }

    // ------------------------------------------------------------------
    // 2) 譜面 → 判定キュー (Judge.EnsureQueue と同じ組み方)
    // ------------------------------------------------------------------

    [Test]
    public void PublishedChart_QueueIsLaneWiseAndTimeOrdered()
    {
        ChartRunner runner = BuildRunner();

        // 譜面ファイルの並び順は「レーン 0 の 1.0 秒 → レーン 0 の 0.0 秒」だが、
        // キューはレーン別・時刻順に並んでいる。
        // 0.0 秒のノーツ (ファイル index 1) が先に来る。
        Assert.IsTrue(runner.Queue.TryPress(0, Lane0First, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry hit, out float lag));
        Assert.AreEqual(1, hit.SourceIndex, "0.0 秒のノーツが先に来るはず");
        Assert.AreEqual(0f, lag, 0.0001f);
        runner.Queue.ConsumeTap(hit);

        Assert.IsTrue(runner.Queue.TryPress(0, Lane0Second, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry second, out _));
        Assert.AreEqual(0, second.SourceIndex, "1.0 秒のノーツが次に来るはず");
        runner.Queue.ConsumeTap(second);

        // 他レーンのノーツは自分のレーンで独立に並ぶ。
        Assert.AreEqual(1, runner.Queue.PendingCount(2));
        Assert.AreEqual(1, runner.Queue.PendingCount(3));
        Assert.AreEqual(1, runner.Queue.PendingCount(5));
        Assert.AreEqual(0, runner.Queue.PendingCount(1));
        Assert.AreEqual(3, runner.Queue.TotalPendingCount);
    }

    [Test]
    public void PublishedChart_NoteOnOtherLaneDoesNotBlockJudgement()
    {
        ChartRunner runner = BuildRunner();

        // ファイル先頭はレーン 0 の 1.0 秒のノーツ。old 実装 (全体の先頭だけ
        // を見る) だと 0.5 秒のレーン 2 の入力がレーン 0 の未処理ノーツに
        // 阻害されていた。レーン別キューでは成立する。
        Assert.IsTrue(runner.Queue.TryPress(2, Lane2Tap, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry tap, out _));
        Assert.AreEqual(2, tap.SourceIndex);
        Assert.AreEqual(2, tap.Lane);
        runner.Queue.ConsumeTap(tap);
    }

    // ------------------------------------------------------------------
    // 3) + 4) 判定の並び順 (Perfect / Great / Bad / Miss / コンボ / スコア)
    // ------------------------------------------------------------------

    [Test]
    public void PublishedChart_AllNotesPressedPerfectly_YieldsExpectedSequence()
    {
        ChartRunner runner = BuildRunner();

        // 同時押し: レーン 0 (0.0 秒) とレーン 3 の HOLD 開始 (0.5 秒) は
        // 別レーンなので独立に判定される。
        runner.Press(0, Lane0First);
        runner.Press(3, Lane3HoldStart);
        runner.Press(2, Lane2Tap);
        runner.Press(0, Lane0Second);
        runner.Press(5, Lane5Tap);
        runner.Release(3, Lane3HoldEnd);
        runner.Update(Lane3HoldEnd);

        JudgeScoreboard board = runner.Board;
        Assert.AreEqual(5, board.Perfect, "5 ノーツ全部 Perfect");
        Assert.AreEqual(0, board.Great);
        Assert.AreEqual(0, board.Bad);
        Assert.AreEqual(0, board.Miss);
        Assert.AreEqual(5, board.Combo);
        Assert.AreEqual(5, board.MaxCombo);
        Assert.AreEqual(5 * LongNote.SCORE_PERFECT, board.RatioScore, 0.0001f);

        // 1 ノーツ = 1 判定 (HOLD の終端で離しても加点しない)。
        Assert.AreEqual(5, board.Perfect + board.Great + board.Bad + board.Miss);
        Assert.AreEqual(0, runner.Queue.TotalPendingCount);
        Assert.IsFalse(runner.Queue.IsHolding(3), "HOLD は終端で離した時点で終了している");
    }

    [Test]
    public void PublishedChart_HoldBrokenRelease_CountsAsExactlyOneBadJudgement()
    {
        ChartRunner runner = BuildRunner();

        runner.Press(0, Lane0First);
        runner.Press(2, Lane2Tap);
        runner.Press(3, Lane3HoldStart);      // HOLD の先頭 (Perfect)
        runner.Press(0, Lane0Second);
        runner.Release(3, 1f);                // 終端 2.0 秒より前に離した (中断)
        runner.Update(1f);
        runner.Press(5, Lane5Tap);

        JudgeScoreboard board = runner.Board;
        Assert.AreEqual(4, board.Perfect, "HOLD の先頭の Perfect だけが BAD に差し替わる");
        Assert.AreEqual(1, board.Bad);
        Assert.AreEqual(0, board.Great);
        Assert.AreEqual(0, board.Miss);
        Assert.AreEqual(5, board.Combo, "中断でコンボは消えない (spec 164 R4)");
        Assert.AreEqual(4 * LongNote.SCORE_PERFECT + LongNote.SCORE_BAD, board.RatioScore, 0.0001f);

        // 押し直しで救済されない (1 ノーツ = 1 判定)。
        runner.Press(3, 1.1f);
        Assert.AreEqual(5, board.Perfect + board.Great + board.Bad + board.Miss);
    }

    [Test]
    public void PublishedChart_UnpressedNoteBecomesMiss_OtherLanesUnaffected()
    {
        ChartRunner runner = BuildRunner();

        runner.Press(0, Lane0First);
        runner.Press(2, Lane2Tap);
        runner.Press(3, Lane3HoldStart);
        runner.Press(0, Lane0Second);
        // レーン 5 (1.5 秒) は押さない。

        runner.Update(Lane5Tap + LongNote.BAD_WINDOW + 0.01f);

        JudgeScoreboard board = runner.Board;
        Assert.AreEqual(4, board.Perfect);
        Assert.AreEqual(1, board.Miss, "レーン 5 のノーツだけ Miss");
        Assert.AreEqual(0, board.Combo, "Miss でコンボは切れる");
        Assert.AreEqual(4, board.MaxCombo);
    }

    [Test]
    public void PublishedChart_LatePressIsGreat_TooLateIsNotJudged()
    {
        ChartRunner runner = BuildRunner();

        // 0.12 秒遅れ → Great 幅 (0.15 秒) の内側。
        runner.Press(2, Lane2Tap + 0.12f);
        Assert.AreEqual(0, runner.Board.Perfect);
        Assert.AreEqual(1, runner.Board.Great);

        // 0.3 秒遅れ → Bad 幅 (0.20 秒) の外側なので判定にならない。
        runner.Press(5, Lane5Tap + 0.3f);
        Assert.AreEqual(0, runner.Board.Perfect);
        Assert.AreEqual(1, runner.Board.Great);
        Assert.AreEqual(0, runner.Board.Bad);
    }

    // ------------------------------------------------------------------
    // ヘルパー
    // ------------------------------------------------------------------

    /// <summary>
    /// Web 公開の譜面を、NotesManager.LoadRoutine (NotesManager.cs:143) と
    /// Judge.EnsureQueue (Judge.cs:192) と同じ作り方で判定キューへ入れる。
    /// </summary>
    private static ChartRunner BuildRunner()
    {
        Data chart = CloudCatalog.ParseChartResponse(PublishedChartResponseJson);
        Assert.IsNotNull(chart, "Web 公開の譜面が読めない");

        ChartRunner runner = new ChartRunner();

        // NotesManager.LoadRoutine: ノーツの時刻と、ロングノーツの終端。
        // 本番は GManager.noteTiming * 0.01f (設定値) を開始と終端の両方に
        // 加算する (NotesManager.cs:145,155)。全ノーツに同じ量だけ足す
        // 一様な移動なので、レーン別の並び順と判定幅は変わらない。ここでは
        // 0 として同じ式をなぞっている (押し時刻も同じ式で求めるため)。
        for (int i = 0; i < chart.notes.Length; i++)
        {
            Note note = chart.notes[i];
            float time = NotesManager.NoteTimeSeconds(chart.BPM, note);
            float endTime = 0f;
            if (note.type == LongNote.TYPE_HOLD)
            {
                endTime = LongNote.EndSeconds(chart.BPM, note);
                if (endTime <= time) endTime = time + (60f / chart.BPM);
            }

            // Judge.EnsureQueue: 譜面ファイルの順に積む。並び替えは
            // JudgeQueue の内部 (レーン別・時刻順) が行う。
            runner.Queue.Add(new JudgeQueue.NoteEntry
            {
                Time = time,
                EndTime = endTime,
                Type = note.type,
                Lane = note.block,
                SourceIndex = i,
            });
        }

        return runner;
    }

    /// <summary>
    /// Judge (Assets/Game/Scripts/Gameplay/Judge.cs) の判定経路を
    /// EditMode テストで再現する薄いラッパー。
    ///
    ///   - Press / Release: キー入力 (Judge.PressLane, Judge.cs:154)。
    ///   - Update: ロングノーツの保持状態 (Judge.cs:112) と
    ///     時刻切れの Miss (Judge.cs:133) の処理。
    ///
    /// 本物の Judge は MonoBehaviour で Input と GManager.step_time に依存
    /// するため EditMode からは呼べない。呼び出し順序と判定式は
    /// production とまったく同じ。
    /// </summary>
    private sealed class ChartRunner
    {
        public readonly JudgeQueue Queue = new JudgeQueue(Lanes);
        public readonly JudgeScoreboard Board = new JudgeScoreboard();

        private readonly bool[] keyDown = new bool[Lanes];
        private readonly LongNote.Judgement[] holdHead = new LongNote.Judgement[Lanes];

        public void Press(int lane, float mediaTime)
        {
            keyDown[lane] = true;
            if (!Queue.TryPress(lane, mediaTime, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry hit, out float lag)) return;

            LongNote.Judgement judgement = LongNote.JudgeHead(lag);
            if (judgement == LongNote.Judgement.None) return;

            Board.ApplyHead(judgement);

            if (hit.Type == LongNote.TYPE_HOLD)
            {
                holdHead[lane] = judgement;
                Queue.BeginHold(hit);
            }
            else
            {
                Queue.ConsumeTap(hit);
            }
        }

        public void Release(int lane, float mediaTime)
        {
            keyDown[lane] = false;
        }

        public void Update(float mediaTime)
        {
            // Judge.cs:110-128 と同じ順序。
            for (int lane = 0; lane < Lanes; lane++)
            {
                JudgeQueue.HoldResult result = Queue.UpdateHold(lane, mediaTime, keyDown[lane], LongNote.GREAT_WINDOW, out JudgeQueue.NoteEntry holdEnd);
                if (result == JudgeQueue.HoldResult.Broken)
                {
                    Board.DowngradeHoldToBad(holdHead[lane]);
                }
            }

            // Judge.cs:131-141 と同じ順序。
            for (int lane = 0; lane < Lanes; lane++)
            {
                if (Queue.TryPeekMiss(lane, mediaTime, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry missed))
                {
                    Queue.Remove(missed);
                    Board.ApplyMiss();
                }
            }
        }
    }
}
