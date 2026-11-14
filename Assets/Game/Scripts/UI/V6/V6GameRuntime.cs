using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V6.Game.Hud;
using FlashBeat.UI.V6.Game.Loading;
using FlashBeat.UI.V6.Game.Pause;
using FlashBeat.UI.V6.Option;

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
        private GameSceneManager cachedSceneManager;

        /// <summary>検証用の固定表示中か (<see cref="ShowLoadingForAudit"/>)。</summary>
        private bool loadingAudit;

        /// <summary>隠し対象の旧 v5 ポーズ (キャッシュ。シーン再ロードで作り直す)。</summary>
        private FlashBeat.UI.V5.PauseMenuView legacyPause;

        /// <summary>隠し対象の旧 v5 ローディング / エラー Canvas (キャッシュ)。</summary>
        private Canvas legacyOverlayCanvas;

        /// <summary>隠し対象の旧 v5 Ready プロンプト Canvas (キャッシュ)。</summary>
        private Canvas legacyReadyCanvas;

        /// <summary>ローディング表示の開始判定に使う MusicManager (シーン内で 1 つ、遅延取得)。</summary>
        private MusicManager cachedMusicManager;
        private bool musicManagerSearched;

        /// <summary>
        /// 旧 v5 ローディング Canvas 配下の Graphic をキャッシュする (Issue #381)。
        ///
        /// 以前は LateUpdate のたびに <c>GetComponentsInChildren&lt;Graphic&gt;(true)</c>
        /// を呼んでいた。GameScene の生存中は Canvas 配下の構成が変わらないので、
        /// 初回だけ集めて使い回す (毎フレームの全走査をしない)。
        /// </summary>
        private Graphic[] legacyOverlayGraphics;

        /// <summary>
        /// 旧 v5 オブジェクトの検索を試みたフレーム数 (Issue #381)。
        ///
        /// 以前は <c>LateUpdate</c> が毎フレーム <c>FindObjectsOfType&lt;Canvas&gt;()</c>
        /// を呼んでいた。これはシーン全体を走査する。v5 のオブジェクトは
        /// GameHudController (実行順 100) の Start で作られるので数フレームで
        /// 見つかる。見つからないまま探し続けるのは無駄なので上限を置く。
        /// </summary>
        private int legacyLookupFrames;

        /// <summary>
        /// 旧 v5 オブジェクトを探すフレーム数の上限。GameHudController は
        /// Start (実行順 100) で作るので、1 秒あれば十分に足りる。
        /// </summary>
        private const int LegacyLookupFrameLimit = 60;

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
        /// シーンを離れるときに v6 のビューを破棄する。生成した GameObject はシーンと
        /// 一緒に消えるが、<see cref="V6LoadingView"/> が持つ RenderTexture と
        /// Sprite.Create の実体は GameObject に属さないため、Dispose を呼ばないと
        /// 曲を 1 回遊ぶごとにネイティブ領域が残る (FHD の出力先で約 8 MB)。
        /// </summary>
        private void OnDestroy()
        {
            if (loading != null)
            {
                loading.Dispose();
                loading = null;
            }

            if (pause != null)
            {
                pause.Dispose();
                pause = null;
            }
        }

        /// <summary>
        /// 旧 v5 のポーズ / ローディング / エラー表示を隠す (表示の所有は v6)。
        ///
        /// Issue #400: 状態検出と v5 UI 生成を分けた結果、
        /// **v6 構成では v5 の Ready / Loading / Error は生成されない**
        /// (GameHudController.Start が v6Hud のとき作らない)。したがって
        /// 以前の「毎フレーム隠し直す」処理は、v6 ポーズが無効なときの
        /// v5 ポーズと、シーンに残るレガシー (シーン配置の旧 UI) だけを見ればよい。
        ///
        /// 探索には上限を置き、見つかった後の非表示は維持する
        /// (v5 ポーズは Update で自分を開き直すことがあるため)。
        /// </summary>
        private void LateUpdate()
        {
            // 旧 v5 オブジェクトの**検索**には上限を置く (Issue #381)。
            bool mayLookUp = legacyLookupFrames < LegacyLookupFrameLimit;
            if (mayLookUp)
            {
                legacyLookupFrames++;
            }

            if (legacyPause == null && mayLookUp)
            {
                legacyPause = FindObjectOfType<FlashBeat.UI.V5.PauseMenuView>();
            }

            // v6 ポーズが無効 (V6PauseView.Enabled = false) のときは v5 ポーズが役割を持つ。
            if (legacyPause != null && V6PauseView.Enabled && legacyPause.gameObject.activeSelf)
            {
                legacyPause.gameObject.SetActive(false);
            }

            // Issue #400: v6 構成では V5ReadyCanvas / V5OverlayCanvas を作らないので、
            // それらを探して毎フレーム隠す必要は無い (GamePlayPhaseService が
            // 生成前の旧 UI を 1 回掃除する)。v5 ポーズだけは上で維持する。
        }

        /// <summary>
        /// キャッシュ済みの Graphic のうち、有効なものを無効化する。
        /// すべて無効なら true を返す。配列は呼び出し側が持つので、ここでは
        /// 新規確保しない (Issue #381: 毎フレームの再帰的な収集を無くす)。
        /// </summary>
        public static bool HideLegacyOverlayGraphics(Graphic[] graphics)
        {
            if (graphics == null)
            {
                return true;
            }

            bool allHidden = true;
            for (int g = 0; g < graphics.Length; g++)
            {
                Graphic graphic = graphics[g];
                if (graphic == null)
                {
                    continue; // 破棄された要素は数えない (MissingReference を避ける)
                }

                if (graphic.enabled)
                {
                    graphic.enabled = false;
                }

                if (graphic.enabled)
                {
                    allHidden = false;
                }
            }

            return allHidden;
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

                // Issue #393: 更新 (Refresh) の所有者は HUD 自身の Update。
                // ここから Refresh を呼ぶと 1 フレームに 2 回走っていた
                // (V6GameRuntime.Update と V6GameHudView.Update の両方)。
                // 表示の出し分けだけを行い、データ反映はビューへ任せる。
                hud.RequestRefresh();
            }
        }

        /// <summary>
        /// v6 ポーズの開閉と入力。Esc で開閉、↑↓ で項目移動、←→ で値の変更。
        /// 正本 uDNC0 の凡例 (↑↓ 選択 / ←→ 変更 / Tab タブ切替 / Esc 再開) が
        /// そのまま仕様。矢印は KeyActions に ↑↓ の割当が無いため固定キー
        /// (Title の H/R と同じ扱い。←→ を NavPrev/NavNext に流用すると
        /// 「選択」と「変更」が同じキーに重なる)。
        ///
        /// 開閉状態の正本は <see cref="V6PauseView.IsOpen"/> (v6 自前の状態)。
        /// かつて GameSceneManager.Menu を「ポーズが開いている」意味で読み書きしていたが、
        /// Menu はレガシー歌詞パネルのフラグ (MusicManager.Start が true を書く) で、
        /// 副作用で開閉判定が壊れるため読まない・書かない (v6 有効時は Menu を触らない)。
        /// Esc は押下エッジで 1 回だけ発火するので、開閉は 1 押下につき 1 回。
        /// キー設定 (KeyConfigUI) が前面のときは、その入力 (Esc=閉じる) を
        /// ポーズに渡さない (ポーズとキー設定が同時に閉じる事故を防ぐ)。
        /// </summary>
        private void UpdatePause()
        {
            if (pause == null) return;

            // 再開カウントダウン中は V6ResumeCountdown だけが入力 (Esc = 取り消し) を扱う。
            // ここで Back を読むと同じフレームにポーズが開き直すため、先に抜ける。
            if (V6ResumeCountdown.IsRunning) return;

            // オーバーレイを開閉したフレームのキーは背面へ流さない (SceneInputGate の契約)。
            // KeyConfigUI.Update が先に走って Esc で閉じた場合、IsOpen が同じフレームで
            // false になり、ここが Back を拾ってポーズまで閉じてしまう。Update の実行順は
            // 保証されないため、フレーム単位のゲートをここでも見る。
            if (!FlashBeat.UI.SceneInputGate.InputAllowed)
            {
                return;
            }

            if (FlashBeat.UI.V6.Common.V6KeyConfigModal.BlocksInput)
            {
                // v6 のキー設定モーダル (Issue #376) が Esc / Enter / 矢印を消費する。
                return;
            }

            if (KeyConfigUI.IsOpen)
            {
                // キー設定中の F1 = 編集キャンセル (保存済みの割当へ戻して閉じる)。
                // Option 画面の Update と同じ扱い (V6OptionView.CancelKeyConfigEdit)。
                // KeyConfigUI は Esc / F2 しか消費しないので F1 はここで受け取れる。
                if (Input.GetKeyDown(KeyCode.F1))
                {
                    V6OptionView.CancelKeyConfigEdit();
                }

                return;
            }

            // Key Layout リセットの確認ダイアログ表示中は、背面のポーズ操作を止める。
            // Scrim はマウスしか遮らないため、Esc=再開 / R=リトライ / Backspace=リタイア /
            // 矢印=行移動 がダイアログ越しに実行されてしまう。Esc は確認の取り消しに使う。
            if (pause.HasConfirmDialog)
            {
                if (KeyActions.IsDown(KeyActions.KeyAction.Back))
                {
                    pause.CancelResetKeyLayout();
                }

                return;
            }

            if (KeyActions.IsDown(KeyActions.KeyAction.Back))
            {
                if (pause.IsOpen)
                {
                    pause.RequestResume();
                }
                else
                {
                    pause.Open();
                }
                return;
            }

            if (!pause.IsOpen) return;

            if (KeyActions.IsDown(KeyActions.KeyAction.MenuSwitch)) pause.SelectNextTab(1);
            if (Input.GetKeyDown(KeyCode.UpArrow)) pause.MoveSelection(-1);
            if (Input.GetKeyDown(KeyCode.DownArrow)) pause.MoveSelection(1);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) pause.AdjustSelected(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) pause.AdjustSelected(1);
            // 決定 (Space): キー設定を開く等、行の Click を持つ行をキーボードでも
            // 実行できるようにする (Issue #376 — マウス専用だった行動経路の補完)。
            if (KeyActions.IsDown(KeyActions.KeyAction.Confirm)) pause.ActivateSelected();
            // ポーズ中は毎フレーム来るため、シーン全体検索を繰り返さず 1 度だけ解決する。
            if (cachedSceneManager == null) cachedSceneManager = FindAnyObjectByType<GameSceneManager>();
            var manager = cachedSceneManager;
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
            if (loadingAudit) return; // 検証用に固定表示中 (実状態で上書きしない)
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
                // Issue #378: シーンが立ち上がってから MusicManager の準備が
                // Preparing に立つまでの隙間でも、黒いまま無反応にしない。
                // メディア準備が始まる前 (MediaReady 前) はローディング画面を
                // 出し続け、Ready 待ち (MediaReady 後) やプレイ中は隠す。
                if (!musicManagerSearched)
                {
                    musicManagerSearched = true;
                    cachedMusicManager = FindFirstObjectByType<MusicManager>();
                }
                var music = cachedMusicManager;
                bool sessionStarting = music != null
                    && !music.MediaReady
                    && !music.PlaybackStarted
                    && !GManager.played;
                if (sessionStarting)
                {
                    var song = GManager.GetSong(GManager.songID);
                    float? chartProgress = GameLoadingProgress.HasNotesProgress
                        ? GameLoadingProgress.NotesProgress
                        : (float?)null;
                    loading.SetLoading(song != null ? song.title : "", song != null ? song.musician : "",
                        LoadJacket(song), chartProgress);
                    loading.SetVisible(true);
                }
                else
                {
                    loading.SetVisible(false);
                }
            }
        }

        /// <summary>
        /// 検証用 (自動撮影): ローディング画面を実データで固定表示する。
        ///
        /// 通常の表示は毎フレームの phase (Preparing / Failed) が決めるため、
        /// GameScene の自動撮影では数秒で Hidden に戻ってしまう。
        /// ここで固定している間は <see cref="UpdateLoading"/> の上書きを止める
        /// (解除は <see cref="HideLoadingForAudit"/>)。
        /// </summary>
        /// <param name="songId">表示する曲 (カタログ index、design と同じデータで撮るなら 5)。</param>
        /// <param name="chartProgress">譜面準備の進捗 (null = 不明 → 不定インジケータ)。</param>
        public void ShowLoadingForAudit(int songId, float? chartProgress)
        {
            if (loading == null) return;
            var song = GManager.GetSong(songId);
            loadingAudit = true;
            loading.SetLoading(song != null ? song.title : "", song != null ? song.musician : "",
                LoadJacket(song), chartProgress);
            loading.SetVisible(true);
        }

        /// <summary>検証用の固定表示を解除する (実状態の表示に戻す)。</summary>
        public void HideLoadingForAudit()
        {
            loadingAudit = false;
            if (loading != null) loading.SetVisible(false);
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
