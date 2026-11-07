using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// Formats the currently accepted action keys for small on-screen key caps.
    /// KeyActions is the single source of truth; labels must never be inferred
    /// from the physical keyboard layout or hard-coded action defaults.
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
