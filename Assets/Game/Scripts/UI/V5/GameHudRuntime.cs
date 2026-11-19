using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// GameScene のゲーム UI ランタイム起動と状態配線 (Issue #122 / #740)。
    ///
    /// Issue #400 で責務を 2 つに分けた:
    ///   - **状態判定と旧 UI の後始末** … <see cref="GamePlayPhaseService"/>
    ///     (v6 でも必要な部分。UI を生成しない)
    ///   - **v5 の View の生成と表示** … かつてはこのクラスが担ったが、
    ///     Issue #740 で v5 View (GameHudView / GameReadyView / GameLoadingView /
    ///     GameErrorView / PauseMenuView) を削除した。出荷構成は v6 HUD +
    ///     v6 ポーズだけなので、ここでは View を一切生成しない。
    ///
    /// 残すもの:
    ///   - 状態判定 (<see cref="GamePlayPhaseService.LastPhase"/>) … v6 のローディングが読む
    ///   - 旧 HUD の非表示 / Raycaster 無効化 / 歌詞復帰 … v6 の表示を邪魔しないため
    ///   - VideoPlayer のエラー購読 … 失敗検出は v6 も使う
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

    /// <summary>ゲーム UI の状態配線本体 (旧称 GameHudController)。</summary>
    [DefaultExecutionOrder(100)] // GameSceneManager の Update の後に走らせ、Esc の二重処理を避ける
    public sealed class GameHudController : MonoBehaviour
    {
        /// <summary>状態判定サービス。</summary>
        private GamePlayPhaseService phaseService;

        /// <summary>
        /// 直近のパイプライン状態。v6 のローディング表示 (V6GameRuntime) が読む。
        /// 実体は <see cref="GamePlayPhaseService.LastPhase"/>。
        /// </summary>
        public static HudPlayPhase LastPhase
        {
            get { return GamePlayPhaseService.LastPhase; }
        }

        /// <summary>v5 View を生成しない構成であることの監査フラグ (常に true)。</summary>
        public static bool SkipsV5ViewsForAudit { get; private set; }

        private void Start()
        {
            // 出荷構成は v6 HUD + v6 ポーズのみ。v5 の View は生成しない
            // (Issue #740 で v5 View 自体を削除済み)。
            SkipsV5ViewsForAudit = true;

            // --- 状態判定サービス ---
            phaseService = gameObject.AddComponent<GamePlayPhaseService>();
            phaseService.Controller = this;
            GamePlayPhaseService.ResetForAudit();

            // 旧 UI の非表示と Raycaster 無効化は View 生成より先に行う
            // (生成した直後に自分で消す無駄を無くす)。
            phaseService.HideLegacyHudOnce();
            phaseService.DisableLegacyRaycaster();
        }

        private void Update()
        {
            // 状態判定 (UI 生成から分離したサービス)。
            if (phaseService != null) phaseService.Tick();
        }

        private void LateUpdate()
        {
            // レガシーのポーズメニュー (Animator 付き) が開かないようにする。
            // GameSceneManager の Esc ハンドラが毎回 OpenMenu するため、
            // 開いた直後に閉じて、歌詞パネルの非表示フラグを元に戻す。
            if (phaseService != null) phaseService.KeepLegacyMenuClosed();
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

        internal void DisableLegacyRaycasterInScene()
        {
            DisableLegacyRaycastersIn(gameObject.scene);
        }

        private void OnDestroy()
        {
            if (phaseService != null)
            {
                phaseService.ForgetVideoPlayers();
            }
        }

        /// <summary>v5 側が担うレガシー HUD を隠す対象の名前 (Issue #233)。</summary>
        internal static readonly string[] LegacyHideNames =
        {
            "SpacetoStart", "ScoreText", "comboText", "Score", "Gauge",
            "HudStatus", "HudBackToSelectButton", "MenuPanel", "MenuButton",
        };

        /// <summary>ロード中シグナルとして残す旧オブジェクトの名前。</summary>
        internal static readonly string[] LegacyLoadingNames = { "loadingmovie", "loadingmovie (1)" };

        // --- 旧 UI の歌詞 / ロード表示の名前 (旧 GameUiExclusivity から移設) ---

        /// <summary>
        /// v6 モードで隠すべき旧 (v5) の **歌詞** オブジェクト名の正本。
        ///
        /// v6 の HUD は歌詞を自分のレイヤーで描くので、旧の歌詞パネル
        /// (QuestionText / FuriganaText / RomajiText) が残っていると
        /// **同じ歌詞が二重表示** になる (Issue #233)。
        /// </summary>
        public static readonly string[] LegacyLyricObjectNames =
        {
            "QuestionText",
            "FuriganaText",
            "RomajiText",
        };

        /// <summary>
        /// 実行時生成の旧ロード表示 (LoadingOverlay) の名前。
        /// DontDestroyOnLoad なのでシーンをまたいで残り、v6 モードでも
        /// 「描かれているか」を名前で判定する。
        /// </summary>
        public const string LegacyLoadingOverlayName = "LoadingOverlay";

        /// <summary>
        /// 名前が「v6 モードで隠すべき旧の歌詞オブジェクト」か。
        /// </summary>
        public static bool IsLegacyLyricObjectName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < LegacyLyricObjectNames.Length; i++)
            {
                if (LegacyLyricObjectNames[i] == name) return true;
            }
            return false;
        }
    }
}
