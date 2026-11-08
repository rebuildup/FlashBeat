using System;
using FlashBeat.UI.V6.Select.View;
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
    /// Option 画面のキー入力 (名前付き)。bool の位置渡しで意味が読めない箇所を無くすため用意する。
    /// <see cref="V6OptionLogic.MapKey(V6OptionInput)"/> に渡す。
    /// </summary>
    public struct V6OptionInput
    {
        /// <summary>モーダル (確認ダイアログ等) が開いているか。</summary>
        public bool ModalOpen;
        /// <summary>↑ キー。</summary>
        public bool Up;
        /// <summary>↓ キー。</summary>
        public bool Down;
        /// <summary>← キー (値の減)。</summary>
        public bool Left;
        /// <summary>→ キー (値の増)。</summary>
        public bool Right;
        /// <summary>決定 (Enter / Space)。</summary>
        public bool Confirm;
        /// <summary>取り消し (Esc)。</summary>
        public bool Cancel;
        /// <summary>タブ切替 (Tab)。</summary>
        public bool TabNext;
        /// <summary>タブ切替 (Shift + Tab)。</summary>
        public bool TabPrev;
    }

    /// <summary>
    /// v6 Option 画面の純ロジック (EditMode テスト対象)。
    /// design v6 の 06 Play (B6If6Z) / 06 Display (EzLfb) / 06 Sound (z7joSy) / 06 Data (DQHHp)
    /// 共通フレーム 1728x972 基準。Unity API は KeyCode (列挙体) と数値計算のみに限定する。
    /// </summary>
    public static class V6OptionLogic
    {
        /// <summary>
        /// design v6 共通フレームの基準サイズ (1728x972)。
        /// 実体は他の v6 画面と同じ共通実装
        /// (<see cref="V6SelectViewLogic.DesignW"/> / <see cref="V6SelectViewLogic.DesignH"/>) を参照する。
        /// </summary>
        public const float DesignW = V6SelectViewLogic.DesignW;
        public const float DesignH = V6SelectViewLogic.DesignH;

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
        /// design フレーム (1728×972) を実画面へ <b>1:1 (等倍)</b> で中央配置するときの
        /// 左上 origin = ((w−1728)/2, (h−972)/2) (整数切り捨て)。
        /// 正本: design gaI3p "Resolution Check · Option" の実配置
        /// (FHD 1920×1080 → (96,54) / 2K 2560×1440 → (416,234) / Mac 2560×1664 → (416,346))。
        /// 旧実装の min(W/1728, H/972) による引き伸ばし (等比スケール) は廃止
        /// (Owner 指示: design フレームは固定解像度であり、拡大縮小は起こらない。
        /// 解像度ごとに origin だけが変わる)。異常値 (0 以下) は (0,0) にフォールバックする。
        /// </summary>
        public static Vector2 FrameOrigin(int screenW, int screenH)
        {
            if (screenW <= 0 || screenH <= 0)
            {
                return Vector2.zero;
            }

            return new Vector2(
                Mathf.Floor((screenW - DesignW) / 2f),
                Mathf.Floor((screenH - DesignH) / 2f));
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

        /// <summary>
        /// キー入力をアクションに写像する (モーダル中は Confirm / Cancel だけを返す)。
        ///
        /// bool の羅列なので引数の位置が意味を決める。位置は次のとおり:
        /// 1: modalOpen / 2: up / 3: down / 4: right / 5: left /
        /// 6: confirm / 7: tabNext / 8: tabPrev / 9: cancel。
        /// (4 は「右」= AdjustNext、9 は「モーダル中の Cancel」= Cancel。)
        /// 引数の位置を間違えやすい呼び出し側は <see cref="V6OptionInput"/> 版を使う。
        /// </summary>
        public static V6OptionAction MapKey(bool modalOpen, bool up, bool down, bool right, bool left,
            bool confirm, bool tabNext, bool tabPrev, bool cancel)
        {
            return MapKey(new V6OptionInput
            {
                ModalOpen = modalOpen,
                Up = up,
                Down = down,
                Left = left,
                Right = right,
                Confirm = confirm,
                Cancel = cancel,
                TabNext = tabNext,
                TabPrev = tabPrev,
            });
        }

        /// <summary>キー入力をアクションに写像する (名前付き引数版)。</summary>
        public static V6OptionAction MapKey(V6OptionInput input)
        {
            if (input.ModalOpen)
            {
                if (input.Cancel) return V6OptionAction.Cancel;
                if (input.Confirm) return V6OptionAction.Confirm;
                return V6OptionAction.None;
            }

            if (input.Up) return V6OptionAction.MoveUp;
            if (input.Down) return V6OptionAction.MoveDown;
            if (input.Right) return V6OptionAction.AdjustNext;
            if (input.Left) return V6OptionAction.AdjustPrev;
            if (input.Confirm) return V6OptionAction.Confirm;
            if (input.Cancel) return V6OptionAction.Cancel;
            if (input.TabNext) return V6OptionAction.NextTab;
            if (input.TabPrev) return V6OptionAction.PrevTab;
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
