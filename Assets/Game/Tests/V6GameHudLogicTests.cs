using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI;
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
        public void GaugeBestX_ProportionalWithinScoreSpace()
        {
            // 点数空間は 0..1,000,000 (既存 gauge.fillAmount = score/1000000f と同じ)。
            // 得点に比例し、Best マーカーは自分のスコア位置に置かれる。
            Assert.AreEqual(0f, V6HudLogic.GaugeBestX(0, 530f), 1e-3f);
            Assert.AreEqual(265f, V6HudLogic.GaugeBestX(500000, 530f), 0.5f);
            Assert.AreEqual(530f, V6HudLogic.GaugeBestX(1000000, 530f), 0.5f);
        }

        [Test]
        public void GaugeBestX_StaysOnTrackBeyondScoreSpace()
        {
            // design (docs/design/v6/flashbeat-v6-nodes.json) の Best x=553 は
            // hiscore 1,043,396 をクランプせず 530 で割った点数空間外のサンプル。
            // ゲームの点数は空間外に出ないが、保存スコアが空間上限を超える
            // データが来てもマーカーがトラック外に出ないこと (クランプ) を決定的に検証する。
            Assert.AreEqual(530f, V6HudLogic.GaugeBestX(1043396, 530f), 0.5f);
            Assert.AreEqual(530f, V6HudLogic.GaugeBestX(int.MaxValue, 530f), 0.5f);
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

        // --- 1920×1080 HUD テンプレート ---

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
            Assert.AreEqual(524, m.GaugeFillMax);
            Assert.AreEqual(180, m.ComboValueFs);
            Assert.AreEqual(56, m.KeyCapW);
            Assert.AreEqual(36, m.KeyCapH);
        }

        [Test]
        public void Metrics_TwoK_MatchesDesign()
        {
            // 契約 §8 の 2K (E51yo 2560×1440) design 実測値
            // (docs/design/v6/flashbeat-v6-contract.md)。FHD の 4/3 倍と一致しない項目
            // (デザイナーの丸め: Grade fs 95→125 等) があるため、V6HudMetrics.TwoK は
            // 契約値をそのまま保持する。
            var m = V6HudMetrics.TwoK();
            Assert.AreEqual(347, m.TopShadeH);        // Top Shade 2560×347
            Assert.AreEqual(11, m.ProgressH);         // Progress Bar 2560×11
            Assert.AreEqual(64, m.Margin);            // Grade (64,64)
            Assert.AreEqual(125, m.GradeFs);          // fs125 (FHD 95 の 4/3 ではない)
            Assert.AreEqual(800, m.GaugeW);           // Score Gauge 800×48
            Assert.AreEqual(48, m.GaugeH);
            Assert.AreEqual(699, m.GaugeFillMax);     // Fill 699×14
            Assert.AreEqual(800, m.ComboW);           // Combo 800×293
            Assert.AreEqual(293, m.ComboH);
            Assert.AreEqual(240, m.ComboValueFs);     // Value fs240
            Assert.AreEqual(213, m.StatusBlockH);     // Combo Status 480×213
            Assert.AreEqual(200, m.FcChipW);          // FC チップ 200×53
            Assert.AreEqual(53, m.FcChipH);
            Assert.AreEqual(75, m.KeyCapW);           // KeyCap 75×48 (Key fs32)
            Assert.AreEqual(48, m.KeyCapH);
            Assert.AreEqual(32, m.KeyCapLabelFs);
            // KeyGuide 格子: x = 529, 733, … (左端 + KeyCap 幅の半分が中心)
            Assert.AreEqual(566.5f, m.KeyFirstCenter, 0.5f);
            Assert.AreEqual(203.571f, m.KeySpacing, 0.01f);
        }

        [Test]
        public void Metrics_MacProfile_MatchesTwoKSizes()
        {
            // 契約 §8: Mac (QrSwA 2560×1664) は 2K と同じ寸法で、縦位置だけ
            // 中央 / 下端アンカーに追従してずれる (Combo y685 / Status y725 / KeyGuide y1571)。
            // 実装は「FHD 論理テンプレート × 等比拡大 + アンカー追従」でこれを実現する:
            // design px = テンプレート値 × ScaleForResolution、
            // 中央 / 下端アンカー要素の上端 = (論理高さ / 2 − オフセット) × scale 等。
            var m = V6HudMetrics.Fhd(1f);
            float s2k = V6HudMetrics.ScaleForResolution(2560, 1440);
            float sMac = V6HudMetrics.ScaleForResolution(2560, 1664);
            Assert.AreEqual(4f / 3f, s2k, 1e-4f);
            Assert.AreEqual(4f / 3f, sMac, 1e-4f);

            // 共有寸法は Mac も 2K と同じ (design px = FHD テンプレート × 4/3、
            // デザイナーの丸めで ±1.5px)
            Assert.AreEqual(347, m.TopShadeH * s2k, 1.5f);
            Assert.AreEqual(800, m.GaugeW * s2k, 0.5f);
            Assert.AreEqual(24, m.GaugeTrackH * s2k, 0.5f);
            Assert.AreEqual(699, m.GaugeFillMax * s2k, 1.5f);
            Assert.AreEqual(240, m.ComboValueFs * s2k, 0.5f);
            Assert.AreEqual(75, m.KeyCapW * s2k, 0.5f);
            Assert.AreEqual(48, m.KeyCapH * s2k, 0.5f);
            Assert.AreEqual(32, m.KeyCapLabelFs * s2k, 0.5f);

            // 縦位置は中央 / 下端アンカーが効いて 2K からずれる (契約 Mac 列)
            float comboOffset = m.ComboH * 0.5f;
            Assert.AreEqual(430f, V6HudMetrics.MiddleAnchoredTopDesignY(1920, 1080, comboOffset), 0.5f);
            Assert.AreEqual(573f, V6HudMetrics.MiddleAnchoredTopDesignY(2560, 1440, comboOffset), 1.5f);
            Assert.AreEqual(685f, V6HudMetrics.MiddleAnchoredTopDesignY(2560, 1664, comboOffset), 1.5f);
            float statusOffset = m.RightColTopOffset;
            Assert.AreEqual(460f, V6HudMetrics.MiddleAnchoredTopDesignY(1920, 1080, statusOffset), 0.5f);
            Assert.AreEqual(613f, V6HudMetrics.MiddleAnchoredTopDesignY(2560, 1440, statusOffset), 1.5f);
            Assert.AreEqual(725f, V6HudMetrics.MiddleAnchoredTopDesignY(2560, 1664, statusOffset), 1.5f);
            float guideOffset = m.KeyGuideBottom + m.KeyGuideH;
            Assert.AreEqual(1011f, V6HudMetrics.BottomAnchoredTopDesignY(1920, 1080, m.KeyGuideBottom, m.KeyGuideH), 0.5f);
            Assert.AreEqual(1347f, V6HudMetrics.BottomAnchoredTopDesignY(2560, 1440, m.KeyGuideBottom, m.KeyGuideH), 2f);
            Assert.AreEqual(1571f, V6HudMetrics.BottomAnchoredTopDesignY(2560, 1664, m.KeyGuideBottom, m.KeyGuideH), 2f);
        }

        [TestCase(1920, 1080, 1f)]
        [TestCase(2560, 1440, 4f / 3f)]
        [TestCase(2560, 1664, 4f / 3f)]
        [TestCase(1440, 900, 0.75f)]
        [TestCase(1280, 720, 2f / 3f)]
        public void TemplateScale_UsesContainFit(int width, int height, float expected)
        {
            Assert.AreEqual(expected, V6HudMetrics.ScaleForResolution(width, height), 0.0001f);
        }

        [TestCase(1920, 1080, 1920f, 1080f)]
        [TestCase(2560, 1440, 1920f, 1080f)]
        [TestCase(2560, 1664, 1920f, 1248f)]
        [TestCase(1440, 900, 1920f, 1200f)]
        [TestCase(1280, 720, 1920f, 1080f)]
        public void LogicalViewportSize_FillsResolutionAndExpandsOnlyFreeAxis(
            int width, int height, float expectedWidth, float expectedHeight)
        {
            var logical = V6HudMetrics.LogicalViewportSize(width, height);

            Assert.AreEqual(expectedWidth, logical.x, 0.01f);
            Assert.AreEqual(expectedHeight, logical.y, 0.01f);
            Assert.AreEqual(width, logical.x * V6HudMetrics.ScaleForResolution(width, height), 0.01f);
            Assert.AreEqual(height, logical.y * V6HudMetrics.ScaleForResolution(width, height), 0.01f);
        }

        [Test]
        public void Metrics_FhdScaled_ScalesAllDimensions()
        {
            var m = V6HudMetrics.Fhd(0.5f);
            Assert.AreEqual(130, m.TopShadeH);
            Assert.AreEqual(300, m.GaugeW);
            Assert.AreEqual(48, m.GradeFs); // 95*0.5 = 47.5 → RoundToInt → 48
            Assert.AreEqual(7, m.GaugeSegH);
            Assert.AreEqual(7, m.GaugeFillH);
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

        // --- 歌詞 (design HcP8N: Furigana/Lyrics/Romaji の 3 行) ---

        [Test]
        public void LyricsTimeline_Build_CarriesFuriganaAndRomaji()
        {
            var lines = FlashBeat.UI.LyricsTimeline.Build(
                new[] { 0f, 10f },
                new[] { 4f, 14f },
                new[] { "終夜ノ摩天楼は", "夢を見る" },
                new[] { "よやのまてんろうわ", "ゆめをみる" },
                new[] { "shuya no matenrou wa", "yume wo miru" });

            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("終夜ノ摩天楼は", lines[0].Text);
            Assert.AreEqual("よやのまてんろうわ", lines[0].Furigana);
            Assert.AreEqual("shuya no matenrou wa", lines[0].Romaji);
            Assert.AreEqual("夢を見る", lines[1].Text);
            Assert.AreEqual("yume wo miru", lines[1].Romaji);
        }

        [Test]
        public void LyricsTimeline_Build_WithoutRomaji_RomajiIsNull()
        {
            var lines = FlashBeat.UI.LyricsTimeline.Build(
                new[] { 0f },
                new[] { 4f },
                new[] { "終夜ノ摩天楼は" },
                new[] { "よやのまてんろうわ" });

            Assert.AreEqual(1, lines.Count);
            Assert.IsNull(lines[0].Romaji);
        }

        // --- 歌詞スロットの写像 (Issue #379) ---

        [Test]
        public void LyricTopY_MatchesDesignMappingForThreeResolutions()
        {
            // 250 × (画面高さ / 972)。FHD 278 / 2K 370 / Mac 428。
            Assert.AreEqual(278f, V6HudLogic.LyricTopY(1080f), 0.6f);
            Assert.AreEqual(370f, V6HudLogic.LyricTopY(1440f), 0.6f);
            Assert.AreEqual(428f, V6HudLogic.LyricTopY(1664f), 0.6f);
        }

        [Test]
        public void LyricWidthScalesWithHeight()
        {
            // 幅は design の 1120 を高さ合わせで写す (変えない)。
            Vector2 fhd = V6HudLogic.LyricSize(1080f, 18f, 36f, 16f);
            Assert.AreEqual(1244f, fhd.x, 1f);

            Vector2 mac = V6HudLogic.LyricSize(1664f, 24f, 48f, 22f);
            Assert.AreEqual(1917f, mac.x, 1f);
        }

        [Test]
        public void LyricHeightFollowsTheTextNotTheDesignBlanket()
        {
            // 2026-11-12 オーナー指摘: 高さを 220 × scale にすると、フォントが
            // 画面倍率に追従しない分だけ「大きな空の板」になる。高さは行構成から
            // 求めるので、design の 220 × scale より小さくなる。
            Vector2 mac = V6HudLogic.LyricSize(1664f, 24f, 48f, 22f);
            Assert.Less(mac.y, 377f, "design の 220 × scale をそのまま使わない");
            Assert.Greater(mac.y, 48f, "歌詞 1 行は必ず入る");
        }

        // --- スコアのポップ (Issue #379) ---

        [Test]
        public void ScorePulseScale_RisesThenReturnsToOne()
        {
            Assert.AreEqual(1f, V6HudLogic.ScorePulseScale(0f), 1e-4f);
            Assert.AreEqual(1.06f, V6HudLogic.ScorePulseScale(0.08f), 1e-4f);
            Assert.AreEqual(1f, V6HudLogic.ScorePulseScale(0.2f), 1e-4f);
            Assert.AreEqual(1f, V6HudLogic.ScorePulseScale(5f), 1e-4f);
            Assert.AreEqual(1f, V6HudLogic.ScorePulseScale(-1f), 1e-4f);
        }
    }
}
