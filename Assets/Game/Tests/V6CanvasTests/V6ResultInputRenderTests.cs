using System.Collections.Generic;
using System.Reflection;
using FlashBeat.UI.V6.Result;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// Issue #676 再検証: Result 登録モーダルの名前入力について、
    /// TMP_InputField が実際に生成するキャレット / 選択ハイライトの
    /// 頂点ジオメトリと、スクロールで動く textComponent の配置を
    /// 「Text Viewport」のワールド矩形と照合する。
    ///
    /// 検証しているもの (実ジオメトリ):
    /// - <see cref="TMP_InputField"/> の private OnFillVBO を実際に呼び、
    ///   生成されたメッシュ頂点 (caret quad / selection highlight quad) を
    ///   Caret GameObject の Transform でワールド座標へ変換したバウンディング
    ///   ボックスが、固定ノード "Name Field/Text Viewport" のワールド矩形に
    ///   収まること。OnFillVBO 内部では GenerateCaret / GenerateHighlight が
    ///   <c>AdjustRectTransformRelativeToViewport</c> を通じて
    ///   textComponent.rectTransform.anchoredPosition を実際にずらす
    ///   (= 本番の canvas LatePreRender rebuild と同じコード経路)。
    /// - 文字の先頭 (textInfo.characterInfo[0].origin) のワールド X が
    ///   viewport 左端からの内側余白に留まり、編集を往復してもドリフト
    ///   しないこと (Issue #676: viewport==text rect だとスクロール量が
    ///   二重計上されて余白からずれる)。
    /// - オーバーフロー時に caret が viewport 内に追従すること
    ///   (キャレット追従スクロールの実動作)。
    ///
    /// EditMode での再現方法 (本番との差分を明示):
    /// - TMP_InputField.UpdateGeometry は <c>Application.isPlaying</c> で
    ///   早期 return し、OnEnable も EditMode では走らないため、Caret
    ///   GameObject (TMP_SelectionCaret + CanvasRenderer) を OnEnable の
    ///   playmode 経路と同じ規則で手動生成し、caretRectTrans /
    ///   m_CachedInputRenderer を reflection で配線する。
    /// - フォーカスは既存テストと同じく m_AllowInput を直接立てる
    ///   (EventSystem / ActivateInputField は EditMode では完走しない)。
    /// - IME 候補ウィンドウ位置合わせ (Input.compositionCursorPos) は
    ///   OS 依存のため、フラグを落としてスキップする。
    ///
    /// 検証していないもの (物理環境が必要):
    /// - 実 IME の変換・確定・候補ウィンドウ位置、実キーボードからの
    ///   入力、GPU 実描画ピクセル。ここで見るのは uGUI/TMP が生成する
    ///   頂点ジオメトリと RectTransform 配置であり、ピクセルそのものではない。
    /// </summary>
    public sealed class V6ResultInputRenderTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly FieldInfo AllowInputField =
            typeof(TMP_InputField).GetField("m_AllowInput", PrivateInstance);
        private static readonly FieldInfo CaretRectTransField =
            typeof(TMP_InputField).GetField("caretRectTrans", PrivateInstance);
        private static readonly FieldInfo CachedRendererField =
            typeof(TMP_InputField).GetField("m_CachedInputRenderer", PrivateInstance);
        private static readonly FieldInfo ImeWindowPosField =
            typeof(TMP_InputField).GetField("m_ShouldUpdateIMEWindowPosition", PrivateInstance);
        private static readonly MethodInfo OnFillVboMethod =
            typeof(TMP_InputField).GetMethod("OnFillVBO", PrivateInstance);
        private static readonly MethodInfo AssignPositioningMethod =
            typeof(TMP_InputField).GetMethod("AssignPositioningIfNeeded", PrivateInstance);

        private GameObject canvasGo;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6ResultInputRenderTests Canvas", typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }

            canvasGo = null;
        }

        private sealed class Fixture
        {
            public TMP_InputField Input;
            public RectTransform Viewport;
            public RectTransform TextRect;
            public GameObject CaretGo;
            public Mesh Mesh;
        }

        /// <summary>canvas 配下に実ビューを建て、playmode の OnEnable が作る
        /// Caret オブジェクトを再現してから名前入力を返す。</summary>
        private Fixture CreateFixture()
        {
            var view = V6ResultView.Create((RectTransform)canvasGo.transform, null, null, null);
            var modalRoot = view.Root.Find("Modal 05b");
            Assert.IsNotNull(modalRoot, "modal root");
            modalRoot.gameObject.SetActive(true);

            var dialog = modalRoot.Find("Dialog");
            Assert.IsNotNull(dialog, "dialog");
            var nameField = dialog.Find("Name Field");
            Assert.IsNotNull(nameField, "Name Field");

            var input = nameField.GetComponent<TMP_InputField>();
            Assert.IsNotNull(input, "Name Field must carry TMP_InputField");
            Assert.IsTrue(input.interactable, "name input must be interactable");

            var vp = dialog.Find("Name Field/Text Viewport");
            Assert.IsNotNull(vp, "Text Viewport");
            var textRect = (RectTransform)input.textComponent.rectTransform;

            input.textComponent.ForceMeshUpdate();
            // EditMode では Update が走らずフォーカス状態 (m_AllowInput) が
            // 立たないため直接立てる (V6ResultInputEditTests と同じ手法)。
            AllowInputField?.SetValue(input, true);

            // TMP_InputField.OnEnable が Application.isPlaying 時にだけ作る
            // Caret オブジェクトを同じ規則で再現する。これがないと
            // GenerateCaret が m_CachedInputRenderer / caretRectTrans を
            // 参照できず、OnFillVBO を EditMode で呼べない。
            var caretGo = new GameObject("Caret", typeof(TMP_SelectionCaret));
            caretGo.hideFlags = HideFlags.DontSave;
            caretGo.transform.SetParent(textRect.parent);
            caretGo.transform.SetAsFirstSibling();
            caretGo.layer = input.gameObject.layer;
            caretGo.AddComponent<LayoutElement>().ignoreLayout = true;

            var caretRect = caretGo.GetComponent<RectTransform>();
            var caretRenderer = caretGo.GetComponent<CanvasRenderer>();
            Assert.IsNotNull(caretRenderer, "caret GO must carry CanvasRenderer");
            CaretRectTransField?.SetValue(input, caretRect);
            CachedRendererField?.SetValue(input, caretRenderer);
            caretRenderer.SetMaterial(Graphic.defaultGraphicMaterial, Texture2D.whiteTexture);
            AssignPositioningMethod?.Invoke(input, null);

            return new Fixture
            {
                Input = input,
                Viewport = (RectTransform)vp,
                TextRect = textRect,
                CaretGo = caretGo,
                Mesh = new Mesh(),
            };
        }

        private static Event Key(KeyCode key, char character = '\0', EventModifiers mods = EventModifiers.None)
        {
            return new Event
            {
                type = EventType.KeyDown,
                keyCode = key,
                character = character,
                modifiers = mods,
            };
        }

        /// <summary>
        /// 本番の canvas LatePreRender rebuild (TMP_InputField.Rebuild →
        /// UpdateGeometry → OnFillVBO) に相当する処理を EditMode で手動実行する。
        /// UpdateLabel で text / キャレット表示状態を更新し、textInfo を
        /// 再生成してから OnFillVBO を呼ぶ。OnFillVBO 内の
        /// AdjustRectTransformRelativeToViewport が textComponent の
        /// anchoredPosition を実際にスクロールさせる。
        /// </summary>
        private static void Render(Fixture f)
        {
            f.Input.ForceLabelUpdate();
            f.Input.textComponent.ForceMeshUpdate();
            // IME 候補ウィンドウ位置合わせは OS 依存なので EditMode では止める。
            ImeWindowPosField?.SetValue(f.Input, false);
            f.Mesh.Clear();
            Assert.IsNotNull(OnFillVboMethod, "TMP_InputField.OnFillVBO must exist");
            OnFillVboMethod.Invoke(f.Input, new object[] { f.Mesh });
        }

        private static void Press(Fixture f, Event e)
        {
            f.Input.ProcessEvent(e);
            Render(f);
        }

        private static void Type(Fixture f, string s)
        {
            foreach (char c in s)
            {
                Press(f, Key(KeyCode.None, c));
            }
        }

        private sealed class WorldGeometry
        {
            public readonly List<Vector3> Caret = new List<Vector3>();
            public readonly List<Vector3> Selection = new List<Vector3>();
            public Rect CaretBounds;
            public Rect SelectionBounds;
            public bool HasCaretBounds;
            public bool HasSelectionBounds;
            /// <summary>textInfo 先頭文字のワールド X (文字がなければ NaN)。</summary>
            public float FirstCharLeftX = float.NaN;
            /// <summary>textComponent のワールド矩形 (スクロール適用後)。</summary>
            public Rect TextBounds;
        }

        /// <summary>
        /// 直前の Render が残したメッシュ頂点を Caret GameObject の Transform で
        /// ワールド座標へ変換し、caret 色 / 選択色で仕分けてバウンディングを取る。
        /// あわせて textInfo 先頭文字と textComponent のワールド矩形も返す。
        /// </summary>
        private static WorldGeometry Measure(Fixture f)
        {
            var g = new WorldGeometry();
            Vector3[] verts = f.Mesh.vertices;
            Color[] colors = f.Mesh.colors;
            Color caretColor = f.Input.caretColor;
            Color selectionColor = f.Input.selectionColor;
            Transform caretTransform = f.CaretGo.transform;

            var caretMin = new Vector2(float.MaxValue, float.MaxValue);
            var caretMax = new Vector2(float.MinValue, float.MinValue);
            var selMin = new Vector2(float.MaxValue, float.MaxValue);
            var selMax = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 world = caretTransform.TransformPoint(verts[i]);
                Color c = i < colors.Length ? colors[i] : Color.white;
                bool isCaret = ColorDistance(c, caretColor) < 0.05f;
                bool isSelection = !isCaret && ColorDistance(c, selectionColor) < 0.05f;
                if (!isCaret && !isSelection)
                {
                    continue;
                }

                Vector2 p = new Vector2(world.x, world.y);
                if (isCaret)
                {
                    g.Caret.Add(world);
                    caretMin = Vector2.Min(caretMin, p);
                    caretMax = Vector2.Max(caretMax, p);
                }
                else
                {
                    g.Selection.Add(world);
                    selMin = Vector2.Min(selMin, p);
                    selMax = Vector2.Max(selMax, p);
                }
            }

            if (g.Caret.Count > 0)
            {
                g.CaretBounds = Rect.MinMaxRect(caretMin.x, caretMin.y, caretMax.x, caretMax.y);
                g.HasCaretBounds = true;
            }

            if (g.Selection.Count > 0)
            {
                g.SelectionBounds = Rect.MinMaxRect(selMin.x, selMin.y, selMax.x, selMax.y);
                g.HasSelectionBounds = true;
            }

            TMP_TextInfo info = f.Input.textComponent.textInfo;
            if (info != null && info.characterCount > 0)
            {
                TMP_CharacterInfo first = info.characterInfo[0];
                // characterInfo の origin/xAdvance は textComponent のローカル
                // 座標系。TransformPoint でワールドへ写すと viewport と同じ
                // 座標系で比較できる。
                Vector3 p = f.TextRect.TransformPoint(new Vector3(first.origin, first.descender, 0f));
                g.FirstCharLeftX = p.x;
            }

            Vector3[] corners = new Vector3[4];
            f.TextRect.GetWorldCorners(corners);
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (Vector3 c in corners)
            {
                minX = Mathf.Min(minX, c.x);
                minY = Mathf.Min(minY, c.y);
                maxX = Mathf.Max(maxX, c.x);
                maxY = Mathf.Max(maxY, c.y);
            }

            g.TextBounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return g;
        }

        private static float ColorDistance(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g)
                + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a);
        }

        private static Rect ViewportWorldRect(Fixture f)
        {
            Vector3[] corners = new Vector3[4];
            f.Viewport.GetWorldCorners(corners);
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (Vector3 c in corners)
            {
                minX = Mathf.Min(minX, c.x);
                minY = Mathf.Min(minY, c.y);
                maxX = Mathf.Max(maxX, c.x);
                maxY = Mathf.Max(maxY, c.y);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private const float Eps = 1.5f;

        /// <summary>
        /// caret quad の「アンカー位置」(LTR では quad の左端 = TMP が
        /// scroll 計算に使う caretPosition) が Text Viewport のワールド矩形
        /// 内にあることを検査する。
        ///
        /// 注意: TMP のスクロール計算 (AdjustRectTransformRelativeToViewport)
        /// は viewport 端から m_CaretWidth (design 2px) ぶんしか退避を確保しないが、
        /// 実際に描かれる caret quad の幅はフォントの lineHeight から算出される
        /// (数 px)。そのため caret quad の右端が viewport 右端を自分の幅ぶん
        /// はみ出すのは TMP 標準の正常動作であり、ここでは quad 全体の包含では
        /// なくアンカー (左端) の包含と quad が viewport と交差すること、
        /// および上下の包含を見る。#676 のドリフトはアンカー自体が viewport を
        /// 出る形で現れるため、この不変条件で検出できる。
        /// </summary>
        private static void AssertCaretInsideViewport(Fixture f, WorldGeometry g, string label)
        {
            Assert.IsTrue(g.HasCaretBounds,
                label + ": caret quad must be generated (focused + non-empty text)");
            Rect vp = ViewportWorldRect(f);
            Rect b = g.CaretBounds;
            Assert.GreaterOrEqual(b.xMin, vp.xMin - Eps,
                label + ": caret anchor " + b.xMin + " escaped viewport left " + vp.xMin);
            Assert.LessOrEqual(b.xMin, vp.xMax + Eps,
                label + ": caret anchor " + b.xMin + " escaped viewport right " + vp.xMax);
            Assert.Greater(b.xMax, vp.xMin,
                label + ": caret quad must intersect viewport (fully clipped left)");
            Assert.GreaterOrEqual(b.yMin, vp.yMin - Eps,
                label + ": caret bottom " + b.yMin + " escaped viewport bottom " + vp.yMin);
            Assert.LessOrEqual(b.yMax, vp.yMax + Eps,
                label + ": caret top " + b.yMax + " escaped viewport top " + vp.yMax);
        }

        /// <summary>選択ハイライトが viewport と交差することを検査する。
        /// 長文の全選択では両端が viewport 外へはみ出すのが正常 (RectMask2D が
        /// クリップする) ため、完全包含ではなく交差を見る。</summary>
        private static void AssertSelectionIntersectsViewport(Fixture f, WorldGeometry g, string label)
        {
            Assert.IsTrue(g.HasSelectionBounds,
                label + ": selection highlight must be generated");
            Rect vp = ViewportWorldRect(f);
            Rect b = g.SelectionBounds;
            Assert.IsTrue(b.xMax > vp.xMin && b.xMin < vp.xMax && b.yMax > vp.yMin && b.yMin < vp.yMax,
                label + ": selection highlight must intersect viewport");
        }

        /// <summary>短文 (viewport に収まる文字列) の選択ハイライトは
        /// viewport に完全に収まるべき。</summary>
        private static void AssertSelectionInsideViewport(Fixture f, WorldGeometry g, string label)
        {
            Assert.IsTrue(g.HasSelectionBounds,
                label + ": selection highlight must be generated");
            Rect vp = ViewportWorldRect(f);
            Rect b = g.SelectionBounds;
            Assert.GreaterOrEqual(b.xMin, vp.xMin - Eps,
                label + ": selection left " + b.xMin + " escaped viewport left " + vp.xMin);
            Assert.LessOrEqual(b.xMax, vp.xMax + Eps,
                label + ": selection right " + b.xMax + " escaped viewport right " + vp.xMax);
        }

        [Test]
        public void Typing_KeepsCaretInsideViewport_AndPaddingStable()
        {
            Fixture f = CreateFixture();
            Rect vp = ViewportWorldRect(f);

            // 左余白の基準値: caret を先頭に置いたときの caret 左端 −
            // viewport 左端。Issue #676 のドリフトはこの値が編集のたびに
            // ずれる形で現れる。
            float baselinePadding = float.NaN;

            Type(f, "ab");
            WorldGeometry g = Measure(f);
            AssertCaretInsideViewport(f, g, "type 'ab'");

            // caret を先頭へ戻すと文字の先頭 = viewport 左余白 + padding。
            Press(f, Key(KeyCode.LeftArrow));
            Press(f, Key(KeyCode.LeftArrow));
            g = Measure(f);
            Assert.AreEqual(0, f.Input.caretPosition);
            AssertCaretInsideViewport(f, g, "caret at start");
            Assert.IsFalse(float.IsNaN(g.FirstCharLeftX), "first char must exist");
            baselinePadding = g.FirstCharLeftX - vp.xMin;
            Assert.Greater(baselinePadding, -Eps,
                "first char must not start left of viewport: " + baselinePadding);
            Assert.Less(baselinePadding, vp.width * 0.25f,
                "first char must start near viewport left edge, not drifted: " + baselinePadding);

            // 編集を往復しても余白がドリフトしないこと。
            Type(f, "cd");
            Press(f, Key(KeyCode.Backspace));
            for (int i = 0; i < 3; i++) { Press(f, Key(KeyCode.LeftArrow)); }
            Assert.AreEqual(0, f.Input.caretPosition);
            g = Measure(f);
            AssertCaretInsideViewport(f, g, "after round-trip caret at start");
            float padding = g.FirstCharLeftX - vp.xMin;
            Assert.AreEqual(baselinePadding, padding, 2f,
                "left padding drifted: baseline=" + baselinePadding + " now=" + padding);
        }

        [Test]
        public void EditOperations_CaretStaysInsideViewport()
        {
            Fixture f = CreateFixture();
            Type(f, "hello");
            WorldGeometry g = Measure(f);
            AssertCaretInsideViewport(f, g, "type 'hello'");

            Press(f, Key(KeyCode.Backspace));
            g = Measure(f);
            AssertCaretInsideViewport(f, g, "backspace");

            Press(f, Key(KeyCode.LeftArrow));
            Press(f, Key(KeyCode.Delete));
            g = Measure(f);
            AssertCaretInsideViewport(f, g, "left+delete");

            Press(f, Key(KeyCode.RightArrow));
            Press(f, Key(KeyCode.RightArrow));
            Press(f, Key(KeyCode.RightArrow));
            g = Measure(f);
            AssertCaretInsideViewport(f, g, "arrows");

            // Shift+矢印の選択 → Backspace で選択削除。
            Press(f, Key(KeyCode.LeftArrow, '\0', EventModifiers.Shift));
            Press(f, Key(KeyCode.LeftArrow, '\0', EventModifiers.Shift));
            g = Measure(f);
            AssertSelectionIntersectsViewport(f, g, "shift selection");
            Press(f, Key(KeyCode.Backspace));
            g = Measure(f);
            AssertCaretInsideViewport(f, g, "backspace after selection");
        }

        [Test]
        public void SelectAll_HighlightInsideViewport_AndReplaceKeepsCaretInside()
        {
            Fixture f = CreateFixture();
            Type(f, "abc");

            Press(f, Key(KeyCode.A, '\0', EventModifiers.Command));
            WorldGeometry g = Measure(f);
            // 短文なので選択範囲全体が viewport に収まるはず。
            AssertSelectionInsideViewport(f, g, "select all");

            Press(f, Key(KeyCode.None, 'x'));
            g = Measure(f);
            Assert.AreEqual("x", f.Input.text);
            AssertCaretInsideViewport(f, g, "replace selection");
        }

        [Test]
        public void OverflowText_CaretFollowScroll_KeepsCaretInsideViewport()
        {
            Fixture f = CreateFixture();
            int savedLimit = f.Input.characterLimit;
            f.Input.characterLimit = 0; // 720px を超える長さまで入力できるよう上限を外す
            try
            {
                // 1 文字ごとに実ジオメトリを検査する。キャレット追従スクロールが
                // 効いていなければ caret は viewport 右へ抜ける。
                for (int i = 0; i < 80; i++)
                {
                    Press(f, Key(KeyCode.None, 'a'));
                    WorldGeometry g = Measure(f);
                    AssertCaretInsideViewport(f, g, "overflow char " + (i + 1));
                }

                WorldGeometry end = Measure(f);
                // スクロールが実際に掛かったこと (textComponent が左へずれた)。
                Assert.Less(f.TextRect.anchoredPosition.x, -1f,
                    "textComponent must be scrolled left for overflow text");
                // 先頭側の文字は viewport 左へクリップされるのが正常。
                Assert.Less(end.FirstCharLeftX, ViewportWorldRect(f).xMin,
                    "leading chars should be clipped left after scroll");
                // 末尾 caret は viewport 内に追従している。
                AssertCaretInsideViewport(f, end, "caret at end of overflow");
            }
            finally
            {
                f.Input.characterLimit = savedLimit;
            }
        }

        [Test]
        public void ClearAndRetype_ScrollResets_AndCaretStaysInside()
        {
            Fixture f = CreateFixture();
            int savedLimit = f.Input.characterLimit;
            f.Input.characterLimit = 0;
            try
            {
                Type(f, new string('a', 60));
                WorldGeometry g = Measure(f);
                Assert.Less(f.TextRect.anchoredPosition.x, -1f, "precondition: scrolled");
                AssertCaretInsideViewport(f, g, "overflow typed");

                // 全選択 → Backspace で空にする。
                Press(f, Key(KeyCode.A, '\0', EventModifiers.Command));
                Press(f, Key(KeyCode.Backspace));
                g = Measure(f);
                Assert.AreEqual(string.Empty, f.Input.text);
                // 空になったらスクロールは原点へ戻る (m_HasTextBeenRemoved 経路)。
                Assert.LessOrEqual(Mathf.Abs(f.TextRect.anchoredPosition.x), 1f,
                    "scroll offset must reset after clearing, got " + f.TextRect.anchoredPosition.x);

                // 入力し直しても caret は viewport 内、余白も初期位置。
                Type(f, "hi");
                g = Measure(f);
                AssertCaretInsideViewport(f, g, "retype after clear");
                Assert.IsFalse(float.IsNaN(g.FirstCharLeftX));
                Rect vp = ViewportWorldRect(f);
                float padding = g.FirstCharLeftX - vp.xMin;
                Assert.Greater(padding, -Eps);
                Assert.Less(padding, vp.width * 0.25f,
                    "padding after retype drifted: " + padding);
            }
            finally
            {
                f.Input.characterLimit = savedLimit;
            }
        }

        /// <summary>
        /// 感度確認: Issue #676 以前の構造 (Value/Placeholder が Name Field 直下、
        /// textViewport に text rect 自身 = Issue #583 の指定) を階層ごと再現すると、
        /// TMP のスクロール基準枠が文字と一緒に動くため caret 追従が効かず、
        /// 同じ編集列で caret アンカーが固定 Text Viewport の外へ出ることを確認する。
        /// 上の各テストが「実装定数」ではなく不変条件 (描画 caret/選択が
        /// viewport 内) を見ている証拠。
        /// </summary>
        [Test]
        public void LegacyViewportEqualsTextRect_CaretDriftsOutOfViewport()
        {
            Fixture f = CreateFixture();
            int savedLimit = f.Input.characterLimit;
            f.Input.characterLimit = 0;
            try
            {
                // pre-#676 の階層を再現する。単に textViewport 参照を替えるだけでは
                // AdjustRectTransformRelativeToViewport の localPosition 加算が
                // 相殺されてスクロールが効いてしまうため、Value/Placeholder を
                // Name Field 直下 (viewport と同じ 32,20 720x58) へ移し、
                // textViewport に text rect 自身を渡す。当時は RectMask2D も無く、
                // Value は固定 viewport の子ではないため実質マスク無しと同等。
                RectTransform fieldRt = (RectTransform)f.Input.transform;
                Transform valueParent = f.Input.textComponent.transform.parent;
                var valueGo = f.Input.textComponent.gameObject;
                valueGo.transform.SetParent(fieldRt, false);
                var placeholder = valueParent.Find("Placeholder");
                if (placeholder != null)
                {
                    placeholder.SetParent(fieldRt, false);
                }

                // field ローカルで固定 viewport と同じ矩形に置く
                // (pre-#676 の V6Layout.Place(32,20,720,58) と同じ結果)。
                CopyRectTransform(f.Viewport, (RectTransform)valueGo.transform);
                if (placeholder != null)
                {
                    CopyRectTransform(f.Viewport, (RectTransform)placeholder);
                }

                // Caret GO も textComponent と同じ親へ (OnEnable は
                // m_TextComponent.transform.parent の下に作る)。
                f.CaretGo.transform.SetParent(fieldRt, false);
                AssignPositioningMethod?.Invoke(f.Input, null);

                f.Input.textViewport = f.TextRect;

                bool escaped = false;
                string escapeLabel = null;
                Rect vp = ViewportWorldRect(f);

                // ドリフトはスクロール量のずれとして現れる: 右へ溢れる文字を
                // 入れたあと caret を先頭まで戻すと、pre-#676 では文字の先頭が
                // viewport 左端の内側余白 (32px) を越えて左へずれる。
                // 右送り・左戻しの両フェーズで caret アンカーの viewport 脱出を見る。
                for (int step = 0; step < 140 && !escaped; step++)
                {
                    bool typing = step < 70;
                    Press(f, typing ? Key(KeyCode.None, 'a') : Key(KeyCode.LeftArrow));
                    WorldGeometry g = Measure(f);
                    if (!g.HasCaretBounds)
                    {
                        continue;
                    }

                    Rect b = g.CaretBounds;
                    // caret quad 右端の自分の幅ぶんのはみ出しは正常動作なので、
                    // ドリフト判定はアンカー (左端) の viewport 脱出で行う。
                    if (b.xMin < vp.xMin - Eps || b.xMin > vp.xMax + Eps
                        || b.yMin < vp.yMin - Eps || b.yMax > vp.yMax + Eps)
                    {
                        escaped = true;
                        escapeLabel = (typing ? "type " : "left ") + step
                            + " caretPos=" + f.Input.caretPosition
                            + " caret anchor x=" + b.xMin
                            + " y=[" + b.yMin + " - " + b.yMax + "]"
                            + " viewport x=[" + vp.xMin + " - " + vp.xMax + "]"
                            + " y=[" + vp.yMin + " - " + vp.yMax + "]";
                    }
                }

                Assert.IsTrue(escaped,
                    "pre-#676 構造 (viewport==text rect) では caret アンカーが"
                    + " viewport 外へドリフトするはず。実ジオメトリで確認できなかった"
                    + "場合はハーネスの感度不足を疑うこと。");
                TestContext.Out.WriteLine("drift observed: " + escapeLabel);
            }
            finally
            {
                f.Input.characterLimit = savedLimit;
            }
        }

        /// <summary>RectTransform の配置プロパティをそっくり写す
        /// (V6Layout.Place と同じ矩形を別ノードへ再現するため)。</summary>
        private static void CopyRectTransform(RectTransform src, RectTransform dst)
        {
            dst.pivot = src.pivot;
            dst.anchorMin = src.anchorMin;
            dst.anchorMax = src.anchorMax;
            dst.anchoredPosition3D = src.anchoredPosition3D;
            dst.sizeDelta = src.sizeDelta;
            dst.localRotation = src.localRotation;
            dst.localScale = src.localScale;
        }
    }
}
