using System.Collections.Generic;
using UnityEngine;

namespace FlashBeat.UI.V6
{
    /// <summary>
    /// V6JacketProvider の crop variant 領域 (Issue #385 Phase 2.11 で分割)。
    /// 原 Texture からの切り抜き Sprite を (Texture 参照 + Rect) を鍵にキャッシュする。
    /// ローカル/遠隔は <see cref="IsRemoteTexture"/> で判定して別辞書に分ける。
    /// partial class のためフィールド・メソッドは V6JacketProvider 本体と共有する。
    /// </summary>
    public static partial class V6JacketProvider
    {
        /// <summary>
        /// crop variant のキャッシュキー。**原 Texture は参照そのもの**で比較する
        /// (ID のハッシュ値では分けられない: ハッシュ衝突で別のテクスチャの
        /// 切り抜きが返ると、誤画像がキャッシュに残り続ける)。
        /// </summary>
        private sealed class SpriteKey
        {
            public Texture2D Texture;
            public float CropX;
            public float CropY;
            public float CropW;
            public float CropH;

            public override bool Equals(object obj)
            {
                var other = obj as SpriteKey;
                if (other == null) return false;
                // Rect の丸めをしない (設計由来の float 差をそのまま鍵に含む)。
                return ReferenceEquals(Texture, other.Texture)
                    && CropX == other.CropX && CropY == other.CropY
                    && CropW == other.CropW && CropH == other.CropH;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    // 参照のハッシュ値を容器として使い、比較は Equals が担う。
                    int hash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Texture);
                    hash = hash * 397 ^ CropX.GetHashCode();
                    hash = hash * 397 ^ CropY.GetHashCode();
                    hash = hash * 397 ^ CropW.GetHashCode();
                    hash = hash * 397 ^ CropH.GetHashCode();
                    return hash;
                }
            }
        }

        // --- crop variant (原 Texture からの切り抜き) ---

        private static readonly Dictionary<SpriteKey, Sprite> localCropCache = new Dictionary<SpriteKey, Sprite>();
        private static readonly Dictionary<SpriteKey, Sprite> remoteCropCache = new Dictionary<SpriteKey, Sprite>();

        // ------------------------------------------------------------------
        // crop variant
        // ------------------------------------------------------------------

        /// <summary>
        /// 原 Texture から矩形を切り抜いた Sprite。同じ (Texture, Rect) は同じ実体を返す。
        ///
        /// design の "cover" 指定 (ローディング 56×56 等) を、原画像の再デコードなしで
        /// 作るためのもの。戻り値は provider が所有する。
        /// </summary>
        public static Sprite Crop(Texture2D texture, Rect rect, string name)
        {
            return Crop(texture, rect, name, false);
        }

        /// <summary>
        /// 既知の原 Texture からの切り抜き。このメソッドは既存の公開 API として残す。
        /// ローカル/遠隔の区別は <see cref="IsRemoteTexture"/> で判定する
        /// (遠隔テクスチャは <see cref="RequestRemote"/> の破棄後も他画面が参照しうるので、
        /// 遠隔由来の切り抜きは InvalidateLocal では消さない)。
        /// </summary>
        private static Sprite Crop(Texture2D texture, Rect rect, string name, bool forceRemote)
        {
            if (texture == null || rect.width <= 0f || rect.height <= 0f)
            {
                return null;
            }

            bool remote = forceRemote || IsRemoteTexture(texture);
            var cache = remote ? remoteCropCache : localCropCache;
            var key = new SpriteKey
            {
                Texture = texture,
                CropX = rect.x,
                CropY = rect.y,
                CropW = rect.width,
                CropH = rect.height,
            };

            Sprite cached;
            if (cache.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }

            var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
            if (!string.IsNullOrEmpty(name))
            {
                sprite.name = name;
            }

            CropCreateCount++;
            cache[key] = sprite;
            return sprite;
        }

        /// <summary>このテクスチャが遠隔取得のものか (remoteCache に載っているか)。</summary>
        private static bool IsRemoteTexture(Texture2D texture)
        {
            if (texture == null) return false;
            foreach (var pair in remoteCache)
            {
                var entry = pair.Value;
                if (entry != null && ReferenceEquals(entry.Texture, texture))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>中心を正方形に取る crop 矩形 (テクスチャ座標、原点は左下)。</summary>
        public static Rect CoverCropRect(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            float side = Mathf.Min(width, height);
            return new Rect((width - side) * 0.5f, (height - side) * 0.5f, side, side);
        }

        /// <summary>
        /// 既知の原 Texture を、そのプロジェクトに合う設定で包む（推奨の正方形 crop）。
        /// design の mode:"cover"（セル・56 の枠など）を、再デコードせずに得るためのもの。
        /// </summary>
        public static Sprite SquareCrop(Texture2D texture, string name)
        {
            if (texture == null)
            {
                return null;
            }

            return Crop(texture, CoverCropRect(texture.width, texture.height), name, false);
        }
    }
}
