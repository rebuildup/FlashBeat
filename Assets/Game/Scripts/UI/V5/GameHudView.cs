using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// GameScene の v5 HUD (design exports: 03-game-default / 03b-game-over-1m)。
    ///
    /// - 右上: ランク (96px) + SCORE + スコアレール (Prefab/ScoreRail 相当) + COMBO BONUS
    /// - 左下: COMBO (FsCombo) + NO MISS / FULL COMBO + 次のボーナス
    /// - 下中央: 判定ポップアップ (PERFECT 等)
    /// - 右下: ポーズボタン
    /// - 上部: Top Shade (歌詞はレガシー Canvas (order 0) の方が上に来る)
    ///
    /// 状態はすべて GManager の静的フィールドから毎フレーム観測する。
    /// レーン/ノーツの 3D 描画 (NotesManager/Notes) には触れない。
    /// </summary>
    public sealed class GameHudView : MonoBehaviour
    {
        private const float RailWidth = 600f;
        private const float RailHeight = 14f;
        private const float FillSpan = 530f;   // C..SS セグメントの合計幅 (rail 600 のうち)
        private const float PlusSpan = 70f;    // + ゾーンの幅
        private const float ComboTrackWidth = 280f;
        private const float JudgeShowSeconds = 0.8f;

        private TextMeshProUGUI rankText;
        private TextMeshProUGUI scoreText;
        private RectTransform railFill;
        private RectTransform railPlusFill;
        private RectTransform railBest;
        private HorizontalLayoutGroup bonusRow;
        private TextMeshProUGUI bonusText;
        private TextMeshProUGUI comboText;
        private GameObject noMissRow;
        private GameObject fullComboTag;
        private RectTransform comboFill;
        private TextMeshProUGUI nextBonusText;
        private TextMeshProUGUI nextBonusReward;
        private TextMeshProUGUI judgeText;
        private CanvasGroup judgeGroup;

        private int lastJudged = -1;
        private int lastPerfect;
        private int lastGreat;
        private int lastBad;
        private int lastMiss;
        private float judgeTimer;
        private float popupScanUntil;
        private static readonly string[] JudgeCloneNames = { "Perfect(Clone)", "Great(Clone)", "Bad(Clone)", "Miss(Clone)" };

        /// <summary>fhd 基準の実ピクセル値をプロファイルへ合わせる倍率。</summary>
        public static float S
        {
            get { return UISizeProfile.Current.Margin / 48f; }
        }

        public Canvas Canvas { get; private set; }

        public Button PauseButton { get; private set; }

        public void Build()
        {
            Canvas = V5GameUi.NewCanvas("V5GameHudCanvas", -1);
            var p = UISizeProfile.Current;

            // Top Shade (歌詞より下に置くため canvas order -1)
            var shade = V5Ui.Node(Canvas.gameObject, "Top Shade");
            var shadeImg = shade.AddComponent<Image>();
            shadeImg.color = new Color(0f, 0f, 0f, 0.5f);
            V5Ui.PinTop(shade.GetComponent<RectTransform>(), 260f * S, 0f);

            BuildScoreBlock(p);
            BuildComboBlock(p);
            BuildJudgePopup(p);
            BuildPauseButton(p);
        }

        // --- 右上: Score Block ---

        private void BuildScoreBlock(UISizeProfile p)
        {
            float s = S;
            var block = V5Ui.Node(Canvas.gameObject, "Score Block");
            var rt = block.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-p.Margin, -p.Margin + 4f * s);
            rt.sizeDelta = new Vector2(RailWidth * s, 0f);

            var vLayout = block.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 6f * s;
            vLayout.childAlignment = TextAnchor.UpperRight;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = false;
            vLayout.childForceExpandHeight = false;

            // Row: ランク + (伸びる余白) + SCORE/スコア
            var row = V5GameUi.Row(block, "Row", 24f * s, TextAnchor.MiddleLeft);
            row.childControlWidth = true;

            var rank = V5GameUi.Text(row.gameObject, "Rank", "C", p.FsL * 2.5f, V5Tokens.Text, TextAlignmentOptions.Left);
            rank.textWrappingMode = TextWrappingModes.NoWrap;
            var rankLe = rank.gameObject.AddComponent<LayoutElement>();
            rankLe.minWidth = p.FsL * 2.5f;
            rankText = rank;

            var scoreCol = V5GameUi.Column(row.gameObject, "ScoreCol", 6f * s, TextAnchor.UpperRight);
            scoreCol.childControlWidth = true;
            var scoreLe = scoreCol.gameObject.AddComponent<LayoutElement>();
            scoreLe.flexibleWidth = 1f;

            var label = V5GameUi.Text(scoreCol.gameObject, "SCORE", "SCORE", p.FsS, V5Tokens.TextMuted, TextAlignmentOptions.Right);
            var labelLe = label.gameObject.AddComponent<LayoutElement>();
            labelLe.flexibleWidth = 1f;
            scoreText = V5GameUi.Text(scoreCol.gameObject, "Value", "0", p.FsL, V5Tokens.Text, TextAlignmentOptions.Right);
            var valueLe = scoreText.gameObject.AddComponent<LayoutElement>();
            valueLe.flexibleWidth = 1f;

            // スコアレール (Prefab/ScoreRail 相当: 600x14, fill + plus + best)
            var rail = BuildRail(block);
            var railLe = rail.gameObject.AddComponent<LayoutElement>();
            railLe.preferredHeight = RailHeight * s;

            // Bonus 行
            bonusRow = V5GameUi.Row(block, "Bonus", 12f * s, TextAnchor.MiddleRight);
            var bonusLe = bonusRow.gameObject.AddComponent<LayoutElement>();
            bonusLe.preferredHeight = p.FsS + 8f * s;
            var plusIcon = V5GameUi.Text(bonusRow.gameObject, "Icon", "+", p.FsS + 2f, V5Tokens.Warn, TextAlignmentOptions.Center);
            var iconLe = plusIcon.gameObject.AddComponent<LayoutElement>();
            iconLe.minWidth = p.FsS + 8f;
            bonusText = V5GameUi.Text(bonusRow.gameObject, "Text", "+ COMBO BONUS +0", p.FsS, V5Tokens.WarnLight, TextAlignmentOptions.Right);
        }

        private GameObject BuildRail(GameObject parent)
        {
            float s = S;
            var track = V5Ui.Panel(parent, "Rail", new Vector2(RailWidth * s, RailHeight * s), V5Tokens.Well, V5Tokens.RadiusSmall);
            var trackRt = track.GetComponent<RectTransform>();

            railFill = CreateRailBar(trackRt, "Fill", V5Tokens.Text, RailHeight * s);
            railPlusFill = CreateRailBar(trackRt, "Plus Fill", V5Tokens.Warn, RailHeight * s);
            railPlusFill.anchoredPosition = new Vector2(FillSpan * s, 0f);

            railBest = CreateRailBar(trackRt, "Best", V5Tokens.Warn, (RailHeight + 8f) * s);
            railBest.sizeDelta = new Vector2(4f * s, (RailHeight + 8f) * s);
            return track;
        }

        private RectTransform CreateRailBar(RectTransform parent, string name, Color color, float height)
        {
            var bar = V5Ui.Panel(parent.gameObject, name, new Vector2(height, height), color, V5Tokens.RadiusSmall);
            var rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, height);
            return rt;
        }

        // --- 左下: Combo Block ---

        private void BuildComboBlock(UISizeProfile p)
        {
            float s = S;
            var block = V5Ui.Node(Canvas.gameObject, "Combo Block");
            var rt = block.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(p.Margin, 520f * s);
            rt.sizeDelta = new Vector2(360f * s, 400f * s);

            var vLayout = block.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 0f;
            vLayout.childAlignment = TextAnchor.UpperLeft;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = false;
            vLayout.childForceExpandHeight = false;

            var label = V5GameUi.Text(block, "COMBO", "COMBO", p.FsS, V5Tokens.TextMuted);
            var labelLe = label.gameObject.AddComponent<LayoutElement>();
            labelLe.preferredHeight = p.FsS + 6f * s;

            comboText = V5GameUi.Text(block, "Value", "0", p.FsCombo, V5Tokens.Text, TextAlignmentOptions.Left);
            comboText.textWrappingMode = TextWrappingModes.NoWrap;
            comboText.lineSpacing = 75f; // design: lineHeight 1 (詰めめ)
            var comboLe = comboText.gameObject.AddComponent<LayoutElement>();
            comboLe.preferredHeight = p.FsCombo * 1.05f;

            var noMiss = V5GameUi.Column(block, "No Miss", 8f * s, TextAnchor.UpperLeft);
            noMissRow = noMiss.gameObject;
            noMiss.padding = new RectOffset(0, 0, Mathf.RoundToInt(16f * s), 0);
            var noMissLe = noMissRow.AddComponent<LayoutElement>();
            noMissLe.preferredWidth = ComboTrackWidth * s;

            var nmRow = V5GameUi.Row(noMiss.gameObject, "Row", 12f * s, TextAnchor.MiddleLeft);
            var nmText = V5GameUi.Text(nmRow.gameObject, "NO MISS", "NO MISS", p.FsS, V5Tokens.AccentLight, TextAlignmentOptions.Left);
            var nmLe = nmText.gameObject.AddComponent<LayoutElement>();
            nmLe.minWidth = p.FsS * 4.2f;

            fullComboTag = V5Ui.Panel(nmRow.gameObject, "FC", new Vector2(p.FsS * 5.6f, 28f * s), V5Tokens.WarnTint, V5Tokens.RadiusSmall);
            var fcLayout = fullComboTag.AddComponent<LayoutElement>();
            fcLayout.minWidth = p.FsS * 5.6f;
            fcLayout.preferredHeight = 28f * s;
            var fcText = V5GameUi.Text(fullComboTag, "Text", "FULL COMBO", p.FsS, V5Tokens.WarnLight, TextAlignmentOptions.Center);
            var fcTextRt = fcText.rectTransform;
            fcTextRt.anchorMin = Vector2.zero;
            fcTextRt.anchorMax = Vector2.one;
            fcTextRt.offsetMin = new Vector2(10f * s, 0f);
            fcTextRt.offsetMax = new Vector2(-10f * s, 0f);

            var track = V5Ui.Panel(noMiss.gameObject, "Track", new Vector2(ComboTrackWidth * s, 8f * s), V5Tokens.Well, V5Tokens.RadiusSmall);
            var trackLe = track.AddComponent<LayoutElement>();
            trackLe.preferredHeight = 8f * s;
            comboFill = CreateRailBar(track.GetComponent<RectTransform>(), "Fill", V5Tokens.Text, 8f * s);

            var next = V5GameUi.Row(noMiss.gameObject, "Next", 8f * s, TextAnchor.MiddleLeft);
            nextBonusText = V5GameUi.Text(next.gameObject, "Label", "次のボーナス 300", p.FsS, V5Tokens.TextMuted);
            nextBonusReward = V5GameUi.Text(next.gameObject, "Reward", "+3,000", p.FsS, V5Tokens.WarnLight, TextAlignmentOptions.Right);
            var rewardLe = nextBonusReward.gameObject.AddComponent<LayoutElement>();
            rewardLe.flexibleWidth = 1f;
        }

        // --- 下中央: 判定ポップアップ ---

        private void BuildJudgePopup(UISizeProfile p)
        {
            float s = S;
            var popup = V5Ui.Node(Canvas.gameObject, "Judge Popup");
            var rt = popup.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 222f * s);
            rt.sizeDelta = new Vector2(400f * s, 60f * s);

            judgeGroup = popup.AddComponent<CanvasGroup>();
            judgeGroup.alpha = 0f;
            judgeGroup.blocksRaycasts = false;

            judgeText = V5GameUi.Text(popup, "Text", "PERFECT", p.FsL, V5Tokens.AccentLight, TextAlignmentOptions.Center);
            var textRt = judgeText.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
        }

        // --- 右下: ポーズボタン ---

        private void BuildPauseButton(UISizeProfile p)
        {
            float s = S;
            var btn = V5Ui.Panel(Canvas.gameObject, "Pause Button", new Vector2(56f * s, 56f * s), V5Tokens.Key4, V5Tokens.RadiusMedium);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-p.Margin, p.Margin);
            var button = btn.AddComponent<Button>();
            button.targetGraphic = btn.GetComponent<Image>();
            PauseButton = button;

            // 一時停止アイコン (lucide "pause" 相当: 細い縦棒 2 本)
            var bar1 = V5Ui.Panel(btn, "Bar1", new Vector2(5f * s, 20f * s), V5Tokens.Text, 2f);
            var bar1Rt = bar1.GetComponent<RectTransform>();
            bar1Rt.anchorMin = new Vector2(0.5f, 0.5f);
            bar1Rt.anchorMax = new Vector2(0.5f, 0.5f);
            bar1Rt.anchoredPosition = new Vector2(-5f * s, 0f);
            var bar2 = V5Ui.Panel(btn, "Bar2", new Vector2(5f * s, 20f * s), V5Tokens.Text, 2f);
            var bar2Rt = bar2.GetComponent<RectTransform>();
            bar2Rt.anchorMin = new Vector2(0.5f, 0.5f);
            bar2Rt.anchorMax = new Vector2(0.5f, 0.5f);
            bar2Rt.anchoredPosition = new Vector2(5f * s, 0f);

            BuildKeyGuide(p);
        }

        /// <summary>
        /// 下部のコンパクトキーガイド (design 03: 8 レーンの代表キー)。
        /// レーンのジオメトリは触らず、判定線より下の帯に置く。
        /// 表示モードは Select の Ready 画面が選んだ <see cref="PlaySession.KeyGuide"/> に従う
        /// (Issue #167: Off なら出さない / Full ならレーンの全キーを出す)。
        /// 全キー一覧は Tab の KeyGuideUI (既存) が担う。
        /// </summary>
        private void BuildKeyGuide(UISizeProfile p)
        {
            if (PlaySession.KeyGuide == KeyGuideMode.Off) { return; }

            float s = S;
            var row = V5GameUi.Row(Canvas.gameObject, "Key Guide", 41.4f * s, TextAnchor.MiddleCenter);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 0f);
            rowRt.anchorMax = new Vector2(1f, 0f);
            rowRt.pivot = new Vector2(0.5f, 0f);
            rowRt.anchoredPosition = new Vector2(0f, 13.4f * s);
            rowRt.sizeDelta = new Vector2(0f, 56f * s);
            rowRt.offsetMin = new Vector2(142.9f * s, 13.4f * s);
            rowRt.offsetMax = new Vector2(-142.9f * s, -13.4f * s);
            row.childControlWidth = true;
            row.childForceExpandWidth = true;

            BuildKeyGuideCaps(row.gameObject, p);
        }

        /// <summary>レーンごとのキーキャップを並べる。</summary>
        private void BuildKeyGuideCaps(GameObject row, UISizeProfile p)
        {
            float s = S;
            for (int lane = 0; lane < KeyBindings.LaneCount; lane++)
            {
                var cap = V5Ui.KeyCap(row, GuideKeyText(lane), new Vector2(168f * s, 56f * s));
                var capLe = cap.AddComponent<LayoutElement>();
                capLe.minWidth = 168f * s;
                capLe.preferredHeight = 56f * s;
                var img = cap.GetComponent<Image>();
                if (img != null) img.color = LaneColor(lane);
                var label = cap.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.color = V5Tokens.Text;
                    label.fontSize = p.FsM;
                }
            }
        }

        /// <summary>
        /// キーガイドを作り直す (Issue #167: Pause の Display タブでモードを変えた時に呼ぶ)。
        /// </summary>
        public void RebuildKeyGuide()
        {
            if (Canvas == null) { return; }

            var existing = Canvas.transform.Find("Key Guide");
            if (existing == null)
            {
                BuildKeyGuide(UISizeProfile.Current);
                return;
            }

            // 帯は残して中身だけ差し替える (帯を作り直すと 1 フレーム二重に出る)。
            for (int i = existing.childCount - 1; i >= 0; i--)
            {
                var child = existing.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            if (PlaySession.KeyGuide == KeyGuideMode.Off)
            {
                existing.gameObject.SetActive(false);
                return;
            }

            existing.gameObject.SetActive(true);
            BuildKeyGuideCaps(existing.gameObject, UISizeProfile.Current);
        }

        /// <summary>表示モードに応じたレーンのキー文字列 (Compact = 代表キー / Full = 全キー)。</summary>
        private static string GuideKeyText(int lane)
        {
            if (PlaySession.KeyGuide == KeyGuideMode.Full)
            {
                var keys = KeyBindings.GetKeys(lane);
                if (keys == null || keys.Length == 0) { return string.Empty; }
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < keys.Length; i++)
                {
                    if (i > 0) { sb.Append(' '); }
                    sb.Append(KeyGuideUI.GetKeyDisplayName(keys[i]));
                }

                return sb.ToString();
            }

            return RepresentativeKey(lane);
        }

        /// <summary>レーンの代表キー (A S D F J K L ;)。文字キーを優先し、無ければセミコロン、それも無ければ先頭。</summary>
        private static string RepresentativeKey(int lane)
        {
            KeyCode[] keys = KeyBindings.GetKeys(lane);
            KeyCode fallback = keys.Length > 0 ? keys[0] : KeyCode.None;
            foreach (var key in keys)
            {
                if (key >= KeyCode.A && key <= KeyCode.Z) return key.ToString();
            }
            if (System.Array.IndexOf(keys, KeyCode.Semicolon) >= 0) return ";";
            return KeyGuideUI.GetKeyDisplayName(fallback);
        }

        /// <summary>レーン 8 本の青みがかったグラデーション (design 03c/04d 実測)。</summary>
        internal static Color LaneColor(int lane)
        {
            float t = lane / 7f;
            return new Color(0.302f, 0.302f + 0.2f * t, 0.302f + 0.42f * t);
        }

        // --- 毎フレームの状態反映 ---

        private void Update()
        {
            int score = GManager.score;
            string rank = GameHudLogic.RankText(score);
            rankText.text = rank;
            rankText.color = rank == "SS" ? V5Tokens.WarnLight : V5Tokens.Text;
            scoreText.text = GameHudLogic.FormatScore(score);
            scoreText.color = score >= 1_000_000 ? V5Tokens.WarnLight : V5Tokens.Text;

            float s = S;
            railFill.sizeDelta = new Vector2(GameHudLogic.RailFillWidth(score, 1f) * RailWidth * s, RailHeight * s);
            railPlusFill.sizeDelta = new Vector2(GameHudLogic.RailPlusWidth(score, 1f) * PlusSpan * s, RailHeight * s);
            int hiIndex = GManager.songID < GManager.Hiscore.Length ? GManager.songID : 0;
            railBest.anchoredPosition = new Vector2(GameHudLogic.RailBestX(GManager.Hiscore[hiIndex], 1f) * RailWidth * s, 0f);

            bonusRow.gameObject.SetActive(GManager.combo > 0);
            bonusText.text = "+ COMBO BONUS +" + GameHudLogic.FormatScore((int)GameHudLogic.BonusTotal(GManager.combo));

            comboText.text = GManager.combo.ToString();
            bool judged = GManager.perfect + GManager.great + GManager.bad + GManager.miss > 0;
            noMissRow.SetActive(GManager.miss == 0 && judged);
            fullComboTag.SetActive(GManager.miss == 0 && judged);
            int milestone = GameHudLogic.NextBonusMilestone(GManager.combo);
            comboFill.sizeDelta = new Vector2(
                Mathf.Min(1f, GManager.combo / (float)milestone) * ComboTrackWidth * s, 8f * s);
            nextBonusText.text = "次のボーナス " + milestone;
            nextBonusReward.text = "+" + GameHudLogic.FormatScore(GameHudLogic.NextBonusReward(milestone));

            UpdateJudgePopup();
            SuppressJudgeClones();
        }

        private void UpdateJudgePopup()
        {
            int total = GManager.perfect + GManager.great + GManager.bad + GManager.miss;
            if (total != lastJudged)
            {
                if (GManager.perfect != lastPerfect) judgeText.text = "PERFECT";
                else if (GManager.great != lastGreat) judgeText.text = "GREAT";
                else if (GManager.bad != lastBad) judgeText.text = "BAD";
                else if (GManager.miss != lastMiss) judgeText.text = "MISS";
                judgeText.color = JudgeColor(judgeText.text);
                lastPerfect = GManager.perfect;
                lastGreat = GManager.great;
                lastBad = GManager.bad;
                lastMiss = GManager.miss;
                lastJudged = total;
                judgeTimer = JudgeShowSeconds;
                popupScanUntil = Time.unscaledTime + 1f;
            }

            if (judgeTimer > 0f)
            {
                judgeTimer -= Time.deltaTime;
                judgeGroup.alpha = Mathf.Clamp01(judgeTimer / JudgeShowSeconds);
            }
            else
            {
                judgeGroup.alpha = 0f;
            }
        }

        private static Color JudgeColor(string judge)
        {
            switch (judge)
            {
                case "PERFECT": return V5Tokens.AccentLight;
                case "GREAT": return V5Tokens.Text;
                case "BAD": return V5Tokens.WarnLight;
                default: return V5Tokens.DangerLight;
            }
        }

        /// <summary>
        /// Judge (Gameplay, 所有者外) がレーン上に Instantiate する判定表示
        /// (Perfect/Great/Bad/Miss.prefab) を隠し、v5 の中央ポップアップに一本化する。
        /// </summary>
        private void SuppressJudgeClones()
        {
            if (Time.unscaledTime > popupScanUntil) return;
            var roots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                string n = roots[i].name;
                for (int j = 0; j < JudgeCloneNames.Length; j++)
                {
                    if (n == JudgeCloneNames[j])
                    {
                        roots[i].SetActive(false);
                        break;
                    }
                }
            }
        }
    }
}
