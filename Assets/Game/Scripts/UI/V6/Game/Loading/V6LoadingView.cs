using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.UI.V6.Game.Loading
{
    /// <summary>
    /// v6 曲ローディング画面 (Issue #224, design/flashbeat-ui.pen の `g4Q3F`)。
    ///
    /// - design: タイトル欄は空、右上にジャケット 56×56 + 曲名 fs22 / 作家 fs16、
    ///   フッターの凡例は Esc「戻る」のみ、補助 / 主操作は非表示、メインは空。
    /// - 進捗の表現は design に無い → この実装が決める: 中央に「準備中…」+ 不定バー。
    ///   失敗時はメッセージ + 「選曲にもどる」(実遷移: Initiate.Fade("SelectScene"))。
    /// - 状態は SetLoading / SetError で受け取る (統合担当が MusicManager /
    ///   HudPlayState の状態から呼ぶ)。キー入力は持たない (Esc は既存の
    ///   HudPlayStatus が「戻る」を担う)。
    /// - すべてコードから uGUI を生成。寸法は design px (1728x972)、
    ///   フレームを min(W/1728, H/972) 倍して画面へ等比拡大。
    /// </summary>
    public sealed class V6LoadingView
    {
        /// <summary>現在生成されている v6 ローディング画面。統合側・検証から参照する。</summary>
        public static V6LoadingView Current { get; private set; }

        // --- design px 定数 (flashbeat-v6-nodes.json g4Q3F / rw7zx) ---
        private const float FrameW = 1728f;
        private const float FrameH = 972f;

        private const float HeaderX = 24f, HeaderY = 0f, HeaderW = 1680f, HeaderH = 96f;
        private const float FooterX = 24f, FooterY = 876f;

        private const float TrackW = 464f;
        private const float TrackH = 8f;
        private const float FillW = 160f;

        private GameObject rootGo;
        private VideoPlayer videoPlayer;
        private bool videoErrorLogged;
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
            if (rootGo != null)
            {
                V5GameUi.DestroyNow(rootGo);
            }

            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }
        }

        public void SetVisible(bool visible)
        {
            if (rootGo != null)
            {
                rootGo.SetActive(visible);
            }

            if (visible)
            {
                UpdateScale();
                // ローディング映像: 表示のたびに最初から再生 (design: 動画を再生するだけ)。
                if (videoPlayer != null && videoPlayer.clip != null)
                {
                    videoErrorLogged = false;
                    videoPlayer.Play();
                }
            }
        }

        /// <summary>ローディング映像の再生エラー。無音で推移しない (Issue #233)。</summary>
        private void OnVideoError(VideoPlayer source, string message)
        {
            if (videoErrorLogged) return;
            videoErrorLogged = true;
            Debug.LogWarning($"[V6Loading] loading video unavailable: {message}");
        }

        /// <summary>ロード中の表示 (実データ: 曲情報は統合担当が渡す)。</summary>
        public void SetLoading(string songTitle, string artist, Texture2D jacket)
        {
            errorShown = false;
            leaving = false;
            if (headline != null)
            {
                headline.text = V6LoadingViewLogic.LoadingHeadline();
            }

            if (detail != null)
            {
                detail.text = V6LoadingViewLogic.LoadingDetail();
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
            var bg = V5Ui.Node(rootGo, "Loading Background");
            V5Ui.Fill(bg.GetComponent<RectTransform>(), 0f);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = V5Tokens.Bg;
            bgImg.raycastTarget = true;

            // ローディング映像 (design: ローディング画面は動画を再生するだけ。
            // Assets/Game/Resources/Videos/loading.mp4 = 自作素材、同梱する)。
            var videoBg = V5Ui.Node(rootGo, "Loading Video");
            V5Ui.Fill(videoBg.GetComponent<RectTransform>(), 0f);
            var videoRaw = videoBg.AddComponent<RawImage>();
            videoRaw.raycastTarget = false;
            videoPlayer = videoBg.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.renderMode = VideoRenderMode.APIOnly;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.clip = Resources.Load<VideoClip>("Videos/loading");
            videoPlayer.errorReceived += OnVideoError;

            frame = V5Ui.Node(rootGo, "V6 Loading Frame 1728x972").GetComponent<RectTransform>();
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(FrameW, FrameH);

            BuildHeader();
            BuildMain();
            BuildFooter();

            driver = rootGo.AddComponent<V6LoadingViewDriver>();
            driver.View = this;

            rootGo.SetActive(false);
            UpdateScale();
        }

        private void BuildHeader()
        {
            var header = V5Ui.Node(frame.gameObject, "Header");
            PlaceAt(header, HeaderX, HeaderY, new Vector2(HeaderW, HeaderH));

            // design: 戻るボタン (Esc)。
            var back = V5Ui.Panel(header, "Back", new Vector2(56f, 56f), V5Tokens.Key4, 8f);
            PlaceAt(back, 0f, 20f);
            var backLayout = back.AddComponent<VerticalLayoutGroup>();
            backLayout.spacing = 2f;
            backLayout.childAlignment = TextAnchor.MiddleCenter;
            backLayout.childControlWidth = true;
            backLayout.childControlHeight = true;
            backLayout.childForceExpandWidth = false;
            backLayout.childForceExpandHeight = false;
            var backArrow = V6Icons.Create(back, "chevron-left", 24f, V5Tokens.TextMuted, "Icon");
            if (backArrow != null)
            {
                LayoutItem(backArrow.gameObject, 0f, 24f);
            }
            var backKey = V5Ui.Text(back, "Key", "Esc", 16f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            LayoutItem(backKey.gameObject, 0f, 16f);
            var backBtn = back.AddComponent<Button>();
            backBtn.targetGraphic = back.GetComponent<Image>();
            backBtn.onClick.AddListener(Back);

            // design: タイトル欄は空 (0×0)。

            // 右上: ジャケット 56×56 + 曲名 fs22 / 作家 fs16。
            var info = V5Ui.Node(header, "Song Info");
            PlaceAt(info, 1080f, 0f, new Vector2(300f, HeaderH));
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

            var texts = V5Ui.Node(info, "Texts");
            LayoutItem(texts, 0f, 56f);
            var textsLayout = texts.AddComponent<VerticalLayoutGroup>();
            textsLayout.spacing = 2f;
            textsLayout.childAlignment = TextAnchor.MiddleLeft;
            textsLayout.childControlWidth = true;
            textsLayout.childControlHeight = true;
            textsLayout.childForceExpandWidth = false;
            textsLayout.childForceExpandHeight = false;
            songTitle = V5Ui.Text(texts, "Song Title", "", 22f, V5Tokens.Text, TextAlignmentOptions.Left);
            songTitle.textWrappingMode = TextWrappingModes.NoWrap;
            songTitle.overflowMode = TextOverflowModes.Ellipsis;
            LayoutItem(songTitle.gameObject, 0f, 26f);
            songArtist = V5Ui.Text(texts, "Song Artist", "", 16f, V5Tokens.TextMuted, TextAlignmentOptions.Left);
            songArtist.textWrappingMode = TextWrappingModes.NoWrap;
            songArtist.overflowMode = TextOverflowModes.Ellipsis;
            LayoutItem(songArtist.gameObject, 0f, 20f);
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

            // 失敗時: メッセージ + 「選曲にもどる」(実遷移)。
            var error = V5Ui.Node(main, "Error");
            PlaceAt(error, 608f, 240f, new Vector2(464f, 300f));
            errorGroup = error;

            var errorHeadline = V5Ui.Text(error, "Headline", "読み込み / 再生に失敗しました", 30f,
                V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceAt(errorHeadline.gameObject, 0f, 0f, new Vector2(464f, 40f));

            var errorDetail = V5Ui.Text(error, "Detail", "", 18f,
                V5Tokens.TextMuted, TextAlignmentOptions.Center);
            PlaceAt(errorDetail.gameObject, 0f, 52f, new Vector2(464f, 24f));

            var backBtn = V5Ui.Panel(error, "Back To Select", new Vector2(266f, 56f), V5Tokens.ButtonPrimaryBg, 8f);
            PlaceAt(backBtn, 99f, 120f);
            var btnLayout = backBtn.AddComponent<HorizontalLayoutGroup>();
            btnLayout.spacing = 12f;
            btnLayout.childAlignment = TextAnchor.MiddleCenter;
            btnLayout.childControlWidth = true;
            btnLayout.childControlHeight = true;
            btnLayout.childForceExpandWidth = false;
            btnLayout.childForceExpandHeight = false;
            var btnLabel = V5Ui.Text(backBtn, "Label", "選曲にもどる", 30f,
                V5Tokens.ButtonPrimaryLabel, TextAlignmentOptions.Center, FontStyles.Bold);
            LayoutItem(btnLabel.gameObject, 0f, 34f);
            var btnButton = backBtn.AddComponent<Button>();
            btnButton.targetGraphic = backBtn.GetComponent<Image>();
            btnButton.onClick.AddListener(Back);

            errorGroup.SetActive(false);
        }

        private void BuildFooter()
        {
            var footer = V5Ui.Node(frame.gameObject, "Footer");
            PlaceAt(footer, FooterX, FooterY, new Vector2(1680f, 96f));

            // design: 凡例は Esc「戻る」だけ (決定 / 曲送りは 0×0、補助 / 主操作は非表示)。
            var item = V5Ui.Node(footer, "Legend Back");
            PlaceAt(item, 0f, 30f, new Vector2(200f, 36f));
            var layout = item.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var cap = V5Ui.Panel(item, "Cap", new Vector2(0f, 36f), V5Tokens.Key4, 4f);
            var capLayout = cap.AddComponent<HorizontalLayoutGroup>();
            capLayout.padding = new RectOffset(Mathf.RoundToInt(10f), Mathf.RoundToInt(10f), 0, 0);
            capLayout.childAlignment = TextAnchor.MiddleCenter;
            capLayout.childControlWidth = true;
            capLayout.childControlHeight = true;
            capLayout.childForceExpandWidth = false;
            capLayout.childForceExpandHeight = false;
            var capLe = cap.AddComponent<LayoutElement>();
            capLe.preferredHeight = 36f;
            var keyTmp = V5Ui.Text(cap, "Key", "Esc", 22f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            LayoutItem(keyTmp.gameObject, 0f, 26f);

            var labelTmp = V5Ui.Text(item, "Label", "戻る", 22f, V5Tokens.TextMuted, TextAlignmentOptions.Left);
            LayoutItem(labelTmp.gameObject, 0f, 26f);
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
                if (jacket != null)
                {
                    jacketImage.sprite = Sprite.Create(jacket,
                        new Rect(0f, 0f, jacket.width, jacket.height), new Vector2(0.5f, 0.5f), 100f);
                    jacketImage.type = Image.Type.Simple;
                    jacketImage.preserveAspect = true;
                    jacketImage.color = Color.white;
                }
                else
                {
                    jacketImage.sprite = null;
                    jacketImage.color = V5Tokens.Key2;
                }
            }
        }

        // --- スケール / 毎フレーム ---

        private void UpdateScale()
        {
            if (frame == null)
            {
                return;
            }

            int w = Screen.width;
            int h = Screen.height;
            float u = V6SelectViewLogic.DesignScale(w, h);
            frame.localScale = new Vector3(u, u, 1f);
        }

        internal void Tick()
        {
            UpdateScale();
            if (fill != null && !errorShown && progressGroup != null && progressGroup.activeSelf)
            {
                float x = V6LoadingViewLogic.IndeterminateX(Time.unscaledTime, TrackW, FillW);
                fill.anchoredPosition = new Vector2(x, 0f);
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

    /// <summary>V6LoadingView の毎フレーム処理 (スケール + 不定インジケータ)。</summary>
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
}
