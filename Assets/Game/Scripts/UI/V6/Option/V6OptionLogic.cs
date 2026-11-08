using System;
using UnityEngine;

namespace FlashBeat.UI.V6.Option
{
    /// <summary>v6 Option 画面のキーボードアクション (実キーへの割り当ては V6OptionView 側)。</summary>
    public enum V6OptionAction
    {
        None,
        MoveUp,
        MoveDown,
        AdjustPrev,
        AdjustNext,
        Confirm,
        Cancel,
        NextTab,
        PrevTab,
    }

    /// <summary>
    /// v6 Option 画面の純ロジック (EditMode テスト対象)。
    /// design v6 の 06 Play (B6If6Z) / 06 Display (EzLfb) / 06 Sound (z7joSy) / 06 Data (DQHHp)
    /// 共通フレーム 1728x972 基準。Unity API は KeyCode (列挙体) と数値計算のみに限定する。
    /// </summary>
    public static class V6OptionLogic
    {
        /// <summary>design v6 共通フレームの基準サイズ。</summary>
        public const float DesignW = 1728f;
        public const float DesignH = 972f;

        /// <summary>タブ数 (Play / Display / Sound / Data)。</summary>
        public const int TabCount = 4;

        public static readonly string[] TabLabels = { "Play", "Display", "Sound", "Data" };

        // 設定パネル (列2-3) の行ジオメトリ (design px)。
        public const float SettingsPanelH = 688f;
        public const float SettingsRowFirstY = 60f;
        public const float SettingsRowH = 56f;
        /// <summary>通常の行間 (Play / Sound / Data の design 実測)。</summary>
        public const float RowStepLoose = 76f;
        /// <summary>行が多いときの詰め行間 (Display の design 実測)。</summary>
        public const float RowStepTight = 60f;

        /// <summary>
        /// design px → 実ピクセルの倍率。実画面 3 解像度
        /// (1920x1080 / 2560x1440 / 2560x1664) でフレームが収まるよう
        /// min(W/1728, H/972) 倍する (V6SelectViewLogic.DesignScale と同じ方式)。
        /// </summary>
        public static float DesignScale(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return 1f;
            }

            float byW = width / DesignW;
            float byH = height / DesignH;
            return byW < byH ? byW : byH;
        }

        /// <summary>タブ index を範囲内に収める。</summary>
        public static int ClampTabIndex(int index)
        {
            if (index < 0) return 0;
            if (index > TabCount - 1) return TabCount - 1;
            return index;
        }

        /// <summary>行選択の折り返し。</summary>
        public static int WrapIndex(int index, int count, int delta)
        {
            if (count <= 0)
            {
                return 0;
            }

            return (index + delta % count + count) % count;
        }

        /// <summary>
        /// 設定行の行間。design は行数が少ないタブでは 76px、
        /// 行がパネル (688px) からあふれるタブでは 60px に詰める
        /// (06 Display が 9 行を 60px 間隔で置いた実測に合わせる)。
        /// </summary>
        public static float RowStep(int rowCount)
        {
            if (rowCount <= 0)
            {
                return RowStepLoose;
            }

            float needed = SettingsRowFirstY + RowStepLoose * (rowCount - 1) + SettingsRowH;
            return needed <= SettingsPanelH ? RowStepLoose : RowStepTight;
        }

        /// <summary>音量を 0..1 にクランプし 0.1 刻みで±する (V5 OptionView と同じ数式)。</summary>
        public static float AdjustVolume(float value, int delta)
        {
            float shifted = value + delta * 0.1f;
            // 浮動小数の誤差で 0.3 + 0.1 が 0.40000001 になるのを避けて 10 倍整数で丸める。
            float rounded = (float)Math.Round(shifted * 10f) / 10f;
            if (rounded < 0f) return 0f;
            if (rounded > 1f) return 1f;
            return rounded;
        }

        /// <summary>音量の表示 (% 整数)。</summary>
        public static string FormatVolume(float value)
        {
            float clamped = value < 0f ? 0f : (value > 1f ? 1f : value);
            return (int)Math.Round(clamped * 100f) + "%";
        }

        /// <summary>NoteSpeed の表示 (F1)。</summary>
        public static string FormatSpeed(float value)
        {
            return value.ToString("F1");
        }

        /// <summary>FlashSpeed (GManager.FlashBG) の循環: 1 → 2 → 4 → 8 → 1。範囲外は 1 から。</summary>
        public static int CycleFlashSpeed(int current, int delta)
        {
            int[] steps = { 1, 2, 4, 8 };
            int index = Array.IndexOf(steps, current);
            if (index < 0)
            {
                index = 0;
            }

            return steps[(index + delta % steps.Length + steps.Length) % steps.Length];
        }

        /// <summary>キー入力をアクションに写像する (モーダル中は Confirm / Cancel だけを返す)。</summary>
        public static V6OptionAction MapKey(bool modalOpen, bool up, bool down, bool left, bool right,
            bool confirm, bool cancel, bool tabNext, bool tabPrev)
        {
            if (modalOpen)
            {
                if (cancel) return V6OptionAction.Cancel;
                if (confirm) return V6OptionAction.Confirm;
                return V6OptionAction.None;
            }

            if (up) return V6OptionAction.MoveUp;
            if (down) return V6OptionAction.MoveDown;
            if (left) return V6OptionAction.AdjustPrev;
            if (right) return V6OptionAction.AdjustNext;
            if (confirm) return V6OptionAction.Confirm;
            if (cancel) return V6OptionAction.Cancel;
            if (tabNext) return V6OptionAction.NextTab;
            if (tabPrev) return V6OptionAction.PrevTab;
            return V6OptionAction.None;
        }

        /// <summary>キーの表示名 (KeyConfigUI.GetKeyDisplayName と同じ対応表)。</summary>
        public static string KeyDisplayName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Alpha0: return "0";
                case KeyCode.Alpha1: return "1";
                case KeyCode.Alpha2: return "2";
                case KeyCode.Alpha3: return "3";
                case KeyCode.Alpha4: return "4";
                case KeyCode.Alpha5: return "5";
                case KeyCode.Alpha6: return "6";
                case KeyCode.Alpha7: return "7";
                case KeyCode.Alpha8: return "8";
                case KeyCode.Alpha9: return "9";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.Caret: return "^";
                case KeyCode.At: return "@";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Colon: return ":";
                case KeyCode.Backslash: return "\\";
                case KeyCode.BackQuote: return "`";
                case KeyCode.Slash: return "/";
                case KeyCode.Underscore: return "_";
                case KeyCode.Asterisk: return "*";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Less: return "<";
                case KeyCode.Greater: return ">";
                default: return key.ToString();
            }
        }

        /// <summary>
        /// key が割り当てられているレーン (1 始まりの表示レーン番号)。
        /// 割り当てが無ければ -1。table は KeyBindings.Current (レーン 0..7)。
        /// </summary>
        public static int DisplayLaneOf(KeyCode key, KeyCode[][] table)
        {
            if (table == null)
            {
                return -1;
            }

            for (int lane = 0; lane < table.Length; lane++)
            {
                KeyCode[] keys = table[lane];
                if (keys == null)
                {
                    continue;
                }

                for (int i = 0; i < keys.Length; i++)
                {
                    if (keys[i] == key)
                    {
                        return lane + 1;
                    }
                }
            }

            return -1;
        }

        // --- プレビューの JIS キーボード (06 Play design 実測) ---

        /// <summary>キーピッチ (34px 鍵 + 2px 間隔)。</summary>
        public const float GridKeyW = 34f;
        public const float GridKeyH = 40f;
        public const float GridKeyStepX = 36f;

        /// <summary>各段の y (design px) と先頭 x。</summary>
        public static readonly float[] GridRowY = { 356f, 400f, 444f, 488f };
        public static readonly float[] GridRowX = { 8f, 25f, 42f, 59f };

        /// <summary>JIS 配列の物理キー (上段から。design の Key 1..Key _ と同じ並び)。</summary>
        public static readonly KeyCode[][] JisGridKeys =
        {
            new[] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0, KeyCode.Minus, KeyCode.Caret, KeyCode.Backslash },
            new[] { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T, KeyCode.Y, KeyCode.U, KeyCode.I, KeyCode.O, KeyCode.P, KeyCode.At, KeyCode.LeftBracket },
            new[] { KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.G, KeyCode.H, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.Semicolon, KeyCode.Colon, KeyCode.RightBracket },
            new[] { KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V, KeyCode.B, KeyCode.N, KeyCode.M, KeyCode.Comma, KeyCode.Period, KeyCode.Slash, KeyCode.Underscore },
        };
    }
}
