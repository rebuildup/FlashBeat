using UnityEngine;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// Design System v5 (design/flashbeat-ui.pen) のデザイントークン。
    /// 値は pen CLI `Print(GetVariables())` (2026-11-07 取得) と
    /// Components v5 の Prefab/* ノード実測値からの写し。実行時の変更はしない。
    /// サイズ系 (フォントサイズ/行の高さ等) は <see cref="UISizeProfile"/> を使うこと。
    /// </summary>
    public static class V5Tokens
    {
        // --- surface / text (variables) ---
        public static readonly Color Bg = FromHex("#1B1B1B");
        public static readonly Color Text = FromHex("#FFFFFF");
        public static readonly Color TextMuted = FromHex("#B0B0B0");
        public static readonly Color TextDim = FromHex("#8C8C8C");
        public static readonly Color Overlay = FromHex("#222222");
        public static readonly Color Deep = FromHex("#1A1A1A");
        public static readonly Color Well = FromHex("#131313");
        public static readonly Color Base = FromHex("#2D2D2D");

        // --- accent / status (variables) ---
        public static readonly Color Accent = FromHex("#3FA9F5");
        public static readonly Color AccentTint = FromHex("#2B4457");
        public static readonly Color AccentLight = FromHex("#9AD3FA");
        public static readonly Color Danger = FromHex("#FF6B6B");
        public static readonly Color DangerTint = FromHex("#4D3232");
        public static readonly Color DangerLight = FromHex("#FFA8A8");
        public static readonly Color Warn = FromHex("#E9B949");
        public static readonly Color WarnTint = FromHex("#4A4029");
        public static readonly Color WarnLight = FromHex("#F4D88A");
        public static readonly Color Ok = FromHex("#4CC38A");
        public static readonly Color OkTint = FromHex("#2B4A3C");
        public static readonly Color OkLight = FromHex("#8FE0B8");

        // --- raised controls (variables u1..u5) ---
        public static readonly Color Raised1 = FromHex("#363636");
        public static readonly Color Raised2 = FromHex("#404040");
        public static readonly Color Raised3 = FromHex("#4B4B4B");
        public static readonly Color Raised4 = FromHex("#585858");
        public static readonly Color Raised5 = FromHex("#696969");

        // --- recessed lists (variables d1..d4) ---
        public static readonly Color Recessed1 = FromHex("#222222");
        public static readonly Color Recessed2 = FromHex("#1B1B1B");
        public static readonly Color Recessed3 = FromHex("#151515");
        public static readonly Color Recessed4 = FromHex("#0F0F0F");

        // --- keycaps (variables k0..k6) ---
        public static readonly Color Key0 = FromHex("#131313");
        public static readonly Color Key1 = FromHex("#1B1B1B");
        public static readonly Color Key2 = FromHex("#222222");
        public static readonly Color Key3 = FromHex("#2D2D2D");
        public static readonly Color Key4 = FromHex("#3A3A3A");
        public static readonly Color Key5 = FromHex("#454545");
        public static readonly Color Key6 = FromHex("#525252");

        // --- Components v5 実測 (Prefab ノードの fill / cornerRadius) ---
        /// <summary>Button Primary の背景 (variables には無いリテラル)。</summary>
        public static readonly Color ButtonPrimaryBg = FromHex("#EDEDED");
        /// <summary>Button Primary のラベル/アイコン色。</summary>
        public static readonly Color ButtonPrimaryLabel = FromHex("#1B1B1B");
        /// <summary>Button Secondary / KeyCap / Tag / MenuButton / SearchField の背景。</summary>
        public static readonly Color ControlBg = Key4;

        /// <summary>KeyCap / Tag / サムネイルの角丸 (px, fhd 基準)。</summary>
        public const float RadiusSmall = 4f;
        /// <summary>Button / Row / SearchField の角丸 (px, fhd 基準)。</summary>
        public const float RadiusMedium = 8f;

        /// <summary>UI 用フォントファミリ名 (variables font-ui / font-num)。</summary>
        public const string FontFamily = "Noto Sans JP";

        /// <summary>フォントの TMP アセットパス (JapaneseFontFixer と同じ)。</summary>
        public const string FontAssetPath = "Assets/Game/Fonts/Japanese/NotoSansJP-Medium SDF.asset";

        private static Color FromHex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
