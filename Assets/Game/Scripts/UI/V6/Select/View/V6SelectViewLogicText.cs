namespace FlashBeat.UI.V6.Select.View
{
    /// <summary>
    /// V6SelectViewLogic のテキスト計測部分 (Issue #385 Phase 2.10 で分割)。
    /// 文字幅の概算 (em 単位) とキーキャップ幅の算出を持つ。partial class のため
    /// 定数は V6SelectViewLogic 本体と共有する。
    /// </summary>
    public static partial class V6SelectViewLogic
    {
        /// <summary>
        /// 文字の概算幅 (design px)。実フォントの字幅より広く取り、
        /// 「この幅があれば 1 行に収まる」ことの保証に使う。
        /// </summary>
        public static float EstimateTextWidth(string text, float fontSize)
        {
            if (string.IsNullOrEmpty(text) || fontSize <= 0f)
            {
                return 0f;
            }

            return TextEmWidth(text) * fontSize * TextWidthSafety;
        }

        /// <summary>文字の概算幅 (em 単位)。安全係数をかけない素の字幅。</summary>
        public static float TextEmWidth(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            float em = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                em += CharEmWidth(text[i]);
            }

            return em;
        }

        /// <summary>1 文字の字幅 (em 単位)。全角 = 1.0、半角は種類別に固定的。</summary>
        public static float CharEmWidth(char c)
        {
            if (c >= 0x1100 && (c <= 0x115F || c == 0x2329 || c == 0x232A
                || (c >= 0x2E80 && c <= 0xA4CF && c != 0x303F)
                || (c >= 0xAC00 && c <= 0xD7A3)
                || (c >= 0xF900 && c <= 0xFAFF)
                || (c >= 0xFE30 && c <= 0xFE6F)
                || (c >= 0xFF00 && c <= 0xFF60)
                || (c >= 0xFFE0 && c <= 0xFFE6)
                || (c >= 0x20000 && c <= 0x3FFFD)))
            {
                return 1f;
            }

            if (c >= 0x2000 && c <= 0x2BFF)
            {
                // 記号・矢印・絵文字 (↑ ↓ ← → ♪ ≡ ⚙ ▶ …) は全角扱いに倒す。
                return 1f;
            }

            switch (c)
            {
                case ' ':
                    return 0.28f;
                case 'i':
                case 'l':
                case 'j':
                case '.':
                case ',':
                case ':':
                case ';':
                case '!':
                case '\'':
                case '|':
                    return 0.30f;
                case 't':
                case 'f':
                case 'r':
                case 'I':
                case '(':
                case ')':
                case '[':
                case ']':
                case '{':
                case '}':
                case '/':
                case '\\':
                    return 0.36f;
                case 'm':
                case 'w':
                case 'M':
                case 'W':
                    return 0.85f;
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    return 0.56f;
                case 'A':
                case 'B':
                case 'C':
                case 'D':
                case 'E':
                case 'F':
                case 'G':
                case 'H':
                case 'J':
                case 'K':
                case 'L':
                case 'N':
                case 'O':
                case 'P':
                case 'Q':
                case 'R':
                case 'S':
                case 'T':
                case 'U':
                case 'V':
                case 'X':
                case 'Y':
                case 'Z':
                    return 0.66f;
                default:
                    return 0.58f;
            }
        }

        /// <summary>
        /// キーキャップの幅 (design px)。ラベルの自然幅 + 左右余白。
        /// キャップの中でラベルが 1 行に収まることを保証する。
        /// </summary>
        public static float KeyCapWidth(string label, float fontSize)
        {
            return EstimateTextWidth(label, fontSize) + CapPadX * 2f;
        }
    }
}
