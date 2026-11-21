using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6;

namespace FlashBeat.V6HudTests
{
    /// <summary>
    /// Issue #824: 開始前背景 (動画サムネイル) の cover UV の EditMode テスト。
    /// アスペクト比を保ったまま対象領域を埋める中央クロップを検証する。
    /// </summary>
    public class V6BackdropFitTests
    {
        [Test]
        public void CoverUV_SameAspect_ReturnsFullTexture()
        {
            var uv = V6BackdropFit.CoverUV(1920f, 1080f, 1920f, 1080f);
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), uv);
        }

        [Test]
        public void CoverUV_WiderTexture_CropsLeftRight()
        {
            // 16:9 テクスチャを 4:3 領域へ → 左右を削る。
            var uv = V6BackdropFit.CoverUV(1920f, 1080f, 1440f, 1080f);
            // viewAspect = 4/3, texAspect = 16/9 → w = (4/3)/(16/9) = 0.75
            Assert.AreEqual(0.75f, uv.width, 0.0001f);
            Assert.AreEqual(1f, uv.height, 0.0001f);
            Assert.AreEqual(0.125f, uv.x, 0.0001f);
            Assert.AreEqual(0f, uv.y, 0.0001f);
        }

        [Test]
        public void CoverUV_TallerTexture_CropsTopBottom()
        {
            // 4:3 テクスチャ (hqdefault) を 16:9 領域へ → 上下を削る。
            var uv = V6BackdropFit.CoverUV(480f, 360f, 1920f, 1080f);
            // texAspect = 4/3, viewAspect = 16/9 → h = (4/3)/(16/9) = 0.75
            Assert.AreEqual(1f, uv.width, 0.0001f);
            Assert.AreEqual(0.75f, uv.height, 0.0001f);
            Assert.AreEqual(0f, uv.x, 0.0001f);
            Assert.AreEqual(0.125f, uv.y, 0.0001f);
        }

        [Test]
        public void CoverUV_DegenerateInput_ReturnsFullTexture()
        {
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), V6BackdropFit.CoverUV(0f, 1080f, 1920f, 1080f));
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), V6BackdropFit.CoverUV(1920f, 1080f, 0f, 1080f));
        }

        // --- Issue #848: CoverRect (Sprite.Create の rect 用ピクセル座標) ---

        [Test]
        public void CoverRect_SameAspect_ReturnsFullTexture()
        {
            var rect = V6BackdropFit.CoverRect(480f, 360f, 640f, 480f);
            Assert.AreEqual(new Rect(0f, 0f, 480f, 360f), rect);
        }

        [Test]
        public void CoverRect_TallerTexture_CropsTopBottom()
        {
            // hqdefault (4:3) を 16:9 のジャケット枠へ → 上下を削る。
            var rect = V6BackdropFit.CoverRect(480f, 360f, 512f, 288f);
            // CoverUV と同じ規則: uv = (0, 0.125, 1, 0.75) → px = (0, 45, 480, 270)。
            Assert.AreEqual(0f, rect.x, 0.001f);
            Assert.AreEqual(45f, rect.y, 0.001f);
            Assert.AreEqual(480f, rect.width, 0.001f);
            Assert.AreEqual(270f, rect.height, 0.001f);
        }

        [Test]
        public void CoverRect_WiderTexture_CropsLeftRight()
        {
            // 16:9 のテクスチャを 4:3 の枠へ → 左右を削る。
            var rect = V6BackdropFit.CoverRect(640f, 360f, 400f, 300f);
            // uv = (0.125, 0, 0.75, 1) → px = (80, 0, 480, 360)。
            Assert.AreEqual(80f, rect.x, 0.001f);
            Assert.AreEqual(0f, rect.y, 0.001f);
            Assert.AreEqual(480f, rect.width, 0.001f);
            Assert.AreEqual(360f, rect.height, 0.001f);
        }

        [Test]
        public void CoverRect_DegenerateInput_ReturnsFullTexture()
        {
            Assert.AreEqual(new Rect(0f, 0f, 480f, 360f),
                V6BackdropFit.CoverRect(480f, 360f, 0f, 288f));
            Assert.AreEqual(new Rect(0f, 0f, 480f, 360f),
                V6BackdropFit.CoverRect(480f, 360f, 512f, 0f));
        }
    }
}
