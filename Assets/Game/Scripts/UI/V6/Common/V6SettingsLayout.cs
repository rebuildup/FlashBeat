using UnityEngine;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// 設定画面 (Option / Pause) の共有レイアウト規則 (Issue #733 フォローアップ)。
    ///
    /// design v6 の設定画面 (Option B6If6Z 系 / Pause uDNC0) は同じ 1728×972
    /// 共通フレームの同じ Main ゾーン ((24,96) 1680×780) に、同じタブ帯・
    /// 設定パネル・プレビューパネルを置く。寸法の正本はここに置き、
    /// <see cref="V6SettingsScreen"/> が実際に組み立てる。
    /// <c>V6OptionLogic</c> の対応定数はこのクラスへのエイリアスとして残す
    /// (EditMode テストと OptionSceneManager が V6OptionLogic 経由で参照するため)。
    /// </summary>
    public static class V6SettingsLayout
    {
        // --- タブ帯 (design C0piZz): Main ゾーン内 (438,0) 824x48、タブ 200x48 gap8 ---
        public const float TabsX = 438f, TabsY = 0f, TabsW = 824f, TabsH = 48f;
        public const float TabW = 200f, TabGap = 8f;

        /// <summary>タブ名 (design uDNC0 / B6If6Z 共通の 4 タブ)。</summary>
        public const int TabCount = 4;
        public static readonly string[] TabLabels = { "Play", "Display", "Sound", "Data" };

        /// <summary>i 番目のタブの design 左上座標 (Main ゾーン基準)。</summary>
        public static Vector2 TabTopLeft(int index)
        {
            return new Vector2(TabsX + (TabW + TabGap) * index, TabsY);
        }

        // --- 設定パネル (design crVgG / uDNC0 共通): Main (304,68) 512x688 $k2 r16 ---
        /// <summary>パネル高さ (行の収容判定にも使う)。</summary>
        public const float SettingsPanelH = 688f;
        public const float SetX = 304f, SetY = 68f, SetW = 512f, SetH = SettingsPanelH;
        /// <summary>行の内側配置 (パネル左上から): x=8、y=60+step*k、496x56。</summary>
        public const float SetRowX = 8f, SetRowW = 496f;
        public const float SettingsRowFirstY = 60f;
        public const float SettingsRowH = 56f;
        /// <summary>通常の行間 (行数が少ないタブ)。</summary>
        public const float RowStepLoose = 76f;
        /// <summary>行がパネルからあふれるタブの詰め行間。</summary>
        public const float RowStepTight = 60f;

        /// <summary>
        /// 行の行間。design は行数が少ないタブでは 76px、
        /// 行がパネル (688px) からあふれるタブでは 60px に詰める。
        /// 計算の正本は <see cref="V6SettingRowView.RowStep"/> (行ビューと同じ規則)。
        /// </summary>
        public static float RowStep(int rowCount)
        {
            return V6SettingRowView.RowStep(rowCount, SettingsPanelH);
        }

        // --- プレビューパネル (design cqQMc / uDNC0 共通): Main (864,68) 512x688 ---
        public const float PrevX = 864f, PrevY = 68f, PrevW = 512f, PrevH = 688f;

        /// <summary>Game Preview well (design 共通): パネル内 (24,60) 464x280 $k0 r8。</summary>
        public const float GamePrevX = 24f, GamePrevY = 60f;
        public const float GamePrevW = 464f, GamePrevH = 280f;

        // --- 説明ブロック (Divider / Desc Title / Desc。design 共通) ---
        /// <summary>Divider Y はタブごとに違う (r23 で 4 タブを撮った実測値)。</summary>
        public const float PlayDescDividerY = 544f, DisplayDescDividerY = 456f;
        /// <summary>
        /// Sound は音量スライダ (値 fs58 + トラック + ヒント = 156px) を
        /// Game Preview (60..340) の直下に置くため 504 へ下げてある
        /// (design 更新は pen `z7joSy` に反映)。
        /// </summary>
        public const float SoundDescDividerY = 504f, DataDescDividerY = 380f;
        public const float DescTitleOffsetY = 20f, DescOffsetY = 70f;
        public const float DescW = 464f, DescH = 68f;

        /// <summary>タブごとの説明ブロック上端 (Divider Y)。</summary>
        public static float DescDividerYOf(int tab)
        {
            switch (tab)
            {
                case 1: return DisplayDescDividerY;
                case 2: return SoundDescDividerY;
                case 3: return DataDescDividerY;
                default: return PlayDescDividerY;
            }
        }

        // --- Display タブ右パネル: 判定色の凡例 (design EzLfb Judge PERFECT..MISS) ---
        // チップ (24,360) 106x64 r8 $k1 を 118 ピッチ (gap12) で 4 個。
        // 中身は Marker (37,16) 32x4 r2 (判定色) + Label (fs18 $text-dim, 中央, y22 h26)。
        public const float JudgeChipX = 24f, JudgeChipY = 360f;
        public const float JudgeChipW = 106f, JudgeChipH = 64f, JudgeChipStep = 118f;
        public const float JudgeMarkerX = 37f, JudgeMarkerY = 16f;
        public const float JudgeMarkerW = 32f, JudgeMarkerH = 4f;
        public static readonly string[] JudgeLabels = { "PERFECT", "GREAT", "BAD", "MISS" };

        // --- Sound タブ右パネル: 音量スライダの置き場所 (design o0zh7 J7tBYP) ---
        // 部品の寸法・写像は V6VolumeSlider が持つ。ここは置き場所だけ。
        public const float VolumeSliderX = 24f, VolumeSliderY = 344f;

        // --- Play タブ右パネル: JIS キーボードのキー一覧 (design B6If6Z / uDNC0 共通) ---
        // キーピッチ (34px 鍵 + 2px 間隔)、各段の y と先頭 x (段差)。
        public const float GridKeyW = 34f;
        public const float GridKeyH = 40f;
        public const float GridKeyStepX = 36f;
        public static readonly float[] GridRowY = { 356f, 400f, 444f, 488f };
        public static readonly float[] GridRowX = { 8f, 25f, 42f, 59f };

        /// <summary>JIS 刻印の文字配列 (上段から。design の Key 一覧と同じ並び)。
        /// Pause 側の旧 <c>V6PauseViewLogic.JisKeyRows</c> もここへエイリアスする。
        /// <see cref="JisGridKeys"/> の構築に使うため、必ず先に宣言する
        /// (static フィールドは宣言順に初期化される)。</summary>
        public static readonly char[][] JisGridChars =
        {
            new[] { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '-', '^', '\\' },
            new[] { 'Q', 'W', 'E', 'R', 'T', 'Y', 'U', 'I', 'O', 'P', '@', '[' },
            new[] { 'A', 'S', 'D', 'F', 'G', 'H', 'J', 'K', 'L', ';', ':', ']' },
            new[] { 'Z', 'X', 'C', 'V', 'B', 'N', 'M', ',', '.', '/', '_' },
        };

        /// <summary>JIS 配列の物理キー (上段から。design の Key 1..Key _ と同じ並び)。
        /// JIS 刻印 (<see cref="JisGridChars"/>) から <see cref="CharToKeyCodes"/> で導出する。</summary>
        public static readonly KeyCode[][] JisGridKeys = BuildJisGridKeys();

        private static KeyCode[][] BuildJisGridKeys()
        {
            KeyCode[][] keys = new KeyCode[JisGridChars.Length][];
            for (int r = 0; r < JisGridChars.Length; r++)
            {
                keys[r] = new KeyCode[JisGridChars[r].Length];
                for (int k = 0; k < JisGridChars[r].Length; k++)
                {
                    KeyCode[] candidates = CharToKeyCodes(JisGridChars[r][k]);
                    keys[r][k] = candidates.Length > 0 ? candidates[0] : KeyCode.None;
                }
            }

            return keys;
        }

        /// <summary>
        /// JIS 刻印 1 文字 → 候補 KeyCode。
        /// KeyBindings の既定は '-' を Equals と Minus の両方で受ける (KeyConfigUI.DefaultLayout)。
        /// </summary>
        public static KeyCode[] CharToKeyCodes(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return new[] { KeyCode.Alpha0 + (c - '0') };
            }

            if (c >= 'A' && c <= 'Z')
            {
                // KeyCode の英字は小文字の ASCII 値 (KeyCode.A = 97 = 'a')。
                // カルチャ非依存にする (tr-TR の 'I'→'ı' 問題、PR #787 review)。
                return new[] { (KeyCode)char.ToLowerInvariant(c) };
            }

            switch (c)
            {
                case '-': return new[] { KeyCode.Minus, KeyCode.Equals };
                case '^': return new[] { KeyCode.Caret };
                case '\\': return new[] { KeyCode.Backslash };
                case '@': return new[] { KeyCode.At };
                case '[': return new[] { KeyCode.LeftBracket };
                case ';': return new[] { KeyCode.Semicolon };
                case ':': return new[] { KeyCode.Colon };
                case ']': return new[] { KeyCode.RightBracket };
                case ',': return new[] { KeyCode.Comma };
                case '.': return new[] { KeyCode.Period };
                case '/': return new[] { KeyCode.Slash };
                case '_': return new[] { KeyCode.Underscore };
                case '*': return new[] { KeyCode.Asterisk };
                case '`': return new[] { KeyCode.BackQuote };
                default: return System.Array.Empty<KeyCode>();
            }
        }

        /// <summary>キーキャップに出すキー名 (表は <see cref="V6KeyConfigLogic.KeyLabel"/> に一本化)。</summary>
        public static string KeyDisplayName(KeyCode key)
        {
            return V6KeyConfigLogic.KeyLabel(key);
        }
    }
}
