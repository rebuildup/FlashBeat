using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Option;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.V5CanvasTests
{
    /// <summary>
    /// v6 共通フレームの 1:1 固定解像度配置 (純ロジック)。
    ///
    /// 旧テスト (V6OptionDesignScaleTests) は「共通フレーム 1728x972 を
    /// min(W/1728, H/972) 倍して引き伸ばす」前提を固定していたが、
    /// Owner 指示「design フレームは固定解像度。引き伸ばしは一切起こらない。
    /// そのために各解像度にフレームがわざわざある」により契約変更になったため、
    /// 1:1 中央配置の origin 契約に置き換えた。
    ///
    /// 正本: design gaI3p "Resolution Check · Option" の実配置 —
    /// 各解像度フレーム (FHD 1920×1080 / 2K 2560×1440 / Mac 2560×1664) に
    /// 06 Play · v6 (B6If6Z) が 1:1 で載る:
    /// - FHD 1920×1080 → origin (96, 54)
    /// - 2K 2560×1440 → origin (416, 234)
    /// - Mac 2560×1664 → origin (416, 346)
    /// (いずれも ((w−1728)/2, (h−972)/2) の整数切り捨て)。
    ///
    /// ゲーム HUD は解像度別固有フレーム (契約 §8) なので本テストの対象外。
    /// </summary>
    public class V6OptionFramePlacementTests
    {
        private static readonly int[][] DesignResolutions =
        {
            new[] { 1920, 1080 },   // FHD
            new[] { 2560, 1440 },   // 2K
            new[] { 2560, 1664 },   // MacBook Air M5
        };

        private static readonly Vector2[] DesignOrigins =
        {
            new Vector2(96f, 54f),
            new Vector2(416f, 234f),
            new Vector2(416f, 346f),
        };

        [Test]
        public void OptionFrameOrigin_MatchesDesignResolutionTable()
        {
            for (int i = 0; i < DesignResolutions.Length; i++)
            {
                int w = DesignResolutions[i][0];
                int h = DesignResolutions[i][1];
                Assert.AreEqual(DesignOrigins[i], V6OptionLogic.FrameOrigin(w, h),
                    $"{w}x{h}: design gaI3p の origin 表と一致しない");
            }
        }

        [Test]
        public void OptionFrameOrigin_IsCenteredFloorFormula_AtOtherResolutions()
        {
            // design 表に無い解像度も同じ式 ((w−1728)/2, (h−972)/2) 切り捨てに従う。
            Assert.AreEqual(Vector2.zero, V6OptionLogic.FrameOrigin(1728, 972));
            Assert.AreEqual(new Vector2(96f, 114f), V6OptionLogic.FrameOrigin(1920, 1200));
            // 奇数余りは切り捨て (design 表記は整数 origin)
            Assert.AreEqual(Vector2.zero, V6OptionLogic.FrameOrigin(1729, 973));
            // フレーム未満はクランプしない (負 origin = 中央のままはみ出す)
            Assert.AreEqual(new Vector2(-224f, -126f), V6OptionLogic.FrameOrigin(1280, 720));
        }

        [Test]
        public void OptionFrameOrigin_InvalidSize_FallsBackToZero()
        {
            Assert.AreEqual(Vector2.zero, V6OptionLogic.FrameOrigin(0, 0));
            Assert.AreEqual(Vector2.zero, V6OptionLogic.FrameOrigin(1920, -1));
        }

        [Test]
        public void OptionFrameOrigin_NeverScales()
        {
            // 1:1 配置の契約上、Option ロジックに倍率 API は存在しない
            // (min(W/1728, H/972) の復活 = 引き伸ばしの再導入を防ぐ)。
            var method = typeof(V6OptionLogic).GetMethod("DesignScale");
            Assert.IsNull(method, "DesignScale (引き伸ばし倍率) は廃止済み。再追加しないこと");
        }

        [Test]
        public void SharedFrameSize_IsSameForOptionAndSelect()
        {
            Assert.AreEqual(V6SelectViewLogic.DesignW, V6OptionLogic.DesignW, 0.0001f);
            Assert.AreEqual(V6SelectViewLogic.DesignH, V6OptionLogic.DesignH, 0.0001f);
        }
    }
}
