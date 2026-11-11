using System;
using FlashBeat.UI.V6.Common;
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

        // --- design ジオメトリ (design B6If6Z パッチ実測、Main ゾーン (24,96) 内のローカル座標) ---

        /// <summary>タブ帯 (design C0piZz): メイン (438,0) 824x48、タブ 200x48 gap8。</summary>
        public const float TabsX = 438f, TabsY = 0f, TabsW = 824f, TabsH = 48f, TabW = 200f, TabGap = 8f;

        /// <summary>設定パネル (design crVgG): メイン (304,68) 512x688。行 (8, 60+step*k) 496x56。</summary>
        public const float SetX = 304f, SetY = 68f, SetW = 512f;
        public const float SetH = SettingsPanelH;
        public const float SetRowX = 8f, SetRowW = 496f;

        /// <summary>プレビューパネル (design cqQMc): メイン (864,68) 512x688。</summary>
        public const float PrevX = 864f, PrevY = 68f, PrevW = 512f, PrevH = 688f;

        /// <summary>Game Preview (design 共通): プレビューパネル内 (24,60) 464x280 $k0 r8。</summary>
        public const float GamePrevX = 24f, GamePrevY = 60f, GamePrevW = 464f, GamePrevH = 280f;

        /// <summary>
        /// 説明ブロック (design): Divider (24,Y) / Desc Title (24,Y+20) fs32 / Desc (24,Y+70) w464。
        /// Y はタブごとに違う — Play 544 / Display 456 / Sound 472 / Data 380
        /// (r23 で 4 タブを撮ったところ、旧実装は Play の 544 を全タブへ流用しており、
        /// Display / Sound / Data で説明文が design より 88〜234px 下に落ちていた)。
        /// </summary>
        public const float PlayDescDividerY = 544f, DisplayDescDividerY = 456f;
        /// <summary>
        /// Sound は 472 だったが、音量スライダ (Issue #375、値 fs58 + トラック +
        /// ヒント = 156px) を Game Preview (60..340) の直下に置くため 504 へ下げた
        /// (design 更新は pen `z7joSy` に反映)。
        /// </summary>
        public const float SoundDescDividerY = 504f, DataDescDividerY = 380f;
        public const float DescTitleOffsetY = 20f, DescOffsetY = 70f;
        public const float DescW = 464f, DescH = 68f;

        /// <summary>旧名 (Play タブの値)。しきい値の互換のため残す。</summary>
        public const float DescDividerY = PlayDescDividerY;
        public const float DescTitleY = PlayDescDividerY + DescTitleOffsetY;
        public const float DescY = PlayDescDividerY + DescOffsetY;

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

        // --- Display 右パネル: 判定色の凡例 (design EzLfb Judge PERFECT..MISS) ---
        // チップ (24,360) 106x64 r8 $k1 を 118 ピッチ (gap12) で 4 個。中身は
        // Marker (37,16) 32x4 r2 (判定色) + Label (fs18 $text-dim, 中央, y22 h26)。
        public const float JudgeChipX = 24f, JudgeChipY = 360f;
        public const float JudgeChipW = 106f, JudgeChipH = 64f, JudgeChipStep = 118f;
        public const float JudgeMarkerX = 37f, JudgeMarkerY = 16f;
        public const float JudgeMarkerW = 32f, JudgeMarkerH = 4f;
        public static readonly string[] JudgeLabels = { "PERFECT", "GREAT", "BAD", "MISS" };

        // --- Sound 右パネル: 音量の値とトラック (design z7joSy) ---
        // Value (24,360) 106x58 fs40 / Track (24,420) 464x20 r4 $k1 + Fill $k6。
        public const float VolumeValueX = 24f, VolumeValueY = 360f;
        public const float VolumeValueW = 106f, VolumeValueH = 58f;
        public const float VolumeTrackX = 24f, VolumeTrackY = 420f;
        public const float VolumeTrackW = 464f, VolumeTrackH = 20f;

        /// <summary>音量 (%) に対する Fill 幅。design の 100% = 464 (トラック全幅)。</summary>
        public static float VolumeFillWidth(int volumePercent)
        {
            int clamped = volumePercent < 0 ? 0 : (volumePercent > 100 ? 100 : volumePercent);
            return VolumeTrackW * (clamped / 100f);
        }

        /// <summary>
        /// フッター キー凡例 (design パッチ r4IJx): フッター内 (0,30)、高さ 32、項目間 gap24。
        /// 項内は KeyCap (h32 pad[0,8]) + Label (fs22) を gap8 で並べる。
        /// </summary>
        public const float LegendY = 30f, LegendH = 32f, LegendGap = 24f;

        /// <summary>i 番目のタブの design 左上座標 (Main ゾーン基準)。</summary>
        public static Vector2 TabTopLeft(int index)
        {
            return new Vector2(TabsX + (TabW + TabGap) * index, TabsY);
        }

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

        // --- Sound 右パネル: 音量スライダ (design o0zh7 J7tBYP、Issue #375) ---
        // 部品の寸法・写像は V6VolumeSlider が持つ (Option と Pause で同じ部品)。
        // ここは置き場所 (プレビューパネル内の左上) だけを持つ。
        public const float VolumeSliderX = 24f, VolumeSliderY = 344f;

        /// <summary>音量を 0..1 にクランプし 0.1 刻みで±する (旧ステッパー。互換のため残す)。</summary>
        public static float AdjustVolume(float value, int delta)
        {
            return V6VolumeSlider.Nudge(value, delta, true);
        }

        /// <summary>
        /// キー / ステッパー 1 回分の値変更 (Issue #375 決定 §3.2)。
        /// 通常は 1%、Shift 併用で 10%。スライダーのドラッグと同じ値モデルを編集する。
        /// </summary>
        public static float NudgeVolume(float value, int delta, bool shift)
        {
            return V6VolumeSlider.Nudge(value, delta, shift);
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

        /// <summary>キーの表示名 (表は <see cref="V6KeyConfigLogic.KeyLabel"/> に一本化)。</summary>
        public static string KeyDisplayName(KeyCode key)
        {
            return V6KeyConfigLogic.KeyLabel(key);
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
