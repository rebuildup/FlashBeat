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
    /// V6LoadingView の uGUI 構築部分 (Issue #385 Phase 2.8 で分割)。
    /// 背景 / ローディング映像 / 共通フレーム / ヘッダー (曲バッジ) /
    /// メイン (進捗・失敗) / フッター (凡例) の生成を持つ。
    /// partial class のためフィールド・メソッドは V6LoadingView 本体と共有する。
    /// </summary>
    public sealed partial class V6LoadingView
    {
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
            V6Layout.PlaceAt(info, 1080f, 0f, new Vector2(300f, V6Header.Height));
            var infoLayout = info.AddComponent<HorizontalLayoutGroup>();
            infoLayout.spacing = 12f;
            infoLayout.childAlignment = TextAnchor.MiddleLeft;
            infoLayout.childControlWidth = true;
            infoLayout.childControlHeight = true;
            infoLayout.childForceExpandWidth = false;
            infoLayout.childForceExpandHeight = false;

            // 曲バッジ (Jacket + Texts 列) は共通部品 (Issue #385 Phase 1.2)。
            // Song Info は HorizontalLayoutGroup 駆動なので LayoutDriven で作り、
            // Jacket / Texts の配置はレイアウトに委ねる (design Frw5C:
            // Texts (68,20) 113×57、Song Title (0,0) 111×32 fs22、
            // Song Artist (0,34) 113×23 fs16 — どちらも左上揃え)。
            var badge = V6SongBadge.Create(info, new V6SongBadge.Spec
            {
                LayoutDriven = true,
                ThumbName = "Jacket",
                ThumbSize = new Vector2(56f, 56f),
                ThumbColor = V5Tokens.Key2,
                ThumbRaycastTarget = false,
                ColumnName = "Texts",
                ColumnSize = new Vector2(113f, 56f),
                Title = new V6SongBadge.Line
                {
                    Name = "Song Title", Pos = new Vector2(0f, 0f), Size = new Vector2(111f, 32f),
                    FontSize = 22f, Color = V5Tokens.Text, Align = TextAlignmentOptions.TopLeft,
                    // h32 は行送り (fs22 × 1.448 = 31.9) とほぼ同じ。Ellipsis の 0 行化を
                    // 避けるため上下 margin を広げる (矩形とベースラインは変えない)。
                    FitLineHeight = true, FitHeight = 32f,
                },
                Sub = new V6SongBadge.Line
                {
                    Name = "Song Artist", Pos = new Vector2(0f, 34f), Size = new Vector2(113f, 23f),
                    FontSize = 16f, Color = V5Tokens.TextMuted, Align = TextAlignmentOptions.TopLeft,
                    FitLineHeight = true, FitHeight = 23f,
                },
            });
            var jacket = badge.Thumb;
            V6Layout.LayoutItem(jacket, 56f, 56f);
            jacketImage = jacket.GetComponent<Image>();
            // ジャケットが無い曲は「無し」を正直に表示する (空欄/壊れた画像にしない)。
            noJacketLabel = V5Ui.Text(jacket, "No Jacket", "無し", 16f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            noJacketLabel.rectTransform.anchorMin = Vector2.zero;
            noJacketLabel.rectTransform.anchorMax = Vector2.one;
            noJacketLabel.rectTransform.offsetMin = Vector2.zero;
            noJacketLabel.rectTransform.offsetMax = Vector2.zero;
            noJacketLabel.fontSize = 14f;
            noJacketLabel.gameObject.SetActive(false);

            // 旧実装は VerticalLayoutGroup の中央寄せで、2 行が design より
            // 数 px 上に寄っていた (設計の矩形をそのまま使う)。
            var texts = badge.Column;
            V6Layout.LayoutItem(texts, 113f, 56f);
            songTitle = badge.Title;
            songArtist = badge.Sub;
        }

        /// <summary>design ではメイン空。進捗の表現はこの実装が決めた:
        /// 中央に見出し + 補足 + 不定バー (ロード中) / 失敗メッセージ + 「選曲にもどる」。</summary>
        private void BuildMain()
        {
            var main = V5Ui.Node(frame.gameObject, "Main");
            V6Layout.PlaceAt(main, 24f, 96f, new Vector2(1680f, 780f));

            var center = V5Ui.Node(main, "Progress");
            V6Layout.PlaceAt(center, 608f, 240f, new Vector2(464f, 300f));
            progressGroup = center;

            headline = V5Ui.Text(center, "Headline", V6LoadingViewLogic.LoadingHeadline(), 30f,
                V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            V6Layout.PlaceAt(headline.gameObject, 0f, 0f, new Vector2(464f, 40f));

            detail = V5Ui.Text(center, "Detail", V6LoadingViewLogic.LoadingDetail(), 18f,
                V5Tokens.TextMuted, TextAlignmentOptions.Center);
            V6Layout.PlaceAt(detail.gameObject, 0f, 52f, new Vector2(464f, 24f));

            var track = V5Ui.Panel(center, "Track", new Vector2(TrackW, TrackH), V5Tokens.Key0, 4f);
            V6Layout.PlaceAt(track, 0f, 108f);
            fill = V5Ui.Panel(track, "Fill", new Vector2(FillW, TrackH), V5Tokens.Text, 4f)
                .GetComponent<RectTransform>();
            fill.anchorMin = new Vector2(0f, 0.5f);
            fill.anchorMax = new Vector2(0f, 0.5f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;

            // 失敗時: メッセージ + 「再試行」(実遷移: RetryGame) + 「選曲にもどる」(実遷移)。
            var error = V5Ui.Node(main, "Error");
            V6Layout.PlaceAt(error, 608f, 240f, new Vector2(464f, 300f));
            errorGroup = error;

            var errorHeadline = V5Ui.Text(error, "Headline", "読み込み / 再生に失敗しました", 30f,
                V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            V6Layout.PlaceAt(errorHeadline.gameObject, 0f, 0f, new Vector2(464f, 40f));

            var errorDetail = V5Ui.Text(error, "Detail", "", 18f,
                V5Tokens.TextMuted, TextAlignmentOptions.Center);
            V6Layout.PlaceAt(errorDetail.gameObject, 0f, 52f, new Vector2(464f, 24f));

            var retryBtn = V5Ui.Panel(error, "Retry", new Vector2(200f, 56f), V5Tokens.ButtonPrimaryBg, 8f);
            V6Layout.PlaceAt(retryBtn, 32f, 120f);
            var retryLayout = retryBtn.AddComponent<HorizontalLayoutGroup>();
            retryLayout.spacing = 12f;
            retryLayout.childAlignment = TextAnchor.MiddleCenter;
            retryLayout.childControlWidth = true;
            retryLayout.childControlHeight = true;
            retryLayout.childForceExpandWidth = false;
            retryLayout.childForceExpandHeight = false;
            var retryLabel = V5Ui.Text(retryBtn, "Label", "再試行", 30f,
                V5Tokens.ButtonPrimaryLabel, TextAlignmentOptions.Center, FontStyles.Bold);
            V6Layout.LayoutItem(retryLabel.gameObject, 0f, 34f);
            var retryButton = retryBtn.AddComponent<Button>();
            retryButton.targetGraphic = retryBtn.GetComponent<Image>();
            V6ButtonPressFeedback.Attach(retryBtn);
            // 再試行は GameScene への遷移なので、押下リングが見えるだけ遅らせる。
            retryButton.onClick.AddListener(() => V6ButtonPressFeedback.RunAfterFeedback(retryBtn, Retry));

            var backBtn = V5Ui.Panel(error, "Back To Select", new Vector2(200f, 56f), V5Tokens.ButtonPrimaryBg, 8f);
            V6Layout.PlaceAt(backBtn, 232f, 120f);
            var btnLayout = backBtn.AddComponent<HorizontalLayoutGroup>();
            btnLayout.spacing = 12f;
            btnLayout.childAlignment = TextAnchor.MiddleCenter;
            btnLayout.childControlWidth = true;
            btnLayout.childControlHeight = true;
            btnLayout.childForceExpandWidth = false;
            btnLayout.childForceExpandHeight = false;
            var btnLabel = V5Ui.Text(backBtn, "Label", "選曲にもどる", 26f,
                V5Tokens.ButtonPrimaryLabel, TextAlignmentOptions.Center, FontStyles.Bold);
            V6Layout.LayoutItem(btnLabel.gameObject, 0f, 34f);
            var btnButton = backBtn.AddComponent<Button>();
            btnButton.targetGraphic = backBtn.GetComponent<Image>();
            V6ButtonPressFeedback.Attach(backBtn);
            btnButton.onClick.AddListener(() => V6ButtonPressFeedback.RunAfterFeedback(backBtn, Back));

            errorGroup.SetActive(false);
        }

        private void BuildFooter()
        {
            var footer = V5Ui.Node(frame.gameObject, "Footer");
            V6Layout.PlaceAt(footer, FooterX, FooterY, new Vector2(1680f, 96f));

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
    }
}
