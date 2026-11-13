using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.UI.V6
{
    /// <summary>
    /// 変化していない UI へ同じ値を書き戻さないための小さな判定 (Issue #402)。
    ///
    /// なぜ要るか:
    ///   uGUI の <c>Graphic.color</c> / <c>RectTransform.sizeDelta</c> /
    ///   <c>anchoredPosition</c> に **同じ値** を代入しても、Unity 側は
    ///   「値が変わった」と判定してレイアウト/バッチを dirty にすることがある。
    ///   実際に Canvas rebuild が起きるかは値と経路によるため、ここでは
    ///   **同値代入をしない** という安全側の規則だけを提供する。
    ///
    /// 何を守るか:
    ///   - 時間依存の表示 (歌詞・進捗・スコア・判定) は値が変わるので、そのまま更新される。
    ///   - 固定配置 (レーン格子・パネル) は解像度や値が変わったときだけ書く。
    ///
    /// 計測:
    ///   <see cref="SuppressedWrites"/> が「書かなかった回数」。EditMode テストで
    ///   「同じ状態で 2 回更新しても 2 回目は書かない」ことを固定する。
    /// </summary>
    public static class V6LayoutWrites
    {
        /// <summary>座標の比較しきい値 (px)。サブピクセルの揺れで dirty にしない。</summary>
        public const float PositionEpsilon = 0.01f;

        /// <summary>幅・高さの比較しきい値 (px)。</summary>
        public const float SizeEpsilon = 0.01f;

        /// <summary>アルファの比較しきい値 (0..1)。</summary>
        public const float AlphaEpsilon = 0.001f;

        /// <summary>同値代入を避けた回数 (計測用)。</summary>
        public static int SuppressedWrites { get; private set; }

        /// <summary>実際に代入した回数 (計測用)。</summary>
        public static int AppliedWrites { get; private set; }

        /// <summary>sizeDelta が変わるときだけ書く。書いたら true。</summary>
        public static bool SizeDeltaIfChanged(RectTransform rt, float width, float height)
        {
            if (rt == null)
            {
                return false;
            }

            Vector2 current = rt.sizeDelta;
            if (NearlyEqual(current.x, width, SizeEpsilon) && NearlyEqual(current.y, height, SizeEpsilon))
            {
                SuppressedWrites++;
                return false;
            }

            rt.sizeDelta = new Vector2(width, height);
            AppliedWrites++;
            return true;
        }

        /// <summary>anchoredPosition が変わるときだけ書く。書いたら true。</summary>
        public static bool AnchoredPositionIfChanged(RectTransform rt, float x, float y)
        {
            if (rt == null)
            {
                return false;
            }

            Vector2 current = rt.anchoredPosition;
            if (NearlyEqual(current.x, x, PositionEpsilon) && NearlyEqual(current.y, y, PositionEpsilon))
            {
                SuppressedWrites++;
                return false;
            }

            rt.anchoredPosition = new Vector2(x, y);
            AppliedWrites++;
            return true;
        }

        /// <summary>
        /// 色 (とくにアルファ) が変わるときだけ書く。RGB はそのまま維持し、
        /// アルファだけを差し替える。書いたら true。
        /// </summary>
        public static bool AlphaIfChanged(Graphic graphic, float alpha)
        {
            if (graphic == null)
            {
                return false;
            }

            Color current = graphic.color;
            if (NearlyEqual(current.a, alpha, AlphaEpsilon))
            {
                SuppressedWrites++;
                return false;
            }

            current.a = alpha;
            graphic.color = current;
            AppliedWrites++;
            return true;
        }

        /// <summary>Active 状態が変わるときだけ SetActive する。変えたら true。</summary>
        public static bool ActiveIfChanged(GameObject go, bool active)
        {
            if (go == null)
            {
                return false;
            }

            if (go.activeSelf == active)
            {
                SuppressedWrites++;
                return false;
            }

            go.SetActive(active);
            AppliedWrites++;
            return true;
        }

        /// <summary>テスト用: 計測点を戻す。</summary>
        public static void ResetCountersForAudit()
        {
            SuppressedWrites = 0;
            AppliedWrites = 0;
        }

        /// <summary>浮動小数の比較 (絶対差)。</summary>
        public static bool NearlyEqual(float a, float b, float epsilon)
        {
            return Mathf.Abs(a - b) <= epsilon;
        }
    }
}
