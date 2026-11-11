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

        /// <summary>確認モーダルの種類 (issue-162 C-6: Key Layout のリセットも確認する)。</summary>
        private enum ConfirmKind { ClearSaveData, ResetKeyLayout }

        private ConfirmKind confirmKind = ConfirmKind.ClearSaveData;

        /// <summary>Clear save data の実行結果 (消去完了の文言。空なら未実行)。</summary>
        private string clearResult = "";

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
            // オーバーレイが消費しない F1 だけはここで受け取り、編集のキャンセルにする:
            // 確定 = Esc / F2 で閉じる (= 保存)、キャンセル = F1 で保存値へ戻して閉じる。
            if (KeyConfigUI.IsOpen)
            {
                if (Input.GetKeyDown(KeyCode.F1))
                {
                    CancelKeyConfigEdit();
                }

                return;
            }

            if (!FlashBeat.UI.SceneInputGate.InputAllowed)
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
                        CancelClearConfirm();
                    }
                    else if (onBack != null)
                    {
                        onBack();
                    }
                    return;
                case V6OptionAction.Confirm:
                    if (confirmClearOpen)
                    {
                        if (confirmKind == ConfirmKind.ClearSaveData)
                        {
                            ConfirmClearSaveData();
                        }
                        else
                        {
                            ConfirmResetKeyLayout();
                        }
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

            V6Controls.FillBackButton(back, "Esc");

            V6Controls.Text(header.gameObject, "Title", "OPTION",
                new Vector2(72f, 20f), new Vector2(400f, 56f), 38f, V6Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
        }

        // --- フッター (契約 §4 + design B6If6Z パッチ: (24,876) 1680x96) ---
        // キー凡例はパッチ r4IJx: フッター (0,30) 高さ 32 / 項目間 gap24、
        // 項内は KeyCap (h32 r4 padding[0,8] #3A3A3A, Key fs22 $text-muted) + Label
        // (fs22 $text-muted) を gap8 で並べる。design 実測の項幅は
        // ↑↓選択 114 / ←→変更 114 / Tabタブ切替 151 / Esc戻る 104 / Enter決定 124。
        // Label の幅は概算 0.62em ではなく TMP 実測で詰める (「タブ切替」= 4 全角で
        // 89px。固定 60px だと次の項へはみ出し、凡例が「タブ切替 Esc」と繋がって見えた)。
        // 補助操作 / 主操作はパッチ FChYu / F8SwvS で「Op · なし」(空フレーム) に
        // 差し替え済み — Option にフッターボタンは無い (決定は Enter / 行クリック)。

        private void BuildFooter()
        {
            var footer = Zone("Footer");
            float x = 0f;
            x = V6Legend.Item(footer.gameObject, x, V6Legend.Y, "↑↓", "選択");
            x = V6Legend.Item(footer.gameObject, x, V6Legend.Y, "←→", "変更");
            x = V6Legend.Item(footer.gameObject, x, V6Legend.Y, "Tab", "タブ切替");
            x = V6Legend.Item(footer.gameObject, x, V6Legend.Y, "Esc", "戻る");
            V6Legend.Item(footer.gameObject, x, V6Legend.Y, "Enter", "決定");
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

                // ステッパー (◀ ▶) を持たない行 (キー設定 / Media cache / Clear save data) は
                // クリック＝実行にする。値を変える行は ◀ ▶ ボタンで変えられるので、
                // どの行もマウスだけで操作できるようにするため (Acceptance: マウスとキーボードの両方)。
                if (row.IsAction && row.Activate != null)
                {
                    row.Activate();
                }

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
                    return "キー割当 (" + (IsDefaultKeyLayout() ? "JIS / 標準" : "カスタム") + ")";
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
            // design のレーン番号は (6 + 58i, 246) 40x26 の中心、つまり判定線上の中心は
            // 26 + 58i (中心 229)。464 幅の中央 232 から -3 ずれるが design 実測に合わせる。
            const float judgeY = 240f;
            const float bottomSpacing = 58f;
            const float bottomCenterX = 26f;
            const float topSpacingRatio = 7f / 58f;
            for (int i = 0; i < 8; i++)
            {
                float bottomX = bottomCenterX + bottomSpacing * i;
                float hub = GamePrevW * 0.5f;
                float topX = hub + (bottomX - hub) * topSpacingRatio;
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
                // 番号はレーン中心線の真下 (design: (6 + 58i, 246) 40x26 fs18)。
                float bottomX = bottomCenterX + bottomSpacing * i;
                V6Controls.Text(well, "Lane Label " + i, (i + 1).ToString(),
                    new Vector2(bottomX - 20f, 246f), new Vector2(40f, 26f), 18f, V6Tokens.TextMuted,
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
                    V6Controls.Text(cap, "Lane", lane > 0 ? lane.ToString() : "",
                        new Vector2(0f, 9f), new Vector2(V6OptionLogic.GridKeyW, 20f), 20f, V6Tokens.Text,
                        TextAlignmentOptions.Center);
                }
            }
        }

        /// <summary>Divider + Desc Title + Desc (design 共通: Divider y544 / Title y564 h46 / Desc y614 w464 h68)。</summary>
        private void BuildDesc(GameObject panel, float dividerY, Row row)
        {
            var divider = Rect(panel, "Divider", 464f, 1f, FromHex("#FFFFFF1F"));
            V6Layout.Place(divider.GetComponent<RectTransform>(), new Vector2(24f, dividerY), new Vector2(464f, 1f));

            // draw shape は「Title y564 h46」— 高さ 40 だと NotoSansJP の行送り (fs32 で約 46px) を
            // 下回り、TMP の Ellipsis が 1 行も描画しない (実行時に Title が丸ごと消えた原因)。
            // design と同じ h46 にし、Ellipsis は付けない (はみ出す場合も design は自動幅なので
            // 切り落とすより描く。値は短い固定文言のみ)。
            var title = V6Controls.Text(panel, "Desc Title",
                row != null ? row.DescTitle : "", new Vector2(24f, V6OptionLogic.DescTitleY),
                new Vector2(464f, 46f), 32f, V6Tokens.Text, TextAlignmentOptions.Left);

            // Desc は design 上で複数行に折り返す段落 (w464 h68 lineHeight1.4)。
            // NoWrap のままだと 1 行で右パネルの外まで伸びて切れていた。
            var desc = V6Controls.Text(panel, "Desc", row != null ? row.Desc : "",
                new Vector2(24f, V6OptionLogic.DescY), new Vector2(V6OptionLogic.DescW, V6OptionLogic.DescH),
                24f, V6Tokens.Text, TextAlignmentOptions.TopLeft, FontStyles.Normal,
                TextWrappingModes.Normal);
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

        // --- モーダル (確認。契約 §4 モーダルゾーン: Scrim 全面 +
        //     Dialog (444,231) 840x510 $k1 r16 — V6Frame.Create が層ごと所有) ---

        /// <summary>確認モーダルの文言 (種類ごと)。</summary>
        private void ConfirmTexts(out string title, out string desc, out string confirmLabel)
        {
            if (confirmKind == ConfirmKind.ResetKeyLayout)
            {
                title = "Key Layout";
                desc = "すべてのレーンのキー割り当てを標準に戻します。\n現在のカスタム割り当ては消去されます (元に戻せません)。";
                confirmLabel = "戻す";
                return;
            }

            title = "Clear save data";
            desc = "ハイスコアとキー設定 (レーン・メニュー操作) を消去します。\n実行後は元に戻せません。";
            confirmLabel = "消去する";
        }

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

            string titleText, descText, confirmLabelText;
            ConfirmTexts(out titleText, out descText, out confirmLabelText);

            var title = V6Controls.Text(dialog, "Head", titleText,
                new Vector2(28f, 28f), new Vector2(784f, 40f), 30f, V6Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.NoWrap;

            var desc = V6Controls.Text(dialog, "Desc", descText,
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
            cancelButton.onClick.AddListener(CancelClearConfirm);
            V6Controls.Text(cancel, "Label", "キャンセル", Vector2.zero, new Vector2(198f, 50f),
                26f, V6Tokens.Text, TextAlignmentOptions.Center);

            var clear = V6Controls.Panel(dialog, "Op Clear", new Vector2(784f - 252f + 28f, 432f),
                new Vector2(252f, 50f), DangerLabel, V6Tokens.RadiusMedium);
            var clearButton = clear.AddComponent<Button>();
            clearButton.transition = Selectable.Transition.None;
            var clearNav = clearButton.navigation;
            clearNav.mode = Navigation.Mode.None;
            clearButton.navigation = clearNav;
            clearButton.onClick.AddListener(confirmKind == ConfirmKind.ResetKeyLayout
                ? (UnityEngine.Events.UnityAction)ConfirmResetKeyLayout
                : (UnityEngine.Events.UnityAction)ConfirmClearSaveData);
            V6Controls.Text(clear, "Label", confirmLabelText, Vector2.zero, new Vector2(252f, 50f),
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
                    "判定タイミングのずれを調整します。変更は全曲の譜面設定 (offset) に書き込まれます。"),
                new Row("Key Layout",
                    () => IsDefaultKeyLayout() ? "標準" : "カスタム",
                    d =>
                    {
                        if (d > 0)
                        {
                            OpenKeyConfig(); // カスタムはキー設定で編集する
                        }
                        else if (!IsDefaultKeyLayout())
                        {
                            OpenResetKeyLayoutConfirm(); // 破壊的なリセットは確認してから (issue-162 C-6)
                        }
                    },
                    OpenKeyConfig,
                    "Key Layout",
                    "キー配列を選びます。標準は既定の配列、カスタムはキー設定で編集します。← で標準に戻します (確認ダイアログが出ます)。"),
                new Row("キー設定",
                    () => "F2",
                    null,
                    OpenKeyConfig,
                    "キー設定",
                    "キーの割り当てを編集します (F2)。Esc / F2 で確定 (保存)、F1 でキャンセル (元に戻して閉じます)。",
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
                            ToggleFlash();
                        }
                    },
                    ToggleFlash,
                    "Flash",
                    "ノーツ判定時の背景フラッシュの On/Off。"),
                new Row("FlashTiming",
                    () => GManager.FlashT.ToString(),
                    AdjustFlashTiming,
                    null,
                    "FlashTiming",
                    "フラッシュのタイミングを調整します。"),
                new Row("FlashSpeed",
                    () => GManager.FlashBG.ToString(),
                    AdjustFlashSpeed,
                    null,
                    "FlashSpeed",
                    "フラッシュの速さ。1 が最も速く、8 が最も遅い。"),
                new Row("Key Guide",
                    () => PlaySession.KeyGuideLabel(PlaySession.KeyGuide),
                    SetKeyGuide,
                    () => SetKeyGuide(1),
                    "Key Guide",
                    "プレイ中の下部キーガイドの表示形式。"),
                new Row("Lyrics",
                    () => PlaySession.LyricsLabel(PlaySession.LyricsEnabled),
                    d =>
                    {
                        if (d != 0)
                        {
                            ToggleLyrics();
                        }
                    },
                    ToggleLyrics,
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
                    d => AdjustMainVolume(d),
                    null,
                    "Main Volume",
                    "全体の音量。"),
                new Row("Effect Volume",
                    () => V6OptionLogic.FormatVolume(GManager.effectVolume),
                    d => AdjustEffectVolume(d),
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
                    () => MediaCacheSizeLabel(),
                    null,
                    ClearMediaCache,
                    "Media cache",
                    "ダウンロードした楽曲映像のキャッシュ。消すと次回プレイ時に再ダウンロードします。",
                    danger: true,
                    isAction: true),
                new Row("Clear save data",
                    () => clearResult,
                    null,
                    OpenClearConfirm,
                    "Clear save data",
                    "ハイスコアとキー設定 (レーン・メニュー操作) を消去します。確認してから実行されます。",
                    danger: true,
                    isAction: true),
            };
        }

        // --- データ操作 (既存 API へ委譲するだけ) ---
        //
        // ここで新しい保存経路は作らない。表示値・実動作・保存先は既存の
        // OptionSettings (PlayerPrefs) / ScorePersistence (安定キーのベストスコア) /
        // KeyBindings・KeyActions (キー設定) / PlaySession (セッション設定) /
        // MediaCacheManager (キャッシュ) がそのまま担う。
        // public static にしてあるので、EditMode テストから同じ経路を呼べる。

        /// <summary>NoteSpeed。既存の OptionSceneManager.AdjustNoteSpeed (clamp + 保存) へ委譲。</summary>
        public static void AdjustNoteSpeed(int delta)
        {
            OptionSceneManager.AdjustNoteSpeed(delta);
        }

        /// <summary>NoteTiming。既存の OptionSceneManager.AdjustNoteTiming (保存 + offset 書込) へ委譲。</summary>
        public static void AdjustNoteTiming(int delta)
        {
            OptionSceneManager.AdjustNoteTiming(delta);
        }

        /// <summary>Main Volume。即時反映 (ApplyVolumes) + 保存。</summary>
        public static void AdjustMainVolume(int delta)
        {
            GManager.mainVolume = V6OptionLogic.AdjustVolume(GManager.mainVolume, delta);
            OptionSceneManager.ApplyVolumes();
            OptionSettings.Save();
        }

        /// <summary>Effect Volume。即時反映 (ApplyVolumes) + 保存。</summary>
        public static void AdjustEffectVolume(int delta)
        {
            GManager.effectVolume = V6OptionLogic.AdjustVolume(GManager.effectVolume, delta);
            OptionSceneManager.ApplyVolumes();
            OptionSettings.Save();
        }

        /// <summary>BGM Volume。即時反映 (ApplyVolumes) + 保存。</summary>
        public static void AdjustBgmVolume(int delta)
        {
            GManager.BGMVolume = V6OptionLogic.AdjustVolume(GManager.BGMVolume, delta);
            OptionSceneManager.ApplyVolumes();
            OptionSettings.Save();
        }

        /// <summary>背景フラッシュの On/Off を切り替えて保存する。</summary>
        public static void ToggleFlash()
        {
            GManager.Flash = !GManager.Flash;
            OptionSettings.Save();
        }

        /// <summary>フラッシュのタイミング調整。負の値 (計算が成立しない) は 0 へ寄せる。</summary>
        public static void AdjustFlashTiming(int delta)
        {
            GManager.FlashT = Mathf.Max(0, GManager.FlashT + delta);
            OptionSettings.Save();
        }

        /// <summary>フラッシュの速さ (1 → 2 → 4 → 8 → 1)。</summary>
        public static void AdjustFlashSpeed(int delta)
        {
            GManager.FlashBG = V6OptionLogic.CycleFlashSpeed(GManager.FlashBG, delta);
            OptionSettings.Save();
        }

        /// <summary>キーガイドの表示形式 (セッション設定。保存はしない)。</summary>
        public static void SetKeyGuide(int delta)
        {
            PlaySession.SetKeyGuide(PlaySession.NextKeyGuide(PlaySession.KeyGuide, delta));
        }

        /// <summary>歌詞の表示 On/Off (セッション設定。保存はしない)。</summary>
        public static void ToggleLyrics()
        {
            PlaySession.SetLyricsEnabled(!PlaySession.LyricsEnabled);
        }

        /// <summary>レーンのキー割り当てを既定へ戻す (保存は KeyBindings が行う)。</summary>
        public static void ResetKeyLayout()
        {
            KeyBindings.ResetToDefaults();
        }

        /// <summary>キャッシュ容量の表示 (既存の MediaCacheManager の実測値)。</summary>
        public static string MediaCacheSizeLabel()
        {
            return Gameplay.MediaCacheManager.FormatSize(Gameplay.MediaCacheManager.GetCacheSizeBytes());
        }

        /// <summary>ダウンロード済みキャッシュを消す (既存の MediaCacheManager)。</summary>
        public static void ClearMediaCache()
        {
            Gameplay.MediaCacheManager.ClearAll();
        }

        /// <summary>
        /// ハイスコアとキー設定 (レーン・メニュー操作) を消去する。
        ///
        /// 消去対象は案内している既存データだけ:
        /// - ベストスコア (実体は安定キーの ScorePersistence / FlashBeat.ScoreStore)。
        ///   旧 PlayerPrefs の Hiscore だけ消すと、安定した保存先から記録が復活する
        ///   ので、既存の ScorePersistence.ResetAll() でまとめて消す。
        /// - キー設定 (レーン = KeyBindings / メニュー操作 = KeyActions)。
        ///
        /// 既存の API (ScorePersistence.ResetAll / KeyBindings.ResetToDefaults /
        /// KeyActions.ResetToDefaults) をそのまま使うので、新しい保存経路は増えない。
        /// </summary>
        public static void ClearSaveData()
        {
            FlashBeat.Persistence.ScorePersistence.ResetAll();

            if (GManager.Hiscore != null)
            {
                Array.Clear(GManager.Hiscore, 0, GManager.Hiscore.Length);
            }

            KeyBindings.ResetToDefaults();
            KeyActions.ResetToDefaults();
        }

        /// <summary>キー割当が既定と同じか (正本は KeyBindings.DefaultKeys)。</summary>
        public static bool IsDefaultKeyLayout()
        {
            return KeyConfigUI.IsDefaultLayout(KeyBindings.Current, KeyBindings.DefaultKeys);
        }

        // --- Clear save data の確認モーダル (マウスとキーボードで同じ結果にする) ---

        /// <summary>確認モーダルを開く (まだ何も消去しない)。</summary>
        private void OpenClearConfirm()
        {
            confirmKind = ConfirmKind.ClearSaveData;
            confirmClearOpen = true;
        }

        /// <summary>Key Layout のリセット確認を開く (まだ何も戻さない)。</summary>
        private void OpenResetKeyLayoutConfirm()
        {
            confirmKind = ConfirmKind.ResetKeyLayout;
            confirmClearOpen = true;
            Rebuild();
        }

        /// <summary>確認モーダルを閉じるだけ (何も変えない)。</summary>
        private void CancelClearConfirm()
        {
            confirmClearOpen = false;
            Rebuild();
        }

        /// <summary>消去を実行し、結果をモーダルを閉じて表示する (マウス・キーボードで共通)。</summary>
        private void ConfirmClearSaveData()
        {
            ClearSaveData();
            clearResult = "消去しました";
            confirmClearOpen = false;
            Rebuild();
        }

        /// <summary>レーン割当を標準へ戻し、モーダルを閉じる (マウス・キーボードで共通)。</summary>
        private void ConfirmResetKeyLayout()
        {
            ResetKeyLayout();
            confirmClearOpen = false;
            Rebuild();
        }

        private void OpenKeyConfig()
        {
            KeyConfigUI.Open();
        }

        /// <summary>
        /// キー設定 (KeyConfigUI) の編集をキャンセルして閉じる (Option の F1)。
        ///
        /// 保存済みの状態 (KeyBindings / KeyActions の保存先) へ戻してから閉じる。
        /// 閉じるときの保存は戻した内容を書き直すだけなので、保存値は変わらない
        /// (元の割当に戻る)。確定は従来どおり Esc / F2 で閉じる (= 保存)。
        ///
        /// 「開いた時点の状態 == 保存先」は既存の保存経路が保証する
        /// (KeyConfigUI の閉じる保存 / ResetToDefaults の保存 / ClearSaveData)。
        /// 未保存のまま残ることは無いので、Load() で開いた時点へ正確に戻れる。
        /// </summary>
        public static void CancelKeyConfigEdit()
        {
            KeyBindings.Load();
            KeyActions.Load();
            KeyConfigUI.Close();
        }

        // --- uGUI ユーティリティ (design px 左上原点) ---

        /// <summary>素の Image (角丸無し)。細線 / 矩形用 (共通部品に 1px 線は無いため最小限のローカル helper)。</summary>
        private static GameObject Rect(GameObject parent, string name, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            // サイズを必ず反映する。既定 (100×100) のまま回転すると、細線のつもりの
            // レーン線が 100px 幅の帯になり、プレビューが灰色のコーン状に潰れる。
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
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
