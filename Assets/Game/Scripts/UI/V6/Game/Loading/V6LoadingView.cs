using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Game.Loading
{
    /// <summary>
    /// v6 曲ローディング画面 (Issue #224, design/flashbeat-ui.pen の `g4Q3F`)。
    ///
    /// - design: タイトル欄は空、右上にジャケット 56×56 + 曲名 fs22 / 作家 fs16、
    ///   フッターの凡例は Esc「戻る」のみ、補助 / 主操作は非表示、メインは空。
    /// - 進捗の表現は design に無い → この実装が決める: 中央に「準備中…」+ バー。
    ///   実進捗 (譜面準備, Issue #224) が渡されたら確定バー + 割合表示、
    ///   不明のときだけ不定バーへフォールバックする。
    /// - 失敗時はメッセージ + 「再試行」(RetryGame) + 「選曲にもどる」(実遷移)。
    /// - 状態は SetLoading / SetError で受け取る (統合担当が MusicManager /
    ///   HudPlayState の状態から呼ぶ)。キー入力は持たない (Esc は既存の
    ///   HudPlayStatus が「戻る」を担う)。
    /// - すべてコードから uGUI を生成。寸法は design px (1728x972)。
    ///   共通フレームを 1:1 (等倍) で画面中央に置く
    ///   (<see cref="V6LoadingViewLogic.FrameOrigin"/>、ポーズと同じ規約)。
    ///   等比拡大 (min(W/1728, H/972)) は行わない — design は各解像度ごとに
    ///   フレーム原点 ((w-1728)/2, (h-972)/2) を固定している。
    /// </summary>
    public sealed class V6LoadingView
    {
        /// <summary>現在生成されている v6 ローディング画面。統合側・検証から参照する。</summary>
        public static V6LoadingView Current { get; private set; }

        // --- design px 定数 (flashbeat-v6-nodes.json g4Q3F / rw7zx) ---
        private const float FrameW = 1728f;
        private const float FrameH = 972f;

        // ヘッダー帯の寸法は V6Header (Width/Height/FramePos) が正本。
        private const float FooterX = 24f, FooterY = 876f;

        private const float TrackW = 464f;
        private const float TrackH = 8f;
        private const float FillW = 160f;

        private GameObject rootGo;
        private VideoPlayer videoPlayer;
        private RawImage videoRaw;
        private Image bgImage;
        private bool videoErrorLogged;
        private bool videoErrored;
        private bool shown;
        private V6LoadingVideoSurface? lastSurface;
        private RectTransform frame;
        private V6LoadingViewDriver driver;
        private TextMeshProUGUI headline;
        private TextMeshProUGUI detail;
        private RectTransform fill;
        private GameObject progressGroup;
        private GameObject errorGroup;
        private TextMeshProUGUI songTitle;
        private TextMeshProUGUI songArtist;
        private Image jacketImage;
        private TextMeshProUGUI noJacketLabel;
        private float? progress;            // null = 不明 (不定インジケータ)
        private Texture2D jacketTexture;    // スプライトの生成元 (変化時だけ再生成)
        private Sprite jacketSprite;        // ApplySong で作ったスプライト (Dispose で破棄)
        private RenderTexture videoTexture; // RenderTexture モードの出力先 (Dispose で破棄)
        private bool errorShown;
        private bool leaving;
        private GameSceneManager sceneManager;

        private V6LoadingView()
        {
        }

        /// <summary>View を生成する (非表示のまま)。統合担当から呼ぶ。</summary>
        public static V6LoadingView Create(RectTransform parent)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var view = new V6LoadingView();
            view.Build(parent);
            Current = view;
            return view;
        }

        public RectTransform Root
        {
            get { return frame; }
        }

        /// <summary>生成したビューを破棄する。</summary>
        public void Dispose()
        {
            // Issue #398: jacketSprite は V6JacketProvider が所有する (原 Texture +
            // crop 矩形で共有)。ここで Destroy すると、同じ画像を使っている他の画面が
            // 白くなる。手放すだけにする。
            jacketSprite = null;
            jacketTexture = null;

            if (videoTexture != null)
            {
                if (videoPlayer != null)
                {
                    videoPlayer.targetTexture = null;
                }

                if (videoTexture.IsCreated())
                {
                    videoTexture.Release();
                }

                UnityEngine.Object.Destroy(videoTexture);
                videoTexture = null;
            }

            if (rootGo != null)
            {
                V5GameUi.DestroyNow(rootGo);
            }

            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }
        }

        /// <summary>検証ハーネス用: ローディング映像が準備できたか。</summary>
        internal bool VideoPreparedForAudit
        {
            get { return videoPlayer != null && videoPlayer.isPrepared; }
        }

        /// <summary>検証ハーネス用: 映像に使えるクリップがあるか。</summary>
        internal bool VideoClipForAudit
        {
            get { return videoPlayer != null && videoPlayer.clip != null; }
        }

        /// <summary>検証ハーネス用: 映像面 (RawImage) が実際に表示されているか。</summary>
        internal bool VideoVisibleForAudit
        {
            get { return videoRaw != null && videoRaw.enabled; }
        }

        /// <summary>検証ハーネス用: 直近に選んだ映像面の種類。</summary>
        internal string VideoSurfaceForAudit
        {
            get { return lastSurface.HasValue ? lastSurface.Value.ToString() : "none"; }
        }

        public void SetVisible(bool visible)
        {
            bool wasShown = shown;
            shown = visible;
            if (rootGo != null)
            {
                rootGo.SetActive(visible);
            }

            if (visible)
            {
                UpdatePlacement();
                // 画面に出始めた瞬間だけ初期化する。SetVisible(true) は毎フレーム
                // 呼ばれるので、ここで Play を呼び直したり警告フラグを戻したり
                // すると、途中で届いた映像エラーが毎回鳴り直す (review Finding A)。
                if (!wasShown)
                {
                    videoErrorLogged = false;
                    videoErrored = false;
                    // 映像面を作り直す (テクスチャをreleasedした状態から戻す)。
                    lastSurface = null;
                    StartVideo();
                }
            }
            else
            {
                if (wasShown)
                {
                    StopVideo();
                }
            }

            ApplyVideoSurface();
        }

        /// <summary>
        /// ローディング映像を頭から再生する。clip が無ければ何もしない
        /// (その場合は背景のみ — 白画面は出さない)。
        /// </summary>
        private void StartVideo()
        {
            if (videoPlayer == null || videoPlayer.clip == null)
            {
                return;
            }

            if (videoTexture != null)
            {
                if (!videoTexture.IsCreated())
                {
                    videoTexture.Create();
                }

                // 出力先を張ってから再生する (Prepare が targetTexture を要求する)。
                videoPlayer.targetTexture = videoTexture;
            }

            videoPlayer.Play();
        }

        /// <summary>
        /// 非表示になった時に映像を止めテクスチャを解放する (素の映像を
        /// 裏で再生し続ける / 古いテクスチャを保持したままにしない)。
        /// </summary>
        private void StopVideo()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                videoPlayer.targetTexture = null;
            }

            if (videoRaw != null)
            {
                videoRaw.texture = null;
                videoRaw.enabled = false;
            }

            if (videoTexture != null && videoTexture.IsCreated())
            {
                videoTexture.Release();
            }
        }

        /// <summary>
        /// 映像面の状態を決める (純ロジック — <see cref="V6LoadingVideoPolicy"/> に
        /// 抽出して EditMode テストする)。
        /// </summary>
        private void ApplyVideoSurface()
        {
            if (videoRaw == null)
            {
                return;
            }

            bool hasClip = videoPlayer != null && videoPlayer.clip != null;
            bool prepared = videoPlayer != null && videoPlayer.isPrepared;
            V6LoadingVideoSurface surface = V6LoadingVideoPolicy.Decide(shown, hasClip, prepared, videoErrored);
            if (surface == lastSurface)
            {
                return;
            }

            lastSurface = surface;

            switch (surface)
            {
                case V6LoadingVideoSurface.Video:
                    if (videoTexture != null)
                    {
                        if (!videoTexture.IsCreated())
                        {
                            videoTexture.Create();
                        }

                        videoPlayer.targetTexture = videoTexture;
                        videoRaw.texture = videoTexture;
                    }

                    videoRaw.enabled = true;
                    break;

                case V6LoadingVideoSurface.Background:
                    // 映像が無い / 準備できていない / エラー → 白い RawImage を残さない。
                    videoRaw.enabled = false;
                    videoRaw.texture = null;
                    break;

                default:
                    // Hidden: ビューごと隠れている — テクスチャも背景も残さない。
                    videoRaw.enabled = false;
                    videoRaw.texture = null;
                    break;
            }

            // 背景は常に Commerce 背景 (fallback) として残す。映像が無いときは
            // こちらだけが画面を埋め、白い画面は出ない。
            if (bgImage != null)
            {
                bgImage.enabled = surface != V6LoadingVideoSurface.Hidden;
            }
        }

        /// <summary>ローディング映像の再生エラー。無音で推移しない (Issue #233)。</summary>
        private void OnVideoError(VideoPlayer source, string message)
        {
            videoErrored = true;
            ApplyVideoSurface();
            if (videoErrorLogged) return;
            videoErrorLogged = true;
            Debug.LogWarning($"[V6Loading] loading video unavailable: {message}");
        }

        /// <summary>ロード中の表示 (実データ: 曲情報は統合担当が渡す)。</summary>
        /// <param name="chartProgress">
        /// 譜面準備の実進捗 (0..1)。null は進捗不明 → 不定インジケータにフォールバック。
        /// 0 は既知 (準備が始まったばかり) として確定バーで出す。
        /// </param>
        public void SetLoading(string songTitle, string artist, Texture2D jacket, float? chartProgress)
        {
            errorShown = false;
            leaving = false;
            progress = chartProgress;
            if (headline != null)
            {
                headline.text = V6LoadingViewLogic.LoadingHeadline();
            }

            if (detail != null)
            {
                // 進捗が既知なら実割合、不明なら従来の補足テキスト。
                detail.text = chartProgress.HasValue
                    ? V6LoadingViewLogic.ProgressDetail(chartProgress.Value)
                    : V6LoadingViewLogic.LoadingDetail();
            }

            if (progressGroup != null)
            {
                progressGroup.SetActive(true);
            }

            if (errorGroup != null)
            {
                errorGroup.SetActive(false);
            }

            ApplySong(songTitle, artist, jacket);
        }

        /// <summary>失敗の表示 + 「選曲にもどる」の導線 (message は実状態から)。</summary>
        public void SetError(string message)
        {
            errorShown = true;
            leaving = false;
            if (headline != null)
            {
                headline.text = "読み込み / 再生に失敗しました";
            }

            if (detail != null)
            {
                detail.text = message != null && message.Length > 0 ? message : "選曲画面へ戻れます";
            }

            if (progressGroup != null)
            {
                progressGroup.SetActive(false);
            }

            if (errorGroup != null)
            {
                errorGroup.SetActive(true);
            }
        }

        /// <summary>選曲画面へ戻る (既存 HudPlayStatus.BackToSelect と同じ遷移)。</summary>
        public void Back()
        {
            if (leaving)
            {
                return;
            }

            leaving = true;
            if (sceneManager != null)
            {
                sceneManager.RetireGame();
            }
            else
            {
                Initiate.Fade("SelectScene", Color.black, 1.0f);
            }
        }

        /// <summary>
        /// 同じ曲を再試行する (Issue #224)。実在する操作だけを割り当てる:
        /// GameSceneManager.RetryGame (GManager.ResetSession + GameScene を読み込み直す)。
        /// シーン読み込み直しで NotesManager/MusicManager が最初から準備し直す。
        /// シーンマネージャが無い場合は同じ遷移を直接行う。
        /// </summary>
        public void Retry()
        {
            if (leaving)
            {
                return;
            }

            leaving = true;
            if (sceneManager != null)
            {
                sceneManager.RetryGame();
            }
            else
            {
                GManager.ResetSession();
                Initiate.Fade("GameScene", Color.black, 1.0f);
            }
        }

        // --- 構築 ---

        private void Build(RectTransform parent)
        {
            sceneManager = UnityEngine.Object.FindFirstObjectByType<GameSceneManager>();

            rootGo = V5Ui.Node(parent.gameObject, "V6 Loading View");
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // design: フレーム全面が背景 (ロード中はゲームの背後を隠す)。
            // 映像が使えない時はこの背景がそのまま見える (白い画面を出さない)。
            // 上下グラデーションにするのは、映像の準備が終わるまでの数フレームを
            // 単色ではなく設計された面として見せるため (Issue #378)。
            var bg = V5Ui.Node(rootGo, "Loading Background");
            V5Ui.Fill(bg.GetComponent<RectTransform>(), 0f);
            bgImage = bg.AddComponent<Image>();
            bgImage.sprite = GradientSprite();
            bgImage.type = Image.Type.Simple;
            bgImage.color = Color.white;
            bgImage.raycastTarget = true;


            // ローディング映像 (design: ローディング画面は動画を再生するだけ。
            // Assets/Game/Resources/Videos/loading.mp4 = 自作素材、同梱する)。
            // RenderTexture モードで 1920x1080 のテクスチャへ描き、同じテクスチャを
            // RawImage に出す (Issue #224)。
            var videoBg = V5Ui.Node(rootGo, "Loading Video");
            V5Ui.Fill(videoBg.GetComponent<RectTransform>(), 0f);
            videoRaw = videoBg.AddComponent<RawImage>();
            videoRaw.raycastTarget = false;
            videoRaw.color = Color.white;
            videoTexture = new RenderTexture(1920, 1080, 0);
            videoPlayer = videoBg.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            // APIOnly + targetTexture は macOS プレイヤーで黒いテクスチャのまま
            // 描画されなかった (2026-11-12 実機)。RenderTexture モードは
            // テクスチャへの描画を VideoPlayer 側が受け持つ標準経路。
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.targetTexture = videoTexture;
            videoPlayer.clip = Resources.Load<VideoClip>("Videos/loading");
            videoPlayer.errorReceived += OnVideoError;
            // 映像が準備できるまで RawImage は出さない (白画面回避)。
            videoRaw.enabled = false;
            videoRaw.texture = null;

            frame = V5Ui.Node(rootGo, "V6 Loading Frame 1728x972").GetComponent<RectTransform>();
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.zero;
            frame.pivot = Vector2.zero;
            frame.sizeDelta = new Vector2(FrameW, FrameH);

            BuildHeader();
            BuildMain();
            BuildFooter();

            driver = rootGo.AddComponent<V6LoadingViewDriver>();
            driver.View = this;

            rootGo.SetActive(false);
            UpdatePlacement();
        }

        static Sprite loadingGradient;

        /// <summary>
        /// ローディング背景の上下グラデーション (上が #141414 / 下が #232323)。
        /// 映像の準備が終わるまでの面を「単色の黒」ではなく設計された面にする。
        /// 1×2 のテクスチャを伸ばすので、サイズに関係なく滑らかに出る。
        /// </summary>
        static Sprite GradientSprite()
        {
            if (loadingGradient != null)
            {
                return loadingGradient;
            }

            var tex = new Texture2D(1, 2, TextureFormat.RGBA32, false);
            tex.name = "LoadingGradient";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.SetPixel(0, 0, V5Tokens.FromHex("#232323"));
            tex.SetPixel(0, 1, V5Tokens.FromHex("#141414"));
            tex.Apply(false, false);

            loadingGradient = Sprite.Create(tex, new Rect(0f, 0f, 1f, 2f), new Vector2(0.5f, 0.5f), 1f);
            loadingGradient.name = "LoadingGradient";
            loadingGradient.hideFlags = HideFlags.HideAndDontSave;
            return loadingGradient;
        }

        private void BuildHeader()
        {
            // 帯 + 戻るボタンは共通部品 (Issue #385)。帯は素の RectTransform
            // (従来実装どおり panel=false)。design zUcsi: (0,20) 56×56 $k4。
            var header = V6Header.Create(frame.gameObject, false);

            string backKeyText = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Back);
            if (string.IsNullOrEmpty(backKeyText))
            {
                backKeyText = "Esc";
            }

            V6Header.BackButton(header, "Back", backKeyText, Back);

            // design: タイトル欄は空 (0×0)。

            // 右上 (JJwdM): ジャケット 56×56 + 曲名 fs22 / 作家 fs16。
            var info = V5Ui.Node(header, "Song Info");
            PlaceAt(info, 1080f, 0f, new Vector2(300f, V6Header.Height));
            var infoLayout = info.AddComponent<HorizontalLayoutGroup>();
            infoLayout.spacing = 12f;
            infoLayout.childAlignment = TextAnchor.MiddleLeft;
            infoLayout.childControlWidth = true;
            infoLayout.childControlHeight = true;
            infoLayout.childForceExpandWidth = false;
            infoLayout.childForceExpandHeight = false;

            var jacket = V5Ui.Panel(info, "Jacket", new Vector2(56f, 56f), V5Tokens.Key2, 4f);
            LayoutItem(jacket, 56f, 56f);
            jacketImage = jacket.GetComponent<Image>();
            jacketImage.raycastTarget = false;
            // ジャケットが無い曲は「無し」を正直に表示する (空欄/壊れた画像にしない)。
            noJacketLabel = V5Ui.Text(jacket, "No Jacket", "無し", 16f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            noJacketLabel.rectTransform.anchorMin = Vector2.zero;
            noJacketLabel.rectTransform.anchorMax = Vector2.one;
            noJacketLabel.rectTransform.offsetMin = Vector2.zero;
            noJacketLabel.rectTransform.offsetMax = Vector2.zero;
            noJacketLabel.fontSize = 14f;
            noJacketLabel.gameObject.SetActive(false);

            // design Frw5C: Texts (68,20) 113×57、Song Title (0,0) 111×32 fs22、
            // Song Artist (0,34) 113×23 fs16 — どちらも左上揃え。
            // 旧実装は VerticalLayoutGroup の中央寄せで、2 行が design より
            // 数 px 上に寄っていた (設計の矩形をそのまま使う)。
            var texts = V5Ui.Node(info, "Texts");
            LayoutItem(texts, 113f, 56f);
            songTitle = V5Ui.Text(texts, "Song Title", "", 22f, V5Tokens.Text, TextAlignmentOptions.TopLeft);
            songTitle.textWrappingMode = TextWrappingModes.NoWrap;
            songTitle.overflowMode = TextOverflowModes.Ellipsis;
            PlaceAt(songTitle.gameObject, 0f, 0f, new Vector2(111f, 32f));
            // h32 は行送り (fs22 × 1.448 = 31.9) とほぼ同じ。Ellipsis の 0 行化を
            // 避けるため上下 margin を広げる (矩形とベースラインは変えない)。
            V5Ui.FitEllipsisToLineHeight(songTitle, 32f);
            songArtist = V5Ui.Text(texts, "Song Artist", "", 16f, V5Tokens.TextMuted, TextAlignmentOptions.TopLeft);
            songArtist.textWrappingMode = TextWrappingModes.NoWrap;
            songArtist.overflowMode = TextOverflowModes.Ellipsis;
            PlaceAt(songArtist.gameObject, 0f, 34f, new Vector2(113f, 23f));
            V5Ui.FitEllipsisToLineHeight(songArtist, 23f);
        }

        /// <summary>design ではメイン空。進捗の表現はこの実装が決めた:
        /// 中央に見出し + 補足 + 不定バー (ロード中) / 失敗メッセージ + 「選曲にもどる」。</summary>
        private void BuildMain()
        {
            var main = V5Ui.Node(frame.gameObject, "Main");
            PlaceAt(main, 24f, 96f, new Vector2(1680f, 780f));

            var center = V5Ui.Node(main, "Progress");
            PlaceAt(center, 608f, 240f, new Vector2(464f, 300f));
            progressGroup = center;

            headline = V5Ui.Text(center, "Headline", V6LoadingViewLogic.LoadingHeadline(), 30f,
                V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceAt(headline.gameObject, 0f, 0f, new Vector2(464f, 40f));

            detail = V5Ui.Text(center, "Detail", V6LoadingViewLogic.LoadingDetail(), 18f,
                V5Tokens.TextMuted, TextAlignmentOptions.Center);
            PlaceAt(detail.gameObject, 0f, 52f, new Vector2(464f, 24f));

            var track = V5Ui.Panel(center, "Track", new Vector2(TrackW, TrackH), V5Tokens.Key0, 4f);
            PlaceAt(track, 0f, 108f);
            fill = V5Ui.Panel(track, "Fill", new Vector2(FillW, TrackH), V5Tokens.Text, 4f)
                .GetComponent<RectTransform>();
            fill.anchorMin = new Vector2(0f, 0.5f);
            fill.anchorMax = new Vector2(0f, 0.5f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;

            // 失敗時: メッセージ + 「再試行」(実遷移: RetryGame) + 「選曲にもどる」(実遷移)。
            var error = V5Ui.Node(main, "Error");
            PlaceAt(error, 608f, 240f, new Vector2(464f, 300f));
            errorGroup = error;

            var errorHeadline = V5Ui.Text(error, "Headline", "読み込み / 再生に失敗しました", 30f,
                V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceAt(errorHeadline.gameObject, 0f, 0f, new Vector2(464f, 40f));

            var errorDetail = V5Ui.Text(error, "Detail", "", 18f,
                V5Tokens.TextMuted, TextAlignmentOptions.Center);
            PlaceAt(errorDetail.gameObject, 0f, 52f, new Vector2(464f, 24f));

            var retryBtn = V5Ui.Panel(error, "Retry", new Vector2(200f, 56f), V5Tokens.ButtonPrimaryBg, 8f);
            PlaceAt(retryBtn, 32f, 120f);
            var retryLayout = retryBtn.AddComponent<HorizontalLayoutGroup>();
            retryLayout.spacing = 12f;
            retryLayout.childAlignment = TextAnchor.MiddleCenter;
            retryLayout.childControlWidth = true;
            retryLayout.childControlHeight = true;
            retryLayout.childForceExpandWidth = false;
            retryLayout.childForceExpandHeight = false;
            var retryLabel = V5Ui.Text(retryBtn, "Label", "再試行", 30f,
                V5Tokens.ButtonPrimaryLabel, TextAlignmentOptions.Center, FontStyles.Bold);
            LayoutItem(retryLabel.gameObject, 0f, 34f);
            var retryButton = retryBtn.AddComponent<Button>();
            retryButton.targetGraphic = retryBtn.GetComponent<Image>();
            V6ButtonPressFeedback.Attach(retryBtn);
            // 再試行は GameScene への遷移なので、押下リングが見えるだけ遅らせる。
            retryButton.onClick.AddListener(() => V6ButtonPressFeedback.RunAfterFeedback(retryBtn, Retry));

            var backBtn = V5Ui.Panel(error, "Back To Select", new Vector2(200f, 56f), V5Tokens.ButtonPrimaryBg, 8f);
            PlaceAt(backBtn, 232f, 120f);
            var btnLayout = backBtn.AddComponent<HorizontalLayoutGroup>();
            btnLayout.spacing = 12f;
            btnLayout.childAlignment = TextAnchor.MiddleCenter;
            btnLayout.childControlWidth = true;
            btnLayout.childControlHeight = true;
            btnLayout.childForceExpandWidth = false;
            btnLayout.childForceExpandHeight = false;
            var btnLabel = V5Ui.Text(backBtn, "Label", "選曲にもどる", 26f,
                V5Tokens.ButtonPrimaryLabel, TextAlignmentOptions.Center, FontStyles.Bold);
            LayoutItem(btnLabel.gameObject, 0f, 34f);
            var btnButton = backBtn.AddComponent<Button>();
            btnButton.targetGraphic = backBtn.GetComponent<Image>();
            V6ButtonPressFeedback.Attach(backBtn);
            btnButton.onClick.AddListener(() => V6ButtonPressFeedback.RunAfterFeedback(backBtn, Back));

            errorGroup.SetActive(false);
        }

        private void BuildFooter()
        {
            var footer = V5Ui.Node(frame.gameObject, "Footer");
            PlaceAt(footer, FooterX, FooterY, new Vector2(1680f, 96f));

            // design: 凡例は Esc「戻る」だけ (決定 / 曲送りは 0×0、補助 / 主操作は非表示)。
            // 位置はフレーム x56 — 正本は 0×0 の 決定 / 曲送り を据え置き、項目間 gap16 で
            // 戻る まで進めるため (旧実装は x24 に置いていた = 32px 左)。
            // FooterLegendX はフレーム座標なので、フッターゾーン (x24) のローカルに直す。
            // 寸法は Title の凡例と同じ別設計 (KeyCap h36 / pad[0,10] / 項内 gap10 /
            // 項目間 gap16 / fs22)。他画面 (h32 / pad[0,8] / gap8 / gap24) ではない。
            // キー表示は実際の割当 (Back = Esc) を出す — 変更されても嘘のキーを出さない。
            string backKey = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Back);
            if (string.IsNullOrEmpty(backKey))
            {
                backKey = "Esc";
            }

            V6Legend.Item(footer, V6LoadingViewLogic.FooterLegendX - FooterX,
                V6LoadingViewLogic.FooterLegendY, backKey, "戻る",
                V6LoadingViewLogic.FooterLegendCapHeight, V6Controls.KeyCapPaddingX,
                V6LoadingViewLogic.FooterLegendInnerGap, V6LoadingViewLogic.FooterLegendItemGap,
                V6LoadingViewLogic.FooterLegendFontSize, V6Tokens.Key4, V6Tokens.TextMuted);
        }

        private void ApplySong(string title, string artist, Texture2D jacket)
        {
            if (songTitle != null)
            {
                songTitle.text = title != null ? title : "";
            }

            if (songArtist != null)
            {
                songArtist.text = artist != null ? artist : "";
            }

            if (jacketImage != null)
            {
                // SetLoading は毎フレーム呼ばれる。テクスチャが同じ間は
                // Sprite.Create を繰り返さない (実体の作り直しはリークになる)。
                if (!ReferenceEquals(jacketTexture, jacket))
                {
                    jacketTexture = jacket;
                    // Issue #398: design N3HPJ は 56×56 を mode:"cover" で埋める。
                    // 実素材は 16:9 (320×180) なので中心を正方形に切り出してから
                    // Sprite 化する。切り抜きは provider が所有する (同じ原 Texture +
                    // 同じ矩形なら同じ実体を返すので、画面を出入りしても増えない)。
                    // 全体をそのまま入れると 56×31.5 の帯になり、design の
                    // 正方形から大きく外れる (r15 実機で確認)。
                    jacketSprite = V6JacketProvider.SquareCrop(jacket, "V6LoadingJacket");
                }

                jacketImage.sprite = jacketSprite;
                if (jacket != null)
                {
                    jacketImage.type = Image.Type.Simple;
                    jacketImage.preserveAspect = true;
                    jacketImage.color = Color.white;
                }
                else
                {
                    jacketImage.color = V5Tokens.Key2;
                }
            }

            if (noJacketLabel != null)
            {
                noJacketLabel.gameObject.SetActive(jacket == null);
            }
        }

        // --- 配置 / 毎フレーム ---

        /// <summary>
        /// 共通フレームを 1:1 (等倍) で画面中央に置く
        /// (<see cref="V6LoadingViewLogic.FrameOrigin"/>、ポーズと同じ規約)。
        /// 等比拡大 (min(W/1728, H/972)) は行わない。
        /// </summary>
        private void UpdatePlacement()
        {
            if (frame == null)
            {
                return;
            }

            int w = Screen.width;
            int h = Screen.height;
            Vector2 origin = V6LoadingViewLogic.FrameOrigin(w, h);
            frame.localScale = Vector3.one;
            // anchor/pivot (0,0) = 親 (全画面 root) の左下基準。design 原点は左上。
            frame.anchoredPosition = new Vector2(origin.x, h - origin.y - FrameH);
        }

        internal void Tick()
        {
            UpdatePlacement();
            // VideoPlayer の準備完了は非同期なので、準備できた瞬間に映像面が
            // 動画に切り替わるよう毎フレーム状態を見直す。
            ApplyVideoSurface();
            if (fill != null && !errorShown && progressGroup != null && progressGroup.activeSelf)
            {
                if (V6LoadingViewLogic.HasProgress(progress))
                {
                    // 実進捗: 塗り幅が割合に比例 (0 でも 0 幅で「既知」を出す)。
                    fill.anchoredPosition = Vector2.zero;
                    fill.sizeDelta = new Vector2(
                        V6LoadingViewLogic.DeterminateFillWidth(progress.Value, TrackW), TrackH);
                }
                else
                {
                    // 進捗不明: 不定インジケータ (往復)。
                    fill.sizeDelta = new Vector2(FillW, TrackH);
                    float x = V6LoadingViewLogic.IndeterminateX(Time.unscaledTime, TrackW, FillW);
                    fill.anchoredPosition = new Vector2(x, 0f);
                }
            }
        }

        // --- ユーティリティ ---

        private static RectTransform PlaceAt(Component node, float x, float y, Vector2? size = null)
        {
            return PlaceAt(node.gameObject, x, y, size);
        }

        private static RectTransform PlaceAt(GameObject node, float x, float y, Vector2? size = null)
        {
            var rt = node.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            if (size.HasValue)
            {
                rt.sizeDelta = size.Value;
            }

            return rt;
        }

        private static void LayoutItem(GameObject go, float minWidth, float height)
        {
            var le = go.AddComponent<LayoutElement>();
            if (minWidth > 0f)
            {
                le.minWidth = minWidth;
                le.preferredWidth = minWidth;
            }

            le.preferredHeight = height;
        }
    }

    /// <summary>V6LoadingView の毎フレーム処理 (1:1 配置 + 不定インジケータ)。</summary>
    internal sealed class V6LoadingViewDriver : MonoBehaviour
    {
        internal V6LoadingView View;

        private void Update()
        {
            if (View != null)
            {
                View.Tick();
            }
        }
    }

    /// <summary>ローディング画面の映像面の状態。</summary>
    public enum V6LoadingVideoSurface
    {
        /// <summary>ローディング映像を出す (RawImage に映像テクスチャを接続)。</summary>
        Video = 0,

        /// <summary>映像は出さず、背景だけを見せる (白い画面を出さない)。</summary>
        Background = 1,

        /// <summary>何も出さない (ビューごと非表示)。</summary>
        Hidden = 2,
    }

    /// <summary>
    /// ローディング画面の映像面をどの状態にするかの判断 (純ロジック)。
    /// EditMode テストから直接呼べるよう、VideoPlayer に依存しない形で
    /// (clip 有無, 準備完了, エラー) → 状態 だけを受け取る。
    /// </summary>
    public static class V6LoadingVideoPolicy
    {
        /// <summary>
        /// 映像面の状態を決める。
        /// </summary>
        /// <param name="visible">ローディング画面が出ているか。</param>
        /// <param name="hasClip">再生する clip があるか。</param>
        /// <param name="prepared">VideoPlayer が準備完了か。</param>
        /// <param name="errored">再生エラーが起きたか。</param>
        public static V6LoadingVideoSurface Decide(bool visible, bool hasClip, bool prepared, bool errored)
        {
            if (!visible)
            {
                return V6LoadingVideoSurface.Hidden;
            }

            // clip が無い / 準備できない / エラー → 映像は出さない (白画面回避)。
            if (!hasClip || errored || !prepared)
            {
                return V6LoadingVideoSurface.Background;
            }

            return V6LoadingVideoSurface.Video;
        }
    }
}
