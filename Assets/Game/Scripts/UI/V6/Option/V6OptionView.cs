using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Option
{
    /// <summary>
    /// Option 画面の v6 UI。共通部品 (<see cref="V6Frame"/> / <see cref="V6Controls"/> /
    /// <see cref="V6Layout"/>) だけで design v6 の 06 Play (B6If6Z) / 06 Display (EzLfb) /
    /// 06 Sound (z7joSy) / 06 Data (DQHHp) を実行時に構築する (共通フレーム 1728x972 / 4 タブ)。
    ///
    /// - design 空間 (1728×972、左上原点・y_down) の座標を 1:1 (scale 1) で描き、
    ///   <see cref="V6OptionLogic.FrameOrigin"/> の origin (design gaI3p "Resolution Check ·
    ///   Option": FHD (96,54) / 2K (416,234) / Mac (416,346)) で画面中央へ配置する。
    /// - 実データ: GManager (noteSpeed / noteTiming / Flash / FlashT / FlashBG / 音量) +
    ///   OptionSettings (保存) + KeyBindings / KeyConfigUI (キー配列) + PlaySession
    ///   (Key Guide / Lyrics) + MediaCacheManager (Media cache)。サンプル値は書かない。
    /// - 設定の書き戻しは既存 API 経由 (OptionSceneManager の SpeedUp/Down・TimingUp/Down、
    ///   GManager 直接 + OptionSettings.Save())。新しい保存経路は作らない。
    /// - design にあるが実データの無い行は表示しない (固定値のダミーは作らない)。
    /// - フッターは design パッチ (QQ4jM / FChYu / F8SwvS) の通りキー凡例のみ
    ///   (ボタン無し)。決定は Enter / 行クリック、戻るは Esc / ヘッダー戻るボタン。
    /// - キー: ↑↓ 行選択 / ←→ 値変更 / Tab タブ切替 / Enter・Space 決定 / Esc 戻る /
    ///   F2 キー設定。マウス: タブ / 行 / ステッパー / 戻る。
    /// - TitleSceneManager は呼ばない。onBack は統合担当が渡す。
    /// </summary>
    public sealed class V6OptionView
    {
        /// <summary>現在生成されている v6 Option 画面 (シーンに 1 個だけ)。</summary>
        public static V6OptionView Current { get; private set; }

        // design px 定数は V6OptionLogic (design B6If6Z パッチ実測) に集約。
        // 以下は view 内の略記 (値の単一ソースは V6OptionLogic)。

        private const float FrameW = 1728f;
        private const float FrameH = 972f;

        private const float TabsX = V6OptionLogic.TabsX, TabsY = V6OptionLogic.TabsY;
        private const float TabsW = V6OptionLogic.TabsW, TabsH = V6OptionLogic.TabsH;
        private const float TabW = V6OptionLogic.TabW, TabGap = V6OptionLogic.TabGap;

        private const float SetX = V6OptionLogic.SetX, SetY = V6OptionLogic.SetY;
        private const float SetW = V6OptionLogic.SetW, SetH = V6OptionLogic.SetH;
        private const float SetRowX = V6OptionLogic.SetRowX, SetRowW = V6OptionLogic.SetRowW;
        private const float SetRowH = V6OptionLogic.SettingsRowH, SetRowY0 = V6OptionLogic.SettingsRowFirstY;

        private const float PrevX = V6OptionLogic.PrevX, PrevY = V6OptionLogic.PrevY;
        private const float PrevW = V6OptionLogic.PrevW, PrevH = V6OptionLogic.PrevH;
        private const float GamePrevX = V6OptionLogic.GamePrevX, GamePrevY = V6OptionLogic.GamePrevY;
        private const float GamePrevW = V6OptionLogic.GamePrevW, GamePrevH = V6OptionLogic.GamePrevH;

        /// <summary>契約 §3 danger トークン (#FF6B6B)。V6Tokens に未定義のため design 値を直接持つ。</summary>
        private static readonly Color DangerLabel = FromHex("#FF6B6B");

        private readonly Action onBack;

        private GameObject rootGo;
        private RectTransform frame;
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

            PlaceFrame();

            bool up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            bool down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            bool left = Input.GetKeyDown(KeyCode.LeftArrow);
            bool right = Input.GetKeyDown(KeyCode.RightArrow);
            bool confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space);
            bool cancel = Input.GetKeyDown(KeyCode.Escape);
            bool tab = Input.GetKeyDown(KeyCode.Tab);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var action = V6OptionLogic.MapKey(new V6OptionInput
            {
                ModalOpen = confirmClearOpen,
                Up = up,
                Down = down,
                Left = left,
                Right = right,
                Confirm = confirm,
                Cancel = cancel,
                TabNext = tab && !shift,
                TabPrev = tab && shift,
            });

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
            rootGo = new GameObject("V6 Option View", typeof(RectTransform));
            rootGo.transform.SetParent(parent.transform, false);
            var rt = rootGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            // 画面全体に v6 の背景色 (契約 §3 bg = #1B1B1B) を敷く。
            // design (gaI3p Resolution Check) は bg-anim3 (ゲームのアニメーション背景) の上に
            // フレームを載せるが、OptionScene にはその背景が無いため代わりの下敷き。
            // (V6Tokens.Bg の doc「全面に敷かない」は bg-anim3 がある画面の規約。)
            var bg = new GameObject("V6 Option Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(rootGo.transform, false);
            var bgImage = bg.GetComponent<Image>();
            bgImage.color = V6Tokens.Bg;
            bgImage.raycastTarget = false;
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // 共通フレーム (契約 §4): Header/Main/Footer ゾーン + モーダル層 + 外周 stroke。
            frame = V6Frame.Create(rootGo.transform, "V6 Option Frame 1728x972").GetComponent<RectTransform>();

            var driver = rootGo.AddComponent<V6OptionViewDriver>();
            driver.View = this;
            PlaceFrame();
        }

        /// <summary>
        /// フレームを 1:1 (等倍) で中央配置する (引き伸ばしはしない)。
        /// origin = ((w−1728)/2, (h−972)/2) 切り捨て (V6OptionLogic.FrameOrigin、
        /// design gaI3p "Resolution Check · Option" の実配置)。
        /// </summary>
        private void PlaceFrame()
        {
            int w = Screen.width;
            int h = Screen.height;
            if (w == lastWidth && h == lastHeight)
            {
                return;
            }

            lastWidth = w;
            lastHeight = h;
            if (frame != null)
            {
                Common.V6Layout.Place(frame, V6OptionLogic.FrameOrigin(w, h),
                    new Vector2(FrameW, FrameH));
            }
        }

        private void Rebuild()
        {
            if (rootGo == null)
            {
                return;
            }

            PlaceFrame();

            // 共通フレームのゾーン (Header/Main/Footer) とモーダル Dialog の中身だけ作り直す。
            // ゾーン自体 / 外周 stroke / モーダル層は V6Frame.Create が所有する
            // (ゾーン名は V6Frame.Create の契約: Header / Main / Footer / Modal > Dialog)。
            ClearChildren(Zone("Main"));
            ClearChildren(Zone("Header"));
            ClearChildren(Zone("Footer"));
            var modal = V6Frame.GetModal(frame.gameObject);
            if (modal != null)
            {
                var dialog = modal.transform.Find("Dialog");
                if (dialog != null)
                {
                    ClearChildren(dialog);
                }
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
                BuildConfirmClearModal(modal);
            }
            else
            {
                V6Frame.HideModal(frame.gameObject);
            }
        }

        private Transform Zone(string zoneName)
        {
            var zone = frame.transform.Find(zoneName);
            if (zone == null)
            {
                throw new InvalidOperationException("V6 frame zone missing: " + zoneName);
            }

            return zone;
        }

        private static void ClearChildren(Transform parent)
        {
            var children = new List<GameObject>();
            foreach (Transform child in parent)
            {
                children.Add(child.gameObject);
            }

            for (int i = 0; i < children.Count; i++)
            {
                V5GameUi.DestroyNow(children[i]);
            }
        }

        // --- ヘッダー (契約 §4 + design B6If6Z パッチ: (24,0) 1680x96) ---
        // Title はパッチ ci867 で「OPTION」に差し替え。右上情報ゾーン (840,0) は空
        // (パッチ sBFcr で子なしに差し替え)。戻るは base zUcsi/zCQBY (0,20) 56×56
        // #3A3A3A r8 + arrow-left 24 + Key "Esc" fs16。

        private void BuildHeader()
        {
            var header = Zone("Header");

            var back = V6Controls.Panel(header.gameObject, "Back button",
                new Vector2(0f, 20f), new Vector2(56f, 56f), V6Tokens.Key4, V6Tokens.RadiusMedium);
            var backButton = back.AddComponent<Button>();
            backButton.transition = Selectable.Transition.None;
            var nav = backButton.navigation;
            nav.mode = Navigation.Mode.None;
            backButton.navigation = nav;
            backButton.onClick.AddListener(() =>
            {
                if (onBack != null) onBack();
            });

            var backCol = V6Layout.Column(back, "Inner", new Vector2(0f, 7f), new Vector2(56f, 42f), 2f);
            var backIcon = V6Icons.Create(backCol, "arrow-left", 24f, V6Tokens.TextMuted, "Icon");
            var backKey = V6Controls.Text(backCol, "Key", "Esc", Vector2.zero, new Vector2(56f, 16f),
                16f, V6Tokens.TextMuted, TextAlignmentOptions.Center);
            var icons = new[] { backIcon, (Component)backKey };
            V6Layout.StackVertical(backCol.GetComponent<RectTransform>(), 2f, icons);

            V6Controls.Text(header.gameObject, "Title", "OPTION",
                new Vector2(72f, 20f), new Vector2(400f, 56f), 38f, V6Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
        }

        // --- フッター (契約 §4 + design B6If6Z パッチ: (24,876) 1680x96) ---
        // キー凡例はパッチ QQ4jM → r4IJx (0,30) gap24: ↑↓選択 / ←→変更 / Tabタブ切替 /
        // Esc戻る / Enter決定 (KeyCap h36 r4 padding[0,8] fs22 + Label fs22, 項内 gap8)。
        // 補助操作 / 主操作はパッチ FChYu / F8SwvS で「Op · なし」(空フレーム) に
        // 差し替え済み — Option にフッターボタンは無い (決定は Enter / 行クリック)。

        private void BuildFooter()
        {
            var footer = Zone("Footer");

            var legend = V6Layout.Row(footer.gameObject, "Zone Key Ops",
                new Vector2(0f, 30f), new Vector2(840f, 36f), 24f);
            var items = new Component[]
            {
                LegendItem(legend, "↑↓", "選択").transform,
                LegendItem(legend, "←→", "変更").transform,
                LegendItem(legend, "Tab", "タブ切替").transform,
                LegendItem(legend, "Esc", "戻る").transform,
                LegendItem(legend, "Enter", "決定").transform,
            };
            V6Layout.StackHorizontal(legend.GetComponent<RectTransform>(), 24f, items);
        }

        /// <summary>凡例 1 項目 (KeyCap + Label, gap8 — design パッチの L0..L4 構造)。</summary>
        private static GameObject LegendItem(GameObject parent, string key, string label)
        {
            float keyW = Mathf.Ceil(V6Ui.LabelAdvanceEm(key) * V6Controls.KeyCapFontSize)
                + 8f * 2f; // design パッチの KeyCap padding [0,8]
            var item = V6Layout.Row(parent, "Legend " + label, Vector2.zero,
                new Vector2(keyW + 8f + 60f, 36f), 8f);
            var cap = V6Controls.KeyCap(item, key, new Vector2(0f, 0f), new Vector2(keyW, V6Tokens.KeyCapHeight));
            V6Controls.Text(item, "Label", label,
                new Vector2(keyW + 8f, 0f), new Vector2(60f, 36f), 22f, V6Tokens.TextMuted,
                TextAlignmentOptions.Left);
            return item;
        }

        // --- タブ (design C0piZz: メイン (438,0) 824x48, タブ 200x48 gap8) ---
        // 選択タブ: fill $k6 / Label fs24 w500 $text。非選択: $k2 / fs24 $text-muted。

        private void BuildTabs()
        {
            var main = Zone("Main");
            var strip = V6Layout.Row(main.gameObject, "Zone Tabs",
                new Vector2(TabsX, TabsY), new Vector2(TabsW, TabsH), TabGap);

            var tabs = new Component[V6OptionLogic.TabCount];
            for (int i = 0; i < V6OptionLogic.TabCount; i++)
            {
                int index = i;
                bool selected = i == selectedTab;
                var tab = V6Controls.Panel(strip, "Tab " + V6OptionLogic.TabLabels[i],
                    new Vector2((TabW + TabGap) * i, 0f), new Vector2(TabW, TabsH),
                    selected ? V6Tokens.Key6 : V6Tokens.Key2, V6Tokens.RadiusMedium);
                var button = tab.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                var nav = button.navigation;
                nav.mode = Navigation.Mode.None;
                button.navigation = nav;
                button.onClick.AddListener(() => SelectTab(index));
                var label = V6Controls.Text(tab, "Label", V6OptionLogic.TabLabels[i],
                    new Vector2(0f, 0f), new Vector2(TabW, TabsH), 24f,
                    selected ? V6Tokens.Text : V6Tokens.TextMuted, TextAlignmentOptions.Center,
                    selected ? FontStyles.Bold : FontStyles.Normal);
                var labelRt = label.rectTransform;
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
                tabs[i] = tab.transform;
            }

            V6Layout.StackHorizontal(strip.GetComponent<RectTransform>(), TabGap, tabs);
        }

        // --- 設定 (design crVgG: メイン (304,68) 512x688 $k2 r16) ---
        // Head (8,8) 496x36 (Title fs18 $text-dim)。行 (8, 60+step*k) 496x56 r8:
        // 背景 $k1 (選択行 / アクション行 キー設定 は $k6)。Label w200 fs24、
        // Value w120 右寄せ fs24、Prev/Next 40x40 $k4 r8 (icon 24 $text-muted)。
        // 行の内側 padding [0,12] gap12 → Prev x=392 / Next x=444 / Value 右端 x=380。

        private void BuildSettings()
        {
            var main = Zone("Main");
            var panel = V6Controls.Panel(main.gameObject, "Settings",
                new Vector2(SetX, SetY), new Vector2(SetW, SetH), V6Tokens.Key2, V6Tokens.RadiusLarge);

            var head = V6Layout.Row(panel, "Head", new Vector2(8f, 8f), new Vector2(SetRowW, 36f), 12f);
            V6Controls.Text(head, "Title", V6OptionLogic.TabLabels[selectedTab],
                new Vector2(12f, 0f), new Vector2(200f, 36f), 18f, V6Tokens.TextDim);

            float step = V6OptionLogic.RowStep(currentRows.Count);
            for (int i = 0; i < currentRows.Count; i++)
            {
                BuildSettingRow(panel, currentRows[i], i, SetRowY0 + step * i);
            }
        }

        private void BuildSettingRow(GameObject panel, Row row, int index, float y)
        {
            bool selected = index == selectedRow;
            var line = V6Controls.Panel(panel, "Set " + row.Label, new Vector2(SetRowX, y),
                new Vector2(SetRowW, SetRowH), selected ? V6Tokens.Key6 : V6Tokens.Key1, V6Tokens.RadiusMedium);

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

            var label = V6Controls.Text(line, "Label", row.Label, new Vector2(12f, 0f),
                new Vector2(200f, SetRowH), 24f, row.Danger ? DangerLabel : V6Tokens.Text);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;

            var value = V6Controls.Text(line, "Value", row.Value(), new Vector2(260f, 0f),
                new Vector2(120f, SetRowH), 24f, V6Tokens.Text, TextAlignmentOptions.Right);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.overflowMode = TextOverflowModes.Ellipsis;

            if (row.IsAction)
            {
                return;
            }

            var prev = V6Controls.IconButton(line, "chevron-left", new Vector2(392f, 8f),
                new Vector2(40f, 40f), V6Tokens.Key4, V6Tokens.RadiusMedium, 24f, V6Tokens.TextMuted);
            prev.GetComponent<Button>().onClick.AddListener(() =>
            {
                selectedRow = index;
                AdjustSelected(-1);
            });

            var next = V6Controls.IconButton(line, "chevron-right", new Vector2(444f, 8f),
                new Vector2(40f, 40f), V6Tokens.Key4, V6Tokens.RadiusMedium, 24f, V6Tokens.TextMuted);
            next.GetComponent<Button>().onClick.AddListener(() =>
            {
                selectedRow = index;
                AdjustSelected(1);
            });
        }

        // --- プレビュー (design cqQMc: メイン (864,68) 512x688 $k2 r16) ---
        // 4 タブ共通: Head (8,8) / Game Preview (24,60) 464x280 $k0 r8 /
        // Divider (24,544) 464x1 #FFFFFF1F / Desc Title (24,564) fs32 w500 /
        // Desc (24,614) w464 fs24 lineHeight1.4。Play のみキー一覧 (8,356〜) を追加。

        private void BuildPreview()
        {
            var main = Zone("Main");
            var panel = V6Controls.Panel(main.gameObject, "Preview",
                new Vector2(PrevX, PrevY), new Vector2(PrevW, PrevH), V6Tokens.Key2, V6Tokens.RadiusLarge);

            var head = V6Layout.Row(panel, "Head", new Vector2(8f, 8f), new Vector2(496f, 36f), 12f);
            V6Controls.Text(head, "Title", PreviewHeadTitle(),
                new Vector2(12f, 0f), new Vector2(400f, 36f), 18f, V6Tokens.TextDim);

            BuildGamePreview(panel);

            if (selectedTab == 0)
            {
                BuildKeyGrid(panel);
            }

            BuildDesc(panel, V6OptionLogic.DescDividerY, SelectedRowDesc());
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
            var well = V6Controls.Panel(panel, "Game Preview", new Vector2(GamePrevX, GamePrevY),
                new Vector2(GamePrevW, GamePrevH), V6Tokens.Key0, V6Tokens.RadiusMedium);

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
                V6Layout.Place(note.GetComponent<RectTransform>(), new Vector2(notes[i, 0], notes[i, 1]),
                    new Vector2(notes[i, 2], 10f));
            }

            var judgeLine = Rect(well, "Judge Line", GamePrevW, 3f, FromHex("#EDEDED"));
            V6Layout.Place(judgeLine.GetComponent<RectTransform>(), new Vector2(0f, judgeY),
                new Vector2(GamePrevW, 3f));

            for (int i = 0; i < 8; i++)
            {
                V6Controls.Text(well, "Lane Label " + i, (i + 1).ToString(),
                    new Vector2(6f + 58f * i, 246f), new Vector2(40f, 22f), 18f, V6Tokens.TextMuted,
                    TextAlignmentOptions.Center);
            }
        }

        /// <summary>Play タブ: JIS キーボード + 実際のレーン割当 (design の Key 1..Key _)。</summary>
        private void BuildKeyGrid(GameObject panel)
        {
            KeyCode[][] table = KeyBindings.Current;
            for (int r = 0; r < V6OptionLogic.JisGridKeys.Length; r++)
            {
                KeyCode[] keys = V6OptionLogic.JisGridKeys[r];
                float y = V6OptionLogic.GridRowY[r];
                float x0 = V6OptionLogic.GridRowX[r];
                for (int k = 0; k < keys.Length; k++)
                {
                    var cap = V6Controls.Panel(panel, "Key " + V6OptionLogic.KeyDisplayName(keys[k]),
                        new Vector2(x0 + V6OptionLogic.GridKeyStepX * k, y),
                        new Vector2(V6OptionLogic.GridKeyW, V6OptionLogic.GridKeyH),
                        V6Tokens.Key4, V6Tokens.RadiusSmall);
                    int lane = V6OptionLogic.DisplayLaneOf(keys[k], table);
                    var laneText = V6Controls.Text(cap, "Lane", lane > 0 ? lane.ToString() : "",
                        new Vector2(0f, 9f), new Vector2(V6OptionLogic.GridKeyW, 20f), 20f, V6Tokens.Text,
                        TextAlignmentOptions.Center);
                    var laneRt = laneText.rectTransform;
                    laneRt.anchorMin = new Vector2(0f, 1f);
                    laneRt.anchorMax = new Vector2(0f, 1f);
                    laneRt.pivot = new Vector2(0f, 1f);
                    laneRt.anchoredPosition = new Vector2(0f, -9f);
                }
            }
        }

        /// <summary>Divider + Desc Title + Desc (design 共通: Divider y544 / Title y564 / Desc y614)。</summary>
        private void BuildDesc(GameObject panel, float dividerY, Row row)
        {
            var divider = Rect(panel, "Divider", 464f, 1f, FromHex("#FFFFFF1F"));
            V6Layout.Place(divider.GetComponent<RectTransform>(), new Vector2(24f, dividerY), new Vector2(464f, 1f));

            var title = V6Controls.Text(panel, "Desc Title",
                row != null ? row.DescTitle : "", new Vector2(24f, dividerY + 20f), new Vector2(464f, 40f),
                32f, V6Tokens.Text, TextAlignmentOptions.Left); // design w500 = 既定フォント (Medium) のウェイト
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;

            var desc = V6Controls.Text(panel, "Desc", row != null ? row.Desc : "",
                new Vector2(24f, dividerY + 70f), new Vector2(V6OptionLogic.DescW, V6OptionLogic.DescH),
                24f, V6Tokens.Text, TextAlignmentOptions.TopLeft);
            desc.lineSpacing = 40f; // design lineHeight 1.4 (TMP lineSpacing は fs 比の追加率: 24×0.4=9.6px → 40%)
        }

        private Row SelectedRowDesc()
        {
            if (currentRows.Count == 0)
            {
                return null;
            }

            return currentRows[selectedRow];
        }

        // --- モーダル (Clear save data の確認。契約 §4 モーダルゾーン: Scrim 全面 +
        //     Dialog (444,231) 840x510 $k1 r16 — V6Frame.Create が層ごと所有) ---

        private void BuildConfirmClearModal(GameObject modal)
        {
            if (modal == null)
            {
                return;
            }

            var dialog = modal.transform.Find("Dialog")?.gameObject;
            if (dialog == null)
            {
                return;
            }

            V6Frame.ShowModal(frame.gameObject);

            var title = V6Controls.Text(dialog, "Head", "Clear save data",
                new Vector2(28f, 28f), new Vector2(784f, 40f), 30f, V6Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.NoWrap;

            var desc = V6Controls.Text(dialog, "Desc",
                "ハイスコアとキー設定を消去します。取り消せません。",
                new Vector2(28f, 100f), new Vector2(784f, 120f), 24f, V6Tokens.Text,
                TextAlignmentOptions.TopLeft);
            desc.lineSpacing = 40f; // design lineHeight 1.4

            var cancel = V6Controls.Panel(dialog, "Op Cancel", new Vector2(28f, 432f),
                new Vector2(198f, 50f), V6Tokens.Key4, V6Tokens.RadiusMedium);
            var cancelButton = cancel.AddComponent<Button>();
            cancelButton.transition = Selectable.Transition.None;
            var cancelNav = cancelButton.navigation;
            cancelNav.mode = Navigation.Mode.None;
            cancelButton.navigation = cancelNav;
            cancelButton.onClick.AddListener(() =>
            {
                confirmClearOpen = false;
                Rebuild();
            });
            V6Controls.Text(cancel, "Label", "キャンセル", Vector2.zero, new Vector2(198f, 50f),
                26f, V6Tokens.Text, TextAlignmentOptions.Center);

            var clear = V6Controls.Panel(dialog, "Op Clear", new Vector2(784f - 252f + 28f, 432f),
                new Vector2(252f, 50f), DangerLabel, V6Tokens.RadiusMedium);
            var clearButton = clear.AddComponent<Button>();
            clearButton.transition = Selectable.Transition.None;
            var clearNav = clearButton.navigation;
            clearNav.mode = Navigation.Mode.None;
            clearButton.navigation = clearNav;
            clearButton.onClick.AddListener(ClearSaveData);
            V6Controls.Text(clear, "Label", "消去する", Vector2.zero, new Vector2(252f, 50f),
                26f, V6Tokens.ButtonPrimaryLabel, TextAlignmentOptions.Center);
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

        /// <summary>素の Image (角丸無し)。細線 / 矩形用 (共通部品に 1px 線は無いため最小限のローカル helper)。</summary>
        private static GameObject Rect(GameObject parent, string name, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            var img = go.GetComponent<Image>();
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

        private static Color FromHex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
