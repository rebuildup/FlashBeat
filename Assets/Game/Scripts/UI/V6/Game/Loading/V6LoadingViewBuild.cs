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
    /// フッター (凡例) の生成を持つ。メインゾーンは design どおり空
    /// (進捗・エラーの専用表示は置かない — Issue #606)。
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
            BuildFooter();
            BuildErrorPanel();

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

        /// <summary>
        /// 失敗時のエラーパネル (Issue #792)。
        ///
        /// design にはエラー専用の表示が無い (Issue #606) が、v0.2.1 の実機受入で
        /// 「ローディング後に説明なく選曲画面へ戻る」が不合格になった。
        /// メインゾーン中央に理由の文言と「再試行」(GameScene の読み直し)
        /// 「選曲にもどる」(既存の Back) の 2 ボタンを出す。
        /// フレームは 1728×972 の design 座標 (左上原点)。
        /// </summary>
        private void BuildErrorPanel()
        {
            const float panelW = 1040f;
            const float panelH = 400f;
            errorPanel = V5Ui.Panel(frame.gameObject, "Error Panel",
                new Vector2(panelW, panelH), V5Tokens.Recessed2, V5Tokens.RadiusMedium);
            V6Layout.PlaceAt(errorPanel, (FrameW - panelW) * 0.5f, (FrameH - panelH) * 0.5f,
                new Vector2(panelW, panelH));
            errorPanel.SetActive(false);

            errorText = V5Ui.Text(errorPanel, "Error Message", "", 26f,
                V5Tokens.Text, TextAlignmentOptions.Center);
            V6Layout.PlaceAt(errorText, 40f, 48f, new Vector2(panelW - 80f, 200f));
            errorText.textWrappingMode = TextWrappingModes.Normal;
            errorText.alignment = TextAlignmentOptions.Center;

            // ボタン行: 「再試行」(主操作) と「選曲にもどる」(副操作)。
            const float btnW = 260f;
            const float btnH = 64f;
            const float btnGap = 40f;
            float btnY = panelH - btnH - 40f;
            float btnX = (panelW - btnW * 2f - btnGap) * 0.5f;

            var retry = V5Ui.ButtonPrimary(errorPanel, "再試行", new Vector2(btnW, btnH));
            V6Layout.PlaceAt(retry, btnX, btnY, new Vector2(btnW, btnH));
            var retryButton = retry.GetComponent<Button>();
            if (retryButton != null)
            {
                retryButton.onClick.AddListener(Retry);
            }

            var back = V5Ui.ButtonSecondary(errorPanel, "選曲にもどる", new Vector2(btnW, btnH));
            V6Layout.PlaceAt(back, btnX + btnW + btnGap, btnY, new Vector2(btnW, btnH));
            var backButton = back.GetComponent<Button>();
            if (backButton != null)
            {
                backButton.onClick.AddListener(Back);
            }
        }
    }
}
