using System.Collections.Generic;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Game.Playfield;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V6.Game.Hud
{
    /// <summary>
    /// v6 Game HUD (Issue #220, design/flashbeat-ui.pen の Game · HUD フレーム)。
    ///
    /// - すべてコードから uGUI を生成する (v5 GameHudView と同じ方針)。
    /// - 表示値はすべて実データ: GManager (score/combo/perfect/great/bad/miss/step_time)、
    ///   KeyBindings (キーガイド, Changed 購読で即時更新)、
    ///   PlaySessionSnapshot (曲の長さ = 進捗バー)、GameSceneManager.InputJson (歌詞)、
    ///   Judge がレーンに Instantiate する判定クローン (レーン別判定行)。
    /// - 判定クローンは v5 と同じく読み取った後に非表示にする (3D プレイフィールドには
    ///   判定ポップアップを出さず、画面最下部の判定行に一本化)。
    /// - 「準備中 / 失敗」の表示は既存 HudPlayStatus (v5) の責務。二重に出さない。
    /// - 寸法は <see cref="V6HudMetrics"/> の解像度プロファイル (契約 §8)。
    ///   design px を実ピクセルのまま使い、等比拡大 (引き伸ばし) はしない
    ///   (Owner 指示 2026-11-08)。ルートは 1:1 でスクリーン左上に置く。
    ///
    /// MusicManager / Judge / NotesManager は所有外。変更しない。
    /// </summary>
    public sealed class V6GameHudView : MonoBehaviour
    {
        private const float JudgeShowSeconds = 0.8f;
        private const float VerdictScanSeconds = 1f;

        // --- 歌詞枠 (design i30vM4) ---
        private const float LyricFuriganaFs = 22f;
        private const float LyricFs = 44f;
        private const float LyricRomajiFs = 20f;
        private static readonly Color LyricBoxColor = new Color(0f, 0f, 0f, 0x99 / 255f);

        /// <summary>上部シェードのグラデーション (上端の不透明度)。</summary>
        private const byte TopShadeAlpha = 0xA6;

        /// <summary>上部シェード用の縦グラデーションスプライト (全 HUD で共有)。</summary>
        private static Sprite topShadeSprite;

        /// <summary>Judge がレーンに置く判定エフェクトのクローン名 (Judge.MessageObj 順)。</summary>
        private static readonly string[] JudgeCloneNames = { "Perfect(Clone)", "Great(Clone)", "Bad(Clone)", "Miss(Clone)" };

        public RectTransform Root { get; private set; }

        /// <summary>メニューボタン (統合側が PauseMenuView 等に接続できるように公開)。</summary>
        public Button MenuButton { get; private set; }

        private V6HudMetrics m;

        // --- 生成した部品 ---
        private TextMeshProUGUI gradeText;
        private TextMeshProUGUI scoreText;
        private TextMeshProUGUI comboText;
        private TextMeshProUGUI lyricText;
        private GameObject lyricBox;
        private RectTransform progressFill;
        private RectTransform progressTrack;
        private RectTransform gaugeFill;
        private RectTransform gaugeBest;
        private RectTransform comboFill;
        private GameObject comboStatusBlock;
        private GameObject comboStatusRow;
        private GameObject fullComboChip;
        private GameObject keyGuideRow;
        private readonly List<TextMeshProUGUI> keyCaps = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> verdictTexts = new List<TextMeshProUGUI>();

        // --- 毎フレームの状態 ---
        private int lastJudged = -1;
        private int lastScore = -1;
        /// <summary>最後に文字として出した値 (同じ値なら TextMeshPro を触らない。#381)。</summary>
        private int lastRenderedScore = int.MinValue;
        private int lastRenderedCombo = int.MinValue;
        private float scorePulseStart = -1f;
        private float verdictScanUntil;
        private readonly HashSet<Object> seenJudgeIds = new HashSet<Object>();
        private readonly string[] laneVerdicts = new string[V6LaneGeometry.LaneCount];
        private readonly float[] laneVerdictUntil = new float[V6LaneGeometry.LaneCount];

        // --- 歌詞 ---
        private GameSceneManager lyricSource;
        private TextData lyricData;
        private List<LyricLine> lyricLines;
        private int lyricIndex = -2;
        private TextMeshProUGUI lyricFuriganaText;
        private TextMeshProUGUI lyricRomajiText;
        /// <summary>開始待ちの案内 (design HcP8N の Start Prompt)。歌詞行とは排他。</summary>
        private GameObject lyricPrompt;

        /// <summary>再開カウントダウン (design o0zh7 の Countdown xuQnv)。歌詞と排他。</summary>
        private GameObject countdownOverlay;
        private TextMeshProUGUI countdownText;

        /// <summary>曲終了の黒フェード (全画面) と「終了」表示 (design xXKic)。</summary>
        private Image endFade;
        private GameObject endOverlay;

        /// <summary>
        /// Canvas 配下に自分自身を作る。親は Canvas の RectTransform
        /// (Constant Pixel Size x1 前提 = v5 CanvasConfigurator 方式)。
        /// </summary>
        public static V6GameHudView Create(RectTransform parent)
        {
            var go = V5Ui.Node(parent.gameObject, "V6GameHud");
            var view = go.AddComponent<V6GameHudView>();
            view.Build();
            return view;
        }

        /// <summary>表示/非表示。非表示中は Refresh も何もしない。</summary>
        public void SetVisible(bool visible)
        {
            if (Root != null)
            {
                Root.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// 実データを UI へ反映する。毎フレーム呼んでよい (冪等)。
        /// 自身の Update からも呼ぶので、統合側が呼ばなくても動く。
        /// </summary>
        public void Refresh()
        {
            if (Root == null || !Root.gameObject.activeInHierarchy)
            {
                return;
            }

            int score = GManager.score;
            // TextMeshPro の文字列設定は値が変わるまで呼ばない (issue #381:
            // 毎フレームの書式化は GC を呼び、それがカクつきになる)。
            if (score != lastRenderedScore)
            {
                gradeText.text = GameHudLogic.RankText(score);
                scoreText.text = GameHudLogic.FormatScore(score);
                lastRenderedScore = score;
            }

            if (lastScore >= 0 && score != lastScore)
            {
                scorePulseStart = Time.unscaledTime;
            }

            lastScore = score;

            // スコアゲージ (塗りは + セグメントの手前まで, Best は保存スコア)
            gaugeFill.sizeDelta = new Vector2(
                V6HudLogic.GaugeFillWidth(score, m.GaugeFillMax), m.GaugeFillH);
            int hi = GManager.songID >= 0 && GManager.songID < GManager.Hiscore.Length
                ? GManager.Hiscore[GManager.songID]
                : GManager.BestScoreOf(GManager.songID); // 固定テーブル外は安定キー (issue-162 A-6)
            gaugeBest.anchoredPosition = new Vector2(
                V6HudLogic.GaugeBestX(hi, m.GaugeFillMax), 0f);

            // コンボ (表示は値が変わったときだけ更新する)
            if (GManager.combo != lastRenderedCombo)
            {
                comboText.text = GManager.combo.ToString();
                lastRenderedCombo = GManager.combo;
            }

            int judged = GManager.perfect + GManager.great + GManager.bad + GManager.miss;
            bool showStatus = V6HudLogic.ShowComboStatus(GManager.miss, judged);
            // 契約 §12: この座標 (FHD 1512,460) は Combo Status が使う
            // (旧 Combo Bonus block は廃止 — issue-162 B-4)。
            comboStatusBlock.SetActive(showStatus);

            // FULL COMBO は曲完走 (全ノーツ判定済み) かつノーミスのときだけ出す。
            // 演奏中ずっと出しっぱなしにすると内容を反映しない表示になる (Issue #233)。
            var currentSong = GManager.GetSong(GManager.songID);
            bool songComplete = GManager.played && currentSong != null && currentSong.totalHits > 0
                && judged >= currentSong.totalHits;
            if (fullComboChip != null && fullComboChip.activeSelf != (songComplete && GManager.miss == 0))
            {
                fullComboChip.SetActive(songComplete && GManager.miss == 0);
            }

            int milestone = GameHudLogic.NextBonusMilestone(GManager.combo);
            comboFill.sizeDelta = new Vector2(
                V6HudLogic.ComboFillFraction(GManager.combo, milestone) * m.StatusTrackW, m.StatusTrackH);

            // issue-162 B-4 再照合 (2026-11-11): 正本 Uc9v6 には Gx3kq "Combo Bonus" が
            // BIKrJ "Combo Status" と同じ (1512,460) に重ねて置かれている (design 側の
            // 状態の重複)。Gx3kq の「+1,200」「次のボーナス 300 / +3,000」は
            // JudgeScoreboard にコンボボーナス加点が無いため実在しない値で、
            // BonusTotal / NextBonusReward も近似式にすぎない。加点そのものは
            // 新しいゲーム仕様 (今回のスコープ外) なので、数値を捏造せず
            // 同じ座標を Combo Status (NO MISS / FULL COMBO) が使う。

            // 進捗バー + 歌詞 (メディアタイム = GManager.step_time)
            float duration = CurrentSongDuration();
            progressFill.sizeDelta = new Vector2(
                V6HudLogic.ProgressFillWidth(GManager.step_time, duration, progressTrack.rect.width),
                progressFill.rect.height);

            UpdateLyrics();
            UpdateLaneVerdicts();
            UpdateLaneGrids();
        }

        private void Update()
        {
            ApplyRootTransform();

            // 解像度変更 (プロファイル境界をまたぐ) で HUD を作り直す。
            // 寸法は design px を実ピクセルで使うので、アンカー追従だけでは対応できない。
            Vector2 canvasSize = CanvasSize();
            if (m == null || !m.MatchesResolution(canvasSize.x, canvasSize.y))
            {
                Rebuild();
            }

            Refresh();
            UpdateScorePulse();
        }

        /// <summary>加算直後だけスコア値を拡大する。レイアウト (anchoredPosition) は変えない。</summary>
        private void UpdateScorePulse()
        {
            if (scoreText == null)
            {
                return;
            }

            float scale = 1f;
            if (scorePulseStart >= 0f)
            {
                float elapsed = Time.unscaledTime - scorePulseStart;
                scale = V6HudLogic.ScorePulseScale(elapsed);
                if (elapsed >= V6HudLogic.ScorePulseSeconds)
                {
                    scorePulseStart = -1f;
                }
            }

            scoreText.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// 描画面 (Canvas) の実ピクセル寸法。v6 の Canvas は
        /// CanvasConfigurator (Constant Pixel Size x1) の ScreenSpaceOverlay なので
        /// Canvas の rect サイズ = 実スクリーン解像度。
        /// </summary>
        private Vector2 CanvasSize()
        {
            if (cachedCanvas == null)
            {
                cachedCanvas = GetComponentInParent<Canvas>();
            }

            var rt = cachedCanvas != null ? cachedCanvas.transform as RectTransform : null;
            if (rt != null && rt.rect.width > 0f && rt.rect.height > 0f)
            {
                return rt.rect.size;
            }

            return new Vector2(Screen.width, Screen.height);
        }

        private Canvas cachedCanvas;

        /// <summary>
        /// HUD ルートを実スクリーン解像度の <b>1:1</b> (等倍) で置く。
        ///
        /// 親フレーム (V6GameRuntime.hudFrame) は Canvas (= 実スクリーン解像度) 全体を
        /// 覆い、scale は常に 1 (引き伸ばしなし / Owner 指示 2026-11-08)。
        /// かつて親の等比拡大を 1/parentScale で打ち消す逆倍率を毎フレーム掛けていたが、
        /// 親が等倍になったので廃止。ルートは左上アンカーで親フレームいっぱいに置き、
        /// 子は design px の絶対座標 (左上原点) と端アンカーの両方で置ける。
        /// 解像度変更で親が変わるので毎フレーム同期する (変化があればのみ dirty)。
        /// </summary>
        private void ApplyRootTransform()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            var size = parent.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                return;
            }

            var rt = GetRect(gameObject);
            if (rt.anchorMin == new Vector2(0f, 1f) && rt.anchorMax == new Vector2(0f, 1f)
                && rt.pivot == new Vector2(0f, 1f) && rt.anchoredPosition == Vector2.zero
                && rt.sizeDelta == size && rt.localScale == Vector3.one)
            {
                return;
            }

            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
        }

        /// <summary>
        /// 解像度プロファイルが変わったとき、HUD を作り直す。
        /// 子は全てコード生成なので消して <see cref="Build"/> し直すだけでよい。
        /// V5GameUi.DestroyNow は Play モードではフレーム末破棄なので、旧子が
        /// 1 フレームだけ残る (解像度変更の瞬間のみの二重描画で、実害はない)。
        /// </summary>
        private void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                V5GameUi.DestroyNow(transform.GetChild(i).gameObject);
            }

            keyCaps.Clear();
            verdictTexts.Clear();
            seenJudgeIds.Clear();
            lastJudged = -1;
            songIdCached = int.MinValue;
            lyricData = null;
            lyricLines = null;
            lyricIndex = -2;
            cachedCanvas = null;
            progressTrack = null;
            progressFill = null;
            gaugeFill = null;
            gaugeBest = null;
            comboFill = null;
            comboStatusBlock = null;
            comboStatusRow = null;
            fullComboChip = null;
            keyGuideRow = null;
            gradeText = null;
            scoreText = null;
            comboText = null;
            lyricText = null;
            lyricFuriganaText = null;
            lyricRomajiText = null;
            lyricBox = null;
            lyricPrompt = null;
            MenuButton = null;

            Build();
        }

        private void OnEnable()
        {
            KeyBindings.Changed += RebuildKeyGuide;
        }

        private void OnDestroy()
        {
            KeyBindings.Changed -= RebuildKeyGuide;
        }

        // --- 構築 ---

        private void Build()
        {
            // 契約 §8: HUD は解像度ごとの固有フレーム (1920×1080 / 2560×1440 / 2560×1664)。
            // design px を実ピクセルのまま使い、等比拡大 (引き伸ばし) はしない
            // (Owner 指示 2026-11-08)。
            Vector2 canvasSize = CanvasSize();
            m = V6HudMetrics.ForResolution(canvasSize.x, canvasSize.y);
            // Root は自分自身。未代入だと Refresh のガードが常に早期 return し、
            // スコア / コンボ / 歌詞 / 判定行 / レーン格子の更新が一切走らない。
            Root = GetRect(gameObject);
            ApplyRootTransform();

            BuildDim();
            BuildTopShade();
            BuildProgressBar();
            BuildLyric();
            BuildCountdown();
            BuildGrade();
            BuildScoreGauge();
            BuildScoreTexts();
            BuildMenuButton();
            BuildComboBlock();
            BuildRightColumn();
            BuildVerdictRow();
            BuildKeyGuide();
            BuildEndOverlay();
        }

        private void BuildDim()
        {
            var dim = V5Ui.Node(gameObject, "Video Dim");
            var rt = GetRect(dim);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = dim.AddComponent<Image>();
            img.color = V6LaneGeometry.VideoDimColor;
            img.raycastTarget = false;
        }

        private void BuildTopShade()
        {
            var shade = V5Ui.Node(gameObject, "Top Shade");
            V5Ui.PinTop(GetRect(shade), m.TopShadeH, 0f);
            var img = shade.AddComponent<Image>();
            // 上端 #000000A6 → 下端 #00000000。一様な黒帯にすると下端の境界が見えるため、
            // 縦グラデーションにして動画の暗部へ溶かす (契約 §8)。
            img.sprite = TopShadeSprite();
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = false;
        }

        /// <summary>
        /// 1×2 px の縦グラデーション。下が透明、上が TopShadeAlpha。
        /// バイリニア補間で高さ方向に滑らかに伸ばすため、解像度ごとに作り直さない。
        /// </summary>
        private static Sprite TopShadeSprite()
        {
            if (topShadeSprite != null)
            {
                return topShadeSprite;
            }

            var tex = new Texture2D(1, 2, TextureFormat.RGBA32, false);
            tex.name = "TopShadeGradient";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
            tex.SetPixel(0, 1, new Color(0f, 0f, 0f, TopShadeAlpha / 255f));
            tex.Apply(false, false);

            topShadeSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 2f),
                new Vector2(0.5f, 0.5f), 1f);
            topShadeSprite.name = "TopShadeGradient";
            topShadeSprite.hideFlags = HideFlags.HideAndDontSave;
            return topShadeSprite;
        }

        private void BuildProgressBar()
        {
            var bar = V5Ui.Node(gameObject, "Progress Bar");
            var rt = V5Ui.PinTop(GetRect(bar), m.ProgressH, 0f);
            var img = bar.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0xAA / 255f);
            img.raycastTarget = false;
            progressTrack = rt;

            var fill = V5Ui.Node(bar, "Fill");
            var fillRt = GetRect(fill);
            fillRt.anchorMin = new Vector2(0f, 0.5f);
            fillRt.anchorMax = new Vector2(0f, 0.5f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(0f, m.ProgressH);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = V5Tokens.ButtonPrimaryBg; // design: #EDEDED
            fillImg.raycastTarget = false;
            progressFill = fillRt;
        }

        private void BuildLyric()
        {
            // design i30vM4 (契約 §8): ゲームフレーム 1728×972 の (304,250) 1120×220 を
            // プレイフィールドと同じ高さ合わせ写像で置く。FHD y=278 / 2K y=370 / Mac y=428。
            // 内部の Furigana fs22 / Lyrics fs44 / Romaji fs20、gap 6、padding 24/16 は
            // design のまま (契約 §8)。
            Vector2 lyricSize = V6HudLogic.LyricSize(CanvasSize().y);
            var box = V5Ui.Panel(gameObject, "Lyric Box", lyricSize, LyricBoxColor, 8f);
            box.GetComponent<Image>().raycastTarget = false;
            lyricBox = box;
            var boxRt = GetRect(box);
            boxRt.anchorMin = new Vector2(0.5f, 1f);
            boxRt.anchorMax = new Vector2(0.5f, 1f);
            boxRt.pivot = new Vector2(0.5f, 1f);
            boxRt.anchoredPosition = new Vector2(0f, -m.LyricY);

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
            lyricFuriganaText = V5Ui.Text(box, "Furigana", "", LyricFuriganaFs, V5Tokens.TextDim,
                TextAlignmentOptions.Center);
            SetLyricRowHeight(lyricFuriganaText, LyricFuriganaFs);
            lyricText = V5Ui.Text(box, "Lyrics", "", LyricFs, V5Tokens.Text, TextAlignmentOptions.Center);
            SetLyricRowHeight(lyricText, LyricFs);
            lyricRomajiText = V5Ui.Text(box, "Romaji", "", LyricRomajiFs, V5Tokens.TextDim,
                TextAlignmentOptions.Center);
            SetLyricRowHeight(lyricRomajiText, LyricRomajiFs);

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
            var rt = GetRect(countdownOverlay);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = V6HudLogic.LyricSize(CanvasSize().y);
            rt.anchoredPosition = new Vector2(0f, -m.LyricY);

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

            countdownText.text = "START";
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
            var fadeRt = GetRect(fadeGo);
            fadeRt.anchorMin = Vector2.zero;
            fadeRt.anchorMax = Vector2.one;
            fadeRt.offsetMin = Vector2.zero;
            fadeRt.offsetMax = Vector2.zero;
            endFade = fadeGo.AddComponent<Image>();
            endFade.color = new Color(0f, 0f, 0f, 0f);
            endFade.raycastTarget = false;
            fadeGo.SetActive(false);

            endOverlay = V5Ui.Node(gameObject, "Song End Overlay");
            var rt = GetRect(endOverlay);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = V6HudLogic.LyricSize(CanvasSize().y);
            rt.anchoredPosition = new Vector2(0f, -m.LyricY);

            float scale = V6LaneGeometry.ScreenScale(CanvasSize());
            var box = V5Ui.Panel(endOverlay, "Panel", V6HudLogic.LyricSize(CanvasSize().y),
                LyricBoxColor, 8f);
            box.GetComponent<Image>().raycastTarget = false;

            var title = V5Ui.Text(box, "Title", "終了", V6HudLogic.SongEndTitleFontSize(scale),
                V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(0f, 34f * scale);
            titleRt.offsetMax = new Vector2(0f, -8f * scale);

            var sub = V5Ui.Text(box, "Sub", "リザルトへ", V6HudLogic.SongEndSubFontSize(scale),
                V5Tokens.TextDim, TextAlignmentOptions.Center);
            var subRt = sub.rectTransform;
            subRt.anchorMin = new Vector2(0f, 0f);
            subRt.anchorMax = new Vector2(1f, 0f);
            subRt.pivot = new Vector2(0.5f, 0f);
            subRt.anchoredPosition = new Vector2(0f, 16f * scale);
            subRt.sizeDelta = new Vector2(0f, 28f * scale);

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
            FillRect(capKey.rectTransform);
            float capW = MeasureTextWidth(capKey, "Space", V6HudLogic.ReadyPromptFontSize)
                + V6Ui.KeyCapPaddingX * 2f;
            GetRect(cap).sizeDelta = new Vector2(capW, V6HudLogic.ReadyPromptCapHeight);

            var label = V5Ui.Text(lyricPrompt, "Label", "to start", V6HudLogic.ReadyPromptFontSize,
                V5Tokens.TextMuted, TextAlignmentOptions.Left);
            float labelW = MeasureTextWidth(label, "to start", V6HudLogic.ReadyPromptFontSize);
            GetRect(label).sizeDelta = new Vector2(labelW, V6HudLogic.ReadyPromptCapHeight);

            lyricPrompt.SetActive(false);
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

        private void BuildGrade()
        {
            gradeText = V5Ui.Text(gameObject, "Grade", "C", m.GradeFs, V5Tokens.Text,
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            var rt = gradeText.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(m.Margin, -m.Margin);
            rt.sizeDelta = new Vector2(m.GaugeX - m.Margin, m.GradeFs * 1.2f);
        }

        private void BuildScoreGauge()
        {
            var gauge = V5Ui.Node(gameObject, "Score Gauge");
            V5Ui.Place(GetRect(gauge), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(m.GaugeX, -m.GaugeY), new Vector2(m.GaugeW, m.GaugeH));

            // Track (k0, 角丸 2)
            var track = V5Ui.Panel(gauge, "Track", new Vector2(m.GaugeW, m.GaugeTrackH),
                V5Tokens.Key0, 2f);
            var trackRt = GetRect(track);
            trackRt.anchorMin = new Vector2(0f, 1f);
            trackRt.anchorMax = new Vector2(0f, 1f);
            trackRt.pivot = new Vector2(0f, 1f);
            trackRt.anchoredPosition = Vector2.zero;

            // セグメント C/B/A/S/SS/+ (design は track と同色。+ だけ契約 §8 の #3A3220)。
            for (int i = 0; i < m.GaugeSegX.Length; i++)
            {
                var seg = V5Ui.Panel(track, "Seg " + i,
                    new Vector2(m.GaugeSegW[i], m.GaugeSegH),
                    i == m.GaugeSegX.Length - 1 ? V6HudLogic.AccentBase : V5Tokens.Key0, 2f);
                var segRt = GetRect(seg);
                segRt.anchorMin = new Vector2(0f, 0f);
                segRt.anchorMax = new Vector2(0f, 0f);
                segRt.pivot = new Vector2(0f, 0.5f);
                segRt.anchoredPosition = new Vector2(m.GaugeSegX[i], m.GaugeSegY);
            }

            // 塗り (Fill)
            var fill = V5Ui.Panel(track, "Fill",
                new Vector2(0f, m.GaugeFillH), V5Tokens.Text, 2f);
            var fillRt = GetRect(fill);
            fillRt.anchorMin = new Vector2(0f, 1f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 1f);
            fillRt.anchoredPosition = new Vector2(0f, 0f);
            gaugeFill = fillRt;

            // Best マーカー
            var best = V5Ui.Panel(track, "Best",
                new Vector2(m.GaugeBestW, m.GaugeBestH), V5Tokens.Warn, 2f);
            var bestRt = GetRect(best);
            bestRt.anchorMin = new Vector2(0f, 0.5f);
            bestRt.anchorMax = new Vector2(0f, 0.5f);
            bestRt.pivot = new Vector2(0f, 0.5f);
            bestRt.anchoredPosition = Vector2.zero;
            gaugeBest = bestRt;

            // ラベル行 (C B A S SS +)
            string[] labels = { "C", "B", "A", "S", "SS", "+" };
            for (int i = 0; i < labels.Length; i++)
            {
                var label = V5Ui.Text(gauge, "Label " + labels[i], labels[i], m.GaugeLabelFs,
                    V5Tokens.TextMuted, TextAlignmentOptions.Center);
                var labelRt = label.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 1f);
                labelRt.anchorMax = new Vector2(0f, 1f);
                labelRt.pivot = new Vector2(0f, 0f);
                labelRt.anchoredPosition = new Vector2(
                    m.GaugeSegX[i] + m.GaugeSegW[i] * 0.5f, -m.GaugeLabelY);
                labelRt.sizeDelta = new Vector2(m.GaugeSegW[i], m.GaugeLabelFs * 1.4f);
            }
        }

        private void BuildScoreTexts()
        {
            var label = V5Ui.Text(gameObject, "SCORE", "SCORE", m.ScoreLabelFs, V5Tokens.TextMuted,
                TextAlignmentOptions.TopLeft);
            PlaceTopLeft(label.rectTransform, m.GaugeX, -m.ScoreLabelY);

            scoreText = V5Ui.Text(gameObject, "Score Value", "0", m.ScoreFs, V5Tokens.Text,
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            // 1 行固定。8 桁になっても折り返さず、幅を超えた分だけ自動縮小する
            // (下限 28px、契約 §8)。rect 幅はゲージ幅に合わせる。
            scoreText.textWrappingMode = TextWrappingModes.NoWrap;
            scoreText.enableAutoSizing = true;
            scoreText.fontSizeMin = V6HudLogic.ScoreAutoSizeMin;
            scoreText.fontSizeMax = m.ScoreFs;
            PlaceTopLeft(scoreText.rectTransform, m.GaugeX, -m.ScoreValueY);
            scoreText.rectTransform.sizeDelta = new Vector2(m.GaugeW, m.ScoreFs * 1.4f);
        }

        private void BuildMenuButton()
        {
            var btn = V5Ui.Panel(gameObject, "Menu Button",
                new Vector2(m.MenuW, m.MenuH), V5Tokens.Key4, 8f);
            var rt = GetRect(btn);
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-m.Margin, -m.Margin);

            // design T3U7u6: [ラベル fs22/30][gap 10][KeyCap] を 180×48 の中で中央寄せ。
            // KeyCap は padding [0,10] の自動幅・自動高 (55×32 @(112,8) at FHD)。
            // 以前は layout padding 16 / gap 8 / 固定幅 52 で置き、さらに Key の fs が
            // 未代入 (0) だったため「Esc」が描画されなかった。
            var label = V5Ui.Text(btn, "Label", "メニュー", m.MenuFs, V5Tokens.Text,
                TextAlignmentOptions.Left);
            float labelW = MeasureTextWidth(label, "メニュー", m.MenuFs);

            var cap = V5Ui.Panel(btn, "KeyCap",
                new Vector2(0f, m.MenuCapH), new Color(0f, 0f, 0f, 0x40 / 255f), 4f);
            var capKey = V5Ui.Text(cap, "Key", "Esc", m.MenuFs, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            FillRect(capKey.rectTransform);
            float capW = MeasureTextWidth(capKey, "Esc", m.MenuFs) + V6Ui.KeyCapPaddingX * 2f;
            GetRect(cap).sizeDelta = new Vector2(capW, m.MenuCapH);

            float total = labelW + m.MenuGap + capW;
            float rowX = Mathf.Round((m.MenuW - total) * 0.5f);
            float rowY = Mathf.Round((m.MenuH - m.MenuCapH) * 0.5f);
            PlaceTopLeft(label.rectTransform, rowX, -rowY);
            GetRect(label).sizeDelta = new Vector2(labelW, m.MenuCapH);
            PlaceTopLeft(GetRect(cap), rowX + labelW + m.MenuGap, -rowY);

            MenuButton = btn.AddComponent<Button>();
            MenuButton.targetGraphic = btn.GetComponent<Image>();
            MenuButton.onClick.AddListener(OpenPauseMenu);
        }

        /// <summary>
        /// HUD のメニューボタンの既定リスナー。v6 ポーズが有効なときは何もしない —
        /// V6PauseRuntime が同じボタンを v6 ポーズへ接線する。ここで v5 PauseMenuView を
        /// 開くと (非表示のまま活性化して) v6 ポーズと二重表示・二重入力になる。
        /// </summary>
        private void OpenPauseMenu()
        {
            if (FlashBeat.UI.V6.V6UiMode.GameHud && FlashBeat.UI.V6.Game.Pause.V6PauseView.Enabled)
            {
                return;
            }

            var pause = FindFirstObjectByType<PauseMenuView>();
            if (pause != null)
            {
                pause.Open();
            }
        }

        private void BuildComboBlock()
        {
            // 契約 §8: Combo 左上は design px 絶対座標 (FHD (48,430) / 2K (64,573) / Mac (64,685))。
            // 1:1 固定配置なので中央アンカー + オフセット計算は不要 (左上アンカーでそのまま置く)。
            var block = V5Ui.Node(gameObject, "Combo Block");
            var rt = GetRect(block);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(m.ComboX, -m.ComboY);
            rt.sizeDelta = new Vector2(m.ComboW, m.ComboH);

            var label = V5Ui.Text(block, "COMBO", "COMBO", m.ComboLabelFs, V5Tokens.TextMuted,
                TextAlignmentOptions.TopLeft);
            PlaceTopLeft(label.rectTransform, 0f, 0f);

            comboText = V5Ui.Text(block, "Value", "0", m.ComboValueFs, V5Tokens.Text,
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            comboText.textWrappingMode = TextWrappingModes.NoWrap;
            PlaceTopLeft(comboText.rectTransform, 0f, -m.ComboValueY);
        }

        private void BuildRightColumn()
        {
            // 両 block は同座標 (排他) なので、コラムの高さは高い方 = ステータス block。
            // 契約 §8: 右コラム左上は design px (FHD (1512,460) / 2K・Mac (2016,613/725))。
            // 右端アンカーで置く (右端 = design 右マージン: FHD 48 / 2K・Mac 64) ので、
            // 未知解像度でプロファイルを流用したときも画面外へはみ出さない。
            float columnH = m.StatusBlockH;
            var column = V5Ui.Node(gameObject, "Right Column");
            var rt = GetRect(column);
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-m.Margin, -m.RightColY);
            rt.sizeDelta = new Vector2(m.RightColW, columnH);

            // --- コンボステータス ---
            // issue-162 B-4 再照合: ここにあった「+ COMBO BONUS」「次のボーナス」の
            // 2 行は実在しない加点の表示 (近似値 / 仮の式) なので廃止した。
            // 契約 §12: Combo Status はこの座標 (FHD 1512,460) を使う。
            var status = V5Ui.Node(column, "Combo Status");
            var statusRt = GetRect(status);
            statusRt.anchorMin = new Vector2(1f, 1f);
            statusRt.anchorMax = new Vector2(1f, 1f);
            statusRt.pivot = new Vector2(1f, 1f);
            statusRt.anchoredPosition = Vector2.zero;
            statusRt.sizeDelta = new Vector2(m.RightColW, m.StatusBlockH);
            comboStatusBlock = status;

            var track = V5Ui.Panel(status, "Track",
                new Vector2(m.StatusTrackW, m.StatusTrackH), V5Tokens.Key0, 4f);
            var trackRt = GetRect(track);
            trackRt.anchorMin = new Vector2(0f, 1f);
            trackRt.anchorMax = new Vector2(0f, 1f);
            trackRt.pivot = new Vector2(0f, 1f);
            trackRt.anchoredPosition = new Vector2(0f, -m.StatusTrackY);

            var fill = V5Ui.Panel(track, "Fill",
                new Vector2(0f, m.StatusTrackH), V5Tokens.Text, 4f);
            var fillRt = GetRect(fill);
            fillRt.anchorMin = new Vector2(0f, 0.5f);
            fillRt.anchorMax = new Vector2(0f, 0.5f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            comboFill = fillRt;

            comboStatusRow = V5Ui.Node(status, "Status Row");
            var rowRt = GetRect(comboStatusRow);
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(0f, 1f);
            rowRt.pivot = new Vector2(0f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -m.StatusRowY);
            rowRt.sizeDelta = new Vector2(m.StatusTrackW, m.StatusRowH);

            var rowLayout = comboStatusRow.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f * Scale;
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            var noMiss = V5Ui.Text(comboStatusRow, "No Miss", "NO MISS", m.StatusFs,
                V5Tokens.AccentLight);
            var noMissLe = noMiss.gameObject.AddComponent<LayoutElement>();
            noMissLe.minWidth = m.FcChipW * 0.6f;
            noMissLe.preferredHeight = m.StatusRowH;

            // 契約 §8: FC チップ 150×40 #3A3220 r4 (fs22 $warn-light)。
            var chip = V5Ui.Panel(comboStatusRow, "Full Combo",
                new Vector2(m.FcChipW, m.FcChipH), V6HudLogic.AccentBase, 4f);
            fullComboChip = chip;
            var chipLe = chip.AddComponent<LayoutElement>();
            chipLe.minWidth = m.FcChipW;
            chipLe.preferredHeight = m.FcChipH;
            V5Ui.Text(chip, "Text", "FULL COMBO", m.StatusFs, V5Tokens.WarnLight,
                TextAlignmentOptions.Center);
        }

        /// <summary>v6 のレーン別判定行を使うか (既定は false = 元の 3D 判定エフェクト)。</summary>
        private static bool VerdictRowEnabled
        {
            get { return FlashBeat.UI.V6.V6UiMode.VerdictRow; }
        }

        private void BuildVerdictRow()
        {
            // 既定 (VerdictRow=false) では判定のフィードバックは元の 3D エフェクトが担う。
            if (!VerdictRowEnabled) return;

            var row = V5Ui.Node(gameObject, "Verdict Row");
            V5Ui.PinBottom(GetRect(row), m.VerdictH, m.VerdictBottom);

            for (int lane = 0; lane < V6LaneGeometry.LaneCount; lane++)
            {
                var text = V5Ui.Text(row, "Verdict " + lane, "", m.VerdictFs, V5Tokens.Text,
                    TextAlignmentOptions.Center);
                text.textWrappingMode = TextWrappingModes.NoWrap;
                var rt = text.rectTransform;
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(m.KeyColumnCenter(lane), 0f);
                rt.sizeDelta = new Vector2(m.VerdictW, m.VerdictH);
                SetAlpha(text, 0f);
                verdictTexts.Add(text);
            }
        }

        private void BuildKeyGuide()
        {
            if (PlaySession.KeyGuide == KeyGuideMode.Off)
            {
                return;
            }

            keyGuideRow = V5Ui.Node(gameObject, "Key Guide");
            V5Ui.PinBottom(GetRect(keyGuideRow), m.KeyGuideH, m.KeyGuideBottom);

            for (int lane = 0; lane < KeyBindings.LaneCount; lane++)
            {
                // contract json: bg #3A3A3A r4 + ラベル $text-muted (Key fs24, design 空間)
                var cap = V6Ui.KeyCap(keyGuideRow, V6HudLogic.RepresentativeKeyText(KeyBindings.GetKeys(lane)),
                    new Vector2(m.KeyCapW, m.KeyCapH), m.KeyCapLabelFs);
                var label = cap.GetComponentInChildren<TextMeshProUGUI>();
                var rt = GetRect(cap);
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(m.KeyColumnCenter(lane), 0f);
                rt.sizeDelta = new Vector2(m.KeyCapW, m.KeyCapH);
                keyCaps.Add(label);
            }

            RefreshKeyCaps();
        }

        /// <summary>KeyBindings (正本) からキーガイドを作り直す (設定変更で即時)。</summary>
        private void RebuildKeyGuide()
        {
            if (keyGuideRow == null)
            {
                if (PlaySession.KeyGuide != KeyGuideMode.Off)
                {
                    BuildKeyGuide();
                }

                return;
            }

            if (PlaySession.KeyGuide == KeyGuideMode.Off)
            {
                V5GameUi.DestroyNow(keyGuideRow);
                keyGuideRow = null;
                keyCaps.Clear();
                return;
            }

            for (int i = keyCaps.Count - 1; i >= 0; i--)
            {
                V5GameUi.DestroyNow(keyCaps[i].gameObject);
            }

            keyCaps.Clear();
            BuildKeyGuide();
        }

        /// <summary>
        /// キーガイド / 判定行の 8 列の x を実際の画面へ合わせる (Issue #233 実機指摘 +
        /// 契約 §8 整合 + Owner 指示 2026-11-08「キーガイドをレーン中心に一致させる」)。
        ///
        /// - v6 プレイフィールド有効時: 契約 §6 のレーン中心線 (判定線 y=792 上の下端
        ///   x = 339 + 150·lane) を <see cref="V6HudMetrics.LaneCenterScreenX"/>
        ///   (= <see cref="V6LaneGeometry.DesignToScreen"/>: 高さ合わせ + 水平中央寄せ) で
        ///   実画面 x へ写す。プレイフィールドと同じ写像なので、キーガイド / 判定行は
        ///   レーン中心と必ず一致する (契約 §8 の KeyGuide 格子 310.. / 529.. / 464.. は
        ///   design サンプル値で、レーン中心 (FHD 376.7 / 2K 502.2 / Mac 381.1) とはずれる)。
        /// - レガシー 3D プレイフィールド時: レーン判定位置 (y=0.76, z=0.15) を
        ///   Camera.Main で投影してローカル x へ写す (契約 §8 ⚠ の「レーン中心への追従」)。
        ///   投影できない (カメラ無し等) ときは build 時の design px のまま。
        ///
        /// 判定行も同じ格子に載せる (契約 §8: 判定行サンプルは KeyGuide 格子と同位置)。
        ///
        /// 変換式は純ロジック (<see cref="V6HudLogic.ScreenXToTemplateX"/> /
        /// <see cref="V6HudMetrics.LaneCenterScreenX"/>) として EditMode テスト可能。
        /// </summary>
        private void UpdateLaneGrids()
        {
            Vector2 canvasSize = CanvasSize();
            // HUD ルートは 1:1 (等倍) なのでローカル x = スクリーン x。
            // lossyScale で割っておけば親フレームが何をしていても成立する。
            float rootScale = transform.lossyScale.x;
            if (rootScale <= 0f) return;

            if (FlashBeat.UI.V6.V6UiMode.Playfield)
            {
                for (int lane = 0; lane < V6LaneGeometry.LaneCount; lane++)
                {
                    float x = V6HudMetrics.LaneCenterScreenX(lane, canvasSize.x, canvasSize.y);
                    SetLaneGridX(lane, V6HudLogic.ScreenXToTemplateX(x, rootScale));
                }
                return;
            }

            var cam = Camera.main;
            if (cam == null) return;
            for (int lane = 0; lane < V6LaneGeometry.LaneCount; lane++)
            {
                Vector3 world = new Vector3(Judge.LaneWorldX(lane), 0.76f, 0.15f);
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0f) continue;
                SetLaneGridX(lane, V6HudLogic.ScreenXToTemplateX(screen.x, rootScale));
            }
        }

        /// <summary>
        /// レーン格子の第 lane 列へキーキャップと判定テキストを載せる。
        /// x は canvas (root) 左原点。子は PinBottom された行の中に (0, 0.5) アンカーで
        /// 置かれているので anchoredPosition は行左端基準 → 行の inset (offsetMin.x) を
        /// 引いてから載さないと行 inset 分 (Mac KeyGuide 45px / Verdict 98px) 右にずれる。
        /// キーキャップはラベル (TMP, stretch アンカー) ではなくキャップ (親) を動かす。
        /// </summary>
        private void SetLaneGridX(int lane, float canvasX)
        {
            if (lane < verdictTexts.Count)
            {
                var rt = verdictTexts[lane].rectTransform;
                rt.anchoredPosition = new Vector2(canvasX - ((RectTransform)rt.parent).offsetMin.x, rt.anchoredPosition.y);
            }
            if (lane < keyCaps.Count)
            {
                var cap = (RectTransform)keyCaps[lane].transform.parent;
                cap.anchoredPosition = new Vector2(canvasX - ((RectTransform)cap.parent).offsetMin.x, cap.anchoredPosition.y);
            }
        }

        private void RefreshKeyCaps()
        {
            for (int lane = 0; lane < keyCaps.Count; lane++)
            {
                keyCaps[lane].text = V6HudLogic.RepresentativeKeyText(KeyBindings.GetKeys(lane));
            }
        }

        // --- 毎フレームの更新 ---

        private float CurrentSongDuration()
        {
            if (songIdCached != GManager.songID)
            {
                songIdCached = GManager.songID;
                var song = PlaySessionSnapshot.ResolveSong(GManager.songID);
                songDurationCached = song != null ? song.duration : 0f;
            }

            return songDurationCached;
        }

        private int songIdCached = int.MinValue;
        private float songDurationCached;

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
                    lyricFuriganaText.text = "";
                    lyricRomajiText.text = "";
                }

                SetLyricLinesVisible(false);
                SetLyricPromptVisible(true);
                SetLyricBoxVisible(true);
                return;
            }

            EnsureLyricLines();
            if (lyricLines == null || lyricLines.Count == 0 || !PlaySession.LyricsEnabled)
            {
                if (lyricIndex != -1)
                {
                    lyricIndex = -1;
                    lyricText.text = "";
                    lyricFuriganaText.text = "";
                    lyricRomajiText.text = "";
                }

                SetLyricPromptVisible(false);
                SetLyricBoxVisible(false);
                return;
            }

            SetLyricPromptVisible(false);
            SetLyricLinesVisible(true);

            int index = LyricsTimeline.IndexAt(lyricLines, GManager.step_time);
            if (index != lyricIndex)
            {
                lyricIndex = index;
                bool hasLine = index >= 0 && index < lyricLines.Count;
                lyricText.text = hasLine && lyricLines[index].HasText
                    ? lyricLines[index].Text
                    : "";
                SetLyricTextFontSize(m.LyricFs); // Ready prompt の拡大 fs を戻す
                lyricFuriganaText.text = hasLine ? lyricLines[index].Furigana ?? "" : "";
                lyricRomajiText.text = hasLine ? lyricLines[index].Romaji ?? "" : "";
            }

            SetLyricBoxVisible(lyricText.text.Length > 0);
        }

        /// <summary>歌詞の 3 行 (Furigana / Lyrics / Romaji) の表示。Start Prompt と排他。</summary>
        private void SetLyricLinesVisible(bool visible)
        {
            if (lyricFuriganaText.gameObject.activeSelf != visible)
            {
                lyricFuriganaText.gameObject.SetActive(visible);
                lyricText.gameObject.SetActive(visible);
                lyricRomajiText.gameObject.SetActive(visible);
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
            lyricLines = LyricsTimeline.Build(
                lyricData.StartTime ?? new float[0],
                lyricData.EndTime ?? new float[0],
                lyricData.Mondai ?? new string[0],
                lyricData.Furigana ?? new string[0],
                lyricData.romaji ?? new string[0]);
            lyricIndex = -2;
        }

        /// <summary>
        /// レーン別判定行。Judge がレーンに Instantiate する判定クローン
        /// (Perfect/Great/Bad/Miss.prefab) を観測して、どのレーンで何が出たかを表示する。
        /// クローンは表示後に非表示化 (3D フィールドには判定を出さない)。
        /// </summary>
        private void UpdateLaneVerdicts()
        {
            int judged = GManager.perfect + GManager.great + GManager.bad + GManager.miss;
            if (judged != lastJudged)
            {
                if (judged < lastJudged)
                {
                    // Retry 等でカウンタが戻った: レーン判定をクリア
                    for (int lane = 0; lane < laneVerdicts.Length; lane++)
                    {
                        laneVerdicts[lane] = null;
                        laneVerdictUntil[lane] = 0f;
                        SetAlpha(verdictTexts[lane], 0f);
                    }
                }

                lastJudged = judged;
                verdictScanUntil = Time.unscaledTime + VerdictScanSeconds;
            }

            if (Time.unscaledTime < verdictScanUntil)
            {
                ScanJudgeClones();
            }

            float now = Time.unscaledTime;
            for (int lane = 0; lane < verdictTexts.Count; lane++)
            {
                float remain = laneVerdictUntil[lane] - now;
                if (remain <= 0f)
                {
                    if (laneVerdicts[lane] != null)
                    {
                        laneVerdicts[lane] = null;
                        SetAlpha(verdictTexts[lane], 0f);
                    }

                    continue;
                }

                SetAlpha(verdictTexts[lane], Mathf.Clamp01(remain / JudgeShowSeconds));
            }
        }

        private void ScanJudgeClones()
        {
            // 判定行が無い (VerdictRow=false) ときは表示先が無いので走査しない。
            if (verdictTexts.Count != V6LaneGeometry.LaneCount) return;

            var roots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var t = roots[i].transform;
                string n = t.name;
                bool isJudge = false;
                for (int j = 0; j < JudgeCloneNames.Length; j++)
                {
                    if (n == JudgeCloneNames[j])
                    {
                        isJudge = true;
                        break;
                    }
                }

                if (!isJudge || !seenJudgeIds.Add(t.gameObject))
                {
                    continue;
                }

                int lane = V6HudLogic.LaneFromEffectPositionX(t.position.x);
                string label = V6HudLogic.VerdictLabel(n);
                laneVerdicts[lane] = label;
                laneVerdictUntil[lane] = Time.unscaledTime + JudgeShowSeconds;
                verdictTexts[lane].text = label;
                verdictTexts[lane].color = V6HudLogic.VerdictColor(label);
                // 判定行を使わない既定では、元の 3D 判定エフェクトをそのまま残す
                // (Judge の挙動を変えない)。
                if (VerdictRowEnabled) t.gameObject.SetActive(false);
            }
        }

        private static void SetAlpha(TMP_Text text, float alpha)
        {
            var c = text.color;
            c.a = alpha;
            text.color = c;
        }

        private static void PlaceTopLeft(RectTransform rt, float x, float y)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>親いっぱいに広げる (KeyCap 内のラベルなど)。</summary>
        private static void FillRect(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// TMP の実測送り幅 (design px)。design の自動幅 KeyCap (padding [0,10]) を
        /// 再現するために、推定 em ではなく実フォントで測る。
        /// フォント未設定などで測れないときは em 推定に落ちる。
        /// </summary>
        private static float MeasureTextWidth(TMP_Text text, string content, float fontSize)
        {
            float fallback = V6Ui.LabelAdvanceEm(content) * fontSize;
            if (text == null)
            {
                return Mathf.Ceil(fallback);
            }

            text.text = content;
            text.fontSize = fontSize;
            text.ForceMeshUpdate();
            float width = text.GetPreferredValues(content).x;
            return Mathf.Ceil(width > 0f ? width : fallback);
        }

        private float Scale
        {
            get { return m == null ? 1f : m.Scale; }
        }

        private static RectTransform GetRect(Component component)
        {
            return component.GetComponent<RectTransform>();
        }

        private static RectTransform GetRect(GameObject go)
        {
            return go.GetComponent<RectTransform>();
        }
    }
}
