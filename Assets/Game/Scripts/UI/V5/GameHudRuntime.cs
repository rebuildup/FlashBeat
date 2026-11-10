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

        /// <summary>直近のパイプライン状態。v6 のローディング表示 (V6GameRuntime) が読む。</summary>
        public static HudPlayPhase LastPhase;

        private readonly HashSet<VideoPlayer> subscribedPlayers = new HashSet<VideoPlayer>();
        private float nextVideoScan;
        private float nextLegacyHide;
        private bool videoError;
        private float loadingSeconds;
        private GameObject legacyMenuPanel;
        private GameObject legacyLoadingIndicator;

        /// <summary>直近に適用した UI フェーズ (GameUiExclusivity の正本、観測用)。</summary>
        private GameUiPhase currentPhase;

        /// <summary>旧 UI の再適用 (名前ベースの SetActive(false)) の間隔 (秒)。</summary>
        private const float LegacyHideInterval = 0.5f;

        private void Start()
        {
            // v6 のゲーム HUD が有効なときは、旧 HUD (GameHudView) を作らない
            // (同じ箇所に 2 枚の HUD が重なると崩れて見える)。
            // 準備中 / 失敗 / ポーズは未移植 (P1) なので旧実装のまま使う。
            bool v6Hud = FlashBeat.UI.V6.V6UiMode.GameHud;

            if (!v6Hud)
            {
                hud = gameObject.AddComponent<GameHudView>();
                hud.Build();
            }

            // 550: v6 の dim (-1) / プレイフィールド (400) / HUD (450) より手前。
            // Ready 中は v6 HUD を隠すので、この画面が Ready 専用表示になる (Issue #233)。
            var readyCanvas = V5GameUi.NewCanvas("V5ReadyCanvas", 550);
            ready = readyCanvas.gameObject.AddComponent<GameReadyView>();
            ready.Build(readyCanvas);

            var overlayCanvas = V5GameUi.NewCanvas("V5OverlayCanvas", 500);
            loading = overlayCanvas.gameObject.AddComponent<GameLoadingView>();
            loading.Build(overlayCanvas);
            LoadingOverlayHook.Bind(loading);

            error = overlayCanvas.gameObject.AddComponent<GameErrorView>();
            error.Build(overlayCanvas);

            var pauseCanvas = V5GameUi.NewCanvas("V5PauseCanvas", 600);
            pause = pauseCanvas.gameObject.AddComponent<PauseMenuView>();
            pause.Build(pauseCanvas);
            if (hud != null && hud.PauseButton != null)
            {
                hud.PauseButton.onClick.AddListener(pause.Open);
            }

            // Issue #167: v5 のボタンは自身の矢印キー処理で選択するため、
            // Unity の Selectable 自動ナビゲーションは切っておく (二重動作防止)。
            if (hud != null)
            {
                V5GameUi.ApplySingleInputModel(hud.Canvas);
            }
            V5GameUi.ApplySingleInputModel(pauseCanvas);

            HideLegacyHud();
            DisableLegacyRaycaster();

            // 初期フェーズを決定して適用 (Ready 中は HUD を出さない — Issue #233)。
            currentPhase = GameUiExclusivity.ResolvePhase(
                HudPlayPhase.Hidden, GManager.played, false);
            Apply(GameUiExclusivity.Resolve(currentPhase, V6Flags(), pause != null && pause.IsOpen));
        }

        private void Update()
        {
            ScanForVideoPlayers();

            // 旧 UI の非表示は 1 回きりだと後から再有効化されたオブジェクトが
            // 残る (Issue #233 の原因)。状態から毎フレーム再適用する (負荷抑止で
            // LegacyHideInterval 秒ごと)。
            ReapplyLegacyHud();

            // パイプラインの状態判定 (HudPlayState: Issue #93 の純ロジック)
            bool mediaLoading = IsLegacyLoadingIndicatorActive();
            if (mediaLoading && !GManager.played) loadingSeconds += Time.unscaledDeltaTime;

            var play = HudPlayState.Evaluate(videoError, GManager.played, mediaLoading, loadingSeconds,
                MusicManager.SelectReturnPending);
            LastPhase = play; // v6 側 (V6GameRuntime) がローディング表示に使う
            if (play != HudPlayPhase.Preparing) loadingSeconds = 0f;

            // 表示の正本は GameUiExclusivity (Issue #233)。毎フレーム状態から再適用する。
            bool pauseOpen = pause != null && pause.IsOpen;
            currentPhase = GameUiExclusivity.ResolvePhase(play, GManager.played, pauseOpen);
            Apply(GameUiExclusivity.Resolve(currentPhase, V6Flags(), pauseOpen));

            // エラー表示中はポーズを開かない (Esc は選曲へ戻る導線)。
            if (pause != null) pause.InputEnabled = currentPhase != GameUiPhase.Failed;
        }

        private static V6UiFlags V6Flags()
        {
            return new V6UiFlags
            {
                GameHud = FlashBeat.UI.V6.V6UiMode.GameHud,
                PauseEnabled = FlashBeat.UI.V6.Game.Pause.V6PauseView.Enabled,
            };
        }

        private void Apply(in GameUiVisibility vis)
        {
            GameUiExclusivity.Apply(vis, ready, hud, loading, error, pause);
        }

        private void LateUpdate()
        {
            // レガシーのポーズメニュー (Animator 付き) が開かないようにする。
            // GameSceneManager の Esc ハンドラが毎回 OpenMenu するため、
            // 開いた直後に閉じて、歌詞パネルの非表示フラグを元に戻す。
            if (legacyMenuPanel == null)
            {
                legacyMenuPanel = FindFirstDeep("MenuPanel");
                if (legacyMenuPanel == null) return;
            }
            if (legacyMenuPanel.activeSelf)
            {
                legacyMenuPanel.SetActive(false);
                RestoreLyricsVisibility();
            }
        }

        private bool IsLegacyLoadingIndicatorActive()
        {
            // MusicManager が準備中に active にする既存 UI (HudPlayStatus と同じ観測)。
            // GameObject は active のまま (シグナル保持)、見た目だけ Start で無効化済み。
            return legacyLoadingIndicator != null && legacyLoadingIndicator.activeInHierarchy;
        }

        /// <summary>
        /// レガシー Canvas の GraphicRaycaster を無効化する (Issue #167)。
        /// レガシー Canvas は sortingOrder 0 で v5 HUD (-1) より手前にあり、
        /// 歌詞パネルなどの Raycast 対象が v5 のボタン (ポーズ等) を横取りしていた。
        /// v5 UI が画面上の操作を受け持つため、レガシー側はクリックを受けない。
        /// シーンを引数に取る公開純粋関数として抽出した (EditMode テストから
        /// 直接検証するため)。返り値は今回無効化した個数 (すでに無効なものは
        /// 数えない = 冪等)。
        /// </summary>
        public static int DisableLegacyRaycastersIn(Scene scene)
        {
            int disabled = 0;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var legacyCanvas = roots[i].GetComponent<Canvas>();
                if (legacyCanvas == null) { continue; }
                var raycaster = legacyCanvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null && raycaster.enabled)
                {
                    raycaster.enabled = false;
                    disabled++;
                }
            }
            return disabled;
        }

        private void DisableLegacyRaycaster()
        {
            DisableLegacyRaycastersIn(gameObject.scene);
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
        /// 対象は Canvas 配下など階層のどこにあっても良い。
        /// Start の 1 回きりではなく <see cref="ReapplyLegacyHud"/> から
        /// 定期的に再適用する (後から再有効化された旧 UI を消し続ける — Issue #233)。
        /// </summary>
        private void HideLegacyHud()
        {
            var roots = gameObject.scene.GetRootGameObjects();
            foreach (var name in LegacyHideNames)
            {
                foreach (var go in FindAllDeep(roots, name))
                {
                    go.SetActive(false);
                }
            }

            // loadingmovie は GameObject を active のまま (ロード中シグナル)、見た目だけ無効化
            foreach (var name in LegacyLoadingNames)
            {
                foreach (var go in FindAllDeep(roots, name))
                {
                    DisableVisuals(go);
                    if (legacyLoadingIndicator == null) legacyLoadingIndicator = go;
                }
            }
        }

        /// <summary>
        /// 旧 UI 非表示の再適用。毎フレームは重い (シーン全体の走査) ので
        /// <see cref="LegacyHideInterval"/> 秒ごとに実施する。冪等。
        /// </summary>
        private void ReapplyLegacyHud()
        {
            if (Time.unscaledTime < nextLegacyHide) return;
            nextLegacyHide = Time.unscaledTime + LegacyHideInterval;
            HideLegacyHud();
        }

        private static readonly string[] LegacyHideNames =
        {
            "SpacetoStart", "ScoreText", "comboText", "Score", "Gauge",
            "HudStatus", "HudBackToSelectButton", "MenuPanel", "MenuButton",
        };

        private static readonly string[] LegacyLoadingNames = { "loadingmovie", "loadingmovie (1)" };

        /// <summary>非アクティブも含めて名前が一致する全オブジェクトを返す (起動時の 1 回用)。</summary>
        private static List<GameObject> FindAllDeep(GameObject[] roots, string name)
        {
            var found = new List<GameObject>();
            for (int i = 0; i < roots.Length; i++)
            {
                var children = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < children.Length; j++)
                {
                    if (children[j].name == name && !found.Contains(children[j].gameObject))
                    {
                        found.Add(children[j].gameObject);
                    }
                }
            }
            return found;
        }

        /// <summary>非アクティブも含めて名前で最初の 1 つを返す (LateUpdate の初回用)。</summary>
        private GameObject FindFirstDeep(string name)
        {
            var roots = gameObject.scene.GetRootGameObjects();
            var found = FindAllDeep(roots, name);
            return found.Count > 0 ? found[0] : null;
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
