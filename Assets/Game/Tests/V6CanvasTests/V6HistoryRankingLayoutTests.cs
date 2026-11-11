using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.History;
using FlashBeat.UI.V6.Ranking;

namespace FlashBeat.UI.V6.History.Tests
{
    /// <summary>
    /// v6 履歴 / ランキング両画面のフレーム配置 (1:1 固定・中央配置) の EditMode テスト。
    ///
    /// design 正本: flashbeat-v6-nodes.json
    /// - 共通フレーム rw7zx は 1728×972 の固定解像度 (V6Frame.DesignW/H)。
    /// - 「Resolution Check · Select/Result/Option」(vxSO0 / s88VZk / gaI3p) の
    ///   フレーム origin: FHD (96,54) / 2K (416,234) / MacBook (416,346)
    ///   = floor((screen − 1728×972) / 2)。
    /// - 引き伸ばし (min(W/1728, H/972) の等比スケール) は行わない。
    /// </summary>
    public sealed class V6HistoryRankingLayoutTests
    {
        private static readonly int[][] TargetResolutions =
        {
            new[] { 1920, 1080 }, // FHD
            new[] { 2560, 1440 }, // 2K
            new[] { 2560, 1664 }, // MacBook Air M5
        };

        // --- フレーム原点 (design「Resolution Check」表どおり) ---

        [Test]
        public void FrameOrigin_MatchesDesignResolutionTable_History()
        {
            int[][] expected =
            {
                new[] { 96, 54 },   // FHD
                new[] { 416, 234 }, // 2K
                new[] { 416, 346 }, // MacBook Air M5
            };

            for (int i = 0; i < TargetResolutions.Length; i++)
            {
                int w = TargetResolutions[i][0];
                int h = TargetResolutions[i][1];
                Assert.AreEqual(expected[i][0], V6HistoryLogic.FrameOriginX(w),
                    "history frame origin x at " + w + "x" + h);
                Assert.AreEqual(expected[i][1], V6HistoryLogic.FrameOriginY(h),
                    "history frame origin y at " + w + "x" + h);
            }
        }

        [Test]
        public void FrameOrigin_MatchesDesignResolutionTable_Ranking()
        {
            int[][] expected =
            {
                new[] { 96, 54 },
                new[] { 416, 234 },
                new[] { 416, 346 },
            };

            for (int i = 0; i < TargetResolutions.Length; i++)
            {
                int w = TargetResolutions[i][0];
                int h = TargetResolutions[i][1];
                Assert.AreEqual(expected[i][0], V6RankingLogic.FrameOriginX(w),
                    "ranking frame origin x at " + w + "x" + h);
                Assert.AreEqual(expected[i][1], V6RankingLogic.FrameOriginY(h),
                    "ranking frame origin y at " + w + "x" + h);
            }
        }

        // --- 1:1 中央配置の数式 (floor((screen − frame) / 2)) ---

        [Test]
        public void FrameOrigin_IsFloorCenteredDivision()
        {
            // 正の余りは床切り (奇数差は切り捨て): 2561x1441 → (416, 234)
            Assert.AreEqual(416, V6HistoryLogic.FrameOriginX(2561));
            Assert.AreEqual(234, V6HistoryLogic.FrameOriginY(1441));

            // 負側も数学的な floor (C# の切捨て除算は 0 方向なので自前で補正):
            // 1001 → floor((1001 − 1728) / 2) = floor(−363.5) = −364
            Assert.AreEqual(-364, V6HistoryLogic.FrameOriginX(1001));
            Assert.AreEqual(-364, V6RankingLogic.FrameOriginX(1001));
        }

        [Test]
        public void FrameOrigin_IsCenteredOnScreen()
        {
            foreach (int[] res in TargetResolutions)
            {
                int w = res[0];
                int h = res[1];
                int x = V6HistoryLogic.FrameOriginX(w);
                int y = V6HistoryLogic.FrameOriginY(h);
                // 中央配置: 左余り = 右余り / 上余り = 下余り
                Assert.AreEqual(w - V6HistoryLogic.FrameW - x, x, "history frame is centered horizontally at " + w);
                Assert.AreEqual(h - V6HistoryLogic.FrameH - y, y, "history frame is centered vertically at " + h);
            }
        }

        // --- フレームは共通フレーム (rw7zx) と同寸・スケールなし ---

        [Test]
        public void FrameSize_MatchesSharedFrameWithoutScaling()
        {
            Assert.AreEqual(V6Frame.DesignW, V6HistoryLogic.FrameW, "history frame uses the shared 1728px frame");
            Assert.AreEqual(V6Frame.DesignH, V6HistoryLogic.FrameH, "history frame uses the shared 972px frame");
            Assert.AreEqual(V6Frame.DesignW, V6RankingLogic.FrameW, "ranking frame uses the shared 1728px frame");
            Assert.AreEqual(V6Frame.DesignH, V6RankingLogic.FrameH, "ranking frame uses the shared 972px frame");
        }

        // --- はみ出し防止 (design 幅 1728 / 高 972 の外に出ない) ---

        [Test]
        public void HistoryPanels_StayInsideDesignFrame()
        {
            var frame = new Rect(0f, 0f, V6HistoryLogic.FrameW, V6HistoryLogic.FrameH);
            var designOrigin = new Vector2(0f, 0f);
            var designSize = new Vector2(V6HistoryLogic.FrameW, V6HistoryLogic.FrameH);

            // 履歴リスト (Main 内 (0,0) 560×780)
            var listRect = new Rect(
                V6HistoryLogic.MainX + 0f, V6HistoryLogic.MainY,
                V6HistoryLogic.ListPanelW, V6HistoryLogic.ListPanelH);
            Assert.IsTrue(V6Layout.FitsIn(designOrigin, designSize,
                new Vector2(listRect.x, listRect.y), new Vector2(listRect.width, listRect.height)),
                "history list panel stays inside the design frame");

            // 選択中の曲 (Main 内 (572,0) 536×780)
            var centerRect = new Rect(
                V6HistoryLogic.MainX + V6HistoryLogic.CenterPanelX, V6HistoryLogic.MainY,
                V6HistoryLogic.CenterPanelW, V6HistoryLogic.ListPanelH);
            Assert.IsTrue(V6Layout.FitsIn(designOrigin, designSize,
                new Vector2(centerRect.x, centerRect.y), new Vector2(centerRect.width, centerRect.height)),
                "selected song panel stays inside the design frame");

            // 記録情報 (Main 内 (1120,0) 560×780、パネルは x16 w528)
            var rightRect = new Rect(
                V6HistoryLogic.MainX + V6HistoryLogic.RightColumnX, V6HistoryLogic.MainY,
                V6HistoryLogic.ListPanelW, V6HistoryLogic.ListPanelH);
            Assert.IsTrue(V6Layout.FitsIn(designOrigin, designSize,
                new Vector2(rightRect.x, rightRect.y), new Vector2(rightRect.width, rightRect.height)),
                "record column stays inside the design frame");
            Assert.LessOrEqual(rightRect.xMax, V6HistoryLogic.FrameW,
                "record column right edge stays inside the 1728px design width");

            // フッター右の主操作ボタン (design: (840+567,20) 273×56)
            float gotoRight = V6HistoryLogic.MainX + V6HistoryLogic.FooterSettingsButtonX
                + V6HistoryLogic.FooterGotoButtonX - V6HistoryLogic.FooterSettingsButtonX
                + 273f;
            Assert.LessOrEqual(V6HistoryLogic.MainX + V6HistoryLogic.FooterGotoButtonX + 273f,
                V6HistoryLogic.FrameW, "footer primary button stays inside the design width");

            // フッター下端 = フレーム下端 (design: footer (24,876) 1680×96)
            Assert.AreEqual(V6HistoryLogic.FooterY + V6HistoryLogic.FooterH, V6HistoryLogic.FrameH,
                "footer bottom edge meets the frame bottom edge");
        }

        [Test]
        public void RankingPanels_StayInsideDesignFrame()
        {
            var designOrigin = new Vector2(0f, 0f);
            var designSize = new Vector2(V6RankingLogic.FrameW, V6RankingLogic.FrameH);

            // 曲リスト (Main 内 (0,0) 560×780)
            var listRect = new Rect(V6RankingLogic.MainX, V6RankingLogic.MainY,
                V6RankingLogic.ListPanelW, V6RankingLogic.ListPanelH);
            Assert.IsTrue(V6Layout.FitsIn(designOrigin, designSize,
                new Vector2(listRect.x, listRect.y), new Vector2(listRect.width, listRect.height)),
                "ranking song list stays inside the design frame");

            // SCORE RANKING (Main 内 (584,24) 512×732)
            var scoreRect = new Rect(
                V6RankingLogic.MainX + V6RankingLogic.ScorePanelX,
                V6RankingLogic.MainY + V6RankingLogic.ScorePanelY,
                V6RankingLogic.ScorePanelW, V6RankingLogic.ScorePanelH);
            Assert.IsTrue(V6Layout.FitsIn(designOrigin, designSize,
                new Vector2(scoreRect.x, scoreRect.y), new Vector2(scoreRect.width, scoreRect.height)),
                "score ranking panel stays inside the design frame");

            // PLAYERS (Main 内 (1144,24) 512×732)
            var playersRect = new Rect(
                V6RankingLogic.MainX + V6RankingLogic.PlayersPanelX,
                V6RankingLogic.MainY + V6RankingLogic.ScorePanelY,
                V6RankingLogic.ScorePanelW, V6RankingLogic.ScorePanelH);
            Assert.IsTrue(V6Layout.FitsIn(designOrigin, designSize,
                new Vector2(playersRect.x, playersRect.y), new Vector2(playersRect.width, playersRect.height)),
                "players panel stays inside the design frame");
            Assert.LessOrEqual(playersRect.xMax, V6RankingLogic.FrameW,
                "players panel right edge stays inside the 1728px design width");
        }

        // --- 行の可視数がパネル/フレームに収まる ---

        [Test]
        public void HistoryRows_FitVisibleArea()
        {
            // design: 行 (16, 48+72k) 496×64、可視 10 行 → 下端 48 + 9*72 + 64 = 760 <= 780
            float lastBottom = V6HistoryLogic.HistoryRowY0
                + V6HistoryLogic.HistoryRowStep * (V6HistoryLogic.HistoryVisibleRows - 1)
                + V6HistoryLogic.HistoryRowH;
            Assert.LessOrEqual(lastBottom, V6HistoryLogic.ListPanelH,
                "the last visible history row fits the list panel height");
            Assert.LessOrEqual(V6HistoryLogic.HistoryRowX + V6HistoryLogic.HistoryRowW,
                V6HistoryLogic.ListPanelW, "history row width fits the list panel width");
        }

        [Test]
        public void RankingRows_FitVisibleArea()
        {
            // design (S1TZD): 行 (8, 60+64k) 496×56、可視 10 行 → 下端 60 + 9*64 + 56 = 692 <= 732
            float lastBottom = V6RankingLogic.RankRowY0
                + V6RankingLogic.RankRowStep * (V6RankingLogic.RankVisibleRows - 1)
                + V6RankingLogic.RankRowH;
            Assert.LessOrEqual(lastBottom, V6RankingLogic.ScorePanelH,
                "the last visible ranking row fits the score ranking panel height");
            Assert.LessOrEqual(V6RankingLogic.RankRowX + V6RankingLogic.RankRowW,
                V6RankingLogic.ScorePanelW, "ranking row width fits the score ranking panel width");

            // 曲リスト行 (design: (16, 48+76k) 504×72、可視 9 行 → 下端 48 + 8*76 + 72 = 728 <= 780)
            float songLastBottom = V6RankingLogic.SongRowY0
                + V6RankingLogic.SongRowStep * (V6RankingLogic.ListVisibleRows - 1)
                + V6RankingLogic.SongRowH;
            Assert.LessOrEqual(songLastBottom, V6RankingLogic.ListPanelH,
                "the last visible song row fits the song list panel height");
        }

        // --- 共通部品の寸法 (design 実測値) が design どおり ---

        [Test]
        public void RankRowConstants_MatchDesignSpec()
        {
            // design Hbxqf (Ranking 行): 496×56 / Pos 36×36 / Name w160 fs22 /
            // Value w180 右寄せ / Grade 56×36。History の T8C1Hu は同じ内部座標で高さ 52。
            // (契約 doc §5 の「496×44 / Name w140」は Select のランキング列
            //  OENRv jnIY9 系の値。この部品の正本は Ranking/History のノード)。
            Assert.AreEqual(496f, V6Lists.RankRowW, "RankRow width matches the design");
            Assert.AreEqual(56f, V6Lists.RankRowH, "RankRow height matches design Hbxqf");
            Assert.AreEqual(36f, V6Lists.RankPosSize, "RankRow pos badge size matches the design");
            Assert.AreEqual(160f, V6Lists.RankNameW, "RankRow name width matches design (w160, not 140)");
            Assert.AreEqual(180f, V6Lists.RankValueW, "RankRow value width matches the design");
            Assert.AreEqual(56f, V6Lists.RankGradeW, "RankRow grade width matches the design");
            Assert.AreEqual(36f, V6Lists.RankGradeH, "RankRow grade height matches the design");
            Assert.AreEqual(22f, V6Lists.RankFs, "RankRow font size matches the design");
            // design の親内 x (Pos 12 / Name 60 / Value 232 / Grade 424)。
            Assert.AreEqual(12f, V6Lists.RankPosX, "Pos x");
            Assert.AreEqual(60f, V6Lists.RankNameX, "Name x");
            Assert.AreEqual(232f, V6Lists.RankValueX, "Value x");
            Assert.AreEqual(424f, V6Lists.RankGradeX, "Grade x");
        }
    }
}
