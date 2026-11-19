using TMPro;
using FlashBeat.UI.V5;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// V6 の「テキストを design 矩形に収める」共通規約 (Issue #749)。
    ///
    /// V6 のテキストはほぼすべて「固定矩形に収める」前提なので、はみ出し方は
    /// 2 系統に整理する:
    ///
    /// - <see cref="Ellipsis"/>: 人名・曲名など「読めなくても位置が分かればよい」
    ///   可変長テキスト。はみ出す分は末尾を … で省略する (V6Lists の行 Name、
    ///   Select ヘッダー Status、KeyConfig の Status など)。
    /// - <see cref="AutoFit"/>: 数値・短い状態文・ボタンラベルなど「全文を読ませたい」
    ///   テキスト。現在の fontSize を上限に TMP auto-size で矩形内へ縮める
    ///   (Result モーダルのスコア/ステータス、Pause 行の値、History 空状態など)。
    ///
    /// どちらも textWrappingMode = NoWrap を前提にする (Normal 折返しは
    /// 固定高矩形で 2 行目が下のパネルへはみ出す #749 の主症状)。
    /// Ellipsis 側は TMP の「実効高 &lt; 行送りで 0 行」仕様があるため
    /// <see cref="V5Ui.FitEllipsisToLineHeight"/> を必ず併用する。
    /// </summary>
    public static class V6TextFit
    {
        /// <summary>
        /// 1 行固定 + 末尾省略 (NoWrap + Ellipsis + 行送り実効高の補正)。
        /// rect / fontSize を確定した後に呼ぶこと (V5Ui.FitEllipsisToLineHeight と同じ契約)。
        /// rectHeight は LayoutGroup 配下など rect 未確定のときの矩形高明示 (0 以下は rect から読む)。
        /// </summary>
        public static void Ellipsis(TextMeshProUGUI tmp, float rectHeight = 0f)
        {
            if (tmp == null)
            {
                return;
            }

            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            V5Ui.FitEllipsisToLineHeight(tmp, rectHeight);
        }

        /// <summary>
        /// 1 行固定 + TMP auto-size (NoWrap + fontSizeMin..fontSizeMax)。
        /// fontSizeMax は現在の fontSize (design 値) で、短い文字列は design どおり、
        /// 長い文字列だけが min まで縮む。min は可読性の下限で、これを下回る値は
        /// 設計上あり得ない想定 (Result 数値の AutoFit と同じ規約 #737)。
        /// </summary>
        public static void AutoFit(TextMeshProUGUI tmp, float minFontSize)
        {
            if (tmp == null)
            {
                return;
            }

            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            float max = tmp.fontSize;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = minFontSize;
            tmp.fontSizeMax = max;
            tmp.fontSize = max;
        }
    }
}
