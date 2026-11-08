using NUnit.Framework;
using FlashBeat.UI.V6.Result;

namespace FlashBeat.UI.V6.Result.Tests
{
    /// <summary>V6ResultLogic (Issue #217) の純ロジック。グレード算出と表示形式。</summary>
    public sealed class V6ResultLogicTests
    {
        // --- DesignScale (v6 共通: 1728x972 基準, min 倍率) ---

        [Test]
        public void DesignScale_Fhd_ReturnsUniformScale()
        {
            Assert.AreEqual(1920f / 1728f, V6ResultLogic.DesignScale(1920, 1080), 0.0001f);
        }

        [Test]
        public void DesignScale_2k_And_Mac_HeightIsBinding()
        {
            float expected = 1440f / 972f;
            Assert.AreEqual(expected, V6ResultLogic.DesignScale(2560, 1440), 0.0001f);
            Assert.AreEqual(expected, DesignSafe(2560, 1664), 0.0001f);
        }

        [Test]
        public void DesignScale_InvalidSize_FallsBackToOne()
        {
            Assert.AreEqual(1f, V6ResultLogic.DesignScale(0, 1080));
            Assert.AreEqual(1f, V6ResultLogic.DesignScale(1920, -1));
        }

        private static float DesignSafe(int w, int h)
        {
            return V6ResultLogic.DesignScale(w, h);
        }

        // --- GradeOf (既存の Assessment 判定 RankFor と 1:1) ---

        [Test]
        public void GradeOf_MatchesExistingAssessmentThresholds()
        {
            Assert.AreEqual("SS", V6ResultLogic.GradeOf(1000000));
            Assert.AreEqual("S", V6ResultLogic.GradeOf(950000));
            Assert.AreEqual("A", V6ResultLogic.GradeOf(900000));
            Assert.AreEqual("B", V6ResultLogic.GradeOf(700000));
            Assert.AreEqual("C", V6ResultLogic.GradeOf(100));
        }

        [Test]
        public void GradeOf_BoundaryValues()
        {
            Assert.AreEqual("SS", V6ResultLogic.GradeOf(1000000));
            Assert.AreEqual("S", V6ResultLogic.GradeOf(999999));
            Assert.AreEqual("C", V6ResultLogic.GradeOf(699999));
            Assert.AreEqual("–", V6ResultLogic.GradeOf(0));
            Assert.AreEqual("–", V6ResultLogic.GradeOf(-1));
        }

        // --- FormatScore / DeltaLabel ---

        [Test]
        public void FormatScore_UsesCommaGrouping()
        {
            Assert.AreEqual("988,513", V6ResultLogic.FormatScore(988513));
            Assert.AreEqual("0", V6ResultLogic.FormatScore(0));
        }

        [Test]
        public void DeltaLabel_PositiveWhenBeatenOnly()
        {
            Assert.AreEqual("+66,633", V6ResultLogic.DeltaLabel(921880, 988513));
            Assert.AreEqual(string.Empty, V6ResultLogic.DeltaLabel(988513, 921880));
            Assert.AreEqual(string.Empty, V6ResultLogic.DeltaLabel(500, 500));
        }

        // --- ゲージ幾何 (design 実測値との整合) ---

        [Test]
        public void GaugeFillWidth_MapsScoreRatioToDesignLength()
        {
            // design 実測: 988,513 → 815px (824 * 988513/1000000 = 814.94)
            Assert.AreEqual(824f * 988513f / 1000000f, V6ResultLogic.GaugeFillWidth(988513), 0.01f);
            Assert.AreEqual(0f, V6ResultLogic.GaugeFillWidth(0));
        }

        [Test]
        public void GaugeFillWidth_ClampsToTrackLength()
        {
            Assert.AreEqual(V6ResultLogic.GaugeW, V6ResultLogic.GaugeFillWidth(1200000));
        }

        [Test]
        public void GaugeBestX_MatchesDesignSample()
        {
            // design 実測: 前回最高 921,880 → x=760
            Assert.AreEqual(824f * 921880f / 1000000f, V6ResultLogic.GaugeBestX(921880), 0.01f);
        }

        // --- 曲長表示 ---

        [Test]
        public void DurationText_FormatsMinutesSeconds()
        {
            Assert.AreEqual("1:29", V6ResultLogic.DurationText(89f));
            Assert.AreEqual("1:03", V6ResultLogic.DurationText(63f));
            Assert.AreEqual("–", V6ResultLogic.DurationText(0f));
            Assert.AreEqual("–", V6ResultLogic.DurationText(-5f));
        }
    }
}
