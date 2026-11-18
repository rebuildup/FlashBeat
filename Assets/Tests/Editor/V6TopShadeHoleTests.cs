using FlashBeat.UI.V6.Game.Hud;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #675: 上部シェードがレーン帯 (ノーツが通る領域) を暗くしない
    /// ことを検証する構造テスト。
    ///
    /// シェードは Screen Space Overlay の Image で 3D の深度テストを通らないため、
    /// グラデーションのままだとアプローチ中のノーツが黒く沈む。修正後は
    /// テクスチャにレーン帯の抜きが焼き込まれ、帯の内側のアルファは 0。
    /// </summary>
    public class V6TopShadeHoleTests
    {
        private const float ScreenW = 1920f;
        private const float ScreenH = 1080f;
        private const int ShadeH = 260; // V6HudMetrics.Fhd().TopShadeH
        private const int Rows = 64;

        [Test]
        public void CorridorRows_NarrowsTowardTop_AndStaysOnScreen()
        {
            var left = new float[Rows];
            var right = new float[Rows];
            V6TopShadeHole.CorridorRows(ScreenW, ScreenH, ShadeH, Rows, left, right);

            for (int i = 0; i < Rows; i++)
            {
                Assert.Less(left[i], right[i], $"row {i}: corridor inverted");
                Assert.GreaterOrEqual(left[i], -V6TopShadeHole.CorridorMarginPx - 1f);
                Assert.LessOrEqual(right[i], ScreenW + V6TopShadeHole.CorridorMarginPx + 1f);
            }

            // 行 0 = シェード下端、行 Rows-1 = 上端。遠近法なので帯は上ほど狭い。
            Assert.Less(left[Rows - 1], left[0] + ScreenW * 0.25f,
                "corridor should move right toward the top");
            Assert.Greater(right[Rows - 1], right[0] - ScreenW * 0.25f,
                "corridor should move left toward the top");
            Assert.Less(right[Rows - 1] - left[Rows - 1],
                right[0] - left[0], "top corridor must be narrower than bottom");
        }

        [Test]
        public void ShadeAlpha_InsideCorridor_IsClear_Outside_KeepsGradient()
        {
            const float gradient = 0.65f;
            Assert.AreEqual(0f, V6TopShadeHole.ShadeAlpha(500f, 400f, 600f, gradient));
            Assert.AreEqual(gradient, V6TopShadeHole.ShadeAlpha(100f, 400f, 600f, gradient));
            Assert.AreEqual(gradient, V6TopShadeHole.ShadeAlpha(700f, 400f, 600f, gradient));
        }

        /// <summary>
        /// 実際に構築した HUD の Top Shade スプライトを読み、レーン帯の内側が
        /// 透明・帯の外側 (上端) が暗いことを確認する。EditMode ではカメラが
        /// 無いことが多いので、帯の位置は <see cref="V6TopShadeHole.CorridorRows"/>
        /// (本番と同じ関数) から取る。
        /// </summary>
        [Test]
        public void TopShadeSprite_HasLaneCorridorHole()
        {
            var canvasGo = new GameObject("V6TopShadeHoleTests Canvas", typeof(Canvas));
            try
            {
                var canvasRect = (RectTransform)canvasGo.transform;
                canvasRect.sizeDelta = new Vector2(ScreenW, ScreenH);
                var view = FlashBeat.UI.V6.Game.Hud.V6GameHudView.Create(canvasRect);

                var shade = view.TopShadeForAudit;
                Assert.NotNull(shade, "Top Shade image was not built");
                Assert.NotNull(shade.sprite, "Top Shade has no sprite");

                var tex = shade.sprite.texture;
                Assert.NotNull(tex);
                Assert.AreEqual(V6TopShadeHole.TextureWidth, tex.width);
                Assert.AreEqual(ShadeH, tex.height);

                // 上端行 (テクスチャ行 = 高さ-1) のレーン帯中央が透明であること。
                var left = new float[ShadeH];
                var right = new float[ShadeH];
                V6TopShadeHole.CorridorRows(ScreenW, ScreenH, ShadeH, ShadeH, left, right);
                int topRow = ShadeH - 1;
                float midX = (left[topRow] + right[topRow]) * 0.5f;
                int texX = Mathf.Clamp(Mathf.RoundToInt(midX / ScreenW * (tex.width - 1)), 0, tex.width - 1);
                Assert.AreEqual(0f, tex.GetPixel(texX, topRow).a, 0.001f,
                    "lane corridor must be clear at the top of the shade");

                // 帯の外側 (左端) は上端でほぼ不透明のまま (HUD 文字の可読性を維持)。
                float edgeAlpha = tex.GetPixel(2, topRow).a;
                Assert.Greater(edgeAlpha, 0.5f,
                    "outside the lane corridor the shade must keep its gradient");
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
        }
    }
}
