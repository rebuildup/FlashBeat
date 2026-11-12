using UnityEngine;

namespace FlashBeat.UI.V6
{
    /// <summary>
    /// v6 の画像系共通部品の**単一の生成元** (Issue #401)。
    ///
    /// 一次監査で見つかった重複:
    ///   - <c>V5Ui.RoundedSprite</c> と <c>V6Controls.RoundedSprite</c> が
    ///     ほぼ同じ 9-slice 生成を **それぞれの静的辞書に** キャッシュしていた。
    ///     ⇒ 同じ半径でも 2 枚のテクスチャが存在した。
    ///   - <c>V6Icons.LoadSprite</c> は呼ぶたび <c>Resources.Load&lt;Sprite&gt;</c> を解決。
    ///   - <c>V6TitleView.BuildMain</c> / <c>V5GameUi.LoadSprite</c> / <c>V5Decor.LoadSprite</c>
    ///     が個別に <c>Sprite.Create</c> し、所有者が曖昧だった。
    ///
    /// ここで扱うのは **見た目が同一で意味も同じ** ものだけ:
    ///   - 半径だけで決まる角丸 9-slice (色は Image.color で付けるので共有できる)
    ///   - Resources のアイコン Sprite (不変アセット)
    ///   - 原 Texture からの単純な全面 Sprite (pivot 中央・PPU 100)
    ///
    /// ここで扱わないもの (無理に統合しない):
    ///   - v5 の角丸は FHD 実ピクセル基準、v6 は design px 基準で、
    ///     半径の**意味**が違う。統合するのは「生成アルゴリズムと実体」だけ。
    ///     どちらのスケールで呼ぶかは呼び出し側 (V5Ui / V6Controls) が決める。
    ///   - 画面固有の切り抜き (crop) は <see cref="V6JacketProvider"/> の variant。
    /// </summary>
    public static class V6Sprites
    {
        /// <summary>角丸 9-slice のアンチエイリアス余白 (テクスチャ px)。</summary>
        internal const int AntialiasPad = 2;

        /// <summary>
        /// 白で塗った角丸 9-slice。半径ごとに 1 枚だけ実体化し、全画面で共有する。
        /// 色は <c>Image.color</c> で付ける (スプライトは白のまま)。
        ///
        /// 戻り値は <see cref="V6AssetOwner"/> が **プロセス寿命で所有** する。
        /// 呼び出し側は Destroy しない。
        /// </summary>
        public static Sprite Rounded(int radiusPx)
        {
            int radius = Mathf.Max(1, radiusPx);
            return V6AssetOwner.Shared(CacheKey(radius), () => BuildRounded(radius));
        }

        /// <summary>既に角丸スプライトがあるか (生成せずに確認)。</summary>
        public static bool HasRounded(int radiusPx)
        {
            return V6AssetOwner.HasShared(CacheKey(Mathf.Max(1, radiusPx)));
        }

        /// <summary>
        /// Resources のアイコン Sprite。同じ名前は同じ実体を返し、
        /// 呼ぶたびに <c>Resources.Load</c> しない。
        ///
        /// Resources の Sprite は Unity 所有なので、provider は参照を持つだけで破棄しない。
        /// </summary>
        public static Sprite Icon(string iconName)
        {
            if (string.IsNullOrEmpty(iconName))
            {
                return null;
            }

            return V6AssetOwner.Shared("icon:" + iconName,
                () => Resources.Load<Sprite>("Icons/" + iconName));
        }

        /// <summary>
        /// 原 Texture の全面 Sprite (pivot 中央 / PPU 100)。原 Texture と
        /// パラメータが同じなら同じ実体を返す。
        ///
        /// 原 Texture は Unity 所有 (Resources.Load) のことが多いので破棄しない。
        /// 生成した Sprite は provider が所有する。
        /// </summary>
        public static Sprite FromTexture(Texture2D texture, string name)
        {
            if (texture == null)
            {
                return null;
            }

            string key = "tex:" + texture.GetEntityId() + ":" + texture.width + "x" + texture.height
                + ":" + (name ?? "");
            return V6AssetOwner.Shared(key, () =>
            {
                var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                if (!string.IsNullOrEmpty(name))
                {
                    sprite.name = name;
                }

                return sprite;
            });
        }

        /// <summary>
        /// 角丸スプライトの半径 (design px)。角丸スプライトでなければ -1。
        ///
        /// 見た目の契約は「半径が design の値か」であって、実体の名前ではない
        /// (Issue #401 でキャッシュを統合したとき、名前で判定していたテストが
        /// 実装の都合に固定されていた)。9-slice の border は radius + 余白なので、
        /// 余白を引いて半径として返す。
        /// </summary>
        public static int RoundedRadiusOf(Sprite sprite)
        {
            if (sprite == null)
            {
                return -1;
            }

            Vector4 border = sprite.border;
            // 4 辺すべてが同じで 0 でなければ角丸 9-slice。
            if (border.x <= 0f || border.x != border.y || border.y != border.z || border.z != border.w)
            {
                return -1;
            }

            return Mathf.RoundToInt(border.x) - AntialiasPad;
        }

        /// <summary>計測用: このプロセスで保持している共有スプライト数。</summary>
        public static int SharedCount
        {
            get { return V6AssetOwner.SharedCount; }
        }

        /// <summary>計測用: 共有アセットを実際に生成した回数。</summary>
        public static int CreateCount
        {
            get { return V6AssetOwner.SharedCreateCount; }
        }

        private static string CacheKey(int radius)
        {
            return "rounded:" + radius;
        }

        private static Sprite BuildRounded(int radius)
        {
            // +1: 9-slice の中央ストレッチ領域を最低 1px 確保する
            // (border の合計 2*(radius+pad) がテクスチャ幅と一致すると中央が 0px になり伸縮しない)。
            int size = radius * 2 + AntialiasPad * 2 + 1;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "V6RoundedTexture" + radius,
            };

            var pixels = new Color32[size * size];
            var opaque = new Color32(255, 255, 255, 255);
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 角の中心からの距離でアルファを決める (AA 付き角丸)。
                    float cx = Mathf.Min(x, size - 1 - x);
                    float cy = Mathf.Min(y, size - 1 - y);
                    float dx = Mathf.Max(0f, radius - 0.5f - cx);
                    float dy = Mathf.Max(0f, radius - 0.5f - cy);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - dist);
                    pixels[y * size + x] = alpha <= 0f ? clear : Color32.Lerp(clear, opaque, alpha);
                }
            }

            tex.SetPixels32(pixels);
            // makeNoLongerReadable: 生成後に CPU 側のコピーを捨てる (メモリ半減)。
            tex.Apply(false, true);

            float border = radius + AntialiasPad;
            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = "V6Rounded" + radius;
            return sprite;
        }
    }
}
