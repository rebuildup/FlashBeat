using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// 「その Graphic が実際に画面に描かれているか」を判定する (Issue #233)。
    ///
    /// <see cref="UnityEngine.UI.Graphic.isActiveAndEnabled"/> は
    /// 「オブジェクトが有効」でしかなく、祖先 <see cref="CanvasGroup"/> の
    /// alpha が 0 のとき (読込オーバーレイの待機中など) は **画面には何も出ていないのに**
    /// true を返す。旧 UI の残存検査を「本当に見えているか」で行うために使う。
    ///
    /// 検査の-strength として、この判定は「祖先チェーンに alpha 0 の
    /// CanvasGroup が 1 つでもあれば描かれていない」とする。
    /// </summary>
    public static class GraphicVisibility
    {
        /// <summary>これ以下なら「描かれていない」とみなす alpha。</summary>
        public const float InvisibleAlpha = 0.001f;

        /// <summary>
        /// その Graphic が今フレームに描画されるか。
        /// オブジェクトが非 active / 無効、または祖先 (自身を含む) の
        /// CanvasGroup が透明なら false。
        /// </summary>
        public static bool IsDrawn(Graphic graphic)
        {
            if (graphic == null) return false;
            if (!graphic.isActiveAndEnabled) return false;

            Transform t = graphic.transform;
            while (t != null)
            {
                var group = t.GetComponent<CanvasGroup>();
                if (group != null && group.alpha <= InvisibleAlpha) return false;
                t = t.parent;
            }
            return true;
        }

        /// <summary>
        /// その Graphic が、指定した名前のオブジェクト (**自身を含む**) の
        /// 配下にあるか。実行時生成のオーバーレイ (DontDestroyOnLoad) を
        /// 名前で追うために使う。
        /// </summary>
        public static bool IsUnderNamedObject(Graphic graphic, string objectName)
        {
            if (graphic == null || string.IsNullOrEmpty(objectName)) return false;
            Transform t = graphic.transform;
            while (t != null)
            {
                if (t.name == objectName) return true;
                t = t.parent;
            }
            return false;
        }
    }
}
