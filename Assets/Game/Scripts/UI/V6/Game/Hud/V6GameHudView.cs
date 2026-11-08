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
    /// - 寸法は <see cref="V6HudMetrics"/> の 1920×1080 論理テンプレート。
    ///
    /// MusicManager / Judge / NotesManager は所有外。変更しない。
    /// </summary>
    public sealed class V6GameHudView : MonoBehaviour
    {
        private const float JudgeShowSeconds = 0.8f;
        private const float VerdictScanSeconds = 1f;

        // --- 歌詞枠 (design HcP8N) ---
        private const float LyricBoxW = 1120f;
        private const float LyricBoxH = 220f;
        private const float LyricFuriganaFs = 22f;
        private const float LyricFs = 44f;
        private const float LyricRomajiFs = 20f;
        private static readonly Color LyricBoxColor = new Color(0f, 0f, 0f, 0x99 / 255f);

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
        private RectTransform progressFill;
        private RectTransform progressTrack;
        private RectTransform gaugeFill;
        private RectTransform gaugeBest;
        private RectTransform comboFill;
        private GameObject bonusRow;
        private TextMeshProUGUI bonusValueText;
        private GameObject comboStatusBlock;
        private GameObject comboStatusRow;
        private GameObject fullComboChip;
        private TextMeshProUGUI nextBonusText;
        private TextMeshProUGUI nextBonusReward;
        private GameObject keyGuideRow;
        private readonly List<TextMeshProUGUI> keyCaps = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> verdictTexts = new List<TextMeshProUGUI>();

        // --- 毎フレームの状態 ---
        private int lastJudged = -1;
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
            gradeText.text = GameHudLogic.RankText(score);
            scoreText.text = GameHudLogic.FormatScore(score);

            // スコアゲージ (塗りは + セグメントの手前まで, Best は保存スコア)
            gaugeFill.sizeDelta = new Vector2(
                V6HudLogic.GaugeFillWidth(score, m.GaugeFillMax), m.GaugeFillH);
            int hi = GManager.songID >= 0 && GManager.songID < GManager.Hiscore.Length
                ? GManager.Hiscore[GManager.songID]
                : 0;
            gaugeBest.anchoredPosition = new Vector2(
                V6HudLogic.GaugeBestX(hi, m.GaugeFillMax), 0f);

            // コンボ
            comboText.text = GManager.combo.ToString();

            int judged = GManager.perfect + GManager.great + GManager.bad + GManager.miss;
            bool showStatus = V6HudLogic.ShowComboStatus(GManager.miss, judged);
            // 契約 §12: Combo Bonus と Combo Status は同座標・排他。ステータスが出ている間は
            // ボーナスを隠す (縦積みだと契約位置からずれ、両方同時に出てしまう)。
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

            bonusRow.SetActive(V6HudLogic.ShowComboBonus(GManager.combo, showStatus));
            bonusValueText.text = "+" + GameHudLogic.FormatScore((int)GameHudLogic.BonusTotal(GManager.combo));
            nextBonusText.text = "次のボーナス " + milestone;
            nextBonusReward.text = "+" + GameHudLogic.FormatScore(GameHudLogic.NextBonusReward(milestone));

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
            Refresh();
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
            m = V6HudMetrics.Current;
            V5Ui.Fill(GetRect(gameObject), 0f);

            BuildDim();
            BuildTopShade();
            BuildProgressBar();
            BuildLyric();
            BuildGrade();
            BuildScoreGauge();
            BuildScoreTexts();
            BuildMenuButton();
            BuildComboBlock();
            BuildRightColumn();
            BuildVerdictRow();
            BuildKeyGuide();
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
            img.color = new Color(0f, 0f, 0f, 0xA6 / 255f);
            img.raycastTarget = false;
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
            // design HcP8N (Component/歌詞表示枠): 1120×220 / #00000099 / r8 /
            // 縦 layout gap 6 / padding [16, 24] / 中寄せ。
            // Furigana fs22 / Lyrics fs44 / Romaji fs20。
            // 位置は画面上端寄り (design は中央だが、v6 プレイフィールドが画面中央を
            // 使うため動画の視認性を優先して上に寄せる)。
            var box = V5Ui.Panel(gameObject, "Lyric Box",
                new Vector2(LyricBoxW, LyricBoxH) * Scale, LyricBoxColor, 8f);
            box.GetComponent<Image>().raycastTarget = false;
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
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            lyricFuriganaText = V5Ui.Text(box, "Furigana", "", LyricFuriganaFs, V5Tokens.TextDim,
                TextAlignmentOptions.Center);
            lyricText = V5Ui.Text(box, "Lyrics", "", LyricFs, V5Tokens.Text, TextAlignmentOptions.Center);
            lyricRomajiText = V5Ui.Text(box, "Romaji", "", LyricRomajiFs, V5Tokens.TextDim,
                TextAlignmentOptions.Center);
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
            PlaceTopLeft(scoreText.rectTransform, m.GaugeX, -m.ScoreValueY);
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

            var layout = btn.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f * Scale;
            layout.padding = new RectOffset(Mathf.RoundToInt(16f * Scale), Mathf.RoundToInt(16f * Scale), 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var label = V5Ui.Text(btn, "Label", "メニュー", m.MenuFs, V5Tokens.Text,
                TextAlignmentOptions.Right);
            var labelLe = label.gameObject.AddComponent<LayoutElement>();
            // flexibleWidth 1 だとテキストが右端へ飛んで見た目が崩れる (Issue #233 実機)。
            // 自然幅でラベル → KeyCap の順に詰める。
            labelLe.minWidth = 0f;

            // design: KeyCap bg #00000040 r4, "Esc" fs muted
            var cap = V5Ui.Panel(btn, "KeyCap", new Vector2(52f * Scale, m.MenuH - 16f * Scale),
                new Color(0f, 0f, 0f, 0x40 / 255f), 4f);
            var capLe = cap.AddComponent<LayoutElement>();
            capLe.minWidth = 52f * Scale;
            capLe.preferredHeight = m.MenuH - 16f * Scale;
            V5Ui.Text(cap, "Key", "Esc", m.MenuKeyFs, V5Tokens.TextMuted, TextAlignmentOptions.Center);

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
            var block = V5Ui.Node(gameObject, "Combo Block");
            var rt = GetRect(block);
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(m.Margin, m.ComboH * 0.5f);
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
            float columnH = m.StatusBlockH;
            var column = V5Ui.Node(gameObject, "Right Column");
            var rt = GetRect(column);
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-m.Margin, m.RightColTopOffset);
            rt.sizeDelta = new Vector2(m.RightColW, columnH);

            // --- コンボボーナス (上) ---
            bonusRow = V5Ui.Node(column, "Combo Bonus");
            var bonusRt = GetRect(bonusRow);
            bonusRt.anchorMin = new Vector2(1f, 1f);
            bonusRt.anchorMax = new Vector2(1f, 1f);
            bonusRt.pivot = new Vector2(1f, 1f);
            bonusRt.anchoredPosition = Vector2.zero;
            bonusRt.sizeDelta = new Vector2(m.RightColW, m.BonusBlockH);

            bonusValueText = BuildBonusRow(bonusRow, 0f, "+ COMBO BONUS", "+0");
            nextBonusText = BuildBonusRow(bonusRow, m.BonusRowH, "次のボーナス 300", "+0");

            // --- コンボステータス (同座標・排他) ---
            // 契約 §12: Combo Bonus と Combo Status は同じ座標 (FHD 1512,460) にあり排他。
            // 縦積みにすると契約位置からずれ、両方同時に出てしまう。
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

        /// <summary>コンボボーナスの 1 行 (ラベル左寄せ muted / 値右寄せ)。値テキストを返す。</summary>
        private TextMeshProUGUI BuildBonusRow(GameObject parent, float y, string label, string value)
        {
            var row = V5Ui.Node(parent, "Row");
            var rowRt = GetRect(row);
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(0f, 1f);
            rowRt.pivot = new Vector2(0f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -y);
            rowRt.sizeDelta = new Vector2(m.RightColW, m.BonusRowH);

            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            var labelText = V5Ui.Text(row, "Label", label, m.BonusFs, V5Tokens.TextMuted);
            // 折り返すと 1 行目のラベルが 2 行目に重なって崩れる (実機スクリーンショットで確認)。
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            var labelLe = labelText.gameObject.AddComponent<LayoutElement>();
            labelLe.flexibleWidth = 1f;
            labelLe.preferredWidth = m.RightColW - 120f;
            labelLe.preferredHeight = m.BonusRowH;

            var valueText = V5Ui.Text(row, "Value", value, m.BonusFs, V5Tokens.WarnLight,
                TextAlignmentOptions.Right);
            var valueLe = valueText.gameObject.AddComponent<LayoutElement>();
            valueLe.preferredHeight = m.BonusRowH;
            return valueText;
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
        /// 契約 §8 整合)。
        ///
        /// - v6 プレイフィールド有効時: 契約 §8 の design 実測格子
        ///   (KeyGuide x = FHD 310.. / 2K 529.. / Mac 464..) をテンプレート座標へ写す
        ///   (<see cref="V6HudMetrics.KeyGuideTemplateX"/>)。build 時の固定値は
        ///   FHD 格子を等比拡大しただけなので 2K / Mac で契約位置からずれる。
        /// - レガシー 3D プレイフィールド時: レーン判定位置 (y=0.76, z=0.15) を
        ///   Camera.Main で投影してテンプレート x へ写す (契約 §8 ⚠ の
        ///   「レーン中心への追従」推奨案。design px の固定値はカメラ投影とズレる)。
        ///   投影できない (カメラ無し等) ときは build 時の design px のまま。
        ///
        /// 判定行も同じ格子に載せる (契約 §8: 判定行サンプルは KeyGuide 格子と同位置)。
        ///
        /// 注意: 自分 (hud root) の localScale は常に 1 で、等比倍率は親フレーム
        /// (V6GameRuntime.hudFrame) に掛る。かつて自分の localScale (=1) で割っていたため
        /// 2K / Mac で 1.333 倍ズレていた → lossyScale (親フレーム倍率を含む) を使う。
        /// またローカル x の原点はフレーム左端 (アンカー x=0) なので中央原点には寄せない。
        /// 変換式は純ロジック (<see cref="V6HudLogic.ScreenXToTemplateX"/> /
        /// <see cref="V6HudMetrics.KeyGuideTemplateX"/>) として EditMode テスト可能。
        /// </summary>
        private void UpdateLaneGrids()
        {
            var frameRt = transform.parent as RectTransform;
            if (frameRt == null) return;
            float frameScale = frameRt.lossyScale.x;
            if (frameScale <= 0f) return;
            Vector2 canvasSize = frameRt.rect.size * frameScale;

            if (FlashBeat.UI.V6.V6UiMode.Playfield)
            {
                for (int lane = 0; lane < V6LaneGeometry.LaneCount; lane++)
                {
                    float x = V6HudMetrics.KeyGuideTemplateX(canvasSize.x, canvasSize.y, lane);
                    SetLaneGridX(lane, x);
                }
                return;
            }

            var cam = Camera.main;
            if (cam == null) return;
            for (int lane = 0; lane < V6LaneGeometry.LaneCount; lane++)
            {
                Vector3 world = new Vector3(lane - 3.5f, 0.76f, 0.15f);
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0f) continue;
                SetLaneGridX(lane, V6HudLogic.ScreenXToTemplateX(screen.x, frameScale));
            }
        }

        /// <summary>レーン格子の第 lane 列へキーキャップと判定テキストを載せる。</summary>
        private void SetLaneGridX(int lane, float x)
        {
            if (lane < verdictTexts.Count)
            {
                var rt = verdictTexts[lane].rectTransform;
                rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
            }
            if (lane < keyCaps.Count)
            {
                var rt = keyCaps[lane].rectTransform;
                rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
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

        /// <summary>歌詞: 再生時刻と一致した行だけを出す (固定文字列は常時出さない)。</summary>
        private void UpdateLyrics()
        {
            // Space 待ちの間は歌詞位置に「Space to Start」を出す (design の Ready 案内)。
            // 別シーン (v5 ReadyView) はいらない — Issue #233。
            if (!GManager.played)
            {
                if (lyricIndex != -1 || lyricText.text.Length == 0)
                {
                    lyricIndex = -1;
                    lyricText.text = "Space to Start";
                    lyricText.fontSize = m.LyricFs * 1.79f; // design: Ready prompt
                    lyricFuriganaText.text = "";
                    lyricRomajiText.text = "";
                }
                return;
            }

            EnsureLyricLines();
            if (lyricLines == null || lyricLines.Count == 0 || !PlaySession.LyricsEnabled)
            {
                if (lyricIndex != -1)
                {
                    lyricIndex = -1;
                    lyricText.text = "";
                }

                return;
            }

            int index = LyricsTimeline.IndexAt(lyricLines, GManager.step_time);
            if (index == lyricIndex)
            {
                return;
            }

            lyricIndex = index;
            lyricText.text = index >= 0 && index < lyricLines.Count && lyricLines[index].HasText
                ? lyricLines[index].Text
                : "";
            lyricFuriganaText.text = index >= 0 && index < lyricLines.Count
                ? lyricLines[index].Furigana ?? ""
                : "";
            lyricRomajiText.text = index >= 0 && index < lyricLines.Count
                ? lyricLines[index].Romaji ?? ""
                : "";
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
                lyricData.Furigana ?? new string[0]);
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
