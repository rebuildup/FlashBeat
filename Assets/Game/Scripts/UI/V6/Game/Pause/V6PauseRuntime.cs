using UnityEngine;
using UnityEngine.SceneManagement;
using FlashBeat.UI.V6.Game.Hud;

namespace FlashBeat.UI.V6.Game.Pause
{
    /// <summary>
    /// v6 ポーズ (Issue #224) の統合配線。
    ///
    /// Esc での開閉と矢印 / タブ操作は <see cref="V6GameRuntime"/>.UpdatePause が担い、
    /// 開閉状態の正本は <see cref="V6PauseView.IsOpen"/> (自分の状態)。
    /// GameSceneManager.Menu はレガシー歌詞パネルのフラグなので読まない・書かない
    /// (かつてここで毎フレーム Menu を書き戻しており、歌詞パネルの状態と混線していた)。
    /// ここはその外側の 1 点:
    /// - v6 HUD のメニューボタン (右下の Esc キャップ) を v6 ポーズへ接続する。
    ///   ボタンの既存リスナー (V6GameHudView.OpenPauseMenu) は v6 ポーズ有効時は
    ///   何もしないので、ここに v6 用のリスナーを追加する。
    ///
    /// V6UiMode.GameHud が無効のときは何もしない (v5 が従来どおり全部を担う)。
    /// v5 の PauseMenuView は V6GameRuntime.LateUpdate が毎フレーム非表示化する
    /// (Update が走らないので二重入力にならない)。破棄はしない: GameHudController が
    /// 毎フレーム参照していて (pause.InputEnabled / pause.Close)、破棄するとその経路で
    /// MissingReferenceException になる。
    /// </summary>
    public sealed class V6PauseRuntime : MonoBehaviour
    {
        private const string GameSceneName = "GameScene";

        private static bool subscribed;

        /// <summary>HUD のメニューボタンへの配線が済んだか (1 回だけ)。</summary>
        private bool wired;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!subscribed)
            {
                subscribed = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            if (SceneManager.GetActiveScene().name == GameSceneName) Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == GameSceneName) Ensure();
        }

        private static void Ensure()
        {
            if (!V6UiMode.GameHud || !V6PauseView.Enabled) return;
            if (FindObjectOfType<V6PauseRuntime>() != null) return;

            var go = new GameObject("V6PauseRuntime");
            go.AddComponent<V6PauseRuntime>();
        }

        private void Update()
        {
            if (!wired)
            {
                TryWireHudButton();
            }
        }

        /// <summary>v6 HUD のメニューボタン (Pause ボタン) を v6 ポーズへ接続する。</summary>
        private void TryWireHudButton()
        {
            var hud = FindObjectOfType<V6GameHudView>();
            if (hud == null || hud.MenuButton == null) return;

            wired = true;
            hud.MenuButton.onClick.AddListener(OpenPause);
        }

        /// <summary>HUD の Pause ボタンから開く (Esc と同じ経路へ状態を揃える)。</summary>
        private static void OpenPause()
        {
            V6PauseView pause = V6PauseView.Current;
            if (pause == null || pause.IsOpen) return;

            pause.Open();
        }
    }
}
