using System.Collections.Generic;
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

        /// <summary>隠し対象の旧 v5 ポーズ (キャッシュ。シーン再ロードで作り直す)。</summary>
        private FlashBeat.UI.V5.PauseMenuView legacyPause;

        /// <summary>隠し対象の旧 v5 ローディング / エラー Canvas (キャッシュ)。</summary>
        private Canvas legacyOverlayCanvas;

        /// <summary>隠し対象の旧 v5 Ready プロンプト Canvas (キャッシュ)。</summary>
        private Canvas legacyReadyCanvas;

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
        }

        /// <summary>
        /// 旧 v5 のポーズ / ローディング / エラー表示を毎フレーム隠す (表示の所有は v6)。
        /// v5 の GameHudController は [DefaultExecutionOrder(100)] の Start で
        /// PauseMenuView / V5OverlayCanvas (loading / error) を **作り**、Update で
        /// 毎フレーム出し直す (Preparing → loading.SetVisible(true) 等)。
        /// かつて Start (実行順 0) の 1 回だけ隠していたため v5 の生成前に走って
        /// 二重表示になっていた。全 Update の後に走る LateUpdate で隠す。
        /// 破棄はしない: GameHudController が毎フレーム参照していて
        /// (pause.InputEnabled / pause.Close)、破棄するとその経路で
        /// MissingReferenceException になる。
        /// </summary>
        private void LateUpdate()
        {
            if (legacyPause == null)
            {
                legacyPause = FindObjectOfType<FlashBeat.UI.V5.PauseMenuView>();
            }

            // v6 ポーズが無効 (V6PauseView.Enabled = false) のときは v5 ポーズが役割を持つ。
            if (legacyPause != null && V6PauseView.Enabled && legacyPause.gameObject.activeSelf)
            {
                legacyPause.gameObject.SetActive(false);
            }

            if (legacyReadyCanvas == null)
            {
                // GameHudController (実行順 100) が Start で V5ReadyCanvas を作るので
                // v6 より遅い。ここで見つけてから隠す (シーン再ロードで作り直し)。
                var ready = FindObjectOfType<FlashBeat.UI.V5.GameReadyView>();
                if (ready != null) legacyReadyCanvas = ready.GetComponent<Canvas>();
            }

            // v6 HUD が Ready 中の「Space to Start」を出す (V6GameHudView の歌詞位置)。
            // v5 の Ready プロンプト ("Start (設定・表示・ランク)" + Space/Esc KeyCap) は
            // 二重表示になるので、V6UiMode.GameHud が有効な間だけ隠す (v5 モードでは出る)。
            // GameObject を止めると GameReadyView.Update も止まり再表示されない。
            // 破棄はしない (GameHudController が ready 参照を持つ)。
            if (legacyReadyCanvas != null && legacyReadyCanvas.gameObject.activeSelf)
            {
                legacyReadyCanvas.gameObject.SetActive(false);
            }

            if (legacyOverlayCanvas == null)
            {
                var canvases = FindObjectsOfType<Canvas>();
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i] != null && canvases[i].name == "V5OverlayCanvas")
                    {
                        legacyOverlayCanvas = canvases[i];
                        break;
                    }
                }
            }

            if (legacyOverlayCanvas != null)
            {
                var graphics = legacyOverlayCanvas.GetComponentsInChildren<Graphic>(true);
                for (int g = 0; g < graphics.Length; g++)
                {
                    if (graphics[g] != null && graphics[g].enabled)
                    {
                        graphics[g].enabled = false;
                    }
                }
            }
        }

        private void Update()
        {
            UpdatePause();
            UpdateLoading();
            if (hud != null)
            {
                // 表示所有: HUD は常時出す (Ready 中は歌詞位置に Space 案内、
                // 演奏中は歌詞)。別シーンの Ready 画面は廃止 (Issue #233)。
                if (!hud.Root.gameObject.activeSelf)
                {
                    hud.SetVisible(true);
                }
                hud.Refresh();
            }
        }

        /// <summary>
        /// v6 ポーズの開閉と入力。Esc で開閉、←→ で項目移動。
        /// 値の変更はキーボードには配線せず、行のステッパー / クリック (マウス) のみ。
        /// 既存の KeyActions の割当をそのまま使う (Judge / KeyBindings と同じ正本)。
        ///
        /// 開閉状態の正本は <see cref="V6PauseView.IsOpen"/> (v6 自前の状態)。
        /// かつて GameSceneManager.Menu を「ポーズが開いている」意味で読み書きしていたが、
        /// Menu はレガシー歌詞パネルのフラグ (MusicManager.Start が true を書く) で、
        /// 副作用で開閉判定が壊れるため読まない・書かない (v6 有効時は Menu を触らない)。
        /// Esc は押下エッジで 1 回だけ発火するので、開閉は 1 押下につき 1 回。
        /// </summary>
        private void UpdatePause()
        {
            if (pause == null) return;

            if (KeyActions.IsDown(KeyActions.KeyAction.Back))
            {
                if (pause.IsOpen)
                {
                    pause.Close();
                }
                else
                {
                    pause.Open();
                }
                return;
            }

            if (!pause.IsOpen) return;

            if (KeyActions.IsDown(KeyActions.KeyAction.MenuSwitch)) pause.SelectNextTab(1);
            if (KeyActions.IsDown(KeyActions.KeyAction.NavPrev)) pause.MoveSelection(-1);
            if (KeyActions.IsDown(KeyActions.KeyAction.NavNext)) pause.MoveSelection(1);
            var manager = FindObjectOfType<GameSceneManager>();
            if (manager == null) return;
            if (KeyActions.IsDown(KeyActions.KeyAction.MenuRetry)) { pause.Close(); manager.RetryGame(); }
            if (KeyActions.IsDown(KeyActions.KeyAction.MenuEnd)) { pause.Close(); manager.GameEnd(); }
            if (KeyActions.IsDown(KeyActions.KeyAction.MenuRetire)) { pause.Close(); manager.RetireGame(); }
            pause.Refresh();
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
                // 実進捗 (Issue #224): 譜面準備の done/total。未報告なら null
                // (不定インジケータ)。0 は既知として確定バーに出る。
                float? chartProgress = GameLoadingProgress.HasNotesProgress
                    ? GameLoadingProgress.NotesProgress
                    : (float?)null;
                loading.SetLoading(song != null ? song.title : "", song != null ? song.musician : "",
                    LoadJacket(song), chartProgress);
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

        // --- ジャケット (Issue #224) ---

        readonly Dictionary<int, Texture2D> jacketCache = new Dictionary<int, Texture2D>();

        /// <summary>
        /// 選択中の曲のジャケットを Resources から解決する
        /// (<see cref="V6LoadingViewLogic.JacketResourcePathOf"/>= 選曲画面と同じ対応表)。
        /// 無い曲は null — ビューが「無し」と正直に表示する。null もキャッシュする
        /// (毎フレームの Resources.Load を避ける)。
        /// </summary>
        private Texture2D LoadJacket(SongData song)
        {
            if (song == null) return null;
            if (jacketCache.TryGetValue(song.id, out var cached)) return cached;

            Texture2D texture = null;
            string path = V6LoadingViewLogic.JacketResourcePathOf(song.id);
            if (!string.IsNullOrEmpty(path))
            {
                texture = Resources.Load<Texture2D>(path);
            }

            jacketCache[song.id] = texture;
            return texture;
        }

        /// <summary>動画を減光するレイヤー (プレイフィールドより下)。</summary>
        private void BuildDimLayer()
        {
            // design §8 の Video Dim (#00000080 全画面)。
            // 積層は「動画 → 減光 → プレイフィールド → HUD」。
            // この Canvas は sortingOrder -3 で、プレイフィールド (-2) より後ろ、
            // 動画 (ワールド描画) より手前。
            if (!V6UiMode.VideoDim) return;

            var canvas = FlashBeat.UI.V5.V5GameUi.NewCanvas("V6GameDimCanvas", DimSortingOrder);
            dimCanvas = canvas.gameObject;
            dimCanvas.transform.SetParent(transform, false);

            var go = new GameObject("Video Dim", typeof(RectTransform));
            go.transform.SetParent(dimCanvas.transform, false);
            var dim = go.AddComponent<Image>();
            dim.color = FlashBeat.UI.V6.Game.Playfield.V6LaneGeometry.VideoDimColor;
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

            // 契約 §8: HUD は解像度ごとの固有フレーム (FHD 1920×1080 / 2K 2560×1440 /
            // Mac 2560×1664)。フレームは等倍 (scale 1) でスクリーン全体を覆い、
            // 引き伸ばし (等比拡大) は一切しない (Owner 指示 2026-11-08)。
            // 既知 3 解像度ではフレーム寸法 = そのプロファイルのフレーム寸法そのもの。
            // 部品は左上原点の design px 絶対座標 (= 実ピクセル) で置くので、
            // フレームを Canvas (= 実スクリーン解像度) に stretch で合わせる。
            hudFrame = FlashBeat.UI.V5.V5Ui.Node(canvas.gameObject, "V6 HUD Frame")
                .GetComponent<RectTransform>();
            hudFrame.anchorMin = Vector2.zero;
            hudFrame.anchorMax = Vector2.one;
            hudFrame.offsetMin = Vector2.zero;
            hudFrame.offsetMax = Vector2.zero;
            hudFrame.localScale = Vector3.one;

            hud = V6GameHudView.Create(hudFrame);

            // HUD 側の減光は自分のレイヤー (Video Dim canvas) が出す。
            // HUD 内の同名 Image は使わない (順序が崩れるため)。
            DisableInternalDim();
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
        ///
        /// design にはレーンフラッシュ (キー入力時のレーン光) が無いので、
        /// v6 有効時は "LaneLights" (LaneLight1..8 の親) ごと隠す。
        /// </summary>
        private void HideLegacyPlayfield()
        {
            if (!V6UiMode.Playfield) return;

            HideByName("Lane");
            HideByName("JudgeLine");
            // 親を先に隠す (子も同時に非表示になる)。
            HideByName("LaneLights");
            for (int i = 1; i <= 8; i++)
            {
                HideByName("LaneLight" + i);
            }
        }

        /// <summary>
        /// 名前でオブジェクトを非表示にする。
        /// GameObject.Find は非アクティブオブジェクトを見つけられないので、
        /// 見つからなかったときは非アクティブも含めて探し直す。
        /// </summary>
        private static void HideByName(string name)
        {
            var obj = GameObject.Find(name);
            if (obj != null)
            {
                obj.SetActive(false);
                return;
            }

            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i].name != name) continue;
                // Prefab アセット (scene が無い) は触らない。
                if (!all[i].scene.IsValid()) continue;
                all[i].SetActive(false);
            }
        }

        /// <summary>
        /// 旧シーン配置 UI (スコア、Space 案内など) を隠す。
        /// v6 の HUD が同じ場所を描くので、二枚重ねると読めなくなる。
        ///
        /// 対象は **シーン配置の "Canvas" だけ**。旧 v5 のランタイム Canvas のうち
        /// V5OverlayCanvas / V5PauseCanvas は未移植 (P1) の準備中・失敗・ポーズ表示
        /// として残す。V5ReadyCanvas は LateUpdate で隠す (v6 HUD が Ready 案内を持つ)。
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
