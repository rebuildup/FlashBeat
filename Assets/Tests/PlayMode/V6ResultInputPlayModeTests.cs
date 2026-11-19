using System;
using System.Collections;
using System.Reflection;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Result;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FlashBeat.PlayModeTests
{
    /// <summary>
    /// Issue #676 / #706: Result 登録モーダルの名前入力を PlayMode の実イベント
    /// 経路で検証する。EditMode 側 (V6ResultInputEditTests /
    /// V6ResultInputRenderTests) が TMP_InputField.ProcessEvent と OnFillVBO の
    /// reflection 呼び出しで済ませていた部分を、実際の uGUI 入力スタックで再現する。
    ///
    /// 実イベント経路の再現範囲 (Unity が許す範囲で本番と同一のコードを通す):
    /// - フォーカス: <see cref="V6CursorInput.FocusSelectAll"/> — 本番の
    ///   V6ResultModal.FocusNameField が呼ぶのと同じ公開 API。
    ///   EventSystem.SetSelectedGameObject + ActivateInputField + 全選択を
    ///   実フレームの OnEnable / Update を通して行う (m_AllowInput の
    ///   reflection 直書きはしない)。
    /// - 入力イベント: IMGUI の <see cref="Event"/> を内部 API
    ///   Event.QueueEvent (reflection 経由、内部実装には触れない) で
    ///   ネイティブイベントキューへ積み、<b>本番の</b>
    ///   StandaloneInputModule.Process() を同期的に呼ぶ。
    ///   Process → SendUpdateEventToSelectedObject →
    ///   ExecuteEvents.updateSelectedHandler → TMP_InputField.OnUpdateSelected
    ///   → Event.PopEvent → KeyPressed という実経路をそのまま通る。
    ///   (yield だけでは engine がフレーム境界でイベントキューを破棄するため
    ///   届かない — probe で確認済み。Process() の呼び出し主体が
    ///   EventSystem.Update かテストかの違いだけで、処理内容は同一。)
    /// - 描画: 実 canvas の PreRender rebuild (TMP_InputField.UpdateGeometry →
    ///   OnFillVBO → m_CachedInputRenderer.SetMesh) が生成したキャレット /
    ///   選択メッシュを CanvasRenderer.GetMesh() で読み、EditMode と同じ
    ///   viewport 包含・32px 左余白の不変条件を検査する。
    ///
    /// 再現していないもの (物理環境が必要):
    /// - 物理キーボードからの実入力 (OS → engine のイベント生成部)。
    ///   ここで積む Event はエンジンが生成するのと同じ型・同じキューを通るが、
    ///   生成元はテスト。
    /// - IME 変換・確定。TMP は Input.compositionString (読み取り専用、
    ///   OS の IME が供給) を読むため合成できない。変換中文字列が確定文字列を
    ///   壊さないことは EditMode 側の CompositionString テストが固定済み。
    /// - GPU 実描画ピクセル (頂点ジオメトリと RectTransform 配置まで)。
    /// - Enter / Esc の画面側意味づけ: Enter は TMP の Finish →
    ///   DeactivateInputField まで検証するが、onEndEdit → TrySubmit は
    ///   modalOpen (private) が必要なため対象外。Esc のモーダルクローズは
    ///   V6ResultView.Tick の Input.GetKeyDown 依存でレガシー Input を
    ///   合成できないため対象外。
    /// </summary>
    public sealed class V6ResultInputPlayModeTests
    {
        private const float Eps = 1.5f;

        /// <summary>Event.QueueEvent は internal static extern。reflection でのみ呼べる。</summary>
        private static readonly MethodInfo QueueEventMethod =
            typeof(Event).GetMethod("QueueEvent", BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>TMP_KeyPressed と同じ Apple 判定 (macOS では Command が Ctrl 相当)。</summary>
        private static readonly bool IsApplePlatform =
            SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX
            || SystemInfo.operatingSystem.Contains("iOS")
            || SystemInfo.operatingSystem.Contains("tvOS");

        private static EventModifiers CtrlMod
        {
            get { return IsApplePlatform ? EventModifiers.Command : EventModifiers.Control; }
        }

        private GameObject canvasGo;
        private GameObject eventSystemGo;
        private V6ResultView view;
        private TMP_InputField input;
        private RectTransform viewport;
        private RectTransform textRect;
        private Transform caretTransform;
        private CanvasRenderer caretRenderer;

        [TearDown]
        public void TearDown()
        {
            if (view != null)
            {
                view.Dispose();
                view = null;
            }

            if (canvasGo != null)
            {
                UnityEngine.Object.Destroy(canvasGo);
                canvasGo = null;
            }

            if (eventSystemGo != null)
            {
                UnityEngine.Object.Destroy(eventSystemGo);
                eventSystemGo = null;
            }

            input = null;
            viewport = null;
            textRect = null;
            caretTransform = null;
            caretRenderer = null;
        }

        /// <summary>
        /// canvas + EventSystem + 実 V6ResultView を建て、登録モーダルを
        /// 本番と同じ順序で開いて名前入力へフォーカスする。
        /// OpenModal 本体は private で Tick (Input.GetKeyDown) 経由のため
        /// 合成できないので、OpenModal 末尾と同じ
        /// 「modal.SetActive(true) → characterLimit → 初期文字列 →
        /// V6CursorInput.FocusSelectAll」を再現する。
        /// </summary>
        private IEnumerator CreateFixture(string initialText = null)
        {
            Assert.IsNotNull(QueueEventMethod, "Event.QueueEvent must exist (internal API changed?)");

            canvasGo = new GameObject("V6ResultInputPM Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            eventSystemGo = new GameObject("V6ResultInputPM EventSystem",
                typeof(EventSystem), typeof(StandaloneInputModule));
            Assert.IsNotNull(EventSystem.current, "EventSystem.current must be assigned");

            view = V6ResultView.Create((RectTransform)canvasGo.transform, null, null, null);

            var modalRoot = view.Root.Find("Modal 05b");
            Assert.IsNotNull(modalRoot, "modal root");
            modalRoot.gameObject.SetActive(true);

            var dialog = modalRoot.Find("Dialog");
            Assert.IsNotNull(dialog, "dialog");
            var nameField = dialog.Find("Name Field");
            Assert.IsNotNull(nameField, "Name Field");

            input = nameField.GetComponent<TMP_InputField>();
            Assert.IsNotNull(input, "Name Field must carry TMP_InputField");
            Assert.IsTrue(input.interactable, "name input must be interactable");

            var vp = dialog.Find("Name Field/Text Viewport");
            Assert.IsNotNull(vp, "Text Viewport");
            viewport = (RectTransform)vp;
            Assert.IsNotNull(viewport.GetComponent<RectMask2D>(), "Text Viewport must clip");
            textRect = (RectTransform)input.textComponent.rectTransform;

            // OpenModal と同じ入力状態: Local モードの上限 (8文字) と初期文字列。
            input.characterLimit = V6ResultLogic.LocalNameMaxLength;
            // キャレット点滅を止めて常時表示にする。点滅は時間依存の
            // コスメティックな切替 (design 530ms) で、位置・サイズの
            // ジオメトリには影響しない。実フレームの CaretBlink が
            // 「消灯」フェーズを引くと caret quad がメッシュから消えて
            // 検査が非決定的になるため、ここだけ固定する。
            input.caretBlinkRate = 0f;
            if (initialText != null)
            {
                input.SetTextWithoutNotify(initialText);
            }

            // 本番フォーカス経路 (V6ResultModal.FocusNameField → FocusSelectAll)。
            V6CursorInput.FocusSelectAll(input);

            // OnEnable / ActivateInputField / 初回 canvas render を実フレームで進める。
            yield return null;
            yield return null;
            yield return null;

            Assert.IsTrue(input.isFocused, "input must be focused via production path");
            Assert.AreEqual(input.gameObject, EventSystem.current.currentSelectedGameObject,
                "EventSystem selection must point at the name input");

            // キャレット GO は TMP_InputField.OnEnable が playmode で作る実物。
            var caretGo = textRect.parent.Find("Caret");
            Assert.IsNotNull(caretGo, "TMP caret GameObject must exist after real OnEnable");
            caretTransform = caretGo;
            caretRenderer = caretGo.GetComponent<CanvasRenderer>();
            Assert.IsNotNull(caretRenderer, "caret must carry CanvasRenderer");
        }

        /// <summary>
        /// 実 IMGUI イベントをキューへ積み、本番の StandaloneInputModule.Process()
        /// で消費させる。Process は EventSystem.Update が毎フレーム呼ぶのと
        /// 同一メソッドで、SendUpdateEventToSelectedObject → OnUpdateSelected
        /// → Event.PopEvent → KeyPressed の実経路をそのまま通る。
        /// その後 2 フレーム待ち、TMP Update と canvas PreRender (実メッシュ生成)
        /// を進める。
        /// </summary>
        private IEnumerator SendKey(KeyCode key, char character = '\0', EventModifiers mods = EventModifiers.None)
        {
            var e = new Event
            {
                type = EventType.KeyDown,
                keyCode = key,
                character = character,
                modifiers = mods,
            };
            QueueEventMethod.Invoke(null, new object[] { e });
            Assert.AreEqual(1, Event.GetEventCount(), "queued IMGUI event must be pending");

            var module = EventSystem.current.currentInputModule;
            Assert.IsNotNull(module, "StandaloneInputModule must be the current input module");
            module.Process();
            Assert.AreEqual(0, Event.GetEventCount(), "input module must consume the queued event");

            // 実フレームを進めて TMP の Update / canvas rebuild を走らせる。
            yield return null;
            yield return null;
        }

        private IEnumerator Type(string s)
        {
            foreach (char c in s)
            {
                yield return SendKey(KeyCode.None, c);
            }
        }

        // --- 実メッシュ計測 (canvas PreRender が生成した頂点をそのまま読む) ---

        private sealed class WorldGeometry
        {
            public Rect CaretBounds;
            public Rect SelectionBounds;
            public bool HasCaretBounds;
            public bool HasSelectionBounds;
            /// <summary>textInfo 先頭文字のワールド X (文字がなければ NaN)。</summary>
            public float FirstCharLeftX = float.NaN;
            /// <summary>textComponent のワールド矩形 (スクロール適用後)。</summary>
            public Rect TextBounds;
        }

        private WorldGeometry Measure()
        {
            var g = new WorldGeometry();
            var mesh = caretRenderer.GetMesh();
            Assert.IsNotNull(mesh, "caret CanvasRenderer must hold a mesh after real render");

            Vector3[] verts = mesh.vertices;
            Color[] colors = mesh.colors;
            Color caretColor = input.caretColor;
            Color selectionColor = input.selectionColor;
            Transform caretTr = caretTransform;

            var caretMin = new Vector2(float.MaxValue, float.MaxValue);
            var caretMax = new Vector2(float.MinValue, float.MinValue);
            var selMin = new Vector2(float.MaxValue, float.MaxValue);
            var selMax = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 world = caretTr.TransformPoint(verts[i]);
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
                    caretMin = Vector2.Min(caretMin, p);
                    caretMax = Vector2.Max(caretMax, p);
                }
                else
                {
                    selMin = Vector2.Min(selMin, p);
                    selMax = Vector2.Max(selMax, p);
                }
            }

            if (caretMax.x > caretMin.x || caretMax.y > caretMin.y)
            {
                g.CaretBounds = Rect.MinMaxRect(caretMin.x, caretMin.y, caretMax.x, caretMax.y);
                g.HasCaretBounds = true;
            }

            if (selMax.x > selMin.x || selMax.y > selMin.y)
            {
                g.SelectionBounds = Rect.MinMaxRect(selMin.x, selMin.y, selMax.x, selMax.y);
                g.HasSelectionBounds = true;
            }

            TMP_TextInfo info = input.textComponent.textInfo;
            if (info != null && info.characterCount > 0)
            {
                TMP_CharacterInfo first = info.characterInfo[0];
                Vector3 p = textRect.TransformPoint(new Vector3(first.origin, first.descender, 0f));
                g.FirstCharLeftX = p.x;
            }

            Vector3[] corners = new Vector3[4];
            textRect.GetWorldCorners(corners);
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

        private Rect CurrentViewportWorldRect()
        {
            Vector3[] corners = new Vector3[4];
            viewport.GetWorldCorners(corners);
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

        /// <summary>EditMode 側と同じ不変条件: caret quad のアンカー (左端) が
        /// Text Viewport 内にあり、上下も viewport に収まる。</summary>
        private void AssertCaretInsideViewport(WorldGeometry g, string label)
        {
            Assert.IsTrue(g.HasCaretBounds,
                label + ": caret quad must be generated (focused + real render)");
            Rect vp = CurrentViewportWorldRect();
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

        /// <summary>短文 (viewport に収まる文字列) の選択ハイライトは
        /// viewport に完全に収まるべき。</summary>
        private void AssertSelectionInsideViewport(WorldGeometry g, string label)
        {
            Assert.IsTrue(g.HasSelectionBounds,
                label + ": selection highlight must be generated");
            Rect vp = CurrentViewportWorldRect();
            Rect b = g.SelectionBounds;
            Assert.GreaterOrEqual(b.xMin, vp.xMin - Eps,
                label + ": selection left " + b.xMin + " escaped viewport left " + vp.xMin);
            Assert.LessOrEqual(b.xMax, vp.xMax + Eps,
                label + ": selection right " + b.xMax + " escaped viewport right " + vp.xMax);
        }

        /// <summary>長文の選択は両端が viewport 外へはみ出すのが正常
        /// (RectMask2D がクリップする) ため交差だけを見る。</summary>
        private void AssertSelectionIntersectsViewport(WorldGeometry g, string label)
        {
            Assert.IsTrue(g.HasSelectionBounds,
                label + ": selection highlight must be generated");
            Rect vp = CurrentViewportWorldRect();
            Rect b = g.SelectionBounds;
            Assert.IsTrue(b.xMax > vp.xMin && b.xMin < vp.xMax && b.yMax > vp.yMin && b.yMin < vp.yMax,
                label + ": selection highlight must intersect viewport");
        }

        // --- テスト ---

        /// <summary>
        /// 必須シーケンス全体: 文字入力 → Backspace/Delete → 左右移動 →
        /// Ctrl+A 全選択 → Shift+矢印選択 → 長文オーバーフロー →
        /// クリアして再入力。各ステップ後に実メッシュで caret/選択が
        /// Text Viewport 内にあることと、先頭文字の左余白がドリフトしないことを検査する。
        /// </summary>
        [UnityTest]
        public IEnumerator EditSequence_CaretSelectionStayInsideViewport()
        {
            yield return CreateFixture();
            Assert.AreEqual(string.Empty, input.text);

            // 文字入力 (実キュー → 実モジュール → 実 OnUpdateSelected)。
            yield return Type("abc");
            Assert.AreEqual("abc", input.text);
            Assert.AreEqual(3, input.caretPosition);
            WorldGeometry g = Measure();
            AssertCaretInsideViewport(g, "type 'abc'");

            // 左余白の基準値 (Issue #676 のドリフト検出用)。
            yield return SendKey(KeyCode.LeftArrow);
            yield return SendKey(KeyCode.LeftArrow);
            Assert.AreEqual(1, input.caretPosition);
            yield return SendKey(KeyCode.LeftArrow);
            Assert.AreEqual(0, input.caretPosition);
            g = Measure();
            AssertCaretInsideViewport(g, "caret at start");
            Assert.IsFalse(float.IsNaN(g.FirstCharLeftX), "first char must exist");
            Rect vp = CurrentViewportWorldRect();
            float baselinePadding = g.FirstCharLeftX - vp.xMin;
            Assert.Greater(baselinePadding, -Eps,
                "first char must not start left of viewport: " + baselinePadding);
            Assert.Less(baselinePadding, vp.width * 0.25f,
                "first char must start near viewport left edge, not drifted: " + baselinePadding);

            // Backspace / Delete / 左右移動。
            yield return SendKey(KeyCode.RightArrow);
            yield return SendKey(KeyCode.RightArrow);
            yield return SendKey(KeyCode.RightArrow);
            Assert.AreEqual(3, input.caretPosition);
            yield return SendKey(KeyCode.Backspace);
            Assert.AreEqual("ab", input.text, "Backspace removes char before caret");
            Assert.AreEqual(2, input.caretPosition);
            g = Measure();
            AssertCaretInsideViewport(g, "backspace");

            yield return SendKey(KeyCode.LeftArrow);
            Assert.AreEqual(1, input.caretPosition);
            yield return SendKey(KeyCode.Delete);
            Assert.AreEqual("a", input.text, "Delete removes char after caret");
            Assert.AreEqual(1, input.caretPosition);
            g = Measure();
            AssertCaretInsideViewport(g, "left+delete");

            // Ctrl+A (macOS は Command) 全選択 → 短文なので選択全体が viewport 内。
            // TMP の SelectAll は caret 側=末尾 / 選択側=先頭 (anchor=len, focus=0)。
            // 本番の FocusSelectAll は逆向き (0/len) だが選択範囲は同一。
            yield return SendKey(KeyCode.A, '\0', CtrlMod);
            Assert.AreEqual(input.text.Length, input.selectionStringAnchorPosition, "select-all anchor");
            Assert.AreEqual(0, input.selectionStringFocusPosition, "select-all focus");
            g = Measure();
            AssertSelectionInsideViewport(g, "select all");
            // 選択アクティブ中はキャレット quad がメッシュに生成されない
            // (TMP が selection mesh に一本化する) ため caret 検査はしない。

            // 選択範囲への入力で置き換え。
            yield return SendKey(KeyCode.None, 'x');
            Assert.AreEqual("x", input.text, "typing over selection replaces it");
            Assert.AreEqual(1, input.caretPosition);
            g = Measure();
            AssertCaretInsideViewport(g, "replace selection");

            // Shift+矢印で選択を伸ばし、Backspace で選択削除。
            yield return Type("bcd"); // "xbcd", caret=4
            yield return SendKey(KeyCode.LeftArrow, '\0', EventModifiers.Shift);
            yield return SendKey(KeyCode.LeftArrow, '\0', EventModifiers.Shift);
            Assert.AreEqual(4, input.selectionAnchorPosition, "anchor stays at original caret");
            Assert.AreEqual(2, input.selectionFocusPosition, "shift+left moves focus left");
            g = Measure();
            AssertSelectionIntersectsViewport(g, "shift selection");
            yield return SendKey(KeyCode.Backspace);
            Assert.AreEqual("xb", input.text, "Backspace deletes the selection");
            Assert.AreEqual(2, input.caretPosition);
            g = Measure();
            AssertCaretInsideViewport(g, "backspace after selection");

            // 余白ドリフトがないこと (EditMode と同じ不変条件)。
            yield return SendKey(KeyCode.LeftArrow);
            yield return SendKey(KeyCode.LeftArrow);
            Assert.AreEqual(0, input.caretPosition);
            g = Measure();
            AssertCaretInsideViewport(g, "round-trip caret at start");
            Assert.IsFalse(float.IsNaN(g.FirstCharLeftX));
            float padding = g.FirstCharLeftX - vp.xMin;
            Assert.AreEqual(baselinePadding, padding, 2f,
                "left padding drifted: baseline=" + baselinePadding + " now=" + padding);
        }

        /// <summary>
        /// 保存名ありで開いた状態 (Issue #425) を再現: 初期文字列を入れて
        /// FocusSelectAll で開くと全選択になり、入力で丸ごと置き換わる。
        /// </summary>
        [UnityTest]
        public IEnumerator OpenWithSavedName_SelectAll_ReplaceOnType()
        {
            yield return CreateFixture("SAVED01");
            Assert.AreEqual("SAVED01", input.text);
            Assert.AreEqual(0, input.selectionStringAnchorPosition, "select-all anchor at start");
            Assert.AreEqual(7, input.selectionStringFocusPosition, "select-all focus at end");

            WorldGeometry g = Measure();
            AssertSelectionInsideViewport(g, "saved name select-all");

            yield return Type("NEW");
            Assert.AreEqual("NEW", input.text, "typing over select-all replaces saved name");
            Assert.AreEqual(3, input.caretPosition);
            g = Measure();
            AssertCaretInsideViewport(g, "after replace");
        }

        /// <summary>
        /// 長文オーバーフロー: characterLimit を外して (EditMode と同じ逸脱、
        /// 720px viewport を超える長さにするため) 1 文字ごとに実メッシュで
        /// caret が viewport 内に追従することを検査する。
        /// </summary>
        [UnityTest]
        public IEnumerator OverflowText_CaretFollowScroll_KeepsCaretInsideViewport()
        {
            yield return CreateFixture();
            int savedLimit = input.characterLimit;
            input.characterLimit = 0; // 720px を超える長さまで入力できるよう上限を外す
            try
            {
                for (int i = 0; i < 60; i++)
                {
                    yield return SendKey(KeyCode.None, 'a');
                    WorldGeometry g = Measure();
                    AssertCaretInsideViewport(g, "overflow char " + (i + 1));
                }

                Assert.AreEqual(60, input.text.Length);
                Assert.AreEqual(60, input.caretPosition);
                WorldGeometry end = Measure();
                // スクロールが実際に掛かったこと (textComponent が左へずれた)。
                Assert.Less(textRect.anchoredPosition.x, -1f,
                    "textComponent must be scrolled left for overflow text");
                // 先頭側の文字は viewport 左へクリップされるのが正常。
                Assert.Less(end.FirstCharLeftX, CurrentViewportWorldRect().xMin,
                    "leading chars should be clipped left after scroll");
                AssertCaretInsideViewport(end, "caret at end of overflow");
            }
            finally
            {
                input.characterLimit = savedLimit;
            }
        }

        /// <summary>全選択 → Backspace で空にするとスクロールが原点へ戻り、
        /// 入力し直しても caret が viewport 内・余白が初期位置に留まる。</summary>
        [UnityTest]
        public IEnumerator ClearAndRetype_ScrollResets_AndCaretStaysInside()
        {
            yield return CreateFixture();
            int savedLimit = input.characterLimit;
            input.characterLimit = 0;
            try
            {
                for (int i = 0; i < 50; i++)
                {
                    yield return SendKey(KeyCode.None, 'a');
                }

                Assert.Less(textRect.anchoredPosition.x, -1f, "precondition: scrolled");

                yield return SendKey(KeyCode.A, '\0', CtrlMod);
                yield return SendKey(KeyCode.Backspace);
                Assert.AreEqual(string.Empty, input.text);
                WorldGeometry g = Measure();
                // 空になったらスクロールは原点へ戻る (m_HasTextBeenRemoved 経路)。
                Assert.LessOrEqual(Mathf.Abs(textRect.anchoredPosition.x), 1f,
                    "scroll offset must reset after clearing, got " + textRect.anchoredPosition.x);

                yield return Type("hi");
                g = Measure();
                AssertCaretInsideViewport(g, "retype after clear");
                Assert.IsFalse(float.IsNaN(g.FirstCharLeftX));
                Rect vp = CurrentViewportWorldRect();
                float padding = g.FirstCharLeftX - vp.xMin;
                Assert.Greater(padding, -Eps);
                Assert.Less(padding, vp.width * 0.25f,
                    "padding after retype drifted: " + padding);
            }
            finally
            {
                input.characterLimit = savedLimit;
            }
        }

        /// <summary>
        /// Local モードの文字検証 (ValidateNameChar) が実イベント経路でも効く:
        /// A-Z0-9 以外は入力されない。日本語かな・記号・空白を試す。
        /// </summary>
        [UnityTest]
        public IEnumerator LocalModeValidation_RejectsInvalidChars_ThroughRealEvents()
        {
            yield return CreateFixture();
            yield return Type("A0");
            Assert.AreEqual("A0", input.text, "A-Z0-9 must be accepted");

            yield return SendKey(KeyCode.None, '!');
            yield return SendKey(KeyCode.None, ' ');
            yield return SendKey(KeyCode.None, 'あ');
            Assert.AreEqual("A0", input.text, "non A-Z0-9 must be rejected by onValidateInput");
            Assert.AreEqual(2, input.caretPosition);
        }

        /// <summary>
        /// Enter は TMP の Finish → DeactivateInputField まで実経路で処理され、
        /// フォーカスが外れることを確認する (onEndEdit の画面側意味づけは
        /// modalOpen が private のため対象外 — ヘッダコメント参照)。
        /// </summary>
        [UnityTest]
        public IEnumerator EnterKey_DeactivatesInputField_ThroughRealEvents()
        {
            yield return CreateFixture();
            yield return Type("ab");
            Assert.IsTrue(input.isFocused);

            yield return SendKey(KeyCode.Return);
            Assert.IsFalse(input.isFocused, "Enter must finish editing and deactivate the field");
            Assert.AreEqual("ab", input.text, "text must be preserved after Enter");
        }
    }
}
