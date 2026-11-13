using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// GameScene の v5 UI の起動と状態配線 (Issue #122)。
    ///
    /// Issue #400 で責務を 2 つに分けた:
    ///   - **状態判定と旧 UI の後始末** … <see cref="GamePlayPhaseService"/>
    ///     (v6 でも必要な部分。UI を生成しない)
    ///   - **v5 の View の生成と表示** … このクラス (v6 有効時は生成しない)
    ///
    /// 以前は v6 有効でも `V5ReadyCanvas` / `V5OverlayCanvas` / `V5PauseCanvas` を
    /// 生成し、`V6GameRuntime` が毎フレーム隠していた。pixel を描かないだけで、
    /// GameObject・Graphic・レイアウトの初期化コストは毎回払っていた。
    ///
    /// v6 有効時に **残す** もの:
    ///   - 状態判定 (<see cref="GamePlayPhaseService.LastPhase"/>) … v6 のローディングが読む
    ///   - 旧 HUD の非表示 / Raycaster 無効化 / 歌詞復帰 … v6 の表示を邪魔しないため
    ///   - VideoPlayer のエラー購読 … 失敗検出は v6 も使う
    /// v6 有効時に **作らない** もの:
    ///   - <see cref="GameHudView"/> (HUD は v6 が描く)
    ///   - <see cref="GameReadyView"/> (Start プロンプトは v6 HUD が出す)
    ///   - <see cref="GameLoadingView"/> / <see cref="GameErrorView"/> (v6 Loading が出す)
    ///   - <see cref="PauseMenuView"/> (v6 Pause が有効なとき)
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

        /// <summary>状態判定サービス (v6 でも必要)。</summary>
        private GamePlayPhaseService phaseService;

        /// <summary>
        /// 直近のパイプライン状態。v6 のローディング表示 (V6GameRuntime) が読む。
        ///
        /// Issue #400: 実体は <see cref="GamePlayPhaseService.LastPhase"/>。
        /// 既存の読み手 (V6GameRuntime) との互換のため転送する。
        /// </summary>
        public static HudPlayPhase LastPhase
        {
            get { return GamePlayPhaseService.LastPhase; }
        }

        /// <summary>直近に適用した UI フェーズ (GameUiExclusivity の正本、観測用)。</summary>
        private GameUiPhase currentPhase;

        /// <summary>v6 有効時に v5 の View を作らなかったか (監査用)。</summary>
        public static bool SkipsV5ViewsForAudit { get; private set; }

        private void Start()
        {
            // v6 のゲーム HUD が有効なときは、旧 HUD を作らない
            // (同じ箇所に 2 枚の HUD が重なると崩れて見える)。
            bool v6Hud = FlashBeat.UI.V6.V6UiMode.GameHud;
            bool v6Pause = FlashBeat.UI.V6.Game.Pause.V6PauseView.Enabled;
            SkipsV5ViewsForAudit = v6Hud;

            // --- 状態判定サービス (v6 でも必要) ---
            phaseService = gameObject.AddComponent<GamePlayPhaseService>();
            phaseService.Controller = this;
            GamePlayPhaseService.ResetForAudit();

            // 旧 UI の非表示と Raycaster 無効化は、v5 View の生成より先に行う
            // (生成した直後に自分で消す無駄を無くす)。
            phaseService.HideLegacyHudOnce();
            phaseService.DisableLegacyRaycaster();

            if (!v6Hud)
            {
                hud = gameObject.AddComponent<GameHudView>();
                hud.Build();
            }

            // Ready は v6 HUD が「Space to Start」を出すので、v6 有効時は作らない。
            if (!v6Hud)
            {
                // 550: v6 の dim (-1) / プレイフィールド (400) / HUD (450) より手前。
                // Ready 中は v6 HUD を隠すので、この画面が Ready 専用表示になる (Issue #233)。
                var readyCanvas = V5GameUi.NewCanvas("V5ReadyCanvas", 550);
                ready = readyCanvas.gameObject.AddComponent<GameReadyView>();
                ready.Build(readyCanvas);
            }

            // Loading / Error は v6 の V6LoadingView が単独で表示する。
            if (!v6Hud)
            {
                var overlayCanvas = V5GameUi.NewCanvas("V5OverlayCanvas", 500);
                loading = overlayCanvas.gameObject.AddComponent<GameLoadingView>();
                loading.Build(overlayCanvas);
                LoadingOverlayHook.Bind(loading);

                error = overlayCanvas.gameObject.AddComponent<GameErrorView>();
                error.Build(overlayCanvas);
            }

            // Pause は v6 ポーズが有効なときは作らない (v6 が同じ操作を持つ)。
            if (!v6Hud || !v6Pause)
            {
                var pauseCanvas = V5GameUi.NewCanvas("V5PauseCanvas", 600);
                pause = pauseCanvas.gameObject.AddComponent<PauseMenuView>();
                pause.Build(pauseCanvas);
                if (hud != null && hud.PauseButton != null)
                {
                    hud.PauseButton.onClick.AddListener(pause.Open);
                }

                V5GameUi.ApplySingleInputModel(pauseCanvas);
            }

            // Issue #167: v5 のボタンは自身の矢印キー処理で選択するため、
            // Unity の Selectable 自動ナビゲーションは切っておく (二重動作防止)。
            if (hud != null)
            {
                V5GameUi.ApplySingleInputModel(hud.Canvas);
            }

            // 初期フェーズを決定して適用 (Ready 中は HUD を出さない — Issue #233)。
            currentPhase = GameUiExclusivity.ResolvePhase(
                HudPlayPhase.Hidden, GManager.played, false);
            Apply(GameUiExclusivity.Resolve(currentPhase, V6Flags(), pause != null && pause.IsOpen));
        }

        private void Update()
        {
            // 状態判定 (Issue #400: UI 生成から分離したサービス)。
            if (phaseService != null) phaseService.Tick();
            var play = phaseService != null ? GamePlayPhaseService.LastPhase : HudPlayPhase.Hidden;

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
    }
}
