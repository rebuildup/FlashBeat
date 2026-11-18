using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// v6 共通の「カーソル付きテキスト入力」部品 (Issue #385 Phase 1.4)。
    /// TMP_InputField の組み立て (テキスト / プレースホルダ / キャレット /
    /// 選択色 / IME / 確定通知) とフォーカス操作を 1 箇所に集約する。
    ///
    /// 対象は現在 1 箇所だけ: Result の名前入力モーダル
    /// (<see cref="FlashBeat.UI.V6.Result.V6ResultView"/>、Issue #384 の
    /// 名前入力カーソル)。Select 画面の検索欄は TMP_InputField を使わず
    /// <c>Input.inputString</c> を query へ追記するだけの別機構 (カーソル位置を
    /// 持たない) なので、ここには含めない。V5 の ResultView も別系統。
    /// 1 箇所しか使わないが、Issue #384 系のカーソル挙動 (点滅周期・全選択で
    /// 開く・Esc で確定しない) を画面側に散らさないため、TMP 設定の塊だけを
    /// 共通部品として切り出した。ウィジェット全体 (枠・ヒント・ボタン) は
    /// 画面ごとに design が違うため、ここでは作らない。
    ///
    /// フォーカス / 全選択 / 確定の意味づけ (空は確定しない、ローカル名の
    /// 文字検証、二重送信防止など) は画面側の責務で、ここには持ち込まない。
    /// </summary>
    public static class V6CursorInput
    {
        /// <summary>カーソル点滅の半周期 (design 530ms、Issue #384 の design 決定)。</summary>
        public const float CaretHalfCycleSeconds = 0.53f;

        /// <summary><see cref="Attach"/> の指定。未指定の項目は v6 既定値。</summary>
        public sealed class Spec
        {
            /// <summary>入力文字の表示 (textComponent)。必須。</summary>
            public TextMeshProUGUI Value;
            /// <summary>空のときの表示 (placeholder)。null 可。</summary>
            public Graphic Placeholder;
            /// <summary>
            /// 選択ハイライトの対象 Graphic。null なら host の Graphic
            /// (枠パネルの Image) を使う。
            /// </summary>
            public Graphic TargetGraphic;
            /// <summary>
            /// テキストの表示領域。null なら host の RectTransform を使う。
            /// textComponent の rect とは別の固定ノードを渡すこと —
            /// TMP は textComponent の anchoredPosition をスクロールでずらす
            /// 前提なので、viewport==text rect だとスクロール量が二重計上
            /// されて文字・キャレット・選択・IME 変換文字が内側余白から
            /// ドリフトする (Issue #676)。実際のクリップには viewport 側へ
            /// RectMask2D を付け、textComponent はその子に置く。
            /// </summary>
            public RectTransform TextViewport;
            /// <summary>入力方式。既定 Standard = IME 変換可 (日本語名を入れるため)。</summary>
            public TMP_InputField.ContentType ContentType = TMP_InputField.ContentType.Standard;
            /// <summary>行種別。既定 SingleLine。</summary>
            public TMP_InputField.LineType LineType = TMP_InputField.LineType.SingleLine;
            /// <summary>1 文字ごとの検証 (null なら全て受け付ける)。</summary>
            public TMP_InputField.OnValidateInput OnValidateInput;
            /// <summary>Enter / フォーカス喪失での確定通知 (null 可)。</summary>
            public UnityEngine.Events.UnityAction<string> OnEndEdit;
            /// <summary>キャレット幅 (design px)。既定 2。</summary>
            public int CaretWidth = 2;
            /// <summary>キャレット色。既定 $text。</summary>
            public Color CaretColor = V6Tokens.Text;
            /// <summary>選択範囲の色。既定は白 20% のハイライト。</summary>
            public Color SelectionColor = new Color(1f, 1f, 1f, 0.2f);
            /// <summary>点滅半周期 (秒)。既定は design の 530ms。</summary>
            public float CaretBlinkHalfCycleSeconds = CaretHalfCycleSeconds;
            /// <summary>
            /// フォーカス時に全選択するか。既定 false — 開く側は
            /// <see cref="FocusSelectAll"/> で「全選択」を明示する
            /// (Issue #425: 保存名を残したまま開く画面と、選択状態で開く
            /// 画面を同じ部品で両立させるため、TMP 側の自動全選択は切る)。
            /// </summary>
            public bool OnFocusSelectAll;
            /// <summary>
            /// Esc で編集を取り消したとき元の文字列へ戻すか。既定 false —
            /// 戻すと確定扱いの onEndEdit が古い名前を送る事故になる
            /// (Issue #384 / PR #387)。
            /// </summary>
            public bool RestoreOriginalTextOnEscape;
        }

        /// <summary>
        /// host (枠パネル等) に TMP_InputField を付け、<paramref name="spec"/>
        /// の表示先とコールバックを配線する。GameObject 名・階層は host 側が
        /// 決める (ここでは子を作らない)。文字数上限 (characterLimit) や
        /// 初期文字列は呼び出し側が開くたびに設定する。
        /// </summary>
        public static TMP_InputField Attach(GameObject host, Spec spec)
        {
            if (host == null)
            {
                throw new ArgumentNullException("host");
            }

            if (spec == null)
            {
                throw new ArgumentNullException("spec");
            }

            if (spec.Value == null)
            {
                throw new ArgumentException("spec.Value is required", "spec");
            }

            var input = host.AddComponent<TMP_InputField>();
            input.targetGraphic = spec.TargetGraphic != null
                ? spec.TargetGraphic
                : host.GetComponent<Graphic>();
            input.textViewport = spec.TextViewport != null
                ? spec.TextViewport
                : host.GetComponent<RectTransform>();
            input.textComponent = spec.Value;
            input.placeholder = spec.Placeholder;
            input.lineType = spec.LineType;
            input.contentType = spec.ContentType;
            input.caretWidth = spec.CaretWidth;
            // design は点滅の半周期 530ms。TMP は 1/rate 秒ごとに表示を反転する。
            input.caretBlinkRate = 1f / spec.CaretBlinkHalfCycleSeconds;
            input.customCaretColor = true;
            input.caretColor = spec.CaretColor;
            input.selectionColor = spec.SelectionColor;
            input.onFocusSelectAll = spec.OnFocusSelectAll;
            input.restoreOriginalTextOnEscape = spec.RestoreOriginalTextOnEscape;
            if (spec.OnValidateInput != null)
            {
                input.onValidateInput = spec.OnValidateInput;
            }

            if (spec.OnEndEdit != null)
            {
                input.onEndEdit.AddListener(spec.OnEndEdit);
            }

            return input;
        }

        /// <summary>
        /// 入力欄へフォーカスし、全選択状態で開く (Issue #425)。
        /// 文字列位置で選択する (caret 位置は TMP の textInfo 生成前だと
        /// 0 へクランプされて選択が潰れるため、編集判定が使う string index
        /// 側を直接置く)。何も打たずに Enter なら現在の文字列がそのまま
        /// 確定し、何か入力すれば選択範囲が置き換わる。
        /// </summary>
        public static void FocusSelectAll(TMP_InputField input)
        {
            if (input == null)
            {
                return;
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(input.gameObject);
            }

            input.ActivateInputField();
            input.selectionStringAnchorPosition = 0;
            input.selectionStringFocusPosition = input.text.Length;
        }

        /// <summary>
        /// 入力欄のフォーカスを外す。選択解除も行う
        /// (EventSystem の選択がこの入力欄を指しているときだけ)。
        /// </summary>
        public static void Unfocus(TMP_InputField input)
        {
            if (input == null)
            {
                return;
            }

            if (input.isFocused)
            {
                input.DeactivateInputField();
            }

            if (EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == input.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}
