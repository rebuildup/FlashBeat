using System;
using UnityEngine;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// v6 デザイントークン (docs/design/v6/flashbeat-v6-contract.md §3/§4)。
    /// 値は契約の variables 写し。実行時の変更はしない。
    /// v5 (<see cref="FlashBeat.UI.V5.V5Tokens"/>) には依存しない独立実装
    /// (v5 の値を v6 契約の範囲で流用)。
    /// すべての寸法は design 空間 (1728×972、左上原点・y_down) 基準。
    /// </summary>
    public static class V6Tokens
    {
        // --- text / surface (契約 §3) ---
        public static readonly Color Text = FromHex("#FFFFFF");
        public static readonly Color TextMuted = FromHex("#B0B0B0");
        public static readonly Color TextDim = FromHex("#8C8C8C");
        public static readonly Color Overlay = FromHex("#222222");

        // --- accent / status (契約 §3) ---
        public static readonly Color Accent = FromHex("#3FA9F5");
        public static readonly Color AccentLight = FromHex("#9AD3FA");
        public static readonly Color WarnLight = FromHex("#F4D88A");

        // --- key ランプ k0..k6 (契約 §3) ---
        public static readonly Color Key0 = FromHex("#131313");
        public static readonly Color Key1 = FromHex("#1B1B1B");
        public static readonly Color Key2 = FromHex("#222222");
        public static readonly Color Key3 = FromHex("#2D2D2D");
        public static readonly Color Key4 = FromHex("#3A3A3A");
        public static readonly Color Key5 = FromHex("#454545");
        public static readonly Color Key6 = FromHex("#525252");

        // --- 共通ボタン (契約 §4) ---
        public static readonly Color ButtonPrimaryBg = FromHex("#EDEDED");
        public static readonly Color ButtonPrimaryLabel = FromHex("#1B1B1B");

        // --- モーダル / 外周 (契約 §4) ---
        public static readonly Color Scrim = FromHex("#00000099");
        public static readonly Color Border = FromHex("#EDEDED33");

        // --- 角丸 (契約 §3/§4) ---
        public const float RadiusSmall = 4f;
        public const float RadiusMedium = 8f;
        public const float RadiusLarge = 16f;

        // --- 共通部品の高さ (契約 §4) ---
        public const float ButtonHeight = 56f;
        public const float KeyCapHeight = 36f;

        /// <summary>
        /// "#RRGGBB" / "#RRGGBBAA" / "#RGB" 形式のカラーパーサ。
        /// Unity の ColorUtility.TryParseHtmlString は icall のため Unity 外で動かず、
        /// 失敗時に透明黒を黙って返すため、純 managed で実装している。
        /// 不正な値は FormatException (トークンの打ち間違いはテストで検出する)。
        /// </summary>
        private static Color FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex[0] != '#')
            {
                throw new FormatException("color token must start with '#': " + hex);
            }

            string body = hex.Substring(1);
            byte r;
            byte g;
            byte b;
            if (body.Length == 3)
            {
                r = (byte)(FromHexDigit(body[0]) * 17);
                g = (byte)(FromHexDigit(body[1]) * 17);
                b = (byte)(FromHexDigit(body[2]) * 17);
            }
            else if (body.Length == 6 || body.Length == 8)
            {
                r = FromHexPair(body, 0);
                g = FromHexPair(body, 2);
                b = FromHexPair(body, 4);
            }
            else
            {
                throw new FormatException("unsupported color token length: " + hex);
            }

            float a = body.Length == 8 ? FromHexPair(body, 6) / 255f : 1f;
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        private static int FromHexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            throw new FormatException("invalid hex digit: " + c);
        }

        private static byte FromHexPair(string s, int offset)
        {
            return (byte)((FromHexDigit(s[offset]) << 4) | FromHexDigit(s[offset + 1]));
        }
    }
}
