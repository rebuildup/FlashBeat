using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// Issue #120: OptionScene の v5 UI (design 06/06b/06c Option, fhd 1920x1080 実測値)。
    /// コードでレイアウトを構築し、既存 OptionSceneManager の振る舞い
    /// (SpeedUp/SpeedDown/TimingUp/TimingDown/TitleScene) をコールバックで再利用する。
    /// Flash/FlashT/FlashBG/音量は GManager の静的フィールドを直接更新する
    /// (旧メソッドはレガシー TMP 参照を持つため、非アクティブ化後の安全のため直接更新)。
    ///
    /// タブ: Play / Display / Sound / Data。ナビゲーションはレイアウト非依存:
    /// ↑↓ (W/S) 行選択、←→ 値変更、Tab でタブ切替、Esc で戻る、Enter/Space 決定、マウスクリック。
    /// デザインにあるが実データの無い行 (Key Guide / Judge Text / Lyrics / Video Dim /
    /// Combo Display / Player Name / Ranking Submit / History Keep) は表示しない。
    /// </summary>
    public sealed class OptionView : MonoBehaviour
    {
        /// <summary>設定行の定義 (表示値と操作をデリゲートで持つ)。</summary>
        public sealed class Row
        {
            public string Label;
            public Func<string> Value;
            public Action<int> Adjust;
            public Action Activate;
            public string DescTitle;
            public string Desc;
            public Color LabelColor = V5Tokens.Text;

            public Row(string label, Func<string> value, Action<int> adjust, Action activate,
                string descTitle, string desc)
            {
                Label = label;
                Value = value;
                Adjust = adjust;
                Activate = activate;
                DescTitle = descTitle;
                Desc = desc;
            }
        }

        private static readonly string[] TabLabels = { "Play", "Display", "Sound", "Data" };

        /// <summary>凡例「戻る」のキャプ。入力はメニュー操作設定 (KeyActions.Back) の設定値に従う。</summary>
        public static string BackCap()
        {
            return ActionKeyHint.Primary(KeyActions.KeyAction.Back);
        }

        private Action onBack;
        private Action speedUp;
        private Action speedDown;
        private Action timingUp;
        private Action timingDown;

        private Image[] tabBackgrounds;
        private GameObject listPanel;
        private readonly List<Image> rowBackgrounds = new List<Image>();
        private readonly List<TextMeshProUGUI> rowValues = new List<TextMeshProUGUI>();
        private TextMeshProUGUI descTitle;
        private TextMeshProUGUI descText;
        private GameObject previewWell;

        private int selectedTab;
        private int selectedRow;
        private bool clearConfirmArmed;
        private TextMeshProUGUI backCap;

        private List<Row> currentRows = new List<Row>();

        // --- 純ロジック (テスト対象) ---

        /// <summary>音量を 0..1 にクランプし 0.1 刻みで±する。</summary>
        public static float AdjustVolume(float value, int delta)
        {
            return Mathf.Clamp(Mathf.Round((value + delta * 0.1f) * 10f) / 10f, 0f, 1f);
        }

        /// <summary>音量の表示 (% 整数)。</summary>
        public static string FormatVolume(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }

        /// <summary>NoteSpeed の表示 (F1)。</summary>
        public static string FormatSpeed(float value)
        {
            return value.ToString("F1");
        }

        /// <summary>FlashSpeed (FlashBG) の循環: 1 → 2 → 4 → 8 → 1。</summary>
        public static int CycleFlashSpeed(int current, int delta)
        {
            int[] steps = { 1, 2, 4, 8 };
            int index = Array.IndexOf(steps, current);
            if (index < 0)
            {
                index = 0;
            }

            return steps[(index + delta % steps.Length + steps.Length) % steps.Length];
        }

        /// <summary>Key Layout 行の表示値 (保存済みキーバインドがあればカスタム)。
        /// キー名は KeyBindings の PlayerPrefsStorage と同じ (private なのでここでも literal)。</summary>
        public const string KeyBindingsPrefsKey = "FlashBeat.KeyBindings";

        public static string KeyLayoutLabel()
        {
            return PlayerPrefs.HasKey(KeyBindingsPrefsKey) ? "カスタム" : "標準";
        }

        /// <summary>行選択の折り返し。</summary>
        public static int WrapIndex(int index, int count, int delta)
        {
            if (count <= 0)
            {
                return 0;
            }

            return (index + delta % count + count) % count;
        }

        // --- 構築 ---

        /// <summary>
        /// Canvas (ScreenSpaceOverlay, Constant Pixel Size x1) ごと新規に構築する。
        /// レガシー UI は呼び出し側が非アクティブ化すること。
        /// </summary>
        public static OptionView Build(Action back, Action speedUp, Action speedDown,
            Action timingUp, Action timingDown)
        {
            var canvasGo = new GameObject("V5 Option Canvas", typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasConfigurator.Configure(canvas);

            var root = V5Ui.Node(canvasGo, "Root");
            V5Ui.Fill(root.GetComponent<RectTransform>(), 0f);
            var view = root.AddComponent<OptionView>();
            view.onBack = back;
            view.speedUp = speedUp;
            view.speedDown = speedDown;
            view.timingUp = timingUp;
            view.timingDown = timingDown;

            V5Decor.BuildBackdrop(root);
            view.BuildHeader();
            view.BuildTabStrip();
            view.BuildRightColumn();
            view.BuildLegend();
            view.SelectTab(0);
            return view;
        }

        private float S(float fhdPx)
        {
            return V5Decor.S(fhdPx);
        }

        private void Update()
        {
            var rows = currentRows;
            if (rows.Count == 0)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                SelectRow(WrapIndex(selectedRow, rows.Count, -1));
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                SelectRow(WrapIndex(selectedRow, rows.Count, 1));
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Adjust(rows, -1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                Adjust(rows, 1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                SelectTab(WrapIndex(selectedTab, TabLabels.Length, 1));
                return;
            }

            if (KeyActions.IsDown(KeyActions.KeyAction.Back))
            {
                if (onBack != null) onBack();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space))
            {
                var row = rows[selectedRow];
                if (row.Activate != null) row.Activate();
            }
        }

        private void Adjust(List<Row> rows, int delta)
        {
            var row = rows[selectedRow];
            clearConfirmArmed = false;
            if (row.Adjust != null) row.Adjust(delta);
            RefreshRowValues();
        }

        // --- タブ/行の状態 ---

        private void SelectTab(int index)
        {
            selectedTab = Mathf.Clamp(index, 0, TabLabels.Length - 1);
            selectedRow = 0;
            clearConfirmArmed = false;
            for (int i = 0; i < tabBackgrounds.Length; i++)
            {
                tabBackgrounds[i].color = i == selectedTab ? V5Tokens.Key6 : V5Tokens.Key2;
            }

            BuildRows();
        }

        private void SelectRow(int index)
        {
            selectedRow = WrapIndex(index, currentRows.Count, 0);
            clearConfirmArmed = false;
            for (int i = 0; i < rowBackgrounds.Count; i++)
            {
                rowBackgrounds[i].color = i == selectedRow ? V5Tokens.Key6 : V5Tokens.Overlay;
            }

            UpdateDescription();
        }

        private void RefreshRowValues()
        {
            for (int i = 0; i < currentRows.Count && i < rowValues.Count; i++)
            {
                rowValues[i].text = currentRows[i].Value();
            }
        }

        private void UpdateDescription()
        {
            if (currentRows.Count == 0)
            {
                return;
            }

            var row = currentRows[selectedRow];
            descTitle.text = row.DescTitle;
            descText.text = row.Desc;
        }

        // --- 行定義 ---

        private List<Row> BuildPlayRows()
        {
            return new List<Row>
            {
                new Row("NoteSpeed",
                    () => FormatSpeed(GManager.noteSpeed),
                    d => { if (d > 0 && speedUp != null) speedUp(); else if (d < 0 && speedDown != null) speedDown(); },
                    null,
                    "NoteSpeed",
                    "ノーツが出現してから判定線に届くまでの時間。数値が大きいほど速く落ちます。"),
                new Row("NoteTiming",
                    () => GManager.noteTiming.ToString(),
                    d => { if (d > 0 && timingUp != null) timingUp(); else if (d < 0 && timingDown != null) timingDown(); },
                    null,
                    "NoteTiming",
                    "判定タイミングのずれを調整します。"),
                new Row("Key Layout",
                    KeyLayoutLabel,
                    d =>
                    {
                        if (d > 0)
                        {
                            OpenKeyConfig(); // カスタムはキー設定で編集する
                        }
                        else
                        {
                            KeyBindings.ResetToDefaults();
                            KeyBindings.Save();
                        }
                    },
                    OpenKeyConfig,
                    "Key Layout",
                    "キー配列を選びます。標準は既定の配列、カスタムはキー設定で編集します。"),
                new Row("キー設定",
                    () => "F2",
                    null,
                    OpenKeyConfig,
                    "キー設定",
                    "キーの割り当てを編集します。F2 でも開けます。"),
            };
        }

        private List<Row> BuildDisplayRows()
        {
            return new List<Row>
            {
                new Row("Flash",
                    () => GManager.Flash ? "On" : "Off",
                    d => { if (d != 0) GManager.Flash = !GManager.Flash; },
                    () => { GManager.Flash = !GManager.Flash; RefreshRowValues(); },
                    "Flash",
                    "ノーツ判定時の背景フラッシュの On/Off。"),
                new Row("FlashTiming",
                    () => GManager.FlashT.ToString(),
                    d => GManager.FlashT += d,
                    null,
                    "FlashTiming",
                    "フラッシュのタイミングを調整します。"),
                new Row("FlashSpeed",
                    () => GManager.FlashBG.ToString(),
                    d => GManager.FlashBG = CycleFlashSpeed(GManager.FlashBG, d),
                    null,
                    "FlashSpeed",
                    "フラッシュの速さ。1 が最も速く、8 が最も遅い。"),
            };
        }

        private List<Row> BuildSoundRows()
        {
            return new List<Row>
            {
                new Row("Main Volume",
                    () => FormatVolume(GManager.mainVolume),
                    d =>
                    {
                        GManager.mainVolume = AdjustVolume(GManager.mainVolume, d);
                        AudioListener.volume = GManager.mainVolume;
                    },
                    null,
                    "Main Volume",
                    "全体の音量。"),
                new Row("Effect Volume",
                    () => FormatVolume(GManager.effectVolume),
                    d => GManager.effectVolume = AdjustVolume(GManager.effectVolume, d),
                    null,
                    "Effect Volume",
                    "効果音の音量。"),
                new Row("BGM Volume",
                    () => FormatVolume(GManager.BGMVolume),
                    d => GManager.BGMVolume = AdjustVolume(GManager.BGMVolume, d),
                    null,
                    "BGM Volume",
                    "BGM の音量。"),
            };
        }

        private List<Row> BuildDataRows()
        {
            return new List<Row>
            {
                new Row("Clear save data",
                    () => clearConfirmArmed ? "もう一度 Enter" : "",
                    null,
                    ClearSaveData,
                    "Clear save data",
                    "ハイスコアとキー設定を消去します。取り消せません。")
                {
                    LabelColor = V5Tokens.Danger,
                },
            };
        }

        private void ClearSaveData()
        {
            if (!clearConfirmArmed)
            {
                clearConfirmArmed = true;
                RefreshRowValues();
                return;
            }

            clearConfirmArmed = false;
            PlayerPrefs.DeleteKey("Hiscore");
            PlayerPrefs.DeleteKey(OptionView.KeyBindingsPrefsKey);
            PlayerPrefs.Save();
            Array.Clear(GManager.Hiscore, 0, GManager.Hiscore.Length);
            KeyBindings.ResetToDefaults();
            KeyBindings.Save();
            RefreshRowValues();
        }

        /// <summary>
        /// KeyConfigUI.Open() を反射で呼ぶ (issue optkeys で追加予定)。
        /// 未追加の環境では Toggle() にフォールバックする。
        /// </summary>
        private static void OpenKeyConfig()
        {
            var ui = FindAnyObjectByType<KeyConfigUI>();
            if (ui == null)
            {
                return;
            }

            var method = typeof(KeyConfigUI).GetMethod("Open", Type.EmptyTypes);
            if (method != null)
            {
                if (method.IsStatic)
                {
                    method.Invoke(null, null);
                }
                else
                {
                    method.Invoke(ui, null);
                }

                return;
            }

            ui.Toggle();
        }

        // --- レイアウト ---

        /// <summary>戻るボタン (48,48 / 56x56) + OPTION (124,44 / fs52)。</summary>
        private void BuildHeader()
        {
            var back = V5Ui.Panel(gameObject, "Back", new Vector2(S(56f), S(56f)), V5Tokens.Key4,
                V5Tokens.RadiusMedium);
            V5Ui.Place(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(S(48f), -S(48f)), new Vector2(S(56f), S(56f)));
            var backLabel = V5Ui.Text(back, "Label", "<", UISizeProfile.Current.FsM, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            var backLabelRt = backLabel.rectTransform;
            backLabelRt.anchorMin = Vector2.zero;
            backLabelRt.anchorMax = Vector2.one;
            backLabelRt.offsetMin = Vector2.zero;
            backLabelRt.offsetMax = Vector2.zero;
            var backButton = back.AddComponent<Button>();
            backButton.onClick.AddListener(() => { if (onBack != null) onBack(); });

            var title = V5Ui.Text(gameObject, "OPTION", "OPTION", S(52f), V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            V5Ui.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(S(124f), -S(44f)), new Vector2(S(400f), S(64f)));
            title.textWrappingMode = TextWrappingModes.NoWrap;
        }

        /// <summary>タブ strip (560,48 / 800x48 / bg Key2 / 選択 Key6)。</summary>
        private void BuildTabStrip()
        {
            var strip = V5Ui.Panel(gameObject, "Tab Strip", new Vector2(S(800f), S(48f)), V5Tokens.Key2,
                V5Tokens.RadiusMedium);
            V5Ui.Place(strip.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(S(560f), -S(48f)), new Vector2(S(800f), S(48f)));

            float tabW = S(800f) / TabLabels.Length;
            tabBackgrounds = new Image[TabLabels.Length];
            for (int i = 0; i < TabLabels.Length; i++)
            {
                int index = i;
                var tab = V5Ui.Panel(strip, "Tab " + TabLabels[i], new Vector2(tabW - S(8f), S(40f)),
                    V5Tokens.Key2, V5Tokens.RadiusMedium);
                var tabRt = tab.GetComponent<RectTransform>();
                tabRt.anchorMin = new Vector2(0f, 0.5f);
                tabRt.anchorMax = new Vector2(0f, 0.5f);
                tabRt.pivot = new Vector2(0f, 0.5f);
                tabRt.anchoredPosition = new Vector2(tabW * i + S(4f), 0f);

                var label = V5Ui.Text(tab, "Label", TabLabels[i], UISizeProfile.Current.FsM,
                    V5Tokens.TextMuted, TextAlignmentOptions.Center);
                var labelRt = label.rectTransform;
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;

                var button = tab.AddComponent<Button>();
                button.onClick.AddListener(() => SelectTab(index));

                tabBackgrounds[i] = tab.GetComponent<Image>();
            }
        }

        /// <summary>右側 (1052,164 / 548幅): Preview well (Play のみ) + 説明。</summary>
        private void BuildRightColumn()
        {
            var col = V5Ui.Node(gameObject, "Right");
            V5Ui.Place(col.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(S(1052f), -S(164f)), new Vector2(S(548f), S(700f)));

            previewWell = V5Ui.Panel(col, "Preview", new Vector2(S(548f), S(300f)), V5Tokens.Well,
                V5Tokens.RadiusMedium);
            var previewRt = previewWell.GetComponent<RectTransform>();
            previewRt.anchorMin = new Vector2(0f, 1f);
            previewRt.anchorMax = new Vector2(1f, 1f);
            previewRt.pivot = new Vector2(0.5f, 1f);
            previewRt.anchoredPosition = new Vector2(0f, 0f);
            previewRt.sizeDelta = new Vector2(0f, S(300f));

            descTitle = V5Ui.Text(col, "Desc Title", "", S(38f), V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            var titleRt = descTitle.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -S(328f));
            titleRt.sizeDelta = new Vector2(0f, S(48f));
            descTitle.textWrappingMode = TextWrappingModes.NoWrap;
            descTitle.overflowMode = TextOverflowModes.Ellipsis;

            descText = V5Ui.Text(col, "Desc", "", S(30f), V5Tokens.TextMuted);
            var descRt = descText.rectTransform;
            descRt.anchorMin = new Vector2(0f, 1f);
            descRt.anchorMax = new Vector2(1f, 1f);
            descRt.pivot = new Vector2(0f, 1f);
            descRt.anchoredPosition = new Vector2(0f, -S(384f));
            descRt.sizeDelta = new Vector2(0f, S(200f));
        }

        /// <summary>左リスト (320,164 / 700幅 / 行 64 + gap 10 / bg Well) を作り直す。</summary>
        private void BuildRows()
        {
            currentRows = selectedTab == 0 ? BuildPlayRows()
                : selectedTab == 1 ? BuildDisplayRows()
                : selectedTab == 2 ? BuildSoundRows()
                : BuildDataRows();

            if (listPanel != null)
            {
                Destroy(listPanel);
            }

            rowBackgrounds.Clear();
            rowValues.Clear();

            int n = currentRows.Count;
            float listH = n * S(64f) + (n - 1) * S(10f);
            listPanel = V5Ui.Panel(gameObject, "List", new Vector2(S(700f), listH), V5Tokens.Well,
                V5Tokens.RadiusMedium);
            V5Ui.Place(listPanel.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(S(320f), -S(164f)), new Vector2(S(700f), listH));

            for (int i = 0; i < n; i++)
            {
                int index = i;
                var row = currentRows[i];
                var rowGo = V5Ui.Panel(listPanel, "Row " + row.Label, new Vector2(S(700f) - S(8f), S(64f)),
                    V5Tokens.Overlay, V5Tokens.RadiusMedium);
                var rowRt = rowGo.GetComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0f, 1f);
                rowRt.anchorMax = new Vector2(1f, 1f);
                rowRt.pivot = new Vector2(0.5f, 1f);
                rowRt.anchoredPosition = new Vector2(0f, -i * S(74f));
                rowRt.sizeDelta = new Vector2(-S(8f), S(64f));

                var label = V5Ui.Text(rowGo, "Label", row.Label, UISizeProfile.Current.FsM, row.LabelColor);
                var labelRt = label.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(1f, 1f);
                labelRt.offsetMin = new Vector2(S(20f), 0f);
                labelRt.offsetMax = new Vector2(-S(220f), 0f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;

                var value = V5Ui.Text(rowGo, "Value", row.Value(), UISizeProfile.Current.FsM, V5Tokens.Text,
                    TextAlignmentOptions.Center, FontStyles.Bold);
                var valueRt = value.rectTransform;
                valueRt.anchorMin = new Vector2(1f, 0f);
                valueRt.anchorMax = new Vector2(1f, 1f);
                valueRt.pivot = new Vector2(0.5f, 0.5f);
                valueRt.anchoredPosition = new Vector2(-S(134f), 0f);
                valueRt.sizeDelta = new Vector2(S(104f), 0f);
                value.textWrappingMode = TextWrappingModes.NoWrap;
                value.overflowMode = TextOverflowModes.Ellipsis;

                MakeStepper(rowGo, "Prev", new Vector2(-S(226f), 0f), "<", () =>
                {
                    SelectRow(index);
                    Adjust(currentRows, -1);
                });
                MakeStepper(rowGo, "Next", new Vector2(-S(42f), 0f), ">", () =>
                {
                    SelectRow(index);
                    Adjust(currentRows, 1);
                });

                var rowButton = rowGo.AddComponent<Button>();
                rowButton.onClick.AddListener(() => SelectRow(index));

                rowBackgrounds.Add(rowGo.GetComponent<Image>());
                rowValues.Add(value);
            }

            SelectRow(selectedRow < n ? selectedRow : 0);
            UpdateDescription();
            RefreshRowValues();
            previewWell.SetActive(selectedTab == 0);
            // Play タブのときは説明を Preview の下に置く (design 06)。
            bool withPreview = selectedTab == 0;
            descTitle.rectTransform.anchoredPosition = new Vector2(0f, withPreview ? -S(328f) : -S(8f));
            descText.rectTransform.anchoredPosition = new Vector2(0f, withPreview ? -S(384f) : -S(64f));
            if (withPreview)
            {
                BuildLanePreview();
            }
        }

        /// <summary>行内の < / > ステッパーボタン (40x40, Key4, r8)。</summary>
        private void MakeStepper(GameObject row, string name, Vector2 pos, string glyph, Action onClick)
        {
            var btn = V5Ui.Panel(row, name, new Vector2(S(40f), S(40f)), V5Tokens.Key4, V5Tokens.RadiusMedium);
            var btnRt = btn.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 0.5f);
            btnRt.anchorMax = new Vector2(1f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = pos;

            var glyphTmp = V5Ui.Text(btn, "Glyph", glyph, S(24f), V5Tokens.TextMuted, TextAlignmentOptions.Center);
            var glyphRt = glyphTmp.rectTransform;
            glyphRt.anchorMin = Vector2.zero;
            glyphRt.anchorMax = Vector2.one;
            glyphRt.offsetMin = Vector2.zero;
            glyphRt.offsetMax = Vector2.zero;

            btn.AddComponent<Button>().onClick.AddListener(() => onClick());
        }

        /// <summary>Play タブの簡易レーンプレビュー (判定線 + ノーツ帯)。</summary>
        private void BuildLanePreview()
        {
            // 再構築のたびに子が積まるのを防ぐ
            for (int i = previewWell.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(previewWell.transform.GetChild(i).gameObject);
            }

            // 判定線 (白, 下部)
            var judgeLine = V5Ui.Node(previewWell, "Judge Line");
            V5Ui.Place(judgeLine.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, S(44f)), new Vector2(S(440f), S(4f)));
            var judgeImg = judgeLine.AddComponent<Image>();
            judgeImg.color = V5Tokens.Text;
            judgeImg.raycastTarget = false;

            // ノーツ帯 (白, 階段状)
            Vector2[] notes =
            {
                new Vector2(-S(150f), S(90f)),
                new Vector2(-S(50f), S(150f)),
                new Vector2(S(60f), S(210f)),
                new Vector2(S(160f), S(260f)),
            };
            foreach (var pos in notes)
            {
                var note = V5Ui.Node(previewWell, "Note");
                V5Ui.Place(note.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                    pos, new Vector2(S(90f), S(24f)));
                var noteImg = note.AddComponent<Image>();
                noteImg.color = V5Tokens.Text;
                noteImg.raycastTarget = false;
            }

            // 左右のレーンガイド (細い線, TextDim)
            for (int i = -2; i <= 2; i++)
            {
                var lane = V5Ui.Node(previewWell, "Lane " + i);
                V5Ui.Place(lane.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(i * S(110f), 0f), new Vector2(S(2f), S(300f)));
                var laneImg = lane.AddComponent<Image>();
                laneImg.color = V5Tokens.TextDim;
                laneImg.raycastTarget = false;
            }
        }

        /// <summary>Legend (48,1000): ↑↓ 項目 / ←→ 値 / Tab カテゴリ / 戻るキー (KeyActions.Back)。</summary>
        private void BuildLegend()
        {
            string back = BackCap();
            var legendGo = V5Decor.BuildLegend(gameObject, new Vector2(S(48f), -S(1000f)), new[]
            {
                ("↑", S(46f), (string)"項目"),
                ("↓", S(46f), (string)null),
                ("←", S(46f), (string)"値"),
                ("→", S(46f), (string)null),
                ("Tab", S(76f), (string)"カテゴリ"),
                (back, S(76f), (string)"戻る"),
            });
            var capTransform = legendGo.transform.Find("KeyCap " + back);
            if (capTransform != null)
            {
                backCap = capTransform.GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        private void OnEnable()
        {
            // キー設定オーバーレイ (F2) はこの画面を生かしたまま割り当てを変えられる。
            KeyActions.Changed += RefreshKeyCaps;
            KeyBindings.Changed += RefreshKeyCaps;
        }

        private void OnDisable()
        {
            KeyActions.Changed -= RefreshKeyCaps;
            KeyBindings.Changed -= RefreshKeyCaps;
        }

        /// <summary>キー設定が変わったら凡例キャップと行の値 (Key Layout など) を描き直す。</summary>
        private void RefreshKeyCaps()
        {
            if (backCap != null)
            {
                backCap.text = BackCap();
            }

            if (currentRows.Count > 0)
            {
                RefreshRowValues();
            }
        }
    }
}
