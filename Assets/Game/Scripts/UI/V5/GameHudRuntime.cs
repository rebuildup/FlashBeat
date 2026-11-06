using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// GameScene の v5 UI の起動と状態配線 (Issue #122)。
    ///
    /// - GameScene がロードされたら <see cref="GameHudView"/>, <see cref="GameReadyView"/>,
    ///   <see cref="GameLoadingView"/>, <see cref="GameErrorView"/>, <see cref="PauseMenuView"/>
    ///   をコードから構築し、レガシーの同機能 UI を隠す。
    /// - 再生パイプラインの状態は <see cref="HudPlayState"/> (Issue #93 の純ロジック) で判定し、
    ///   Loading / Error / Ready の各ビューへ配る。
    /// - ポーズは Esc で開閉 (PauseMenuView)、HUD 右下のボタンからも開ける。
    ///
    /// MusicManager / Judge は所有外なので変更しない。観測は公開状態と
    /// VideoPlayer.errorReceived の購読だけ。
    /// </summary>
    public static class GameHudRuntime
    {
        private const string GameSceneName = "GameScene";

        private static bool subscribed;

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
            var go = new GameObject("V5GameHud");
            go.AddComponent<GameHudController>();
        }
    }

    /// <summary>v5 UI の状態配線本体。</summary>
    [DefaultExecutionOrder(100)] // GameSceneManager の Update の後に走らせ、Esc の二重処理を避ける
    public sealed class GameHudController : MonoBehaviour
    {
        private GameHudView hud;
        private GameReadyView ready;
        private GameLoadingView loading;
        private GameErrorView error;
        private PauseMenuView pause;

        private readonly HashSet<VideoPlayer> subscribedPlayers = new HashSet<VideoPlayer>();
        private float nextVideoScan;
        private bool videoError;
        private float loadingSeconds;
        private GameObject legacyMenuPanel;

        private void Start()
        {
            hud = gameObject.AddComponent<GameHudView>();
            hud.Build();

            var readyCanvas = V5GameUi.NewCanvas("V5ReadyCanvas", 1);
            ready = readyCanvas.gameObject.AddComponent<GameReadyView>();
            ready.Build(readyCanvas);

            var overlayCanvas = V5GameUi.NewCanvas("V5OverlayCanvas", 500);
            loading = overlayCanvas.gameObject.AddComponent<GameLoadingView>();
            loading.Build(overlayCanvas);
            LoadingOverlayHook.Bind(loading);

            error = overlayCanvas.gameObject.AddComponent<GameErrorView>();
            error.Build(overlayCanvas);

            var pauseCanvas = V5GameUi.NewCanvas("V5PauseCanvas", 500);
            pause = pauseCanvas.gameObject.AddComponent<PauseMenuView>();
            pause.Build(pauseCanvas);
            if (hud.PauseButton != null)
            {
                hud.PauseButton.onClick.AddListener(pause.Open);
            }

            HideLegacyHud();
        }

        private void Update()
        {
            ScanForVideoPlayers();

            // パイプラインの状態判定 (HudPlayState: Issue #93 の純ロジック)
            bool mediaLoading = IsLegacyLoadingIndicatorActive();
            if (mediaLoading && !GManager.played) loadingSeconds += Time.unscaledDeltaTime;

            var phase = HudPlayState.Evaluate(videoError, GManager.played, mediaLoading, loadingSeconds,
                MusicManager.SelectReturnPending);

            if (phase == HudPlayPhase.Failed)
            {
                error.Show();
                loading.SetVisible(false);
                pause.Close();
            }
            else if (phase == HudPlayPhase.Preparing)
            {
                error.Hide();
                loading.SetVisible(true);
            }
            else
            {
                error.Hide();
                loading.SetVisible(false);
                loadingSeconds = 0f;
            }

            // エラー表示中はポーズを開かない (Esc は選曲へ戻る導線)。
            pause.InputEnabled = !error.IsShown;
        }

        private void LateUpdate()
        {
            // レガシーのポーズメニュー (Animator 付き) が開かないようにする。
            // GameSceneManager の Esc ハンドラが毎回 OpenMenu するため、
            // 開いた直後に閉じて、歌詞パネルの非表示フラグを元に戻す。
            KeepLegacyMenuClosed();
        }

        private bool IsLegacyLoadingIndicatorActive()
        {
            // MusicManager が準備中に active にする既存 UI (HudPlayStatus と同じ観測)。
            // GameObject は active のまま (シグナル保持)、見た目だけ Start で無効化済み。
            var scene = gameObject.scene;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == "loadingmovie" || roots[i].name == "loadingmovie (1)")
                {
                    if (roots[i].activeInHierarchy) return true;
                }
            }
            return false;
        }

        private void KeepLegacyMenuClosed()
        {
            if (legacyMenuPanel == null)
            {
                legacyMenuPanel = FindInactiveByName("MenuPanel");
                if (legacyMenuPanel == null) return;
            }
            if (legacyMenuPanel.activeSelf)
            {
                legacyMenuPanel.SetActive(false);
                RestoreLyricsVisibility();
            }
        }

        /// <summary>非アクティブも含めて名前でシーン内のオブジェクトを探す (起動時の 1 回用)。</summary>
        private GameObject FindInactiveByName(string name)
        {
            var roots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name) return roots[i];
                var t = roots[i].transform.Find(name);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        /// <summary>
        /// レガシーの OpenMenu は歌詞パネルを消したままにする (menuVisible は private)。
        /// ポーズを閉じたら歌詞表示を戻す。
        /// </summary>
        private void RestoreLyricsVisibility()
        {
            var manager = legacyMenuPanel != null
                ? legacyMenuPanel.GetComponentInParent<GameSceneManager>()
                : null;
            if (manager == null) manager = FindFirstObjectByType<GameSceneManager>();
            if (manager == null) return;
            var field = typeof(GameSceneManager).GetField("menuVisible",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(manager, false);
            manager.TextPanelOn();
        }

        /// <summary>
        /// v5 側が担うレガシー HUD を隠す。歌詞 (TextPanel) は残す。
        /// loadingmovie は GameObject を残す (ロード中シグナル) ので見た目だけ無効化。
        /// </summary>
        private void HideLegacyHud()
        {
            var scene = gameObject.scene;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                string n = root.name;
                switch (n)
                {
                    case "SpacetoStart":
                    case "ScoreText":
                    case "comboText":
                    case "Score":
                    case "Gauge":
                    case "HudStatus":
                    case "HudBackToSelectButton":
                    case "MenuPanel":
                    case "MenuButton":
                        root.SetActive(false);
                        break;
                    case "loadingmovie":
                    case "loadingmovie (1)":
                        DisableVisuals(root);
                        break;
                }
            }
        }

        private static void DisableVisuals(GameObject go)
        {
            // GameObject は active のまま (ロード中シグナル)、見た目 (RawImage) と
            // 装飾用 VideoPlayer だけ無効化する。
            foreach (var raw in go.GetComponentsInChildren<RawImage>(true))
            {
                raw.enabled = false;
            }
            foreach (var player in go.GetComponentsInChildren<VideoPlayer>(true))
            {
                player.enabled = false;
            }
        }

        private void OnDestroy()
        {
            foreach (var player in subscribedPlayers)
            {
                if (player != null) player.errorReceived -= OnVideoError;
            }
            subscribedPlayers.Clear();
        }

        /// <summary>VideoPlayer (実行時に生成される Cloud Catalog 用も含む) を購読する。</summary>
        private void ScanForVideoPlayers()
        {
            if (Time.unscaledTime < nextVideoScan) return;
            nextVideoScan = Time.unscaledTime + 1f;
            foreach (var player in FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None))
            {
                if (subscribedPlayers.Add(player))
                {
                    player.errorReceived += OnVideoError;
                }
            }
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            if (IsLegacyLoadingIndicatorActive())
            {
                // 準備中のエラーは direct URL probe の試行も含むので無視 (タイムアウトに任せる)。
                return;
            }
            Debug.LogWarning($"[GameHudRuntime] VideoPlayer error: {message}");
            videoError = true;
        }
    }
}
