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
    /// V6OptionView の uGUI 構築部分 (Issue #385 Phase 2.5 で分割)。
    /// ルート/背景/共通フレーム、1:1 配置 (PlaceFrame)、Rebuild の骨格保持差分更新、
    /// Header / Footer / タブ帯の生成と更新を持つ。partial class のため
    /// フィールド・メソッドは V6OptionView 本体と共有する。
    /// </summary>
    public sealed partial class V6OptionView
    {
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

            // キー設定モーダル (design vo4kB)。フレーム直下に置き、開くとき最前面へ出す。
            keyConfigModal = V6KeyConfigModal.Build(frame.gameObject);
            keyConfigModal.Closed += OnKeyConfigClosed;

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

            // 共通フレームのゾーン (Header/Main/Footer) のうち、状態が変わるのは
            // Main の中身 (タブ選択・行選択・値表示) だけ。Header/Footer は静的なので
            // 初回だけ作る (Issue #415: 全再構築をやめる)。
            bool tabChanged = scaffoldTab != selectedTab;
            if (!scaffoldBuilt)
            {
                // 初回: ゾーンごと作り直す (従来と同じ)。
                V6Hierarchy.ClearChildren(Zone("Main"));
                V6Hierarchy.ClearChildren(Zone("Header"));
                V6Hierarchy.ClearChildren(Zone("Footer"));
                ClearScaffoldRefs();
                RebuildCountForAudit++;
            }
            else
            {
                IncrementalUpdateCountForAudit++;
            }

            if (scaffoldBuilt)
            {
                UpdateHeader();
                UpdateTabs();
            }
            else
            {
                BuildHeader();
                BuildFooter();
                BuildTabs();
            }

            if (tabChanged)
            {
                RebuildMainPanels();
            }
            else if (scaffoldBuilt)
            {
                UpdateSettings();
                UpdatePreview();
            }
            else
            {
                RebuildMainPanels();
            }

            scaffoldBuilt = true;

            var modal = V6Frame.GetModal(frame.gameObject);
            if (modal != null)
            {
                var dialog = modal.transform.Find("Dialog");
                if (dialog != null)
                {
                    V6Hierarchy.ClearChildren(dialog);
                }
            }

            if (confirmClearOpen)
            {
                BuildConfirmClearModal(modal);
            }
            else
            {
                V6Frame.HideModal(frame.gameObject);
            }
        }

        /// <summary>
        /// Settings / Preview だけを作り直す (タブ切替 = 行定義が丸ごと変わるタイミング)。
        /// タブ帯 (Zone Tabs) は保持する。
        /// </summary>
        private void RebuildMainPanels()
        {
            currentRows = RowsForTab(selectedTab);
            if (selectedRow >= currentRows.Count)
            {
                selectedRow = 0;
            }

            // 子を作り直すため、旧オブジェクトへの参照は捨てる (Build* が再設定する)。
            volumeSlider = null;
            selectedRowValueText = null;
            volumeSliderRowIndex = -1;

            // Main のうち Tabs だけを残し、Settings / Preview だけを作り直す。
            V6Hierarchy.DetachAndDestroy(Zone("Main").Find("Settings")?.gameObject);
            V6Hierarchy.DetachAndDestroy(Zone("Main").Find("Preview")?.gameObject);
            rowObjects.Clear();
            rowBackgrounds.Clear();
            rowValueTexts.Clear();
            settingsHeadTitle = null;
            previewPanelGo = null;
            previewHeadTitle = null;
            descTitleText = null;
            descBodyText = null;

            BuildSettings();
            BuildPreview();
            scaffoldTab = selectedTab;
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

        // --- ヘッダー (契約 §4 + design B6If6Z パッチ: (24,0) 1680x96) ---
        // Title はパッチ ci867 で「OPTION」に差し替え。右上情報ゾーン (840,0) は空
        // (パッチ sBFcr で子なしに差し替え)。戻るは base zUcsi/zCQBY (0,20) 56×56
        // #3A3A3A r8 + arrow-left 24 + Key "Esc" fs16。

        /// <summary>ヘッダーは静的なので、中身が無いときだけ作る (Issue #415)。</summary>
        private void UpdateHeader()
        {
            if (Zone("Header").childCount == 0)
            {
                BuildHeader();
            }
        }

        private void BuildHeader()
        {
            var header = Zone("Header");

            // 戻るボタン + タイトルは共通部品 (Issue #385)。
            // 戻る: 56×56 / $k4 r8 / arrow-left 24 + Key fs16 (design zUcsi/zCQBY)。
            V6Header.BackButton(header.gameObject, "Back button", "Esc",
                () =>
                {
                    if (onBack != null) onBack();
                });

            V6Header.Title(header.gameObject, "OPTION");
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

        /// <summary>
        /// Issue #415: タブ帯は骨格 (4 タブ) を保持し、選択状態の色・ラベルだけを
        /// 既存 Image/TMP に適用する。参照が壊れているときだけ作り直す。
        /// </summary>
        private void UpdateTabs()
        {
            if (tabBackgrounds.Count != V6OptionLogic.TabCount)
            {
                // 参照が壊れている (タブ帯が消えた) ときだけ作り直す。
                V6Hierarchy.DetachAndDestroy(Zone("Main").Find("Zone Tabs")?.gameObject);
                BuildTabs();
                return;
            }

            for (int i = 0; i < V6OptionLogic.TabCount; i++)
            {
                bool selected = i == selectedTab;
                if (tabBackgrounds[i] != null)
                {
                    tabBackgrounds[i].color = selected ? V6Tokens.Key6 : V6Tokens.Key2;
                }

                if (i < tabLabels.Count && tabLabels[i] != null)
                {
                    tabLabels[i].color = selected ? V6Tokens.Text : V6Tokens.TextMuted;
                    tabLabels[i].fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }

        private void BuildTabs()
        {
            tabObjects.Clear();
            tabBackgrounds.Clear();
            tabLabels.Clear();
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
                V6ButtonPressFeedback.Attach(tab);
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
                tabObjects.Add(tab);
                tabBackgrounds.Add(tab.GetComponent<Image>());
                tabLabels.Add(label);
            }

            V6Layout.StackHorizontal(strip.GetComponent<RectTransform>(), TabGap, tabs);
        }
    }
}
