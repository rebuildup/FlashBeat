using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V6.Game.Hud;
using FlashBeat.UI.V6.Game.Loading;
using FlashBeat.UI.V6.Game.Pause;

namespace FlashBeat.UI.V6
{
    /// <summary>
    /// v6 ゲーム画面 (HUD) の起動 (Issue #223 統合担当)。
    ///
    /// 描画の積層 (v6 デザイン `Uc9v6` / `nrfw8` の順):
    ///   1. 動画 (VideoPlayer が描く)
    ///   2. Video Dim  #00000080              ← Canvas "V6GameDimCanvas" (sorting -3)
    ///   3. プレイフィールド (レーン / ノーツ) ← Canvas "V6PlayfieldCanvas" (sorting -2, #219 が作る)
    ///   4. HUD (グレード / スコア / コンボ / キーガイド / 歌詞) ← Canvas "V6GameHudCanvas" (sorting -1)
    ///
    /// プレイフィールドは <c>V6PlayfieldRuntime</c> (#219) が同じ方式で起動する。
    /// ここで二重に作らない (片方だけが Current を持つ)。
    ///
    /// 旧 3D プレイフィールド (GameScene の "Lane" / "JudgeLine" / "LaneLight*") と
    /// 旧シーン配置 UI は v6 有効時は隠す (2 枚重なると崩れて見える)。
    /// 判定・入力・譜面データ (Judge / Notes / NotesManager) には触らない。
    /// </summary>
    public sealed class V6GameRuntime : MonoBehaviour
    {
        private const string GameSceneName = "GameScene";

        /// <summary>動画を減光するレイヤー (プレイフィールドより下)。</summary>
        private const int DimSortingOrder = -3;

        /// <summary>HUD (プレイフィールドより上)。</summary>
        private const int HudSortingOrder = -1;

        /// <summary>ポーズ / ローディング (最前面)。</summary>
        private const int PauseSortingOrder = 600;

        private static bool subscribed;

        private V6GameHudView hud;
        private GameObject dimCanvas;
        private RectTransform hudFrame;
        private V6PauseView pause;
        private V6LoadingView loading;
        private Vector2 appliedCanvasSize;

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
            if (!V6UiMode.GameHud) return;
            if (FindObjectOfType<V6GameRuntime>() != null) return;

            var go = new GameObject("V6GameUi");
            go.AddComponent<V6GameRuntime>();
        }

        private void Start()
        {
            BuildDimLayer();
            BuildHudLayer();
            BuildPauseAndLoading();
            HideLegacyPlayfield();
            HideLegacyUi();
            HideLegacyPauseAndLoading();
        }

        private void Update()
        {
            UpdatePause();
            UpdateLoading();
            ApplyHudScale();
            if (hud != null)
            {
                // 表示所有: Ready (再生前) は v5 の GameReadyView が単独所有し、
                // 演奏中は v6 HUD だけを出す。同時に出ると重なって崩れる。
                bool show = GManager.played;
                if (hud.Root != null && hud.Root.gameObject.activeSelf != show)
                {
                    hud.SetVisible(show);
                }
                hud.Refresh();
            }
        }

        /// <summary>
        /// v6 ポーズの開閉と入力。Esc で開閉、矢印で項目移動、左右で値変更。
        /// 既存の KeyActions の割当をそのまま使う (Judge / KeyBindings と同じ正本)。
        /// </summary>
        private void UpdatePause()
        {
            if (pause == null) return;

            bool menuOpen = GameSceneManager.Menu;
            if (!menuOpen && KeyActions.IsDown(KeyActions.KeyAction.Back))
            {
                pause.Open();
                GameSceneManager.Menu = true;
                return;
            }
            if (menuOpen && pause.IsOpen)
            {
                if (KeyActions.IsDown(KeyActions.KeyAction.Back))
                {
                    pause.Close();
                    GameSceneManager.Menu = false;
                    return;
                }
                if (KeyActions.IsDown(KeyActions.KeyAction.MenuSwitch)) pause.SelectNextTab(1);
                if (KeyActions.IsDown(KeyActions.KeyAction.NavPrev)) pause.MoveSelection(-1);
                if (KeyActions.IsDown(KeyActions.KeyAction.NavNext)) pause.MoveSelection(1);
                var manager = FindObjectOfType<GameSceneManager>();
                if (manager == null) return;
                if (KeyActions.IsDown(KeyActions.KeyAction.MenuRetry)) { pause.Close(); GameSceneManager.Menu = false; manager.RetryGame(); }
                if (KeyActions.IsDown(KeyActions.KeyAction.MenuEnd)) { pause.Close(); GameSceneManager.Menu = false; manager.GameEnd(); }
                if (KeyActions.IsDown(KeyActions.KeyAction.MenuRetire)) { pause.Close(); GameSceneManager.Menu = false; manager.RetireGame(); }
                pause.Refresh();
            }
        }

        /// <summary>
        /// v6 のローディング / 失敗表示を実際のロード状態へ接続する (Issue #233)。
        /// 状態判定は v5 の GameHudController が毎フレーム計算した LastPhase を読む —
        /// v5 の OverlayCanvas は非表示化済みなので、表示の所有はここへ移す。
        /// </summary>
        private void UpdateLoading()
        {
            if (loading == null) return;
            var phase = FlashBeat.UI.V5.GameHudController.LastPhase;

            if (phase == HudPlayPhase.Preparing)
            {
                var song = GManager.GetSong(GManager.songID);
                loading.SetLoading(song != null ? song.title : "", song != null ? song.musician : "", null);
                loading.SetVisible(true);
            }
            else if (phase == HudPlayPhase.Failed)
            {
                // v5 と同じ: 失敗時はポーズを閉じ、選曲への導線を優先する。
                if (pause != null && pause.IsOpen) pause.Close();
                loading.SetError("Esc キーか下のボタンで選曲画面へ戻れます");
                loading.SetVisible(true);
            }
            else
            {
                loading.SetVisible(false);
            }
        }

        /// <summary>動画を減光するレイヤー (プレイフィールドより下)。</summary>
        private void BuildDimLayer()
        {
            // 既定では映像を減光しない (元の明るさを保つ。減光すると 3D レーンも暗くなる)。
            if (!V6UiMode.VideoDim) return;

            var canvas = FlashBeat.UI.V5.V5GameUi.NewCanvas("V6GameDimCanvas", DimSortingOrder);
            dimCanvas = canvas.gameObject;
            dimCanvas.transform.SetParent(transform, false);

            var go = new GameObject("Video Dim", typeof(RectTransform));
            go.transform.SetParent(dimCanvas.transform, false);
            var dim = go.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 128f / 255f); // #00000080
            dim.raycastTarget = false;
            var rt = dim.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// v6 のポーズ / 曲ローディング (#224)。旧 v5 の実装は作らない
        /// (V6UiMode.GameHud が有効なときだけ v6 側を作る)。
        /// </summary>
        private void BuildPauseAndLoading()
        {
            var canvas = FlashBeat.UI.V5.V5GameUi.NewCanvas("V6GamePauseCanvas", PauseSortingOrder);
            canvas.gameObject.transform.SetParent(transform, false);

            pause = V6PauseView.Create(canvas.GetComponent<RectTransform>());
            loading = V6LoadingView.Create(canvas.GetComponent<RectTransform>());
            loading.SetVisible(false);
        }

        private void BuildHudLayer()
        {
            var canvas = FlashBeat.UI.V5.V5GameUi.NewCanvas("V6GameHudCanvas", HudSortingOrder);
            canvas.gameObject.transform.SetParent(transform, false);

            // HUD 部品は 1920x1080 の共通テンプレート座標で組み立て、親フレームを
            // min(width / 1920, height / 1080) で等比拡大する。余分な軸はフレームを広げる。
            hudFrame = FlashBeat.UI.V5.V5Ui.Node(canvas.gameObject, "V6 HUD Logical Viewport · 1920x1080 base")
                .GetComponent<RectTransform>();
            hudFrame.anchorMin = new Vector2(0.5f, 0.5f);
            hudFrame.anchorMax = new Vector2(0.5f, 0.5f);
            hudFrame.pivot = new Vector2(0.5f, 0.5f);
            hudFrame.sizeDelta = new Vector2(1920f, 1080f);

            hud = V6GameHudView.Create(hudFrame);

            // HUD 側の減光は自分のレイヤー (Video Dim canvas) が出す。
            // HUD 内の同名 Image は使わない (順序が崩れるため)。
            DisableInternalDim();
        }

        /// <summary>
        /// <summary>
        /// HUD フレーム全体に一度だけ等比倍率を適用する。
        ///
        /// 部品寸法は 1920x1080 の論理テンプレートを使い、フレームの論理サイズを
        /// 画面寸法 / 倍率へ広げる。Mac 2560x1664 は 1920x1248 の論理領域となり、
        /// 上端・中央・下端に配置した既存要素がそれぞれのアンカーに追従する。
        /// </summary>
        private void ApplyHudScale()
        {
            if (hudFrame == null) return;
            var parent = hudFrame.parent as RectTransform;
            if (parent == null) return;
            Vector2 size = parent.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;

            float s = V6HudMetrics.ScaleForResolution(size.x, size.y);
            Vector2 logicalSize = V6HudMetrics.LogicalViewportSize(size.x, size.y);
            if (size == appliedCanvasSize && hudFrame.sizeDelta == logicalSize) return;
            appliedCanvasSize = size;
            hudFrame.sizeDelta = logicalSize;
            hudFrame.localScale = new Vector3(s, s, 1f);
        }

        private void DisableInternalDim()
        {
            if (hud == null || hud.Root == null) return;
            Transform found = FindChild(hud.Root, "Video Dim");
            if (found == null) return;
            found.gameObject.SetActive(false);
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindChild(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        /// <summary>
        /// 旧 3D のプレイフィールド (板 / 判定線 / レーン光) を隠す。
        /// 既定 (v6 プレイフィールド無効) では **隠さない** — 元のレーン描画と
        /// キー入力時のレーン光をそのまま使うため。
        /// </summary>
        private void HideLegacyPlayfield()
        {
            if (!V6UiMode.Playfield) return;

            HideByName("Lane");
            HideByName("JudgeLine");
            for (int i = 1; i <= 8; i++)
            {
                HideByName("LaneLight" + i);
            }
        }

        private static void HideByName(string name)
        {
            var obj = GameObject.Find(name);
            if (obj != null) obj.SetActive(false);
        }

        /// <summary>
        /// 旧シーン配置 UI (スコア、Space 案内など) を隠す。
        /// v6 の HUD が同じ場所を描くので、二枚重ねると読めなくなる。
        ///
        /// 対象は **シーン配置の "Canvas" だけ**。旧 v5 のランタイム Canvas
        /// (V5ReadyCanvas / V5OverlayCanvas / V5PauseCanvas) は未移植 (P1) の
        /// 準備中・失敗・ポーズ表示として残す。
        /// "loadingmovie" はロード中シグナルとして v5 が使うので触らない。
        /// </summary>
        private void HideLegacyUi()
        {
            var canvases = FindObjectsOfType<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null) continue;
                if (canvas.name != "Canvas") continue;

                var graphics = canvas.GetComponentsInChildren<Graphic>(true);
                for (int g = 0; g < graphics.Length; g++)
                {
                    Graphic graphic = graphics[g];
                    if (graphic == null) continue;
                    if (graphic.transform.IsChildOf(transform)) continue;
                    if (IsLoadingSignal(graphic)) continue;
                    graphic.enabled = false;
                }

                var raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null) raycaster.enabled = false;
            }
        }

        /// <summary>
        /// 旧 v5 の ReadyView (右上の曲情報 + 中央の "Space / Start") を隠す。
        /// v6 は自分の Start Hint を出す (同じ位置・同じ書体)。
        /// </summary>
        private static void HideLegacyReadyView()
        {
            var ready = FindObjectsOfType<FlashBeat.UI.V5.GameReadyView>();
            for (int i = 0; i < ready.Length; i++)
            {
                if (ready[i] != null) ready[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 旧 v5 のポーズ / ローディング / エラー表示を隠す (v6 が同じ役割を持つ)。
        /// ReadyView (Space 待ち) は残す — 表示所有は Ready 中が v5 / 演奏中が v6。
        /// </summary>
        private void HideLegacyPauseAndLoading()
        {
            var pauseViews = FindObjectsOfType<FlashBeat.UI.V5.PauseMenuView>();
            for (int i = 0; i < pauseViews.Length; i++)
            {
                if (pauseViews[i] != null) pauseViews[i].gameObject.SetActive(false);
            }

            var canvases = FindObjectsOfType<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null) continue;
                if (canvas.name != "V5OverlayCanvas") continue;
                var graphics = canvas.GetComponentsInChildren<Graphic>(true);
                for (int g = 0; g < graphics.Length; g++)
                {
                    if (graphics[g] != null) graphics[g].enabled = false;
                }
            }
        }

        private static bool IsLoadingSignal(Graphic graphic)
        {
            Transform t = graphic.transform;
            while (t != null)
            {
                if (t.name == "loadingmovie") return true;
                t = t.parent;
            }
            return false;
        }
    }
}
