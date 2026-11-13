using System.Collections.Generic;
using UnityEngine;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// JIS 配列 (OADG 109A) のビジュアルキーボード定義 (Issue #428)。
    ///
    /// キー設定モーダルのテキスト一覧を置き換えるための純データ。
    /// 各キーは Unity の <see cref="KeyCode"/> (US 配列位置ベース) と
    /// 表示ラベル・幅 (キーキャップ単位)・割当可否を持つ。
    /// 修飾キー・IME 系は表示だけで割当対象にしない
    /// (<see cref="V6KeyConfigLogic.IsModifier"/> / 予約キーで別途弾く)。
    /// </summary>
    public static class V6JisKeyboard
    {
        /// <summary>キーボードの 1 キー。</summary>
        public struct Key
        {
            /// <summary>Unity KeyCode (US 配置位置)。表示のみのキーは None。</summary>
            public KeyCode Code;
            /// <summary>キーキャップに出す文字。</summary>
            public string Label;
            /// <summary>幅 (標準キーキャップ 1 個 = 1)。</summary>
            public float Width;
            /// <summary>クリックで割当・レーン選択できるか。</summary>
            public bool Assignable;
        }

        /// <summary>標準キーキャップの幅・高さ (design px)。</summary>
        public const float KeyW = 52f;
        public const float KeyH = 44f;
        public const float KeyGap = 4f;
        /// <summary>行と行の間隔。</summary>
        public const float RowGap = 6f;

        private static Key K(KeyCode code, string label, float width = 1f, bool assignable = true)
        {
            return new Key { Code = code, Label = label, Width = width, Assignable = assignable };
        }

        private static Key Blank(float width, string label = "")
        {
            return new Key { Code = KeyCode.None, Label = label, Width = width, Assignable = false };
        }

        /// <summary>
        /// JIS 配列の行 (上から順)。割当対象は判定キーとして使える文字キーのみ。
        /// `@ [ ] ; : \ / _ * &lt; &gt; ^` などの記号 KeyCode は JIS の物理キーに
        /// 対応づけて表示する (US 配列の位置ではなく文字が出るキー)。
        /// </summary>
        public static readonly Key[][] Rows =
        {
            // 数字行: ` 1! 2@ 3# 4$ 5% 6^ 7& 8* 9( 0) -^ =~ (BackSpace は省略)
            new[]
            {
                Blank(1f, "Esc"),
                K(KeyCode.Alpha1, "1"), K(KeyCode.Alpha2, "2"), K(KeyCode.Alpha3, "3"),
                K(KeyCode.Alpha4, "4"), K(KeyCode.Alpha5, "5"), K(KeyCode.Alpha6, "6"),
                K(KeyCode.Alpha7, "7"), K(KeyCode.Alpha8, "8"), K(KeyCode.Alpha9, "9"),
                K(KeyCode.Alpha0, "0"),
                K(KeyCode.Minus, "-"), K(KeyCode.Caret, "^"), K(KeyCode.Equals, "="),
            },
            // QWERTY 行: Tab Q..P @ [{ ]} Enter
            new[]
            {
                Blank(1.4f, "Tab"),
                K(KeyCode.Q, "Q"), K(KeyCode.W, "W"), K(KeyCode.E, "E"), K(KeyCode.R, "R"),
                K(KeyCode.T, "T"), K(KeyCode.Y, "Y"), K(KeyCode.U, "U"), K(KeyCode.I, "I"),
                K(KeyCode.O, "O"), K(KeyCode.P, "P"),
                K(KeyCode.At, "@"), K(KeyCode.LeftBracket, "["), K(KeyCode.RightBracket, "]"),
                Blank(1.8f, "Enter"),
            },
            // A 行: Caps A..L ;: +' Enter
            new[]
            {
                Blank(1.8f, "Caps"),
                K(KeyCode.A, "A"), K(KeyCode.S, "S"), K(KeyCode.D, "D"), K(KeyCode.F, "F"),
                K(KeyCode.G, "G"), K(KeyCode.H, "H"), K(KeyCode.J, "J"), K(KeyCode.K, "K"),
                K(KeyCode.L, "L"),
                K(KeyCode.Semicolon, ";"), K(KeyCode.Quote, "'"),
                Blank(1.8f, "Enter"),
            },
            // Z 行: Shift Z..M ,< .> /? \| Shift
            new[]
            {
                Blank(2.2f, "Shift"),
                K(KeyCode.Z, "Z"), K(KeyCode.X, "X"), K(KeyCode.C, "C"), K(KeyCode.V, "V"),
                K(KeyCode.B, "B"), K(KeyCode.N, "N"), K(KeyCode.M, "M"),
                K(KeyCode.Comma, ","), K(KeyCode.Period, "."), K(KeyCode.Slash, "/"),
                K(KeyCode.Backslash, "\\"),
                Blank(2.7f, "Shift"),
            },
            // 下段: Ctrl Win Alt Space 無変換 変換 ひらがな カタカナ Alt Win Menu
            new[]
            {
                Blank(1.2f, "Ctrl"), Blank(1.2f, "Win"), Blank(1.2f, "Alt"),
                Blank(6.4f, "Space"),
                Blank(1.4f, "無変換"), Blank(1.4f, "変換"),
                Blank(1.4f, "ひら"), Blank(1.4f, "カナ"),
                Blank(1.2f, "Alt"), Blank(1.2f, "Win"), Blank(1.2f, "Menu"),
            },
        };

        /// <summary>行の幅 (px、gap 込み)。</summary>
        public static float RowWidth(Key[] row)
        {
            float w = 0f;
            for (int i = 0; i < row.Length; i++)
            {
                w += row[i].Width * KeyW;
                if (i > 0) w += KeyGap;
            }

            return w;
        }
    }
}
