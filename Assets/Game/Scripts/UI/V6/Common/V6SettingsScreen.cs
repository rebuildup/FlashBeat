using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Game.Hud;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// 設定画面の共有スキャフォールド (Issue #733 フォローアップ)。
    /// Option / Pause の設定画面が「同じ画面」を出すための単一実装:
    ///
    /// - タブ帯 ("Zone Tabs"、4 タブ 200x48 gap8、選択 $k6 / 非選択 $k2)
    /// - 設定パネル ("Settings" 512x688: Head + V6SettingRowView の行)
    /// - プレビューパネル ("Preview" 512x688: Head / Game Preview well /
    ///   タブ別の右パネル要素 / Divider / Desc Title / スクロール付き Desc)
    ///
    /// タブ別の右パネル要素も共通:
    /// - Play: JIS キーボードのキー一覧 (キー名表示)
    /// - Display: 判定色の凡例 (V6HudLogic.VerdictColor と同じ割り当て)
    /// - Sound: 選択行の音量へ結線した V6VolumeSlider
    /// - Data: なし
    ///
    /// 説明文は選択行の <see cref="V6SettingRow.DescTitle"/> / <see cref="V6SettingRow.Desc"/>
    /// を出す (長い文は Desc Viewport の ScrollRect で縦スクロール)。
    ///
    /// 選択・値変更の規則も共通: 行選択 / タブ切替は端で止める (clamp)。
    /// ←→ は音量行なら <see cref="IV6SettingsHost.Volumes"/> を、
    /// それ以外は行の Adjust を呼ぶ。Enter / 行クリックは行の Activate
    /// (アクション行はクリック＝決定)。
    ///
    /// ホスト側が持つのは画面固有の部分だけ: ヘッダー / フッター /
    /// 開閉と状態管理 (PauseGate・再開カウントダウン等) / Data タブの行。
    /// </summary>
    public sealed class V6SettingsScreen
    {
        private readonly GameObject main;
        private readonly IV6SettingsHost host;
        private readonly string[] tabLabels;
        private readonly Func<int, List<V6SettingRow>> rowsForTab;

        private bool built;
        /// <summary>Settings/Preview を作ったときのタブ (-1 = 未構築)。</summary>
        private int scaffoldTab = -1;

        // --- タブ帯 (構築後は選択状態の色・ラベルだけを更新する) ---
        private readonly List<Image> tabBackgrounds = new List<Image>();
        private readonly List<TextMeshProUGUI> tabLabelTexts = new List<TextMeshProUGUI>();

        // --- 設定パネル ---
        private List<V6SettingRow> currentRows = new List<V6SettingRow>();
        private readonly List<V6SettingRowView> rowViews = new List<V6SettingRowView>();
        private TextMeshProUGUI settingsHeadTitle;

        // --- プレビューパネル ---
        private GameObject previewPanelGo;
        private TextMeshProUGUI previewHeadTitle;
        private TextMeshProUGUI descTitleText;
        private TextMeshProUGUI descBodyText;
        private RectTransform descScrollContent;
        private ScrollRect descScrollRect;
        /// <summary>最後にフィットさせた説明文。本文が変わったときだけ先頭へ戻す。</summary>
        private string fittedDescText;
        private V6VolumeSlider volumeSlider;
        /// <summary>スライダーが結線されている VolumeIndex (-1 = 未構築)。</summary>
        private int volumeSliderRowIndex = -1;

        /// <summary>ゲームプレビューの共有ホスト (最初の構築で生成し、破棄まで保持)。</summary>
        private V6GamePreview gamePreview;

        public V6SettingsScreen(GameObject mainParent, IV6SettingsHost settingsHost,
            string[] labels, Func<int, List<V6SettingRow>> rowsForTabFunc)
        {
            if (mainParent == null)
            {
                throw new ArgumentNullException(nameof(mainParent));
            }

            main = mainParent;
            host = settingsHost;
            tabLabels = labels ?? throw new ArgumentNullException(nameof(labels));
            rowsForTab = rowsForTabFunc ?? throw new ArgumentNullException(nameof(rowsForTabFunc));
        }

        /// <summary>選択中のタブ (0..tabLabels.Length-1)。</summary>
        public int SelectedTab { get; private set; }

        /// <summary>選択中の行 (選択タブ内)。</summary>
        public int SelectedRow { get; private set; }

        /// <summary>選択タブの行定義 (監査 API 用。UI は作らない)。</summary>
        public List<V6SettingRow> RowsForTab(int tab)
        {
            return rowsForTab(tab);
        }

        /// <summary>選択タブの行定義。</summary>
        public List<V6SettingRow> CurrentRows
        {
            get { return currentRows; }
        }

        /// <summary>ゲームプレビュー (未構築なら null)。Tick / SetRenderingEnabled / Dispose はホストが呼ぶ。</summary>
        public V6GamePreview GamePreview
        {
            get { return gamePreview; }
        }

        /// <summary>検証ハーネス用: 音量スライダ (Sound タブ以外では null)。</summary>
        public V6VolumeSlider VolumeSliderForAudit
        {
            get { return volumeSlider; }
        }

        /// <summary>ビューが持つプレビューを破棄する (ルート自体の破棄はホスト側)。</summary>
        public void Dispose()
        {
            if (gamePreview != null)
            {
                gamePreview.Dispose();
                gamePreview = null;
            }
        }

        // --- 構築と更新 ---

        /// <summary>
        /// 画面を最新の実データへ合わせる。初回はタブ帯・設定・プレビューを構築し、
        /// 以降はタブ切替時だけ Settings/Preview を作り直し、それ以外は
        /// 値・選択色・説明文だけを差分更新する (Issue #415 の骨格保持と同じ規律)。
        /// ポーズ画面のように毎フレーム呼んでも再構築は起きない。
        /// </summary>
        public void Refresh()
        {
            if (!built)
            {
                BuildTabs();
                RebuildMainPanels();
                built = true;
                return;
            }

            UpdateTabs();
            if (scaffoldTab != SelectedTab)
            {
                RebuildMainPanels();
            }
            else
            {
                UpdateSettings();
                UpdatePreview();
            }
        }

        /// <summary>タブを直接指定で切替 (マウスクリックからも)。端は止める。</summary>
        public void SelectTab(int index)
        {
            SelectedTab = ClampIndex(index, tabLabels.Length);
            SelectedRow = 0;
            Refresh();
        }

        /// <summary>Tab キー相当のタブ移動 (端で止める。両画面で同じ規則)。</summary>
        public void SelectNextTab(int delta)
        {
            SelectTab(SelectedTab + delta);
        }

        /// <summary>
        /// 行選択を移動する (↑↓)。端は巡回する — Option の WrapIndex と同じ規則を
        /// Pause にも適用する (Issue #733 フォローアップで選択規則を統一)。
        /// </summary>
        public void MoveSelection(int delta)
        {
            if (currentRows.Count == 0)
            {
                return;
            }

            SelectedRow = WrapIndex(SelectedRow, currentRows.Count, delta);
            Refresh();
        }

        /// <summary>選択中の行を値変更する (←→ / ステッパー)。</summary>
        public void AdjustSelected(int delta, bool shift)
        {
            if (currentRows.Count == 0 || delta == 0)
            {
                return;
            }

            int index = SelectedRow;
            if (index < 0 || index >= currentRows.Count)
            {
                return;
            }

            V6SettingRow row = currentRows[index];
            if (row.VolumeIndex >= 0 && host != null)
            {
                // 音量行はスライダーと同じ値モデルを編集する (Issue #375 §3.2)。
                // Rebuild せずスライダーと値テキストだけを更新し、保存は
                // V6VolumeSettings 側でまとめる。
                host.Volumes.Nudge(row.VolumeIndex, delta, shift);
                UpdateVolumeVisuals();
                return;
            }

            if (row.Adjust != null)
            {
                row.Adjust(delta, shift);
            }

            Refresh();
        }

        /// <summary>選択中の行を決定する (Enter / Space。行の Activate を呼ぶ)。</summary>
        public void ActivateSelected()
        {
            if (currentRows.Count == 0)
            {
                return;
            }

            int index = SelectedRow;
            if (index < 0 || index >= currentRows.Count)
            {
                return;
            }

            V6SettingRow row = currentRows[index];
            if (row.Activate == null)
            {
                return;
            }

            row.Activate();
            Refresh();
        }

        /// <summary>行を選択する (監査 API・テスト用。現在タブのみ。範囲外は巡回で丸める)。</summary>
        public void SelectRow(int row)
        {
            SelectedRow = currentRows.Count == 0 ? 0 : WrapIndex(row, currentRows.Count, 0);
            Refresh();
        }

        /// <summary>選択中タブの行に実際に描画されている値テキスト。</summary>
        public string RenderedRowValue(int row)
        {
            if (row < 0 || row >= rowViews.Count || rowViews[row].ValueText == null)
            {
                return null;
            }

            return rowViews[row].ValueText.text;
        }

        private static int ClampIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return 0;
            }

            return index > count - 1 ? count - 1 : index;
        }

        /// <summary>行選択の巡回 (端で反対側へ回る。V6OptionLogic.WrapIndex と同じ規則)。</summary>
        private static int WrapIndex(int index, int count, int delta)
        {
            if (count <= 0)
            {
                return 0;
            }

            // index 自体が範囲外 (SelectRow(-4) 等) でも有効な添字へ正規化する
            // (PR #787 review: -1 が返り全行非選択になるのを防ぐ)。
            return ((index + delta) % count + count) % count;
        }

        // --- タブ帯 (design C0piZz: メイン (438,0) 824x48, タブ 200x48 gap8) ---
        // 選択タブ: fill $k6 / Label fs24 w500 $text。非選択: $k2 / fs24 $text-muted。

        private void UpdateTabs()
        {
            if (tabBackgrounds.Count != tabLabels.Length)
            {
                // 参照が壊れている (タブ帯が消えた) ときだけ作り直す。
                V6Hierarchy.DetachAndDestroy(main.transform.Find("Zone Tabs")?.gameObject);
                BuildTabs();
                return;
            }

            for (int i = 0; i < tabLabels.Length; i++)
            {
                bool selected = i == SelectedTab;
                if (tabBackgrounds[i] != null)
                {
                    tabBackgrounds[i].color = selected ? V6Tokens.Key6 : V6Tokens.Key2;
                }

                if (i < tabLabelTexts.Count && tabLabelTexts[i] != null)
                {
                    tabLabelTexts[i].color = selected ? V6Tokens.Text : V6Tokens.TextMuted;
                    tabLabelTexts[i].fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }

        private void BuildTabs()
        {
            tabBackgrounds.Clear();
            tabLabelTexts.Clear();
            var strip = V6Layout.Row(main, "Zone Tabs",
                new Vector2(V6SettingsLayout.TabsX, V6SettingsLayout.TabsY),
                new Vector2(V6SettingsLayout.TabsW, V6SettingsLayout.TabsH), V6SettingsLayout.TabGap);

            var tabs = new Component[tabLabels.Length];
            for (int i = 0; i < tabLabels.Length; i++)
            {
                int index = i;
                bool selected = i == SelectedTab;
                var tab = V6Controls.Panel(strip, "Tab " + tabLabels[i],
                    new Vector2((V6SettingsLayout.TabW + V6SettingsLayout.TabGap) * i, 0f),
                    new Vector2(V6SettingsLayout.TabW, V6SettingsLayout.TabsH),
                    selected ? V6Tokens.Key6 : V6Tokens.Key2, V6Tokens.RadiusMedium);
                var button = tab.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                var nav = button.navigation;
                nav.mode = Navigation.Mode.None;
                button.navigation = nav;
                V6ButtonPressFeedback.Attach(tab);
                button.onClick.AddListener(() => SelectTab(index));
                var label = V6Controls.Text(tab, "Label", tabLabels[i],
                    new Vector2(0f, 0f), new Vector2(V6SettingsLayout.TabW, V6SettingsLayout.TabsH), 24f,
                    selected ? V6Tokens.Text : V6Tokens.TextMuted, TextAlignmentOptions.Center,
                    selected ? FontStyles.Bold : FontStyles.Normal);
                var labelRt = label.rectTransform;
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
                tabs[i] = tab.transform;
                tabBackgrounds.Add(tab.GetComponent<Image>());
                tabLabelTexts.Add(label);
            }

            V6Layout.StackHorizontal(strip.GetComponent<RectTransform>(), V6SettingsLayout.TabGap, tabs);
        }

        // --- 設定パネル (design crVgG / uDNC0 共通: Main (304,68) 512x688 $k2 r16) ---
        // Head (8,8) 496x36 (Title fs18 $text-dim)。行 (8, 60+step*k) 496x56 r8。

        /// <summary>
        /// Settings / Preview だけを作り直す (タブ切替 = 行定義が丸ごと変わる
        /// タイミング)。タブ帯 (Zone Tabs) は保持する。
        /// </summary>
        private void RebuildMainPanels()
        {
            currentRows = rowsForTab(SelectedTab);
            if (SelectedRow >= currentRows.Count)
            {
                SelectedRow = 0;
            }

            // 子を作り直すため、旧オブジェクトへの参照は捨てる (Build* が再設定する)。
            volumeSlider = null;
            volumeSliderRowIndex = -1;

            V6Hierarchy.DetachAndDestroy(main.transform.Find("Settings")?.gameObject);
            V6Hierarchy.DetachAndDestroy(main.transform.Find("Preview")?.gameObject);
            rowViews.Clear();
            settingsHeadTitle = null;
            previewPanelGo = null;
            previewHeadTitle = null;
            descTitleText = null;
            descBodyText = null;
            descScrollContent = null;
            descScrollRect = null;
            fittedDescText = null;

            BuildSettings();
            BuildPreview();
            scaffoldTab = SelectedTab;
        }

        private void UpdateSettings()
        {
            if (rowViews.Count != currentRows.Count)
            {
                // 行数が合わない (骨格破損) ときは Settings/Preview ごと作り直す。
                RebuildMainPanels();
                return;
            }

            if (settingsHeadTitle != null)
            {
                settingsHeadTitle.text = tabLabels[SelectedTab];
            }

            for (int i = 0; i < currentRows.Count; i++)
            {
                rowViews[i].SetSelected(i == SelectedRow);
                rowViews[i].RefreshValue();
            }
        }

        private void BuildSettings()
        {
            rowViews.Clear();
            var panel = V6Controls.Panel(main, "Settings",
                new Vector2(V6SettingsLayout.SetX, V6SettingsLayout.SetY),
                new Vector2(V6SettingsLayout.SetW, V6SettingsLayout.SetH),
                V6Tokens.Key2, V6Tokens.RadiusLarge);

            var head = V6Layout.Row(panel, "Head", new Vector2(8f, 8f), new Vector2(V6SettingsLayout.SetRowW, 36f), 12f);
            settingsHeadTitle = V6Controls.Text(head, "Title", tabLabels[SelectedTab],
                new Vector2(12f, 0f), new Vector2(200f, 36f), 18f, V6Tokens.TextDim);

            float step = V6SettingsLayout.RowStep(currentRows.Count);
            for (int i = 0; i < currentRows.Count; i++)
            {
                rowViews.Add(BuildSettingRow(panel, currentRows[i], i,
                    V6SettingsLayout.SetRowX, V6SettingsLayout.SettingsRowFirstY + step * i));
            }
        }

        /// <summary>
        /// 設定行の組み立て (<see cref="V6SettingRowView"/> — Option / Pause 共通)。
        /// 行の中身は <see cref="V6SettingsCatalog"/> の定義。
        /// </summary>
        private V6SettingRowView BuildSettingRow(GameObject panel, V6SettingRow row, int index,
            float x, float y)
        {
            bool selected = index == SelectedRow;
            return V6SettingRowView.Build(panel, row, index,
                new Vector2(x, y), selected,
                (delta, shift) =>
                {
                    bool selectionChanged = SelectedRow != index;
                    SelectedRow = index;
                    AdjustSelected(delta, shift);
                    // 音量行の AdjustSelected はスライダーと値だけを更新して
                    // Refresh しない。別行のステッパーを押して選択が変わった
                    // ときは、選択色・説明文・スライダー結線を更新する
                    // (PR #787 review)。同じ行の調整では値更新だけで済む。
                    if (selectionChanged)
                    {
                        Refresh();
                    }
                },
                () =>
                {
                    SelectedRow = index;

                    // ステッパー (◀ ▶) を持たない行 (キー設定 / Media cache /
                    // Clear save data) はクリック＝実行にする。値を変える行は
                    // ◀ ▶ ボタンで変えられるので、どの行もマウスだけで操作できる
                    // ようにするため (Acceptance: マウスとキーボードの両方)。
                    if (row.IsAction && row.Activate != null)
                    {
                        row.Activate();
                    }

                    Refresh();
                });
        }

        // --- プレビューパネル (design cqQMc / uDNC0 共通) ---
        // 4 タブ共通: Head (8,8) / Game Preview (24,60) 464x280 $k0 r8 /
        // Divider (24,Y) 464x1 #FFFFFF1F / Desc Title (24,Y+20) fs32 /
        // Desc (24,Y+70) w464 fs24 lineHeight1.4。Y はタブごと
        // (<see cref="V6SettingsLayout.DescDividerYOf"/>)。
        // タブ別の追加要素: Play=キー一覧 / Display=判定凡例 / Sound=音量スライダー。

        private void UpdatePreview()
        {
            if (previewPanelGo == null)
            {
                // 骨格が壊れているときは Settings/Preview ごと作り直す。
                RebuildMainPanels();
                return;
            }

            if (previewHeadTitle != null)
            {
                previewHeadTitle.text = PreviewHeadTitle();
            }

            V6SettingRow row = SelectedRowDesc();
            if (descTitleText != null)
            {
                descTitleText.text = row != null ? row.DescTitle ?? string.Empty : string.Empty;
            }

            if (descBodyText != null)
            {
                descBodyText.text = row != null ? row.Desc ?? string.Empty : string.Empty;
                FitDescContent();
            }

            // Sound タブ: スライダーが結線されている音量対象が変わったら、
            // スライダーだけを作り直す (プレビュー全体は作り直さない)。
            int vi = row != null ? row.VolumeIndex : -1;
            if (vi != volumeSliderRowIndex)
            {
                V6Hierarchy.DetachAndDestroy(previewPanelGo.transform.Find("Volume Slider")?.gameObject);
                volumeSlider = null;
                volumeSliderRowIndex = -1;
                if (vi >= 0)
                {
                    BuildVolumePreview(previewPanelGo);
                }
            }

            // ドラッグ・外部からの値変更 (Pause の SetSelectedPauseVolume 等)
            // でもノブ位置と値表示が追従するよう、毎回現在値で再描画する。
            if (volumeSlider != null)
            {
                volumeSlider.Refresh();
            }
        }

        private void BuildPreview()
        {
            var panel = V6Controls.Panel(main, "Preview",
                new Vector2(V6SettingsLayout.PrevX, V6SettingsLayout.PrevY),
                new Vector2(V6SettingsLayout.PrevW, V6SettingsLayout.PrevH),
                V6Tokens.Key2, V6Tokens.RadiusLarge);
            previewPanelGo = panel;

            var head = V6Layout.Row(panel, "Head", new Vector2(8f, 8f), new Vector2(496f, 36f), 12f);
            previewHeadTitle = V6Controls.Text(head, "Title", PreviewHeadTitle(),
                new Vector2(12f, 0f), new Vector2(400f, 36f), 18f, V6Tokens.TextDim);

            BuildGamePreview(panel);

            if (SelectedTab == 0)
            {
                BuildKeyGrid(panel);
            }
            else if (SelectedTab == 1)
            {
                BuildJudgeLegend(panel);
            }
            else if (SelectedTab == 2)
            {
                BuildVolumePreview(panel);
            }

            BuildDesc(panel, V6SettingsLayout.DescDividerYOf(SelectedTab), SelectedRowDesc());
        }

        private string PreviewHeadTitle()
        {
            switch (SelectedTab)
            {
                case 0:
                    // design: キー割当 (JIS / 標準)。実際の配列状態を出す。
                    return "キー割当 (" + (V6SettingsCatalog.IsCustomKeyLayout() ? "カスタム" : "JIS / 標準") + ")";
                case 1:
                    return "判定表示";
                case 2:
                    return "音量";
                default:
                    return "設定";
            }
        }

        /// <summary>
        /// Game Preview well (design 共通: 464x280 $k0 r8)。
        ///
        /// Issue #579: 静止した 2D 図形ではなく、本編と同じ 3D レーン・
        /// ノーツを専用カメラの RenderTexture 経由で描く。
        /// Issue #733: well の中身は <see cref="V6GamePreview"/> (中身は
        /// V6OptionPreviewRig) で、Option / Pause とも同じものを使う。
        /// </summary>
        private void BuildGamePreview(GameObject panel)
        {
            var well = V6Controls.Panel(panel, "Game Preview",
                new Vector2(V6SettingsLayout.GamePrevX, V6SettingsLayout.GamePrevY),
                new Vector2(V6SettingsLayout.GamePrevW, V6SettingsLayout.GamePrevH),
                V6Tokens.Key0, V6Tokens.RadiusMedium);

            // ホストはビューの寿命と同じ (タブ切替で Preview パネルが
            // 作り直されても RawImage が張り替わるだけで 3D シーンは残る)。
            if (gamePreview == null)
            {
                gamePreview = new V6GamePreview();
            }

            gamePreview.Bind(well);
        }

        /// <summary>
        /// Play タブ: JIS キーボード + キー名の一覧 (design の Key 1..Key _)。
        /// キャップに出すのはキー名で、レーン番号ではない
        /// (2026-11-12 オーナー指摘: レーン番号だと同じレーンのキーが
        /// 同じ数字になり「1 2 3 4 4 5 5 6 7 8 8 8」のように崩れて見えた)。
        /// </summary>
        private void BuildKeyGrid(GameObject panel)
        {
            for (int r = 0; r < V6SettingsLayout.GridRowY.Length; r++)
            {
                KeyCode[] keys = V6SettingsLayout.JisGridKeys[r];
                float y = V6SettingsLayout.GridRowY[r];
                float x0 = V6SettingsLayout.GridRowX[r];
                for (int k = 0; k < keys.Length; k++)
                {
                    string name = V6SettingsLayout.KeyDisplayName(keys[k]);
                    var cap = V6Controls.Panel(panel, "Key " + name,
                        new Vector2(x0 + V6SettingsLayout.GridKeyStepX * k, y),
                        new Vector2(V6SettingsLayout.GridKeyW, V6SettingsLayout.GridKeyH),
                        V6Tokens.Key4, V6Tokens.RadiusSmall);
                    V6Controls.Text(cap, "Label", name,
                        new Vector2(0f, 9f), new Vector2(V6SettingsLayout.GridKeyW, 20f), 20f, V6Tokens.Text,
                        TextAlignmentOptions.Center);
                }
            }
        }

        /// <summary>
        /// Display タブ: 判定色の凡例 (design EzLfb Judge PERFECT..MISS)。
        /// チップ (24,360) 106×64 r8 $k1 / Marker (37,16) 32×4 r2 判定色 /
        /// Label fs18 $text-dim 中央。判定色は HUD と同じ割り当て。
        /// </summary>
        private void BuildJudgeLegend(GameObject panel)
        {
            for (int i = 0; i < V6SettingsLayout.JudgeLabels.Length; i++)
            {
                string label = V6SettingsLayout.JudgeLabels[i];
                var chip = V6Controls.Panel(panel, "Judge " + label,
                    new Vector2(V6SettingsLayout.JudgeChipX + V6SettingsLayout.JudgeChipStep * i,
                        V6SettingsLayout.JudgeChipY),
                    new Vector2(V6SettingsLayout.JudgeChipW, V6SettingsLayout.JudgeChipH),
                    V6Tokens.Key1, V6Tokens.RadiusMedium);

                var marker = Rect(chip, "Marker", V6SettingsLayout.JudgeMarkerW, V6SettingsLayout.JudgeMarkerH,
                    JudgeColor(label));
                V6Layout.Place(marker.GetComponent<RectTransform>(),
                    new Vector2(V6SettingsLayout.JudgeMarkerX, V6SettingsLayout.JudgeMarkerY),
                    new Vector2(V6SettingsLayout.JudgeMarkerW, V6SettingsLayout.JudgeMarkerH));

                V6Controls.Text(chip, "Label", label, new Vector2(0f, 22f),
                    new Vector2(V6SettingsLayout.JudgeChipW, 26f), 18f, V6Tokens.TextDim,
                    TextAlignmentOptions.Center);
            }
        }

        /// <summary>判定色。HUD の判定表示と同じ割り当てを <see cref="V6HudLogic.VerdictColor"/> から取得する (Issue #679)。独自テーブルを持つと凡例と実際の判定色がずれる。</summary>
        private static Color JudgeColor(string label)
        {
            return V6HudLogic.VerdictColor(label);
        }

        /// <summary>
        /// Sound タブ: 選択行の音量を共通スライダ部品 (<see cref="V6VolumeSlider"/>)
        /// で出す。値ラベル / トラック / フィル / ノブ / ヒントを
        /// プレビューパネル内 (24,344) に置き、フォーカス行の音量と双方向に同期する。
        /// </summary>
        private void BuildVolumePreview(GameObject panel)
        {
            V6SettingRow row = SelectedRowDesc();
            if (row == null || row.VolumeIndex < 0 || host == null)
            {
                return;
            }

            int index = row.VolumeIndex;
            volumeSlider = V6VolumeSlider.Build(panel, "Volume Slider",
                new Vector2(V6SettingsLayout.VolumeSliderX, V6SettingsLayout.VolumeSliderY),
                () => V6VolumeSettings.Get(index),
                (value, final) =>
                {
                    host.Volumes.Set(index, value, final);
                    UpdateVolumeVisuals();
                });
            volumeSliderRowIndex = index;
        }

        /// <summary>スライダと選択行の値表示だけを更新する (レイアウトは作り直さない)。</summary>
        private void UpdateVolumeVisuals()
        {
            if (volumeSlider != null)
            {
                volumeSlider.Refresh();
            }

            V6SettingRow row = SelectedRowDesc();
            if (row == null || row.VolumeIndex < 0)
            {
                return;
            }

            int selected = SelectedRow;
            if (selected >= 0 && selected < rowViews.Count)
            {
                rowViews[selected].RefreshValue();
            }
        }

        /// <summary>Divider + Desc Title + Desc (design 共通: Divider Y はタブ別)。</summary>
        private void BuildDesc(GameObject panel, float dividerY, V6SettingRow row)
        {
            var divider = Rect(panel, "Divider", 464f, 1f, V6Tokens.Divider);
            V6Layout.Place(divider.GetComponent<RectTransform>(), new Vector2(24f, dividerY), new Vector2(464f, 1f));

            // draw shape は「Title y+20 h46」— 高さ 40 だと NotoSansJP の行送り
            // (fs32 で約 46px) を下回り、TMP の Ellipsis が 1 行も描画しない。
            // design と同じ h46 にし、Ellipsis は付けない。
            descTitleText = V6Controls.Text(panel, "Desc Title",
                row != null ? row.DescTitle ?? string.Empty : string.Empty,
                new Vector2(24f, dividerY + V6SettingsLayout.DescTitleOffsetY),
                new Vector2(V6SettingsLayout.DescW, 46f), 32f, V6Tokens.Text, TextAlignmentOptions.Left);

            // Desc は design 上で複数行に折り返す段落 (w464 h68 lineHeight1.4)。
            // 長い説明文が固定 68px の外へはみ出さないよう、ビューポート
            // (RectMask2D) + ScrollRect で縦スクロール可能にし、
            // 文字は必要高まで伸ばす。
            var viewport = V5Ui.Node(panel, "Desc Viewport");
            var viewportRt = V6Layout.RectOf(viewport);
            V6Layout.Place(viewportRt,
                new Vector2(24f, dividerY + V6SettingsLayout.DescOffsetY),
                new Vector2(V6SettingsLayout.DescW, V6SettingsLayout.DescH));
            viewport.AddComponent<RectMask2D>();

            // ビューポートは Graphic を持たない Node なので、このままでは
            // ポインタイベントが背後へ抜けて OnScroll に届かない
            // (V6CompactScreen の catcher と同じく、透明な raycast 対象を敷く)。
            var hit = viewport.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.viewport = viewportRt;
            // ホイールは ScrollRect.OnScroll が処理する。別の IScrollHandler を
            // 重ねると 1 入力で二重スクロールになる (PR #787 review)。

            var content = V5Ui.Node(viewport, "Desc Content");
            var contentRt = V6Layout.RectOf(content);
            // 幅はビューポートにストレッチ、高さはテキスト必要高
            // (FitDescContent が preferredHeight で更新する)。
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            var desc = V6Controls.Text(content, "Desc", row != null ? row.Desc ?? string.Empty : string.Empty,
                Vector2.zero, new Vector2(V6SettingsLayout.DescW, V6SettingsLayout.DescH),
                24f, V6Tokens.Text, TextAlignmentOptions.TopLeft, FontStyles.Normal,
                TextWrappingModes.Normal);
            descBodyText = desc;
            // design lineHeight 1.4 (fs24 → 33.6px)。TMP の lineSpacing は fs への
            // 追加率で、0 (フォント本来の行送り) が design 実測と一致する。
            desc.lineSpacing = 0f;
            // TMP 自体は content いっぱいにストレッチ (高さは content 側が決める)。
            var descRt = desc.rectTransform;
            descRt.anchorMin = Vector2.zero;
            descRt.anchorMax = Vector2.one;
            descRt.offsetMin = Vector2.zero;
            descRt.offsetMax = Vector2.zero;

            scroll.content = contentRt;
            descScrollContent = contentRt;
            descScrollRect = scroll;
            FitDescContent();
        }

        /// <summary>
        /// 説明文の必要高へ content を伸ばす。選択行の切替
        /// (descBodyText.text 更新) のたびに呼ぶ。
        /// 本文が前回と同じならスクロール位置を保持し、変わったときだけ
        /// 先頭へ戻す (値変更など本文不変の再描画で読んでいる位置を失わない)。
        /// </summary>
        private void FitDescContent()
        {
            if (descBodyText == null || descScrollContent == null)
            {
                return;
            }

            bool textChanged = descBodyText.text != fittedDescText;
            if (!textChanged && descScrollContent.sizeDelta.y > 0f)
            {
                // 本文も収容高も変わっていない (値変更などの再描画)。
                // ForceMeshUpdate を省き、スクロール位置もそのまま保つ
                // (Pause は開いている間 Refresh を毎フレーム呼ぶ)。
                return;
            }

            fittedDescText = descBodyText.text;
            float scrollPos = descScrollRect != null
                ? descScrollRect.verticalNormalizedPosition : 1f;

            descBodyText.ForceMeshUpdate();
            float needed = descBodyText.preferredHeight;
            // ビューポート高 (DescH) を下回る場合も DescH に揃える
            // (短い文でスクロールバー的な遊びが出ないように)。
            descScrollContent.sizeDelta = new Vector2(0f,
                Mathf.Max(V6SettingsLayout.DescH, needed));
            if (descScrollRect != null)
            {
                descScrollRect.verticalNormalizedPosition = textChanged
                    ? 1f
                    : Mathf.Clamp01(scrollPos);
            }
        }

        private V6SettingRow SelectedRowDesc()
        {
            if (currentRows.Count == 0)
            {
                return null;
            }

            int index = SelectedRow;
            if (index < 0 || index >= currentRows.Count)
            {
                return null;
            }

            return currentRows[index];
        }

        /// <summary>素の Image (角丸無し)。細線 / 矩形用。</summary>
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
    }
}
