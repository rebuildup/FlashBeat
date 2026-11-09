using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.UI.V6
{
    /// <summary>
    /// v6 アイコン スプライトの共通ヘルパ。
    /// Resources/Icons/ 以下の PNG (白ストローク, 透明背景, 96x96) を読み込み、
    /// Image コンポーネントとして配置・着色する。
    /// スプライトが見つからないときは例外を出さず、何も作らない/何も変えない。
    /// </summary>
    public static class V6Icons
    {
        private const string IconsPath = "Icons/";

        /// <summary>
        /// 指定親の下に新しい Image (Icon) を作り、指定アイコンを割り当てる。
        /// 戻り値は作成した Image (失敗時は null)。
        /// </summary>
        public static Image Create(GameObject parent, string iconName, float size, Color color, string objectName = "Icon")
        {
            var sprite = LoadSprite(iconName);
            if (sprite == null)
            {
                return null;
            }

            var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent.transform, false);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = true;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size, size);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            return image;
        }

        /// <summary>
        /// 既存の Image にアイコンを適用する (sprite/color/size 更新)。
        /// 失敗時は何もしない。
        /// </summary>
        public static bool Apply(Image image, string iconName, float size, Color color)
        {
            var sprite = LoadSprite(iconName);
            if (sprite == null)
            {
                return false;
            }

            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;

            var rt = image.rectTransform;
            rt.sizeDelta = new Vector2(size, size);
            return true;
        }

        /// <summary>
        /// Text コンポーネント (アイコン文字) を Image (スプライトアイコン) に置き換える。
        /// 同じ RectTransform サイズ/位置/アンカーを維持し、Text を破棄して Image を追加する。
        /// 戻り値は作成した Image (失敗時は null、元の Text は残る)。
        /// </summary>
        public static Image ReplaceText(GameObject parent, string iconName, float size, Color color)
        {
            var sprite = LoadSprite(iconName);
            if (sprite == null)
            {
                return null;
            }

            var text = parent.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (text == null)
            {
                return null;
            }

            var rt = text.rectTransform;
            var anchoredPos = rt.anchoredPosition;
            var sizeDelta = rt.sizeDelta;
            var anchorMin = rt.anchorMin;
            var anchorMax = rt.anchorMax;
            var pivot = rt.pivot;

            UnityEngine.Object.DestroyImmediate(text);

            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent.transform, false);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = true;

            var newRt = go.GetComponent<RectTransform>();
            newRt.anchoredPosition = anchoredPos;
            newRt.sizeDelta = new Vector2(size, size);
            newRt.anchorMin = anchorMin;
            newRt.anchorMax = anchorMax;
            newRt.pivot = pivot;

            return image;
        }

        private static Sprite LoadSprite(string iconName)
        {
            if (string.IsNullOrEmpty(iconName))
            {
                return null;
            }
            return Resources.Load<Sprite>(IconsPath + iconName);
        }
    }
}