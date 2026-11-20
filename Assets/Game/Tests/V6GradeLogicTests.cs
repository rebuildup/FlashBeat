using NUnit.Framework;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.V5CanvasTests
{
    /// <summary>
    /// V6GradeLogic (Issue #758 / ADR-0034) の純ロジック。
    /// scoringVersion ごとのスコア満点と v4 グレード帯、および実際の
    /// scoring v4 計算 (<see cref="Scoring.ScoreV4"/>) が生むスコアが
    /// 意図したグレード帯に入ることを検証する。
    /// </summary>
    public sealed class V6GradeLogicTests
    {
        // --- スコア満点 ---

        [Test]
        public void ScoreMaxOf_FollowsScoringVersion()
        {
            Assert.AreEqual(1000000, V6GradeLogic.ScoreMaxOf(Scoring.Version1));
            Assert.AreEqual(1000000, V6GradeLogic.ScoreMaxOf(Scoring.Version2));
            Assert.AreEqual(1000000, V6GradeLogic.ScoreMaxOf(Scoring.Version3));
            Assert.AreEqual(2000000, V6GradeLogic.ScoreMaxOf(Scoring.Version4));
            Assert.AreEqual(2000000, V6GradeLogic.ScoreMaxOf(Scoring.LatestVersion));
        }

        // --- v4 グレード帯 (境界値) ---

        [Test]
        public void GradeOfV4_BandBoundaries()
        {
            Assert.AreEqual("SS", V6GradeLogic.GradeOfV4(2000000));
            Assert.AreEqual("S", V6GradeLogic.GradeOfV4(1999999));
            Assert.AreEqual("S", V6GradeLogic.GradeOfV4(1900000));
            Assert.AreEqual("A", V6GradeLogic.GradeOfV4(1899999));
            Assert.AreEqual("A", V6GradeLogic.GradeOfV4(1800000));
            Assert.AreEqual("B", V6GradeLogic.GradeOfV4(1799999));
            Assert.AreEqual("B", V6GradeLogic.GradeOfV4(1600000));
            Assert.AreEqual("C", V6GradeLogic.GradeOfV4(1599999));
            Assert.AreEqual("C", V6GradeLogic.GradeOfV4(0));
            Assert.AreEqual("C", V6GradeLogic.GradeOfV4(-1));
        }

        [Test]
        public void GradeOf_DispatchesByScoringVersion()
        {
            // RankFor 系の帯: v4 以降は 2M 帯、v1–v3 は RankFor 閾値。
            Assert.AreEqual("SS", V6GradeLogic.GradeOf(2000000, Scoring.Version4));
            Assert.AreEqual("B", V6GradeLogic.GradeOf(1600000, Scoring.LatestVersion));
            // 同じスコアでも scoringVersion で帯が変わる: 950,000 は v4 帯では
            // C (B 境界 1,600,000 未満)、v1–v3 の RankFor では S (≥950,000)。
            Assert.AreEqual("C", V6GradeLogic.GradeOf(950000, Scoring.LatestVersion));
            Assert.AreEqual("S", V6GradeLogic.GradeOf(950000, Scoring.Version3));
            Assert.AreEqual("SS", V6GradeLogic.GradeOf(1000000, Scoring.Version3));
            Assert.AreEqual("B", V6GradeLogic.GradeOf(700000, Scoring.Version1));
        }

        // --- 実際の scoring v4 スコアが帯に収まること ---

        /// <summary>判定内訳から v4 スコアを計算するテスト用ヘルパ。</summary>
        private static int V4Score(int perfect, int great, int bad, int miss,
            int maxCombo, int comboMilestones, int noteCount, bool judgmentsComplete)
        {
            return Scoring.ScoreV4(perfect, great, bad, miss, maxCombo,
                comboMilestones, noteCount, judgmentsComplete);
        }

        [Test]
        public void GradeOfV4_PerfectFullCombo_IsSS()
        {
            // 全 Perfect + フルコンボ終了: 基礎 1,000,000 + ボーナス 1,000,000。
            int score = V4Score(100, 0, 0, 0, 100, 2, 100, true);
            Assert.AreEqual(2000000, score);
            Assert.AreEqual("SS", V6GradeLogic.GradeOfV4(score));
        }

        [Test]
        public void GradeOfV4_NoMissPlay_LandsOnJudgmentQualityBands()
        {
            // ノーミスのクリアはボーナスが満点 1,000,000 に張り付くため、
            // グレードは実質「基礎スコア (判定の質)」で決まる。
            // All-Great FC: 基礎 600,000 → 1,600,000 = B 境界。
            int allGreat = V4Score(0, 100, 0, 0, 100, 2, 100, true);
            Assert.AreEqual(1600000, allGreat);
            Assert.AreEqual("B", V6GradeLogic.GradeOfV4(allGreat));

            // Perfect 50 / Great 50: 基礎 800,000 → 1,800,000 = A 境界。
            int halfGreat = V4Score(50, 50, 0, 0, 100, 2, 100, true);
            Assert.AreEqual(1800000, halfGreat);
            Assert.AreEqual("A", V6GradeLogic.GradeOfV4(halfGreat));

            // Perfect 90 / Great 10: 基礎 960,000 → 1,960,000 = S。
            int mostlyPerfect = V4Score(90, 10, 0, 0, 100, 2, 100, true);
            Assert.GreaterOrEqual(mostlyPerfect, 1900000);
            Assert.Less(mostlyPerfect, 2000000);
            Assert.AreEqual("S", V6GradeLogic.GradeOfV4(mostlyPerfect));
        }

        [Test]
        public void GradeOfV4_MissPlay_StaysBelowB()
        {
            // miss を含むプレイはノーミス加点・終了加点を失い、コンボ継続も
            // そこで打ち切られるためボーナスが大きく削られる。終盤 miss の
            // 最良ケース (残り全 Perfect) でも B 未満 = C に留まることを、
            // 様々なノーツ数で確認する (v1–v3 の「miss = 実効 C」と同じ峻別)。
            int[] noteCounts = { 1, 2, 10, 50, 51, 100, 500, 1000 };
            foreach (int n in noteCounts)
            {
                // 最後の 1 ノーツだけ miss、他は全 Perfect、コンボは
                // N-1 まで継続した最良ケース。
                int score = V4Score(n - 1, 0, 0, 1, n - 1, (n - 1) / 50, n, true);
                Assert.Less(score, V6GradeLogic.GradeV4B,
                    "miss を含むプレイは B 未満 (n=" + n + ", score=" + score + ")");
                Assert.AreEqual("C", V6GradeLogic.GradeOfV4(score), "n=" + n);
            }
        }

        [Test]
        public void GradeOfV4_MissEarlyPlay_IsC()
        {
            // 序盤で miss したプレイはコンボ継続もほぼ無く、さらに低い。
            int score = V4Score(49, 0, 0, 1, 10, 0, 100, true);
            Assert.AreEqual("C", V6GradeLogic.GradeOfV4(score));
        }
    }
}
