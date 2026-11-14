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
            // 幅は 1120 固定、高さは本文 1 行 (と Start Prompt) に合わせる。
            Vector2 lyricSize = V6HudLogic.LyricSize(ScaledLyricFs);
            var box = V5Ui.Panel(gameObject, "Lyric Box", lyricSize, LyricBoxColor, 8f);
            box.GetComponent<Image>().raycastTarget = false;
            lyricBox = box;
            var boxRt = V6Layout.RectOf(box);
            boxRt.anchorMin = new Vector2(0.5f, 1f);
            boxRt.anchorMax = new Vector2(0.5f, 1f);
            boxRt.pivot = new Vector2(0.5f, 1f);
            boxRt.anchoredPosition = LyricSlotAnchoredPosition();

            var layout = box.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 6f;
            layout.padding = new RectOffset(24, 24, 16, 16);
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
            SetLyricRowHeight(lyricText, ScaledLyricFs);

            BuildLyricPrompt(box);
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
            // Issue #433: 歌詞行だけになって箱が薄くなったので、カウントダウンの
            // 大きな数字が収まる高さは文字サイズから確保する。
            float countdownFs = V6HudLogic.CountdownFontSize(CanvasSize().y);
            Vector2 lyricSize = V6HudLogic.LyricSize(ScaledLyricFs);
            rt.sizeDelta = new Vector2(lyricSize.x, Mathf.Max(lyricSize.y, countdownFs * 1.3f));
            rt.anchoredPosition = LyricSlotAnchoredPosition();

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
            Vector2 lyricSize = V6HudLogic.LyricSize(ScaledLyricFs);
            // Issue #433: 「終了」だけを大きく出すので、タイトルが収まる高さを確保する。
            rt.sizeDelta = new Vector2(lyricSize.x, Mathf.Max(lyricSize.y, endTitleFs * 1.6f));
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
            var le = lyricPrompt.AddComponent<LayoutElement>();
            le.preferredHeight = V6HudLogic.ReadyPromptCapHeight;

            var layout = lyricPrompt.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = V6HudLogic.ReadyPromptGap;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var cap = V5Ui.Panel(lyricPrompt, "KeyCap Space",
                new Vector2(0f, V6HudLogic.ReadyPromptCapHeight), V5Tokens.Key4, 4f);
            var capKey = V5Ui.Text(cap, "Key", "Space", V6HudLogic.ReadyPromptFontSize,
                V5Tokens.TextMuted, TextAlignmentOptions.Center);
            V6Layout.StretchFill(capKey.rectTransform);
            float capW = MeasureTextWidth(capKey, "Space", V6HudLogic.ReadyPromptFontSize)
                + V6Ui.KeyCapPaddingX * 2f;
            V6Layout.RectOf(cap).sizeDelta = new Vector2(capW, V6HudLogic.ReadyPromptCapHeight);

            var label = V5Ui.Text(lyricPrompt, "Label", "to start", V6HudLogic.ReadyPromptFontSize,
                V5Tokens.TextMuted, TextAlignmentOptions.Left);
            float labelW = MeasureTextWidth(label, "to start", V6HudLogic.ReadyPromptFontSize);
            V6Layout.RectOf(label).sizeDelta = new Vector2(labelW, V6HudLogic.ReadyPromptCapHeight);

            lyricPrompt.SetActive(false);
        }

        /// <summary>
        /// 歌詞スロット (上中央アンカー) の anchoredPosition。
        /// 左上 = 共有フレーム origin + (304,250)。x はスロット中心 - 画面中心。
        /// </summary>
        private Vector2 LyricSlotAnchoredPosition()
        {
            Vector2 canvasSize = CanvasSize();
            Vector2 slotOrigin = V6HudLogic.LyricSlotOrigin(canvasSize.x, canvasSize.y);
            return new Vector2(
                slotOrigin.x + V6HudLogic.LyricDesignWidth * 0.5f - canvasSize.x * 0.5f,
                -slotOrigin.y);
        }

        /// <summary>歌詞行の高さを fs × 1.25 (行送り) で確保する。</summary>
        private static void SetLyricRowHeight(TextMeshProUGUI text, float fontSize)
        {
            var le = text.GetComponent<LayoutElement>();
            if (le == null) le = text.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = fontSize * 1.25f;
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

        /// <summary>歌詞: 再生時刻と一致した行だけを出す (固定文字列は常時出さない)。
        /// 中身が無いときは枠ごと隠す (空の黒い矩形を出さない)。</summary>
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
            // Issue #433: パネルは「行間」で消したりはしない。譜面に歌詞データが
            // 無いときだけパネル自体を隠す (Space to Start / カウントダウン /
            // 終了表示は別オーバーレイなので影響しない)。
            bool hasLyrics = lyricLines != null && lyricLines.Count > 0 && PlaySession.LyricsEnabled;
            if (!hasLyrics)
            {
                if (lyricIndex != -1)
                {
                    lyricIndex = -1;
                    lyricText.text = "";
                }

                SetLyricPromptVisible(false);
                SetLyricBoxVisible(false);
                return;
            }

            SetLyricPromptVisible(false);
            SetLyricLinesVisible(true);
            SetLyricBoxVisible(true);

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

        /// <summary>歌詞枠の表示/非表示。空の黒い矩形を出さない。</summary>
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
