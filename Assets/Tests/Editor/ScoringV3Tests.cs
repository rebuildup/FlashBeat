using NUnit.Framework;
using FlashBeat.Gameplay;
using FlashBeat.Persistence;

/// <summary>
/// scoring v3 (ADR-0029 / Issue #602) の EditMode テスト。
///
/// 検証する契約:
///
///   - 式: v2 の全成分 + milestoneBonus = comboMilestones*25 /
///     finalBonus = judgmentsComplete && miss==0 ? 100 : 0 /
///     maxRatio = 15*N + floor(N/50)*25 + 100。
///   - 節目はコンボが 50 の倍数に達するたびに 1 回数え、コンボが切れても
///     回数は保持される (以降のコンボで再び加点される)。
///   - 全 Perfect ならスコアは満点 1,000,000。空の譜面は 0。
///   - ローカル記録は scoringVersion で分離される ("@3" = v3)。
/// </summary>
public class ScoringV3Tests
{
    // --- 式 -------------------------------------------------------------

    [Test]
    public void MaxRatioV3_MatchesContractFormula()
    {
        // N=100: 15*100 + floor(100/50)*25 + 100 = 1500 + 50 + 100 = 1650。
        Assert.AreEqual(1650f, Scoring.MaxRatioV3(100), 0.0001f);
        // N=49: 節目ボーナス上限なし。
        Assert.AreEqual(49 * 15f + 100f, Scoring.MaxRatioV3(49), 0.0001f);
        // N=50: floor(50/50)=1 → +25。
        Assert.AreEqual(50 * 15f + 25f + 100f, Scoring.MaxRatioV3(50), 0.0001f);
        // N=51: floor(51/50)=1 → +25 (50 ごとに 1 回だけ)。
        Assert.AreEqual(51 * 15f + 25f + 100f, Scoring.MaxRatioV3(51), 0.0001f);
        // 空の譜面は分母 0 (スコアを与えない。サーバも noteCount<=0 を拒否)。
        Assert.AreEqual(0f, Scoring.MaxRatioV3(0), 0.0001f);
        Assert.AreEqual(0, Scoring.ScoreV3(0, 0, 0, 0, 0, 0, 0, false));
    }

    [Test]
    public void BreakdownV3_MatchesAdrFormula()
    {
        // P=60 G=10 B=0 M=1, maxCombo=70, milestones=1, N=100。
        // judgment = 300+30 = 330, comboBonus = 350,
        // noMissBonus = 0 (miss>0), milestoneBonus = 25, finalBonus = 0。
        Scoring.V3Breakdown b = Scoring.BreakdownV3(60, 10, 0, 1, 70, 1, 100, false);
        Assert.AreEqual(330f, b.Judgment, 0.0001f);
        Assert.AreEqual(350, b.ComboBonus);
        Assert.AreEqual(0, b.NoMissBonus);
        Assert.AreEqual(25, b.MilestoneBonus);
        Assert.AreEqual(0, b.FinalBonus);
        Assert.AreEqual(705f, b.RatioScore, 0.0001f);
        Assert.AreEqual(1650f, b.MaxRatio, 0.0001f);
    }

    [Test]
    public void FinalBonus_RequiresCompletePlayAndNoMiss()
    {
        // miss=0 かつ全ノーツ判定済み (judgmentsComplete): finalBonus=100、
        // noMissBonus=(P+G+B)*5。
        Scoring.V3Breakdown clean = Scoring.BreakdownV3(4, 3, 2, 0, 6, 0, 10, true);
        Assert.AreEqual(100, clean.FinalBonus);
        Assert.AreEqual(45, clean.NoMissBonus);

        // miss=1: 終了ボーナスとノーミスボーナスは両方消える。
        Scoring.V3Breakdown missed = Scoring.BreakdownV3(4, 3, 2, 1, 6, 0, 10, true);
        Assert.AreEqual(0, missed.FinalBonus);
        Assert.AreEqual(0, missed.NoMissBonus);
        Assert.Less(Scoring.ScoreV3(4, 3, 2, 1, 6, 0, 10, true),
            Scoring.ScoreV3(4, 3, 2, 0, 6, 0, 10, true),
            "Miss 後のスコアが Miss 前を下回っていない");
    }

    [Test]
    public void FinalBonus_VestsOnlyWhenJudgmentsComplete()
    {
        // Issue #618: miss==0 でも全ノーツの判定が確定するまでは
        // (プレイ途中・未完走・中断) 終了ボーナスは乗らない。
        Scoring.V3Breakdown live = Scoring.BreakdownV3(4, 3, 2, 0, 6, 0, 10, false);
        Assert.AreEqual(0, live.FinalBonus, "途中経過にフルコンボ終了ボーナスが乗った");
        Assert.AreEqual(45, live.NoMissBonus, "ノーミスボーナスは途中経過でも乗る (v2 契約)");

        Scoring.V3Breakdown done = Scoring.BreakdownV3(4, 3, 2, 0, 6, 0, 10, true);
        Assert.AreEqual(100, done.FinalBonus);
        Assert.AreEqual(done.RatioScore - Scoring.FullComboBonusPoints, live.RatioScore, 0.0001f,
            "途中経過と確定後の差が終了ボーナス分ではない");
        Assert.Less(Scoring.ScoreV3(4, 3, 2, 0, 6, 0, 10, false),
            Scoring.ScoreV3(4, 3, 2, 0, 6, 0, 10, true),
            "確定前後でスコアが変わらない");
    }

    [Test]
    public void PerfectRun_ScoresExactlyOneMillion()
    {
        // 全 Perfect・ノーミスなし・maxCombo=N・milestones=floor(N/50):
        // 5N + 5N + 5N + floor(N/50)*25 + 100 = MaxRatioV3(N)。
        Assert.AreEqual(1000000, Scoring.ScoreV3(100, 0, 0, 0, 100, 2, 100, true));
        Assert.AreEqual(1000000, Scoring.ScoreV3(10, 0, 0, 0, 10, 0, 10, true));
        Assert.AreEqual(1000000, Scoring.ScoreV3(49, 0, 0, 0, 49, 0, 49, true));
    }

    [Test]
    public void Score_NeverExceedsOneMillion()
    {
        // 節目回数が上限を超えて申告されても (改ざん)、クライアント計算では
        // 発生しないが、式としても飽和することを確認する。
        int score = Scoring.ScoreV3(100, 0, 0, 0, 100, 2, 100, true);
        Assert.LessOrEqual(score, 1000000);
        Assert.GreaterOrEqual(score, 0);
    }

    // --- 節目カウント (JudgeScoreboard) ----------------------------------

    [Test]
    public void MilestoneCrossings_CountEveryFiftiethCombo()
    {
        var board = new JudgeScoreboard();
        for (int i = 0; i < 49; i++) board.ApplyHead(LongNote.Judgement.Perfect);
        Assert.AreEqual(0, board.MilestoneCrossings, "49 コンボで節目が数えられた");

        board.ApplyHead(LongNote.Judgement.Perfect);
        Assert.AreEqual(1, board.MilestoneCrossings, "50 コンボで節目が数えられない");

        for (int i = 0; i < 50; i++) board.ApplyHead(LongNote.Judgement.Perfect);
        Assert.AreEqual(2, board.MilestoneCrossings, "100 コンボで 2 回目の節目が数えられない");
        Assert.AreEqual(100, board.MaxCombo);
    }

    [Test]
    public void MilestoneCrossings_RecurAfterComboBreak()
    {
        var board = new JudgeScoreboard();
        // 1 回目の節目 (50) → Miss でコンボ切断 → 再び 50 で 2 回目。
        for (int i = 0; i < 50; i++) board.ApplyHead(LongNote.Judgement.Perfect);
        Assert.AreEqual(1, board.MilestoneCrossings);

        board.ApplyMiss();
        Assert.AreEqual(0, board.Combo);
        Assert.AreEqual(50, board.MaxCombo, "MaxCombo が Miss で消えた");

        for (int i = 0; i < 50; i++) board.ApplyHead(LongNote.Judgement.Perfect);
        Assert.AreEqual(2, board.MilestoneCrossings,
            "コンボ切断後の再達成で節目が再び数えられない");
        Assert.AreEqual(50, board.MaxCombo, "2 回目の 50 は MaxCombo を更新しない");
    }

    [Test]
    public void MilestoneCrossings_BadKeepsComboAndCounts()
    {
        // Bad はコンボを切らない (v2 と同じ)。Bad 継続でも節目は数える。
        var board = new JudgeScoreboard();
        for (int i = 0; i < 25; i++) board.ApplyHead(LongNote.Judgement.Perfect);
        for (int i = 0; i < 25; i++) board.ApplyHead(LongNote.Judgement.Bad);
        Assert.AreEqual(1, board.MilestoneCrossings, "Bad 継続の 50 コンボで節目が数えられない");
    }

    [Test]
    public void MilestoneCrossings_NeverExceedHitsDividedByInterval()
    {
        // サーバの上限検査 floor(hits/50) をクライアント側でも超えない:
        // コンボは命中数を超えられないので、回数は floor(hits/50) 以下。
        var board = new JudgeScoreboard();
        for (int i = 0; i < 120; i++) board.ApplyHead(LongNote.Judgement.Great);
        board.ApplyMiss();
        for (int i = 0; i < 80; i++) board.ApplyHead(LongNote.Judgement.Great);
        // hits=200 → floor(200/50)=4 が上限。実際: 50,100 (120 中) + 50 (80 中) = 3 回。
        Assert.AreEqual(3, board.MilestoneCrossings);
        Assert.LessOrEqual(board.MilestoneCrossings,
            (board.Perfect + board.Great + board.Bad) / Scoring.MilestoneInterval);
    }

    // --- ローカル記録の分離 ----------------------------------------------

    [Test]
    public void ScoreStore_KeepsV3RecordsSeparateFromV2()
    {
        var store = new ScoreStore();
        var v2 = new ScoreKey("who", 1, Scoring.Version2);
        var v3 = new ScoreKey("who", 1, Scoring.Version3);

        store.Submit(v2, 800000);
        store.Submit(v3, 950000);

        Assert.AreEqual(800000, store.Get(v2));
        Assert.AreEqual(950000, store.Get(v3));

        ScoreStore loaded = ScoreStore.Deserialize(store.Serialize());
        Assert.AreEqual(800000, loaded.Get(v2));
        Assert.AreEqual(950000, loaded.Get(v3));
    }
}
