using System.Collections.Generic;
using UnityEngine;

namespace FlashBeat.UI.V6.Game.Hud
{
    /// <summary>
    /// HUD の中央パネル (歌詞ボックス等) がレーン回廊を暗くしないための
    /// カットアウトスプライト (Issue #760)。
    ///
    /// 歌詞ボックスは黒 α≈0.6 の半透明パネルで、上部シェードと同じく
    /// Screen Space Overlay の Image なので 3D の深度テストを通らない。
    /// レーン帯の抜きが無いままだと、パネルの帯を通過するノーツが
    /// 最終合成で ~40% 輝度まで暗くなる (#735 の画素経路調査)。
    /// #734 で歌詞 ON の間は枠が演奏中常時表示になったため影響が恒常化した。
    ///
    /// 対処は <see cref="V6TopShadeHole"/> と同じ「テクスチャにレーン帯の抜きを
    /// 焼き込む」方式。パネル全面を 1 テクセル = 1 canvas px で生成し、
    /// 各行のレーン帯 (<see cref="V6TopShadeHole.CorridorRows(float, float, float, float, int, float[], float[])"/>)
    /// の内側ではアルファを 0 にする。角丸はパネル本体と同じ半径の
    /// SDF で焼き込む (9-slice の実体は帯の抜きと両立しないため、
    /// ここでは実寸テクスチャ + <see cref="Image.Type.Simple"/> で使う)。
    ///
    /// 帯の外側 (レーンの左右) ではパネルの暗幕が残るので、
    /// パネルに重なる HUD 文字の視認性は従来どおり確保される。
    /// レーン帯の内側では暗幕が消えるため、歌詞本文は
    /// 動画の上に直接乗る — 可読性は文字側の縁取り
    /// (V6HudLyrics の outline) で担保する。
    /// </summary>
    public static class V6PanelCorridorHole
    {
        /// <summary>
        /// パネル用スプライトのキャッシュ (寸法・位置ごと)。
        /// 解像度プロファイルは少数だが EditMode テスト等で任意サイズが
        /// 作られ得るので頭打ちにする (上部シェードと同じ方針)。
        /// </summary>
        private static readonly Dictionary<string, Sprite> sprites =
            new Dictionary<string, Sprite>();

        private const int CacheLimit = 8;

        /// <summary>
        /// 画面上の矩形 (<paramref name="leftPx"/>, <paramref name="topPx"/> は
        /// 画面左上原点 px、<paramref name="widthPx"/> × <paramref name="heightPx"/>)
        /// に置く角丸パネルのスプライトを返す。テクスチャは実寸 px で、
        /// レーン帯の内側は透明。色は Image.color 側で付ける (スプライトは白)。
        /// </summary>
        /// <param name="screenSize">HUD canvas の実ピクセル寸法。</param>
        /// <param name="cornerRadiusPx">角丸半径 (canvas px)。</param>
        public static Sprite ForPanel(float leftPx, float topPx,
            float widthPx, float heightPx, Vector2 screenSize, float cornerRadiusPx)
        {
            int w = Mathf.Max(2, Mathf.RoundToInt(widthPx));
            int h = Mathf.Max(2, Mathf.RoundToInt(heightPx));
            string key = leftPx.ToString("0.#") + "," + topPx.ToString("0.#")
                + "," + w + "x" + h
                + "," + screenSize.x.ToString("0") + "x" + screenSize.y.ToString("0")
                + "," + cornerRadiusPx.ToString("0.#");
            if (sprites.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.name = "V6PanelCorridorHole";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;

            // テクスチャ行 y (0 = 下端) ごとのレーン帯 (画面 px)。
            // 帯の上端 = topPx、高さ = h なので、行 y は画面上端から
            // topPx + h - y 付近を通る — CorridorRows の行規約
            // (0 = 帯下端) と一致するようそのまま index する。
            float[] corridorLeft = new float[h];
            float[] corridorRight = new float[h];
            V6TopShadeHole.CorridorRows(screenSize.x, screenSize.y,
                topPx, h, h, corridorLeft, corridorRight);

            float radius = Mathf.Min(cornerRadiusPx, Mathf.Min(w, h) * 0.5f);
            for (int y = 0; y < h; y++)
            {
                float left = corridorLeft[y];
                float right = corridorRight[y];
                for (int x = 0; x < w; x++)
                {
                    // テクセル中心の画面座標 (左原点 px)。
                    // 最終アルファ = 角丸被覆率 × (レーン帯の外なら 1 / 内なら 0)。
                    // RGB は白のまま — 色は Image.color (LyricBoxColor) が掛ける。
                    float sx = leftPx + x + 0.5f;
                    float shape = RoundedAlpha(x + 0.5f, y + 0.5f, w, h, radius);
                    float a = V6TopShadeHole.ShadeAlpha(sx, left, right, shape);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "V6PanelCorridorHole";
            sprite.hideFlags = HideFlags.HideAndDontSave;

            if (sprites.Count >= CacheLimit)
            {
                sprites.Clear();
            }

            sprites[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 角丸矩形の被覆率 (0..1、約 1px の AA 付き)。
        /// テクセル中心座標 (左下原点) で評価する。
        /// </summary>
        private static float RoundedAlpha(float px, float py, float w, float h, float radius)
        {
            if (radius <= 0f)
            {
                return (px >= 0f && px <= w && py >= 0f && py <= h) ? 1f : 0f;
            }

            // 角丸矩形の符号付き距離 (外側が正)。
            float qx = Mathf.Abs(px - w * 0.5f) - (w * 0.5f - radius);
            float qy = Mathf.Abs(py - h * 0.5f) - (h * 0.5f - radius);
            float ox = Mathf.Max(qx, 0f);
            float oy = Mathf.Max(qy, 0f);
            float d = Mathf.Min(Mathf.Max(qx, qy), 0f)
                + Mathf.Sqrt(ox * ox + oy * oy) - radius;
            return Mathf.Clamp01(0.5f - d);
        }
    }
}
