using NUnit.Framework;
using FlashBeat.Gameplay;
using FlashBeat.Persistence;

/// <summary>
/// scoring v4 (ADR-0033 / Issue #736) の EditMode テスト。
///
/// 検証する契約:
///
///   - 式: 基礎スコア = floor(judgment * 1,000,000 / (5 * noteCount))
///     (判定由来のみ、上限 1,000,000)。ボーナススコア =
///     floor(bonusRatio * 1,000,000 / maxBonusRatio) (上限 1,000,000)。
///     score = baseScore + bonusScore (上限 2,000,000)。
///   - bonusRatio / maxBonusRatio は v3 のボーナス成分をそのまま使う
///     (comboBonus + noMissBonus + milestoneBonus + finalBonus)。
///   - 全 Perfect のフルコンボは 2,000,000 — v1〜v3 の 1,000,000 天井を
///     正当に超える (Issue #736 の受入条件)。
///   - 基礎スコアは判定だけから決まるので、ボーナスがいくら乗っても
///     1,000,000 を超えない。
///   - ローカル記録は scoringVersion で分離される ("@4" = v4)。
/// </summary>
public class ScoringV4Tests
{
    // --- 式 -------------------------------------------------------------

    [Test]
    public void MaxBonusRatioV4_MatchesContractFormula()
    {
        // N=100: 10*100 + floor(100/50)*25 + 100 = 1000 + 50 + 100 = 1150。
        Assert.AreEqual(1150f, Scoring.MaxBonusRatioV4(100), 0.0001f);
        // N=49: 節目分母なし。
        Assert.AreEqual(49 * 10f + 100f, Scoring.MaxBonusRatioV4(49), 0.0001f);
        // N=50: floor(50/50)=1 → +25。
        Assert.AreEqual(50 * 10f + 25f + 100f, Scoring.MaxBonusRatioV4(50), 0.0001f);
        // N=51: floor(51/50)=1 → +25。
        Assert.AreEqual(51 * 10f + 25f + 100f, Scoring.MaxBonusRatioV4(51), 0.0001f);
        // 空の譜面は分母 0 (スコアを与えない。サーバも noteCount<=0 を拒否)。
        Assert.AreEqual(0f, Scoring.MaxBonusRatioV4(0), 0.0001f);
        Assert.AreEqual(0, Scoring.ScoreV4(0, 0, 0, 0, 0, 0, 0, false));
    }

    [Test]
    public void BreakdownV4_SplitsBaseAndBonus()
    {
        // P=60 G=10 B=0 M=1, maxCombo=70, milestones=1, N=100, miss>0。
        // judgment = 330 → baseScore = floor(330*1e6/500) = 660,000。
        // bonusRatio = 350 + 0 + 25 + 0 = 375。
        //   bonusScore = floor(375*1e6/1150) = 326,086。
        Scoring.V4Breakdown b = Scoring.BreakdownV4(60, 10, 0, 1, 70, 1, 100, false);
        Assert.AreEqual(330f, b.Judgment, 0.0001f);
        Assert.AreEqual(350, b.ComboBonus);
        Assert.AreEqual(0, b.NoMissBonus);
        Assert.AreEqual(25, b.MilestoneBonus);
        Assert.AreEqual(0, b.FinalBonus);
        Assert.AreEqual(660000, b.BaseScore);
        Assert.AreEqual(326086, b.BonusScore);
        Assert.AreEqual(986086, b.Score);
        Assert.AreEqual(1150f, b.MaxBonusRatio, 0.0001f);
    }

    [Test]
    public void PerfectFullCombo_ScoresTwoMillion()
    {
        // 全 Perfect・maxCombo=N・milestones=floor(N/50)・miss=0:
        //   baseScore = 1,000,000 (judgment = 5N の上限)
        //   bonusRatio = 5N + 5N + floor(N/50)*25 + 100 = MaxBonusRatioV4(N)
        //   → bonusScore = 1,000,000 → score = 2,000,000。
        Assert.AreEqual(2000000, Scoring.ScoreV4(100, 0, 0, 0, 100, 2, 100, true));
        Assert.AreEqual(2000000, Scoring.ScoreV4(10, 0, 0, 0, 10, 0, 10, true));
        Assert.AreEqual(2000000, Scoring.ScoreV4(49, 0, 0, 0, 49, 0, 49, true));
        Assert.AreEqual(2000000, Scoring.ScoreV4(139, 0, 0, 0, 139, 2, 139, true));
    }

    [Test]
    public void BaseScore_NeverExceedsOneMillion()
    {
        // 基礎スコアは判定由来のみ: 全 Perfect でも 1,000,000 で頭打ち。
        // ボーナスが上限を超える入力 (改ざん相当) を渡しても、各半分は
        // それぞれの分母で正規化されるため合計は 2,000,000 を超えない。
        for (int n = 1; n <= 200; n++)
        {
            Scoring.V4Breakdown b = Scoring.BreakdownV4(n, 0, 0, 0, n, n / 50, n, true);
            Assert.LessOrEqual(b.BaseScore, 1000000, $"N={n}: baseScore が 1,000,000 を超えた");
            Assert.LessOrEqual(b.BonusScore, 1000000, $"N={n}: bonusScore が 1,000,000 を超えた");
            Assert.LessOrEqual(b.Score, Scoring.ScoreMaxV4, $"N={n}: score が 2,000,000 を超えた");
            Assert.GreaterOrEqual(b.Score, 0);
        }
    }

    [Test]
    public void BonusScore_ExcludesNoMissAndFinalWhenMissed()
    {
        // miss>0 なら noMissBonus と finalBonus は 0 (v3 と同じ契約)。
        Scoring.V4Breakdown missed = Scoring.BreakdownV4(4, 3, 2, 1, 6, 0, 10, true);
        Assert.AreEqual(0, missed.NoMissBonus);
        Assert.AreEqual(0, missed.FinalBonus);
        Assert.Less(Scoring.ScoreV4(4, 3, 2, 1, 6, 0, 10, true),
            Scoring.ScoreV4(4, 3, 2, 0, 6, 0, 10, true),
            "Miss 後のスコアが Miss 前を下回っていない");
    }

    [Test]
    public void FinalBonus_VestsOnlyWhenJudgmentsComplete()
    {
        // Issue #618 の契約は v4 でも同じ: miss==0 でも全ノーツの判定が
        // 確定するまでは終了ボーナスは乗らない。
        Scoring.V4Breakdown live = Scoring.BreakdownV4(4, 3, 2, 0, 6, 0, 10, false);
        Assert.AreEqual(0, live.FinalBonus, "途中経過にフルコンボ終了ボーナスが乗った");
        Assert.AreEqual(45, live.NoMissBonus, "ノーミスボーナスは途中経過でも乗る (v2 契約)");

        Scoring.V4Breakdown done = Scoring.BreakdownV4(4, 3, 2, 0, 6, 0, 10, true);
        Assert.AreEqual(100, done.FinalBonus);
        Assert.LessOrEqual(Scoring.ScoreV4(4, 3, 2, 0, 6, 0, 10, false),
            Scoring.ScoreV4(4, 3, 2, 0, 6, 0, 10, true),
            "確定前後でスコアが増えていない");
    }

    [Test]
    public void LatestVersion_IsV5()
    {
        // ADR-0035 / Issue #797: 新規プレイは scoringVersion 5。
        // v4 の式は Scoring.ScoreV4 に残り、既存の v4 記録は再計算しない。
        Assert.AreEqual(5, Scoring.LatestVersion);
        Assert.AreEqual(Scoring.Version5, Scoring.LatestVersion);
    }

    // --- ローカル記録の分離 ----------------------------------------------

    [Test]
    public void ScoreStore_KeepsV4RecordsSeparateFromV3()
    {
        var store = new ScoreStore();
        var v3 = new ScoreKey("who", 1, Scoring.Version3);
        var v4 = new ScoreKey("who", 1, Scoring.Version4);

        store.Submit(v3, 950000);
        store.Submit(v4, 1861870);

        Assert.AreEqual(950000, store.Get(v3));
        Assert.AreEqual(1861870, store.Get(v4));

        ScoreStore loaded = ScoreStore.Deserialize(store.Serialize());
        Assert.AreEqual(950000, loaded.Get(v3));
        Assert.AreEqual(1861870, loaded.Get(v4));
    }
}
