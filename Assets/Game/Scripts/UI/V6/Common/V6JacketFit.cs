using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.UI.V6
{
    /// <summary>
    /// Issue #848: ジャケット画像をスロットへ収める共通のフィット規則。
    ///
    /// design は Hero (512×288) / リスト行 Thumb (96×54) / バッジ Thumb とも
    /// 「cover」(アスペクトを保ち、はみ出た側を中央で切る)。ところがリモート
    /// ジャケットは常に hqdefault (4:3) で、uGUI の <c>Image</c> は UV を
    /// 直接いじれないため、Simple + preserveAspect では 16:9 枠の中で
    /// レターボックス状に縮んで見えていた。
    ///
    /// ローディング背景と同じ規則 (<see cref="V6BackdropFit.CoverUV"/>) を
    /// 使い、原 Texture から切り抜いた Sprite (<see cref="V6JacketProvider.Crop"/>)
    /// を返す。切り抜きは provider がキャッシュするので、同じ曲・同じ枠なら
    /// 実体は増えない。
    /// </summary>
    public static class V6JacketFit
    {
        /// <summary>
        /// <paramref name="sprite"/> を <paramref name="thumb"/> の矩形アスペクトへ
        /// cover crop した Sprite を返す。スロット寸法が未確定 (rect が 0) か、
        /// crop が全面と同じ (アスペクト一致) なら元の sprite をそのまま返す。
        /// 戻り値は <see cref="V6JacketProvider"/> が所有する (呼び出し側は
        /// Destroy しない)。
        /// </summary>
        public static Sprite CoverSprite(Image thumb, Sprite sprite)
        {
            if (thumb == null || sprite == null || sprite.texture == null)
            {
                return sprite;
            }

            Rect slot = thumb.rectTransform != null ? thumb.rectTransform.rect : new Rect();
            if (slot.width <= 0f || slot.height <= 0f)
            {
                return sprite;
            }

            Texture2D texture = sprite.texture;
            Rect crop = V6BackdropFit.CoverRect(
                texture.width, texture.height, slot.width, slot.height);

            // 全面 crop (アスペクト一致、または 1px 未満の誤差) なら
            // 切り抜き実体を増やさない。
            if (crop.x <= 0.5f && crop.y <= 0.5f
                && crop.width >= texture.width - 1f
                && crop.height >= texture.height - 1f)
            {
                return sprite;
            }

            return V6JacketProvider.Crop(texture, crop, sprite.name);
        }
    }
}
