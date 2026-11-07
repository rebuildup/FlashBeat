using UnityEngine;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// Issue #120: Title 画面のレイアウト純関数。design 01 Title (1920x1080) 実測値を
    /// 画面 (W,H) への「比率」として持つので、16:9 / 16:10 / 4:3 のどの解像度でも
    /// ロゴが上端に食い込まず完全に見え、フレーム線が画面の右下まで伸びる。
    /// 各 Rect の y は画面上端からの距離 (design 座標)。適用は TitleView 側で行う。
    /// </summary>
    public static class TitleLayout
    {
        // --- design 01 Title (1920x1080) 実測比率 ---

        /// <summary>ロゴ上端 y (115/1080)。</summary>
        public const float LogoTopFrac = 115f / 1080f;

        /// <summary>ロゴ高さ (225/1080)。フィードバック指定: 高さは画面高さに比例。</summary>
        public const float LogoHFrac = 225f / 1080f;

        /// <summary>横バー上端 y (424/1080)。</summary>
        public const float BarTopFrac = 424f / 1080f;

        /// <summary>フレーム線の太さ (16/1080、高さ基準でロゴと同じ規則)。</summary>
        public const float LineHFrac = 16f / 1080f;

        /// <summary>フレーム右端 x (1500/1920)。</summary>
        public const float FrameRightFrac = 1500f / 1920f;

        /// <summary>縦バー上端 y (440/1080 = 横バーの下端)。</summary>
        public const float BarVTopFrac = 440f / 1080f;

        /// <summary>メニュー左端 x (256/1920)。</summary>
        public const float MenuLeftFrac = 256f / 1920f;

        /// <summary>メニュー上端 y (603.5/1080、先頭行の上端)。</summary>
        public const float MenuTopFrac = 603.5f / 1080f;

        /// <summary>メニュー幅 (332/1920)。</summary>
        public const float MenuWFrac = 332f / 1920f;

        /// <summary>行高さ (64/1080)。</summary>
        public const float RowHFrac = 64f / 1080f;

        /// <summary>行間 (10/1080)。</summary>
        public const float RowGapFrac = 10f / 1080f;

        /// <summary>カタログ バッジ左端 x (652.5/1920)。</summary>
        public const float CatalogLeftFrac = 652.5f / 1920f;

        /// <summary>カタログ バッジ上端 y (695.5/1080)。</summary>
        public const float CatalogTopFrac = 695.5f / 1080f;

        /// <summary>凡例行左端 x (659/1920)。</summary>
        public const float LegendLeftFrac = 659f / 1920f;

        /// <summary>凡例行上端 y (775.5/1080)。</summary>
        public const float LegendTopFrac = 775.5f / 1080f;

        /// <summary>計算結果。y はすべて画面上端からの距離。</summary>
        public struct TitleRects
        {
            public Rect Logo;
            public Rect BarH;
            public Rect BarV;
            public Rect Menu;
            public Rect Catalog;
            public Rect Legend;
            public float RowH;
            public float RowGap;
        }

        /// <summary>
        /// 画面サイズと表示行数から各要素の Rect を求める。
        /// ロゴは左右中央 (design もほぼ中央)、幅は高さから設計比率で算出し、
        /// 横に極端に狭い画面でも画面幅を超えないよう上限をかける
        /// (preserveAspect で描画は比率を保つ)。
        /// </summary>
        public static TitleRects Compute(float screenW, float screenH, int visibleRows)
        {
            var r = new TitleRects();

            float logoH = screenH * LogoHFrac;
            float logoW = Mathf.Min(logoH * V5Decor.LogoAspect, screenW * 0.95f);
            r.Logo = CenteredX(screenW, logoW, screenH * LogoTopFrac, logoH);

            float thickness = screenH * LineHFrac;
            float frameRight = screenW * FrameRightFrac;
            r.BarH = new Rect(0f, screenH * BarTopFrac, frameRight, thickness);
            // 縦バーは横バーの右端に合わせ、画面下端まで伸ばす (design: 下端で画面外へ)。
            r.BarV = new Rect(frameRight - thickness, screenH * BarVTopFrac, thickness,
                screenH - screenH * BarVTopFrac);

            float rowH = screenH * RowHFrac;
            float rowGap = screenH * RowGapFrac;
            int rows = Mathf.Max(1, visibleRows);
            r.RowH = rowH;
            r.RowGap = rowGap;
            r.Menu = new Rect(screenW * MenuLeftFrac, screenH * MenuTopFrac,
                screenW * MenuWFrac, rows * rowH + (rows - 1) * rowGap);

            r.Catalog = new Rect(screenW * CatalogLeftFrac, screenH * CatalogTopFrac,
                screenW * 0.25f, screenH * 32f / 1080f);
            r.Legend = new Rect(screenW * LegendLeftFrac, screenH * LegendTopFrac,
                screenW * 0.3f, screenH * 36f / 1080f);
            return r;
        }

        /// <summary>左右中央・上端 yTop の Rect (y は上端基準)。</summary>
        private static Rect CenteredX(float screenW, float width, float yTop, float height)
        {
            return new Rect((screenW - width) * 0.5f, yTop, width, height);
        }
    }
}
