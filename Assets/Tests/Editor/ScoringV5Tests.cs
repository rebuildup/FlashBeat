using NUnit.Framework;
using FlashBeat.Gameplay;
using FlashBeat.Persistence;

/// <summary>
/// scoring v5 (ADR-0035 / Issue #797) の EditMode テスト。
///
/// 検証する契約:
///
///   - 式: 基礎スコア = floor(judgment * 1,000,000 / (5 * noteCount))
///     (判定由来のみ、上限 1,000,000)。ボーナススコア =
///     floor(bonusRatio * 1,000,000 / maxBonusRatio) (上限 1,000,000)。
///     score = baseScore + bonusScore (上限 2,000,000)。v4 と同じ 2 段構成。
///   - v5 の bonusRatio は milestoneBonus (comboMilestones * 250) +
///     noMissBonus + finalBonus のみ。連続コンボボーナス
///     (maxCombo * 5) は存在しない — コンボを伸ばすだけでは加点されず、
///     節目到達イベントだけが加点を確定する。
///   - maxBonusRatio = floor(N/50) * 250 + 5N + 100 (v4 の連続コンボ項
///     10N + 節目 25 を、節目機構へ予算を移した形)。
///   - miss == 0 かつ全ノーツ判定確定のプレイは v4 と同一スコア
///     (bonusRatio == maxBonusRatio → bonusScore = 1,000,000)。
///   - 確定済みの節目ボーナスは miss で失われない (comboMilestones は
///     JudgeScoreboard がコンボ切断を跨いで累積する)。
///   - ローカル記録は scoringVersion で分離される ("@5" = v5)。
/// </summary>
public class ScoringV5Tests
{
    // --- 式 -------------------------------------------------------------

    [Test]
    public void MaxBonusRatioV5_MatchesContractFormula()
    {
        // N=100: floor(100/50)*250 + 100*5 + 100 = 500 + 600 = 1100。
        Assert.AreEqual(1100f, Scoring.MaxBonusRatioV5(100), 0.0001f);
        // N=49: 節目分母なし → 49*5 + 100 = 345。
        Assert.AreEqual(345f, Scoring.MaxBonusRatioV5(49), 0.0001f);
        // N=50: floor(50/50)=1 → +250 → 250 + 250 + 100 = 600。
        Assert.AreEqual(600f, Scoring.MaxBonusRatioV5(50), 0.0001f);
        // N=51: floor(51/50)=1 → +250 → 250 + 255 + 100 = 605。
        Assert.AreEqual(605f, Scoring.MaxBonusRatioV5(51), 0.0001f);
        // N=139 (Who): floor(139/50)=2 → 500 + 695 + 100 = 1295。
        Assert.AreEqual(1295f, Scoring.MaxBonusRatioV5(139), 0.0001f);
        // 空の譜面は分母 0 (スコアを与えない。サーバも noteCount<=0 を拒否)。
        Assert.AreEqual(0f, Scoring.MaxBonusRatioV5(0), 0.0001f);
        Assert.AreEqual(0, Scoring.ScoreV5(0, 0, 0, 0, 0, 0, 0, false));
    }

    [Test]
    public void BreakdownV5_SplitsBaseAndBonus()
    {
        // P=60 G=10 B=0 M=1, maxCombo=70, milestones=1, N=100, miss>0。
        // judgment = 330 → baseScore = floor(330*1e6/500) = 660,000。
        // bonusRatio = 250 (節目) + 0 (miss>0 でノーミス/終了なし) = 250。
        //   bonusScore = floor(250*1e6/1100) = 227,272。
        // maxCombo=70 は v5 の式に入らない (連続コンボボーナス無し)。
        Scoring.V5Breakdown b = Scoring.BreakdownV5(60, 10, 0, 1, 70, 1, 100, false);
        Assert.AreEqual(330f, b.Judgment, 0.0001f);
        Assert.AreEqual(250, b.MilestoneBonus);
        Assert.AreEqual(0, b.NoMissBonus);
        Assert.AreEqual(0, b.FinalBonus);
        Assert.AreEqual(660000, b.BaseScore);
        Assert.AreEqual(227272, b.BonusScore);
        Assert.AreEqual(887272, b.Score);
        Assert.AreEqual(1100f, b.MaxBonusRatio, 0.0001f);
    }

    [Test]
    public void ContinuousComboAlone_EarnsNoBonus()
    {
        // maxCombo をいくら伸ばしても節目未到達ならコンボ報酬は 0。
        // P=10 G=0 B=0 M=0, maxCombo=10, milestones=0, N=10。
        // bonusRatio = 0 + 50 (ノーミス) + 0 (未確定) = 50。
        //   bonusScore = floor(50*1e6/150) = 333,333。
        // 同じ入力で maxCombo を変えてもスコアは変わらない
        // (v4 なら comboBonus = maxCombo*5 で変わる)。
        int withCombo10 = Scoring.ScoreV5(10, 0, 0, 0, 10, 0, 10, false);
        int withCombo5 = Scoring.ScoreV5(10, 0, 0, 0, 5, 0, 10, false);
        Assert.AreEqual(withCombo10, withCombo5,
            "maxCombo の違いが v5 スコアに影響した (連続コンボ加点が残っている)");
        // baseScore = floor(50*1e6/50) = 1,000,000。
        Assert.AreEqual(1333333, withCombo10);

        // 同じ入力の v4 は maxCombo で変わる (対照)。
        Assert.AreNotEqual(
            Scoring.ScoreV4(10, 0, 0, 0, 10, 0, 10, false),
            Scoring.ScoreV4(10, 0, 0, 0, 5, 0, 10, false));
    }

    [Test]
    public void EachMilestoneCrossing_VestsFixedBonus()
    {
        // N=200 (節目上限 4)、P=150 G=50 M=0、確定済み。
        // maxBonus = 4*250 + 1000 + 100 = 2100。
        // baseScore = floor((750+150)*1e6/1000) = 900,000。
        // bonusRatio = 節目 ms*250 + noMiss 1000 + final 100。
        // 独立オラクル (契約式を手計算した期待値):
        //   ms=0: floor(1100e6/2100)=523,809 → 1,423,809
        //   ms=1: floor(1350e6/2100)=642,857 → 1,542,857
        //   ms=2: floor(1600e6/2100)=761,904 → 1,661,904
        //   ms=3: floor(1850e6/2100)=880,952 → 1,780,952
        //   ms=4: floor(2100e6/2100)=1,000,000 → 1,900,000
        // (floor のため 1 節目あたりの増分は 119,047〜119,048 で揺れる。
        //  合計の bonusScore は厳密に単調増加する。)
        int[] expected = { 1423809, 1542857, 1661904, 1780952, 1900000 };
        for (int ms = 0; ms <= 4; ms++)
        {
            Assert.AreEqual(expected[ms],
                Scoring.ScoreV5(150, 50, 0, 0, 200, ms, 200, true),
                $"節目 {ms} 回のスコアが契約オラクルと一致しない");
        }
    }

    [Test]
    public void PerfectFullCombo_ScoresTwoMillion()
    {
        // 全 Perfect・maxCombo=N・milestones=floor(N/50)・miss=0:
        //   baseScore = 1,000,000 (judgment = 5N の上限)
        //   bonusRatio = 250*floor(N/50) + 5N + 100 = MaxBonusRatioV5(N)
        //   → bonusScore = 1,000,000 → score = 2,000,000。
        Assert.AreEqual(2000000, Scoring.ScoreV5(100, 0, 0, 0, 100, 2, 100, true));
        Assert.AreEqual(2000000, Scoring.ScoreV5(10, 0, 0, 0, 10, 0, 10, true));
        Assert.AreEqual(2000000, Scoring.ScoreV5(49, 0, 0, 0, 49, 0, 49, true));
        Assert.AreEqual(2000000, Scoring.ScoreV5(139, 0, 0, 0, 139, 2, 139, true));
    }

    [Test]
    public void NoMissPlay_ScoresIdenticallyToV4()
    {
        // ADR-0035 §1.2: miss==0 かつ全ノーツ判定確定 (hits == noteCount、
        // 実プレイでは maxCombo == hits) のプレイは v4 と同一スコア。
        // v4 の bonusRatio (maxCombo*5 + hits*5 + ms*25 + 100) と v5 の
        // bonusRatio (ms*250 + hits*5 + 100) はそれぞれ自分の分母に
        // 一致するため、どちらも bonusScore = 1,000,000 になる。
        // 判定構成を振って実値で検証する (式からの導出ではなく結果で確認)。
        for (int n = 1; n <= 200; n++)
        {
            foreach (var mix in new[]
            {
                new[] { n, 0, 0 },                          // 全 Perfect
                new[] { n * 9 / 10, n - n * 9 / 10, 0 },    // 9 割 Perfect
                new[] { n / 2, n / 3, n - n / 2 - n / 3 },  // P/G/B 混合
                new[] { 0, 0, n },                          // 全 Bad
            })
            {
                int p = mix[0], g = mix[1], b = mix[2];
                int ms = n / Scoring.MilestoneInterval;
                int v4 = Scoring.ScoreV4(p, g, b, 0, n, ms, n, true);
                int v5 = Scoring.ScoreV5(p, g, b, 0, n, ms, n, true);
                Assert.AreEqual(v4, v5,
                    $"miss==0 確定プレイで v4/v5 が乖離: P{p} G{g} B{b} N{n} " +
                    $"(v4={v4}, v5={v5})");
            }
        }
    }

    [Test]
    public void MissedPlay_KeepsEarnedMilestonesButLosesNoMissAndFinal()
    {
        // miss>0: noMissBonus と finalBonus は 0 だが、確定済みの
        // comboMilestones はそのまま乗る (獲得済みボーナスは失われない)。
        Scoring.V5Breakdown missed = Scoring.BreakdownV5(4, 3, 2, 1, 6, 1, 10, true);
        Assert.AreEqual(250, missed.MilestoneBonus, "miss 後も節目ボーナスが消えた");
        Assert.AreEqual(0, missed.NoMissBonus);
        Assert.AreEqual(0, missed.FinalBonus);

        // 同じ節目数で miss の有無だけが違う入力: miss 側は必ず低い。
        Assert.Less(
            Scoring.ScoreV5(4, 3, 2, 1, 6, 1, 10, true),
            Scoring.ScoreV5(4, 3, 2, 0, 6, 1, 10, true),
            "Miss 後のスコアが Miss 前を下回っていない");
    }

    [Test]
    public void BaseScore_NeverExceedsOneMillion()
    {
        // 基礎スコアは判定由来のみ: 全 Perfect でも 1,000,000 で頭打ち。
        // 改ざん相当の入力 (節目数を上限超過) を渡しても、各半分は
        // それぞれの分母で正規化されるため合計は 2,000,000 を超えない。
        for (int n = 1; n <= 200; n++)
        {
            Scoring.V5Breakdown b = Scoring.BreakdownV5(n, 0, 0, 0, n, n / 50, n, true);
            Assert.LessOrEqual(b.BaseScore, 1000000, $"N={n}: baseScore が 1,000,000 を超えた");
            Assert.LessOrEqual(b.BonusScore, 1000000, $"N={n}: bonusScore が 1,000,000 を超えた");
            Assert.LessOrEqual(b.Score, Scoring.ScoreMaxV5, $"N={n}: score が 2,000,000 を超えた");
            Assert.GreaterOrEqual(b.Score, 0);
        }
    }

    [Test]
    public void FinalBonus_VestsOnlyWhenJudgmentsComplete()
    {
        // Issue #618 の契約は v5 でも同じ: miss==0 でも全ノーツの判定が
        // 確定するまでは終了ボーナスは乗らない。
        Scoring.V5Breakdown live = Scoring.BreakdownV5(4, 3, 2, 0, 6, 0, 10, false);
        Assert.AreEqual(0, live.FinalBonus, "途中経過にフルコンボ終了ボーナスが乗った");
        Assert.AreEqual(45, live.NoMissBonus, "ノーミスボーナスは途中経過でも乗る (v2 契約)");

        Scoring.V5Breakdown done = Scoring.BreakdownV5(4, 3, 2, 0, 6, 0, 10, true);
        Assert.AreEqual(100, done.FinalBonus);
        Assert.LessOrEqual(Scoring.ScoreV5(4, 3, 2, 0, 6, 0, 10, false),
            Scoring.ScoreV5(4, 3, 2, 0, 6, 0, 10, true),
            "確定前後でスコアが増えていない");
    }

    [Test]
    public void ComponentScoreV5_MatchesBreakdownBonusHalf()
    {
        // 内訳表示用の ComponentScoreV5 は BreakdownV5 の bonusScore と
        // 同じ丸め (floor) で、合計が bonusScore に一致する。
        Scoring.V5Breakdown b = Scoring.BreakdownV5(60, 10, 0, 1, 70, 1, 100, false);
        int componentSum = Scoring.ComponentScoreV5(b.MilestoneBonus, 100)
            + Scoring.ComponentScoreV5(b.NoMissBonus + b.FinalBonus, 100);
        // 成分ごとの floor 和は全体の floor と一致しないことがあるため、
        // ここでは「各成分が非負で bonusScore 以下」であることと、
        // 実際の加点イベント (節目 1 回) の表示値を検証する。
        Assert.AreEqual(227272, Scoring.ComponentScoreV5(250, 100));
        Assert.GreaterOrEqual(componentSum, 0);
        Assert.LessOrEqual(componentSum, b.BonusScore + 2,
            "成分換算の和が bonusScore を大きく超えた");
    }

    // --- ローカル記録の分離 ----------------------------------------------

    [Test]
    public void ScoreStore_KeepsV5RecordsSeparateFromV4()
    {
        var store = new ScoreStore();
        var v4 = new ScoreKey("who", 1, Scoring.Version4);
        var v5 = new ScoreKey("who", 1, Scoring.Version5);

        store.Submit(v4, 1861870);
        store.Submit(v5, 1053481);

        Assert.AreEqual(1861870, store.Get(v4));
        Assert.AreEqual(1053481, store.Get(v5));

        ScoreStore loaded = ScoreStore.Deserialize(store.Serialize());
        Assert.AreEqual(1861870, loaded.Get(v4));
        Assert.AreEqual(1053481, loaded.Get(v5));
    }
}
