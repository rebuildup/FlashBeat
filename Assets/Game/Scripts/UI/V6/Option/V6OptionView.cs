using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;

namespace FlashBeat.UI.V6.Option
{
    /// <summary>
    /// Option 画面の v6 UI。design/flashbeat-ui.pen v6 の
    /// 06 Play (B6If6Z) / 06 Display (EzLfb) / 06 Sound (z7joSy) / 06 Data (DQHHp) を
    /// 実行時に構築する (1 フレーム 1728x972 / 4 タブ)。
    ///
    /// - 実データ: GManager (noteSpeed / noteTiming / Flash / FlashT / FlashBG / 音量) +
    ///   OptionSettings (保存) + KeyBindings / KeyConfigUI (キー配列) + PlaySession
    ///   (Key Guide / Lyrics) + MediaCacheManager (Media cache)。サンプル値は書かない。
    /// - 設定の書き戻しは既存 API 経由 (NoteSpeed / NoteTiming は OptionSceneManager の
    ///   SpeedUp / SpeedDown / TimingUp / TimingDown — Timing は全曲の offset JSON への
    ///   書き戻しを含む既存経路。Flash / 音量は V5 OptionView と同じ GManager 直接 +
    ///   OptionSettings.Save()。新しい保存経路は作らない)。
    /// - design にあるが実データの無い行 (Live Rank / Hit Timing / Judge Text / Progress Bar)
    ///   は表示しない (V5 OptionView と同じ方針。固定値のダミーは作らない)。
    /// - 構築はコードのみ (シーン YAML / .meta は触らない)。Refresh で frame の全子を作り直す。
    /// - キー: ↑↓ 行選択 / ←→ 値変更 / Tab タブ切替 / Enter・Space 決定 / Esc 戻る /
    ///   F2 キー設定。マウス: タブ / 行 / ステッパー / 戻る / フッター。
    /// - TitleSceneManager は呼ばない。onBack は統合担当が渡す。
    /// </summary>
    public sealed class V6OptionView
    {
        /// <summary>現在生成されている v6 Option 画面 (シーンに 1 個だけ)。</summary>
        public static V6OptionView Current { get; private set; }

        // --- design px 定数 (06 Play / Display / Sound / Data の実測) ---
        private const float FrameW = 1728f;
        private const float FrameH = 972f;

        private const float MainX = 24f, MainY = 96f;
        private const float TabsX = 438f, TabsY = 0f, TabsW = 824f, TabsH = 48f, TabW = 200f, TabGap = 8f;

        // 設定 (列2-3) (304,68) 512x688
        private const float SetX = 304f, SetY = 68f, SetW = 512f, SetH = 688f;
        private const float SetRowX = 8f, SetRowW = 496f, SetRowH = 56f, SetRowY0 = 60f;

        // プレビュー (列4-5) (864,68) 512x688
        private const float PrevX = 864f, PrevY = 68f, PrevW = 512f, PrevH = 688f;
        private const float GamePrevX = 24f, GamePrevY = 60f, GamePrevW = 464f, GamePrevH = 280f;

        private readonly Action onBack;

        private GameObject rootGo;
        private RectTransform frame;
        private float u = 1f;
        private int lastWidth;
        private int lastHeight;

        private int selectedTab;
        private int selectedRow;
        private bool confirmClearOpen;

        private List<Row> currentRows = new List<Row>();

        /// <summary>設定行の定義 (表示値と操作をデリゲートで持つ。V5 OptionView.Row と同じ形)。</summary>
        private sealed class Row
        {
            public readonly string Label;
            public readonly Func<string> Value;
            public readonly Action<int> Adjust;
            public readonly Action Activate;
            public readonly string DescTitle;
            public readonly string Desc;
            public readonly bool Danger;
            public readonly bool IsAction; // ステッパーを持たない行 (キー設定 / Data の 2 行)

            public Row(string label, Func<string> value, Action<int> adjust, Action activate,
                string descTitle, string desc, bool danger = false, bool isAction = false)
            {
                Label = label;
                Value = value;
                Adjust = adjust;
                Activate = activate;
                DescTitle = descTitle;
                Desc = desc;
                Danger = danger;
                IsAction = isAction;
            }
        }

        private V6OptionView(Action onBack)
        {
            this.onBack = onBack;
        }

        /// <summary>View を生成する。統合担当 (OptionSceneManager 側) から呼ぶ。</summary>
        public static V6OptionView Create(RectTransform parent, Action onBack)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var view = new V6OptionView(onBack);
            view.BuildRoot(parent);
            // キー設定 (KeyConfigUI) での変更をプレビューのキー割当に反映する。
            KeyBindings.Changed += view.OnBindingsChanged;
            view.Rebuild();
            Current = view;
            return view;
        }

        public RectTransform Root => frame;

        /// <summary>実データ (GManager / KeyBindings / MediaCache) から表示を作り直す。</summary>
        public void Refresh()
        {
            Rebuild();
        }

        public void SetVisible(bool visible)
        {
            if (rootGo != null)
            {
                rootGo.SetActive(visible);
            }
        }

        /// <summary>生成したビューを破棄するとき購読を解除する。</summary>
        public void Dispose()
        {
            KeyBindings.Changed -= OnBindingsChanged;
            if (rootGo != null)
            {
                V5GameUi.DestroyNow(rootGo);
            }

            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }
        }

        private void OnBindingsChanged()
        {
            if (rootGo == null || !rootGo.activeInHierarchy)
            {
                return;
            }

            Rebuild();
        }

        // --- 毎フレーム (V6OptionViewDriver から呼ばれる) ---

        internal void Tick()
        {
            if (rootGo == null || !rootGo.activeInHierarchy)
            {
                return;
            }

            // キー設定 (KeyConfigUI) が開いている間は背面の操作を止める
            // (ドライバが suspend されない環境でも二重入力しない)。
            if (KeyConfigUI.IsOpen || !FlashBeat.UI.SceneInputGate.InputAllowed)
            {
                return;
            }

            UpdateScale();

            bool up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            bool down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            bool left = Input.GetKeyDown(KeyCode.LeftArrow);
            bool right = Input.GetKeyDown(KeyCode.RightArrow);
            bool confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space);
            bool cancel = Input.GetKeyDown(KeyCode.Escape);
            bool tab = Input.GetKeyDown(KeyCode.Tab);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var action = V6OptionLogic.MapKey(confirmClearOpen, up, down, left, right, confirm, cancel,
                tab && !shift, tab && shift);

            if (Input.GetKeyDown(KeyCode.F2))
            {
                OpenKeyConfig();
                return;
            }

            switch (action)
            {
                case V6OptionAction.Cancel:
                    if (confirmClearOpen)
                    {
                        confirmClearOpen = false;
                        Rebuild();
                    }
                    else if (onBack != null)
                    {
                        onBack();
                    }
                    return;
                case V6OptionAction.Confirm:
                    if (confirmClearOpen)
                    {
                        ClearSaveData();
                        confirmClearOpen = false;
                        Rebuild();
                        return;
                    }
                    ActivateRow();
                    return;
                case V6OptionAction.None:
                    return;
            }

            if (currentRows.Count == 0)
            {
                return;
            }

            switch (action)
            {
                case V6OptionAction.MoveUp:
                    selectedRow = V6OptionLogic.WrapIndex(selectedRow, currentRows.Count, -1);
                    Rebuild();
                    return;
                case V6OptionAction.MoveDown:
                    selectedRow = V6OptionLogic.WrapIndex(selectedRow, currentRows.Count, 1);
                    Rebuild();
                    return;
                case V6OptionAction.AdjustPrev:
                    AdjustSelected(-1);
                    return;
                case V6OptionAction.AdjustNext:
                    AdjustSelected(1);
                    return;
                case V6OptionAction.NextTab:
                    SelectTab(selectedTab + 1);
                    return;
                case V6OptionAction.PrevTab:
                    SelectTab(selectedTab - 1);
                    return;
            }
        }

        private void AdjustSelected(int delta)
        {
            if (currentRows.Count == 0)
            {
                return;
            }

            var row = currentRows[selectedRow];
            if (row.Adjust != null)
            {
                row.Adjust(delta);
            }

            Rebuild();
        }

        private void ActivateRow()
        {
            if (currentRows.Count == 0)
            {
                return;
            }

            var row = currentRows[selectedRow];
            if (row.Activate != null)
            {
                row.Activate();
                Rebuild();
            }
        }

        private void SelectTab(int index)
        {
            selectedTab = V6OptionLogic.ClampTabIndex(index);
            selectedRow = 0;
            Rebuild();
        }

        // --- 構築 ---

        private void BuildRoot(RectTransform parent)
        {
            rootGo = V5Ui.Node(parent.gameObject, "V6 Option View");
            var rt = rootGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            frame = V5Ui.Node(rootGo, "V6 Option Frame 1728x972").GetComponent<RectTransform>();
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(FrameW, FrameH);

            var driver = rootGo.AddComponent<V6OptionViewDriver>();
            driver.View = this;
            UpdateScale();
        }

        private void UpdateScale()
        {
            int w = Screen.width;
            int h = Screen.height;
            if (w == lastWidth && h == lastHeight)
            {
                return;
            }

            lastWidth = w;
            lastHeight = h;
            u = V6OptionLogic.DesignScale(w, h);
            if (frame != null)
            {
                frame.localScale = new Vector3(u, u, 1f);
            }
        }

        private void Rebuild()
        {
            if (rootGo == null)
            {
                return;
            }

            UpdateScale();

            var children = new List<GameObject>();
            foreach (Transform child in frame.transform)
            {
                children.Add(child.gameObject);
            }

            for (int i = 0; i < children.Count; i++)
            {
                V5GameUi.DestroyNow(children[i]);
            }

            currentRows = RowsForTab(selectedTab);
            if (selectedRow >= currentRows.Count)
            {
                selectedRow = 0;
            }

            BuildHeader();
            BuildFooter();
            BuildTabs();
            BuildSettings();
            BuildPreview();
            if (confirmClearOpen)
            {
                BuildConfirmClearModal();
            }
        }

        // --- ヘッダー (24,0 1680x96) ---

        private void BuildHeader()
        {
            var header = At(V5Ui.Panel(frame.gameObject, "Header", new Vector2(1680f, 96f), Clear(), 0f), 24f, 0f);

            // 戻るボタン 56x56 (#3A3A3A) + Esc キャプション (V6SelectView と同じ構造)
            var backZone = At(V5Ui.Node(header, "Zone Back"), 0f, 20f);
            var back = Button(backZone, "Back Button", 56f, 56f, 8f, V5Tokens.Key4, () =>
            {
                if (onBack != null) onBack();
            });
            var backCol = V5GameUi.Column(back, "Inner", 2f, TextAnchor.MiddleCenter);
            V5GameUi.Text(backCol.gameObject, "Icon", "←", 22f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            V5GameUi.Text(backCol.gameObject, "Key", "Esc", 13f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(backCol.GetComponent<RectTransform>(), 56f, 56f);

            var title = V5GameUi.Text(header.gameObject, "Title", "FlashBeat", 38f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            At(title, 72f, 20f, 400f, 56f);
        }

        // --- フッター (24,876 1680x96) ---

        private void BuildFooter()
        {
            var footer = At(V5Ui.Panel(frame.gameObject, "Footer", new Vector2(1680f, 96f), Clear(), 0f), 24f, 876f);

            var legend = V5GameUi.Row(footer.gameObject, "Legend", 24f, TextAnchor.MiddleLeft);
            At(legend.GetComponent<RectTransform>(), 0f, 30f, 700f, 36f);
            V5GameUi.LegendItem(legend.gameObject, "↑↓", "選択");
            V5GameUi.LegendItem(legend.gameObject, "←→", "値変更");
            V5GameUi.LegendItem(legend.gameObject, "Tab", "タブ切替");
            V5GameUi.LegendItem(legend.gameObject, "Space", "決定");
            V5GameUi.LegendItem(legend.gameObject, "Esc", "戻る");

            // 右下: 決定 (secondary) + 戻る (primary)。design の座標 (補助 358 / 主 567) を右詰め。
            var decide = Button(footer, "Button Decide", 197f, 56f, 8f, V5Tokens.ControlBg, ActivateRow);
            At(decide, 1198f, 20f);
            var decideRow = V5GameUi.Row(decide, "Inner", 10f, TextAnchor.MiddleCenter);
            Stretch(decideRow.GetComponent<RectTransform>(), 197f, 56f);
            V5GameUi.Text(decideRow.gameObject, "Label", "決定", 30f, V5Tokens.Text, TextAlignmentOptions.Center);
            var decideCap = V5Ui.Panel(decideRow.gameObject, "Cap", new Vector2(84f, 32f),
                new Color(0f, 0f, 0f, 0.25f), 4f);
            var decideCapText = V5GameUi.Text(decideCap, "Key", "Space", 20f, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            Stretch(decideCapText.GetComponent<RectTransform>(), 84f, 32f);

            var back = Button(footer, "Button Back", 273f, 56f, 8f, V5Tokens.ButtonPrimaryBg, () =>
            {
                if (onBack != null) onBack();
            });
            At(back, 1407f, 20f);
            var backRow = V5GameUi.Row(back, "Inner", 10f, TextAnchor.MiddleCenter);
            Stretch(backRow.GetComponent<RectTransform>(), 273f, 56f);
            V5GameUi.Text(backRow.gameObject, "Icon", "◀", 24f, V5Tokens.ButtonPrimaryLabel, TextAlignmentOptions.Center);
            V5GameUi.Text(backRow.gameObject, "Label", "戻る", 30f, V5Tokens.ButtonPrimaryLabel,
                TextAlignmentOptions.Center);
            var backCap = V5Ui.Panel(backRow.gameObject, "Cap", new Vector2(64f, 32f),
                new Color(0f, 0f, 0f, 0.15f), 4f);
            var backCapText = V5GameUi.Text(backCap, "Key", "Esc", 20f, V5Tokens.ButtonPrimaryLabel,
                TextAlignmentOptions.Center);
            Stretch(backCapText.GetComponent<RectTransform>(), 64f, 32f);
        }

        // --- タブ (メイン上部 438,0 824x48) ---

        private void BuildTabs()
        {
            var strip = At(V5Ui.Panel(frame.gameObject, "Tabs", new Vector2(TabsW, TabsH), Clear(), 0f),
                MainX + TabsX, MainY + TabsY);

            for (int i = 0; i < V6OptionLogic.TabCount; i++)
            {
                int index = i;
                bool selected = i == selectedTab;
                var tab = Button(strip, "Tab " + V6OptionLogic.TabLabels[i], TabW, TabsH, 8f,
                    selected ? V5Tokens.Key6 : V5Tokens.Key2, () => SelectTab(index));
                At(tab, (TabW + TabGap) * i, 0f);
                var label = V5GameUi.Text(tab, "Label", V6OptionLogic.TabLabels[i], 24f,
                    selected ? V5Tokens.Text : V5Tokens.TextMuted, TextAlignmentOptions.Center,
                    selected ? FontStyles.Bold : FontStyles.Normal);
                Stretch(label.GetComponent<RectTransform>(), TabW, TabsH);
            }
        }

        // --- 設定 (列2-3) (304,68) 512x688 ---

        private void BuildSettings()
        {
            var panel = At(V5Ui.Panel(frame.gameObject, "Settings", new Vector2(SetW, SetH), V5Tokens.Key2, 16f),
                MainX + SetX, MainY + SetY);

            var head = V5GameUi.Row(panel.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            At(head.GetComponent<RectTransform>(), 8f, 8f, SetRowW, 36f);
            V5GameUi.Text(head.gameObject, "Title", V6OptionLogic.TabLabels[selectedTab], 18f, V5Tokens.TextDim);

            float step = V6OptionLogic.RowStep(currentRows.Count);
            for (int i = 0; i < currentRows.Count; i++)
            {
                BuildSettingRow(panel, currentRows[i], i, SetRowY0 + step * i);
            }
        }

        private void BuildSettingRow(GameObject panel, Row row, int index, float y)
        {
            bool selected = index == selectedRow;
            var line = At(V5Ui.Panel(panel, "Set " + row.Label, new Vector2(SetRowW, SetRowH),
                selected ? V5Tokens.Key6 : V5Tokens.Key1, 8f), SetRowX, y);

            var rowButton = line.AddComponent<Button>();
            rowButton.transition = Selectable.Transition.None;
            var nav = rowButton.navigation;
            nav.mode = Navigation.Mode.None;
            rowButton.navigation = nav;
            rowButton.onClick.AddListener(() =>
            {
                selectedRow = index;
                Rebuild();
            });

            var label = V5GameUi.Text(line.gameObject, "Label", row.Label, 24f,
                row.Danger ? V5Tokens.Danger : V5Tokens.Text, TextAlignmentOptions.Left);
            At(label, 12f, 0f, 200f, SetRowH, new Vector2(0f, 0.5f));
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;

            var value = V5GameUi.Text(line.gameObject, "Value", row.Value(), 24f, V5Tokens.Text,
                TextAlignmentOptions.Right);
            At(value, 260f, 0f, 120f, SetRowH, new Vector2(0f, 0.5f));
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.overflowMode = TextOverflowModes.Ellipsis;

            if (row.IsAction)
            {
                return;
            }

            var prev = Button(line, "Prev", 40f, 40f, 8f, V5Tokens.Key4, () =>
            {
                selectedRow = index;
                AdjustSelected(-1);
            });
            At(prev, 392f, 8f);
            var prevIcon = V5GameUi.Text(prev, "Icon", "←", 24f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(prevIcon.GetComponent<RectTransform>(), 40f, 40f);

            var next = Button(line, "Next", 40f, 40f, 8f, V5Tokens.Key4, () =>
            {
                selectedRow = index;
                AdjustSelected(1);
            });
            At(next, 444f, 8f);
            var nextIcon = V5GameUi.Text(next, "Icon", "→", 24f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(nextIcon.GetComponent<RectTransform>(), 40f, 40f);
        }

        // --- プレビュー (列4-5) (864,68) 512x688 ---

        private void BuildPreview()
        {
            var panel = At(V5Ui.Panel(frame.gameObject, "Preview", new Vector2(PrevW, PrevH), V5Tokens.Key2, 16f),
                MainX + PrevX, MainY + PrevY);

            var head = V5GameUi.Row(panel.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            At(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 36f);
            V5GameUi.Text(head.gameObject, "Title", PreviewHeadTitle(), 18f, V5Tokens.TextDim);

            BuildGamePreview(panel);

            switch (selectedTab)
            {
                case 0:
                    BuildPlayPreviewExtra(panel);
                    break;
                case 1:
                    BuildDisplayPreviewExtra(panel);
                    break;
                case 2:
                    BuildSoundPreviewExtra(panel);
                    break;
                default:
                    BuildDataPreviewExtra(panel);
                    break;
            }
        }

        private string PreviewHeadTitle()
        {
            switch (selectedTab)
            {
                case 0:
                    // design: キー割当 (JIS / 標準)。実際の配列状態を出す。
                    return "キー割当 (" + (KeyConfigUI.IsCustomLayout() ? "カスタム" : "JIS / 標準") + ")";
                case 1:
                    return "判定表示";
                case 2:
                    return "音量";
                default:
                    return "設定";
            }
        }

        /// <summary>8 レーンの遠近法プレビュー (design 共通: 464x280 / 判定線 y240 / レーン番号)。</summary>
        private void BuildGamePreview(GameObject panel)
        {
            var well = At(V5Ui.Panel(panel, "Game Preview", new Vector2(GamePrevW, GamePrevH), V5Tokens.Key0, 8f),
                GamePrevX, GamePrevY);

            // レーン中心線: 下端 (判定線 y240) で 58px 間隔、上端 (y0) で約 7px 間隔
            // (design プレイフィールドの 150 → 18 px の縮約)。
            const float judgeY = 240f;
            const float bottomSpacing = 58f;
            const float topSpacingRatio = 7f / 58f;
            float centerX = GamePrevW * 0.5f;
            for (int i = 0; i < 8; i++)
            {
                float bottomX = centerX + (i - 3.5f) * bottomSpacing;
                float topX = centerX + (bottomX - centerX) * topSpacingRatio;
                DrawLine(well, "Lane " + i, bottomX, judgeY, topX, 0f, 1.5f, FromHex("#FFFFFF24"));
            }

            // サンプルノート (design の Note 0..5)。プレビュー装飾 (実データではない)。
            float[,] notes =
            {
                { 62f, 70f, 33f }, { 205f, 130f, 21f }, { 273f, 96f, 27f },
                { 291f, 186f, 14f }, { 204f, 210f, 14f }, { 327f, 58f, 35f },
            };
            for (int i = 0; i < notes.GetLength(0); i++)
            {
                var note = Rect(well, "Note " + i, notes[i, 2], 10f, FromHex("#EDEDED"));
                At(note, notes[i, 0], notes[i, 1]);
            }

            var judgeLine = Rect(well, "Judge Line", GamePrevW, 3f, FromHex("#EDEDED"));
            At(judgeLine, 0f, judgeY);

            for (int i = 0; i < 8; i++)
            {
                var laneLabel = V5GameUi.Text(well.gameObject, "Lane Label " + i, (i + 1).ToString(), 18f,
                    V5Tokens.TextMuted, TextAlignmentOptions.Center);
                At(laneLabel, 6f + 58f * i, 246f, 40f, 22f);
            }
        }

        /// <summary>Play タブ: JIS キーボード + 実際のレーン割当 + 説明。</summary>
        private void BuildPlayPreviewExtra(GameObject panel)
        {
            KeyCode[][] table = KeyBindings.Current;
            for (int r = 0; r < V6OptionLogic.JisGridKeys.Length; r++)
            {
                KeyCode[] keys = V6OptionLogic.JisGridKeys[r];
                float y = V6OptionLogic.GridRowY[r];
                float x0 = V6OptionLogic.GridRowX[r];
                for (int k = 0; k < keys.Length; k++)
                {
                    var cap = At(V5Ui.Panel(panel, "Key " + V6OptionLogic.KeyDisplayName(keys[k]),
                        new Vector2(V6OptionLogic.GridKeyW, V6OptionLogic.GridKeyH), V5Tokens.Key4, 4f),
                        x0 + V6OptionLogic.GridKeyStepX * k, y);
                    int lane = V6OptionLogic.DisplayLaneOf(keys[k], table);
                    var laneText = V5GameUi.Text(cap, "Lane", lane > 0 ? lane.ToString() : "", 20f,
                        V5Tokens.Text, TextAlignmentOptions.Center);
                    Stretch(laneText.GetComponent<RectTransform>(), V6OptionLogic.GridKeyW, V6OptionLogic.GridKeyH);
                }
            }

            BuildDesc(panel, 544f, SelectedRowDesc());
        }

        /// <summary>Display タブ: 判定チップ (PERFECT / GREAT / BAD / MISS) + 説明。</summary>
        private void BuildDisplayPreviewExtra(GameObject panel)
        {
            string[] labels = { "PERFECT", "GREAT", "BAD", "MISS" };
            Color[] markers = { V5Tokens.Text, V5Tokens.Ok, V5Tokens.Warn, V5Tokens.Danger };
            for (int i = 0; i < labels.Length; i++)
            {
                var chip = At(V5Ui.Panel(panel, "Judge " + labels[i], new Vector2(106f, 64f), V5Tokens.Key1, 8f),
                    24f + 118f * i, 360f);
                var marker = Rect(chip, "Marker", 32f, 4f, markers[i]);
                At(marker, (106f - 32f) * 0.5f, 14f);
                var text = V5GameUi.Text(chip, "Label", labels[i], 20f, V5Tokens.TextDim, TextAlignmentOptions.Center);
                At(text, 0f, 30f, 106f, 24f);
            }

            BuildDesc(panel, 456f, SelectedRowDesc());
        }

        /// <summary>Sound タブ: Main Volume の値とゲージ + 説明。</summary>
        private void BuildSoundPreviewExtra(GameObject panel)
        {
            var value = V5GameUi.Text(panel.gameObject, "Volume Value", V6OptionLogic.FormatVolume(GManager.mainVolume),
                40f, V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            At(value, 24f, 360f, 464f, 48f);
            value.textWrappingMode = TextWrappingModes.NoWrap;

            var track = At(V5Ui.Panel(panel, "Track", new Vector2(464f, 20f), V5Tokens.Key1, 4f), 24f, 420f);
            var fill = Rect(track, "Fill", Mathf.Max(2f, 464f * Mathf.Clamp01(GManager.mainVolume)), 20f,
                V5Tokens.Key6);
            At(fill, 0f, 0f);

            BuildDesc(panel, 472f, SelectedRowDesc());
        }

        /// <summary>Data タブ: 説明のみ (design はプレビューに Game Preview + 説明)。</summary>
        private void BuildDataPreviewExtra(GameObject panel)
        {
            BuildDesc(panel, 380f, SelectedRowDesc());
        }

        /// <summary>Divider + Desc Title + Desc (design の各タブ実測 y)。</summary>
        private void BuildDesc(GameObject panel, float dividerY, Row row)
        {
            var divider = Rect(panel, "Divider", 464f, 1f, FromHex("#FFFFFF1F"));
            At(divider, 24f, dividerY);

            var title = V5GameUi.Text(panel.gameObject, "Desc Title",
                row != null ? row.DescTitle : "", 32f, V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Normal);
            At(title, 24f, dividerY + 20f, 464f, 40f);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;

            var desc = V5GameUi.Text(panel.gameObject, "Desc", row != null ? row.Desc : "", 24f,
                V5Tokens.Text, TextAlignmentOptions.TopLeft);
            At(desc, 24f, dividerY + 70f, 464f, 100f);
            desc.lineSpacing = 40f; // design lineHeight 1.4 (TMP はフォントサイズ比の追加率)
        }

        private Row SelectedRowDesc()
        {
            if (currentRows.Count == 0)
            {
                return null;
            }

            return currentRows[selectedRow];
        }

        // --- モーダル (Clear save data の確認。design のモーダルゾーン 444,231 840x510) ---

        private void BuildConfirmClearModal()
        {
            var scrim = At(V5Ui.Panel(frame.gameObject, "Confirm Scrim", new Vector2(FrameW, FrameH),
                FromHex("#00000099"), 0f), 0f, 0f);
            var scrimButton = scrim.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;
            var nav = scrimButton.navigation;
            nav.mode = Navigation.Mode.None;
            scrimButton.navigation = nav;
            scrimButton.onClick.AddListener(() =>
            {
                confirmClearOpen = false;
                Rebuild();
            });

            var dialog = At(V5Ui.Panel(scrim, "Dialog", new Vector2(840f, 510f), V5Tokens.Key1, 16f), 444f, 231f);

            var title = V5GameUi.Text(dialog.gameObject, "Head", "Clear save data", 30f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            At(title, 28f, 28f, 784f, 40f);
            title.textWrappingMode = TextWrappingModes.NoWrap;

            var desc = V5GameUi.Text(dialog.gameObject, "Desc",
                "ハイスコアとキー設定を消去します。取り消せません。", 24f, V5Tokens.Text, TextAlignmentOptions.TopLeft);
            At(desc, 28f, 100f, 784f, 120f);
            desc.lineSpacing = 40f;

            var cancel = Button(dialog, "Op Cancel", 198f, 50f, 8f, V5Tokens.Key4, () =>
            {
                confirmClearOpen = false;
                Rebuild();
            });
            At(cancel, 28f, 432f);
            var cancelText = V5GameUi.Text(cancel, "Label", "キャンセル", 26f, V5Tokens.Text, TextAlignmentOptions.Center);
            Stretch(cancelText.GetComponent<RectTransform>(), 198f, 50f);

            var clear = Button(dialog, "Op Clear", 252f, 50f, 8f, V5Tokens.Danger, ClearSaveData);
            At(clear, 784f - 252f + 28f, 432f);
            var clearText = V5GameUi.Text(clear, "Label", "消去する", 26f, V5Tokens.ButtonPrimaryLabel,
                TextAlignmentOptions.Center);
            Stretch(clearText.GetComponent<RectTransform>(), 252f, 50f);
        }

        // --- 行定義 (実データに結線) ---

        private List<Row> RowsForTab(int tab)
        {
            switch (tab)
            {
                case 0: return BuildPlayRows();
                case 1: return BuildDisplayRows();
                case 2: return BuildSoundRows();
                default: return BuildDataRows();
            }
        }

        private List<Row> BuildPlayRows()
        {
            return new List<Row>
            {
                new Row("NoteSpeed",
                    () => V6OptionLogic.FormatSpeed(GManager.noteSpeed),
                    AdjustNoteSpeed,
                    null,
                    "NoteSpeed",
                    "ノーツが出現してから判定線に届くまでの時間。数値が大きいほど速く落ちます。"),
                new Row("NoteTiming",
                    () => GManager.noteTiming.ToString(),
                    AdjustNoteTiming,
                    null,
                    "NoteTiming",
                    "判定タイミングのずれを調整します。"),
                new Row("Key Layout",
                    () => KeyConfigUI.IsCustomLayout() ? "カスタム" : "標準",
                    d =>
                    {
                        if (d > 0)
                        {
                            OpenKeyConfig(); // カスタムはキー設定で編集する
                        }
                        else
                        {
                            KeyBindings.ResetToDefaults();
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
                    "キーの割り当てを編集します。F2 でも開けます。",
                    isAction: true),
            };
        }

        private List<Row> BuildDisplayRows()
        {
            return new List<Row>
            {
                new Row("Flash",
                    () => GManager.Flash ? "On" : "Off",
                    d =>
                    {
                        if (d != 0)
                        {
                            GManager.Flash = !GManager.Flash;
                            OptionSettings.Save();
                        }
                    },
                    () =>
                    {
                        GManager.Flash = !GManager.Flash;
                        OptionSettings.Save();
                    },
                    "Flash",
                    "ノーツ判定時の背景フラッシュの On/Off。"),
                new Row("FlashTiming",
                    () => GManager.FlashT.ToString(),
                    d =>
                    {
                        GManager.FlashT += d;
                        OptionSettings.Save();
                    },
                    null,
                    "FlashTiming",
                    "フラッシュのタイミングを調整します。"),
                new Row("FlashSpeed",
                    () => GManager.FlashBG.ToString(),
                    d =>
                    {
                        GManager.FlashBG = V6OptionLogic.CycleFlashSpeed(GManager.FlashBG, d);
                        OptionSettings.Save();
                    },
                    null,
                    "FlashSpeed",
                    "フラッシュの速さ。1 が最も速く、8 が最も遅い。"),
                new Row("Key Guide",
                    () => PlaySession.KeyGuideLabel(PlaySession.KeyGuide),
                    d => PlaySession.SetKeyGuide(PlaySession.NextKeyGuide(PlaySession.KeyGuide, d)),
                    () => PlaySession.SetKeyGuide(PlaySession.NextKeyGuide(PlaySession.KeyGuide, 1)),
                    "Key Guide",
                    "プレイ中の下部キーガイドの表示形式。"),
                new Row("Lyrics",
                    () => PlaySession.LyricsLabel(PlaySession.LyricsEnabled),
                    d =>
                    {
                        if (d != 0)
                        {
                            PlaySession.SetLyricsEnabled(!PlaySession.LyricsEnabled);
                        }
                    },
                    () => PlaySession.SetLyricsEnabled(!PlaySession.LyricsEnabled),
                    "Lyrics",
                    "プレイ中の歌詞表示の On/Off。"),
            };
        }

        private List<Row> BuildSoundRows()
        {
            return new List<Row>
            {
                new Row("Main Volume",
                    () => V6OptionLogic.FormatVolume(GManager.mainVolume),
                    d => AdjustVolume(d, true),
                    null,
                    "Main Volume",
                    "全体の音量。"),
                new Row("Effect Volume",
                    () => V6OptionLogic.FormatVolume(GManager.effectVolume),
                    d => AdjustVolume(d, false),
                    null,
                    "Effect Volume",
                    "効果音の音量。"),
                new Row("BGM Volume",
                    () => V6OptionLogic.FormatVolume(GManager.BGMVolume),
                    d => AdjustBgmVolume(d),
                    null,
                    "BGM Volume",
                    "BGM の音量。"),
            };
        }

        private List<Row> BuildDataRows()
        {
            return new List<Row>
            {
                new Row("Media cache",
                    () => Gameplay.MediaCacheManager.FormatSize(Gameplay.MediaCacheManager.GetCacheSizeBytes()),
                    null,
                    ClearMediaCache,
                    "Media cache",
                    "ダウンロードした楽曲映像のキャッシュ。消すと次回プレイ時に再ダウンロードします。",
                    danger: true,
                    isAction: true),
                new Row("Clear save data",
                    () => "",
                    null,
                    () =>
                    {
                        confirmClearOpen = true;
                        Rebuild();
                    },
                    "Clear save data",
                    "ハイスコアとキー設定を消去します。取り消せません。",
                    danger: true,
                    isAction: true),
            };
        }

        // --- データ操作 (既存 API 経由) ---

        /// <summary>NoteSpeed。既存の OptionSceneManager.SpeedUp/Down (clamp + 保存) を優先する。</summary>
        private void AdjustNoteSpeed(int delta)
        {
            var mgr = OptionSceneManager.Instance;
            if (mgr != null)
            {
                if (delta > 0) { mgr.SpeedUp(); }
                else if (delta < 0) { mgr.SpeedDown(); }
                return;
            }

            GManager.noteSpeed = Mathf.Clamp(GManager.noteSpeed + delta * 0.1f,
                OptionSettings.NoteSpeedMin, OptionSettings.NoteSpeedMax);
            OptionSettings.Save();
        }

        /// <summary>
        /// NoteTiming。既存の TimingUp/Down (全曲の offset JSON への書き戻しを含む) を優先する。
        /// </summary>
        private void AdjustNoteTiming(int delta)
        {
            if (delta > 0)
            {
                OptionSceneManager.TimingUp();
                return;
            }

            var mgr = OptionSceneManager.Instance;
            if (mgr != null)
            {
                mgr.TimingDown();
                return;
            }

            GManager.noteTiming = Mathf.Clamp(GManager.noteTiming - 1,
                OptionSettings.NoteTimingMin, OptionSettings.NoteTimingMax);
            OptionSettings.Save();
        }

        /// <summary>音量。即時反映 (ApplyVolumes) + 保存 (V5 OptionView と同じ結線)。</summary>
        private void AdjustVolume(int delta, bool main)
        {
            if (main)
            {
                GManager.mainVolume = V6OptionLogic.AdjustVolume(GManager.mainVolume, delta);
                AudioListener.volume = GManager.mainVolume;
            }
            else
            {
                GManager.effectVolume = V6OptionLogic.AdjustVolume(GManager.effectVolume, delta);
            }

            OptionSceneManager.ApplyVolumes();
            OptionSettings.Save();
        }

        private void AdjustBgmVolume(int delta)
        {
            GManager.BGMVolume = V6OptionLogic.AdjustVolume(GManager.BGMVolume, delta);
            OptionSceneManager.ApplyVolumes();
            OptionSettings.Save();
        }

        private void ClearMediaCache()
        {
            Gameplay.MediaCacheManager.ClearAll();
            Rebuild();
        }

        /// <summary>ハイスコアとキー設定を消去する (V5 OptionView.ClearSaveData と同じ範囲)。</summary>
        private void ClearSaveData()
        {
            PlayerPrefs.DeleteKey("Hiscore");
            PlayerPrefs.DeleteKey(FlashBeat.UI.V5.OptionView.KeyBindingsPrefsKey);
            PlayerPrefs.Save();
            Array.Clear(GManager.Hiscore, 0, GManager.Hiscore.Length);
            KeyBindings.ResetToDefaults();
        }

        private void OpenKeyConfig()
        {
            KeyConfigUI.Open();
        }

        // --- uGUI ユーティリティ (design px 左上原点) ---

        /// <summary>素の Image (角丸無し)。細線 / 矩形用。</summary>
        private static GameObject Rect(GameObject parent, string name, float w, float h, Color color)
        {
            var go = V5Ui.Node(parent, name);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return go;
        }

        /// <summary>(x1,y1) → (x2,y2) へ細い線を引く (design px 左上原点)。</summary>
        private static void DrawLine(GameObject parent, string name, float x1, float y1, float x2, float y2,
            float width, Color color)
        {
            // 左上原点 → uGUI の下上原点へ変換 (Game Preview の高さ 280 基準)。
            float lx1 = x1, ly1 = GamePrevH - y1;
            float lx2 = x2, ly2 = GamePrevH - y2;
            float dx = lx2 - lx1;
            float dy = ly2 - ly1;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            var line = Rect(parent, name, width, length, color);
            var rt = line.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2((lx1 + lx2) * 0.5f, (ly1 + ly2) * 0.5f);
            rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg - 90f);
        }

        private static GameObject At(GameObject go, float x, float y, float w, float h)
        {
            return At(go.GetComponent<RectTransform>(), x, y, w, h);
        }

        private static GameObject At(GameObject go, float x, float y)
        {
            return At(go.GetComponent<RectTransform>(), x, y);
        }

        private static GameObject At(GameObject go, float x, float y, float w, float h, Vector2 pivot)
        {
            return At(go.GetComponent<RectTransform>(), x, y, w, h, pivot);
        }

        private static GameObject At(Component node, float x, float y, float w, float h)
        {
            var rt = node.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return node.gameObject;
        }

        private static GameObject At(Component node, float x, float y)
        {
            var rt = node.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            return node.gameObject;
        }

        private static GameObject At(Component node, float x, float y, float w, float h, Vector2 pivot)
        {
            At(node, x, y, w, h);
            var rt = node.GetComponent<RectTransform>();
            rt.pivot = pivot;
            // pivot 変更で位置がずれるので左上 (x, -y) を維持する。
            rt.anchoredPosition = new Vector2(x, -y);
            return node.gameObject;
        }

        private static void Stretch(RectTransform rt, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
        }

        private static GameObject Button(GameObject parent, string name, float w, float h, float radius, Color bg,
            Action onClick)
        {
            var go = V5Ui.Panel(parent, name, new Vector2(w, h), bg, radius);
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            return go;
        }

        private static Color Clear()
        {
            return new Color(0f, 0f, 0f, 0f);
        }

        private static Color FromHex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
