using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlashBeat.UI.V5;

namespace FlashBeat.UI.V6.Game.Playfield
{
    /// <summary>
    /// GameScene で v6 プレイフィールド (<see cref="V6PlayfieldView"/>) を起動する
    /// (Issue #219)。v5 の <c>GameHudRuntime</c> と同じ RuntimeInitializeOnLoad 方式で、
    /// シーンアセットは触らない。
    ///
    /// Canvas は v5 HUD (order -1) と歌詞 (legacy order 0) より下の order -2。
    /// 動画 (ワールド描画) より常に手前に来る。
    /// </summary>
    public static class V6PlayfieldRuntime
    {
        private const string GameSceneName = "GameScene";
        private const int SortingOrder = -2;

        private static bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!subscribed)
            {
                subscribed = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            if (SceneManager.GetActiveScene().name == GameSceneName)
            {
                Ensure();
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == GameSceneName)
            {
                Ensure();
            }
        }

        private static void Ensure()
        {
            // 既存の 3D レーンをそのまま使う (既定)。v6 の远近法プレイフィールドを
            // 使うのは明示的にオンにしたときだけ。
            if (!FlashBeat.UI.V6.V6UiMode.Playfield) return;
            if (V6PlayfieldView.Current != null)
            {
                return;
            }

            var canvasGo = new GameObject("V6PlayfieldCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            // Constant Pixel Size x1 + 解像度変化の再適用は v5 と共通 (Configure は order を
            // 500 に上書きするので戻す)。
            CanvasConfigurator.Configure(canvas);
            canvas.sortingOrder = SortingOrder;

            V6PlayfieldView.Create(canvasGo.GetComponent<RectTransform>());
        }
    }
}
