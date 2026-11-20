using System.Collections.Generic;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Game.Playfield;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V6.Game.Hud
{
    /// <summary>
    /// V6GameHudView の歌詞スロット / Start Prompt / 再開カウントダウン /
    /// 曲終了オーバーレイ部分 (Issue #385 Phase 2.2 で分割)。
    /// partial class のためフィールド・メソッドは V6GameHudView 本体と共有する。
    /// </summary>
    public sealed partial class V6GameHudView
    {
        // --- 歌詞枠 (design i30vM4) ---
        private const float LyricFs = 44f;

        /// <summary>
        /// design の歌詞フォント (1728×972 基準) に対する実画面での倍率。
        ///
        /// 歌詞スロットの箱は画面高に比例して大きくなるのに、フォントを design px の
        /// まま固定すると、高解像度ほど「大きな空の板に小さい文字」になる
        /// (2026-11-12 オーナー指摘)。HUD の他の要素 (スコア・コンボ) は m.* で
        /// 画面に追従しているので、歌詞も同じ倍率で揃える。
        /// </summary>
        private float LyricFontScale()
        {
            float metricsFs = m != null ? m.LyricFs : LyricFs;
            return LyricFs > 0f ? metricsFs / LyricFs : 1f;
        }

        /// <summary>実際に使う歌詞の行フォントサイズ (px)。</summary>
        private float ScaledLyricFs { get { return LyricFs * LyricFontScale(); } }
        private static readonly Color LyricBoxColor = new Color(0f, 0f, 0f, 0x99 / 255f);

        /// <summary>歌詞ボックスの角丸半径 (canvas px)。V5Ui.Panel の radius と同じ値。</summary>
        private const float LyricBoxCornerRadius = 8f;

        /// <summary>
        /// 歌詞本文の縁取り (Issue #760)。レーン回廊カットアウトでパネルの
        /// 暗幕がレーン帯から消えるため、歌詞文字は動画の上に直接乗る。
        /// 明るい映像の上でも読めるよう、細い黒縁を常時付ける。
        /// </summary>
        private const float LyricTextOutlineWidth = 0.25f;
        private static readonly Color32 LyricTextOutlineColor = new Color32(0, 0, 0, 217);

        // --- 生成した部品 (歌詞系) ---
        private TextMeshProUGUI lyricText;
        private GameObject lyricBox;

        // --- 歌詞 ---
        private GameSceneManager lyricSource;
        private TextData lyricData;
        private List<LyricLine> lyricLines;
        private int lyricIndex = -2;
        /// <summary>開始待ちの案内 (design HcP8N の Start Prompt)。歌詞行とは排他。</summary>
        private GameObject lyricPrompt;

        /// <summary>再開カウントダウン (design o0zh7 の Countdown xuQnv)。歌詞と排他。</summary>
        private GameObject countdownOverlay;
        private TextMeshProUGUI countdownText;

        /// <summary>曲終了の黒フェード (全画面) と「終了」表示 (design xXKic)。</summary>
        private Image endFade;
        private GameObject endOverlay;

        private void BuildLyric()
        {
            // design i30vM4 (契約 §8): ゲームフレーム 1728×972 の (304,250) 1120×220 を
            // プレイフィールドと同じ高さ合わせ写像で置く。FHD y=278 / 2K y=370 / Mac y=428。
            // 内部の Furigana fs22 / Lyrics fs44 / Romaji fs20、gap 6、padding 24/16 は
            // design のまま (契約 §8)。
            // Issue #433: 歌詞スロットは共有フレームのメイン領域 6 列グリッドの
            // 中央 4 列 (フレーム内 (304,250) 1120 幅) を 1:1 で置く。
            // Issue #674: 高さは歌詞本文だけでなくカウントダウン・終了タイトルも
            // 収まる共有スロット高 (V6HudLogic.LyricSlotHeight) を使い、
            // 状態遷移でパネルが伸縮しないようにする。
            float slotHeight = V6HudLogic.LyricSlotHeight(ScaledLyricFs, CanvasSize().y);
            // #96/#523: 画面幅を超える design 幅は画面幅に収める (中央配置は不変)。
            var box = V5Ui.Panel(gameObject, "Lyric Box",
                new Vector2(LyricBoxWidth(), slotHeight), LyricBoxColor, LyricBoxCornerRadius);
            var boxImage = box.GetComponent<Image>();
            boxImage.raycastTarget = false;

            // Issue #760: パネルがレーン帯を暗くしないよう、上部シェード
            // (V6TopShadeHole) と同じレーン回廊カットアウトを焼き込んだ
            // スプライトへ差し替える。9-slice の角丸は帯の抜きと両立しない
            // ため、実寸テクスチャ (1 texel = 1 canvas px) + Simple で使う。
            // 暗幕はレーン帯の外側に残るので、パネルの縁の見た目は従来どおり。
            Vector2 slotOrigin = V6HudLogic.LyricSlotOrigin(
                CanvasSize().x, CanvasSize().y);
            boxImage.sprite = V6PanelCorridorHole.ForPanel(
                slotOrigin.x, slotOrigin.y, LyricBoxWidth(), slotHeight,
                CanvasSize(), LyricBoxCornerRadius);
            boxImage.type = Image.Type.Simple;
            lyricBox = box;
            // Issue #734: 初期状態は非表示。最初の UpdateLyrics (毎フレーム) が
            // 設定値と再生状態のポリシーで表示/非表示を決める。
            // Issue #790: SetActive(false) は子の構築が全部終わってから行う。
            // 非アクティブな階層では TMP の実測 (ForceMeshUpdate /
            // GetPreferredValues) が退化値を返し得て、KeyCap の幅が実文字より
            // 狭くなる → 「Space」が cap をはみ出して「to start」と重なる。
            // 構築は 1 フレーム内で同期に終わるので、ここで遅らせても
            // 空の枠が描画されるフレームは生まれない。
            var boxRt = V6Layout.RectOf(box);
            boxRt.anchorMin = new Vector2(0.5f, 1f);
            boxRt.anchorMax = new Vector2(0.5f, 1f);
            boxRt.pivot = new Vector2(0.5f, 1f);
            boxRt.anchoredPosition = LyricSlotAnchoredPosition();

            var layout = box.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 8f;
            // Issue #582: design の padding [16,24] / gap 6 は窮屈だったため、
            // 上下 24 / 左右 32 / gap 8 へ拡大 (寸法の正本は V6HudLogic)。
            layout.padding = new RectOffset(
                Mathf.RoundToInt(V6HudLogic.LyricPaddingX), Mathf.RoundToInt(V6HudLogic.LyricPaddingX),
                Mathf.RoundToInt(V6HudLogic.LyricPaddingY), Mathf.RoundToInt(V6HudLogic.LyricPaddingY));
            layout.childControlWidth = true;
            // true でないと LayoutElement.preferredHeight が無視され、行は V5Ui.Text の
            // rect 高さ 0 のままになる → TMP は 1 行も描画されない (歌詞消失の根本原因)。
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // V5Ui.Text は rect 高さ 0 で作る。preferredHeight (fs × 行送り) を
            // LayoutElement で渡し、上の childControlHeight=true で行の高さを確保する。
            // Issue #433: 表示は歌詞本文の 1 行だけ。振り仮名・ローマ字は出さない。
            lyricText = V5Ui.Text(box, "Lyrics", "", ScaledLyricFs, V5Tokens.Text, TextAlignmentOptions.Center);
            // Issue #760: 回廊カットアウトで背後の暗幕が消えるため、
            // 歌詞文字は動画の上に直接乗る。明るい映像でも読めるよう縁取りを付ける。
            ApplyLyricTextOutline(lyricText);
            SetLyricRowHeight(lyricText, ScaledLyricFs);

            BuildLyricPrompt(box);
            box.SetActive(false);
        }

        /// <summary>
        /// 開始待ちの案内 (design HcP8N / Start Prompt W1ppF):
        /// KeyCap「Space」(36 高 / padding [0,10] / fs22 / #3A3A3A r4) と
        /// ラベル「to start」(fs22 $text-muted) を gap 10 で横に並べる。
        /// 以前は「Space to Start」を fs 約 79 の 1 文で出していた (デザインに無い表示)。
        /// </summary>
        /// <summary>
        /// 再開カウントダウンの数字 (design o0zh7 の Countdown xuQnv)。歌詞スロットと同じ位置・
        /// 同じ幅に置き、カウントダウン中だけ歌詞と Start Prompt を隠す (契約 §9)。
        /// </summary>
        private void BuildCountdown()
        {
            countdownOverlay = V5Ui.Node(gameObject, "Resume Countdown");
            var rt = V6Layout.RectOf(countdownOverlay);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            // Issue #674: 歌詞ボックスと同一の共有スロット寸法を使う
            // (状態ごとの高さ分岐は持たない)。カウントダウンの大きな数字も
            // 共有スロット高に収まる。
            float slotHeight = V6HudLogic.LyricSlotHeight(ScaledLyricFs, CanvasSize().y);
            rt.sizeDelta = new Vector2(LyricBoxWidth(), slotHeight);
            rt.anchoredPosition = LyricSlotAnchoredPosition();

            // v0.2.1 (#734 実機レビュー): カウントダウン / START も
            // 歌詞パネルと同じ「枠」として出す。テキストだけを浮かべると
            // 開始・終了フェーズでパネル自体が消えたように見える
            // (終了オーバーレイと同じ LyricBoxColor + 回廊カットアウト)。
            var panel = V5Ui.Panel(countdownOverlay, "Panel", rt.sizeDelta,
                LyricBoxColor, LyricBoxCornerRadius);
            var panelImage = panel.GetComponent<Image>();
            panelImage.raycastTarget = false;
            Vector2 cdSlotOrigin = V6HudLogic.LyricSlotOrigin(
                CanvasSize().x, CanvasSize().y);
            panelImage.sprite = V6PanelCorridorHole.ForPanel(
                cdSlotOrigin.x, cdSlotOrigin.y, LyricBoxWidth(), slotHeight,
                CanvasSize(), LyricBoxCornerRadius);
            panelImage.type = Image.Type.Simple;
            var panelRt = V6Layout.RectOf(panel);
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            countdownText = V5Ui.Text(countdownOverlay, "Value", "3",
                V6HudLogic.CountdownFontSize(CanvasSize().y), V5Tokens.Text,
                TextAlignmentOptions.Center, FontStyles.Bold);
            var textRt = countdownText.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            countdownOverlay.SetActive(false);
        }

        /// <summary>
        /// 検証ハーネス用: いま表示している再開カウントダウンの数字。
        /// 非表示なら空文字。
        /// </summary>
        internal string CountdownTextForAudit
        {
            get
            {
                if (countdownOverlay == null || !countdownOverlay.activeSelf || countdownText == null)
                {
                    return string.Empty;
                }

                return countdownText.text;
            }
        }

        /// <summary>カウントダウンの数字を表示する (3 / 2 / 1)。</summary>
        public void ShowResumeCountdown(int number)
        {
            if (countdownOverlay == null)
            {
                return;
            }

            countdownText.text = number.ToString();
            // START 表示から戻ってきた場合に備え、数字のサイズへ戻す。
            countdownText.fontSize = V6HudLogic.CountdownFontSize(CanvasSize().y);
            countdownOverlay.SetActive(true);
            SetLyricLinesVisible(false);
            SetLyricPromptVisible(false);
            SetLyricBoxVisible(false);
        }

        /// <summary>開始待ち (startTiming) の間、歌詞スロットに START を出す。</summary>
        public void ShowStartHold()
        {
            if (countdownOverlay == null)
            {
                return;
            }

            // START はカウントダウンと同じ枠を使うが、数字より小さい「見出し」の
            // 体裁にする (2026-11-12 オーナー指摘)。132px の数字サイズのままだと
            // 状態表示ではなく主役に見え、カウントダウンとも区別が付かない。
            countdownText.text = "START";
            countdownText.fontSize = V6HudLogic.StartHoldFontSize(CanvasSize().y);
            countdownOverlay.SetActive(true);
            SetLyricLinesVisible(false);
            SetLyricPromptVisible(false);
            SetLyricBoxVisible(false);
        }

        /// <summary>カウントダウンを消し、次のフレームから歌詞表示へ戻す。</summary>
        public void HideResumeCountdown()
        {
            if (countdownOverlay != null)
            {
                countdownOverlay.SetActive(false);
            }
        }

        /// <summary>
        /// 曲終了のフェードと「終了」表示 (Issue #383 / design xXKic)。
        /// フェードは HUD より手前に置いて全画面を黒へ寄せ、その上に「終了」を出す。
        /// 判定は PauseGate ではなく MusicManager が終端で止める。
        /// </summary>
        private void BuildEndOverlay()
        {
            var fadeGo = V5Ui.Node(gameObject, "Song End Fade");
            var fadeRt = V6Layout.RectOf(fadeGo);
            fadeRt.anchorMin = Vector2.zero;
            fadeRt.anchorMax = Vector2.one;
            fadeRt.offsetMin = Vector2.zero;
            fadeRt.offsetMax = Vector2.zero;
            endFade = fadeGo.AddComponent<Image>();
            endFade.color = new Color(0f, 0f, 0f, 0f);
            endFade.raycastTarget = false;
            fadeGo.SetActive(false);

            endOverlay = V5Ui.Node(gameObject, "Song End Overlay");
            var rt = V6Layout.RectOf(endOverlay);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            float scale = V6LaneGeometry.ScreenScale(CanvasSize());
            float endTitleFs = V6HudLogic.SongEndTitleFontSize(scale);
            // Issue #674: 歌詞ボックス・カウントダウンと同一の共有スロット寸法。
            // 「終了」タイトルも共有スロット高に収まる。
            float slotHeight = V6HudLogic.LyricSlotHeight(ScaledLyricFs, CanvasSize().y);
            rt.sizeDelta = new Vector2(LyricBoxWidth(), slotHeight);
            rt.anchoredPosition = LyricSlotAnchoredPosition();

            var box = V5Ui.Panel(endOverlay, "Panel", rt.sizeDelta,
                LyricBoxColor, 8f);
            box.GetComponent<Image>().raycastTarget = false;

            // Issue #433: 「終了・リザルトへ」の 2 行表示が重なっていたので
            // 「終了」だけにする (遷移先は Result で自明)。
            var title = V5Ui.Text(box, "Title", "終了", endTitleFs,
                V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(0f, 10f * scale);
            titleRt.offsetMax = new Vector2(0f, -10f * scale);

            endOverlay.SetActive(false);
        }

        /// <summary>曲終了の黒フェード量 (0..1)。0 のときは非表示。</summary>
        public void SetEndFadeAlpha(float alpha)
        {
            if (endFade == null)
            {
                return;
            }

            float a = Mathf.Clamp01(alpha);
            bool visible = a > 0.001f;
            if (endFade.gameObject.activeSelf != visible)
            {
                endFade.gameObject.SetActive(visible);
            }

            endFade.color = new Color(0f, 0f, 0f, a);
        }

        /// <summary>「終了」表示を出す。歌詞と Start Prompt は隠す。</summary>
        public void ShowSongEnd()
        {
            if (endOverlay == null)
            {
                return;
            }

            endOverlay.SetActive(true);
            SetLyricLinesVisible(false);
            SetLyricPromptVisible(false);
            SetLyricBoxVisible(false);
        }

        /// <summary>終了表示を消す (再利用時)。</summary>
        public void HideSongEnd()
        {
            if (endOverlay != null)
            {
                endOverlay.SetActive(false);
            }
        }

        private void BuildLyricPrompt(GameObject box)
        {
            lyricPrompt = V5Ui.Node(box, "Start Prompt");
            // Issue #790 (v0.2.1 実機受入): Start Prompt の寸法は
            // V6HudLogic.ReadyPrompt の spec が正本。歌詞本文 fs を基準に
            // cap 高さ・fs・gap・左右 padding・角丸を同じ比率で揃える。
            // 以前は fs だけを拡縮し padding/角丸は design px 固定だったため、
            // 解像度ごとに KeyCap と「to start」の余白・文字組みがずれた。
            var spec = V6HudLogic.ReadyPrompt(ScaledLyricFs);
            var le = lyricPrompt.AddComponent<LayoutElement>();
            le.preferredHeight = spec.CapHeight;

            var layout = lyricPrompt.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spec.Gap;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var cap = V5Ui.Panel(lyricPrompt, "KeyCap Space",
                new Vector2(0f, spec.CapHeight), V5Tokens.Key4, spec.CornerRadius);
            var capKey = V5Ui.Text(cap, "Key", "Space", spec.FontSize,
                V5Tokens.TextMuted, TextAlignmentOptions.Center);
            V6Layout.StretchFill(capKey.rectTransform);
            float capW = MeasureTextWidth(capKey, "Space", spec.FontSize)
                + spec.PaddingX * 2f;
            V6Layout.RectOf(cap).sizeDelta = new Vector2(capW, spec.CapHeight);

            var label = V5Ui.Text(lyricPrompt, "Label", "to start", spec.FontSize,
                V5Tokens.TextMuted, TextAlignmentOptions.Left);
            // Issue #760: Start Prompt も歌詞ボックス内に出るので同じ縁取りを付ける
            // (KeyCap は自前の背景を持つので対象外)。
            ApplyLyricTextOutline(label);
            float labelW = MeasureTextWidth(label, "to start", spec.FontSize);
            V6Layout.RectOf(label).sizeDelta = new Vector2(labelW, spec.CapHeight);

            lyricPrompt.SetActive(false);
        }

        /// <summary>歌詞スロットの実幅。小画面では画面幅に収める (#96/#523)。</summary>
        private float LyricBoxWidth()
        {
            return V6HudLogic.LyricSlotWidth(CanvasSize().x);
        }

        /// <summary>
        /// 歌詞スロット (上中央アンカー) の anchoredPosition。
        /// 左上 = 共有フレーム origin + (304,250)。x はスロット中心 - 画面中心。
        /// </summary>
        private Vector2 LyricSlotAnchoredPosition()
        {
            Vector2 canvasSize = CanvasSize();
            // LyricSlotOrigin はクランプ後の実左端。実幅の半分を足すと
            // デザインスロット中心 (クランプで変わらない) に一致する。
            Vector2 slotOrigin = V6HudLogic.LyricSlotOrigin(canvasSize.x, canvasSize.y);
            return new Vector2(
                slotOrigin.x + LyricBoxWidth() * 0.5f - canvasSize.x * 0.5f,
                -slotOrigin.y);
        }

        /// <summary>
        /// 歌詞系テキストへ暗い縁取りを付ける (Issue #760)。
        /// レーン回廊カットアウトでパネルの暗幕がレーン帯から消えるため、
        /// 歌詞・Start Prompt の文字は動画の上に直接乗る。縁取りで明暗差を
        /// 確保し、明るい映像の上でも可読性を保つ。
        /// </summary>
        private static void ApplyLyricTextOutline(TextMeshProUGUI text)
        {
            // TMP は EditMode 等で font マテリアルが未解決のまま残ると
            // outlineWidth setter 内部 (SetOutlineThickness →
            // CreateMaterialInstance/SetMaterial) が NRE になる。
            // fontSharedMaterial の null チェックだけでは防げない
            // (内部の m_fontMaterial/canvasRenderer 経路で落ちる) ため、
            // 縁取りは付加要素として例外を飲んで構築自体は止めない。
            try
            {
                text.outlineWidth = LyricTextOutlineWidth;
                text.outlineColor = LyricTextOutlineColor;
            }
            catch (System.Exception)
            {
                // 未解決/破棄済みマテリアル環境では縁取りを諦める。
            }
        }

        /// <summary>歌詞行の高さを fs × 1.25 (行送り) で確保する。</summary>
        private static void SetLyricRowHeight(TextMeshProUGUI text, float fontSize)
        {
            var le = text.GetComponent<LayoutElement>();
            if (le == null) le = text.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = fontSize * V6HudLogic.LyricLineHeightFactor;
        }

        /// <summary>
        /// 歌詞行の fs を変えつつ行高を追従させる (Ready prompt の拡大 fs 対応)。
        /// 行高が行送り未満だと TMP は 1 行も描画しない。
        /// </summary>
        private void SetLyricTextFontSize(float fontSize)
        {
            lyricText.fontSize = fontSize;
            SetLyricRowHeight(lyricText, fontSize);
        }

        /// <summary>
        /// 演奏中の歌詞データの有無 (Issue #789)。
        /// 「読み込み未完了」「データなし確定」「データあり」を分ける —
        /// 空行 (本文が空白の LyricLine) はデータあり側に数える
        /// (譜面に歌詞行が存在する以上、間の空行で枠を消さない)。
        /// </summary>
        private enum LyricDataPresence
        {
            /// <summary>InputJson 未到着 (読み込み未完了)。枠の出し入れは保留扱いで出す。</summary>
            Pending,
            /// <summary>InputJson 確定済みで表示区間を持つ行が 0 = 歌詞データなし。</summary>
            None,
            /// <summary>表示区間を持つ行が 1 つ以上ある = 歌詞データあり。</summary>
            Some,
        }

        /// <summary>
        /// 演奏中 (played) の枠ポリシーが参照する歌詞データの状態。
        /// <see cref="EnsureLyricLines"/> が InputJson 確定後に lyricLines を
        /// 同期的に構築するため、InputJson が届いていれば lyricLines は
        /// 必ず非 null (空リスト = データなし確定) になる。
        /// </summary>
        private LyricDataPresence PlaybackLyricPresence()
        {
            if (lyricSource == null || lyricSource.InputJson == null)
            {
                return LyricDataPresence.Pending;
            }

            return LyricsTimeline.HasLines(lyricLines)
                ? LyricDataPresence.Some
                : LyricDataPresence.None;
        }

        /// <summary>
        /// 歌詞スロットの枠 (lyricBox) の可視性ポリシー (Issue #734 / #789)。
        ///
        /// 枠の出し入れは「ユーザー設定 (<see cref="PlaySession.LyricsEnabled"/>)」・
        /// 「HUD の再生状態」・「譜面の歌詞データ有無」で決める (v0.2.1)。
        /// 読み込み完了タイミング・行の空白・行間・表示区間には依存しない。
        /// 歌詞本文・Start Prompt は枠の子として独立に出し入れし、
        /// 枠の寸法や可視性を変えない。
        ///
        /// 再生状態ごとの枠の扱い (v0.2.1 実機レビューで再修正):
        /// - 再開カウントダウン / START 表示 / 曲終了オーバーレイが出ている間は、
        ///   それらが同じスロットを占有する (同位置・同寸法のパネルを自分で持つ)。
        ///   開始・終了フェーズでも枠相当のパネルは必ず出る。
        /// - Space 待ち (!played) は枠の中に Start Prompt (「Space to start」) を
        ///   出す。開始操作の案内であって歌詞ではないため、歌詞設定に関わらず
        ///   枠を出す (設定 OFF でも開始方法が分かる必要がある)。
        /// - 演奏中 (played) は「設定 ON かつ譜面に歌詞データがある」ときだけ
        ///   枠を出す。歌詞データが無い譜面では曲中を通して隠す。
        ///   Issue #789: データ状態は <see cref="LyricDataPresence"/> の
        ///   3 値 (未到着 / なし確定 / あり) で区別する。未到着の間だけ
        ///   判断を保留して出し、空行 (本文が空白の行) は「データあり」
        ///   として扱う。行の有無・表示区間・間奏には依存しない
        ///   (データがあれば歌詞の無い区間でも枠は出しっぱなし)。
        /// </summary>
        private void UpdateLyrics()
        {
            // カウントダウン中・曲終了表示中は歌詞と Start Prompt を再表示しない。
            if (countdownOverlay != null && countdownOverlay.activeSelf)
            {
                return;
            }
            if (endOverlay != null && endOverlay.activeSelf)
            {
                return;
            }

            // Space 待ちの間は歌詞位置に design の Start Prompt (KeyCap「Space」+
            // 「to start」) を出す。別シーン (v5 ReadyView) はいらない — Issue #233。
            // Start Prompt は歌詞ではなく開始操作の案内なので、ここでは
            // 歌詞設定 (LyricsEnabled) を見ずに枠を出す (Issue #734 の
            // ポリシーは演奏中の枠にだけ適用する)。
            if (!GManager.played)
            {
                if (lyricIndex != -1)
                {
                    lyricIndex = -1;
                    lyricText.text = "";
                }

                SetLyricLinesVisible(false);
                SetLyricPromptVisible(true);
                SetLyricBoxVisible(true);
                return;
            }

            EnsureLyricLines();

            if (!PlaySession.LyricsEnabled)
            {
                if (lyricIndex != -1)
                {
                    lyricIndex = -1;
                    lyricText.text = "";
                }

                SetLyricPromptVisible(false);
                SetLyricLinesVisible(false);
                SetLyricBoxVisible(false);
                return;
            }

            // Issue #789 (v0.2.1 実機受入): 歌詞データの状態を 3 値で区別する。
            // 「読み込み未完了」「歌詞データなし」「歌詞データあり (空行を含む)」
            // を 1 つの真偽値に畳むと、到着前の一瞬で枠が消えるフリッカーや、
            // データのある譜面で間奏ごとに枠が消える不具合になる。
            LyricDataPresence presence = PlaybackLyricPresence();
            // Pending (InputJson 未到着) と Some (歌詞データあり) は枠を出す。
            // Some は間奏・表示区間外・全行が空白の区間でも出しっぱなしにし、
            // 本文だけを空にする。None (歌詞データなし確定) だけが曲中ずっと隠す。
            bool boxVisible = presence != LyricDataPresence.None;
            SetLyricPromptVisible(false);
            SetLyricLinesVisible(presence == LyricDataPresence.Some);
            SetLyricBoxVisible(boxVisible);

            if (presence != LyricDataPresence.Some)
            {
                if (lyricIndex != -1)
                {
                    lyricIndex = -1;
                    lyricText.text = "";
                }

                return;
            }

            int index = LyricsTimeline.IndexAt(lyricLines, GManager.step_time);
            if (index != lyricIndex)
            {
                lyricIndex = index;
                bool hasLine = index >= 0 && index < lyricLines.Count;
                lyricText.text = hasLine && lyricLines[index].HasText
                    ? lyricLines[index].Text
                    : "";
                SetLyricTextFontSize(ScaledLyricFs); // Ready prompt の拡大 fs を戻す
            }
        }

        /// <summary>歌詞行 (本文 1 行) の表示。Start Prompt と排他。</summary>
        private void SetLyricLinesVisible(bool visible)
        {
            if (lyricText != null && lyricText.gameObject.activeSelf != visible)
            {
                lyricText.gameObject.SetActive(visible);
            }
        }

        private void SetLyricPromptVisible(bool visible)
        {
            if (lyricPrompt != null && lyricPrompt.activeSelf != visible)
            {
                lyricPrompt.SetActive(visible);
            }
        }

        /// <summary>
        /// 歌詞枠の表示/非表示。可視性の判断は <see cref="UpdateLyrics"/> の
        /// ポリシー (設定値と再生状態) に一本化する。本文が空でも枠は隠さない
        /// (Issue #734: 空の本文は子の責務で、枠の出し入れには使わない)。
        /// </summary>
        private void SetLyricBoxVisible(bool visible)
        {
            if (lyricBox != null && lyricBox.activeSelf != visible)
            {
                lyricBox.SetActive(visible);
            }
        }

        private void EnsureLyricLines()
        {
            if (lyricSource == null)
            {
                lyricSource = FindFirstObjectByType<GameSceneManager>();
            }

            if (lyricSource == null || lyricSource.InputJson == null)
            {
                return;
            }

            if (lyricData == lyricSource.InputJson)
            {
                return;
            }

            lyricData = lyricSource.InputJson;
            // Issue #433: v6 HUD は歌詞本文だけを使う。振り仮名・ローマ字は
            // 読み込まない (LyricLine のフィールド自体は v5 側が使うので残す)。
            lyricLines = LyricsTimeline.Build(
                lyricData.StartTime ?? new float[0],
                lyricData.EndTime ?? new float[0],
                lyricData.Mondai ?? new string[0],
                null,
                null);
            lyricIndex = -2;
        }
    }
}
