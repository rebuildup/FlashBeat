using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Result;

namespace FlashBeat.V5CanvasTests
{
    /// <summary>
    /// v6 Result 共通フレームの 1:1 固定解像度配置 (純ロジック)。
    ///
    /// 正本: design s88VZk "Resolution Check · Result" の実配置 —
    /// 各解像度フレーム (FHD 1920×1080 / 2K 2560×1440 / Mac 2560×1664) に
    /// 05 Result · v6 (zcSHh) が 1:1 (等倍) で載る:
    /// - FHD 1920×1080 → origin (96, 54)
    /// - 2K 2560×1440 → origin (416, 234)
    /// - Mac 2560×1664 → origin (416, 346)
    /// (いずれも ((w−1728)/2, (h−972)/2) の整数切り捨て)。
    /// 旧実装の min(w/1728, h/972) による引き伸ばし (等比スケール) は
    /// Owner 指示により廃止 (design フレームは固定解像度)。
    /// </summary>
    public class V6ResultFramePlacementTests
    {
        [Test]
        public void ResultFrameOrigin_MatchesDesignResolutionTable()
        {
            Assert.AreEqual(new Vector2(96f, 54f), V6ResultLogic.FrameOrigin(1920, 1080));
            Assert.AreEqual(new Vector2(416f, 234f), V6ResultLogic.FrameOrigin(2560, 1440));
            Assert.AreEqual(new Vector2(416f, 346f), V6ResultLogic.FrameOrigin(2560, 1664));
        }

        [Test]
        public void ResultFrameOrigin_IsCenteredFloorFormula()
        {
            // design フレーム自体では origin (0,0)。奇数余りは切り捨て。
            Assert.AreEqual(Vector2.zero, V6ResultLogic.FrameOrigin(1728, 972));
            Assert.AreEqual(new Vector2(0f, 0f), V6ResultLogic.FrameOrigin(1729, 973));
            // フレーム未満はクランプしない (1:1 を維持し中央のままはみ出す)
            Assert.AreEqual(new Vector2(-224f, -126f), V6ResultLogic.FrameOrigin(1280, 720));
        }

        [Test]
        public void ResultFrameOrigin_NeverScales()
        {
            // 1:1 配置の契約上、Result ロジックに倍率 API は存在しない
            // (min(W/1728, H/972) の復活 = 引き伸ばしの再導入を防ぐ)。
            Assert.IsNull(typeof(V6ResultLogic).GetMethod("DesignScale"),
                "DesignScale (引き伸ばし倍率) は廃止済み。再追加しないこと");
        }

        [Test]
        public void ResultMain_IsFiveDesignColumns()
        {
            // 正本 zcSHh: Main (164,96) 1400×780 = 5 列 × 280 (共有フレームの 6 列を
            // そのまま使うと右パネルが 156 px 右へずれる)。右パネルは x=1020 (528 幅)。
            Assert.AreEqual(164f, V6ResultLogic.MainX);
            Assert.AreEqual(1400f, V6ResultLogic.MainW);
            Assert.AreEqual(5f * 280f, V6ResultLogic.MainW);
            Assert.AreEqual(856f, V6ResultLogic.RankX);
            Assert.AreEqual(1020f, V6ResultLogic.MainX + V6ResultLogic.RankX);
        }

        [Test]
        public void ResultMain_AndRightColumn_FitInSharedFrame()
        {
            // メインゾーン (164,96) 1400×780 と右カラム 528 は共有フレーム 1728×972 内。
            var frameOrigin = Vector2.zero;
            var frameSize = new Vector2(V6ResultLogic.DesignW, V6ResultLogic.DesignH);
            Assert.IsTrue(V6Layout.FitsIn(frameOrigin, frameSize,
                new Vector2(V6ResultLogic.MainX, V6ResultLogic.MainY),
                new Vector2(V6ResultLogic.MainW, V6ResultLogic.MainH)));

            // 右カラム (ランキング 2 枚, 幅 528) はフレーム内に収まる。
            // 正本 zcSHh では右カラム右端 x1548 = Main 右端 (164+1400=1564) の
            // 16px 内側 (フッター右端 1704 ではない)。
            float rankLeft = V6ResultLogic.MainX + V6ResultLogic.RankX;
            Assert.IsTrue(V6Layout.FitsIn(frameOrigin, frameSize,
                new Vector2(rankLeft, V6ResultLogic.MainY), new Vector2(528f, V6ResultLogic.MainH)));
            Assert.AreEqual(1548f, rankLeft + 528f);
            Assert.AreEqual(16f, V6ResultLogic.MainX + V6ResultLogic.MainW - (rankLeft + 528f));
        }

        [Test]
        public void ResultFooter_ButtonsAndLegend_FitWithoutOverlap()
        {
            // フッター右の 3 ボタン (OK / Register / Retry) は正本 zcSHh の固定幅
            // (273 / 273 / 220) で、右端から gap16 で横並び。フッター (24,876) 1680x96 内に収まる。
            float[] widths =
            {
                V6ResultLogic.FooterOkButtonW,
                V6ResultLogic.FooterRegisterButtonW,
                V6ResultLogic.FooterRetryButtonW,
            };
            float[] xs = V6ResultLogic.FooterButtonXs(widths);
            var footerOrigin = new Vector2(24f, 876f);
            var footerSize = new Vector2(V6ResultLogic.FooterW, V6ResultLogic.FooterH);

            for (int i = 0; i < xs.Length; i++)
            {
                Assert.IsTrue(V6Layout.FitsIn(footerOrigin, footerSize,
                    new Vector2(24f + xs[i], 876f + V6ResultLogic.FooterButtonY),
                    new Vector2(widths[i], V6ResultLogic.FooterButtonH)), "footer button " + i);
            }

            // ボタン同士が重ならない (右寄せ gap16)。
            Assert.LessOrEqual(xs[1] + widths[1] + V6ResultLogic.FooterButtonGap, xs[0]);
            Assert.LessOrEqual(xs[2] + widths[2] + V6ResultLogic.FooterButtonGap, xs[1]);

            // 左のキー凡例 (y30) とも重ならない。
            Assert.LessOrEqual(V6ResultLogic.FooterLegendWidth() + V6ResultLogic.FooterButtonGap, xs[2]);
        }
        [Test]
        public void ResultSongInfoBand_LabelsAndValues_StayInsideBand()
        {
            // 楽曲情報バンド (Main 内 (0,636)) の子要素はすべてバンド内に収まる
            // (旧実装は Stat の値がバンドの下へはみ出した)。
            // Song* 定数はバンド内相対座標 (V6ResultView の At 呼び出しと同一)。
            // FitsIn は design 絶対座標で判定するため、バンド原点を加算して渡す。
            var bandOrigin = new Vector2(V6ResultLogic.MainX, V6ResultLogic.MainY + V6ResultLogic.SongInfoY);
            var bandSize = new Vector2(V6ResultLogic.SongInfoW, V6ResultLogic.SongInfoH);

            Assert.IsTrue(V6Layout.FitsIn(bandOrigin, bandSize,
                bandOrigin + new Vector2(V6ResultLogic.SongThumbX, V6ResultLogic.SongThumbY),
                new Vector2(V6ResultLogic.SongThumbW, V6ResultLogic.SongThumbH)), "thumb");
            Assert.IsTrue(V6Layout.FitsIn(bandOrigin, bandSize,
                bandOrigin + new Vector2(V6ResultLogic.SongTitleX, V6ResultLogic.SongTitleY),
                new Vector2(V6ResultLogic.SongTitleW, V6ResultLogic.SongTitleH)), "title");
            Assert.IsTrue(V6Layout.FitsIn(bandOrigin, bandSize,
                bandOrigin + new Vector2(V6ResultLogic.SongArtistX, V6ResultLogic.SongArtistY),
                new Vector2(V6ResultLogic.SongArtistW, V6ResultLogic.SongArtistH)), "artist");

            for (int i = 0; i < V6ResultLogic.SongStatX.Length; i++)
            {
                var cellPos = bandOrigin + new Vector2(V6ResultLogic.SongStatX[i], V6ResultLogic.SongStatY);
                var cellSize = new Vector2(V6ResultLogic.SongStatW, V6ResultLogic.SongStatH);
                Assert.IsTrue(V6Layout.FitsIn(bandOrigin, bandSize, cellPos, cellSize), "stat cell " + i);
                // セル内の Label (上 20) + Value (y20 h42) もセル内に収まる。
                Assert.IsTrue(V6Layout.FitsIn(cellPos, cellSize,
                    cellPos,
                    new Vector2(V6ResultLogic.SongStatW, V6ResultLogic.SongStatLabelH)), "stat label " + i);
                Assert.IsTrue(V6Layout.FitsIn(cellPos, cellSize,
                    cellPos + new Vector2(0f, V6ResultLogic.SongStatValueY),
                    new Vector2(V6ResultLogic.SongStatW, V6ResultLogic.SongStatValueH)), "stat value " + i);
            }

            // 曲名 (fs34, 実データ最長 12 文字) の矩形と Stat のラベル行は縦に重ならない。
            Assert.LessOrEqual(V6ResultLogic.SongTitleY + V6ResultLogic.SongTitleH, V6ResultLogic.SongStatY);
        }

        [Test]
        public void ResultRankHead_TitleMetricChevrons_StayInsideHead()
        {
            // Head (パネル内 (8,8) 512x36): 見出し / 集計軸 / 矢印はすべて Head 内
            // (= パネルの枠線より内側)。旧実装は pivot 補正漏れで見出しが枠線に乗り、
            // 「累計スコア」が折り返して矢印と重なった。
            var panelOrigin = new Vector2(V6ResultLogic.MainX + V6ResultLogic.RankX,
                V6ResultLogic.MainY + 452f);
            var headOrigin = panelOrigin + new Vector2(V6ResultLogic.RankHeadX, V6ResultLogic.RankHeadY);
            var headSize = new Vector2(V6ResultLogic.RankHeadW, V6ResultLogic.RankHeadH);

            // title/metric/chevron の定数・リテラルは Head 内相対座標
            // (V6ResultView の At 呼び出しと同一)。FitsIn は design 絶対座標で
            // 判定するため、Head 原点を加算してから渡す。
            var titlePos = headOrigin + new Vector2(12f, 0f);
            var titleSize = new Vector2(V6ResultLogic.RankHeadTitleW, V6ResultLogic.RankHeadH);
            var metricPos = headOrigin + new Vector2(V6ResultLogic.RankHeadMetricX, 0f);
            var metricSize = new Vector2(V6ResultLogic.RankHeadMetricW, V6ResultLogic.RankHeadH);
            var chevronSize = new Vector2(V6ResultLogic.RankHeadChevronSize, V6ResultLogic.RankHeadChevronSize);
            var chevronLPos = headOrigin + new Vector2(V6ResultLogic.RankHeadChevronL, 2f);
            var chevronRPos = headOrigin + new Vector2(V6ResultLogic.RankHeadChevronR, 2f);

            Assert.IsTrue(V6Layout.FitsIn(headOrigin, headSize, titlePos, titleSize), "title");
            Assert.IsTrue(V6Layout.FitsIn(headOrigin, headSize, metricPos, metricSize), "metric");
            Assert.IsTrue(V6Layout.FitsIn(headOrigin, headSize, chevronLPos, chevronSize), "chevron left");
            Assert.IsTrue(V6Layout.FitsIn(headOrigin, headSize, chevronRPos, chevronSize), "chevron right");

            // 横方向の重なりがない (見出し | 矢印 | 集計軸 | 矢印)。
            Assert.LessOrEqual(titlePos.x + titleSize.x, chevronLPos.x);
            Assert.LessOrEqual(metricPos.x + metricSize.x, chevronRPos.x);
        }

        [Test]
        public void ResultModalDialog_FitsInsideSharedFrame()
        {
            // 登録モーダル (design 05b lqHaN) の Dialog は共通フレーム内に収まる。
            // scrim ("Modal 05b") はフレーム全面、Dialog は 840x510。
            // 座標は design px の左上原点 (V6Layout.FitsIn / At と同じ方式)。
            var frameOrigin = Vector2.zero;
            var frameSize = new Vector2(V6ResultLogic.DesignW, V6ResultLogic.DesignH);
            Assert.IsTrue(V6Layout.FitsIn(frameOrigin, frameSize,
                new Vector2(V6ResultLogic.ModalDialogX, V6ResultLogic.ModalDialogY),
                new Vector2(V6ResultLogic.ModalDialogW, V6ResultLogic.ModalDialogH)), "modal dialog");
        }

        [Test]
        public void ResultModalDialog_IsFullyVisibleAtDesignResolutions()
        {
            // Issue #240 の受入条件「3解像度で結果・ボタン・モーダルに重なりや
            // クリップがない」の **幾何部分**。実画面のスクリーンショットは
            // 別 (未検証)。フレームは 1:1 固定解像度なので、Dialog の矩形が
            // 画面矩形に収まること (= クリップされない) を各解像度で確認する。
            int[][] sizes =
            {
                new[] { 1920, 1080 }, // FHD
                new[] { 2560, 1440 }, // 2K
                new[] { 2560, 1664 }, // Mac
            };

            for (int i = 0; i < sizes.Length; i++)
            {
                int w = sizes[i][0];
                int h = sizes[i][1];
                Vector2 topLeft = V6ResultLogic.ModalDialogTopLeft(w, h);

                Assert.GreaterOrEqual(topLeft.x, 0f, "dialog left @ " + w + "x" + h);
                Assert.GreaterOrEqual(topLeft.y, 0f, "dialog top @ " + w + "x" + h);
                Assert.LessOrEqual(topLeft.x + V6ResultLogic.ModalDialogW, w, "dialog right @ " + w + "x" + h);
                Assert.LessOrEqual(topLeft.y + V6ResultLogic.ModalDialogH, h, "dialog bottom @ " + w + "x" + h);
            }
        }
    }
}
