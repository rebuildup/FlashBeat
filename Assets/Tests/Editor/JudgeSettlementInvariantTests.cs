using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;

/// <summary>
/// Issue #182 の受入条件「最終判定数 = noteCount」「スコア API との整合」を
/// **実機と同じ判定経路**で検証する EditMode テスト。
///
/// 既存の <see cref="PublishedChartJudgeRegressionTests"/> は
/// <see cref="Judge"/> の呼び出し順序をテスト側で再現しているため、
/// <see cref="Judge"/> 本体が壊れていても緑になる (false green)。
/// このテストは <see cref="Judge.JudgeStep"/> を直接呼ぶので、
/// 本体 (キュー組み立て → 押下 → HOLD 保持 → Miss → スコア反映) が
/// 通った経路そのものを検証する。
///
/// 検証する不変条件:
///
///   1. **最終判定数 = noteCount**:
///      <c>Perfect + Great + Bad + Miss == 譜面のノーツ数</c>。
///      さらに「押下で確定した数 == 押下了回数」「Miss == 押されなかった数」
///      も見て、同一ノーツの二重判定や取りこぼしがないことを確認する。
///   2. **スコア API との整合**:
///      <c>GManager.maxScore == noteCount x 5</c>、
///      <c>GManager.ratioScore == 5*perfect + 3*great + 1*bad</c>、
///      <c>GManager.score == Judge.ComputeScore(ratioScore, maxScore)</c>、
///      さらに曲終了判定に使う <c>GManager.settledNoteCount</c> が
///      「確定したノーツ数」と一致すること (HOLD 中断の差し替えで二重に
///      数えないこと)。
///   3. 譜面は Web 公開と同じ形 (CloudCatalog.ParseChartResponse) で読み込み、
///      異数のレーン・異数の LPB・HOLD 混在・編集順と時刻順の不一致を含む。
/// </summary>
public class JudgeSettlementInvariantTests
{
    private const int Lanes = 8;
    private const int BPM = 120;

    /// <summary>
    /// Web が公開する形 (contracts/schemas/chart.v1.schema.json) の譜面。
    ///
    /// 特徴:
    ///   - 8 レーンすべてを使う (異数のレーン)。
    ///   - LPB が 4 と 8 が混在する (1 行 = 0.125 秒 / 0.0625 秒)。
    ///   - HOLD (type 2) が 2 本混じる (レーン 3: 0.5→2.0 秒、レーン 1: 0.5→1.5 秒)。
    ///   - 同じレーンのノーツが「後の時刻 → 前の時刻」の順で並ぶ
    ///     (レーン 0: 2.0 秒 → 0.0 秒 → 0.375 秒、レーン 2: 3.0 秒 → 1.0 秒)。
    ///     これが Issue #182 の対象そのもの (編集順 != 時刻順)。
    ///
    /// ノーツ数 = 12。
    /// </summary>
    private const string MixedChartJson =
        "{\"name\":\"182混在譜面\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[" +
        "{\"type\":1,\"num\":16,\"block\":0,\"LPB\":4}," +
        "{\"type\":1,\"num\":0,\"block\":0,\"LPB\":4}," +
        "{\"type\":1,\"num\":12,\"block\":5,\"LPB\":4}," +
        "{\"type\":1,\"num\":24,\"block\":2,\"LPB\":4}," +
        "{\"type\":1,\"num\":4,\"block\":7,\"LPB\":8}," +
        "{\"type\":1,\"num\":8,\"block\":2,\"LPB\":4}," +
        "{\"type\":2,\"num\":4,\"endNum\":16,\"block\":3,\"LPB\":4}," +
        "{\"type\":1,\"num\":2,\"block\":4,\"LPB\":4}," +
        "{\"type\":2,\"num\":8,\"endNum\":24,\"block\":1,\"LPB\":8}," +
        "{\"type\":1,\"num\":16,\"block\":6,\"LPB\":8}," +
        "{\"type\":1,\"num\":6,\"block\":0,\"LPB\":8}," +
        "{\"type\":1,\"num\":20,\"block\":5,\"LPB\":4}" +
        "]}";

    private const int MixedChartNoteCount = 12;

    /// <summary>同じ時刻・同じレーンに 2 ノーツある譜面 (同時押し)。</summary>
    private const string SameLaneSameTimeChartJson =
        "{\"name\":\"同時押し譜面\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[" +
        "{\"type\":1,\"num\":4,\"block\":0,\"LPB\":4}," +
        "{\"type\":1,\"num\":4,\"block\":0,\"LPB\":4}," +
        "{\"type\":1,\"num\":8,\"block\":3,\"LPB\":4}" +
        "]}";

    private const int SameLaneSameTimeNoteCount = 3;

    /// <summary>HOLD 1 本だけの譜面 (中断の二重計数を単独で見るため)。</summary>
    private const string HoldOnlyChartJson =
        "{\"name\":\"HOLDのみ譜面\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[" +
        "{\"type\":2,\"num\":4,\"endNum\":16,\"block\":3,\"LPB\":4}" +
        "]}";

    private const int HoldOnlyNoteCount = 1;

    private GameObject rigRoot;
    private Judge judge;
    private NotesManager notesManager;

    [SetUp]
    public void SetUp()
    {
        GManager.perfect = 0;
        GManager.great = 0;
        GManager.bad = 0;
        GManager.miss = 0;
        GManager.score = 0;
        GManager.combo = 0;
        GManager.maxCombo = 0;
        GManager.ratioScore = 0f;
        GManager.maxScore = 0f;
        // MusicManager は曲開始時に nextNotes = 1 にする (MusicManager.cs:172)。
        //  Judge は「1 + 確定ノーツ数」を書くので、ここでも 1 から始める。
        GManager.nextNotes = 1;
        GManager.settledNoteCount = 0;
        GManager.noteTiming = 0;
    }

    [TearDown]
    public void TearDown()
    {
        // 本番のノーツ生成 (LoadRoutine) で作ったインスタンスを先に片付ける。
        if (notesManager != null)
        {
            for (int i = 0; i < notesManager.NotesObj.Count; i++)
            {
                if (notesManager.NotesObj[i] != null)
                {
                    Object.DestroyImmediate(notesManager.NotesObj[i]);
                }
            }
        }

        if (rigRoot != null)
        {
            Object.DestroyImmediate(rigRoot);
            rigRoot = null;
        }
        judge = null;
        notesManager = null;

        // 固定したプレイセッションを破棄する (他テストに持ち越さない)。
        PlaySessionSnapshot.Clear();

        GManager.perfect = 0;
        GManager.great = 0;
        GManager.bad = 0;
        GManager.miss = 0;
        GManager.score = 0;
        GManager.combo = 0;
        GManager.maxCombo = 0;
        GManager.ratioScore = 0f;
        GManager.maxScore = 0f;
        GManager.nextNotes = 0;
        GManager.settledNoteCount = 0;
        GManager.noteTiming = 5;
    }

    // ------------------------------------------------------------------
    // 1) 最終判定数 = noteCount
    // ------------------------------------------------------------------

    /// <summary>
    /// 全ノーツをちょうど 1 回ずつ判定する譜面。
    /// 判定数 (Perfect+Great+Bad+Miss) が noteCount と一致し、
    /// 譜面の並び順 (編集順) が時刻順と違うことの影響を受けないことを見る。
    /// </summary>
    [Test]
    public void AllNotesPressed_ExactlyOnce_TotalJudgementsEqualNoteCount()
    {
        Rig rig = BuildRig(MixedChartJson, MixedChartNoteCount);
        Player player = new Player(rig);

        // 全ノーツを押す。押し順は問わない (Issue #182: 編集順と時刻順が
        // 違っても判定は時刻順が行われる)。
        foreach (int index in rig.NoteIndices)
        {
            player.SchedulePress(rig.NoteTime[index], rig.NoteLane[index]);
            if (rig.NoteType[index] == LongNote.TYPE_HOLD)
            {
                player.ScheduleRelease(rig.NoteEndTime[index], rig.NoteLane[index]);
            }
        }

        player.Run(Frames(0f, 4f, 0.05f));

        // 1 ノーツ = 1 判定。
        Assert.AreEqual(MixedChartNoteCount, GManager.perfect + GManager.great + GManager.bad + GManager.miss,
            "最終判定数が noteCount と一致しない");
        Assert.AreEqual(MixedChartNoteCount, GManager.perfect, "全ノーツ Perfect");
        Assert.AreEqual(0, GManager.great);
        Assert.AreEqual(0, GManager.bad);
        Assert.AreEqual(0, GManager.miss);
        Assert.AreEqual(MixedChartNoteCount, GManager.settledNoteCount, "確定ノーツ数が noteCount と一致しない");

        // キューは空 (すべてのノーツが 1 回ずつ消費された)。
        Assert.AreEqual(0, rig.Queue.TotalPendingCount, "未判定のノーツが残っている");
        for (int lane = 0; lane < Lanes; lane++)
        {
            Assert.IsFalse(rig.Queue.IsHolding(lane), $"lane {lane} が保持中のまま");
        }
    }

    /// <summary>
    /// 押さないノーツは Miss になり、**合計は noteCount のまま**。
    /// 同時に複数レーンのノーツが時刻切れになる (フレーム落ち相当) ケース。
    /// </summary>
    [Test]
    public void FrameDrop_ManyNotesExpireAtOnce_EveryNoteSettledExactlyOnce()
    {
        Rig rig = BuildRig(MixedChartJson, MixedChartNoteCount);
        Player player = new Player(rig);

        // 押すのは 4 本 (うち 1 本が HOLD)。残りは Miss になる。
        // (1.5 秒のノーツは 1.0 秒 → 3.5 秒のフレーム落ちで入力そのものが
        //  失われるので、意図的に押さない側に入れる)
        int[] pressed = { 1, 4, 6, 5 };
        foreach (int index in pressed)
        {
            player.SchedulePress(rig.NoteTime[index], rig.NoteLane[index]);
        }
        // HOLD (index 6) は 2.0 秒の終端より早く (1.0 秒) 離して中断させる。
        player.ScheduleRelease(1.0f, rig.NoteLane[6]);

        // 0.6 秒まで通常 Frames → 1.0 秒 (HOLD 中断) → 3.5 秒へ大きく飛ばす
        // (フレーム落ち相当。複数のノーツが一度に時刻切れになる) → 最後まで。
        List<float> frames = new List<float>();
        frames.AddRange(Frames(0f, 0.6f, 0.05f));
        frames.Add(1.0f);
        frames.Add(3.5f);
        frames.AddRange(Frames(3.55f, 4f, 0.05f));
        player.Run(frames);

        // 判定数の合計は noteCount のまま (二重判定も取りこぼしも無い)。
        Assert.AreEqual(MixedChartNoteCount, GManager.perfect + GManager.great + GManager.bad + GManager.miss,
            "最終判定数が noteCount と一致しない");

        // 押した 4 本: HOLD の先頭は Perfect → 中断で BAD に差し替え (差し替えで数は増えない)。
        Assert.AreEqual(3, GManager.perfect, "HOLD の先頭以外が Perfect");
        Assert.AreEqual(1, GManager.bad, "中断した HOLD だけが BAD");
        Assert.AreEqual(0, GManager.great);

        // 押さなかった 8 本が Miss。
        Assert.AreEqual(MixedChartNoteCount - pressed.Length, GManager.miss, "未押しのノーツが Miss になっていない");

        // 「確定したノーツ数」(曲終了判定に使われる値) も noteCount。
        Assert.AreEqual(MixedChartNoteCount, GManager.settledNoteCount,
            "GManager.settledNoteCount が noteCount と一致しない (HOLD 中断で二重に数えていないか)");

        Assert.AreEqual(0, rig.Queue.TotalPendingCount, "未判定のノーツが残っている");
    }

    /// <summary>
    /// HOLD を途中で離して BAD に差し替えたとき、**確定ノーツ数は 1 だけ増える**。
    /// (以前は判定イベント数を数えていたので 2 増えていた = Issue #182 違反)
    /// </summary>
    [Test]
    public void BrokenHold_SettlesExactlyOneNote_NotTwo()
    {
        Rig rig = BuildRig(HoldOnlyChartJson, HoldOnlyNoteCount);
        Player player = new Player(rig);

        // HOLD の先頭だけ押して、途中で離す (終端 2.0 秒より前の 1.0 秒)。
        player.SchedulePress(rig.NoteTime[0], rig.NoteLane[0]);
        player.ScheduleRelease(1.0f, rig.NoteLane[0]);

        player.Run(Frames(0f, 1.2f, 0.05f));

        // 確定したノーツは HOLD の 1 本だけ。
        Assert.AreEqual(1, GManager.settledNoteCount, "HOLD 中断で確定ノーツ数が 2 になった");
        Assert.AreEqual(1, GManager.perfect + GManager.great + GManager.bad + GManager.miss);
        Assert.AreEqual(1, GManager.bad, "中断した HOLD は BAD");
        Assert.AreEqual(0, GManager.perfect);
    }

    /// <summary>
    /// 同じ時刻・同じレーンに 2 ノーツあるとき、2 回の入力で 2 ノーツが確定する。
    /// (同時押し / 同時ノーツ: 1 入力 = 1 ノーツ)
    ///
    /// 同じキーの「押しっぱなし」は 1 フレームで 1 エッジだけなので、
    /// 2 つ目のノーツは別のフレームでの入力 (0.05 秒後) で拾う。
    /// </summary>
    [Test]
    public void SameLaneSameTime_TwoPresses_SettleBothNotes()
    {
        Rig rig = BuildRig(SameLaneSameTimeChartJson, SameLaneSameTimeNoteCount);
        Player player = new Player(rig);

        float t0 = rig.NoteTime[0];
        float t2 = rig.NoteTime[2];
        player.SchedulePress(t0, rig.NoteLane[0]);
        player.SchedulePress(t0 + 0.05f, rig.NoteLane[0]);   // 同じ時刻・同じレーンにもう 1 回
        player.SchedulePress(t2, rig.NoteLane[2]);            // 別レーン

        player.Run(Frames(0f, 2f, 0.05f));

        Assert.AreEqual(SameLaneSameTimeNoteCount, GManager.perfect + GManager.great + GManager.bad + GManager.miss,
            "同時ノーツが一度だけ確定していない");
        Assert.AreEqual(SameLaneSameTimeNoteCount, GManager.perfect);
        Assert.AreEqual(SameLaneSameTimeNoteCount, GManager.settledNoteCount);
        Assert.AreEqual(0, rig.Queue.TotalPendingCount);
    }

    /// <summary>
    /// HOLD を保持している最中に、他レーンの Tap が判定されること
    /// (受入条件「HOLD 保持中の他レーン Tap」)。
    /// </summary>
    [Test]
    public void TapOnOtherLane_WhileHolding_IsJudged()
    {
        Rig rig = BuildRig(MixedChartJson, MixedChartNoteCount);
        Player player = new Player(rig);

        // HOLD (index 6, レーン 3, 0.5→2.0 秒) の先頭を押す。
        player.SchedulePress(rig.NoteTime[6], rig.NoteLane[6]);
        // 保持中 (1.0 秒) に、他レーン (index 5, レーン 2) の Tap を押す。
        player.SchedulePress(rig.NoteTime[5], rig.NoteLane[5]);

        // 終端 (2.0 秒) にはまだ到達しない範囲まで回す。
        player.Run(Frames(0f, 1.4f, 0.05f));

        // HOLD の先頭と他レーンの Tap が両方とも Perfect で確定している。
        Assert.AreEqual(2, GManager.perfect, "HOLD 保持中の他レーン Tap が判定されていない");
        Assert.IsTrue(rig.Queue.IsHolding(3), "HOLD の保持が継続していない");
        // レーン 2 には 3.0 秒のノーツだけが残る (1.0 秒の Tap は消費済み)。
        Assert.AreEqual(1, rig.Queue.PendingCount(2), "他レーンの Tap が消費されていない");
        // この時刻 (1.4 秒) までに時刻切れになった未押しのノーツ:
        // 0.0 秒 / 0.25 秒 x2 / 0.375 秒 / 0.5 秒 / 1.0 秒 の 6 本。
        // 確定ノーツ数 = 押下 2 + Miss 6 = 8、残存は 4 本 (2.0 / 1.5 / 3.0 / 2.5 秒)。
        Assert.AreEqual(4, rig.Queue.TotalPendingCount, "残存ノーツ数 (pending) が違う: " + PendingDump(rig));
        Assert.AreEqual(6, GManager.miss, $"Miss の数が違う (perfect={GManager.perfect} bad={GManager.bad}) " + PendingDump(rig));
        Assert.AreEqual(8, GManager.settledNoteCount, "確定ノーツ数が 8 になっていない " + PendingDump(rig));
    }

    // ------------------------------------------------------------------
    // 2) スコア API との整合
    // ------------------------------------------------------------------

    /// <summary>
    /// GManager のスコア API が判定内容と矛盾しないこと:
    /// maxScore = noteCount x 5、ratioScore = 判定の配点合計、
    /// score = ComputeScore(ratioScore, maxScore)、そして結果画面が読む
    /// perfect/great/bad/miss/combo がスコアボードと一致すること。
    /// </summary>
    [Test]
    public void ScoreApi_MatchesJudgements_AndResultScreenFields()
    {
        Rig rig = BuildRig(MixedChartJson, MixedChartNoteCount);
        Player player = new Player(rig);

        // Perfect / Great / Bad が混ざるように、押し時間をずらす。
        for (int i = 0; i < rig.NoteIndices.Count; i++)
        {
            int index = rig.NoteIndices[i];
            float pressTime = rig.NoteTime[index] + (i % 3 == 0 ? 0f : (i % 3 == 1 ? 0.12f : 0.18f));
            player.SchedulePress(pressTime, rig.NoteLane[index]);
            if (rig.NoteType[index] == LongNote.TYPE_HOLD)
            {
                player.ScheduleRelease(rig.NoteEndTime[index], rig.NoteLane[index]);
            }
        }

        player.Run(Frames(0f, 4f, 0.05f));

        // 判定の合計は noteCount。
        int judged = GManager.perfect + GManager.great + GManager.bad + GManager.miss;
        Assert.AreEqual(MixedChartNoteCount, judged);

        // 配点合計は判定の内訳と一致 (1 ノーツ最大 5)。
        float expectedRatio = GManager.perfect * LongNote.SCORE_PERFECT
            + GManager.great * LongNote.SCORE_GREAT
            + GManager.bad * LongNote.SCORE_BAD;
        Assert.AreEqual(expectedRatio, GManager.ratioScore, 0.0001f, "ratioScore が判定と食い違う");
        Assert.AreEqual(MixedChartNoteCount * 5f, GManager.maxScore, 0.0001f, "maxScore != noteCount x 5");
        Assert.LessOrEqual(GManager.ratioScore, GManager.maxScore, "ratioScore が maxScore を超えた (二重加点)");

        // スコアは ComputeScore の式と一致する。
        Assert.AreEqual(Judge.ComputeScore(GManager.ratioScore, GManager.maxScore), GManager.score);
        Assert.GreaterOrEqual(GManager.score, 0);
        Assert.LessOrEqual(GManager.score, 1000000);

        // 結果画面が読むフィールドが 0 のまま (判定が反映されていない) でないこと。
        Assert.Greater(GManager.score, 0, "スコアが 0 のまま (Judge が GManager に反映していない)");
        Assert.Greater(GManager.maxCombo, 0);
        Assert.AreEqual(MixedChartNoteCount, GManager.settledNoteCount);
    }

    /// <summary>
    /// 全ノーツ Perfect ならスコアは満点 (1,000,000) になる。
    /// </summary>
    [Test]
    public void AllPerfect_ScoreIsExactlyOneMillion()
    {
        Rig rig = BuildRig(MixedChartJson, MixedChartNoteCount);
        Player player = new Player(rig);

        foreach (int index in rig.NoteIndices)
        {
            player.SchedulePress(rig.NoteTime[index], rig.NoteLane[index]);
            if (rig.NoteType[index] == LongNote.TYPE_HOLD)
            {
                player.ScheduleRelease(rig.NoteEndTime[index], rig.NoteLane[index]);
            }
        }

        player.Run(Frames(0f, 4f, 0.05f));

        Assert.AreEqual(MixedChartNoteCount, GManager.perfect);
        Assert.AreEqual(MixedChartNoteCount * 5f, GManager.ratioScore, 0.0001f);
        Assert.AreEqual(1000000, GManager.score, "全 Perfect なのに満点になっていない");
    }

    // ------------------------------------------------------------------
    // ヘルパー
    // ------------------------------------------------------------------

    private static List<float> Frames(float from, float to, float step)
    {
        List<float> frames = new List<float>();
        for (float t = from; t <= to + 0.0001f; t += step)
        {
            frames.Add(t);
        }
        return frames;
    }

    /// <summary>
    /// Web 公開の譜面を読み、Judge と NotesManager を組み立てる。
    ///
    /// 譜面の読み込みは **本番と同じ経路**
    /// (<c>NotesManager.LoadRoutine</c>) を通す。テスト側で
    /// GManager.maxScore などを代入しないので、
    /// 「配点上限 = ノーツ数 x 5」の書き込み (NotesManager) も
    /// 本番のコードが設定した値であることを検証できる。
    /// </summary>
    private Rig BuildRig(string chartJson, int expectedNoteCount)
    {
        Data chart = CloudCatalog.ParseChartResponse(
            "{\"songId\":\"w182-test\",\"chartVersion\":1,\"chart\":" + chartJson + "}");
        Assert.IsNotNull(chart, "Web 公開の譜面が読めない");
        Assert.AreEqual(expectedNoteCount, chart.notes.Length);

        rigRoot = new GameObject("JudgeSettlementInvariantTestsRig");
        // 非アクティブ: OnEnable (譜面の読み込みとノーツ生成) が走らないようにする。
        rigRoot.SetActive(false);

        notesManager = rigRoot.AddComponent<NotesManager>();
        judge = rigRoot.AddComponent<Judge>();

        Rig rig = new Rig
        {
            Root = rigRoot,
            Judge = judge,
            Notes = notesManager,
        };

        // 本番の読み込み経路を通す (GManager.maxScore の書き込みを含む)。
        LoadChartThroughProduction(notesManager, chartJson);

        // 期待値はテスト側で独立に計算する (production の時刻式との
        // 食い違いも検出できる)。
        for (int i = 0; i < chart.notes.Length; i++)
        {
            Note note = chart.notes[i];
            float time = NotesManager.NoteTimeSeconds(chart.BPM, note) + GManager.noteTiming * 0.01f;
            float endTime = 0f;
            if (note.type == LongNote.TYPE_HOLD)
            {
                endTime = LongNote.EndSeconds(chart.BPM, note) + GManager.noteTiming * 0.01f;
                if (endTime <= time) endTime = time + (60f / chart.BPM);
            }

            // 本番が作った時刻と一致すること (読み込み経路の検証)。
            Assert.AreEqual(time, notesManager.NotesTime[i], 0.0001f, $"ノーツ {i} の時刻が違う");
            Assert.AreEqual(note.block, notesManager.LaneNum[i], $"ノーツ {i} のレーンが違う");
            Assert.AreEqual(note.type, notesManager.NoteType[i], $"ノーツ {i} の種別が違う");
            Assert.AreEqual(endTime, notesManager.NotesEndTime[i], 0.0001f, $"ノーツ {i} の終端時刻が違う");

            rig.NoteIndices.Add(i);
            rig.NoteTime.Add(time);
            rig.NoteEndTime.Add(endTime);
            rig.NoteLane.Add(note.block);
            rig.NoteType.Add(note.type);
        }

        // Judge の private フィールド (譜面の参照とエフェクトの配列) を差し込む。
        SetPrivateField(judge, "notesManager", notesManager);
        SetPrivateField(judge, "MessageObj", new GameObject[4]);

        return rig;
    }

    /// <summary>
    /// 本番の読み込み経路 (<c>NotesManager.LoadRoutine</c>) で譜面を読み込む。
    ///
    /// rigRoot は非アクティブなので <c>NotesManager.OnEnable</c> は走らない。
    /// そのため private な LoadRoutine を直接呼んで、コルーチンを
    /// 最後まで進める。こうすれば「GManager.maxScore = ノーツ数 x 5」の
    /// 書き込みは **テスト側の代入ではなく** 本番のコードが行う。
    /// </summary>
    private void LoadChartThroughProduction(NotesManager target, string chartJson)
    {
        // 譜面 JSON をプレイセッションに固定する。NotesManager.ParseChart は
        // 固定 JSON (CloudCatalog.ParseChartResponse) を優先して読む。
        var song = new SongData(9001, "w182-invariant-test", "tester", "testVideo", BPM, 5, 0, 60f);
        PlaySessionSnapshot.Begin(song, 1,
            "{\"songId\":\"w182-test\",\"chartVersion\":1,\"chart\":" + chartJson + "}");
        SongData pinned = PlaySessionSnapshot.Current.Song;

        // ノーツ prefab: LoadRoutine が Instantiate する。null だと
        // 破碎するので、テスト用の空オブジェクトを差し込む。
        GameObject notePrefab = new GameObject("W182TestNotePrefab");
        notePrefab.transform.SetParent(rigRoot.transform);
        SetPrivateField(target, "noteObj", notePrefab);

        // 旧ローディング画面 (uGUI) を出さないようにする (UI 副作用を避ける)。
        bool savedGameHud = FlashBeat.UI.V6.V6UiMode.GameHud;
        FlashBeat.UI.V6.V6UiMode.GameHud = true;
        try
        {
            MethodInfo load = typeof(NotesManager).GetMethod("LoadRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(load, "NotesManager.LoadRoutine が見つからない");

            var routine = load.Invoke(target, new object[] { pinned }) as System.Collections.IEnumerator;
            Assert.IsNotNull(routine, "LoadRoutine のコルーチンが取得できない");

            // コルーチンを最後まで進める (yield は手動でスキップする)。
            while (routine.MoveNext())
            {
            }
        }
        finally
        {
            FlashBeat.UI.V6.V6UiMode.GameHud = savedGameHud;
        }
    }

    /// <summary>診断用: レーンごとの未判定ノーツ数。</summary>
    private static string PendingDump(Rig rig)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int lane = 0; lane < Lanes; lane++)
        {
            sb.Append($" lane{lane}={rig.Queue.PendingCount(lane)}");
        }
        sb.Append($" holding3={rig.Queue.IsHolding(3)}");
        return sb.ToString();
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"private field '{fieldName}' が見つからない");
        field.SetValue(target, value);
    }

    /// <summary>
    /// S3: Judge.Update が JudgeStep に渡す時刻が GManager.step_time であること。
    ///
    /// Judge.Update は private なのでリフレクションで呼び、JudgeStep が
    /// **実際に受け取った値** (<see cref="Judge.LastJudgeMediaTime"/>)
    /// observe する。Judge が Time.time など別の時刻を渡すよう改変すると
    /// このテストは赤になる。
    /// </summary>
    [Test]
    public void Update_PassesGManagerStepTimeToJudgeStep()
    {
        rigRoot = new GameObject("JudgeMediaTimeProbe");
        rigRoot.SetActive(false);
        Judge probe = rigRoot.AddComponent<Judge>();

        bool savedStart = GManager.Start;
        float savedStepTime = GManager.step_time;
        try
        {
            PauseGate.HardReset();
            GManager.Start = true;
            GManager.step_time = 123.5f;

            MethodInfo update = typeof(Judge).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(update, "Judge.Update が見つからない");

            update.Invoke(probe, null);

            Assert.AreEqual(GManager.step_time, probe.LastJudgeMediaTime, 0.0001f,
                "Judge.Update が JudgeStep に渡した時刻が GManager.step_time と一致しない");

            // 時刻を変えると追従する (たまたま一致しただけでは通らない)。
            GManager.step_time = 456.75f;
            update.Invoke(probe, null);

            Assert.AreEqual(GManager.step_time, probe.LastJudgeMediaTime, 0.0001f,
                "Judge.Update が JudgeStep に渡した時刻が GManager.step_time を追従しない");
        }
        finally
        {
            GManager.Start = savedStart;
            GManager.step_time = savedStepTime;
        }
    }

    /// <summary>
    /// S2: HOLD の先頭判定が無い (None) とき、中断の差し替えが
    /// 「確定したノーツ数」を水増ししないこと。
    /// </summary>
    [Test]
    public void DowngradeHoldToBad_WithNoHeadJudgement_DoesNotInflateSettledCount()
    {
        var board = new JudgeScoreboard();
        board.ApplyHead(LongNote.Judgement.Perfect);

        board.DowngradeHoldToBad(LongNote.Judgement.None);

        Assert.AreEqual(0, board.Bad, "None の先頭判定で Bad が増えた");
        Assert.AreEqual(1, board.Settled, "None の先頭判定で確定ノーツ数が増えた");
        Assert.AreEqual(LongNote.SCORE_PERFECT, board.RatioScore, 0.0001f,
            "None の先頭判定で配点が変わった");
    }

    /// <summary>
    /// S2: Judge の holdHead (保持中のロングノーツの先頭判定) は
    /// 初期値が None (未判定) であること。
    /// 既定値 (0 = Perfect) だと、HOLD を持たないレーンが
    /// DowngradeHoldToBad を受けたときに確定数を水増しする。
    /// </summary>
    [Test]
    public void Judge_HoldHeadStartsAsNone()
    {
        rigRoot = new GameObject("JudgeHoldHeadProbe");
        rigRoot.SetActive(false);
        Judge probe = rigRoot.AddComponent<Judge>();

        FieldInfo field = typeof(Judge).GetField("holdHead", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, "Judge.holdHead が見つからない");
        var head = (LongNote.Judgement[])field.GetValue(probe);
        Assert.IsNotNull(head, "Judge.holdHead が読めない");
        Assert.AreEqual(Judge.LaneKeys.Length, head.Length, "holdHead の長さがレーン数と違う");

        for (int lane = 0; lane < head.Length; lane++)
        {
            Assert.AreEqual(LongNote.Judgement.None, head[lane], $"lane {lane} の holdHead が None でない");
        }
    }

    /// <summary>
    /// 旧 API nextNotes は互換値「1 + 確定ノーツ数」を保つこと
    /// (正は GManager.settledNoteCount)。
    /// </summary>
    [Test]
    public void NextNotes_StaysCompatibleWithSettledNoteCount()
    {
        Rig rig = BuildRig(HoldOnlyChartJson, HoldOnlyNoteCount);
        Player player = new Player(rig);

        player.SchedulePress(rig.NoteTime[0], rig.NoteLane[0]);
        player.ScheduleRelease(1.0f, rig.NoteLane[0]);
        player.Run(Frames(0f, 1.2f, 0.05f));

        Assert.AreEqual(1, GManager.settledNoteCount, "確定ノーツ数が 1 ではない");
        Assert.AreEqual(GManager.settledNoteCount + 1, GManager.nextNotes,
            "nextNotes の互換値 (1 + 確定ノーツ数) が崩れている");
    }

    /// <summary>
    /// S4: 判定 → pendingNotes → SongEndMachine.Completed の結合テスト。
    ///
    /// 全ノーツを判定したら SongEndMachine が Completed になり、
    /// 未判定ノーツが残っているなら Completed にならないことを確認する。
    /// 修正前 (HOLD 中断で二重計数) ではこのテストが赤になるはず。
    /// </summary>
    [Test]
    public void SongEnd_CompletedOnlyWhenAllNotesSettled()
    {
        // 譜面: 3 ノーツ (Tap x2 + HOLD x1)
        const string chartJson =
            "{\"name\":\"SongEndTest\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[" +
            "{\"type\":1,\"num\":4,\"block\":0,\"LPB\":4}," +   // 0.25s, lane 0
            "{\"type\":1,\"num\":8,\"block\":1,\"LPB\":4}," +   // 0.5s, lane 1
            "{\"type\":2,\"num\":12,\"endNum\":24,\"block\":2,\"LPB\":4}" + // 0.75→1.5s, lane 2
            "]}";
        const int noteCount = 3;

        Rig rig = BuildRig(chartJson, noteCount);
        Player player = new Player(rig);

        // 全ノーツを正しく判定する
        foreach (int index in rig.NoteIndices)
        {
            player.SchedulePress(rig.NoteTime[index], rig.NoteLane[index]);
            if (rig.NoteType[index] == LongNote.TYPE_HOLD)
            {
                player.ScheduleRelease(rig.NoteEndTime[index], rig.NoteLane[index]);
            }
        }
        player.Run(Frames(0f, 2f, 0.05f));

        // 確定ノーツ数 = noteCount
        Assert.AreEqual(noteCount, GManager.perfect + GManager.great + GManager.bad + GManager.miss);
        Assert.AreEqual(noteCount, GManager.settledNoteCount);

        // SongEndTick を作って SongEndMachine.Update を呼ぶ
        var tick = new SongEndTick
        {
            EndReached = true,
            SettleDeadline = rig.NoteTime.Max() + 1f, // 最後のノーツ時刻 + 1秒
            JudgeTime = rig.NoteTime.Max() + 2f,      // 判定時刻は期限を超えている
            PendingNotes = noteCount - (GManager.settledNoteCount), // = 0
            TotalNotes = noteCount,
        };

        var stateMachine = new SongEndMachine();
        bool completed = stateMachine.Update(in tick);

        // 全ノーツ確定済み → Completed になる
        Assert.IsTrue(completed, "全ノーツ確定済みなのに Completed にならない");
        Assert.AreEqual(SongEndState.Completed, stateMachine.State,
            "State が Completed になっていない");

        // --- 今度は未判定ノーツを残すケース ---
        // 譜面を再構築して、1 ノーツだけ押さない
        Rig rig2 = BuildRig(chartJson, noteCount);
        Player player2 = new Player(rig2);

        // 最初の 2 ノーツだけ押す (最後の HOLD は押さない)
        for (int i = 0; i < 2; i++)
        {
            player2.SchedulePress(rig2.NoteTime[i], rig2.NoteLane[i]);
        }
        player2.Run(Frames(0f, 1f, 0.05f));

        // 確定ノーツ数 = 2、未判定 = 1
        Assert.AreEqual(2, GManager.settledNoteCount);

        var tick2 = new SongEndTick
        {
            EndReached = true,
            SettleDeadline = rig2.NoteTime.Max() + 1f,
            JudgeTime = rig2.NoteTime.Max() + 0.5f, // まだ猶予期間内 (SettleGraceSeconds = 2f)
            PendingNotes = noteCount - (GManager.settledNoteCount), // = 1
            TotalNotes = noteCount,
        };

        var stateMachine2 = new SongEndMachine();
        bool completed2 = stateMachine2.Update(in tick2);

        // 未判定ノーツが残っている → Finishing のまま (Completed にならない)
        Assert.IsFalse(completed2, "未判定ノーツが残っているのに Completed になった");
        Assert.AreNotEqual(SongEndState.Completed, stateMachine2.State,
            "State が Completed 以外になっていない");

        // 強制完了 (JudgeTime > SettleDeadline + SettleGraceSeconds) なら Completed になる
        var tick3 = new SongEndTick
        {
            EndReached = true,
            SettleDeadline = rig2.NoteTime.Max() + 1f,
            JudgeTime = rig2.NoteTime.Max() + 3f, // 強制完了ラインを超えている (SettleGraceSeconds = 2f)
            PendingNotes = noteCount - (GManager.settledNoteCount), // = 1
            TotalNotes = noteCount,
        };

        var stateMachine3 = new SongEndMachine();
        bool completed3 = stateMachine3.Update(in tick3);

        Assert.IsTrue(completed3, "強制完了ラインを超えているのに Completed にならない");
        Assert.AreEqual(SongEndState.Completed, stateMachine3.State);
        Assert.IsTrue(stateMachine3.ForcedByDeadline, "ForcedByDeadline が true になっていない");
    }

    /// <summary>組み立てた譜面と Judge の参照。</summary>
    private sealed class Rig
    {
        public GameObject Root;
        public Judge Judge;
        public NotesManager Notes;

        public readonly List<int> NoteIndices = new List<int>();
        public readonly List<float> NoteTime = new List<float>();
        public readonly List<float> NoteEndTime = new List<float>();
        public readonly List<int> NoteLane = new List<int>();
        public readonly List<int> NoteType = new List<int>();

        /// <summary>Judge が組み立てた判定キュー (private フィールド)。</summary>
        public JudgeQueue Queue
        {
            get
            {
                FieldInfo field = typeof(Judge).GetField("queue", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(field, "Judge.queue が見つからない");
                return (JudgeQueue)field.GetValue(Judge);
            }
        }
    }

    /// <summary>
    /// キー入力を再現して Judge.JudgeStep を駆動する。
    /// 実機と同じ順序 (押下 → HOLD 保持 → Miss) をそのまま使う。
    /// </summary>
    private sealed class Player
    {
        private struct Event
        {
            public float Time;
            public int Lane;
            public bool Down;
        }

        private readonly Rig rig;
        private readonly List<Event> events = new List<Event>();
        private readonly bool[] keyDown = new bool[Lanes];
        private readonly bool[] pressedThisFrame = new bool[Lanes];

        public Player(Rig rig)
        {
            this.rig = rig;
        }

        public void SchedulePress(float time, int lane)
        {
            events.Add(new Event { Time = time, Lane = lane, Down = true });
        }

        public void ScheduleRelease(float time, int lane)
        {
            events.Add(new Event { Time = time, Lane = lane, Down = false });
        }

        public void Run(List<float> frames)
        {
            events.Sort((a, b) => a.Time.CompareTo(b.Time));
            int next = 0;

            foreach (float frameTime in frames)
            {
                // このフレームまでの入力を適用する。
                while (next < events.Count && events[next].Time <= frameTime)
                {
                    Event e = events[next];
                    if (e.Down)
                    {
                        keyDown[e.Lane] = true;
                        pressedThisFrame[e.Lane] = true;
                    }
                    else
                    {
                        keyDown[e.Lane] = false;
                    }
                    next++;
                }

                rig.Judge.JudgeStep(frameTime, lane => pressedThisFrame[lane], lane => keyDown[lane]);

                for (int lane = 0; lane < Lanes; lane++)
                {
                    pressedThisFrame[lane] = false;
                }
            }
        }
    }
}
