namespace FlashBeat.UI.V6
{
    /// <summary>
    /// Issue #824: 動画サムネイルを背景としてアスペクト比を保ったまま
    /// 対象領域全体に cover 表示するための UV 矩形。
    ///
    /// 引き伸ばし (scale 1,1) では 4:3 の hqdefault が 16:9 画面で
    /// 縦につぶれて見える。代わりに短辺を合わせはみ出た側を中央で
    /// クロップする UV を返す。uGUI の <c>RawImage.uvRect</c> と
    /// マテリアルの <c>_MainTex_ST</c> (scale=uvRect.size,
    /// offset=uvRect.position) の両方にそのまま使える。
    /// </summary>
    public static class V6BackdropFit
    {
        /// <summary>
        /// <paramref name="texW"/>×<paramref name="texH"/> のテクスチャを
        /// <paramref name="viewW"/>×<paramref name="viewH"/> の領域へ
        /// cover する UV 矩形 (0..1、左下原点)。
        /// 寸法が取れない場合は全面 (0,0,1,1) を返す。
        /// </summary>
        public static UnityEngine.Rect CoverUV(float texW, float texH, float viewW, float viewH)
        {
            if (texW <= 0f || texH <= 0f || viewW <= 0f || viewH <= 0f)
            {
                return new UnityEngine.Rect(0f, 0f, 1f, 1f);
            }

            float texAspect = texW / texH;
            float viewAspect = viewW / viewH;

            if (texAspect > viewAspect)
            {
                // テクスチャの方が横長 → 左右を削る。
                float w = viewAspect / texAspect;
                return new UnityEngine.Rect((1f - w) * 0.5f, 0f, w, 1f);
            }

            // テクスチャの方が縦長 → 上下を削る。
            float h = texAspect / viewAspect;
            return new UnityEngine.Rect(0f, (1f - h) * 0.5f, 1f, h);
        }

        /// <summary>
        /// <see cref="CoverUV"/> と同じ cover 規則を、テクスチャのピクセル座標
        /// (左下原点、<c>Sprite.Create</c> の rect と同じ系) で返す。
        /// uGUI の <c>Image</c> は UV を直接いじれないため、ジャケットのように
        /// 表示側がスプライトを要求する経路ではこちらで切り抜く (Issue #848)。
        /// </summary>
        public static UnityEngine.Rect CoverRect(float texW, float texH, float viewW, float viewH)
        {
            UnityEngine.Rect uv = CoverUV(texW, texH, viewW, viewH);
            return new UnityEngine.Rect(uv.x * texW, uv.y * texH, uv.width * texW, uv.height * texH);
        }
    }
}
