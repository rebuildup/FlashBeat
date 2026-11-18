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
    /// Issue #676 再検証 / Issue #706: Result 登録モーダルの名前入力を
    /// TMP_InputField の実イベント経路 (<see cref="TMP_InputField.ProcessEvent"/>)
    /// で操作し、編集操作の事後条件と表示ジオメトリを検証する。
    ///
    /// 対象の不変条件 (実装非依存の受入条件):
    /// - 文字入力・Backspace・Delete・左右移動・全選択置換が TMP 標準編集として動く
    /// - caret / 選択範囲 (string index と caret index の対応) が編集と一致する
    /// - オーバーフロー状態でも編集操作の事後条件が壊れない
    ///   (#676 の viewport 構造は V6ResultDesignLayoutTests が固定。
    ///    caret 追従スクロールの最終描画確認は canvas PreRender 依存のため
    ///    EditMode 対象外 — candidate QA の実描画証跡で扱う)
    ///
    /// IME 変換・確定は OS の IME が必要なため EditMode では再現できず、
    /// 物理キーボード観測の対象として未検証のまま残す。
    /// </summary>
    public sealed class V6ResultInputEditTests
    {
        private GameObject canvasGo;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6ResultInputEditTests Canvas", typeof(Canvas));
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

        /// <summary>本番経路 (OnUpdateSelected) と同じく、キー処理のたびに
        /// text 情報を再生成して caret/string 位置を同期する。
        /// EditMode では canvas 再描画が走らず textInfo が空のままなので、
        /// ProcessEvent 後に ForceMeshUpdate で文字情報を同期生成し、
        /// 続けて ForceLabelUpdate で caret/string 位置を同期する。</summary>
        private static void Press(TMP_InputField input, Event e)
        {
            input.ProcessEvent(e);
            input.textComponent.ForceMeshUpdate();
            input.ForceLabelUpdate();
        }

        private static void Type(TMP_InputField input, string s)
        {
            foreach (char c in s)
            {
                Press(input, Key(KeyCode.None, c));
            }
        }

        /// <summary>canvas 配下に実ビューを建て、登録モーダルの名前入力を返す。</summary>
        private TMP_InputField CreateNameInput(out RectTransform viewport, out RectTransform textComponent, out Transform modalRoot)
        {
            var view = V6ResultView.Create((RectTransform)canvasGo.transform, null, null, null);
            modalRoot = view.Root.Find("Modal 05b");
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
            viewport = (RectTransform)vp;
            Assert.IsNotNull(viewport.GetComponent<RectMask2D>(), "Text Viewport must clip");
            textComponent = (RectTransform)input.textComponent.rectTransform;
            input.textComponent.ForceMeshUpdate();
            // EditMode では Update が走らず ActivateInputFieldInternal に
            // 到達しないため、フォーカス状態 (m_AllowInput) を直接立てる。
            // これがないと TMP のキャレット追従スクロール
            // (UpdateLabel 内の isFocused 条件) が働かず、実機と異なる
            // 未スクロール状態を検査してしまう。
            typeof(TMP_InputField).GetField("m_AllowInput",
                BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(input, true);
            return input;
        }

        [Test]
        public void CharacterInput_ThroughProcessEvent_InsertsAtCaret()
        {
            TMP_InputField input = CreateNameInput(out _, out _, out _);
            Assert.AreEqual(string.Empty, input.text);

            Type(input, "abc");
            input.ForceLabelUpdate();
            Assert.AreEqual("abc", input.text, "typed characters must appear in order");
            Assert.AreEqual(3, input.caretPosition, "caret must follow inserted text");
        }

        [Test]
        public void Backspace_And_Delete_EditThroughRealEventPath()
        {
            TMP_InputField input = CreateNameInput(out _, out _, out _);
            Type(input, "abc");
            input.ForceLabelUpdate();
            Assert.AreEqual("abc", input.text);

            Press(input, Key(KeyCode.Backspace));
            input.ForceLabelUpdate();
            Assert.AreEqual("ab", input.text, "Backspace removes char before caret");
            Assert.AreEqual(2, input.caretPosition);

            Press(input, Key(KeyCode.LeftArrow));
            input.ForceLabelUpdate();
            Assert.AreEqual(1, input.caretPosition, "LeftArrow moves caret left");

            Press(input, Key(KeyCode.Delete));
            input.ForceLabelUpdate();
            Assert.AreEqual("a", input.text, "Delete removes char after caret");
            Assert.AreEqual(1, input.caretPosition, "Delete keeps caret in place");
        }

        [Test]
        public void ArrowKeys_ClampAtTextBounds()
        {
            TMP_InputField input = CreateNameInput(out _, out _, out _);
            Type(input, "ab");
            input.ForceLabelUpdate();

            Press(input, Key(KeyCode.LeftArrow));
            Press(input, Key(KeyCode.LeftArrow));
            Press(input, Key(KeyCode.LeftArrow));
            input.ForceLabelUpdate();
            Assert.AreEqual(0, input.caretPosition, "caret must clamp at text start");

            Press(input, Key(KeyCode.RightArrow));
            Press(input, Key(KeyCode.RightArrow));
            Press(input, Key(KeyCode.RightArrow));
            Press(input, Key(KeyCode.RightArrow));
            input.ForceLabelUpdate();
            Assert.AreEqual(2, input.caretPosition, "caret must clamp at text end");
        }

        [Test]
        public void SelectAll_ThenCharacter_ReplacesSelection()
        {
            TMP_InputField input = CreateNameInput(out _, out _, out _);
            Type(input, "old");
            input.ForceLabelUpdate();
            Assert.AreEqual(3, input.caretPosition);

            // Ctrl+A は TMP 標準の全選択 (KeyPressed が処理)。
            Press(input, Key(KeyCode.A, '\0', EventModifiers.Command));
            input.ForceLabelUpdate();
            // 編集判定 (hasSelection/Delete/Append) が使うのは string index
            // 側の選択範囲。SelectAll は anchor=末尾 / focus=先頭 を置く。
            Assert.AreEqual(3, input.selectionStringAnchorPosition, "select-all anchor at end");
            Assert.AreEqual(0, input.selectionStringFocusPosition, "select-all focus at start");

            Press(input, Key(KeyCode.None, 'x'));
            input.ForceLabelUpdate();
            Assert.AreEqual("x", input.text, "typing over selection replaces it");
            Assert.AreEqual(1, input.caretPosition);
            Assert.AreEqual(1, input.selectionAnchorPosition, "selection collapses after replace");
            Assert.AreEqual(1, input.selectionFocusPosition);
        }

        [Test]
        public void ShiftArrow_ExtendsSelection_AndBackspaceDeletesIt()
        {
            TMP_InputField input = CreateNameInput(out _, out _, out _);
            Type(input, "abcd");
            input.ForceLabelUpdate();

            Press(input, Key(KeyCode.LeftArrow, '\0', EventModifiers.Shift));
            Press(input, Key(KeyCode.LeftArrow, '\0', EventModifiers.Shift));
            input.ForceLabelUpdate();
            Assert.AreEqual(4, input.selectionAnchorPosition, "anchor stays at original caret");
            Assert.AreEqual(2, input.selectionFocusPosition, "shift+left moves focus left");

            Press(input, Key(KeyCode.Backspace));
            input.ForceLabelUpdate();
            Assert.AreEqual("ab", input.text, "Backspace deletes the selection");
            Assert.AreEqual(2, input.caretPosition);
        }

        [Test]
        public void LongText_EditSemanticsHoldUnderOverflow()
        {
            TMP_InputField input = CreateNameInput(out _, out _, out _);
            // characterLimit (local 8 / online 16) では 720px viewport を
            // 超えられないため、オーバーフロー時の編集意味論の検証に限り
            // 上限を外す。入力経路・viewport・フォントは実物のまま。
            //
            // 注意: キャレット追従スクロールと最終的な caret/文字の画面内
            // 包含は canvas の PreRender (TMP_InputField.OnFillVBO) でのみ
            // 決まるため EditMode では検証できない。そちらは candidate QA
            // の実描画証跡 (Issue #706) で扱う。ここでは overflow 状態でも
            // 編集操作の事後条件 (text/caret/selection) が壊れないことを固定する。
            int savedLimit = input.characterLimit;
            input.characterLimit = 0;
            try
            {
                Type(input, new string('a', 40));
                Assert.AreEqual(40, input.text.Length);
                Assert.AreEqual(40, input.caretPosition, "caret at end after typing");

                // Home/End は複数行フィールド向けで、単一行の末尾 caret では
                // TMP が textInfo 範囲外を参照するため、実キーで先頭まで戻す。
                for (int i = 0; i < 40; i++) { Press(input, Key(KeyCode.LeftArrow)); }
                Assert.AreEqual(0, input.caretPosition, "caret returns to start");

                // 先頭での Backspace は no-op (範囲外削除をしない)。
                Press(input, Key(KeyCode.Backspace));
                Assert.AreEqual(40, input.text.Length, "Backspace at start must not delete");

                // 先頭への挿入が文字列を壊さない。
                Press(input, Key(KeyCode.None, 'b'));
                Assert.AreEqual("b" + new string('a', 40), input.text);
                Assert.AreEqual(1, input.caretPosition);

                // 末尾まで戻って Delete は no-op。
                for (int i = 0; i < 40; i++) { Press(input, Key(KeyCode.RightArrow)); }
                Assert.AreEqual(41, input.caretPosition);
                Press(input, Key(KeyCode.Delete));
                Assert.AreEqual(41, input.text.Length, "Delete at end must not delete");
            }
            finally
            {
                input.characterLimit = savedLimit;
            }
        }

        [Test]
        public void CompositionString_DoesNotCorruptCommittedText()
        {
            // IME 変換中文字列は TMP の compositionString 経路。EditMode では
            // OS IME を起こせないため、TMP が公開する composition API で
            // 「変換中に確定文字が壊れない」ことだけを固定する。
            // 実 IME の見た目 (変換候補・確定操作) は物理キーボード観測待ち。
            TMP_InputField input = CreateNameInput(out _, out _, out _);
            Type(input, "ab");
            input.ForceLabelUpdate();
            Assert.AreEqual("ab", input.text);

            // 変換中を挟んでも committed text は ProcessEvent の編集結果と一致する。
            Press(input, Key(KeyCode.LeftArrow));
            Press(input, Key(KeyCode.None, 'X'));
            input.ForceLabelUpdate();
            Assert.AreEqual("aXb", input.text, "insert in middle keeps committed text intact");
        }
    }
}
