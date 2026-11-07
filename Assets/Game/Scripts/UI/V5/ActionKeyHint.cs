using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// Formats the currently accepted action keys for small on-screen key caps.
    /// KeyActions is the single source of truth; labels must never be inferred
    /// from the physical keyboard layout or hard-coded action defaults.
    /// Naming rule: a cap shows the key that is actually pressed. Keypad keys
    /// are prefixed with "Num" (Keypad0 -> "Num0") so they stay distinguishable
    /// from the number row and fit the fixed 48px cap; symbol keys show their
    /// printed character.
    /// </summary>
    public static class ActionKeyHint
    {
        public static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.None: return "—";
                case KeyCode.Return:
                case KeyCode.KeypadEnter: return "Enter";
                case KeyCode.Escape: return "Esc";
                case KeyCode.Backspace: return "Backspace";
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
                // テンキーは数字キー列と区別するため "Num" 接頭辞を付ける。
                // Enum 名 ("Keypad0") は 7 文字で 48px のキャップに収まらない。
                case KeyCode.Keypad0: return "Num0";
                case KeyCode.Keypad1: return "Num1";
                case KeyCode.Keypad2: return "Num2";
                case KeyCode.Keypad3: return "Num3";
                case KeyCode.Keypad4: return "Num4";
                case KeyCode.Keypad5: return "Num5";
                case KeyCode.Keypad6: return "Num6";
                case KeyCode.Keypad7: return "Num7";
                case KeyCode.Keypad8: return "Num8";
                case KeyCode.Keypad9: return "Num9";
                case KeyCode.KeypadPlus: return "Num+";
                case KeyCode.KeypadMinus: return "Num-";
                case KeyCode.KeypadMultiply: return "Num*";
                case KeyCode.KeypadDivide: return "Num/";
                case KeyCode.KeypadPeriod: return "Num.";
                case KeyCode.KeypadEquals: return "Num=";
                // 記号キーは印字面 (JIS 配列) を優先する。
                case KeyCode.Quote: return "'";
                case KeyCode.Tilde: return "~";
                default: return KeyGuideUI.GetKeyDisplayName(key);
            }
        }

        /// <summary>
        /// The first usable key for a narrow button cap. Other accepted keys
        /// remain visible in the complete key configuration UI and All().
        /// </summary>
        public static string Primary(KeyActions.KeyAction action)
        {
            KeyCode[] keys = KeyActions.Get(action);
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] == KeyCode.None) continue;
                return keys[i] == KeyCode.Backspace ? "BS" : KeyName(keys[i]);
            }
            return "—";
        }

        /// <summary>
        /// All accepted keys, in configured order. Enter and keypad Enter are
        /// represented by one readable name rather than two identical labels.
        /// </summary>
        public static string All(KeyActions.KeyAction action)
        {
            KeyCode[] keys = KeyActions.Get(action);
            var labels = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] == KeyCode.None) continue;
                string name = KeyName(keys[i]);
                if (seen.Add(name)) labels.Add(name);
            }
            return labels.Count == 0 ? "—" : string.Join(" / ", labels.ToArray());
        }
    }
}
