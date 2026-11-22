using System.Collections.Generic;
using NUnit.Framework;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Game.Hud;
using FlashBeat.UI.V6.Result;

namespace FlashBeat.V6HudTests
{
    /// <summary>
    /// Issue #870: スコアゲージとグレードの一致契約。
    ///
    /// グレード文字・ゲージ塗り・帯目盛りは全て
    /// <see cref="V6GradeLogic.ScoreFractionOf"/> の単一写像と
    /// <see cref="V6GradeLogic.GradeBandsOf"/> の帯から導かれる。
    /// このテストは「あるスコアのグレード文字」と「同じスコアがゲージ上で
    /// 属する帯」が全グレード境界で一致することを scoringVersion ごとに
    /// 固定する。さらに実際に描画される目盛り座標
    /// (<see cref="V6HudMetrics.GaugeSegX"/> / <c>GaugeSegW</c>) と
    /// 塗り幅 (<see cref="V6HudLogic.GaugeFillWidth"/>) が同じ割合空間に
    /// あることを検証する。
    /// </summary>
    public class V6GradeGaugeContractTests
    {
        // --- 共通: 帯の連続性 ---

        /// <summary>
        /// GradeBandsOf の帯が [0,1] を隙間・重複なく敷き詰めること。
        /// 帯が飛ぶと、その境界のスコアでグレード文字と目盛りがずれる。
        /// </summary>
        [Test]
        public void GradeBands_TileUnitInterval_ForEveryScoringVersion()
        {
            foreach (int version in new[] { 1, Scoring.Version4, Scoring.Version5, Scoring.Version6 })
            {
                var bands = V6GradeLogic.GradeBandsOf(version);
                Assert.Greater(bands.Count, 0, "version " + version);
                Assert.AreEqual(0f, bands[0].Start, 1e-6f, "version " + version + " first band start");
                for (int i = 1; i < bands.Count; i++)
                {
                    Assert.AreEqual(bands[i - 1].End, bands[i].Start, 1e-6f,
                        "version " + version + " gap/overlap between " + bands[i - 1].Label + " and " + bands[i].Label);
                }
                Assert.AreEqual(1f, bands[bands.Count - 1].End, 1e-6f,
                    "version " + version + " last band end");
            }
        }

        /// <summary>帯ラベルが C/B/A/S/SS の順で一意であること。</summary>
        [Test]
        public void GradeBands_LabelsAreUniqueAndOrdered()
        {
            string[] expected = { "C", "B", "A", "S", "SS" };
            foreach (int version in new[] { 1, Scoring.Version4, Scoring.Version5, Scoring.Version6 })
            {
                var bands = V6GradeLogic.GradeBandsOf(version);
                Assert.AreEqual(expected.Length, bands.Count, "version " + version + " band count");
                for (int i = 0; i < expected.Length; i++)
                {
                    Assert.AreEqual(expected[i], bands[i].Label, "version " + version + " band " + i);
                }
            }
        }

        // --- scoring v6: 譜面別満点の割合帯 ---

        /// <summary>
        /// v6 の各帯の内側 (境界から十分離れた 3 点) で、
        /// グレード文字と ScoreFractionOf の属する帯が一致すること。
        /// </summary>
        [Test]
        public void V6_GradeLetterAndGaugeBand_AgreeInsideEveryBand()
        {
            const int noteCount = 200;
            int max = Scoring.ScoreMaxV6(noteCount);
            var bands = V6GradeLogic.GradeBandsOf(Scoring.Version6);

            foreach (var band in bands)
            {
                if (band.IsPoint)
                {
                    continue;
                }

                float w = band.End - band.Start;
                foreach (float t in new[] { 0.2f, 0.5f, 0.8f })
                {
                    int score = (int)System.Math.Round((band.Start + w * t) * max);
                    if (score < 0 || score > max) continue;

                    string grade = V6GradeLogic.GradeOfV6(score, noteCount);
                    Assert.AreEqual(band.Label, grade,
                        "v6 score " + score + " (fraction "
                        + V6GradeLogic.ScoreFractionOf(score, Scoring.Version6, noteCount)
                        + ") should be grade " + band.Label);

                    float f = V6GradeLogic.ScoreFractionOf(score, Scoring.Version6, noteCount);
                    Assert.GreaterOrEqual(f, band.Start - 1e-6f, "score " + score);
                    Assert.Less(f, band.End + 1e-6f, "score " + score);
                }
            }
        }

        /// <summary>
        /// v6 の各境界割合 (B/A/S) で、境界以上の最小スコアが上位グレード、
        /// その 1 点下が下位グレードになること。境界の定義は
        /// GradeV6B/A/S の割合 >= 比較。
        /// </summary>
        [Test]
        public void V6_BoundaryScores_RoundUpToUpperGrade()
        {
            const int noteCount = 200;
            int max = Scoring.ScoreMaxV6(noteCount);

            AssertBoundary(Scoring.Version6, noteCount, max, V6GradeLogic.GradeV6B, "B", "C");
            AssertBoundary(Scoring.Version6, noteCount, max, V6GradeLogic.GradeV6A, "A", "B");
            AssertBoundary(Scoring.Version6, noteCount, max, V6GradeLogic.GradeV6S, "S", "A");
        }

        /// <summary>v6 で満点ちょうどだけが SS、1 点不足は S であること。</summary>
        [Test]
        public void V6_PerfectScoreIsSS_OneBelowIsS()
        {
            const int noteCount = 139;
            int max = Scoring.ScoreMaxV6(noteCount);
            Assert.AreEqual("SS", V6GradeLogic.GradeOfV6(max, noteCount));
            Assert.AreEqual("S", V6GradeLogic.GradeOfV6(max - 1, noteCount));
            Assert.AreEqual(1f, V6GradeLogic.ScoreFractionOf(max, Scoring.Version6, noteCount), 1e-6f);
            float below = V6GradeLogic.ScoreFractionOf(max - 1, Scoring.Version6, noteCount);
            Assert.GreaterOrEqual(below, V6GradeLogic.GradeV6S);
            Assert.Less(below, 1f);
        }

        // --- scoring v4/v5: 固定満点 2,000,000 の帯 ---

        /// <summary>v4 の全境界 (スコア閾値) で上下のグレードが切り替わること。</summary>
        [Test]
        public void V4_BoundaryScores_MatchGradeThresholds()
        {
            Assert.AreEqual("C", V6GradeLogic.GradeOfV4(V6GradeLogic.GradeV4B - 1));
            Assert.AreEqual("B", V6GradeLogic.GradeOfV4(V6GradeLogic.GradeV4B));
            Assert.AreEqual("B", V6GradeLogic.GradeOfV4(V6GradeLogic.GradeV4A - 1));
            Assert.AreEqual("A", V6GradeLogic.GradeOfV4(V6GradeLogic.GradeV4A));
            Assert.AreEqual("A", V6GradeLogic.GradeOfV4(V6GradeLogic.GradeV4S - 1));
            Assert.AreEqual("S", V6GradeLogic.GradeOfV4(V6GradeLogic.GradeV4S));
            Assert.AreEqual("S", V6GradeLogic.GradeOfV4(V6GradeLogic.ScoreMaxV4 - 1));
            Assert.AreEqual("SS", V6GradeLogic.GradeOfV4(V6GradeLogic.ScoreMaxV4));
        }

        /// <summary>v4 の帯内スコアでグレード文字と割合帯が一致すること。</summary>
        [Test]
        public void V4_GradeLetterAndGaugeBand_AgreeInsideEveryBand()
        {
            int max = V6GradeLogic.ScoreMaxV4;
            var bands = V6GradeLogic.GradeBandsOf(Scoring.Version4);
            foreach (var band in bands)
            {
                if (band.IsPoint) continue;
                float w = band.End - band.Start;
                foreach (float t in new[] { 0.2f, 0.5f, 0.8f })
                {
                    int score = (int)System.Math.Round((band.Start + w * t) * max);
                    if (score > max) continue;
                    Assert.AreEqual(band.Label, V6GradeLogic.GradeOfV4(score), "v4 score " + score);
                    float f = V6GradeLogic.ScoreFractionOf(score, Scoring.Version4);
                    Assert.GreaterOrEqual(f, band.Start - 1e-6f);
                    Assert.Less(f, band.End + 1e-6f);
                }
            }
        }

        // --- v1–v3 (legacy) ---

        /// <summary>legacy 帯の全境界で上下のグレードが切り替わること。</summary>
        [Test]
        public void Legacy_BoundaryScores_MatchRankThresholds()
        {
            Assert.AreEqual("C", V6ResultRules.RankFor(V6ResultRules.RankLegacyB - 1));
            Assert.AreEqual("B", V6ResultRules.RankFor(V6ResultRules.RankLegacyB));
            Assert.AreEqual("B", V6ResultRules.RankFor(V6ResultRules.RankLegacyA - 1));
            Assert.AreEqual("A", V6ResultRules.RankFor(V6ResultRules.RankLegacyA));
            Assert.AreEqual("A", V6ResultRules.RankFor(V6ResultRules.RankLegacyS - 1));
            Assert.AreEqual("S", V6ResultRules.RankFor(V6ResultRules.RankLegacyS));
            Assert.AreEqual("S", V6ResultRules.RankFor(V6ResultRules.RankLegacyMax - 1));
            Assert.AreEqual("SS", V6ResultRules.RankFor(V6ResultRules.RankLegacyMax));
        }

        /// <summary>legacy 帯内スコアでグレード文字と帯が一致すること。</summary>
        [Test]
        public void Legacy_GradeLetterAndGaugeBand_AgreeInsideEveryBand()
        {
            int max = V6GradeLogic.ScoreMaxLegacy;
            var bands = V6GradeLogic.GradeBandsOf(1);
            foreach (var band in bands)
            {
                if (band.IsPoint) continue;
                float w = band.End - band.Start;
                foreach (float t in new[] { 0.2f, 0.5f, 0.8f })
                {
                    int score = (int)System.Math.Round((band.Start + w * t) * max);
                    if (score > max) continue;
                    Assert.AreEqual(band.Label, V6ResultRules.RankFor(score), "legacy score " + score);
                    float f = V6GradeLogic.ScoreFractionOf(score, 1);
                    Assert.GreaterOrEqual(f, band.Start - 1e-6f);
                    Assert.Less(f, band.End + 1e-6f);
                }
            }
        }

        // --- ゲージの実 px 変換 ---

        /// <summary>
        /// GaugeFillWidth / GaugeBestX が ScoreFractionOf の単一写像を
        /// そのまま px へ写すこと (fillMax 倍、クランプ込み)。
        /// </summary>
        [Test]
        public void GaugeGeometry_UsesSingleFractionMapping()
        {
            const float fillMax = 480f;
            const int noteCount = 300;
            int max = Scoring.ScoreMaxV6(noteCount);
            int half = max / 2;

            GManager.chartNoteCount = noteCount;
            try
            {
                float expected = V6GradeLogic.ScoreFractionOf(half, Scoring.Version6, noteCount) * fillMax;
                Assert.AreEqual(expected, V6HudLogic.GaugeFillWidth(half, fillMax), 1e-4f);
                Assert.AreEqual(expected, V6HudLogic.GaugeBestX(half, fillMax), 1e-4f);

                Assert.LessOrEqual(V6HudLogic.GaugeFillWidth(max + 1000, fillMax), fillMax + 1e-4f);
                Assert.GreaterOrEqual(V6HudLogic.GaugeFillWidth(-5, fillMax), -1e-4f);
            }
            finally
            {
                GManager.chartNoteCount = 0;
            }
        }

        /// <summary>
        /// 実際に描画される目盛り座標 (V6HudMetrics.GaugeSegX/W) が
        /// GradeBandsOf の帯をトラック幅へ写したものであること。
        /// 目盛りと塗りが別の式で計算されていると境界でずれる。
        /// </summary>
        [Test]
        public void GaugeTicks_MatchGradeBandsGeometry()
        {
            var m = V6HudMetrics.ForResolution(1920f, 1080f);
            var bands = V6GradeLogic.GradeBandsOf(Scoring.LatestVersion);
            var nonPoint = new List<V6GradeLogic.GradeBand>();
            foreach (var b in bands)
            {
                if (!b.IsPoint) nonPoint.Add(b);
            }

            Assert.AreEqual(nonPoint.Count, m.GaugeSegX.Length);
            Assert.AreEqual(nonPoint.Count, m.GaugeSegW.Length);

            for (int i = 0; i < nonPoint.Count; i++)
            {
                float expectedX = nonPoint[i].Start * m.GaugeW;
                float expectedW = (nonPoint[i].End - nonPoint[i].Start) * m.GaugeW;
                Assert.AreEqual(expectedX, m.GaugeSegX[i], 2f,
                    "tick " + nonPoint[i].Label + " x (rounding tolerance 2px)");
                Assert.AreEqual(expectedW, m.GaugeSegW[i], 2f,
                    "tick " + nonPoint[i].Label + " width (rounding tolerance 2px)");
            }
        }

        /// <summary>
        /// HUD のゲージ塗りとグレード文字が同じスコア・同じノーツ数で
        /// 同じ帯を指すこと (Refresh が使う 2 つの入口の整合)。
        /// </summary>
        [Test]
        public void HudGradeAndGauge_ShareTheSameScoreFraction()
        {
            const int noteCount = 260;
            int max = Scoring.ScoreMaxV6(noteCount);
            GManager.chartNoteCount = noteCount;
            try
            {
                int score = (int)(max * 0.65f);
                string grade = V6GradeLogic.GradeOf(score, Scoring.LatestVersion, noteCount);
                Assert.AreEqual("B", grade);

                float fraction = V6HudLogic.GaugeFillWidth(score, 1000f) / 1000f;
                Assert.GreaterOrEqual(fraction, V6GradeLogic.GradeV6B);
                Assert.Less(fraction, V6GradeLogic.GradeV6A);
            }
            finally
            {
                GManager.chartNoteCount = 0;
            }
        }

        // --- Result 画面のノーツ数は HUD と同じ正本を使う (Issue #870) ---

        /// <summary>
        /// Result のグレード・ゲージは確定済み判定合計ではなく
        /// 譜面ノーツ数 (GManager.chartNoteCount) で正規化する。
        /// 判定未確定のノーツが残る終了 (途中離脱・未判定残り) でも
        /// HUD が見せていたグレードと Result が一致する。
        /// </summary>
        [Test]
        public void ResultNoteCount_UsesChartNoteCount_NotJudgedTotals()
        {
            const int chartNotes = 500;
            // 判定合計 (420) は譜面ノーツ数 (500) より少ない状況を模す。
            int noteCount = V6ResultLogic.ResultNoteCount(chartNotes, 400, 10, 5, 5);
            Assert.AreEqual(chartNotes, noteCount,
                "Result は確定判定合計ではなく譜面ノーツ数を使う");

            // chartNoteCount 未確定のときだけ判定合計へ退避する。
            Assert.AreEqual(420, V6ResultLogic.ResultNoteCount(0, 400, 10, 5, 5));
        }

        // --- ヘルパー ---

        /// <summary>
        /// 割合境界 boundary に対し、境界以上の最小スコアが upperGrade、
        /// その 1 点下が lowerGrade になることを検証する。
        /// </summary>
        private static void AssertBoundary(int version, int noteCount, int max,
            float boundary, string upperGrade, string lowerGrade)
        {
            // 境界スコアは「グレード関数自身が upperGrade を返す最小の
            // スコア」として求める。float 除算の丸めをテスト側で再実装
            // すると実装と 1 点ずれ得るため、判定は GradeOfV6/GradeOfV4
            // に委ねる。探索窓は境界割合 ±4 スコア (粒度は 1 スコア)。
            int center = (int)(boundary * max);
            int at = -1;
            for (int s = System.Math.Max(1, center - 4); s <= center + 4; s++)
            {
                string g = version >= Scoring.Version6
                    ? V6GradeLogic.GradeOfV6(s, noteCount)
                    : V6GradeLogic.GradeOfV4(s);
                if (g == upperGrade)
                {
                    at = s;
                    break;
                }
            }

            Assert.Greater(at, 0,
                "version " + version + " boundary " + boundary + ": no score near " + center + " yields " + upperGrade);

            // その 1 点下は下位グレード (score=1 のときはこれ以上下げられない)。
            if (at > 1)
            {
                int below = at - 1;
                string gradeBelow = version >= Scoring.Version6
                    ? V6GradeLogic.GradeOfV6(below, noteCount)
                    : V6GradeLogic.GradeOfV4(below);
                Assert.AreEqual(lowerGrade, gradeBelow,
                    "version " + version + " score " + below + " (below boundary) should be " + lowerGrade);
            }
        }
    }
}
