using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Common.Tests
{
    /// <summary>
    /// V6UiProfile の検証。design 正本
    /// (docs/design/v6/flashbeat-v6-nodes.json, "Resolution Check · Title" / id POJwG)
    /// の実配置 (ref "01 Title · v6" x/y/w/h, scale なし) に固定する。
    /// UI はスケールしない (scale = 1)。解像度はユーザーが選ばず、
    /// 画面の実解像度から自動判定する。
    /// </summary>
    public sealed class V6UiProfileTests
    {
        private const float FrameW = 1728f;
        private const float FrameH = 972f;

        [Test]
        public void For_Fhd1920x1080_ReturnsDesignOrigin()
        {
            var profile = V6UiProfile.For(1920, 1080);
            Assert.AreEqual(new Vector2(96f, 54f), profile.Origin);
            Assert.AreEqual("FHD 1920x1080", profile.Name);
        }

        [Test]
        public void For_2k2560x1440_ReturnsDesignOrigin()
        {
            var profile = V6UiProfile.For(2560, 1440);
            Assert.AreEqual(new Vector2(416f, 234f), profile.Origin);
            Assert.AreEqual("2K 2560x1440", profile.Name);
        }

        [Test]
        public void For_Mac2560x1664_ReturnsDesignOrigin()
        {
            var profile = V6UiProfile.For(2560, 1664);
            Assert.AreEqual(new Vector2(416f, 346f), profile.Origin);
            Assert.AreEqual("MacBook 2560x1664", profile.Name);
        }

        [Test]
        public void For_AlwaysMatchesV6FrameOriginFor()
        {
            // V6UiProfile.OriginFor と V6Frame.OriginFor は同一の式であり、
            // 登録解像度・未知解像度を問わず origin が一致しなければならない。
            int[] widths = { 1920, 2560, 3440, 1280, 1728, 1930, 3840 };
            int[] heights = { 1080, 1440, 1440, 720, 972, 1090, 2160 };
            for (var i = 0; i < widths.Length; i++)
            {
                Assert.AreEqual(
                    V6Frame.OriginFor(widths[i], heights[i]),
                    V6UiProfile.For(widths[i], heights[i]).Origin,
                    $"screen {widths[i]}x{heights[i]}");
                Assert.AreEqual(
                    V6Frame.OriginFor(widths[i], heights[i]),
                    V6UiProfile.OriginFor(widths[i], heights[i]),
                    $"screen {widths[i]}x{heights[i]}");
            }
        }

        [Test]
        public void For_UnknownResolution_KeepsFrameSizeAndCentersOrigin()
        {
            // 未知解像度でも FrameW/FrameH は 1728×972 のまま (スケールしない)。
            // origin は中央配置の式 ((w-1728)/2, (h-972)/2 切り捨て) に従う。
            var ultrawide = V6UiProfile.For(3440, 1440);
            Assert.AreEqual(FrameW, ultrawide.FrameW);
            Assert.AreEqual(FrameH, ultrawide.FrameH);
            Assert.AreEqual(new Vector2((3440f - FrameW) / 2f, (1440f - FrameH) / 2f), ultrawide.Origin);
            Assert.AreEqual(new Vector2(856f, 234f), ultrawide.Origin);

            var small = V6UiProfile.For(1280, 720);
            Assert.AreEqual(FrameW, small.FrameW);
            Assert.AreEqual(FrameH, small.FrameH);
            Assert.AreEqual(new Vector2(-224f, -126f), small.Origin);
        }

        [Test]
        public void Known_ContainsThreeProfiles()
        {
            Assert.AreEqual(3, V6UiProfile.Known.Length);
            CollectionAssert.AreEquivalent(
                new[] { "FHD 1920x1080", "2K 2560x1440", "MacBook 2560x1664" },
                new[]
                {
                    V6UiProfile.Known[0].Name,
                    V6UiProfile.Known[1].Name,
                    V6UiProfile.Known[2].Name,
                });
        }

        [Test]
        public void AllProfiles_KeepFixedFrameSize()
        {
            // どのプロファイルもフレーム寸法は 1728×972 固定 (拡大縮小しない)。
            foreach (var profile in V6UiProfile.Known)
            {
                Assert.AreEqual(FrameW, profile.FrameW, profile.Name);
                Assert.AreEqual(FrameH, profile.FrameH, profile.Name);
            }
        }

        [Test]
        public void NearestIndex_PicksClosestScreenDimensions()
        {
            Assert.AreEqual(0, V6UiProfile.NearestIndex(1920, 1080));
            Assert.AreEqual(1, V6UiProfile.NearestIndex(2560, 1440));
            Assert.AreEqual(2, V6UiProfile.NearestIndex(2560, 1664));
            // 3440×1440: 2K まで |3440-2560|+0 = 880 / Mac まで 880+224 = 1104 → 2K
            Assert.AreEqual(1, V6UiProfile.NearestIndex(3440, 1440));
            // 1280×720: FHD まで 640+360 = 1000 / 2K まで 1280+720 = 2000 → FHD
            Assert.AreEqual(0, V6UiProfile.NearestIndex(1280, 720));
            // 4K 3840×2160: 2K まで 1280+720 = 2000 / Mac まで 1280+496 = 1776 → Mac
            Assert.AreEqual(2, V6UiProfile.NearestIndex(3840, 2160));
        }

        [Test]
        public void For_ExactMatch_UsesDesignOriginNotRecomputed()
        {
            // design の実配置 ((96,54)/(416,234)/(416,346)) は中央配置の式と一致する。
            // 完全一致時は design 表の値がそのまま使われることを等価性で固定する。
            foreach (var known in V6UiProfile.Known)
            {
                var resolved = V6UiProfile.For(known.ScreenW, known.ScreenH);
                Assert.AreEqual(known.Origin, resolved.Origin, known.Name);
                Assert.AreEqual(known.Name, resolved.Name);
            }
        }
    }
}
