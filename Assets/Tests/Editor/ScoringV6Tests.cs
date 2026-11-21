using NUnit.Framework;
using FlashBeat.Gameplay;
using FlashBeat.Persistence;
using FlashBeat.UI.V6.Common;

/// <summary>
/// scoring v6 (ADR-0037 / Issue #825) の EditMode テスト。
///
/// 検証する契約:
///
///   - 式: 基礎スコア = floor(judgment * 1,000,000 / (5 * noteCount))
///     (上限 1,000,000、v4/v5 と同じ)。追加スコアは絶対点:
///     qualityScore (P*1000 + G*600 + B*200) + milestoneScore
///     (comboMilestones * 5000) + clearScore (judgmentsComplete && miss==0
///     ? 25000 : 0)。score = baseScore + additionalScore。
///   - MaxAdditionalV6(N) = N*1000 + floor(N/50)*5000 + 25000 は
///     noteCount に対して単調増加 — 長い譜面ほど獲得可能な追加スコアの
///     上限が大きい (v4/v5 の固定 1,000,000 正規化では表せなかった性質)。
///   - 全 Perfect・フルコンボ・miss==0 のプレイは score == ScoreMaxV6(N)
///     (その譜面の満点)。SS はこの満点到達のみ。
///   - グレードは譜面満点に対する割合: B ≥ 55% / A ≥ 75% / S ≥ 90% /
///     SS = 満点のみ。分布シミュレーションの結果、A 以上が実プレイで
///     到達可能な帯であることを GradeOfV6_SimulationVectors で固定する。
///   - ローカル記録は scoringVersion で分離される ("@6" = v6)。
///   - v1〜v5 の式・記録は変更しない (ScoringV2〜V5Tests が別途固定)。
/// </summary>
public class ScoringV6Tests
{
    // --- 式 -------------------------------------------------------------

    [Test]
    public void MaxAdditionalV6_MatchesContractFormula()
    {
        // N=100: 100*1000 + 2*5000 + 25000 = 135,000。
        Assert.AreEqual(135000, Scoring.MaxAdditionalV6(100));
        // N=49: 節目なし → 49*1000 + 25000 = 74,000。
        Assert.AreEqual(74000, Scoring.MaxAdditionalV6(49));
        // N=50: floor(50/50)=1 → 50*1000 + 5000 + 25000 = 80,000。
        Assert.AreEqual(80000, Scoring.MaxAdditionalV6(50));
        // N=139 (Who): 139*1000 + 2*5000 + 25000 = 174,000。
        Assert.AreEqual(174000, Scoring.MaxAdditionalV6(139));
        // N=600: 600*1000 + 12*5000 + 25000 = 685,000。
        Assert.AreEqual(685000, Scoring.MaxAdditionalV6(600));
        // 空の譜面は追加スコアを持たない (サーバも noteCount<=0 を拒否)。
        Assert.AreEqual(0, Scoring.MaxAdditionalV6(0));
        Assert.AreEqual(0, Scoring.MaxAdditionalV6(-5));
        Assert.AreEqual(0, Scoring.ScoreV6(0, 0, 0, 0, 0, 0, 0, true));
    }

    [Test]
    public void MaxAdditionalV6_GrowsWithChartLength()
    {
        // ADR-0037 の中核要件: 長い譜面ほど獲得可能な追加スコアの上限が
        // 大きい。v4/v5 のボーナス上限は全譜面で 1,000,000 固定だった。
        Assert.Greater(Scoring.MaxAdditionalV6(600), Scoring.MaxAdditionalV6(139));
        Assert.Greater(Scoring.MaxAdditionalV6(139), Scoring.MaxAdditionalV6(50));
        Assert.Greater(Scoring.MaxAdditionalV6(50), Scoring.MaxAdditionalV6(10));
        // 具体的な数値例 (テストで証明):
        //   N=139 (Who): 追加上限 174,000 → 譜面満点 1,174,000。
        //   N=600:      追加上限 685,000 → 譜面満点 1,685,000。
        //   N=1000:     追加上限 1,125,000 → 譜面満点 2,125,000
        //               (v4/v5 の固定上限 2,000,000 を超える)。
        Assert.AreEqual(1174000, Scoring.ScoreMaxV6(139));
        Assert.AreEqual(1685000, Scoring.ScoreMaxV6(600));
        Assert.AreEqual(2125000, Scoring.ScoreMaxV6(1000));
        Assert.Greater(Scoring.ScoreMaxV6(1000), 2000000,
            "長い譜面の v6 満点が v4/v5 の固定上限を超えていない");
    }

    [Test]
    public void BreakdownV6_SplitsBaseAndAbsoluteAdditional()
    {
        // P=60 G=10 B=0 M=1, maxCombo=70, milestones=1, N=100, miss>0。
        // judgment = 330 → baseScore = floor(330*1e6/500) = 660,000。
        // qualityScore = 60*1000 + 10*600 = 66,000。
        // milestoneScore = 1*5000。clearScore = 0 (miss>0)。
        // additionalScore = 71,000 → score = 731,000。
        // maxCombo=70 は v6 の式に入らない (連続コンボ加点なし、v5 と同じ)。
        Scoring.V6Breakdown b = Scoring.BreakdownV6(60, 10, 0, 1, 70, 1, 100, false);
        Assert.AreEqual(330f, b.Judgment, 0.0001f);
        Assert.AreEqual(66000, b.QualityScore);
        Assert.AreEqual(5000, b.MilestoneScore);
        Assert.AreEqual(0, b.ClearScore);
        Assert.AreEqual(71000, b.AdditionalScore);
        Assert.AreEqual(660000, b.BaseScore);
        Assert.AreEqual(731000, b.Score);
        Assert.AreEqual(135000, b.MaxAdditional);
        Assert.AreEqual(1135000, b.MaxScore);
    }

    [Test]
    public void MaxComboAlone_EarnsNoAdditionalScore()
    {
        // v6 に連続コンボボーナスは無い: maxCombo を変えても score は
        // 変わらない (コンボ由来の加点は節目到達のみ)。
        int withCombo10 = Scoring.ScoreV6(10, 0, 0, 0, 10, 0, 10, false);
        int withCombo5 = Scoring.ScoreV6(10, 0, 0, 0, 5, 0, 10, false);
        Assert.AreEqual(withCombo10, withCombo5,
            "maxCombo の違いが v6 スコアに影響した (連続コンボ加点が残っている)");
        // baseScore = floor(50*1e6/50) = 1,000,000、qualityScore = 10,000、
        // judgmentsComplete=false なので clearScore = 0 → 1,010,000。
        Assert.AreEqual(1010000, withCombo10);
    }

    [Test]
    public void EachMilestoneCrossing_VestsFixedAbsoluteBonus()
    {
        // N=200、P=150 G=50 M=0、確定済み miss==0。
        // baseScore = floor((750+150)*1e6/1000) = 900,000。
        // qualityScore = 150*1000 + 50*600 = 180,000。
        // clearScore = 25,000 (miss==0 && judgmentsComplete)。
        // 節目なしの合計 = 1,105,000。節目ごとに +5,000 の絶対点
        // (正規化なし — v4/v5 の様に floor の端数で揺れない)。
        int[] expected = { 1105000, 1110000, 1115000, 1120000, 1125000, 1130000 };
        for (int ms = 0; ms <= 5; ms++)
        {
            Assert.AreEqual(expected[ms],
                Scoring.ScoreV6(150, 50, 0, 0, 200, ms, 200, true),
                $"節目 {ms} 回のスコアが契約オラクルと一致しない");
        }
    }

    [Test]
    public void PerfectFullCombo_ScoresChartMaximum()
    {
        // 全 Perfect・miss==0・確定済み:
        //   baseScore = 1,000,000 (judgment = 5N の上限)
        //   additionalScore = N*1000 + floor(N/50)*5000 + 25000
        //                    = MaxAdditionalV6(N)
        //   → score = ScoreMaxV6(N) = その譜面の満点。
        // 複数のノーツ数で検証 (長い譜面ほど満点が大きい)。
        foreach (int n in new[] { 10, 49, 50, 139, 600, 1000 })
        {
            int ms = n / Scoring.MilestoneInterval;
            Assert.AreEqual(Scoring.ScoreMaxV6(n),
                Scoring.ScoreV6(n, 0, 0, 0, n, ms, n, true),
                $"N={n}: 全パーフェクトのスコアが譜面満点と一致しない");
        }
        // 具体値: N=139 → 1,174,000。N=600 → 1,685,000 (> v4/v5 の 2,000,000 ではないが譜面満点)。
        Assert.AreEqual(1174000, Scoring.ScoreV6(139, 0, 0, 0, 139, 2, 139, true));
        Assert.AreEqual(1685000, Scoring.ScoreV6(600, 0, 0, 0, 600, 12, 600, true));
    }

    [Test]
    public void MissedPlay_LosesQualityMilestoneAndClearButKeepsEarned()
    {
        // miss>0: clearScore は 0 になるが、確定済みの comboMilestones と
        // それまでに獲得した qualityScore は残る (獲得済みボーナスは
        // 失われない、v5 と同じ契約)。
        Scoring.V6Breakdown missed = Scoring.BreakdownV6(4, 3, 2, 1, 6, 1, 10, true);
        Assert.AreEqual(5000, missed.MilestoneScore, "miss 後も節目ボーナスが消えた");
        Assert.AreEqual(0, missed.ClearScore, "miss 後にフルクリアボーナスが乗った");
        Assert.AreEqual(4000 + 1800 + 400, missed.QualityScore);

        // 同じ節目数で miss の有無だけが違う入力: miss 側は必ず低い。
        Assert.Less(
            Scoring.ScoreV6(4, 3, 2, 1, 6, 1, 10, true),
            Scoring.ScoreV6(4, 3, 2, 0, 6, 1, 10, true),
            "Miss 後のスコアが Miss 前を下回っていない");
    }

    [Test]
    public void ClearScore_VestsOnlyWhenJudgmentsComplete()
    {
        // Issue #618 の契約は v6 でも同じ: miss==0 でも全ノーツの判定が
        // 確定するまではフルクリアボーナスは乗らない。
        Scoring.V6Breakdown live = Scoring.BreakdownV6(4, 3, 2, 0, 6, 0, 10, false);
        Assert.AreEqual(0, live.ClearScore, "途中経過にフルクリアボーナスが乗った");

        Scoring.V6Breakdown done = Scoring.BreakdownV6(4, 3, 2, 0, 6, 0, 10, true);
        Assert.AreEqual(25000, done.ClearScore);
        Assert.Less(Scoring.ScoreV6(4, 3, 2, 0, 6, 0, 10, false),
            Scoring.ScoreV6(4, 3, 2, 0, 6, 0, 10, true),
            "確定前後でスコアが増えていない");
    }

    [Test]
    public void BaseScore_NeverExceedsOneMillion_ScoreNeverExceedsChartMax()
    {
        // 基礎スコアは判定由来のみ: 全 Perfect でも 1,000,000 で頭打ち。
        // 合計はその譜面の満点 ScoreMaxV6(N) を超えない
        // (正当な入力の範囲で。節目数を上限超過する改ざん相当の入力でも
        // quality+milestone+clear は MaxAdditionalV6 の各項を超えない
        // ことを式の構造で保証する — milestone は floor(N/50) 以下、
        // quality は判定数 N 以下を前提とする)。
        foreach (int n in new[] { 1, 10, 49, 50, 139, 300, 600, 1000 })
        {
            Scoring.V6Breakdown b = Scoring.BreakdownV6(n, 0, 0, 0, n, n / 50, n, true);
            Assert.LessOrEqual(b.BaseScore, 1000000, $"N={n}: baseScore が 1,000,000 を超えた");
            Assert.LessOrEqual(b.Score, b.MaxScore, $"N={n}: score が譜面満点を超えた");
            Assert.AreEqual(b.MaxScore, Scoring.ScoreMaxV6(n));
            Assert.GreaterOrEqual(b.Score, 0);
        }
    }

    // --- グレード帯 (ADR-0037) -------------------------------------------

    [Test]
    public void GradeOfV6_SS_OnlyAtChartMaximum()
    {
        int max139 = Scoring.ScoreMaxV6(139); // 1,174,000
        Assert.AreEqual("SS", V6GradeLogic.GradeOfV6(max139, 139));
        // SS は「その譜面の満点ちょうど」のみの特別評価 (ADR-0037)。
        // 満点超過のスコアは API が拒否するが、表示側でも SS にはしない
        // (割合が 1 を超えるので S 帯にクランプされる)。
        Assert.AreEqual("S", V6GradeLogic.GradeOfV6(max139 + 1, 139),
            "満点超過を SS にしてはいけない (SS は厳密な満点一致のみ)");
        // 満点 - 1 は SS ではない: S 帯 (≥90%) に落ちる。
        Assert.AreEqual("S", V6GradeLogic.GradeOfV6(max139 - 1, 139));
        // 別のノーツ数では同じスコアでもグレードが変わる (譜面満点依存)。
        // N=139 の満点 1,174,000 は N=600 の満点 1,685,000 の 69.7% → B。
        Assert.AreEqual("B", V6GradeLogic.GradeOfV6(max139, 600),
            "N=139 の満点スコアを N=600 の譜面に適用すると SS にならない");
    }

    [Test]
    public void GradeOfV6_Bands_AreFractionsOfChartMax()
    {
        int max = Scoring.ScoreMaxV6(139); // 1,174,000
        // 帯境界は整数で固定する (float の丸めで境界を跨がないように)。
        // B ≥ 55% → 645,700 / A ≥ 75% → 880,500 / S ≥ 90% → 1,056,600。
        Assert.AreEqual("B", V6GradeLogic.GradeOfV6(645700, 139));
        Assert.AreEqual("A", V6GradeLogic.GradeOfV6(880500, 139));
        Assert.AreEqual("S", V6GradeLogic.GradeOfV6(1056600, 139));
        Assert.AreEqual("C", V6GradeLogic.GradeOfV6(645699, 139));
        Assert.AreEqual("B", V6GradeLogic.GradeOfV6(880499, 139));
        Assert.AreEqual("A", V6GradeLogic.GradeOfV6(1056599, 139));
        // noteCount 不明 (0 以下) は保守側 (満点を実質無限大扱い → C)。
        Assert.AreEqual("C", V6GradeLogic.GradeOfV6(1174000, 0));
    }

    /// <summary>
    /// グレード帯の分布シミュレーション (ADR-0037 の検証要件)。
    /// 複数のノーツ数 × 判定品質 × ミス率の組み合わせで代表的なスコアを
    /// 生成し、A 以上が「上手いが完璧ではない」プレイで到達可能で、
    /// SS が満点プレイのみに限られることを固定する。
    /// </summary>
    [Test]
    public void GradeOfV6_SimulationVectors_APlusIsReachable()
    {
        // シナリオ: (noteCount, P率, G率, B率, miss率, 期待グレード帯)
        // miss 率は残りを Miss に割り当て、節目数は hits/50 で近似
        // (コンボ切断で実際は減るが、帯の検証には十分な近似)。
        var scenarios = new[]
        {
            // 全パーフェクト FC → SS (満点)。
            new { n = 139, p = 1.0, g = 0.0, b = 0.0, miss = 0.0, minGrade = "SS" },
            // 95% Perfect / 5% Great、ノーミス → S 以上。
            new { n = 139, p = 0.95, g = 0.05, b = 0.0, miss = 0.0, minGrade = "S" },
            // 85% Perfect / 15% Great、ノーミス → A 以上。
            new { n = 139, p = 0.85, g = 0.15, b = 0.0, miss = 0.0, minGrade = "A" },
            // 70% Perfect / 25% Great / 5% Bad、ノーミス → A 以上。
            new { n = 139, p = 0.70, g = 0.25, b = 0.05, miss = 0.0, minGrade = "A" },
            // 上記と同じ判定質で miss 5% → A 以上 (ミスがあっても A に届く)。
            new { n = 139, p = 0.66, g = 0.24, b = 0.05, miss = 0.05, minGrade = "A" },
            // 同じ判定質で miss 10% → B 以上。
            new { n = 139, p = 0.63, g = 0.23, b = 0.04, miss = 0.10, minGrade = "B" },
            // 長い譜面 (600 ノーツ) でも同じ帯構造: 85/15 ノーミス → A。
            new { n = 600, p = 0.85, g = 0.15, b = 0.0, miss = 0.0, minGrade = "A" },
            // 長い譜面で miss 5% → A。
            new { n = 600, p = 0.81, g = 0.14, b = 0.0, miss = 0.05, minGrade = "A" },
        };

        var rank = new System.Collections.Generic.Dictionary<string, int>
        {
            ["C"] = 0, ["B"] = 1, ["A"] = 2, ["S"] = 3, ["SS"] = 4,
        };

        foreach (var s in scenarios)
        {
            int p = (int)(s.n * s.p);
            int g = (int)(s.n * s.g);
            int b = (int)(s.n * s.b);
            int m = (int)(s.n * s.miss);
            // 残りは Bad に寄せて合計を n に合わせる (端数処理)。
            int sum = p + g + b + m;
            if (sum < s.n) b += s.n - sum;
            int hits = p + g + b;
            int ms = hits / Scoring.MilestoneInterval;
            int score = Scoring.ScoreV6(p, g, b, m, hits, ms, s.n, true);
            string grade = V6GradeLogic.GradeOfV6(score, s.n);
            float frac = score / (float)Scoring.ScoreMaxV6(s.n);
            Assert.GreaterOrEqual(rank[grade], rank[s.minGrade],
                $"N={s.n} P{p} G{g} B{b} M{m}: score={score} " +
                $"({frac:P1} of max) grade={grade}, expected >= {s.minGrade}");
        }
    }

    /// <summary>
    /// ADR-0037 の分布検証をスイープで固定する: 複数のノーツ数
    /// (短い 50 / 中間 139 / 長い 600, 1000) × 判定品質 × ミス率で
    /// スコアを生成し、(a) 「上手いが完璧ではない」プレイが A 以上に
    /// 現実的に届くこと、(b) SS が満点プレイのみに限られること、
    /// (c) 極端に悪いプレイが C に落ちることを確認する。
    /// 節目数は hits/50 の近似 (コンボ切断で実際は減るが帯の検証には十分)。
    /// </summary>
    [Test]
    public void GradeOfV6_DistributionSweep_APlusIsNotUnrealisticallyStrict()
    {
        var rank = new System.Collections.Generic.Dictionary<string, int>
        {
            ["C"] = 0, ["B"] = 1, ["A"] = 2, ["S"] = 3, ["SS"] = 4,
        };

        int[] noteCounts = { 50, 139, 300, 600, 1000 };
        // (P率, G率, B率, miss率) — 残りは Bad に寄せる。
        var qualities = new[]
        {
            new { p = 1.00, g = 0.00, b = 0.00, miss = 0.00 }, // 全パーフェクト
            new { p = 0.95, g = 0.05, b = 0.00, miss = 0.00 }, // 高品質ノーミス
            new { p = 0.85, g = 0.15, b = 0.00, miss = 0.00 }, // 標準的な良プレイ
            new { p = 0.70, g = 0.25, b = 0.05, miss = 0.00 }, // 平均的プレイ
            new { p = 0.60, g = 0.25, b = 0.10, miss = 0.05 }, // ミスあり良プレイ
            new { p = 0.40, g = 0.30, b = 0.20, miss = 0.10 }, // 苦しいプレイ
            new { p = 0.10, g = 0.20, b = 0.30, miss = 0.40 }, // 大量ミス
        };
        // 各品質帯の期待下限 (ノーツ数に依らず成り立つべき帯)。
        string[] minGrades = { "SS", "S", "A", "A", "B", "C", "C" };

        foreach (int n in noteCounts)
        {
            for (int qi = 0; qi < qualities.Length; qi++)
            {
                var q = qualities[qi];
                int p = (int)(n * q.p);
                int g = (int)(n * q.g);
                int b = (int)(n * q.b);
                int m = (int)(n * q.miss);
                int sum = p + g + b + m;
                if (sum < n) b += n - sum;
                int hits = p + g + b;
                int ms = hits / Scoring.MilestoneInterval;
                int score = Scoring.ScoreV6(p, g, b, m, hits, ms, n, true);
                string grade = V6GradeLogic.GradeOfV6(score, n);
                Assert.GreaterOrEqual(rank[grade], rank[minGrades[qi]],
                    $"N={n} P{p} G{g} B{b} M{m}: score={score} " +
                    $"({score / (float)Scoring.ScoreMaxV6(n):P1} of max) " +
                    $"grade={grade}, expected >= {minGrades[qi]}");
            }

            // SS は全パーフェクト満点のみ: 満点 - 1 は必ず SS 未満。
            int maxScore = Scoring.ScoreMaxV6(n);
            Assert.AreEqual("SS", V6GradeLogic.GradeOfV6(maxScore, n), $"N={n}");
            Assert.AreNotEqual("SS", V6GradeLogic.GradeOfV6(maxScore - 1, n), $"N={n}");
        }
    }

    [Test]
    public void LatestVersion_Is6_AndScoreStoreSeparatesV6()
    {
        Assert.AreEqual(Scoring.Version6, Scoring.LatestVersion);

        // ローカル記録は scoringVersion で分離される ("@6" = v6)。
        var store = new ScoreStore();
        var v5 = new ScoreKey("who", 1, Scoring.Version5);
        var v6 = new ScoreKey("who", 1, Scoring.Version6);

        store.Submit(v5, 2000000);
        store.Submit(v6, 1174000);

        Assert.AreEqual(2000000, store.Get(v5));
        Assert.AreEqual(1174000, store.Get(v6));

        ScoreStore loaded = ScoreStore.Deserialize(store.Serialize());
        Assert.AreEqual(2000000, loaded.Get(v5));
        Assert.AreEqual(1174000, loaded.Get(v6));
    }
}
