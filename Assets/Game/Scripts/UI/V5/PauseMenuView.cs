using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// ポーズメニュー (design exports: 04-pause-settings / 04b / 04c / 04d)。
    ///
    /// Esc で開閉。開いている間はレガシー UI (MenuPanel/MenuButton) を隠し、
    /// このビューが入力と表示を担う:
    /// - ↑↓ で行を選択 (6 行: NoteSpeed / NoteTiming / MainVolume / BGMVolume / SEVolume / Flash)
    /// - ←→ で値を変更 (NoteSpeed 1..30, NoteTiming -10..10, 音量 0..100 step 5, Flash On/Off)
    /// - S / D / L / K でタブ切替 (Settings / Display / Ranking / Keys)
    /// - リタイア (SelectScene へ) はメニュー操作設定の MenuRetire、リトライ (GameScene を
    ///   読み直し) は MenuRetry に従う (0.1.9)。Esc で再開。
    ///
    /// 値は GManager の静的フィールドへ直接書く (noteSpeed / noteTiming /
    /// mainVolume / BGMVolume / effectVolume / Flash)。音量はシーン内の
    /// AudioSource にも即時反映する (各所有者は起動時にしか読まないため)。
    /// </summary>
    public sealed class PauseMenuView : MonoBehaviour
    {
        private const int RowCount = 6;

        private GameObject root;
        private CanvasGroup group;
        private RectTransform[] rowRects;
        private TextMeshProUGUI[] valueTexts;
        private GameObject[] tabButtons;
        private TextMeshProUGUI[] tabLabels;
        private TextMeshProUGUI helpTitle;
        private TextMeshProUGUI helpDesc;
        private GameObject settingsPage;
        private GameObject displayPage;
        private GameObject rankingPage;
        private GameObject keysPage;
        private GameSceneManager sceneManager;

        private int selectedRow;
        private int selectedTab;
        private bool open;
        private bool keyGuideOn;

        private enum Row
        {
            NoteSpeed = 0,
            NoteTiming = 1,
            MainVolume = 2,
            BGMVolume = 3,
            SEVolume = 4,
            Flash = 5,
        }

        private enum Tab
        {
            Settings = 0,
            Display = 1,
            Ranking = 2,
            Keys = 3,
        }

        private static readonly string[] HelpTitles =
        {
            "NoteSpeed", "NoteTiming", "MainVolume", "BGMVolume", "SEVolume", "Flash",
        };

        private static readonly string[] HelpTexts =
        {
            "ノーツが出現してから判定線に届くまでの時間。数値が大きいほど速く落ちます。",
            "判定タイミングのオフセット。音が遅く感じるときに調整します。",
            "全体の音量です。",
            "BGM (動画音声) の音量です。",
            "効果音 (キー音) の音量です。",
            "背景のフラッシュ演出の On / Off です。",
        };

        /// <summary>fhd 基準の実ピクセル値をプロファイルへ合わせる倍率。</summary>
        public static float S
        {
            get { return UISizeProfile.Current.Margin / 48f; }
        }

        public void Build(Canvas canvas)
        {
            float s = S;
            var p = UISizeProfile.Current;
            sceneManager = FindFirstObjectByType<GameSceneManager>();

            root = V5Ui.Node(canvas.gameObject, "Pause");
            V5Ui.Fill(root.GetComponent<RectTransform>(), 0f);
            var scrim = root.AddComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.92f);
            group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            BuildHeader(p, s);
            BuildTabStrip(p, s);
            BuildSettingsPage(p, s);
            BuildDisplayPage(p, s);
            BuildRankingPage(p, s);
            BuildKeysPage(p, s);
            BuildLegend(p, s);
            BuildActions(p, s);

            root.SetActive(false);
            SelectTab(0);
            SelectRow(0);
        }

        private void BuildHeader(UISizeProfile p, float s)
        {
            var pause = V5GameUi.Text(root, "PAUSE", "PAUSE", p.FsM, V5Tokens.TextMuted, TextAlignmentOptions.TopLeft);
            var prt = pause.rectTransform;
            prt.anchorMin = new Vector2(0f, 1f);
            prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(p.Margin, -p.Margin);
            prt.sizeDelta = new Vector2(200f * s, p.FsM + 8f * s);

            // 右上: 曲情報 (タイトル + アーティスト・時間 + ジャケット)
            var song = GManager.GetSong(GManager.songID);
            var songRow = V5GameUi.Row(root, "Song", 16f * s, TextAnchor.MiddleRight);
            var songRt = songRow.GetComponent<RectTransform>();
            songRt.anchorMin = new Vector2(1f, 1f);
            songRt.anchorMax = new Vector2(1f, 1f);
            songRt.pivot = new Vector2(1f, 1f);
            songRt.anchoredPosition = new Vector2(-p.Margin, -p.Margin + 8f * s);
            songRt.sizeDelta = new Vector2(600f * s, 72f * s);
            songRow.childControlWidth = true;

            var textCol = V5GameUi.Column(songRow.gameObject, "Text", 4f * s, TextAnchor.UpperRight);
            textCol.childControlWidth = true;
            var textLe = textCol.gameObject.AddComponent<LayoutElement>();
            textLe.flexibleWidth = 1f;
            var title = V5GameUi.Text(textCol.gameObject, "Title", song != null ? song.title : "", p.FsL, V5Tokens.Text, TextAlignmentOptions.Right);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
            var titleLe = title.gameObject.AddComponent<LayoutElement>();
            titleLe.flexibleWidth = 1f;
            var meta = V5GameUi.Text(textCol.gameObject, "Meta", song != null ? song.musician : "", p.FsS, V5Tokens.TextMuted, TextAlignmentOptions.Right);
            meta.textWrappingMode = TextWrappingModes.NoWrap;
            meta.overflowMode = TextOverflowModes.Ellipsis;
            var metaLe = meta.gameObject.AddComponent<LayoutElement>();
            metaLe.flexibleWidth = 1f;

            var jacket = V5Ui.Panel(songRow.gameObject, "Jacket", new Vector2(128f * s, 72f * s), V5Tokens.Key2, V5Tokens.RadiusMedium);
            var jacketLe = jacket.AddComponent<LayoutElement>();
            jacketLe.preferredWidth = 128f * s;
            jacketLe.preferredHeight = 72f * s;
        }

        private void BuildTabStrip(UISizeProfile p, float s)
        {
            var tabs = V5GameUi.Row(root, "Tabs", 4f * s, TextAnchor.MiddleCenter);
            var tabsRt = tabs.GetComponent<RectTransform>();
            tabsRt.anchorMin = new Vector2(0.5f, 1f);
            tabsRt.anchorMax = new Vector2(0.5f, 1f);
            tabsRt.pivot = new Vector2(0.5f, 1f);
            tabsRt.anchoredPosition = new Vector2(0f, -116f * s);
            tabsRt.sizeDelta = new Vector2(800f * s, 48f * s);
            tabs.childControlWidth = true;
            tabs.childForceExpandWidth = true;
            tabs.childControlHeight = false;

            tabButtons = new GameObject[4];
            tabLabels = new TextMeshProUGUI[4];
            AddTab(tabs.gameObject, 0, "Settings", p, s);
            AddTab(tabs.gameObject, 1, "Display", p, s);
            AddTab(tabs.gameObject, 2, "Ranking", p, s);
            AddTab(tabs.gameObject, 3, "Keys", p, s);
        }

        private void AddTab(GameObject parent, int index, string label, UISizeProfile p, float s)
        {
            var item = V5Ui.Panel(parent, "Tab " + label, new Vector2(0f, 48f * s), V5Tokens.Raised2, V5Tokens.RadiusMedium);
            var le = item.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.preferredHeight = 48f * s;
            var tmp = V5GameUi.Text(item, "Label", label, p.FsM, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            var rt = tmp.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            tabButtons[index] = item;
            tabLabels[index] = tmp;
            var button = item.AddComponent<Button>();
            button.targetGraphic = item.GetComponent<Image>();
            int captured = index;
            button.onClick.AddListener(() => SelectTab(captured));
        }

        // --- Settings タブ ---

        private void BuildSettingsPage(UISizeProfile p, float s)
        {
            settingsPage = V5Ui.Node(root, "Settings Page");
            var pageRt = settingsPage.GetComponent<RectTransform>();
            V5Ui.Fill(pageRt, 0f);

            // 設定リスト (左)
            var list = V5GameUi.Column(settingsPage, "List", 2f * s, TextAnchor.UpperLeft);
            var listRt = list.GetComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0f, 0.5f);
            listRt.anchorMax = new Vector2(0f, 0.5f);
            listRt.pivot = new Vector2(0f, 0.5f);
            listRt.anchoredPosition = new Vector2(p.Margin, 0f);
            listRt.sizeDelta = new Vector2(640f * s, 520f * s);
            list.padding = new RectOffset(0, 0, Mathf.RoundToInt(8f * s), Mathf.RoundToInt(8f * s));
            list.childControlWidth = true;
            list.childControlHeight = false;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;

            rowRects = new RectTransform[RowCount];
            valueTexts = new TextMeshProUGUI[RowCount];
            AddRow(list.gameObject, (int)Row.NoteSpeed, "NoteSpeed", p, s);
            AddRow(list.gameObject, (int)Row.NoteTiming, "NoteTiming", p, s);
            AddRow(list.gameObject, (int)Row.MainVolume, "MainVolume", p, s);
            AddRow(list.gameObject, (int)Row.BGMVolume, "BGMVolume", p, s);
            AddRow(list.gameObject, (int)Row.SEVolume, "SEVolume", p, s);
            AddRow(list.gameObject, (int)Row.Flash, "Flash", p, s);

            // プレビュー (右): 判定線とノーツの簡易プレビュー
            BuildPreview(settingsPage, p, s);

            // ヘルプ (右下)
            var help = V5GameUi.Column(settingsPage, "Help", 8f * s, TextAnchor.UpperLeft);
            var helpRt = help.GetComponent<RectTransform>();
            helpRt.anchorMin = new Vector2(1f, 0f);
            helpRt.anchorMax = new Vector2(1f, 0f);
            helpRt.pivot = new Vector2(1f, 0f);
            helpRt.anchoredPosition = new Vector2(-p.Margin, p.Margin + 60f * s);
            helpRt.sizeDelta = new Vector2(528f * s, 150f * s);
            helpTitle = V5GameUi.Text(help.gameObject, "Title", "NoteSpeed", p.FsL, V5Tokens.Text, TextAlignmentOptions.TopLeft);
            helpDesc = V5GameUi.Text(help.gameObject, "Desc", HelpTexts[0], p.FsM, V5Tokens.TextMuted, TextAlignmentOptions.TopLeft);
        }

        private void AddRow(GameObject parent, int index, string label, UISizeProfile p, float s)
        {
            var row = V5Ui.Panel(parent, "Row " + label, new Vector2(0f, 64f * s), V5Tokens.Recessed2, V5Tokens.RadiusMedium);
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 64f * s;
            var layout = V5GameUi.Row(row.gameObject, "Layout", 16f * s, TextAnchor.MiddleLeft);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(Mathf.RoundToInt(20f * s), Mathf.RoundToInt(12f * s), 0, 0);

            var labelText = V5GameUi.Text(layout.gameObject, "Label", label, p.FsM, V5Tokens.Text, TextAlignmentOptions.Left);
            var labelLe = labelText.gameObject.AddComponent<LayoutElement>();
            labelLe.flexibleWidth = 1f;

            var prev = MakeStepper(layout.gameObject, "Prev", "<", p, s);
            var valueText = V5GameUi.Text(layout.gameObject, "Value", "", p.FsM, V5Tokens.Text, TextAlignmentOptions.Center);
            var valueBox = valueText.gameObject.AddComponent<LayoutElement>();
            valueBox.minWidth = 104f * s;
            valueTexts[index] = valueText;
            var next = MakeStepper(layout.gameObject, "Next", ">", p, s);

            rowRects[index] = row.GetComponent<RectTransform>();
            int captured = index;
            var button = row.AddComponent<Button>();
            button.targetGraphic = row.GetComponent<Image>();
            button.onClick.AddListener(() => SelectRow(captured));
            if (prev != null) prev.onClick.AddListener(() => Adjust(captured, -1));
            if (next != null) next.onClick.AddListener(() => Adjust(captured, +1));
        }

        private Button MakeStepper(GameObject parent, string name, string glyph, UISizeProfile p, float s)
        {
            var btn = V5Ui.Panel(parent, name, new Vector2(40f * s, 40f * s), V5Tokens.Key4, V5Tokens.RadiusMedium);
            var tmp = V5GameUi.Text(btn, "Icon", glyph, p.FsM, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            var rt = tmp.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var button = btn.AddComponent<Button>();
            button.targetGraphic = btn.GetComponent<Image>();
            return button;
        }

        private void BuildPreview(GameObject parent, UISizeProfile p, float s)
        {
            var preview = V5Ui.Panel(parent, "Preview", new Vector2(528f * s, 297f * s), V5Tokens.Well, 16f * s);
            var prt = preview.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(1f, 0.5f);
            prt.anchorMax = new Vector2(1f, 0.5f);
            prt.pivot = new Vector2(1f, 0.5f);
            prt.anchoredPosition = new Vector2(-p.Margin, 0f);
            preview.AddComponent<RectMask2D>();

            // レーンフィールド (簡易): 判定線 + 区切り線 + ノーツ
            var jline = V5Ui.Panel(preview, "Judge Line", new Vector2(0f, 5f * s), V5Tokens.Text, 2f);
            var jrt = jline.GetComponent<RectTransform>();
            jrt.anchorMin = new Vector2(0f, 0.08f);
            jrt.anchorMax = new Vector2(1f, 0.08f);
            jrt.offsetMin = new Vector2(0f, -2.5f * s);
            jrt.offsetMax = new Vector2(0f, 2.5f * s);
            for (int i = 1; i < 4; i++)
            {
                float x = 0.2f * i;
                var divider = V5Ui.Panel(preview, "Divider " + i, new Vector2(2f * s, 0f), new Color(0.35f, 0.35f, 0.35f), 1f);
                var drt = divider.GetComponent<RectTransform>();
                drt.anchorMin = new Vector2(x, 0f);
                drt.anchorMax = new Vector2(x, 1f);
                drt.offsetMin = new Vector2(-1f * s, 0f);
                drt.offsetMax = new Vector2(1f * s, 0f);
            }
            for (int i = 0; i < 5; i++)
            {
                var note = V5Ui.Panel(preview, "Note " + i, new Vector2(48f * s, 20f * s), V5Tokens.Text, 4f);
                var nrt = note.GetComponent<RectTransform>();
                nrt.anchorMin = new Vector2(0.5f, 0f);
                nrt.anchorMax = new Vector2(0.5f, 0f);
                nrt.pivot = new Vector2(0.5f, 0f);
                nrt.anchoredPosition = new Vector2((i - 2) * 110f * s, (40f + i * 90f) * s);
            }
        }

        // --- Display タブ ---

        private void BuildDisplayPage(UISizeProfile p, float s)
        {
            displayPage = V5Ui.Node(root, "Display Page");
            V5Ui.Fill(displayPage.GetComponent<RectTransform>(), 0f);

            var list = V5GameUi.Column(displayPage, "List", 2f * s, TextAnchor.UpperLeft);
            var listRt = list.GetComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0f, 0.5f);
            listRt.anchorMax = new Vector2(0f, 0.5f);
            listRt.pivot = new Vector2(0f, 0.5f);
            listRt.anchoredPosition = new Vector2(p.Margin, 0f);
            listRt.sizeDelta = new Vector2(640f * s, 520f * s);
            list.padding = new RectOffset(0, 0, Mathf.RoundToInt(8f * s), Mathf.RoundToInt(8f * s));
            list.childControlWidth = true;
            list.childControlHeight = false;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;

            // 表示オプション (Key Guide / Lyrics は実動作に接続、他は表示のみ)
            AddDisplayRow(list.gameObject, "Key Guide", "Off", p, s, () =>
            {
                var guide = FindFirstObjectByType<KeyGuideUI>();
                if (guide != null)
                {
                    guide.Toggle();
                    keyGuideOn = !keyGuideOn;
                }
                return keyGuideOn ? "Compact" : "Off";
            });
            AddDisplayRow(list.gameObject, "Lyrics", GameSceneManager.Menu ? "On" : "Off", p, s, () =>
            {
                if (sceneManager != null) sceneManager.SwitchPanel();
                return GameSceneManager.Menu ? "On" : "Off";
            });
            AddDisplayRow(list.gameObject, "Live Rank", "Off", p, s, null);
            AddDisplayRow(list.gameObject, "Hit Timing", "Off", p, s, null);
            AddDisplayRow(list.gameObject, "Judge Text", "Simple", p, s, null);
            AddDisplayRow(list.gameObject, "Progress Bar", "On", p, s, null);

            // 凡例 (右)
            var legend = V5GameUi.Column(displayPage, "Legend Note", 6f * s, TextAnchor.UpperLeft);
            var legendRt = legend.GetComponent<RectTransform>();
            legendRt.anchorMin = new Vector2(1f, 0.5f);
            legendRt.anchorMax = new Vector2(1f, 0.5f);
            legendRt.pivot = new Vector2(1f, 0.5f);
            legendRt.anchoredPosition = new Vector2(-p.Margin, 120f * s);
            legendRt.sizeDelta = new Vector2(528f * s, 120f * s);
            AddLegendNote(legend.gameObject, V5Tokens.Text, "いま選んでいる項目", p, s);
            AddLegendNote(legend.gameObject, new Color(0.23f, 0.23f, 0.23f), "常に表示", p, s);
            AddLegendNote(legend.gameObject, new Color(0.16f, 0.16f, 0.16f), "オフの項目は表示されません", p, s);
        }

        /// <summary>表示タブの 1 行。toggle が null のときは値固定 (実装未接続の項目)。</summary>
        private void AddDisplayRow(GameObject parent, string label, string value, UISizeProfile p, float s,
            System.Func<string> toggle)
        {
            var row = V5Ui.Panel(parent, "Row " + label, new Vector2(0f, 64f * s), V5Tokens.Recessed2, V5Tokens.RadiusMedium);
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 64f * s;
            var layout = V5GameUi.Row(row.gameObject, "Layout", 16f * s, TextAnchor.MiddleLeft);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(Mathf.RoundToInt(20f * s), Mathf.RoundToInt(12f * s), 0, 0);

            var labelText = V5GameUi.Text(layout.gameObject, "Label", label, p.FsM, V5Tokens.Text, TextAlignmentOptions.Left);
            var labelLe = labelText.gameObject.AddComponent<LayoutElement>();
            labelLe.flexibleWidth = 1f;

            var valueText = V5GameUi.Text(layout.gameObject, "Value", value, p.FsM, V5Tokens.Text, TextAlignmentOptions.Center);
            var valueBox = valueText.gameObject.AddComponent<LayoutElement>();
            valueBox.minWidth = 104f * s;

            if (toggle != null)
            {
                var button = row.AddComponent<Button>();
                button.targetGraphic = row.GetComponent<Image>();
                button.onClick.AddListener(() => valueText.text = toggle());
            }
        }

        private void AddLegendNote(GameObject parent, Color swatch, string label, UISizeProfile p, float s)
        {
            var row = V5GameUi.Row(parent, "Item", 12f * s, TextAnchor.MiddleLeft);
            var dot = V5Ui.Panel(row.gameObject, "Swatch", new Vector2(28f * s, 16f * s), swatch, V5Tokens.RadiusSmall);
            var dotLe = dot.AddComponent<LayoutElement>();
            dotLe.minWidth = 28f * s;
            dotLe.preferredHeight = 16f * s;
            V5GameUi.Text(row.gameObject, "Label", label, p.FsS, V5Tokens.TextMuted, TextAlignmentOptions.Left);
        }

        // --- Ranking タブ ---

        private void BuildRankingPage(UISizeProfile p, float s)
        {
            rankingPage = V5Ui.Node(root, "Ranking Page");
            V5Ui.Fill(rankingPage.GetComponent<RectTransform>(), 0f);

            // 左: 曲別ランキング (データはこの曲のハイスコアのみ)
            var left = V5GameUi.Column(rankingPage, "Per Song", 8f * s, TextAnchor.UpperLeft);
            var leftRt = left.GetComponent<RectTransform>();
            leftRt.anchorMin = new Vector2(0f, 0.5f);
            leftRt.anchorMax = new Vector2(0f, 0.5f);
            leftRt.pivot = new Vector2(0f, 0.5f);
            leftRt.anchoredPosition = new Vector2(p.Margin, 0f);
            leftRt.sizeDelta = new Vector2(640f * s, 520f * s);

            var song = GManager.GetSong(GManager.songID);
            V5GameUi.Text(left.gameObject, "Header", "曲別・" + (song != null ? song.title : ""), p.FsM, V5Tokens.Text, TextAlignmentOptions.Left);
            int hiIndex = GManager.songID < GManager.Hiscore.Length ? GManager.songID : 0;
            V5Ui.RankRow(left.gameObject, "1", "YOU", GameHudLogic.FormatScore(GManager.Hiscore[hiIndex]));

            // 右: 累計 (ハイスコアが記録されている曲数のみ集計できる)
            var right = V5GameUi.Column(rankingPage, "Cumulative", 8f * s, TextAnchor.UpperLeft);
            var rightRt = right.GetComponent<RectTransform>();
            rightRt.anchorMin = new Vector2(1f, 0.5f);
            rightRt.anchorMax = new Vector2(1f, 0.5f);
            rightRt.pivot = new Vector2(1f, 0.5f);
            rightRt.anchoredPosition = new Vector2(-p.Margin, 0f);
            rightRt.sizeDelta = new Vector2(640f * s, 520f * s);

            long total = 0;
            int played = 0;
            for (int i = 1; i < GManager.Hiscore.Length; i++)
            {
                if (GManager.Hiscore[i] > 0)
                {
                    total += GManager.Hiscore[i];
                    played++;
                }
            }
            V5GameUi.Text(right.gameObject, "Header", "登録名ごと (累計)", p.FsM, V5Tokens.Text, TextAlignmentOptions.Left);
            V5Ui.RankRow(right.gameObject, "1", "YOU", GameHudLogic.FormatScore((int)total));
            V5GameUi.Text(right.gameObject, "Songs", played + "曲", p.FsS, V5Tokens.TextMuted, TextAlignmentOptions.Left);
        }

        // --- Keys タブ ---

        private void BuildKeysPage(UISizeProfile p, float s)
        {
            keysPage = V5Ui.Node(root, "Keys Page");
            V5Ui.Fill(keysPage.GetComponent<RectTransform>(), 0f);

            // 上段: レーンの見出し (Lane 1..8)
            var laneLegend = V5GameUi.Row(keysPage, "Lane Legend", 8f * s, TextAnchor.MiddleCenter);
            var laneRt = laneLegend.GetComponent<RectTransform>();
            laneRt.anchorMin = new Vector2(0f, 1f);
            laneRt.anchorMax = new Vector2(1f, 1f);
            laneRt.pivot = new Vector2(0.5f, 1f);
            laneRt.anchoredPosition = new Vector2(0f, -216f * s);
            laneRt.sizeDelta = new Vector2(0f, 56f * s);
            laneLegend.childControlWidth = true;
            laneLegend.childForceExpandWidth = true;

            // 下段: JIS キーボード (Judge.LaneKeys の正本から生成)
            var keyboard = V5GameUi.Column(keysPage, "Keyboard", 8f * s, TextAnchor.UpperLeft);
            var kbRt = keyboard.GetComponent<RectTransform>();
            kbRt.anchorMin = new Vector2(0.5f, 0.5f);
            kbRt.anchorMax = new Vector2(0.5f, 0.5f);
            kbRt.pivot = new Vector2(0.5f, 0.5f);
            kbRt.anchoredPosition = new Vector2(0f, -60f * s);

            // Lane 1..8 の見出し
            for (int lane = 0; lane < Judge.LaneKeys.Length; lane++)
            {
                var cap = V5Ui.Panel(laneLegend.gameObject, "Lane " + (lane + 1), new Vector2(0f, 56f * s), LaneColor(lane), V5Tokens.RadiusMedium);
                var capLe = cap.AddComponent<LayoutElement>();
                capLe.flexibleWidth = 1f;
                capLe.preferredHeight = 56f * s;
                var tmp = V5GameUi.Text(cap, "Label", "Lane " + (lane + 1), p.FsS, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center);
                var rt = tmp.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            // キー配列 (JIS 上段 / 中段 / 下段)。キーキャップは所属レーンの色で塗る
            // (KeyBindings が trunk に無いので Judge.LaneKeys が正本)。
            laneByKey = BuildLaneKeyMap();
            AddKeyRow(keyboard.gameObject, new[] { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '-', '^', '\\' }, p, s, 0f);
            AddKeyRow(keyboard.gameObject, new[] { 'Q', 'W', 'E', 'R', 'T', 'Y', 'U', 'I', 'O', 'P', '@', '[' }, p, s, 0.5f);
            AddKeyRow(keyboard.gameObject, new[] { 'A', 'S', 'D', 'F', 'G', 'H', 'J', 'K', 'L', ';', ':', ']' }, p, s, 1f);
            AddKeyRow(keyboard.gameObject, new[] { 'Z', 'X', 'C', 'V', 'B', 'N', 'M', ',', '.', '/', '_' }, p, s, 1.5f);
            laneByKey = null;
        }

        private Dictionary<KeyCode, int> laneByKey;

        /// <summary>Judge.LaneKeys を KeyCode→レーン番号へ展開する。</summary>
        private static Dictionary<KeyCode, int> BuildLaneKeyMap()
        {
            var map = new Dictionary<KeyCode, int>();
            for (int lane = 0; lane < Judge.LaneKeys.Length; lane++)
            {
                foreach (var key in Judge.LaneKeys[lane])
                {
                    if (!map.ContainsKey(key)) map[key] = lane;
                }
            }
            return map;
        }

        /// <summary>キーボード刻印 1 文字 → KeyCode (Judge.LaneKeys が使うものだけ)。</summary>
        private static KeyCode CharToKeyCode(char c)
        {
            if (c >= '0' && c <= '9') return KeyCode.Alpha0 + (c - '0');
            if (c >= 'A' && c <= 'Z') return (KeyCode)System.Enum.Parse(typeof(KeyCode), c.ToString());
            switch (c)
            {
                case '-': return KeyCode.Minus;
                case '^': return KeyCode.Caret;
                case '\\': return KeyCode.Backslash;
                case '@': return KeyCode.At;
                case '[': return KeyCode.LeftBracket;
                case ';': return KeyCode.Semicolon;
                case ':': return KeyCode.Colon;
                case ']': return KeyCode.RightBracket;
                case ',': return KeyCode.Comma;
                case '.': return KeyCode.Period;
                case '/': return KeyCode.Slash;
                case '_': return KeyCode.Underscore;
                default: return KeyCode.None;
            }
        }

        private static Color LaneColor(int lane)
        {
            return GameHudView.LaneColor(lane);
        }

        private void AddKeyRow(GameObject parent, char[] keys, UISizeProfile p, float s, float indent = 0f)
        {
            var row = V5GameUi.Row(parent, "Row", 8f * s, TextAnchor.MiddleCenter);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(0f, 72f * s);
            var spacer = V5Ui.Node(row.gameObject, "Indent");
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.minWidth = 72f * s * indent;
            spacer.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            for (int i = 0; i < keys.Length; i++)
            {
                Color bg = V5Tokens.Key4;
                if (laneByKey != null)
                {
                    KeyCode code = CharToKeyCode(keys[i]);
                    if (code != KeyCode.None && laneByKey.TryGetValue(code, out int lane))
                    {
                        bg = LaneColor(lane);
                    }
                }
                var cap = V5Ui.KeyCap(row.gameObject, keys[i].ToString(), new Vector2(72f * s, 72f * s));
                var img = cap.GetComponent<Image>();
                if (img != null) img.color = bg;
                var label = cap.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.color = new Color(1f, 1f, 1f, 0.9f);
                var capLe = cap.AddComponent<LayoutElement>();
                capLe.minWidth = 72f * s;
                capLe.preferredHeight = 72f * s;
            }
        }

        // --- 下部共通 ---

        private void BuildLegend(UISizeProfile p, float s)
        {
            var legend = V5GameUi.Row(root, "Legend", 24f * s, TextAnchor.MiddleLeft);
            var legendRt = legend.GetComponent<RectTransform>();
            legendRt.anchorMin = new Vector2(0f, 0f);
            legendRt.anchorMax = new Vector2(0f, 0f);
            legendRt.pivot = new Vector2(0f, 0f);
            legendRt.anchoredPosition = new Vector2(p.Margin, p.Margin);
            legendRt.sizeDelta = new Vector2(0f, 36f * s);
            V5GameUi.LegendItem(legend.gameObject, "↑ ↓", "項目");
            V5GameUi.LegendItem(legend.gameObject, "← →", "値");
            V5GameUi.LegendItem(legend.gameObject, "S D L K", "タブ");
            V5GameUi.LegendItem(legend.gameObject, "Esc", "再開");
        }

        private void BuildActions(UISizeProfile p, float s)
        {
            var actions = V5GameUi.Row(root, "Actions", 12f * s, TextAnchor.MiddleRight);
            var actionsRt = actions.GetComponent<RectTransform>();
            actionsRt.anchorMin = new Vector2(1f, 0f);
            actionsRt.anchorMax = new Vector2(1f, 0f);
            actionsRt.pivot = new Vector2(1f, 0f);
            actionsRt.anchoredPosition = new Vector2(-p.Margin, p.Margin);
            actionsRt.sizeDelta = new Vector2(0f, 48f * s);
            actions.childControlWidth = false;
            actions.childControlHeight = false;
            // リトライ/リタイアはメニュー操作設定 (KeyActions) に従う (0.1.9)。
            // キーキャップ表示も設定値から生成する (既定: R / Backspace)。
            AddAction(actions.gameObject, "Retire", KeyConfigUI.GetKeysText(KeyActions.Get(KeyActions.KeyAction.MenuRetire)), p, s, () =>
            {
                if (sceneManager != null) sceneManager.RetireGame();
                Close();
            });
            AddAction(actions.gameObject, "Retry", KeyConfigUI.GetKeysText(KeyActions.Get(KeyActions.KeyAction.MenuRetry)), p, s, () =>
            {
                if (sceneManager != null) sceneManager.RetryGame();
                Close();
            });
            AddAction(actions.gameObject, "Resume", "Esc", p, s, Close, primary: true);
        }

        private void AddAction(GameObject parent, string label, string key, UISizeProfile p, float s,
            System.Action action, bool primary = false)
        {
            var size = new Vector2(p.FsM * 7.5f, 48f * s);
            var button = primary
                ? V5Ui.ButtonPrimary(parent, label, size)
                : V5Ui.ButtonSecondary(parent, label, size);
            var le = button.AddComponent<LayoutElement>();
            le.minWidth = p.FsM * 7.5f;
            le.preferredHeight = 48f * s;
            var b = button.GetComponent<Button>();
            if (b != null && action != null) b.onClick.AddListener(() => action());
            var cap = V5Ui.KeyCap(parent, key, new Vector2(48f * s, 36f * s));
            var capLe = cap.AddComponent<LayoutElement>();
            capLe.minWidth = 48f * s;
            capLe.preferredHeight = 36f * s;
        }

        // --- 開閉と入力 ---

        public void Open()
        {
            if (open) return;
            open = true;
            RefreshValues();
            root.SetActive(true);
            group.blocksRaycasts = true;
            group.alpha = 1f;
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;
            root.SetActive(false);
        }

        public bool IsOpen
        {
            get { return open; }
        }

        /// <summary>false の間は Esc で開かない (エラー表示中など)。</summary>
        public bool InputEnabled { get; set; } = true;

        public void Toggle()
        {
            if (open) Close();
            else Open();
        }

        private void SelectTab(int index)
        {
            selectedTab = index;
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null || tabLabels[i] == null) continue;
                bool active = i == index;
                var img = tabButtons[i].GetComponent<Image>();
                if (img != null) img.color = active ? V5Tokens.Key6 : V5Tokens.ControlBg;
                tabLabels[i].color = active ? V5Tokens.Text : V5Tokens.TextMuted;
            }
            if (settingsPage != null) settingsPage.SetActive(index == 0);
            if (displayPage != null) displayPage.SetActive(index == 1);
            if (rankingPage != null) rankingPage.SetActive(index == 2);
            if (keysPage != null) keysPage.SetActive(index == 3);
        }

        private void SelectRow(int index)
        {
            selectedRow = index;
            for (int i = 0; i < rowRects.Length; i++)
            {
                if (rowRects[i] == null) continue;
                var img = rowRects[i].gameObject.GetComponent<Image>();
                if (img != null) img.color = i == index ? V5Tokens.Key6 : V5Tokens.Recessed2;
            }
            if (helpTitle != null && index < HelpTitles.Length) helpTitle.text = HelpTitles[index];
            if (helpDesc != null && index < HelpTexts.Length) helpDesc.text = HelpTexts[index];
        }

        private void RefreshValues()
        {
            if (valueTexts == null) return;
            valueTexts[(int)Row.NoteSpeed].text = Mathf.RoundToInt(GManager.noteSpeed).ToString();
            valueTexts[(int)Row.NoteTiming].text = GManager.noteTiming.ToString();
            valueTexts[(int)Row.MainVolume].text = Mathf.RoundToInt(GManager.mainVolume * 100f).ToString();
            valueTexts[(int)Row.BGMVolume].text = Mathf.RoundToInt(GManager.BGMVolume * 100f).ToString();
            valueTexts[(int)Row.SEVolume].text = Mathf.RoundToInt(GManager.effectVolume * 100f).ToString();
            valueTexts[(int)Row.Flash].text = GManager.Flash ? "On" : "Off";
        }

        private void Adjust(int row, int dir)
        {
            switch ((Row)row)
            {
                case Row.NoteSpeed:
                    GManager.noteSpeed = Mathf.Clamp(Mathf.RoundToInt(GManager.noteSpeed) + dir, 1, 30);
                    valueTexts[row].text = Mathf.RoundToInt(GManager.noteSpeed).ToString();
                    break;
                case Row.NoteTiming:
                    GManager.noteTiming = Mathf.Clamp(GManager.noteTiming + dir, -10, 10);
                    valueTexts[row].text = GManager.noteTiming.ToString();
                    break;
                case Row.MainVolume:
                    GManager.mainVolume = Mathf.Clamp01(GManager.mainVolume + dir * 0.05f);
                    valueTexts[row].text = Mathf.RoundToInt(GManager.mainVolume * 100f).ToString();
                    ApplyVolumes();
                    break;
                case Row.BGMVolume:
                    GManager.BGMVolume = Mathf.Clamp01(GManager.BGMVolume + dir * 0.05f);
                    valueTexts[row].text = Mathf.RoundToInt(GManager.BGMVolume * 100f).ToString();
                    ApplyVolumes();
                    break;
                case Row.SEVolume:
                    GManager.effectVolume = Mathf.Clamp01(GManager.effectVolume + dir * 0.05f);
                    valueTexts[row].text = Mathf.RoundToInt(GManager.effectVolume * 100f).ToString();
                    ApplyVolumes();
                    break;
                case Row.Flash:
                    GManager.Flash = !GManager.Flash;
                    valueTexts[row].text = GManager.Flash ? "On" : "Off";
                    break;
            }
        }

        /// <summary>音量は各所有者が起動時にしか読まないので、シーン内の AudioSource にも即時反映する。</summary>
        private static void ApplyVolumes()
        {
            var music = FindFirstObjectByType<MusicManager>();
            if (music != null)
            {
                var src = music.GetComponent<AudioSource>();
                if (src != null) src.volume = GManager.mainVolume;
            }
            var judge = FindFirstObjectByType<Judge>();
            if (judge != null)
            {
                var src = judge.GetComponent<AudioSource>();
                if (src != null) src.volume = GManager.effectVolume;
            }
        }

        private void Update()
        {
            if (InputEnabled && Input.GetKeyDown(KeyCode.Escape))
            {
                Toggle();
                return;
            }
            if (!open) return;
            if (Input.GetKeyDown(KeyCode.UpArrow)) SelectRow(Mathf.Max(0, selectedRow - 1));
            if (Input.GetKeyDown(KeyCode.DownArrow)) SelectRow(Mathf.Min(RowCount - 1, selectedRow + 1));
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Adjust(selectedRow, -1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) Adjust(selectedRow, +1);
            // タブ切替 (S/D/L/K) はこのビュー固有のショートカット (矢印と同類) なので
            // 設定対象外の固定キーのまま。リトライ/リタイアはメニュー操作設定に従う (0.1.9)。
            if (Input.GetKeyDown(KeyCode.S)) SelectTab(0);
            if (Input.GetKeyDown(KeyCode.D)) SelectTab(1);
            if (Input.GetKeyDown(KeyCode.L)) SelectTab(2);
            if (Input.GetKeyDown(KeyCode.K)) SelectTab(3);
            if (KeyActions.IsDown(KeyActions.KeyAction.MenuRetry) && sceneManager != null) sceneManager.RetryGame();
            if (KeyActions.IsDown(KeyActions.KeyAction.MenuRetire) && sceneManager != null) sceneManager.RetireGame();
        }
    }
}
