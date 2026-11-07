using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// ショートカットキーを持つ操作に「キーボード入力だと分かる表示」(キーキャップ) を付ける。
    ///
    /// - 見た目は Prefab/KeyCap 相当 (bg Key4, 角丸 4, ラベル fs_s muted)。
    /// - 読みやすい Shortcut 表記: Space / Esc / Tab / Enter / ← → ↑ ↓ / F2 / BS ...
    /// - 割当は <see cref="KeyActions"/> の保存値から取るので、キー設定を変えると表示も追従する。
    ///
    /// 係る場所:
    /// - v5 の各ビュー (Title / Select / Result / Option / Pause) のボタン
    /// - コード生成のオーバーレイ (KeyGuideUI / KeyConfigUI / RankingPanel)
    /// </summary>
    public static class ShortcutKeyCap
    {
        /// <summary>キャップの高さ (px)。</summary>
        public const float CapHeight = 34f;
        /// <summary>キャップの左右余白 (px)。</summary>
        public const float CapPadX = 10f;
        /// <summary>キャップと対象ラベルの間隔 (px)。</summary>
        public const float Gap = 8f;

        private static readonly Dictionary<KeyCode, string> names = new Dictionary<KeyCode, string>
        {
            { KeyCode.Space, "Space" },
            { KeyCode.Escape, "Esc" },
            { KeyCode.Tab, "Tab" },
            { KeyCode.Return, "Enter" },
            { KeyCode.KeypadEnter, "Enter" },
            { KeyCode.Backspace, "BS" },
            { KeyCode.Delete, "Del" },
            { KeyCode.Insert, "Ins" },
            { KeyCode.Home, "Home" },
            { KeyCode.End, "End" },
            { KeyCode.PageUp, "PgUp" },
            { KeyCode.PageDown, "PgDn" },
            { KeyCode.LeftArrow, "←" },
            { KeyCode.RightArrow, "→" },
            { KeyCode.UpArrow, "↑" },
            { KeyCode.DownArrow, "↓" },
            { KeyCode.LeftShift, "LShift" },
            { KeyCode.RightShift, "RShift" },
            { KeyCode.LeftControl, "LCtrl" },
            { KeyCode.RightControl, "RCtrl" },
            { KeyCode.LeftAlt, "LAlt" },
            { KeyCode.RightAlt, "RAlt" },
        };

        /// <summary>キーを表示用の短い名前にする。</summary>
        public static string KeyName(KeyCode key)
        {
            if (key == KeyCode.None)
            {
                return string.Empty;
            }

            if (names.TryGetValue(key, out var named))
            {
                return named;
            }

            if (key >= KeyCode.A && key <= KeyCode.Z)
            {
                return ((char)('A' + (key - KeyCode.A))).ToString();
            }

            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            {
                return ((char)('0' + (key - KeyCode.Alpha0))).ToString();
            }

            if (key >= KeyCode.F1 && key <= KeyCode.F12)
            {
                return "F" + (key - KeyCode.F1 + 1).ToString();
            }

            return key.ToString();
        }

        /// <summary>キー配列の表示文字列 ("Space / Enter")。空なら空文字。重複はまとめる。</summary>
        public static string KeysText(KeyCode[] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder();
            var seen = new HashSet<string>();
            for (int i = 0; i < keys.Length; i++)
            {
                string n = KeyName(keys[i]);
                if (string.IsNullOrEmpty(n) || !seen.Add(n))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(" / ");
                }

                sb.Append(n);
            }

            return sb.ToString();
        }

        /// <summary>操作の主キーの表示名 (複数割り当てのとき最初の 1 つ)。</summary>
        public static string PrimaryText(KeyActions.KeyAction action)
        {
            var keys = KeyActions.Get(action);
            return keys != null && keys.Length > 0 ? KeyName(keys[0]) : string.Empty;
        }

        /// <summary>KeyActions の現在割当の表示文字列。</summary>
        public static string ActionText(KeyActions.KeyAction action)
        {
            return KeysText(KeyActions.Get(action));
        }

        /// <summary>
        /// キーキャップ (角丸背景 + キー文字) を作る。parent の子として生成し、
        /// RectTransform は呼び出し側で配置する。
        /// </summary>
        public static GameObject Create(RectTransform parent, string label, float fontSize)
        {
            var go = V5Ui.Panel(parent.gameObject, "KeyCap " + label, new Vector2(10f, CapHeight),
                V5Tokens.ControlBg, V5Tokens.RadiusSmall);
            // キャップがクリックを奪わないようにする (親のボタンの操作を邪魔しない)。
            go.GetComponent<Image>().raycastTarget = false;
            var tmp = V5Ui.Text(go, "Key", label, fontSize, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            var rt = tmp.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 文字幅からキャップ幅を決める (レイアウト前でも破綻しない)。
            float textW = tmp.GetPreferredValues(label, 1000f, 1000f).x;
            var capRt = go.GetComponent<RectTransform>();
            capRt.sizeDelta = new Vector2(textW + CapPadX * 2f, CapHeight);
            return go;
        }

        /// <summary>
        /// 操作ボタンの内側右端にキーキャップを置く (右端から inset 分だけ内側)。
        /// 既にキャップがある場合は置き換える。
        /// </summary>
        public static GameObject AttachToControl(RectTransform control, string keysText, float fontSize,
            float rightInset = 16f)
        {
            if (control == null || string.IsNullOrEmpty(keysText))
            {
                return null;
            }

            RemoveExisting(control);
            var cap = Create(control, keysText, fontSize);
            var rt = cap.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-rightInset, 0f);
            return cap;
        }

        /// <summary>KeyActions の操作に対応するキャップを操作ボタンに置く。</summary>
        public static GameObject AttachActionToControl(RectTransform control, KeyActions.KeyAction action,
            float fontSize, float rightInset = 16f)
        {
            return AttachToControl(control, ActionText(action), fontSize, rightInset);
        }

        /// <summary>
        /// ラベルのすぐ右にキーキャップを置く (テキストの右端に続けて並ぶ)。
        /// 「(Tab で開閉)」のような文章ではなく [Tab] キャップで示すときに使う。
        /// ラベルの水平揃え (左/中央/右) に合わせてキャップの位置を決める。
        /// </summary>
        public static GameObject AttachToLabel(TextMeshProUGUI label, string keysText, float fontSize)
        {
            var caps = AttachCapsToLabel(label, new[] { keysText }, fontSize);
            return caps != null && caps.Length > 0 ? caps[0] : null;
        }

        /// <summary>
        /// ラベルの横にキーキャップを複数並べる。左揃えならテキストの右へ、
        /// 中央揃えなら行全体の右へ、右揃えならテキストの左へ置く。
        /// </summary>
        public static GameObject[] AttachCapsToLabel(TextMeshProUGUI label, string[] capTexts, float fontSize)
        {
            if (label == null || capTexts == null || capTexts.Length == 0)
            {
                return null;
            }

            var parent = label.rectTransform;
            RemoveExisting(parent);

            float textW = label.GetPreferredValues(label.text, 1000f, 1000f).x;
            var caps = new List<GameObject>();
            foreach (var t in capTexts)
            {
                if (string.IsNullOrEmpty(t))
                {
                    continue;
                }

                caps.Add(Create(parent, t, fontSize));
            }

            if (caps.Count == 0)
            {
                return caps.ToArray();
            }

            float capsW = 0f;
            foreach (var cap in caps)
            {
                capsW += cap.GetComponent<RectTransform>().sizeDelta.x + Gap;
            }

            capsW -= Gap; // 最後のキャップの後ろの余白は要らない

            // TMP の水平揃え: Top / Center / Bottom は「中央揃え」,
            // TopRight / Right / BottomRight は「右揃え」。
            bool centered = (label.alignment & (TextAlignmentOptions.Top | TextAlignmentOptions.Center
                | TextAlignmentOptions.Bottom)) != 0;
            bool right = !centered && (label.alignment & (TextAlignmentOptions.TopRight
                | TextAlignmentOptions.Right | TextAlignmentOptions.BottomRight)) != 0;

            float cursor;
            float anchorX;
            float pivotX;
            if (centered)
            {
                // 行全体 (テキスト + キャップ) を中央寄せの基準線から左に伸ばす。
                float rowW = textW + Gap + capsW;
                anchorX = 0.5f;
                pivotX = 0f;
                cursor = -rowW * 0.5f + textW + Gap;
            }
            else if (right)
            {
                anchorX = 1f;
                pivotX = 0f;
                cursor = -(textW + Gap + capsW);
            }
            else
            {
                anchorX = 0f;
                pivotX = 0f;
                cursor = textW + Gap;
            }

            foreach (var cap in caps)
            {
                var rt = cap.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(anchorX, 0.5f);
                rt.anchorMax = new Vector2(anchorX, 0.5f);
                rt.pivot = new Vector2(pivotX, 0.5f);
                rt.anchoredPosition = new Vector2(cursor, 0f);
                cursor += rt.sizeDelta.x + Gap;
            }

            return caps.ToArray();
        }

        /// <summary>
        /// (キャップ, 説明) の並びを横一列で作る。footer のような説明文を
        /// 「←→ / PageUp: ページ切替」の羅列から、キーキャップ付きの見取りやすい
        /// 並びに変えるときに使う。
        /// </summary>
        public static GameObject BuildCapRow(RectTransform parent, (string cap, string label)[] items,
            float capFontSize, float labelFontSize)
        {
            var row = V5Ui.Node(parent.gameObject, "Cap Row");
            var rowRt = row.GetComponent<RectTransform>();
            // 縦レイアウト (VerticalLayoutGroup) に入れても高さを持つようにしておく。
            row.AddComponent<LayoutElement>();
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(item.cap))
                {
                    var cap = Create(rowRt, item.cap, capFontSize);
                    var capLe = cap.AddComponent<LayoutElement>();
                    capLe.preferredWidth = cap.GetComponent<RectTransform>().sizeDelta.x;
                    capLe.preferredHeight = CapHeight;
                    capLe.minHeight = CapHeight;
                }

                if (string.IsNullOrEmpty(item.label))
                {
                    continue;
                }

                var label = V5Ui.Text(row, "Label " + item.label, item.label, labelFontSize, V5Tokens.TextMuted);
                var labelLe = label.gameObject.AddComponent<LayoutElement>();
                labelLe.preferredWidth = label.preferredWidth + 4f;
                labelLe.preferredHeight = labelFontSize + 6f;
            }

            return row;
        }

        /// <summary>既に付いているキャップを消す (付け替え用)。</summary>
        public static void RemoveExisting(RectTransform parent)
        {
            if (parent == null)
            {
                return;
            }

            var stale = new List<GameObject>();
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name.StartsWith("KeyCap"))
                {
                    stale.Add(child.gameObject);
                }
            }

            foreach (var go in stale)
            {
                // EditMode (テスト) では DestroyImmediate でないと破棄できない。
                if (Application.isPlaying)
                {
                    Object.Destroy(go);
                }
                else
                {
                    Object.DestroyImmediate(go);
                }
            }
        }
    }
}
