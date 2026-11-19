using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6.Result;

namespace FlashBeat.UI.V6.Result.Tests
{
    /// <summary>V6ResultLogic (Issue #217) の純ロジック。グレード算出と表示形式。</summary>
    public sealed class V6ResultLogicTests
    {
        // --- FrameOrigin (design s88VZk "Resolution Check · Result" の実配置) ---
        // 旧 DesignScale (min 倍率の引き伸ばし) のテストは、Owner 指示「design フレームは
        // 固定解像度・引き伸ばしは起こらない」により契約変更になったため置換。
        // 正本: design s88VZk の各解像度フレームに 05 Result · v6 (zcSHh) が
        // FHD (96,54) / 2K (416,234) / Mac (416,346) に 1:1 で載る実配置。

        [Test]
        public void FrameOrigin_MatchesDesignResolutionTable()
        {
            Assert.AreEqual(new Vector2(96f, 54f), V6ResultLogic.FrameOrigin(1920, 1080));
            Assert.AreEqual(new Vector2(416f, 234f), V6ResultLogic.FrameOrigin(2560, 1440));
            Assert.AreEqual(new Vector2(416f, 346f), V6ResultLogic.FrameOrigin(2560, 1664));
        }

        [Test]
        public void FrameOrigin_IsCenteredFloorFormula()
        {
            // ((w−1728)/2, (h−972)/2) の整数切り捨て (design フレーム自体で (0,0))。
            Assert.AreEqual(Vector2.zero, V6ResultLogic.FrameOrigin(1728, 972));
            Assert.AreEqual(new Vector2(96f, 114f), V6ResultLogic.FrameOrigin(1920, 1200));
            // 奇数余りは切り捨て (中央配置の half-pixel ずれを design 表記に合わせる)
            Assert.AreEqual(new Vector2(0f, 0f), V6ResultLogic.FrameOrigin(1729, 973));
            // フレーム未満はクランプしない (負 origin = 中央のままはみ出す)
            Assert.AreEqual(new Vector2(-224f, -126f), V6ResultLogic.FrameOrigin(1280, 720));
        }

        [Test]
        public void FrameOrigin_InvalidSize_FallsBackToZero()
        {
            Assert.AreEqual(Vector2.zero, V6ResultLogic.FrameOrigin(0, 1080));
            Assert.AreEqual(Vector2.zero, V6ResultLogic.FrameOrigin(1920, -1));
        }

        // --- GradeOf (scoringVersion ごとの帯。v4 以降は ADR-0034 / Issue #758) ---

        [Test]
        public void GradeOf_V4_FollowsTheDoubledThresholds()
        {
            // v4 の満点は 2,000,000 (基礎 + ボーナス)。帯は v1–v3 の閾値の 2 倍。
            Assert.AreEqual("SS", V6ResultLogic.GradeOf(2000000));
            Assert.AreEqual("S", V6ResultLogic.GradeOf(1900000));
            Assert.AreEqual("A", V6ResultLogic.GradeOf(1800000));
            Assert.AreEqual("B", V6ResultLogic.GradeOf(1600000));
            Assert.AreEqual("C", V6ResultLogic.GradeOf(1599999));
            Assert.AreEqual("C", V6ResultLogic.GradeOf(100));
        }

        [Test]
        public void GradeOf_V4_SS_RequiresThePerfectFullComboScore()
        {
            // 「ノーミス = SS」ではない: 2,000,000 未満は全て SS 未満。
            // ノーミスの All-Great フルコンボ (基礎 600,000 + ボーナス
            // 1,000,000 = 1,600,000) でも B どまり。
            Assert.AreEqual("S", V6ResultLogic.GradeOf(1999999));
            Assert.AreEqual("B", V6ResultLogic.GradeOf(1600000));
        }

        [Test]
        public void GradeOf_LegacyVersions_KeepV3Thresholds()
        {
            // v1–v3 のスコアは従来の RankFor 閾値のまま (暗黙の再解釈をしない)。
            Assert.AreEqual("SS", V6ResultLogic.GradeOf(1000000, Scoring.Version1));
            Assert.AreEqual("SS", V6ResultLogic.GradeOf(1000000, Scoring.Version3));
            Assert.AreEqual("S", V6ResultLogic.GradeOf(999999, Scoring.Version3));
            Assert.AreEqual("S", V6ResultLogic.GradeOf(950000, Scoring.Version2));
            Assert.AreEqual("A", V6ResultLogic.GradeOf(900000, Scoring.Version3));
            Assert.AreEqual("B", V6ResultLogic.GradeOf(700000, Scoring.Version3));
            Assert.AreEqual("C", V6ResultLogic.GradeOf(699999, Scoring.Version3));
            Assert.AreEqual("–", V6ResultLogic.GradeOf(0, Scoring.Version3));
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

        // --- ゲージ幾何 (scoring v4 の満点 2,000,000 への正規化。Issue #758) ---

        [Test]
        public void GaugeFillWidth_MapsScoreRatioToDesignLength()
        {
            // v4: 988,513 → 824 * 988513/2000000 = 407.47
            Assert.AreEqual(824f * 988513f / 2000000f, V6ResultLogic.GaugeFillWidth(988513), 0.01f);
            Assert.AreEqual(0f, V6ResultLogic.GaugeFillWidth(0));
        }

        [Test]
        public void GaugeFillWidth_DoesNotPinAboveLegacyMax()
        {
            // v3 以前は 1,000,000 で満タンに張り付いた。v4 では 100 万点は
            // レールの中間 (約 41%) に留まり、満点 2,000,000 で全長になる。
            Assert.AreEqual(V6ResultLogic.GaugeW * 0.5f, V6ResultLogic.GaugeFillWidth(1000000), 0.01f);
            Assert.AreEqual(V6ResultLogic.GaugeW, V6ResultLogic.GaugeFillWidth(2000000));
        }

        [Test]
        public void GaugeFillWidth_ClampsToTrackLength()
        {
            Assert.AreEqual(V6ResultLogic.GaugeW, V6ResultLogic.GaugeFillWidth(3000000));
        }

        [Test]
        public void GaugeBestX_MatchesDesignSample()
        {
            // v4: 前回最高 921,880 → x = 824 * 921880/2000000 = 379.81
            Assert.AreEqual(824f * 921880f / 2000000f, V6ResultLogic.GaugeBestX(921880), 0.01f);
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
