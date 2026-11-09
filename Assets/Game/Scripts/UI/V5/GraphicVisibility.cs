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
    /// 検査の前提として、祖先チェーンの alpha は掛け算で合成し、
    /// 祖先の Canvas が無効なら描かれていないとみなす
    /// (PR #263 レビュー S4)。
    /// </summary>
    public static class GraphicVisibility
    {
        /// <summary>これ以下なら「描かれていない」とみなす alpha。</summary>
        public const float InvisibleAlpha = 0.001f;

        /// <summary>
        /// その Graphic が今フレームに描画されるか。
        ///
        /// 判定材料 (PR #263 レビュー S4):
        ///
        ///   1. 自身が無効 (非 active / Graphic 無効) なら描かれない。
        ///   2. 祖先チェーンの <see cref="CanvasGroup"/> の alpha は
        ///      **掛け算**で合成する (親の alpha 0.5 と子の alpha 0.5 は
        ///      0.25。片方だけ 0 をチェックすると「0.5 x 0.5 = 0.25」の
        ///      ケースを取りこぼす)。
        ///   3. 祖先チェーンの <see cref="Canvas"/> が無効なら描かれない
        ///      (Canvas を切ると下の UI は全部消える)。
        ///   4. Graphic 自身の色 (color.a) が透明なら描かれない。
        /// </summary>
        public static bool IsDrawn(Graphic graphic)
        {
            if (graphic == null) return false;
            if (!graphic.isActiveAndEnabled) return false;

            // 自身の色が透明なら描かない (uGUI は描画はするが「見えていない」)。
            if (graphic.color.a <= InvisibleAlpha) return false;

            float alpha = graphic.color.a;
            Transform t = graphic.transform;
            while (t != null)
            {
                var group = t.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    alpha *= group.alpha;
                    if (alpha <= InvisibleAlpha) return false;
                }

                var canvas = t.GetComponent<Canvas>();
                if (canvas != null && !canvas.isActiveAndEnabled) return false;

                t = t.parent;
            }
            return alpha > InvisibleAlpha;
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
