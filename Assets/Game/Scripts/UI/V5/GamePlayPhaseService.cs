using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// GameScene の「再生パイプラインの状態」と「旧 UI を隠す」責務を、
    /// 画面の生成から切り離したサービス (Issue #400)。
    ///
    /// なぜ分けるか:
    ///   以前は <see cref="GameHudController"/> が
    ///   (a) 状態判定 (<see cref="HudPlayPhase"/> の算出)
    ///   (b) 旧 v5 の Ready / Loading / Error / Pause の **生成**
    ///   (c) その表示制御
    ///   を 1 つの Start/Update で行っていた。v6 有効時は (b) が誰にも使われない
    ///   画面を作り、<c>V6GameRuntime</c> が毎フレーム隠す、という二重構造になっていた。
    ///   v6 は (a) を必要とする (<see cref="LastPhase"/>) ので、単純に削除はできない。
    ///
    /// このクラスが持つもの:
    ///   - 状態判定 (<see cref="Evaluate"/>) とその最新値 (<see cref="LastPhase"/>)
    ///   - 旧 HUD の非表示・Raycaster 無効化・歌詞の復帰 (表示の後始末)
    ///   - VideoPlayer のエラー購読
    ///
    /// このクラスが持たないもの:
    ///   - v5 の View の生成 (<see cref="GameHudController"/> が v6 の有無に応じて決める)
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class GamePlayPhaseService : MonoBehaviour
    {
        /// <summary>直近のパイプライン状態。v6 のローディング表示が読む。</summary>
        public static HudPlayPhase LastPhase { get; private set; }

        /// <summary>直近に観測した「旧ロード表示が出ているか」。</summary>
        public static bool LegacyMediaLoading { get; private set; }

        private readonly System.Collections.Generic.HashSet<VideoPlayer> subscribedPlayers =
            new System.Collections.Generic.HashSet<VideoPlayer>();

        private float nextVideoScan;
        private float loadingSeconds;
        private bool videoError;

        /// <summary>旧 UI 再適用の間隔 (秒)。毎フレームのシーン全走査を避ける。</summary>
        private const float LegacyHideInterval = 0.5f;

        private float nextLegacyHide;
        private GameObject legacyMenuPanel;
        private GameObject legacyLoadingIndicator;

        /// <summary>
        /// このサービスが担当する GameHudController (任意)。旧 HUD を隠す対象の
        /// シーンを決めるためだけに使う。
        /// </summary>
        internal GameHudController Controller { get; set; }

        /// <summary>
        /// 状態を 1 回評価して更新する。<see cref="Controller"/> の Update から呼ぶ。
        /// </summary>
        internal void Tick()
        {
            ScanForVideoPlayers();
            ReapplyLegacyHud();

            bool mediaLoading = IsLegacyLoadingIndicatorActive();
            if (mediaLoading && !GManager.played) loadingSeconds += Time.unscaledDeltaTime;

            var play = HudPlayState.Evaluate(videoError, GManager.played, mediaLoading, loadingSeconds,
                MusicManager.SelectReturnPending);
            LastPhase = play;
            LegacyMediaLoading = mediaLoading;
            if (play != HudPlayPhase.Preparing) loadingSeconds = 0f;
        }

        /// <summary>旧 HUD を隠す処理を 1 回実行する (Start 時)。</summary>
        internal void HideLegacyHudOnce()
        {
            HideLegacyHud();
        }

        /// <summary>
        /// テスト用: 状態を直接設定する (VideoPlayer を作らずに v6 の表示を検証する)。
        /// </summary>
        public static void SetPhaseForAudit(HudPlayPhase phase)
        {
            LastPhase = phase;
        }

        /// <summary>テスト用: 状態を初期化する。</summary>
        public static void ResetForAudit()
        {
            LastPhase = HudPlayPhase.Hidden;
            LegacyMediaLoading = false;
        }

        internal void ForgetVideoPlayers()
        {
            foreach (var player in subscribedPlayers)
            {
                if (player != null)
                {
                    player.errorReceived -= OnVideoError;
                }
            }

            subscribedPlayers.Clear();
        }

        // --- 旧 UI の後始末 (表示の所有は v6 でも、旧側の掃除は必要) ---

        /// <summary>
        /// v5 側が担うレガシー HUD を隠す。歌詞 (TextPanel) は残す。
        /// 対象は Canvas 配下など階層のどこにあっても良い。
        /// Start の 1 回きりではなく <see cref="ReapplyLegacyHud"/> から
        /// 定期的に再適用する (後から再有効化された旧 UI を消し続ける — Issue #233)。
        /// </summary>
        internal void HideLegacyHud()
        {
            if (Controller == null) return;
            var roots = Controller.gameObject.scene.GetRootGameObjects();
            foreach (var name in GameHudController.LegacyHideNames)
            {
                foreach (var go in FindAllDeep(roots, name))
                {
                    go.SetActive(false);
                }
            }

            // loadingmovie は GameObject を active のまま (ロード中シグナル)、見た目だけ無効化
            foreach (var name in GameHudController.LegacyLoadingNames)
            {
                foreach (var go in FindAllDeep(roots, name))
                {
                    DisableVisuals(go);
                    if (legacyLoadingIndicator == null) legacyLoadingIndicator = go;
                }
            }
        }

        /// <summary>旧 UI 非表示の再適用。毎フレームは重い (シーン全体の走査) ので定期的に実施する。冪等。</summary>
        private void ReapplyLegacyHud()
        {
            if (Time.unscaledTime < nextLegacyHide) return;
            nextLegacyHide = Time.unscaledTime + LegacyHideInterval;
            HideLegacyHud();
        }

        /// <summary>
        /// レガシーの OpenMenu は歌詞パネルを消したままにする (menuVisible は private)。
        /// ポーズを閉じたら歌詞表示を戻す。
        /// </summary>
        internal void RestoreLyricsVisibility()
        {
            if (Controller == null) return;
            if (legacyMenuPanel == null)
            {
                legacyMenuPanel = FindFirstDeep("MenuPanel");
                if (legacyMenuPanel == null) return;
            }

            var manager = legacyMenuPanel.GetComponentInParent<GameSceneManager>();
            if (manager == null) manager = FindFirstObjectByType<GameSceneManager>();
            if (manager == null) return;
            var field = typeof(GameSceneManager).GetField("menuVisible",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(manager, false);
            manager.TextPanelOn();
        }

        /// <summary>レガシーのポーズメニューが開かないようにする (LateUpdate から呼ぶ)。</summary>
        internal void KeepLegacyMenuClosed()
        {
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

        internal bool IsLegacyLoadingIndicatorActive()
        {
            return legacyLoadingIndicator != null && legacyLoadingIndicator.activeInHierarchy;
        }

        /// <summary>レガシー Canvas の GraphicRaycaster を無効化する (Issue #167)。</summary>
        internal void DisableLegacyRaycaster()
        {
            if (Controller == null) return;
            Controller.DisableLegacyRaycasterInScene();
        }

        private void OnDestroy()
        {
            ForgetVideoPlayers();
        }

        // --- 内部 ---

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

        /// <summary>非アクティブも含めて名前が一致する全オブジェクトを返す。</summary>
        private static System.Collections.Generic.List<GameObject> FindAllDeep(GameObject[] roots, string name)
        {
            var found = new System.Collections.Generic.List<GameObject>();
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

        /// <summary>非アクティブも含めて名前で最初の 1 つを返す。</summary>
        private GameObject FindFirstDeep(string name)
        {
            if (Controller == null) return null;
            var roots = Controller.gameObject.scene.GetRootGameObjects();
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
    }
}
