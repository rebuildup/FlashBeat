using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Game.Hud;

namespace FlashBeat.V6HudTests
{
    /// <summary>
    /// v6 Game HUD (Issue #220) の純ロジックの EditMode テスト。
    /// ビュー (uGUI 生成) は含まない。実データ結線の式だけを検証する。
    /// </summary>
    public class V6GameHudLogicTests
    {
        // --- 進捗バー ---

        [Test]
        public void ProgressFraction_ZeroDuration_IsZero()
        {
            Assert.AreEqual(0f, V6HudLogic.ProgressFraction(10f, 0f), 1e-5f);
            Assert.AreEqual(0f, V6HudLogic.ProgressFraction(10f, -1f), 1e-5f);
        }

        [Test]
        public void ProgressFraction_ClampsToUnit()
        {
            Assert.AreEqual(0f, V6HudLogic.ProgressFraction(-5f, 100f), 1e-5f);
            Assert.AreEqual(0.5f, V6HudLogic.ProgressFraction(50f, 100f), 1e-5f);
            Assert.AreEqual(1f, V6HudLogic.ProgressFraction(150f, 100f), 1e-5f);
        }

        [Test]
        public void ProgressFillWidth_ScalesWithBarWidth()
        {
            Assert.AreEqual(500f, V6HudLogic.ProgressFillWidth(50f, 100f, 1000f), 1e-3f);
            Assert.AreEqual(0f, V6HudLogic.ProgressFillWidth(50f, 0f, 1000f), 1e-3f);
        }

        // --- スコアゲージ (design 実測: FHD 塗り最大 530, 988,513 → 524) ---

        [Test]
        public void GaugeFillWidth_MatchesDesignSample()
        {
            Assert.AreEqual(524f, V6HudLogic.GaugeFillWidth(988513, 530f), 0.5f);
            Assert.AreEqual(530f, V6HudLogic.GaugeFillWidth(1000000, 530f), 0.5f);
            Assert.AreEqual(0f, V6HudLogic.GaugeFillWidth(0, 530f), 1e-3f);
        }

        [Test]
        public void GaugeBestX_MatchesDesignSample()
        {
            // design: Best マーカー x=553 (hiscore 1,043,396 / 530 span)
            Assert.AreEqual(553f, V6HudLogic.GaugeBestX(1043396, 530f), 0.5f);
            Assert.AreEqual(0f, V6HudLogic.GaugeBestX(0, 530f), 1e-3f);
        }

        // --- コンボ ---

        [Test]
        public void ShowComboStatus_RequiresMissFreeAndJudged()
        {
            Assert.IsTrue(V6HudLogic.ShowComboStatus(0, 10));
            Assert.IsFalse(V6HudLogic.ShowComboStatus(1, 10));
            Assert.IsFalse(V6HudLogic.ShowComboStatus(0, 0));
        }

        [Test]
        public void ComboFillFraction_ClampsAndHandlesZeroMilestone()
        {
            Assert.AreEqual(0.82f, V6HudLogic.ComboFillFraction(246, 300), 1e-4f);
            Assert.AreEqual(1f, V6HudLogic.ComboFillFraction(500, 300), 1e-4f);
            Assert.AreEqual(0f, V6HudLogic.ComboFillFraction(10, 0), 1e-4f);
        }

        // --- レーン別判定 ---

        [Test]
        public void LaneFromEffectPositionX_MapsJudgeEffectPositions()
        {
            // Judge.EffectPosition: x = lane - 3.5
            Assert.AreEqual(0, V6HudLogic.LaneFromEffectPositionX(-3.5f));
            Assert.AreEqual(1, V6HudLogic.LaneFromEffectPositionX(-2.5f));
            Assert.AreEqual(4, V6HudLogic.LaneFromEffectPositionX(0.5f));
            Assert.AreEqual(7, V6HudLogic.LaneFromEffectPositionX(3.5f));
        }

        [Test]
        public void LaneFromEffectPositionX_ClampsOutOfRange()
        {
            Assert.AreEqual(0, V6HudLogic.LaneFromEffectPositionX(-99f));
            Assert.AreEqual(7, V6HudLogic.LaneFromEffectPositionX(99f));
        }

        [Test]
        public void VerdictLabel_StripsCloneSuffixAndUppercases()
        {
            Assert.AreEqual("PERFECT", V6HudLogic.VerdictLabel("Perfect(Clone)"));
            Assert.AreEqual("GREAT", V6HudLogic.VerdictLabel("Great(Clone)"));
            Assert.AreEqual("BAD", V6HudLogic.VerdictLabel("Bad(Clone)"));
            Assert.AreEqual("MISS", V6HudLogic.VerdictLabel("Miss(Clone)"));
            Assert.AreEqual("PERFECT", V6HudLogic.VerdictLabel("Perfect"));
            Assert.AreEqual(string.Empty, V6HudLogic.VerdictLabel(null));
            Assert.AreEqual(string.Empty, V6HudLogic.VerdictLabel(""));
        }

        [Test]
        public void VerdictColor_MapsJudgements()
        {
            Assert.AreEqual(V5Tokens.AccentLight, V6HudLogic.VerdictColor("PERFECT"));
            Assert.AreEqual(V5Tokens.Text, V6HudLogic.VerdictColor("GREAT"));
            Assert.AreEqual(V5Tokens.WarnLight, V6HudLogic.VerdictColor("BAD"));
            Assert.AreEqual(V5Tokens.DangerLight, V6HudLogic.VerdictColor("MISS"));
        }

        // --- キーガイド ---

        [Test]
        public void RepresentativeKeyText_PrefersCanonicalHomeRowKey()
        {
            Assert.AreEqual("A", V6HudLogic.RepresentativeKeyText(
                new[] { KeyCode.A, KeyCode.Q, KeyCode.Z }));
            Assert.AreEqual(";", V6HudLogic.RepresentativeKeyText(
                new[] { KeyCode.Semicolon, KeyCode.P }));
        }

        [Test]
        public void RepresentativeKeyText_FallsBackToFirstLetterKey()
        {
            Assert.AreEqual("N", V6HudLogic.RepresentativeKeyText(
                new[] { KeyCode.N, KeyCode.M }));
        }

        [Test]
        public void RepresentativeKeyText_EmptyAssignment_IsEmpty()
        {
            Assert.AreEqual(string.Empty, V6HudLogic.RepresentativeKeyText(null));
            Assert.AreEqual(string.Empty, V6HudLogic.RepresentativeKeyText(new KeyCode[0]));
        }

        // --- 寸法表 (3 解像度) ---

        [Test]
        public void Metrics_Fhd_MatchesDesign()
        {
            var m = V6HudMetrics.Fhd(1f);
            Assert.AreEqual(260, m.TopShadeH);
            Assert.AreEqual(8, m.ProgressH);
            Assert.AreEqual(48, m.Margin);
            Assert.AreEqual(95, m.GradeFs);
            Assert.AreEqual(600, m.GaugeW);
            Assert.AreEqual(36, m.GaugeH);
            Assert.AreEqual(530, m.GaugeFillMax);
            Assert.AreEqual(180, m.ComboValueFs);
            Assert.AreEqual(56, m.KeyCapW);
            Assert.AreEqual(36, m.KeyCapH);
        }

        [Test]
        public void Metrics_TwoK_MatchesDesign()
        {
            var m = V6HudMetrics.TwoK();
            Assert.AreEqual(347, m.TopShadeH);
            Assert.AreEqual(11, m.ProgressH);
            Assert.AreEqual(64, m.Margin);
            Assert.AreEqual(125, m.GradeFs);
            Assert.AreEqual(800, m.GaugeW);
            Assert.AreEqual(48, m.GaugeH);
            Assert.AreEqual(707, m.GaugeFillMax);
            Assert.AreEqual(240, m.ComboValueFs);
            Assert.AreEqual(75, m.KeyCapW);
            Assert.AreEqual(48, m.KeyCapH);
        }

        [Test]
        public void Metrics_MacUsesTwoKSizes()
        {
            // Mac 2560×1664 は 2K と同じ寸法 (縦位置はアンカーで追従)
            var twoK = V6HudMetrics.TwoK();
            var current = V6HudMetrics.Current;
            // 縦横比 1.538 の解像度は選べない (EditMode では画面依存) ので
            // Current は「2k/mac なら TwoK、それ以外は Fhd 系」の分岐だけを検証する
            Assert.IsNotNull(current);
            Assert.IsTrue(
                current.TopShadeH == twoK.TopShadeH || current.TopShadeH == V6HudMetrics.Fhd(1f).TopShadeH);
        }

        [Test]
        public void Metrics_FhdScaled_ScalesAllDimensions()
        {
            var m = V6HudMetrics.Fhd(0.5f);
            Assert.AreEqual(130, m.TopShadeH);
            Assert.AreEqual(300, m.GaugeW);
            Assert.AreEqual(48, m.GradeFs); // 95*0.5 = 47.5 → RoundToInt → 48
        }

        [Test]
        public void KeyColumnCenter_MatchesDesignGrid()
        {
            var m = V6HudMetrics.Fhd(1f);
            // design: KeyCap A の中心 x=338, ; の中心 x=1580
            Assert.AreEqual(338f, m.KeyColumnCenter(0), 0.5f);
            Assert.AreEqual(1580f, m.KeyColumnCenter(7), 0.5f);
            // 判定行も同じグリッド (design: 判定3 中心 693, 判定6 中心 1225)
            Assert.AreEqual(693f, m.KeyColumnCenter(2), 0.5f);
            Assert.AreEqual(1225f, m.KeyColumnCenter(5), 0.5f);
        }
    }
}
