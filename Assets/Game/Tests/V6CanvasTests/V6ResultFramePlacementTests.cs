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
        public void ResultMain_AndRightColumn_FitInSharedFrame()
        {
            // メインゾーンは共有フレームの Main (24,96) 1680x780 をそのまま使う (契約 §4)。
            var frameOrigin = Vector2.zero;
            var frameSize = new Vector2(V6ResultLogic.DesignW, V6ResultLogic.DesignH);
            Assert.IsTrue(V6Layout.FitsIn(frameOrigin, frameSize,
                new Vector2(V6ResultLogic.MainX, V6ResultLogic.MainY),
                new Vector2(V6ResultLogic.MainW, V6ResultLogic.MainH)));

            // 右カラム (ランキング 2 枚, 幅 528) はフレーム内に収まり、
            // 右端はフッター右端 (design x1704) に揃う (内側に寄らない)。
            float rankLeft = V6ResultLogic.MainX + V6ResultLogic.RankX;
            Assert.IsTrue(V6Layout.FitsIn(frameOrigin, frameSize,
                new Vector2(rankLeft, V6ResultLogic.MainY), new Vector2(528f, V6ResultLogic.MainH)));
            Assert.AreEqual(1704f, rankLeft + 528f);
        }

        [Test]
        public void ResultFooter_ButtonsAndLegend_FitWithoutOverlap()
        {
            // フッター右の 3 ボタン (OK / Register / Retry) は幅を内容から算出し、
            // 右端から gap16 で横並び。フッター (24,876) 1680x96 内に収まる (契約 §4)。
            float[] widths =
            {
                V6ResultLogic.FooterButtonWidth("OK", "Space", true),
                V6ResultLogic.FooterButtonWidth("Register", "Enter", true),
                V6ResultLogic.FooterButtonWidth("Retry", "R", true),
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
    }
}
