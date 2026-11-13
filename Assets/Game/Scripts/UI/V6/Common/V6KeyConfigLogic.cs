using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>待受中に押されたキーの判定結果。</summary>
    public enum V6KeyAssignResult
    {
        /// <summary>代表キーとして確定した (待受は終わる)。</summary>
        Assigned,

        /// <summary>予約キー/修飾キーなので受け付けない (待受は続く)。</summary>
        Reserved,

        /// <summary>他のレーンが使用中なので確定できない (待受は続く)。</summary>
        Conflict,

        /// <summary>待受中ではないので何もしない。</summary>
        Ignored,
    }

    /// <summary>
    /// キー設定モーダル (design <c>vo4kB</c>、設計判断
    /// <c>docs/design/v6/review-2026-11-11-decisions.md</c> §4) の編集モデル。
    ///
    /// 8 レーンの代表キー (配列の先頭) を差し替える。レーンには 1 つとは限らず、
    /// 残りのキーは「他: Q / A / Z」として保持する。確定は「適用」まで行わず、
    /// <see cref="Commit"/> で <see cref="KeyBindings"/> へ一度に書き込む。
    ///
    /// UI から独立した純粋なモデルなので、待受・衝突・既定復帰は EditMode で検証できる。
    /// 入力の受け取り (どのキーが押されたかの検出) はビュー側 <see cref="V6KeyConfigModal"/>。
    /// </summary>
    public sealed class V6KeyConfigLogic
    {
        /// <summary>レーン数 (KeyBindings の既定表と同じ 8)。</summary>
        public const int LaneCount = 8;

        /// <summary>待受中の状態表示 (design §4.2)。</summary>
        public const string ListeningText = "キーを押してください";

        /// <summary>予約キーを押したときの状態表示。</summary>
        public const string ReservedText = "このキーは使用できません";

        private readonly KeyCode[][] working;
        private readonly KeyCode[][] original;
        private bool listening;
        private bool reservedNotice;
        private int conflictLane = -1;

        /// <summary>編集中の表を指定して作る。null なら既定表から始める。</summary>
        public V6KeyConfigLogic(KeyCode[][] source)
        {
            KeyCode[][] from = (source != null && source.Length == LaneCount) ? source : KeyBindings.DefaultKeys;
            working = Clone(from);
            original = Clone(from);
        }

        /// <summary>選択中のレーン (0..7)。</summary>
        public int SelectedLane { get; private set; }

        /// <summary>待受中か (次のキー 1 つを代表キーに割り当てる)。</summary>
        public bool Listening
        {
            get { return listening; }
        }

        /// <summary>直近の衝突先レーン (0..7、無ければ -1)。</summary>
        public int ConflictLane
        {
            get { return conflictLane; }
        }

        /// <summary>レーンの代表キー (先頭)。</summary>
        public KeyCode Representative(int lane)
        {
            KeyCode[] keys = LaneKeys(lane);
            return keys.Length > 0 ? keys[0] : KeyCode.None;
        }

        /// <summary>代表キー以外のキー。</summary>
        public KeyCode[] Alternates(int lane)
        {
            KeyCode[] keys = LaneKeys(lane);
            if (keys.Length <= 1)
            {
                return new KeyCode[0];
            }

            var rest = new KeyCode[keys.Length - 1];
            Array.Copy(keys, 1, rest, 0, rest.Length);
            return rest;
        }

        /// <summary>行の 2 行目 (design: 「他: Q / A / Z」)。残りが無ければ空。</summary>
        public string AlternatesText(int lane)
        {
            KeyCode[] rest = Alternates(lane);
            if (rest.Length == 0)
            {
                return string.Empty;
            }

            var text = new StringBuilder("他: ");
            for (int i = 0; i < rest.Length; i++)
            {
                if (i > 0)
                {
                    text.Append(" / ");
                }

                text.Append(KeyLabel(rest[i]));
            }

            return text.ToString();
        }

        /// <summary>行の状態テキスト (design §4.2)。</summary>
        public string StateText(int lane)
        {
            if (listening && lane == SelectedLane)
            {
                if (conflictLane >= 0)
                {
                    return "Lane " + (conflictLane + 1) + " で使用中";
                }

                return reservedNotice ? ReservedText : ListeningText;
            }

            return lane == SelectedLane ? "Enter で変更" : "変更";
        }

        /// <summary>選択行を動かす (↑↓ / ←→ は行移動。端では折り返す)。</summary>
        public void Move(int delta)
        {
            if (delta == 0)
            {
                return;
            }

            int next = (SelectedLane + delta) % LaneCount;
            if (next < 0)
            {
                next += LaneCount;
            }

            SelectedLane = next;
            conflictLane = -1;
            reservedNotice = false;
        }

        /// <summary>選択行を指定する (マウスクリック)。</summary>
        public void Select(int lane)
        {
            if (lane < 0 || lane >= LaneCount)
            {
                return;
            }

            SelectedLane = lane;
            conflictLane = -1;
            reservedNotice = false;
        }

        /// <summary>待受を始める (Enter / 行クリック)。</summary>
        public void BeginListen()
        {
            listening = true;
            conflictLane = -1;
            reservedNotice = false;
        }

        /// <summary>待受を中止する (Esc)。編集内容はそのまま (行の選択へ戻る)。</summary>
        public void CancelListen()
        {
            listening = false;
            conflictLane = -1;
            reservedNotice = false;
        }

        /// <summary>
        /// 押されたキーを代表キーへ割り当てる。予約キー・修飾キーは受け付けず、
        /// 他レーンが使用中のキーは確定せず衝突先を記録する (どちらも待受は続く)。
        /// </summary>
        public V6KeyAssignResult Assign(KeyCode key)
        {
            if (!listening)
            {
                return V6KeyAssignResult.Ignored;
            }

            if (key == KeyCode.None || KeyBindings.IsReserved(key) || IsModifier(key))
            {
                conflictLane = -1;
                reservedNotice = true; // 「このキーは使用できません」を出す
                return V6KeyAssignResult.Reserved;
            }

            int other = LaneOf(key);
            if (other >= 0 && other != SelectedLane)
            {
                conflictLane = other;
                reservedNotice = false;
                return V6KeyAssignResult.Conflict;
            }

            KeyCode[] keys = LaneKeys(SelectedLane);
            var next = new List<KeyCode>(keys.Length + 1) { key };
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] != key)
                {
                    next.Add(keys[i]); // 旧代表キーは残りキーへ降りる (割り当てを失わない)
                }
            }

            working[SelectedLane] = next.ToArray();
            listening = false;
            conflictLane = -1;
            reservedNotice = false;
            return V6KeyAssignResult.Assigned;
        }

        /// <summary>編集内容を既定表へ戻す (保存は「適用」まで行わない)。</summary>
        public void ResetToDefaults()
        {
            KeyCode[][] defaults = KeyBindings.DefaultKeys;
            for (int lane = 0; lane < LaneCount; lane++)
            {
                working[lane] = CloneLane(defaults[lane]);
            }

            listening = false;
            conflictLane = -1;
            reservedNotice = false;
        }

        /// <summary>編集内容が読み込み時と違うか (キャンセル/適用の判断に使う)。</summary>
        public bool HasChanges()
        {
            for (int lane = 0; lane < LaneCount; lane++)
            {
                if (working[lane].Length != original[lane].Length)
                {
                    return true;
                }

                for (int i = 0; i < working[lane].Length; i++)
                {
                    if (working[lane][i] != original[lane][i])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>編集中の表の写し (テストと保存先の確認用)。</summary>
        public KeyCode[][] Snapshot()
        {
            return Clone(working);
        }

        /// <summary>編集内容を <see cref="KeyBindings"/> へ書き込み、保存する。</summary>
        /// <remarks>
        /// 保存に失敗したら、適用済みの割り当ても元へ戻す (PR #387 レビュー指摘)。
        /// <see cref="KeyBindings.SetAll"/> は保存より先に実行中の表を書き換えて
        /// <c>Changed</c> を通知するので、そのまま失敗を返すと「適用できていないのに
        /// 判定キーだけ変わっている」状態になる。キャンセルでも戻せない。
        /// </remarks>
        public bool Commit()
        {
            KeyCode[][] previous = KeyBindings.Current;
            var restore = new KeyCode[LaneCount][];
            for (int lane = 0; lane < LaneCount; lane++)
            {
                restore[lane] = CloneLane(previous != null && lane < previous.Length ? previous[lane] : null);
            }

            if (!KeyBindings.SetAll(working))
            {
                return false;
            }

            if (!KeyBindings.Save())
            {
                // 保存できなかった: 実行中の割り当てを保存済みの状態へ戻し、
                // キーガイドと判定の表示も追随させる (SetAll が Changed を出す)。
                KeyBindings.SetAll(restore);
                return false;
            }

            return true;
        }

        /// <summary>キーの表示名 (design の KeyCap)。</summary>
        public static string KeyLabel(KeyCode key)
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

        /// <summary>レーン操作に使うキー (Shift / Ctrl / Alt 単体) は代表キーにできない。</summary>
        public static bool IsModifier(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                case KeyCode.LeftCommand:
                case KeyCode.LeftWindows:
                case KeyCode.RightCommand:
                case KeyCode.RightWindows:
                case KeyCode.CapsLock:
                case KeyCode.Numlock:
                case KeyCode.ScrollLock:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// キーが現在の編集表のどのレーンに割り当てられているか
        /// (Issue #428: キーボード表示のキーキャップをクリックしてレーン選択するため)。
        /// 未割当なら -1。
        /// </summary>
        public int LaneOfKey(KeyCode key)
        {
            return LaneOf(key);
        }

        private int LaneOf(KeyCode key)
        {
            for (int lane = 0; lane < LaneCount; lane++)
            {
                KeyCode[] keys = working[lane];
                for (int i = 0; i < keys.Length; i++)
                {
                    if (keys[i] == key)
                    {
                        return lane;
                    }
                }
            }

            return -1;
        }

        private KeyCode[] LaneKeys(int lane)
        {
            if (lane < 0 || lane >= LaneCount)
            {
                return new KeyCode[0];
            }

            return working[lane];
        }

        private static KeyCode[][] Clone(KeyCode[][] table)
        {
            var copy = new KeyCode[LaneCount][];
            for (int lane = 0; lane < LaneCount; lane++)
            {
                copy[lane] = CloneLane(table[lane]);
            }

            return copy;
        }

        private static KeyCode[] CloneLane(KeyCode[] lane)
        {
            if (lane == null)
            {
                return new KeyCode[0];
            }

            var copy = new KeyCode[lane.Length];
            Array.Copy(lane, copy, lane.Length);
            return copy;
        }
    }
}
