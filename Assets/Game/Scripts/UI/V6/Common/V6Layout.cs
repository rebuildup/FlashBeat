using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// design 空間 (左上原点・y_down) の座標・寸法を扱う共通ヘルパ (v6 契約 §4)。
    ///
    /// 【座標系の一本化】
    /// v6 の各画面は design 空間 1728×972 (左上原点・y_down) でレイアウトを組む。
    /// Unity の RectTransform は左下原点・y_up なので、design 座標 → RectTransform
    /// 座標の変換をこのクラスの pure 関数 (<see cref="ToLocal"/>) に集約する。
    /// 全 v6 画面・全共通部品は必ずこの変換経由で配置すること。各画面が自前で
    /// anchoredPosition を計算すると符号や原点の取り違えで崩れる (過去の事故)。
    ///
    /// 変換規約: 子の RectTransform は anchor (0,1) / pivot (0,1) (親の左上基準、
    /// 自分の左上を原点にする) とし、anchoredPosition = (x, -y)。
    /// 実画面ピクセル (UISizeProfile) はここでは扱わない。design 空間 1728×972 は
    /// 実ピクセル 1:1 で描き、解像度別の origin ((w-1728)/2, (h-972)/2 の切り捨て、
    /// <see cref="V6Frame.OriginFor"/>) で画面中央に配置する。
    /// 親側で scale を掛けることはしない (等比拡大は行わない)。
    ///
    /// pure 関数 (RectTransform を触らない) は EditMode テスト対象:
    /// Assets/Game/Tests/V6CanvasTests/V6CommonLayoutTests.cs
    /// </summary>
    public static class V6Layout
    {
        /// <summary>
        /// design 座標 (左上原点・y_down) の点を、anchor/pivot (0,1) の
        /// RectTransform の anchoredPosition (左下原点・y_up) へ変換する。
        /// 全 v6 画面の配置はこの 1 関数のみ経由すること。
        /// </summary>
        public static Vector2 ToLocal(Vector2 designTopLeft)
        {
            return new Vector2(designTopLeft.x, -designTopLeft.y);
        }

        /// <summary>右端 designRightX に右寄せしたときの design 上の左上座標。</summary>
        public static Vector2 RightTopLeft(float designRightX, float designTopY, Vector2 size)
        {
            return new Vector2(designRightX - size.x, designTopY);
        }

        /// <summary>中心 x = designCenterX に中央寄せしたときの design 上の左上座標。</summary>
        public static Vector2 CenterTopLeft(float designCenterX, float designTopY, Vector2 size)
        {
            return new Vector2(designCenterX - size.x * 0.5f, designTopY);
        }

        /// <summary>下端 designBottomY に下寄せしたときの design 上の左上座標。</summary>
        public static Vector2 BottomTopLeft(float designLeftX, float designBottomY, Vector2 size)
        {
            return new Vector2(designLeftX, designBottomY - size.y);
        }

        /// <summary>
        /// 横並びのオフセット (コンテナ左端からの x)。widths の合計 + gap×(n-1) が
        /// コンテナ幅に収まるかは呼び出し側 (テスト) が確認する。
        /// </summary>
        public static float[] HorizontalOffsets(float gap, params float[] widths)
        {
            int n = widths == null ? 0 : widths.Length;
            var offsets = new float[n];
            float x = 0f;
            for (int i = 0; i < n; i++)
            {
                offsets[i] = x;
                x += widths[i] + gap;
            }

            return offsets;
        }

        /// <summary>縦積みのオフセット (コンテナ上端からの design y、y_down)。</summary>
        public static float[] VerticalOffsets(float gap, params float[] heights)
        {
            int n = heights == null ? 0 : heights.Length;
            var offsets = new float[n];
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                offsets[i] = y;
                y += heights[i] + gap;
            }

            return offsets;
        }

        /// <summary>
        /// テキスト 1 行の推定幅 (design px)。TMP の preferredWidth は実行レイアウト前に
        /// 確定しないため、幅の事前見積りが必要な場面 (右寄せ要素の確保幅など) では
        /// この pure 関数を使う。字形幅は <see cref="V6Ui.LabelAdvanceEm"/> (上側推定)。
        /// </summary>
        public static float EstimateTextWidth(string text, float fontSize)
        {
            return V6Ui.LabelAdvanceEm(text) * fontSize;
        }

        /// <summary>
        /// 矩形 (topLeft + size, design 空間) が外部矩形 (outerTopLeft + outerSize) に
        /// 収まるか。右カラムのはみ出し防止 (契約: design 幅 1728 内 / Main ゾーン 24..1704)
        /// の検証に使う。
        /// </summary>
        public static bool FitsIn(Vector2 outerTopLeft, Vector2 outerSize, Vector2 topLeft, Vector2 size)
        {
            return topLeft.x >= outerTopLeft.x
                && topLeft.y >= outerTopLeft.y
                && topLeft.x + size.x <= outerTopLeft.x + outerSize.x
                && topLeft.y + size.y <= outerTopLeft.y + outerSize.y;
        }

        // --- 以下は RectTransform を実際に動かすヘルパ ---

        /// <summary>
        /// design 座標 (左上原点) で指定した位置・サイズを RectTransform に適用する。
        /// anchor/pivot を (0,1) に固定し、<see cref="ToLocal"/> で座標変換する。
        /// アンカー計算を各画面に書かせないための唯一の入口。
        /// </summary>
        public static RectTransform Place(RectTransform rt, Vector2 designTopLeft, Vector2 size)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = ToLocal(designTopLeft);
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>
        /// 右寄せ版の <see cref="Place"/>。designRightX は「要素の右端」の design x。
        /// 右カラムが画面外へ出る事故を防ぐため、アンカー (stretch) ではなく
        /// design 座標で明示指定する方式にする。
        /// </summary>
        public static RectTransform PlaceRight(RectTransform rt, float designRightX, float designTopY, Vector2 size)
        {
            return Place(rt, RightTopLeft(designRightX, designTopY, size), size);
        }

        /// <summary>中央寄せ版の <see cref="Place"/>。designCenterX は要素中心の design x。</summary>
        public static RectTransform PlaceCenter(RectTransform rt, float designCenterX, float designTopY, Vector2 size)
        {
            return Place(rt, CenterTopLeft(designCenterX, designTopY, size), size);
        }

        /// <summary>下寄せ版の <see cref="Place"/>。designBottomY は要素下端の design y。</summary>
        public static RectTransform PlaceBottom(RectTransform rt, float designLeftX, float designBottomY, Vector2 size)
        {
            return Place(rt, BottomTopLeft(designLeftX, designBottomY, size), size);
        }

        /// <summary>
        /// 縦積みコンテナを作る (design 座標指定)。子は <see cref="StackVertical"/> で
        /// 上から gap 順に並べる。コンテナ内も design 空間 (コンテナ左上原点)。
        /// </summary>
        public static GameObject Column(GameObject parent, string name, Vector2 designTopLeft, Vector2 size, float gap)
        {
            var go = NewNode(parent, name);
            Place(go.GetComponent<RectTransform>(), designTopLeft, size);
            return go;
        }

        /// <summary>横並びコンテナを作る。子は <see cref="StackHorizontal"/> で並べる。</summary>
        public static GameObject Row(GameObject parent, string name, Vector2 designTopLeft, Vector2 size, float gap)
        {
            var go = NewNode(parent, name);
            Place(go.GetComponent<RectTransform>(), designTopLeft, size);
            return go;
        }

        /// <summary>
        /// 子要素をコンテナ内に上から gap 順に並べる (design y_down)。
        /// 子の幅/高さは各子の sizeDelta を使う。uGUI のレイアウトグループは使わない
        /// (実行レイアウトの順序で結果が変わるのを避ける)。
        /// </summary>
        public static void StackVertical(RectTransform container, float gap, params Component[] children)
        {
            Stack(container, gap, children, true);
        }

        /// <summary>子要素をコンテナ内に左から gap 順に並べる (design x)。</summary>
        public static void StackHorizontal(RectTransform container, float gap, params Component[] children)
        {
            Stack(container, gap, children, false);
        }

        // --- GameObject を返す配置 (各 View に重複していた private At / PlaceTopLeft を集約) ---
        // 引数は design 座標 (左上原点・y_down)。戻り値は配置した GameObject で、
        // V5Ui.Panel / V5GameUi.Text の親にそのまま渡せる。
        // size を省略する版は sizeDelta を書き換えない (既存サイズを維持する)。

        /// <summary>design (x,y) の左上へ、現在の sizeDelta のまま配置する。</summary>
        public static GameObject Place(GameObject go, float x, float y)
        {
            return Place(go.GetComponent<RectTransform>(), x, y);
        }

        /// <summary>design (x,y) の左上・size で配置する。</summary>
        public static GameObject Place(GameObject go, float x, float y, float w, float h)
        {
            return Place(go.GetComponent<RectTransform>(), x, y, w, h);
        }

        /// <summary>
        /// pivot 指定版。design 上の左上 (x,y) を維持したまま pivot を変える
        /// (anchoredPosition は pivot の位置を指すため pivot 分を補正する)。
        /// </summary>
        public static GameObject Place(GameObject go, float x, float y, float w, float h, Vector2 pivot)
        {
            return Place(go.GetComponent<RectTransform>(), x, y, w, h, pivot);
        }

        /// <summary><see cref="Place(GameObject, float, float)"/> の Component 版。</summary>
        public static GameObject Place(Component node, float x, float y)
        {
            return Place(node.GetComponent<RectTransform>(), x, y);
        }

        /// <summary><see cref="Place(GameObject, float, float, float, float)"/> の Component 版。</summary>
        public static GameObject Place(Component node, float x, float y, float w, float h)
        {
            return Place(node.GetComponent<RectTransform>(), x, y, w, h);
        }

        /// <summary><see cref="Place(GameObject, float, float, float, float, Vector2)"/> の Component 版。</summary>
        public static GameObject Place(Component node, float x, float y, float w, float h, Vector2 pivot)
        {
            return Place(node.GetComponent<RectTransform>(), x, y, w, h, pivot);
        }

        /// <summary>RectTransform 版。sizeDelta を維持して design (x,y) の左上へ配置する。</summary>
        public static GameObject Place(RectTransform rt, float x, float y)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = ToLocal(new Vector2(x, y));
            return rt.gameObject;
        }

        /// <summary>RectTransform 版。design (x,y) の左上・size で配置する。</summary>
        public static GameObject Place(RectTransform rt, float x, float y, float w, float h)
        {
            Place(rt, new Vector2(x, y), new Vector2(w, h));
            return rt.gameObject;
        }

        /// <summary>size を Vector2 で取る版 (Row/Column 子の配置など)。</summary>
        public static GameObject Place(RectTransform rt, float x, float y, Vector2 size)
        {
            Place(rt, new Vector2(x, y), size);
            return rt.gameObject;
        }

        /// <summary>RectTransform 版の pivot 指定。design 上の左上 (x,y) を維持する。</summary>
        public static GameObject Place(RectTransform rt, float x, float y, float w, float h, Vector2 pivot)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = pivot;
            rt.sizeDelta = new Vector2(w, h);
            // anchoredPosition は pivot の位置を指す。design 上の左上 (x,y) を
            // 維持するため pivot 分を補正する (各 View の private At と同じ式)。
            rt.anchoredPosition = ToLocal(new Vector2(
                x + pivot.x * w,
                y + (1f - pivot.y) * h));
            return rt.gameObject;
        }

        /// <summary>
        /// design 座標 (左上原点) で配置し、RectTransform を返す。
        /// size を省略すると sizeDelta を触らない (Game/Pause・Game/Loading の
        /// private PlaceAt と同じ仕様)。
        /// </summary>
        public static RectTransform PlaceAt(Component node, float x, float y, Vector2? size = null)
        {
            return PlaceAt(node.gameObject, x, y, size);
        }

        /// <summary><see cref="PlaceAt(Component, float, float, Vector2?)"/> の GameObject 版。</summary>
        public static RectTransform PlaceAt(GameObject node, float x, float y, Vector2? size = null)
        {
            var rt = node.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = ToLocal(new Vector2(x, y));
            if (size.HasValue)
            {
                rt.sizeDelta = size.Value;
            }

            return rt;
        }

        /// <summary>
        /// Row/Column の中央セルなど、親の中央 (anchor/pivot 0.5) に size で置く
        /// (各 View で重複していた private Stretch(rt, w, h))。
        /// </summary>
        public static void StretchCenter(RectTransform rt, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>親いっぱいに広げる (anchor 0..1 / offset 0)。</summary>
        public static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// レイアウトグループの子にする (minWidth 0 = 内容に合わせる)。
        /// (Game/Pause・Game/Loading の private LayoutItem と同じ仕様)。
        /// </summary>
        public static void LayoutItem(GameObject go, float minWidth, float height)
        {
            var le = go.AddComponent<LayoutElement>();
            if (minWidth > 0f)
            {
                le.minWidth = minWidth;
                le.preferredWidth = minWidth;
            }

            le.preferredHeight = height;
        }

        /// <summary>Component に付いている RectTransform を取る (各 View の private GetRect)。</summary>
        public static RectTransform RectOf(Component component)
        {
            return component.GetComponent<RectTransform>();
        }

        /// <summary>GameObject に付いている RectTransform を取る。</summary>
        public static RectTransform RectOf(GameObject go)
        {
            return go.GetComponent<RectTransform>();
        }

        private static void Stack(RectTransform container, float gap, Component[] children, bool vertical)
        {
            if (container == null || children == null)
            {
                return;
            }

            float offset = 0f;
            foreach (var child in children)
            {
                if (child == null)
                {
                    continue;
                }

                var rt = child.transform as RectTransform;
                if (rt == null)
                {
                    continue;
                }

                var size = rt.sizeDelta;
                var topLeft = vertical ? new Vector2(0f, offset) : new Vector2(offset, 0f);
                Place(rt, topLeft, size);
                offset += (vertical ? size.y : size.x) + gap;
            }
        }

        private static GameObject NewNode(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }
    }
}
