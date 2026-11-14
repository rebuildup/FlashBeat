using System;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.UI.V5;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// v6 の「押せる面」(uGUI Button) を組み立てる共通ヘルパ (Issue #385 phase 1.5)。
    ///
    /// V6Layout が座標・寸法、V6Hierarchy が階層操作を受け持つのに対し、
    /// ここは Button コンポーネントの結線だけを 1 箇所に集約する:
    /// transition / navigation の無効化、押下フィードバック
    /// (<see cref="V6ButtonPressFeedback"/>)、onClick の接続。
    ///
    /// 含めないもの (各 View に残す):
    /// - Title の MakeButton は「既存 Button を再利用し、画面遷移を
    ///   <see cref="V6ButtonPressFeedback.RunAfterFeedback"/> で遅らせる」
    ///   別仕様 (GetComponent-or-add + delayed action) なので共通化しない。
    /// </summary>
    public static class V6Button
    {
        /// <summary>
        /// Button を追加して操作可能にする (transition/nav None + 押下リング + onClick)。
        /// AddComponent なので、既に Button が付いている GameObject には
        /// 2 個目が載る (旧来の各 View の private MakeButton / MakeViewButton と同じ)。
        /// </summary>
        public static GameObject Make(GameObject go, Action onClick)
        {
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            V6ButtonPressFeedback.Attach(go);
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            return go;
        }

        /// <summary>
        /// V6Controls 等が作った既存 Button を操作可能にする
        /// (transition/nav None + onClick。押下リングは付けない。
        /// Ranking / History の private ConfigureButton と同じ仕様)。
        /// </summary>
        public static void Configure(GameObject go, Action onClick)
        {
            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }
        }

        /// <summary>V5Ui.Panel で作った面に Button を載せる (Select / Result の private Button)。</summary>
        public static GameObject Panel(GameObject parent, string name, Vector2 size, Color bg, float radius,
            Action onClick)
        {
            var go = V5Ui.Panel(parent, name, size, bg, radius);
            return Make(go, onClick);
        }

        /// <summary><see cref="Panel(GameObject, string, Vector2, Color, float, Action)"/> の Component 版。</summary>
        public static GameObject Panel(Component parent, string name, Vector2 size, Color bg, float radius,
            Action onClick)
        {
            var go = V5Ui.Panel(parent.gameObject, name, size, bg, radius);
            return Make(go, onClick);
        }
    }
}
