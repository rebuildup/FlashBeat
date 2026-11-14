using System;
using FlashBeat.UI.V6.Common;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #385 (Phase 1.4): V6CursorInput が TMP_InputField の v6 設定
    /// (キャレット点滅 530ms 半周期・$text 色・選択色・IME 可・全選択/復元なし)
    /// とフォーカス操作 (全選択で開く / 選択解除して閉じる) を契約どおりに
    /// 配線することを固定する。現時点の利用者は Result の名前入力モーダル
    /// (Issue #384) のみだが、TMP 設定の塊はここに集約する。
    /// </summary>
    public class V6CursorInputTests
    {
        private GameObject host;
        private GameObject valueGo;
        private GameObject placeholderGo;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Field", typeof(RectTransform), typeof(Image));
            valueGo = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
            placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                UnityEngine.Object.DestroyImmediate(host);
            }

            if (valueGo != null)
            {
                UnityEngine.Object.DestroyImmediate(valueGo);
            }

            if (placeholderGo != null)
            {
                UnityEngine.Object.DestroyImmediate(placeholderGo);
            }
        }

        private V6CursorInput.Spec DefaultSpec()
        {
            return new V6CursorInput.Spec
            {
                Value = valueGo.GetComponent<TextMeshProUGUI>(),
                Placeholder = placeholderGo.GetComponent<Graphic>(),
            };
        }

        [Test]
        public void Attach_ThrowsOnNullHostSpecOrMissingValue()
        {
            Assert.Throws<ArgumentNullException>(() => V6CursorInput.Attach(null, DefaultSpec()));
            Assert.Throws<ArgumentNullException>(() => V6CursorInput.Attach(host, null));
            Assert.Throws<ArgumentException>(() => V6CursorInput.Attach(host,
                new V6CursorInput.Spec { Placeholder = placeholderGo.GetComponent<Graphic>() }));
        }

        [Test]
        public void Attach_WiresComponentsAndCaretContract()
        {
            TMP_InputField input = V6CursorInput.Attach(host, DefaultSpec());

            Assert.AreSame(input, host.GetComponent<TMP_InputField>(), "host に TMP_InputField が付く");
            Assert.AreSame(valueGo.GetComponent<TextMeshProUGUI>(), input.textComponent,
                "textComponent は Value");
            Assert.AreSame(placeholderGo.GetComponent<Graphic>(), input.placeholder,
                "placeholder は Placeholder");
            Assert.AreSame(host.GetComponent<Image>(), input.targetGraphic,
                "targetGraphic 未指定なら host の Graphic");
            Assert.AreSame(host.GetComponent<RectTransform>(), input.textViewport,
                "textViewport 未指定なら host の RectTransform");

            Assert.AreEqual(TMP_InputField.LineType.SingleLine, input.lineType);
            Assert.AreEqual(TMP_InputField.ContentType.Standard, input.contentType,
                "Standard = IME 変換可 (日本語名を入れるため)");
            Assert.AreEqual(2, input.caretWidth);
            Assert.AreEqual(1f / V6CursorInput.CaretHalfCycleSeconds, input.caretBlinkRate, 1e-6f,
                "点滅は design の半周期 530ms");
            Assert.IsTrue(input.customCaretColor);
            Assert.AreEqual(V6Tokens.Text, input.caretColor, "キャレットは $text");
            Assert.AreEqual(new Color(1f, 1f, 1f, 0.2f), input.selectionColor);
            Assert.IsFalse(input.onFocusSelectAll, "TMP 側の自動全選択は切る (#425)");
            Assert.IsFalse(input.restoreOriginalTextOnEscape,
                "Esc で元へ戻すと確定扱いの onEndEdit が古い名前を送る (#384)");
        }

        [Test]
        public void Attach_HonorsSpecOverrides()
        {
            var spec = DefaultSpec();
            spec.CaretBlinkHalfCycleSeconds = 0.25f;
            spec.CaretWidth = 3;
            spec.CaretColor = V6Tokens.Accent;
            spec.SelectionColor = V6Tokens.Danger;
            spec.ContentType = TMP_InputField.ContentType.Alphanumeric;
            spec.LineType = TMP_InputField.LineType.MultiLineNewline;
            spec.OnFocusSelectAll = true;
            spec.RestoreOriginalTextOnEscape = true;

            TMP_InputField input = V6CursorInput.Attach(host, spec);

            Assert.AreEqual(0.25f, 1f / input.caretBlinkRate, 1e-4f, "半周期の指定が効く");
            Assert.AreEqual(3, input.caretWidth);
            Assert.AreEqual(V6Tokens.Accent, input.caretColor);
            Assert.AreEqual(V6Tokens.Danger, input.selectionColor);
            Assert.AreEqual(TMP_InputField.ContentType.Alphanumeric, input.contentType);
            Assert.AreEqual(TMP_InputField.LineType.MultiLineNewline, input.lineType);
            Assert.IsTrue(input.onFocusSelectAll);
            Assert.IsTrue(input.restoreOriginalTextOnEscape);
        }

        [Test]
        public void Attach_WiresValidateAndEndEditCallbacks()
        {
            string ended = null;
            var spec = DefaultSpec();
            spec.OnValidateInput = (text, charIndex, addedChar) => addedChar == 'x' ? addedChar : '\0';
            spec.OnEndEdit = value => ended = value;

            TMP_InputField input = V6CursorInput.Attach(host, spec);

            Assert.IsNotNull(input.onValidateInput);
            Assert.AreEqual('x', input.onValidateInput("ab", 1, 'x'));
            Assert.AreEqual('\0', input.onValidateInput("ab", 1, 'y'), "検証で弾く");

            input.onEndEdit.Invoke("abc");
            Assert.AreEqual("abc", ended, "onEndEdit が呼ばれる");
        }

        [Test]
        public void FocusSelectAll_CaretToEnd_ThenSelectAll()
        {
            TMP_InputField input = V6CursorInput.Attach(host, DefaultSpec());
            input.SetTextWithoutNotify("abc");

            V6CursorInput.FocusSelectAll(input);

            Assert.AreEqual(3, input.caretPosition, "caret は文末");
            Assert.AreEqual(0, input.selectionAnchorPosition, "選択は先頭から");
            Assert.AreEqual(3, input.selectionFocusPosition, "選択は文末まで (全選択)");
        }

        [Test]
        public void FocusAndUnfocus_NullSafe()
        {
            Assert.DoesNotThrow(() => V6CursorInput.FocusSelectAll(null));
            Assert.DoesNotThrow(() => V6CursorInput.Unfocus(null));
        }
    }
}
