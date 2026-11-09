using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// Issue #123: ResultScene の v5 UI (design/flashbeat-ui.pen v5, exports 05-result / 05b-result-register)。
    /// コードでレイアウトを構築し、既存 ResultSceneManager の振る舞い (Retry / SelectScene / サムネ取得) を
    /// コールバックで再利用する。データとして存在するもの (ローカルハイスコア) のみを表示する:
    /// accuracy / ボーナス内訳 / Hold / 全曲ランキングは省略。
    /// 寸法は <see cref="UISizeProfile.Current"/>、色は <see cref="V5Tokens"/>。
    /// デザイン実測値 (fhd 1920x1080) は S(px) = px * FsM/30 でプロファイル倍率に合わせて縮小する。
    /// </summary>
    public sealed class ResultView : MonoBehaviour
    {
        /// <summary>Result 画面に表示する値 (ResultSceneManager が GManager から組む)。</summary>
        public sealed class Snapshot
        {
            public int Score;
            public int Perfect;
            public int Great;
            public int Bad;
            public int Miss;
            public int Combo;
            public int PrevHiscore;
            public int Hiscore;
            public string SongTitle;
            public string Musician;
            public int Bpm;
            public int Level;
            public int TotalHits;
        }

        private const string PlayerNameKey = "V5PlayerName";
        private const int PlayerNameMax = 8;

        private Snapshot snap;
        private Action onRetry;
        private Action onSelect;

        private RawImage backdrop;
        private TextMeshProUGUI hiscoreLine;
        private TextMeshProUGUI hiscoreTotal;
        private TextMeshProUGUI registeredLabel;
        private TextMeshProUGUI rowName;
        private Image rowBg;

        private GameObject modal;
        private TMP_InputField nameInput;

        private readonly GameObject[] actionButtons = new GameObject[3];
        private readonly Image[] actionBgs = new Image[3];
        private readonly GameObject[] actionFocus = new GameObject[3];
        private int selectedAction;
        private bool modalOpen;

        /// <summary>既存の Assessment 判定と同一の閾値 (1:1 維持)。</summary>
        public static string RankFor(int score)
        {
            if (score == 1000000)
            {
                return "SS";
            }

            if (score >= 950000)
            {
                return "S";
            }

            if (score >= 900000)
            {
                return "A";
            }

            if (score >= 700000)
            {
                return "B";
            }

            return "C";
        }

        /// <summary>3桁カンマ区切り (デザイン表記 972,310)。</summary>
        public static string FormatScore(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>ハイスコア差分。更新時は "+50,430"、それ以外は空文字。</summary>
        public static string DeltaLabel(int prevHiscore, int score)
        {
            if (score > prevHiscore)
            {
                return "+" + FormatScore(score - prevHiscore);
            }

            return string.Empty;
        }

        /// <summary>登録名の正規化: 大文字化 + A-Z0-9 のみ + 最大8文字。</summary>
        public static string NormalizeName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var upper = raw.ToUpperInvariant();
            var sb = new StringBuilder();
            foreach (char c in upper)
            {
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(c);
                    if (sb.Length >= PlayerNameMax)
                    {
                        break;
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>保存済み登録名 (未登録は空文字)。</summary>
        public static string SavedName()
        {
            return PlayerPrefs.GetString(PlayerNameKey, string.Empty);
        }

        /// <summary>
        /// Canvas (ScreenSpaceOverlay, Constant Pixel Size x1) ごと新規に構築する。
        /// レガシー UI は呼び出し側が非アクティブ化すること。
        /// </summary>
        public static ResultView Build(Snapshot snapshot, Action retry, Action select)
        {
            // Issue #167: GraphicRaycaster 付きの Canvas にする (マウスクリックを効かせる)。
            var canvas = V5GameUi.NewScreenCanvas("V5 Result Canvas");
            var canvasGo = canvas.gameObject;

            var root = V5Ui.Node(canvasGo, "Root");
            V5Ui.Fill(root.GetComponent<RectTransform>(), 0f);
            var view = root.AddComponent<ResultView>();
            view.snap = snapshot;
            view.onRetry = retry;
            view.onSelect = select;
            view.BuildBackdrop();
            view.BuildHeader();
            view.BuildRankLetter();
            view.BuildScoreColumn();
            view.BuildJudgeStrip();
            view.BuildRankingPanel();
            view.BuildLegend();
            view.BuildActions();
            view.BuildModal();
            view.SetActionSelected(1); // 既定選択 = Name (Enter のレジェンドと一致)
            // Issue #167: 矢印キー操作は ResultView 自身が担うので Unity の自動ナビゲーションは切る。
            V5GameUi.ApplySingleInputModel(canvas);
            return view;
        }

        /// <summary>YouTube サムネ (FetchThumbnail の結果) をジャケット帯に設定する。</summary>
        public void SetBackdrop(Texture2D texture)
        {
            if (backdrop == null || texture == null)
            {
                return;
            }

            backdrop.texture = texture;
            // 中央クロップ (cover): 帯の比率に合う範囲だけ UV で切り出す。
            var rect = backdrop.rectTransform.rect;
            if (rect.height > 0f && texture.height > 0)
            {
                float bandAspect = rect.width / rect.height;
                float texAspect = (float)texture.width / texture.height;
                if (texAspect > bandAspect)
                {
                    float u = bandAspect / texAspect;
                    backdrop.uvRect = new Rect((1f - u) / 2f, 0f, u, 1f);
                }
                else
                {
                    float v = texAspect / bandAspect;
                    backdrop.uvRect = new Rect(0f, (1f - v) / 2f, 1f, v);
                }
            }
        }

        private void Update()
        {
            // オーバーレイ (ランキング登録モーダル等) の開いた・閉じたフレームは
            // 入力を捨てる (Issue #167)。モーダル中の入力遮断自体は
            // ResultRankingOverlay.SuspendScene が担う。
            if (!FlashBeat.UI.SceneInputGate.InputAllowed) { return; }

            if (modalOpen)
            {
                // メニュー操作は KeyActions 経由 (#142)。既定: Esc=閉じる。
                if (KeyActions.IsDown(KeyActions.KeyAction.Back))
                {
                    CloseModal();
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    ConfirmRegister();
                    return;
                }

                return;
            }

            // リトライ (R) と決定 (Space) はメニュー操作設定 (KeyActions) に従う (0.1.9)。
            if (KeyActions.IsDown(KeyActions.KeyAction.Retry))
            {
                onRetry();
                return;
            }

            // Esc (Back 操作) も選曲画面へ戻る (#142)
            if (KeyActions.IsDown(KeyActions.KeyAction.Back))
            {
                onSelect();
                return;
            }

            if (KeyActions.IsDown(KeyActions.KeyAction.Confirm))
            {
                onSelect();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ActivateAction(selectedAction);
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                SetActionSelected((selectedAction + actionButtons.Length - 1) % actionButtons.Length);
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                SetActionSelected((selectedAction + 1) % actionButtons.Length);
            }
        }

        // --- レイアウト ---

        private float S(float fhdPx)
        {
            return fhdPx * (UISizeProfile.Current.FsM / 30f);
        }

        private void BuildBackdrop()
        {
            var band = V5Ui.Node(gameObject, "Jacket Band");
            V5Ui.PinTop(band.GetComponent<RectTransform>(), S(300f), 0f);

            backdrop = band.AddComponent<RawImage>();
            backdrop.color = V5Tokens.Well;
            backdrop.raycastTarget = false;

            var dim = band.transform.Find("Dim");
            var dimGo = dim != null ? dim.gameObject : V5Ui.Node(band, "Dim");
            var dimImg = dimGo.GetComponent<Image>() ?? dimGo.AddComponent<Image>();
            var dimColor = V5Tokens.Bg;
            dimColor.a = 0.55f;
            dimImg.color = dimColor;
            dimImg.raycastTarget = false;
            V5Ui.Fill(dimGo.GetComponent<RectTransform>(), 0f);
        }

        private void BuildHeader()
        {
            var result = V5Ui.Text(gameObject, "RESULT", "RESULT", S(30f), V5Tokens.Text);
            V5Ui.Place(result.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(UISizeProfile.Current.Margin, -S(56f)), new Vector2(S(400f), S(40f)));

            var block = V5Ui.Node(gameObject, "Title Block");
            V5Ui.Place(block.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-UISizeProfile.Current.Margin, -S(176f)), new Vector2(S(600f), S(120f)));

            var title = V5Ui.Text(block, "Title", snap.SongTitle, UISizeProfile.Current.FsXl, V5Tokens.Text,
                TextAlignmentOptions.Right);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, 0f);
            titleRt.sizeDelta = new Vector2(0f, S(72f));
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;

            var sub = V5Ui.Text(block, "Sub", snap.Musician + "・BPM " + snap.Bpm,
                UISizeProfile.Current.FsS, V5Tokens.TextMuted, TextAlignmentOptions.Right);
            var subRt = sub.rectTransform;
            subRt.anchorMin = new Vector2(0f, 1f);
            subRt.anchorMax = new Vector2(1f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -S(76f));
            subRt.sizeDelta = new Vector2(0f, S(30f));
            sub.textWrappingMode = TextWrappingModes.NoWrap;
            sub.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void BuildRankLetter()
        {
            var letter = V5Ui.Text(gameObject, "Rank", RankFor(snap.Score), S(420f), V5Tokens.Text,
                TextAlignmentOptions.MidlineLeft);
            // 縦方向はジャケット帯の下端 (300) から判定ブロックの上端 (244) まで伸縮して中央寄せ。
            // 高さのある画面でも左側の抜けが偏らない (fhd ではデザインの位置とほぼ一致)。
            var letterRt = letter.rectTransform;
            letterRt.anchorMin = new Vector2(0f, 0f);
            letterRt.anchorMax = new Vector2(0f, 1f);
            letterRt.pivot = new Vector2(0f, 0.5f);
            letterRt.offsetMin = new Vector2(S(72f), S(244f));
            letterRt.offsetMax = new Vector2(S(72f) + S(600f), -S(300f));
            letter.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private void BuildScoreColumn()
        {
            var col = V5Ui.Node(gameObject, "Score Column");
            V5Ui.Place(col.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-S(680f), -S(330f)), new Vector2(S(600f), S(560f)));

            var label = V5Ui.Text(col, "Score Label", "SCORE", UISizeProfile.Current.FsS, V5Tokens.TextMuted,
                TextAlignmentOptions.Right);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 1f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.pivot = new Vector2(0.5f, 1f);
            labelRt.anchoredPosition = new Vector2(0f, 0f);
            labelRt.sizeDelta = new Vector2(0f, S(30f));

            var value = V5Ui.Text(col, "Score Value", FormatScore(snap.Score), S(68f), V5Tokens.Text,
                TextAlignmentOptions.Right, FontStyles.Bold);
            var valueRt = value.rectTransform;
            valueRt.anchorMin = new Vector2(0f, 1f);
            valueRt.anchorMax = new Vector2(1f, 1f);
            valueRt.pivot = new Vector2(0.5f, 1f);
            valueRt.anchoredPosition = new Vector2(0f, -S(34f));
            valueRt.sizeDelta = new Vector2(0f, S(96f));
            value.textWrappingMode = TextWrappingModes.NoWrap;

            hiscoreLine = V5Ui.Text(col, "Hiscore Line", HiscoreLineText(), UISizeProfile.Current.FsM,
                V5Tokens.TextMuted, TextAlignmentOptions.Right);
            var lineRt = hiscoreLine.rectTransform;
            lineRt.anchorMin = new Vector2(0f, 1f);
            lineRt.anchorMax = new Vector2(1f, 1f);
            lineRt.pivot = new Vector2(0.5f, 1f);
            lineRt.anchoredPosition = new Vector2(0f, -S(140f));
            lineRt.sizeDelta = new Vector2(0f, S(40f));
            hiscoreLine.richText = true;
            hiscoreLine.textWrappingMode = TextWrappingModes.NoWrap;

            string[] labels = { "BPM", "LEVEL", "NOTES" };
            string[] values =
            {
                snap.Bpm.ToString(CultureInfo.InvariantCulture),
                snap.Level.ToString(CultureInfo.InvariantCulture),
                snap.TotalHits.ToString(CultureInfo.InvariantCulture)
            };
            for (int i = 0; i < labels.Length; i++)
            {
                BuildInfoRow(col, labels[i], values[i], S(208f) + i * S(52f));
            }

            var totalLabel = V5Ui.Text(col, "Total Label", "HISCORE", UISizeProfile.Current.FsL, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            var totalLabelRt = totalLabel.rectTransform;
            totalLabelRt.anchorMin = new Vector2(0f, 1f);
            totalLabelRt.anchorMax = new Vector2(0f, 1f);
            totalLabelRt.pivot = new Vector2(0f, 1f);
            totalLabelRt.anchoredPosition = new Vector2(0f, -S(396f));
            totalLabelRt.sizeDelta = new Vector2(S(300f), S(56f));

            hiscoreTotal = V5Ui.Text(col, "Total Value", FormatScore(snap.Hiscore), S(52f), V5Tokens.Text,
                TextAlignmentOptions.Right, FontStyles.Bold);
            var totalRt = hiscoreTotal.rectTransform;
            totalRt.anchorMin = new Vector2(0f, 1f);
            totalRt.anchorMax = new Vector2(1f, 1f);
            totalRt.pivot = new Vector2(1f, 1f);
            totalRt.anchoredPosition = new Vector2(0f, -S(388f));
            totalRt.sizeDelta = new Vector2(0f, S(64f));
            hiscoreTotal.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private void BuildInfoRow(GameObject parent, string label, string value, float y)
        {
            var labelTmp = V5Ui.Text(parent, "Row " + label + " Label", label, UISizeProfile.Current.FsM,
                V5Tokens.Text);
            var labelRt = labelTmp.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 1f);
            labelRt.anchorMax = new Vector2(0f, 1f);
            labelRt.pivot = new Vector2(0f, 1f);
            labelRt.anchoredPosition = new Vector2(0f, -y);
            labelRt.sizeDelta = new Vector2(S(300f), S(40f));

            var valueTmp = V5Ui.Text(parent, "Row " + label + " Value", value, UISizeProfile.Current.FsM,
                V5Tokens.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            var valueRt = valueTmp.rectTransform;
            valueRt.anchorMin = new Vector2(0f, 1f);
            valueRt.anchorMax = new Vector2(1f, 1f);
            valueRt.pivot = new Vector2(1f, 1f);
            valueRt.anchoredPosition = new Vector2(0f, -y);
            valueRt.sizeDelta = new Vector2(0f, S(40f));
            valueTmp.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private void BuildJudgeStrip()
        {
            // 下部ブロック: 左 48 / 右 680 (デザイン 48,836 1192w、下端 960)
            var block = V5Ui.Node(gameObject, "Judge Block");
            var blockRt = block.GetComponent<RectTransform>();
            blockRt.anchorMin = new Vector2(0f, 0f);
            blockRt.anchorMax = new Vector2(1f, 0f);
            blockRt.pivot = new Vector2(0f, 0f);
            blockRt.offsetMin = new Vector2(S(48f), S(120f));
            blockRt.offsetMax = new Vector2(-S(680f), S(244f));

            string[] names = { "Perfect", "Great", "Bad", "Miss", "Combo" };
            int[] values = { snap.Perfect, snap.Great, snap.Bad, snap.Miss, snap.Combo };
            Color[] markers =
            {
                V5Tokens.Text, V5Tokens.Text, V5Tokens.Text, V5Tokens.Danger, V5Tokens.TextDim,
            };

            for (int i = 0; i < names.Length; i++)
            {
                var column = V5Ui.Node(block, "J " + names[i]);
                var colRt = column.GetComponent<RectTransform>();
                colRt.anchorMin = new Vector2((float)i / names.Length, 0f);
                colRt.anchorMax = new Vector2((float)(i + 1) / names.Length, 1f);
                colRt.pivot = new Vector2(0.5f, 1f);
                colRt.offsetMin = Vector2.zero;
                colRt.offsetMax = Vector2.zero;

                var marker = V5Ui.Panel(column, "Marker", new Vector2(S(32f), S(4f)), markers[i], 3f);
                var markerRt = marker.GetComponent<RectTransform>();
                markerRt.anchorMin = new Vector2(0.5f, 1f);
                markerRt.anchorMax = new Vector2(0.5f, 1f);
                markerRt.pivot = new Vector2(0.5f, 1f);
                markerRt.anchoredPosition = new Vector2(0f, 0f);

                var valueTmp = V5Ui.Text(column, "Value", names[i] == "Combo"
                        ? FormatScore(values[i])
                        : values[i].ToString(CultureInfo.InvariantCulture),
                    UISizeProfile.Current.FsL, V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Bold);
                var valueRt = valueTmp.rectTransform;
                valueRt.anchorMin = new Vector2(0f, 1f);
                valueRt.anchorMax = new Vector2(1f, 1f);
                valueRt.pivot = new Vector2(0.5f, 1f);
                valueRt.anchoredPosition = new Vector2(0f, -S(16f));
                valueRt.sizeDelta = new Vector2(0f, S(56f));
                valueTmp.textWrappingMode = TextWrappingModes.NoWrap;

                var labelTmp = V5Ui.Text(column, "Label", names[i], UISizeProfile.Current.FsS,
                    V5Tokens.TextMuted, TextAlignmentOptions.Center);
                var labelRt = labelTmp.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 1f);
                labelRt.anchorMax = new Vector2(1f, 1f);
                labelRt.pivot = new Vector2(0.5f, 1f);
                labelRt.anchoredPosition = new Vector2(0f, -S(76f));
                labelRt.sizeDelta = new Vector2(0f, S(28f));
            }

            // 判定比率バー (ブロック最下部, h16 r8)。ビルド時点で rect が未確定のため
            // 幅は使わずアンカー (0-1) で伸縮させる。
            var ratio = V5Ui.Panel(block, "Judge Ratio", new Vector2(0f, S(16f)), V5Tokens.Recessed3, 8f);
            var ratioRt = ratio.GetComponent<RectTransform>();
            ratioRt.anchorMin = new Vector2(0f, 0f);
            ratioRt.anchorMax = new Vector2(1f, 0f);
            ratioRt.pivot = new Vector2(0f, 0f);
            ratioRt.offsetMin = new Vector2(0f, 0f);
            ratioRt.offsetMax = new Vector2(0f, S(16f));

            int total = snap.Perfect + snap.Great + snap.Bad + snap.Miss;
            if (total > 0)
            {
                Color[] segColors = { V5Tokens.Text, V5Tokens.Ok, V5Tokens.Warn, V5Tokens.Danger };
                int[] segValues = { snap.Perfect, snap.Great, snap.Bad, snap.Miss };
                float f0 = 0f;
                for (int i = 0; i < segValues.Length; i++)
                {
                    float f1 = f0 + (float)segValues[i] / total;
                    if (segValues[i] <= 0 || f1 <= f0)
                    {
                        continue;
                    }

                    var seg = V5Ui.Panel(ratio, "Seg " + i, Vector2.zero, segColors[i], 8f);
                    var segRt = seg.GetComponent<RectTransform>();
                    segRt.anchorMin = new Vector2(f0, 0f);
                    segRt.anchorMax = new Vector2(f1, 1f);
                    segRt.offsetMin = Vector2.zero;
                    segRt.offsetMax = Vector2.zero;
                    f0 = f1;
                }
            }
        }

        private void BuildRankingPanel()
        {
            var panel = V5Ui.Panel(gameObject, "Ranking Panel",
                new Vector2(S(592f), S(560f)), V5Tokens.Overlay, 16f);
            // 縦方向は伸縮 (fhd では上 332 / 下 188 = デザインどおり 560)。
            // 高さのある画面 (3420x2148 等) ではパネルが下まで伸びてベージュの抜けを埋める。
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 0f);
            panelRt.anchorMax = new Vector2(1f, 1f);
            panelRt.pivot = new Vector2(1f, 1f);
            panelRt.offsetMax = new Vector2(-UISizeProfile.Current.Margin, -S(332f));
            panelRt.offsetMin = new Vector2(-UISizeProfile.Current.Margin - S(592f), S(188f));

            var head = V5Ui.Text(panel, "Head", "HISCORE", UISizeProfile.Current.FsL, V5Tokens.Warn,
                TextAlignmentOptions.Left, FontStyles.Bold);
            var headRt = head.rectTransform;
            headRt.anchorMin = new Vector2(0f, 1f);
            headRt.anchorMax = new Vector2(0f, 1f);
            headRt.pivot = new Vector2(0f, 1f);
            headRt.anchoredPosition = new Vector2(S(24f), -S(24f));
            headRt.sizeDelta = new Vector2(S(300f), S(48f));

            registeredLabel = V5Ui.Text(panel, "Registered", IsRegistered() ? "名前あり" : "名前未設定",
                UISizeProfile.Current.FsS, V5Tokens.TextMuted, TextAlignmentOptions.Right);
            var regRt = registeredLabel.rectTransform;
            regRt.anchorMin = new Vector2(1f, 1f);
            regRt.anchorMax = new Vector2(1f, 1f);
            regRt.pivot = new Vector2(1f, 1f);
            regRt.anchoredPosition = new Vector2(-S(24f), -S(32f));
            regRt.sizeDelta = new Vector2(S(200f), S(32f));

            var well = V5Ui.Panel(panel, "Well", new Vector2(S(544f), S(448f)), V5Tokens.Recessed2, 16f);
            // パネルに合わせて縦方向に伸縮 (fhd では 448 = デザインどおり)。
            var wellRt = well.GetComponent<RectTransform>();
            wellRt.anchorMin = new Vector2(0f, 0f);
            wellRt.anchorMax = new Vector2(1f, 1f);
            wellRt.offsetMin = new Vector2(S(24f), S(24f));
            wellRt.offsetMax = new Vector2(-S(24f), -S(88f));

            var wellHead = V5Ui.Text(well, "Well Head", "ハイスコア", UISizeProfile.Current.FsS,
                V5Tokens.TextMuted);
            var wellHeadRt = wellHead.rectTransform;
            wellHeadRt.anchorMin = new Vector2(0f, 1f);
            wellHeadRt.anchorMax = new Vector2(0f, 1f);
            wellHeadRt.pivot = new Vector2(0f, 1f);
            wellHeadRt.anchoredPosition = new Vector2(S(20f), -S(20f));
            wellHeadRt.sizeDelta = new Vector2(S(300f), S(30f));

            bool newRecord = snap.Score > snap.PrevHiscore;
            var row = V5Ui.Panel(well, "Hiscore Row", new Vector2(S(504f), S(56f)),
                newRecord ? V5Tokens.AccentTint : V5Tokens.Recessed1, 8f);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(0f, 1f);
            rowRt.pivot = new Vector2(0f, 1f);
            rowRt.anchoredPosition = new Vector2(S(20f), -S(64f));
            rowBg = row.GetComponent<Image>();

            var pos = V5Ui.Text(row, "Pos", "1", UISizeProfile.Current.FsM, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            var posRt = pos.rectTransform;
            posRt.anchorMin = new Vector2(0f, 0.5f);
            posRt.anchorMax = new Vector2(0f, 0.5f);
            posRt.pivot = new Vector2(0f, 0.5f);
            posRt.anchoredPosition = new Vector2(S(16f), 0f);
            posRt.sizeDelta = new Vector2(S(40f), S(40f));

            rowName = V5Ui.Text(row, "Name", DisplayName(), UISizeProfile.Current.FsM, V5Tokens.Text);
            var nameRt = rowName.rectTransform;
            nameRt.anchorMin = new Vector2(0f, 0.5f);
            nameRt.anchorMax = new Vector2(0f, 0.5f);
            nameRt.pivot = new Vector2(0f, 0.5f);
            nameRt.anchoredPosition = new Vector2(S(72f), 0f);
            nameRt.sizeDelta = new Vector2(S(220f), S(40f));
            rowName.textWrappingMode = TextWrappingModes.NoWrap;
            rowName.overflowMode = TextOverflowModes.Ellipsis;

            var score = V5Ui.Text(row, "Score", FormatScore(snap.Hiscore), UISizeProfile.Current.FsM,
                V5Tokens.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            var scoreRt = score.rectTransform;
            scoreRt.anchorMin = new Vector2(1f, 0.5f);
            scoreRt.anchorMax = new Vector2(1f, 0.5f);
            scoreRt.pivot = new Vector2(1f, 0.5f);
            scoreRt.anchoredPosition = new Vector2(-S(92f), 0f);
            scoreRt.sizeDelta = new Vector2(S(240f), S(40f));
            score.textWrappingMode = TextWrappingModes.NoWrap;

            var tag = V5Ui.Tag(row, RankFor(snap.Hiscore), new Vector2(S(52f), S(32f)));
            var tagRt = tag.GetComponent<RectTransform>();
            tagRt.anchorMin = new Vector2(1f, 0.5f);
            tagRt.anchorMax = new Vector2(1f, 0.5f);
            tagRt.pivot = new Vector2(1f, 0.5f);
            tagRt.anchoredPosition = new Vector2(-S(20f), 0f);
        }

        private void BuildLegend()
        {
            // レビュー指摘 (#123): 固定オフセットだとラベル幅がキーキャップに被る。
            // HorizontalLayoutGroup + preferred 幅で実間隔で並べる。
            var legend = V5Ui.Node(gameObject, "Legend");
            V5Ui.Place(legend.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(UISizeProfile.Current.Margin, UISizeProfile.Current.Margin),
                new Vector2(S(700f), S(40f)));

            var row = legend.AddComponent<HorizontalLayoutGroup>();
            row.spacing = S(28f);
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            BuildLegendItem(legend, ActionKeyHint.Primary(KeyActions.KeyAction.Retry), S(56f), "Retry");
            BuildLegendItem(legend, "Enter", S(96f), "名前");
            BuildLegendItem(legend, ActionKeyHint.Primary(KeyActions.KeyAction.Confirm), S(96f), "決定");
        }

        private void BuildLegendItem(GameObject parent, string cap, float capW, string label)
        {
            var item = V5Ui.Node(parent, "Legend Item " + label);
            var itemRow = item.AddComponent<HorizontalLayoutGroup>();
            itemRow.spacing = UISizeProfile.Current.GapS;
            itemRow.childAlignment = TextAnchor.MiddleLeft;
            itemRow.childControlWidth = true;
            itemRow.childControlHeight = true;
            itemRow.childForceExpandWidth = false;
            itemRow.childForceExpandHeight = false;

            var keyCap = V5Ui.KeyCap(item, cap, new Vector2(capW, S(36f)));
            var capElement = keyCap.AddComponent<LayoutElement>();
            capElement.minWidth = capW;
            capElement.preferredWidth = capW;
            capElement.preferredHeight = S(36f);

            // TMP が preferred 幅を報告するのでラベルはテキスト幅通りに並ぶ (上書きされない)。
            V5Ui.Text(item, "Legend " + label, label, UISizeProfile.Current.FsS, V5Tokens.TextMuted);
        }

        private void BuildActions()
        {
            var row = V5Ui.Node(gameObject, "Actions");
            V5Ui.Place(row.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-UISizeProfile.Current.Margin, UISizeProfile.Current.Margin),
                new Vector2(S(872f), S(56f)));

            float retryW = S(220f);
            float registerW = S(280f);
            float okW = S(280f);
            float gap = UISizeProfile.Current.GapM;
            float x = 0f; // 右詰め: OK を右端に置き左へ積む
            BuildActionButton(row, 2, "OK", ActionKeyHint.Primary(KeyActions.KeyAction.Confirm), okW, x, true);
            x += okW + gap;
            // Issue #167: このボタンは「オンライン登録」ではなく、この端末に名前を保存する操作。
            // オンライン登録はランキングパネル (F5) 側が行う。
            BuildActionButton(row, 1, "Name", "Enter", registerW, x, false);
            x += registerW + gap;
            BuildActionButton(row, 0, "Retry", ActionKeyHint.Primary(KeyActions.KeyAction.Retry), retryW, x, false);
        }

        private void BuildActionButton(GameObject parent, int index, string label, string cap, float w, float xFromRight, bool primary)
        {
            var go = V5Ui.Panel(parent, "Button " + label, new Vector2(w, S(56f)),
                primary ? V5Tokens.ButtonPrimaryBg : V5Tokens.ControlBg, 8f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-xFromRight, 0f);

            var labelTmp = V5Ui.Text(go, "Label", label, UISizeProfile.Current.FsM,
                primary ? V5Tokens.ButtonPrimaryLabel : V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            var labelRt = labelTmp.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 0.5f);
            labelRt.anchorMax = new Vector2(0f, 0.5f);
            labelRt.pivot = new Vector2(0f, 0.5f);
            labelRt.anchoredPosition = new Vector2(S(20f), 0f);
            labelRt.sizeDelta = new Vector2(w - S(110f), S(40f));

            var capBgColor = primary ? new Color(0.78f, 0.78f, 0.78f, 1f) : V5Tokens.Key2;
            var capLabelColor = primary ? V5Tokens.ButtonPrimaryLabel : V5Tokens.TextMuted;
            var capGo = V5Ui.Panel(go, "KeyCap " + cap, new Vector2(S(80f), S(32f)), capBgColor, 4f);
            var capRt = capGo.GetComponent<RectTransform>();
            capRt.anchorMin = new Vector2(1f, 0.5f);
            capRt.anchorMax = new Vector2(1f, 0.5f);
            capRt.pivot = new Vector2(1f, 0.5f);
            capRt.anchoredPosition = new Vector2(-S(12f), 0f);
            var capTmp = V5Ui.Text(capGo, "Key", cap, UISizeProfile.Current.FsS, capLabelColor,
                TextAlignmentOptions.Center);
            var capTextRt = capTmp.rectTransform;
            capTextRt.anchorMin = Vector2.zero;
            capTextRt.anchorMax = Vector2.one;
            capTextRt.offsetMin = Vector2.zero;
            capTextRt.offsetMax = Vector2.zero;

            var btn = go.AddComponent<Button>();
            btn.image = go.GetComponent<Image>();
            var target = index;
            btn.onClick.AddListener(() => ActivateAction(target));
            actionButtons[index] = go;
            actionBgs[index] = btn.image;

            // フォーカス表示: アクセントの下線 (選択中のみ表示)。
            var focus = V5Ui.Panel(go, "Focus", new Vector2(w - S(24f), S(4f)), V5Tokens.Accent, 2f);
            var focusRt = focus.GetComponent<RectTransform>();
            focusRt.anchorMin = new Vector2(0.5f, 0f);
            focusRt.anchorMax = new Vector2(0.5f, 0f);
            focusRt.pivot = new Vector2(0.5f, 0f);
            focusRt.anchoredPosition = new Vector2(0f, S(6f));
            focus.GetComponent<Image>().raycastTarget = false;
            focus.SetActive(false);
            actionFocus[index] = focus;
        }

        private void SetActionSelected(int index)
        {
            selectedAction = index;
            for (int i = 0; i < actionBgs.Length; i++)
            {
                if (actionBgs[i] == null)
                {
                    continue;
                }

                bool primary = i == 2;
                bool selected = i == selectedAction;
                if (primary)
                {
                    actionBgs[i].color = selected ? Color.white : V5Tokens.ButtonPrimaryBg;
                }
                else
                {
                    actionBgs[i].color = selected ? V5Tokens.Key6 : V5Tokens.ControlBg;
                }

                if (actionFocus[i] != null)
                {
                    actionFocus[i].SetActive(selected);
                }
            }
        }

        private void ActivateAction(int index)
        {
            if (index == 0)
            {
                onRetry();
            }
            else if (index == 1)
            {
                OpenModal();
            }
            else
            {
                onSelect();
            }
        }

        // --- Register モーダル (05b) ---

        private void BuildModal()
        {
            modal = V5Ui.Node(gameObject, "Register Modal");
            V5Ui.Fill(modal.GetComponent<RectTransform>(), 0f);

            var scrim = modal.AddComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.6f);
            scrim.raycastTarget = true;

            var dialog = V5Ui.Panel(modal, "Dialog", new Vector2(S(800f), S(420f)), V5Tokens.Base, 16f);
            var dialogRt = dialog.GetComponent<RectTransform>();
            dialogRt.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRt.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRt.pivot = new Vector2(0.5f, 0.5f);
            dialogRt.anchoredPosition = new Vector2(0f, S(55f));

            var head = V5Ui.Text(dialog, "Head", "この端末の名前", UISizeProfile.Current.FsL, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            var headRt = head.rectTransform;
            headRt.anchorMin = new Vector2(0f, 1f);
            headRt.anchorMax = new Vector2(0f, 1f);
            headRt.pivot = new Vector2(0f, 1f);
            headRt.anchoredPosition = new Vector2(S(32f), -S(28f));
            headRt.sizeDelta = new Vector2(S(500f), S(52f));

            var dot = V5Ui.Panel(dialog, "Local Dot", new Vector2(S(10f), S(10f)), V5Tokens.Ok, 5f);
            var dotRt = dot.GetComponent<RectTransform>();
            dotRt.anchorMin = new Vector2(1f, 1f);
            dotRt.anchorMax = new Vector2(1f, 1f);
            dotRt.pivot = new Vector2(1f, 1f);
            dotRt.anchoredPosition = new Vector2(-S(104f), -S(40f));

            var badge = V5Ui.Text(dialog, "Local Badge", "ローカル", UISizeProfile.Current.FsS,
                V5Tokens.TextMuted);
            var badgeRt = badge.rectTransform;
            badgeRt.anchorMin = new Vector2(1f, 1f);
            badgeRt.anchorMax = new Vector2(1f, 1f);
            badgeRt.pivot = new Vector2(1f, 1f);
            badgeRt.anchoredPosition = new Vector2(-S(32f), -S(34f));
            badgeRt.sizeDelta = new Vector2(S(64f), S(30f));

            var summaryScore = V5Ui.Text(dialog, "Summary Score", FormatScore(snap.Score),
                UISizeProfile.Current.FsM, V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            var sRt = summaryScore.rectTransform;
            sRt.anchorMin = new Vector2(0f, 1f);
            sRt.anchorMax = new Vector2(0f, 1f);
            sRt.pivot = new Vector2(0f, 1f);
            sRt.anchoredPosition = new Vector2(S(32f), -S(96f));
            sRt.sizeDelta = new Vector2(S(200f), S(40f));

            var summaryTag = V5Ui.Tag(dialog, RankFor(snap.Score), new Vector2(S(52f), S(32f)));
            var tagRt = summaryTag.GetComponent<RectTransform>();
            tagRt.anchorMin = new Vector2(0f, 1f);
            tagRt.anchorMax = new Vector2(0f, 1f);
            tagRt.pivot = new Vector2(0f, 1f);
            tagRt.anchoredPosition = new Vector2(S(240f), -S(100f));

            var summaryTitle = V5Ui.Text(dialog, "Summary Title", snap.SongTitle, UISizeProfile.Current.FsM,
                V5Tokens.TextMuted);
            var tRt = summaryTitle.rectTransform;
            tRt.anchorMin = new Vector2(0f, 1f);
            tRt.anchorMax = new Vector2(0f, 1f);
            tRt.pivot = new Vector2(0f, 1f);
            tRt.anchoredPosition = new Vector2(S(312f), -S(96f));
            tRt.sizeDelta = new Vector2(S(456f), S(40f));
            summaryTitle.textWrappingMode = TextWrappingModes.NoWrap;
            summaryTitle.overflowMode = TextOverflowModes.Ellipsis;

            var field = V5Ui.Panel(dialog, "Name Field", new Vector2(S(736f), S(104f)), V5Tokens.Recessed2, 8f);
            var fieldRt = field.GetComponent<RectTransform>();
            fieldRt.anchorMin = new Vector2(0f, 1f);
            fieldRt.anchorMax = new Vector2(0f, 1f);
            fieldRt.pivot = new Vector2(0f, 1f);
            fieldRt.anchoredPosition = new Vector2(S(32f), -S(148f));

            nameInput = field.AddComponent<TMP_InputField>();
            nameInput.characterLimit = PlayerNameMax;
            nameInput.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
            nameInput.targetGraphic = field.GetComponent<Image>();

            var valueTmp = V5Ui.Text(field, "Value", string.Empty, S(52f), V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            var valueRt = valueTmp.rectTransform;
            valueRt.anchorMin = new Vector2(0f, 0f);
            valueRt.anchorMax = new Vector2(1f, 1f);
            valueRt.offsetMin = new Vector2(S(24f), S(8f));
            valueRt.offsetMax = new Vector2(-S(24f), -S(8f));
            valueTmp.textWrappingMode = TextWrappingModes.NoWrap;

            var placeholder = V5Ui.Text(field, "Placeholder", "YOU", S(52f), V5Tokens.TextDim,
                TextAlignmentOptions.Left, FontStyles.Bold);
            var phRt = placeholder.rectTransform;
            phRt.anchorMin = new Vector2(0f, 0f);
            phRt.anchorMax = new Vector2(1f, 1f);
            phRt.offsetMin = new Vector2(S(24f), S(8f));
            phRt.offsetMax = new Vector2(-S(24f), -S(8f));
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;

            nameInput.textComponent = valueTmp;
            nameInput.placeholder = placeholder;
            nameInput.textViewport = fieldRt;

            var hintCap = V5Ui.KeyCap(dialog, "A-Z 0-9", new Vector2(S(120f), S(36f)));
            var hintCapRt = hintCap.GetComponent<RectTransform>();
            hintCapRt.anchorMin = new Vector2(0f, 1f);
            hintCapRt.anchorMax = new Vector2(0f, 1f);
            hintCapRt.pivot = new Vector2(0f, 1f);
            hintCapRt.anchoredPosition = new Vector2(S(32f), -S(276f));

            var hint = V5Ui.Text(dialog, "Hint", "最大8文字・この端末のハイスコアに名前として保存されます",
                UISizeProfile.Current.FsS, V5Tokens.TextMuted);
            var hintRt = hint.rectTransform;
            hintRt.anchorMin = new Vector2(0f, 1f);
            hintRt.anchorMax = new Vector2(0f, 1f);
            hintRt.pivot = new Vector2(0f, 1f);
            hintRt.anchoredPosition = new Vector2(S(168f), -S(278f));
            hintRt.sizeDelta = new Vector2(S(560f), S(32f));

            BuildModalButton(dialog, "Skip", ActionKeyHint.Primary(KeyActions.KeyAction.Back), S(220f), S(32f), false, CloseModal);
            BuildModalButton(dialog, "Save", "Enter", S(280f), S(32f + 220f + UISizeProfile.Current.GapM), true,
                ConfirmRegister);

            modal.SetActive(false);
        }

        private void BuildModalButton(GameObject dialog, string label, string cap, float w, float xFromRight, bool primary, Action onClick)
        {
            var go = V5Ui.Panel(dialog, "Button " + label, new Vector2(w, S(56f)),
                primary ? V5Tokens.ButtonPrimaryBg : V5Tokens.ControlBg, 8f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-xFromRight, -S(336f));

            var labelTmp = V5Ui.Text(go, "Label", label, UISizeProfile.Current.FsM,
                primary ? V5Tokens.ButtonPrimaryLabel : V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            var labelRt = labelTmp.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 0.5f);
            labelRt.anchorMax = new Vector2(0f, 0.5f);
            labelRt.pivot = new Vector2(0f, 0.5f);
            labelRt.anchoredPosition = new Vector2(S(20f), 0f);
            labelRt.sizeDelta = new Vector2(w - S(110f), S(40f));

            var capBgColor = primary ? new Color(0.78f, 0.78f, 0.78f, 1f) : V5Tokens.Key2;
            var capLabelColor = primary ? V5Tokens.ButtonPrimaryLabel : V5Tokens.TextMuted;
            var capGo = V5Ui.Panel(go, "KeyCap " + cap, new Vector2(S(80f), S(32f)), capBgColor, 4f);
            var capRt = capGo.GetComponent<RectTransform>();
            capRt.anchorMin = new Vector2(1f, 0.5f);
            capRt.anchorMax = new Vector2(1f, 0.5f);
            capRt.pivot = new Vector2(1f, 0.5f);
            capRt.anchoredPosition = new Vector2(-S(12f), 0f);
            var capTmp = V5Ui.Text(capGo, "Key", cap, UISizeProfile.Current.FsS, capLabelColor,
                TextAlignmentOptions.Center);
            var capTextRt = capTmp.rectTransform;
            capTextRt.anchorMin = Vector2.zero;
            capTextRt.anchorMax = Vector2.one;
            capTextRt.offsetMin = Vector2.zero;
            capTextRt.offsetMax = Vector2.zero;

            var btn = go.AddComponent<Button>();
            btn.image = go.GetComponent<Image>();
            btn.onClick.AddListener(() => onClick());
        }

        private void OpenModal()
        {
            modalOpen = true;
            nameInput.text = SavedName();
            modal.SetActive(true);
            nameInput.ActivateInputField();
        }

        private void CloseModal()
        {
            modalOpen = false;
            nameInput.DeactivateInputField();
            modal.SetActive(false);
        }

        private void ConfirmRegister()
        {
            var name = NormalizeName(nameInput.text);
            if (string.IsNullOrEmpty(name))
            {
                name = "YOU";
            }

            PlayerPrefs.SetString(PlayerNameKey, name);
            PlayerPrefs.Save();
            if (rowName != null)
            {
                rowName.text = name;
            }

            if (registeredLabel != null)
            {
                registeredLabel.text = "名前あり";
            }

            CloseModal();
        }

        private string HiscoreLineText()
        {
            var delta = DeltaLabel(snap.PrevHiscore, snap.Score);
            if (!string.IsNullOrEmpty(delta))
            {
                return "HISCORE " + FormatScore(snap.PrevHiscore) + "  <color=#" +
                       ColorUtility.ToHtmlStringRGB(V5Tokens.Ok) + ">" + delta + "</color>";
            }

            return "HISCORE " + FormatScore(snap.Hiscore);
        }

        private static bool IsRegistered()
        {
            return SavedName().Length > 0;
        }

        private static string DisplayName()
        {
            var saved = SavedName();
            return string.IsNullOrEmpty(saved) ? "YOU" : saved;
        }
    }
}
