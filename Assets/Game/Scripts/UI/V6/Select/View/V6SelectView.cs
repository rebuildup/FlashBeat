using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Select.View
{
    /// <summary>
    /// Select 画面 (#221) の v6 UI。design/flashbeat-ui.pen v6 の
    /// 02 Select (OENRv) / Panel 選択中の曲 (dRztp) / 02b 該当なし (AqFtx) を実行時に構築する。
    /// 02c 設定 (wSjAm) は右カラム (Ranking) を「このプレイの設定」へ差し替える形で構築する。
    ///
    /// - データは IV6SelectDataSource 経由のみ (#222 が実装)。実カタログ/実保存スコア/実ランキングを
    ///   表示し、デザイン例の固定値 (サンプル曲名・"988,513" 等) は決して出さない。
    /// - 構築はコードのみ (シーン YAML / .meta は触らない)。Refresh で frame の全子を作り直す。
    /// - 寸法は design px (1728x972 基準)。frame は **1:1 (等倍) で中央配置** し、
    ///   引き伸ばし (等比スケール) は行わない (原点は <see cref="V6SelectViewLogic.FrameOrigin"/>)。
    ///   色は V5Tokens を流用する (v6 は新しい色トークンを追加していない)。
    /// - キー: ↑↓ 選択 / Enter・Space Ready (data.CommitReady) / Esc 戻る (onBack) /
    ///   Tab 検索フォーカス (フォーカス中は Input.inputString を SetQuery へ) / O 設定。
    /// - マウス: 行クリック選択 / ホイールスクロール / 戻る・設定・Ready・スクロールボタン。
    /// - SelectSceneManager は呼ばない。onReady / onBack / onOpenSettings は統合担当が渡す。
    /// </summary>
    public sealed class V6SelectView
    {
        /// <summary>
        /// 現在生成されている v6 選曲画面 (選曲シーンに 1 個だけ)。
        /// 検証 (AutoTest) や統合コードから参照するための参照点。
        /// </summary>
        public static V6SelectView Current { get; private set; }

        // --- design px 定数 (PEN-EXTRACT.md §5) ---
        private const float FrameW = 1728f;
        private const float FrameH = 972f;

        // design Frame (1728x972) の Main ゾーンは (24, 96)。列はそのローカル座標。
        private const float MainX = 24f, MainY = 96f;
        private const float ListX = 0f, ListW = 560f, ListH = 780f;
        private const float RowX = 16f, RowY0 = 48f, RowW = 504f, RowH = 72f, RowStep = 76f;
        private const int ListVisibleRows = 9; // y48..y732 (732 / 76)
        private const int RankingVisibleRows = 4; // 300px パネルに 44px 行 + 8px 間隔で 4 行

        private const float CenterX = 572f, CenterW = 536f;
        private const float RightX = 1120f, RightW = 560f;

        private readonly IV6SelectDataSource data;
        private readonly Action onReady;
        private readonly Action onBack;
        private readonly Action onOpenSettings;

        private GameObject rootGo;
        private RectTransform frame;
        private int listScroll;
        private int rankingScroll;
        private bool searchFocused;

        /// <summary>「未プレイのみ」で絞り込む (design の Filter ボタン / F キー)。</summary>
        private bool unplayedOnly;
        private bool settingsOpen;

        private V6SelectView(IV6SelectDataSource data, Action onReady, Action onBack, Action onOpenSettings)
        {
            this.data = data;
            this.onReady = onReady;
            this.onBack = onBack;
            this.onOpenSettings = onOpenSettings;
        }

        /// <summary>View を生成する。統合担当 (SelectSceneManager 側) から呼ぶ。</summary>
        public static V6SelectView Create(RectTransform parent, IV6SelectDataSource data,
            Action onReady, Action onBack, Action onOpenSettings)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var view = new V6SelectView(data, onReady, onBack, onOpenSettings);
            view.BuildRoot(parent);
            // 初期選択が無いと中央パネルが Skeleton のままになるので 1 曲目を選んでおく。
            // 購読前に実行するため Changed による再入再描画は起きない。
            if (data.Selected == null && data.FilteredSongs.Count > 0)
            {
                data.Select(data.FilteredSongs[0].SongId);
            }
            // 実データの変更 (選択の移動・検索・カタログ更新) を再描画の起点にする。
            // 購読しないと中央パネルとランキングが古い状態のままになる。
            data.Changed += view.OnDataChanged;
            view.Rebuild();
            Current = view;
            return view;
        }

        public RectTransform Root => frame;

        /// <summary>データソースの変更通知。選択行が見えるようにスクロールを追従させる。</summary>
        private void OnDataChanged()
        {
            if (rootGo == null || !rootGo.activeInHierarchy) return;
            Rebuild();
        }

        /// <summary>生成したビューを破棄するとき購読を解除する。</summary>
        public void Dispose()
        {
            if (data != null) data.Changed -= OnDataChanged;
            if (rootGo != null) FlashBeat.UI.V5.V5GameUi.DestroyNow(rootGo);
            if (ReferenceEquals(Current, this)) Current = null;
        }

        /// <summary>data.Changed の再描画起点。選択行が見えるようにスクロールを追従させる。</summary>
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

        /// <summary>検索欄にフォーカスする (統合担当が画面表示時に呼べる)。</summary>
        public void FocusSearch()
        {
            searchFocused = true;
            Rebuild();
        }

        /// <summary>
        /// クイック設定 (このプレイの設定) を開く。右カラムを設定パネルへ
        /// 差し替えて再描画する (SelectSceneManager.OnV6OpenSettings から呼ぶ)。
        /// </summary>
        public void OpenQuickSettings()
        {
            settingsOpen = true;
            Rebuild();
        }

        /// <summary>表示する曲リスト (実データ + 未プレイのみトグル)。</summary>
        private List<V6SongEntry> DisplaySongs()
        {
            IReadOnlyList<V6SongEntry> filtered = data.FilteredSongs;
            var list = new List<V6SongEntry>();
            if (filtered == null) return list;
            for (int i = 0; i < filtered.Count; i++)
            {
                V6SongEntry song = filtered[i];
                if (song == null) continue;
                if (unplayedOnly && song.BestScore >= 0) continue; // プレイ済みの曲を除く
                list.Add(song);
            }
            return list;
        }

        /// <summary>「未プレイのみ」絞り込みの切り替え。</summary>
        private void ToggleUnplayedFilter()
        {
            unplayedOnly = !unplayedOnly;
            Rebuild();
        }

        /// <summary>
        /// Ready _button を押したのと同じ動作 (遷移は統合担当が onReady で決める)。
        /// 検証スクリプト (AutoTestV6) から実データ経路で遷移を確認するための口。
        /// 絞り込みで隠れた選択は Ready できない (#221/#222)。
        /// </summary>
        public void TriggerReady()
        {
            if (!ReadyAvailable()) return;
            if (onReady != null) onReady();
        }

        /// <summary>
        /// Ready が有効か。選択中の曲が表示リスト (検索絞り込み + 未プレイのみ)
        /// に見えるときだけ true。隠れた曲は Ready ボタン・Space どちらでも開始できない。
        /// </summary>
        internal bool ReadyAvailable()
        {
            return V6SelectViewLogic.ReadyAvailable(data != null ? data.Selected : null, DisplaySongs());
        }

        /// <summary>検証用: 現在の曲リスト (絞り込み後) の曲数。</summary>
        public int VisibleSongCount
        {
            get { return data == null || data.FilteredSongs == null ? 0 : data.FilteredSongs.Count; }
        }

        /// <summary>検証用: 画面が参照しているデータ供給。</summary>
        public IV6SelectDataSource Data
        {
            get { return data; }
        }

        // --- 毎フレーム (V6SelectViewDriver から呼ばれる) ---

        internal void Tick()
        {
            if (rootGo == null || !rootGo.activeInHierarchy)
            {
                return;
            }

            PlaceFrame();

            // メニュー操作は KeyActions (キー設定の正本, #142) 経由で拾う。
            // v5 SelectView と同じ短縮キーを維持する: ↑↓ / ←→ (NavPrev/NavNext) で
            // 曲送り、Confirm (既定 Space) と Enter/KeypadEnter で Ready、
            // Back (既定 Esc) で戻る。Tab (未プレイのみの絞り込み) は KeyActions に
            // 無い v6 固定ショートカットなので raw KeyCode のまま。
            // 検索は正本 OENRv の凡例どおり「A-Z を押すと開始」。
            bool up = Input.GetKeyDown(KeyCode.UpArrow) || KeyActions.IsDown(KeyActions.KeyAction.NavPrev);
            bool down = Input.GetKeyDown(KeyCode.DownArrow) || KeyActions.IsDown(KeyActions.KeyAction.NavNext);
            bool confirm = KeyActions.IsDown(KeyActions.KeyAction.Confirm)
                || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool cancel = KeyActions.IsDown(KeyActions.KeyAction.Back);
            bool tab = Input.GetKeyDown(KeyCode.Tab);
            var action = V6SelectViewLogic.MapKey(searchFocused, up, down, confirm, cancel, tab);

            switch (action)
            {
                case V6SelectAction.UnfocusSearch:
                    searchFocused = false;
                    Rebuild();
                    return;
                case V6SelectAction.MoveUp:
                    data.MoveSelection(-1);
                    return;
                case V6SelectAction.MoveDown:
                    data.MoveSelection(1);
                    return;
                case V6SelectAction.Confirm:
                    // Enter/Space = Ready。遷移は統合担当が onReady で決める
                    // (data.CommitReady の後にゲーム画面へ移す)。
                    // 絞り込みで隠れた選択は Ready できない (#221/#222)。
                    if (ReadyAvailable() && onReady != null)
                    {
                        onReady();
                    }
                    return;
                case V6SelectAction.Cancel:
                    if (settingsOpen)
                    {
                        CloseSettings();
                    }
                    else if (onBack != null)
                    {
                        onBack();
                    }
                    return;
                case V6SelectAction.ToggleFilter:
                    ToggleUnplayedFilter();
                    return;
            }

            if (searchFocused)
            {
                string input = Input.inputString;
                if (!string.IsNullOrEmpty(input))
                {
                    data.SetQuery(V6SelectViewLogic.ApplySearchInput(data.Query, input));
                }

                return;
            }

            // 正本 OENRv: 英字 (A-Z) を押すと検索欄に入る。制御文字は対象外。
            string typed = Input.inputString;
            char seed = V6SelectViewLogic.SearchSeed(typed);
            if (seed != '\0')
            {
                searchFocused = true;
                data.SetQuery(V6SelectViewLogic.ApplySearchInput("", seed.ToString()));
                Rebuild();
                return;
            }

            if (KeyActions.IsDown(KeyActions.KeyAction.Option)) // 既定 O (#142)
            {
                OpenSettings();
            }
        }

        // --- 構築 ---

        private void BuildRoot(RectTransform parent)
        {
            rootGo = V5Ui.Node(parent.gameObject, "V6 Select View");
            var rt = rootGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            // フレーム外 (画面全体) には背景パネルを敷かない。
            // design の「Resolution · MacBook Air M5 2560×1664」フレーム外周は
            // ゲーム自身の背景 (シーンの 3D BackGround 等) が見えるのが正。
            // 全面不透明パネルは背景アニメーションを潰す (V6Frame.ApplyBackdrop 同様、取り下げ)。

            // design frame (1728×972) は 1:1 (等倍) で中央配置。引き伸ばしはしない。
            frame = V5Ui.Node(rootGo, "V6 Select Frame 1728x972").GetComponent<RectTransform>();
            PlaceFrame();

            var driver = rootGo.AddComponent<V6SelectViewDriver>();
            driver.View = this;
        }

        /// <summary>
        /// frame を実画面中央へ 1:1 で置く。原点は
        /// <see cref="V6SelectViewLogic.FrameOrigin"/> ((w-1728)/2, (h-972)/2 の床切り)。
        /// </summary>
        private void PlaceFrame()
        {
            V6SelectViewLogic.FrameOrigin(Screen.width, Screen.height, out int x, out int y);
            frame.anchorMin = new Vector2(0f, 1f);
            frame.anchorMax = new Vector2(0f, 1f);
            frame.pivot = new Vector2(0f, 1f);
            frame.anchoredPosition = new Vector2(x, -y);
            frame.sizeDelta = new Vector2(FrameW, FrameH);
        }

        private void Rebuild()
        {
            if (rootGo == null)
            {
                return;
            }

            PlaceFrame();

            // frame の全子を作り直す (V5 SelectView.Rebuild と同じ方針)。
            var children = new List<GameObject>();
            foreach (Transform child in frame.transform)
            {
                children.Add(child.gameObject);
            }

            for (int i = 0; i < children.Count; i++)
            {
                V5GameUi.DestroyNow(children[i]);
            }

            // 表示する曲: 実データの絞り込み結果 (+ 「未プレイのみ」トグル)。
            var songs = DisplaySongs();
            int selectIndex = SelectedIndex(songs);
            // 選択中の曲を常にリスト中央に表示する (端ではクランプ)。以前の位置を引きずらない。
            listScroll = V6SelectViewLogic.CenterScroll(selectIndex, songs.Count, ListVisibleRows);
            rankingScroll = V6SelectViewLogic.ClampScroll(rankingScroll,
                data.Selected != null ? data.RankingFor(data.Selected).Count : 0, RankingVisibleRows);

            BuildHeader(songs.Count);
            BuildFooter();
            BuildFrameStroke();
            BuildList(songs, selectIndex);
            BuildCenter();
            if (settingsOpen)
            {
                // wSjAm: 右カラム (Ranking) を「このプレイの設定」へ差し替える。
                BuildSettings();
            }
            else
            {
                BuildRight();
            }
        }

        // --- ヘッダー (x24 y0 1680x96) ---

        /// <summary>
        /// 共有フレーム rw7zx の外周 stroke (#EDEDED33 sw2, inner)。
        /// uGUI に線要素はないため 4 枚の細長パネルで再現する (V6Frame と同じ方針)。
        /// </summary>
        private void BuildFrameStroke()
        {
            var stroke = FromHex("#EDEDED33");
            At(V5Ui.Panel(frame.gameObject, "Stroke Top", new Vector2(FrameW, 2f), stroke, 0f), 0f, 0f);
            At(V5Ui.Panel(frame.gameObject, "Stroke Bottom", new Vector2(FrameW, 2f), stroke, 0f), 0f, FrameH - 2f);
            At(V5Ui.Panel(frame.gameObject, "Stroke Left", new Vector2(2f, FrameH), stroke, 0f), 0f, 0f);
            At(V5Ui.Panel(frame.gameObject, "Stroke Right", new Vector2(2f, FrameH), stroke, 0f), FrameW - 2f, 0f);
        }

        private void BuildHeader(int filteredCount)
        {
            var header = At(V5Ui.Panel(frame.gameObject, "Header", new Vector2(1680f, 96f), Clear(), 0f), 24f, 0f);

            // 戻るボタン 56x56 (#3A3A3A r8) + ← / Esc キャプション。
            // ボタンを Node の中に置くと(Node は 100x100 のまま)中央寄せに
            // なるので、header 直下に置き design 座標で固定する。
            var backRect = V6SelectViewLogic.HeaderBackButton();
            var back = Button(header, "Back Button", backRect.W, backRect.H, 8f, V5Tokens.Key4, () =>
            {
                if (onBack != null) onBack();
            });
            // 設計 OENRv: ヘッダー左端 (フレーム (24,20))。At を忘れると
            // V5Ui.Panel の既定 (親の中央) に落ち、検索欄の左に Esc が出る。
            At(back, backRect.X, backRect.Y);
            // 正本 zCQBY: arrow-left 24 @(16,4) + Key "Esc" fs16 @(15,30)。
            // アイコンを中央寄せすると Key と重なるので専用配置 (共通部品)。
            V6Controls.FillBackButton(back,
                FlashBeat.UI.V5.ActionKeyHint.Primary(KeyActions.KeyAction.Back));

            var title = V5GameUi.Text(header.gameObject, "Title", "選曲", 38f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            var titleRect = V6SelectViewLogic.HeaderTitle();
            At(title, titleRect.X, titleRect.Y, titleRect.W, titleRect.H);

            // 右上: 検索欄 / 件数 / 状態メッセージ / フィルターボタン
            // 右上情報ゾーンは header-local x=840..1680 (padding 24 / gap 12)。
            // Search(幅 580) → Count(140) → Filter(48)。
            var searchRect = V6SelectViewLogic.HeaderSearch();
            var search = Button(header, "Search", searchRect.W, searchRect.H, 8f, V5Tokens.Overlay, FocusSearch);
            At(search, searchRect.X, searchRect.Y);
            var queryRect = V6SelectViewLogic.HeaderSearchQuery();
            var queryText = V5GameUi.Text(search, "Query", SearchDisplayText(), 22f,
                searchFocused ? V5Tokens.Text : V5Tokens.TextDim, TextAlignmentOptions.Left);
            SingleLine(queryText);
            At(queryText, queryRect.X, queryRect.Y, queryRect.W, queryRect.H);
            var capRect = V6SelectViewLogic.HeaderSearchCap();
            var cap = KeyCap(search, "A-Z", V6Ui.KeyCapFontSize(capRect.W, capRect.H, "A-Z"),
                capRect.W, capRect.H);
            At(cap, capRect.X, capRect.Y);

            var count = V5GameUi.Text(header.gameObject, "Count",
                $"{filteredCount} / {data.AllSongs.Count}", 22f, V5Tokens.TextMuted,
                TextAlignmentOptions.Right);
            var countRect = V6SelectViewLogic.HeaderCount();
            At(count, countRect.X, countRect.Y, countRect.W, countRect.H);

            if (!string.IsNullOrEmpty(data.StatusMessage))
            {
                var status = V5GameUi.Text(header.gameObject, "Status", data.StatusMessage, 18f,
                    data.HasError ? V5Tokens.DangerLight : V5Tokens.TextDim, TextAlignmentOptions.Left);
                At(status, 864f, 74f, 792f, 20f);
            }

            var filter = Button(header, "Filter Button", 48f, 48f, 8f,
                unplayedOnly ? V5Tokens.Key6 : V5Tokens.Key4, ToggleUnplayedFilter);
            var filterRect = V6SelectViewLogic.HeaderFilter();
            At(filter, filterRect.X, filterRect.Y);
            V6Icons.Create(filter, "list-filter", 24f, V5Tokens.TextMuted);
        }

        // --- フッター (x24 y876 1680x96) ---

        private void BuildFooter()
        {
            var footer = At(V5Ui.Panel(frame.gameObject, "Footer", new Vector2(1680f, 96f), Clear(), 0f), 24f, 876f);

            // 凡例は絶対位置 (design px)。各項目の幅は純ロジック (V6SelectViewLogic) が決める。
            // 確定キーはキー設定 (KeyActions) の正本から取る (#142 / #221)。
            string confirmKey = FlashBeat.UI.V5.ActionKeyHint.Primary(KeyActions.KeyAction.Confirm);
            var legendItems = V6SelectViewLogic.FooterLegend(confirmKey);
            for (int i = 0; i < legendItems.Length; i++)
            {
                var item = legendItems[i];
                // V6Ui.KeyCapFontSize で幅に収まる fs を決める (Spa/ce 折り返し対策)。
                var cap = KeyCap(footer.gameObject, item.Key,
                    V6Ui.KeyCapFontSize(item.CapWidth, 32f, item.Key), item.CapWidth, 32f);
                At(cap, item.X, 30f);
                var label = V5GameUi.Text(footer.gameObject, "Legend " + item.Label, item.Label,
                    V6SelectViewLogic.LabelFontSize, V5Tokens.TextMuted, TextAlignmentOptions.Left);
                SingleLine(label);
                At(label, item.X + item.CapWidth + V6SelectViewLogic.LegendLabelGap, 46f, item.LabelWidth, 32f,
                    new Vector2(0f, 0.5f));
            }

            // 右下: 設定 (secondary) + Ready (primary)。design 座標のまま右詰め。
            // 設定パネル展開中 (wSjAm) は「閉じる (Esc)」Secondary に差し替える。
            var settingsRect = V6SelectViewLogic.FooterSettings();
            var settingsBtn = Button(footer, settingsOpen ? "Op Close" : "Button Settings",
                settingsRect.W, settingsRect.H, 8f, V5Tokens.ControlBg,
                settingsOpen ? (Action)CloseSettings : OpenSettings);
            At(settingsBtn, settingsRect.X, settingsRect.Y);
            var settingsIconRect = V6SelectViewLogic.FooterSettingsIcon();
            V6Icons.Create(settingsBtn, settingsOpen ? "x" : "settings", 24f, V5Tokens.TextMuted);
            var settingsIconTf = settingsBtn.transform.Find("Icon");
            if (settingsIconTf != null)
            {
                var iconRt = settingsIconTf.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = new Vector2(
                    settingsIconRect.X + settingsIconRect.W * 0.5f, 0f);
            }
            var settingsLabelRect = V6SelectViewLogic.FooterSettingsLabel();
            var settingsLabel = V5GameUi.Text(settingsBtn, "Label", settingsOpen ? "閉じる" : "設定", 30f,
                V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Normal);
            SingleLine(settingsLabel);
            At(settingsLabel, settingsLabelRect.X, settingsLabelRect.Y, settingsLabelRect.W, settingsLabelRect.H);
            // design: KeyCap (高さ 32, #00000040 r4, Key fs22 $text-muted) を右端に。
            var settingsCap = V5Ui.Panel(settingsBtn, "KeyCap", new Vector2(48f, 32f), new Color(0f, 0f, 0f, 0.25f), 4f);
            var capRt = settingsCap.GetComponent<RectTransform>();
            capRt.anchorMin = new Vector2(1f, 0.5f);
            capRt.anchorMax = new Vector2(1f, 0.5f);
            capRt.pivot = new Vector2(1f, 0.5f);
            capRt.anchoredPosition = new Vector2(-8f, 0f);
            V5GameUi.Text(settingsCap, "Key", settingsOpen
                ? FlashBeat.UI.V5.ActionKeyHint.Primary(KeyActions.KeyAction.Back)
                : FlashBeat.UI.V5.ActionKeyHint.Primary(KeyActions.KeyAction.Option), 22f, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            Stretch(settingsCap.transform.Find("Key").GetComponent<RectTransform>(), 48f, 32f);

            var readyRect = V6SelectViewLogic.FooterReady();
            // 絞り込みで隠れた選択は Ready できない (#221/#222)。無効のときは
            // 押しても反応しないよう、クリックを張らず弱い色で描画する。
            bool readyEnabled = ReadyAvailable();
            var ready = Button(footer, "Op Ready", readyRect.W, readyRect.H, 8f,
                readyEnabled ? V5Tokens.ButtonPrimaryBg : V5Tokens.Key1,
                readyEnabled ? (Action)(() =>
                {
                    if (onReady != null) onReady();
                }) : null);
            At(ready, readyRect.X, readyRect.Y);
            var readyIconRect = V6SelectViewLogic.FooterReadyIcon();
            V6Icons.Create(ready, "play", 26f, readyEnabled ? V5Tokens.ButtonPrimaryLabel : V5Tokens.TextDim);
            var readyIconTf = ready.transform.Find("Icon");
            if (readyIconTf != null)
            {
                var iconRt = readyIconTf.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = new Vector2(readyIconRect.X + readyIconRect.W * 0.5f, 0f);
            }
            var readyLabelRect = V6SelectViewLogic.FooterReadyLabel();
            var readyLabel = V5GameUi.Text(ready, "Label", "Ready", 30f,
                readyEnabled ? V5Tokens.ButtonPrimaryLabel : V5Tokens.TextDim,
                TextAlignmentOptions.Left, FontStyles.Normal);
            SingleLine(readyLabel);
            At(readyLabel, readyLabelRect.X, readyLabelRect.Y, readyLabelRect.W, readyLabelRect.H);
            var readyCapRect = V6SelectViewLogic.FooterReadyCap();
            var readyCap = V5Ui.Panel(ready, "Cap", new Vector2(readyCapRect.W, readyCapRect.H),
                new Color(0f, 0f, 0f, 0.15f), 4f);
            At(readyCap, readyCapRect.X, readyCapRect.Y);
            // V6Ui.KeyCapFontSize で幅に収まる fs を決める (Spa/ce 折り返し対策)。
            // キャップ表示もキー設定の正本 (KeyActions.Confirm) に合わせる。
            var readyCapText = V5GameUi.Text(readyCap, "Key", confirmKey,
                V6Ui.KeyCapFontSize(readyCapRect.W, readyCapRect.H, confirmKey),
                readyEnabled ? V5Tokens.ButtonPrimaryLabel : V5Tokens.TextDim,
                TextAlignmentOptions.Center);
            SingleLine(readyCapText);
            At(readyCapText, 0f, 0f, readyCapRect.W, readyCapRect.H);
        }

        // --- 左列: 曲リスト (x0 w560 #131313CC r16) ---

        private void BuildList(IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            var list = At(V5Ui.Panel(frame.gameObject, "Song List", new Vector2(ListW, ListH),
                FromHex("#131313CC"), 16f), MainX + ListX, MainY);
            AddScrollCatcher(list, 0);

            var rows = V5Ui.Node(list, "Rows");
            At(rows.GetComponent<RectTransform>(), 0f, 0f, ListW, ListH);

            if (data.IsLoading)
            {
                // AqFtx のスケルトン行 (読み込み中)。
                for (int i = 0; i < ListVisibleRows; i++)
                {
                    var skeleton = At(V5Ui.Panel(rows, "Song Skeleton " + i, new Vector2(RowW, RowH),
                        V5Tokens.Key1, 8f), RowX, RowY0 + RowStep * i);
                    At(V5Ui.Panel(skeleton, "Level", new Vector2(44f, 44f), V5Tokens.Key3, 22f), 14f, 14f);
                    At(V5Ui.Panel(skeleton, "Thumb", new Vector2(96f, 54f), V5Tokens.Key3, 4f), 72f, 9f);
                    At(V5Ui.Panel(skeleton, "Texts", new Vector2(220f, 26f), V5Tokens.Key3, 4f), 182f, 14f);
                    At(V5Ui.Panel(skeleton, "Artist", new Vector2(120f, 18f), V5Tokens.Key3, 4f), 182f, 44f);
                    At(V5Ui.Panel(skeleton, "Grade", new Vector2(56f, 36f), V5Tokens.Key3, 4f), 432f, 18f);
                }
            }
            else if (songs.Count == 0)
            {
                // AqFtx: 該当なしの空状態。3 要素は絶対位置 (パネル外へはみ出させない)。
                var empty = At(V5Ui.Panel(rows, "Empty Note", new Vector2(448f, 140f), V5Tokens.Key0, 8f), 56f, 300f);
                V6Icons.Create(empty, "music", 32f, V5Tokens.TextDim);
                var emptyIcon = empty.transform.Find("Icon")?.gameObject;
                if (emptyIcon != null)
                {
                    var iconRt = emptyIcon.GetComponent<RectTransform>();
                    iconRt.anchorMin = new Vector2(0f, 0f);
                    iconRt.anchorMax = new Vector2(1f, 0f);
                    iconRt.pivot = new Vector2(0.5f, 0f);
                    iconRt.anchoredPosition = new Vector2(0f, 12f);
                    iconRt.sizeDelta = new Vector2(0f, 40f);
                }
                var emptyTitle = V5GameUi.Text(empty.gameObject, "Title", "曲が見つかりません", 24f, V5Tokens.Text,
                    TextAlignmentOptions.Center, FontStyles.Normal);
                At(emptyTitle, 0f, 52f, 448f, 32f);
                var emptyHint = V5GameUi.Text(empty.gameObject, "Hint", "検索語を変えるか フィルターを解除してください", 20f,
                    V5Tokens.TextMuted, TextAlignmentOptions.Center);
                SingleLine(emptyHint);
                At(emptyHint, 0f, 88f, 448f, 28f);
            }
            else
            {
                // 中央固定: listScroll = 選択 index - visibleRows/2。スロット 0..visibleRows-1 に
                // index listScroll+i を対応させ、[0, songCount) のみ描く (範囲外は空行)。
                // 選択行は常に中央スロット (visibleRows/2) に来る (空スロットには入らない)。
                int first = listScroll;
                int last = first + ListVisibleRows;
                for (int i = first; i < last; i++)
                {
                    if (i < 0 || i >= songs.Count)
                    {
                        continue;
                    }

                    BuildSongRow(rows, songs[i], i, i == selectIndex, RowY0 + RowStep * (i - first));
                }
            }

            BuildScrollButton(list, "Scroll Up", false);
            BuildScrollButton(list, "Scroll Down", true);
            // 正本 OENRv のリスト下端はスクロールボタンとドットのみ。
            // 「1 / 28」のような選択位置テキストは設計に無いので描かない。
            BuildDots(list, songs, selectIndex);
        }

        private void BuildSongRow(GameObject parent, V6SongEntry song, int index, bool selected, float y)
        {
            var row = At(V5Ui.Panel(parent, "Song " + song.Title, new Vector2(RowW, RowH),
                selected ? V5Tokens.Key6 : V5Tokens.Recessed2, 8f), RowX, y);
            var button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            int songId = song.SongId;
            button.onClick.AddListener(() => data.Select(songId));

            var levelBadge = At(V5Ui.Panel(row, "Level", new Vector2(44f, 44f), V5Tokens.Key4, 22f), 14f, 14f);
            var levelText = V5GameUi.Text(levelBadge.gameObject, "Num", song.Level > 0 ? song.Level.ToString() : "–", 22f,
                V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(levelText.GetComponent<RectTransform>(), 44f, 44f);

            var thumb = At(V5Ui.Panel(row, "Thumb", new Vector2(96f, 54f), V5Tokens.Key3, 4f), 72f, 9f);
            SetJacket(thumb, song);

            // 曲名 / アーティストは絶対位置 (行の外へはみ出させない)。
            var songTitle = V5GameUi.Text(row, "Title", song.Title, 26f, V5Tokens.Text, TextAlignmentOptions.Left,
                FontStyles.Normal);
            Ellipsis(songTitle);
            // Ellipsis は「1 行も矩形に収まらないと何も描画しない」。NotoSansJP の
            // 行高は fs の約 1.45 倍なので fs26 には 38px 必要 (28 だと無描画になる)。
            At(songTitle, 182f, 14f, 240f, 38f);
            var songArtist = V5GameUi.Text(row, "Artist", song.Artist, 20f, V5Tokens.TextMuted,
                TextAlignmentOptions.Left, FontStyles.Normal);
            Ellipsis(songArtist);
            // fs20 × 1.448 ≒ 29 → 30px。ink は fs20 (20px) なので Title との重なりは行高 padding のみ。
            At(songArtist, 182f, 42f, 240f, 30f);

            var grade = At(V5Ui.Panel(row, "Grade", new Vector2(56f, 36f), V5Tokens.Key4, 4f), 432f, 18f);
            var gradeText = V5GameUi.Text(grade.gameObject, "Text", string.IsNullOrEmpty(song.BestGrade) ? "–" : song.BestGrade,
                22f, V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(gradeText.GetComponent<RectTransform>(), 56f, 36f);
        }

        private void BuildScrollButton(GameObject list, string name, bool down)
        {
            var rect = V6SelectViewLogic.ListScrollButton(down);
            var btn = Button(list, name, rect.W, rect.H, 8f, V5Tokens.Key4, () => ScrollList(down ? 1 : -1));
            At(btn, rect.X, rect.Y);
            V6Icons.Create(btn, down ? "chevron-down" : "chevron-up", 24f, V5Tokens.TextMuted);
        }

        /// <summary>
        /// ドットインジケクタ。design (AqFtx / OENRv) は **全曲数** の分だけ
        /// (x=536, y=136 から縦に gap 4, size 8、選択中だけ 10px の $accent)。
        /// 絞り込みで曲数が減ってもドット数は変えない (ページ位置の案内)。
        /// 各ドットの y は <see cref="V6SelectViewLogic.DotOffsets"/> (純ロジック) の
        /// とおり size+gap 間隔で積む (親ローカル座標系を統一しないと全ドットが重なる)。
        /// </summary>
        private void BuildDots(GameObject list, IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            var dots = V5Ui.Node(list, "Dot indicator");
            IReadOnlyList<V6SongEntry> all = data.AllSongs;
            int count = all == null ? 0 : all.Count;
            float total = V6SelectViewLogic.DotBottom(count, 0f);
            // レール幅は design どおり最大ドット幅 (10)。ドットはレール中央に置く。
            At(dots.GetComponent<RectTransform>(), V6SelectViewLogic.DotX, V6SelectViewLogic.DotY,
                V6SelectViewLogic.DotActiveSize, total);

            int active = -1;
            if (data.Selected != null && all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != null && all[i].SongId == data.Selected.SongId) { active = i; break; }
                }
            }
            active = V6SelectViewLogic.DotActiveIndex(active, count);

            float[] offsets = V6SelectViewLogic.DotOffsets(count, 0f);
            for (int i = 0; i < count; i++)
            {
                float d = i == active ? V6SelectViewLogic.DotActiveSize : V6SelectViewLogic.DotSize;
                var dot = V5Ui.Panel(dots, "Dot " + (i + 1), new Vector2(d, d),
                    i == active ? V5Tokens.Accent : FromHex("#FFFFFF26"), V6SelectViewLogic.DotRadius);
                At(dot, V6SelectViewLogic.DotRectX(d), offsets[i]);
            }
        }

        // --- 中央列: 選択中の曲 (dRztp, $k2 r16) ---

        private void BuildCenter()
        {
            var panel = At(V5Ui.Panel(frame.gameObject, "Selected Song Panel", new Vector2(CenterW, ListH),
                V5Tokens.Key2, 16f), MainX + CenterX, MainY);
            var song = data.Selected;
            bool hasSong = song != null;

            var hero = At(V5Ui.Panel(panel, "Hero", new Vector2(512f, 288f),
                hasSong ? V5Tokens.Key3 : V5Tokens.Key3, 8f), 12f, 24f);
            if (hasSong)
            {
                SetJacket(hero, song);
            }

            var meta = V5Ui.Node(panel, "Meta");
            At(meta.GetComponent<RectTransform>(), 12f, 332f, 512f, 100f);
            if (hasSong)
            {
                var title = V5GameUi.Text(meta, "Title", song.Title, 48f, V5Tokens.Text,
                    TextAlignmentOptions.Center, FontStyles.Bold);
                title.rectTransform.sizeDelta = new Vector2(512f, 56f);
                var artistRow = V5GameUi.Row(meta, "Artist Row", 10f, TextAnchor.MiddleCenter);
                At(artistRow.GetComponent<RectTransform>(), 0f, 60f, 512f, 36f);
                var tag = V5Ui.Panel(artistRow.gameObject, "Tag No", new Vector2(64f, 30f), V5Tokens.Key4, 4f);
                var tagText = V5GameUi.Text(tag, "Text", "No." + song.SongId, 20f, V5Tokens.TextMuted,
                    TextAlignmentOptions.Center);
                Stretch(tagText.GetComponent<RectTransform>(), 64f, 30f);
                V5GameUi.Text(artistRow.gameObject, "Artist", song.Artist, 26f, V5Tokens.TextMuted);
            }
            else
            {
                At(V5Ui.Panel(meta, "Skeleton Title", new Vector2(280f, 44f), V5Tokens.Key3, 4f), 116f, 10f);
                At(V5Ui.Panel(meta, "Skeleton Artist", new Vector2(160f, 24f), V5Tokens.Key3, 4f), 176f, 62f);
            }

            var stats = At(V5Ui.Panel(panel, "Stats", new Vector2(512f, 196f), V5Tokens.Key1, 8f), 12f, 452f);
            BuildStat(stats, 0f, 0f, "BPM", hasSong && song.Bpm > 0
                ? Mathf.RoundToInt((float)song.Bpm).ToString() : "–");
            BuildStat(stats, 256f, 0f, "NOTES", hasSong && song.NoteCount > 0
                ? song.NoteCount.ToString() : "–");
            BuildStat(stats, 0f, 98f, "LEVEL", hasSong && song.Level > 0
                ? song.Level.ToString() : "–");
            BuildStat(stats, 256f, 98f, "BEST", hasSong
                ? V6SelectViewLogic.FormatScore(song.BestScore) : "–");
            At(V5Ui.Panel(stats, "Divider V", new Vector2(1f, 196f), FromHex("#FFFFFF1F"), 0f), 256f, 0f);
            At(V5Ui.Panel(stats, "Divider H", new Vector2(512f, 1f), FromHex("#FFFFFF1F"), 0f), 0f, 98f);

            // Controls (skip-back / play / skip-forward): 実在する操作だけに結線する
            // (#221 Task 3: 押せない疑似ボタンは禁止)。曲送りは data.MoveSelection、
            // play は Ready (フッターの Ready ボタンと同じ経路)。プレビュー再生の
            // ような新しい操作は作らない (Issue #222 スコープ訂正 2026-11-08)。
            var controls = V5Ui.Node(panel, "Controls");
            At(controls.GetComponent<RectTransform>(), 12f, 668f, 512f, 88f);
            var controlDefs = V6SelectViewLogic.CenterControls();
            for (int i = 0; i < controlDefs.Length; i++)
            {
                BuildControlButton(controls, controlDefs[i]);
            }
        }

        /// <summary>
        /// 統計セル (BPM / NOTES / LEVEL / BEST)。design は「ラベル左 / 値右」の
        /// 2 要素だけなので、LayoutGroup ではなく絶対位置で置く
        /// (LayoutGroup だと値の幅が計算されず、BPM の値だけ描画されない)。
        /// </summary>
        private void BuildStat(GameObject stats, float x, float y, string label, string value)
        {
            var cell = V5Ui.Node(stats, "Stat " + label);
            At(cell.GetComponent<RectTransform>(), x, y, 256f, 98f);

            var labelText = V5GameUi.Text(cell.gameObject, "Label", label, 22f, V5Tokens.TextDim);
            var labelRt = labelText.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0.5f);
            labelRt.anchorMax = new Vector2(0f, 0.5f);
            labelRt.pivot = new Vector2(0f, 0.5f);
            labelRt.anchoredPosition = new Vector2(16f, 0f);
            labelRt.sizeDelta = new Vector2(160f, 30f);

            var valueText = V5GameUi.Text(cell.gameObject, "Value", value, 32f, V5Tokens.Text,
                TextAlignmentOptions.Right, FontStyles.Normal);
            var valueRt = valueText.GetComponent<RectTransform>();
            valueRt.anchorMin = new Vector2(1f, 0.5f);
            valueRt.anchorMax = new Vector2(1f, 0.5f);
            valueRt.pivot = new Vector2(1f, 0.5f);
            valueRt.anchoredPosition = new Vector2(-16f, 0f);
            valueRt.sizeDelta = new Vector2(200f, 44f);
        }

        /// <summary>中央パネルの Controls 行のボタン 1 個分。実操作へ結線する (onClick null は作らない)。</summary>
        private void BuildControlButton(GameObject controls, V6SelectViewLogic.V6CenterControlDef def)
        {
            float h = def.Size;
            Color bg = def.Primary ? V5Tokens.ButtonPrimaryBg : V5Tokens.Key1;
            Action onClick;
            switch (def.Action)
            {
                case V6SelectViewLogic.V6SelectControlAction.MovePrev:
                    onClick = () => data.MoveSelection(-1);
                    break;
                case V6SelectViewLogic.V6SelectControlAction.MoveNext:
                    onClick = () => data.MoveSelection(1);
                    break;
                case V6SelectViewLogic.V6SelectControlAction.Ready:
                    // Ready は絞り込みで隠れた選択には効かない (フッターと同じ規約)。
                    onClick = ReadyAvailable() ? (Action)(() =>
                    {
                        if (onReady != null) onReady();
                    }) : null;
                    break;
                default:
                    // 実在しない操作のボタンは作らない (疑似ボタン禁止)。
                    return;
            }

            var btn = Button(controls, "Btn " + def.Icon, def.Size, def.Size, def.Size * 0.5f, bg, onClick);
            At(btn, def.X, (88f - def.Size) * 0.5f);
            V6Icons.Create(btn, def.Icon, def.Primary ? 36f : 32f,
                def.Primary ? V5Tokens.ButtonPrimaryLabel : V5Tokens.TextMuted, def.Icon);
        }

        // --- 右列: BEST / SCORE RANKING / PLAYERS ---

        private void BuildRight()
        {
            var column = V5Ui.Node(frame.gameObject, "Ranking Column");
            At(column.GetComponent<RectTransform>(), MainX + RightX, MainY, RightW, ListH);

            var best = At(V5Ui.Panel(column, "Best", new Vector2(528f, 112f), Clear(), 0f), 16f, 24f);
            var bestLabel = V5GameUi.Text(best.gameObject, "Label", "BEST", 22f, V5Tokens.TextDim);
            At(bestLabel, 0f, 0f, 528f, 22f);

            var scoreRow = V5GameUi.Row(best.gameObject, "Score Row", 12f, TextAnchor.MiddleLeft);
            At(scoreRow.GetComponent<RectTransform>(), 0f, 30f, 528f, 72f);
            var song = data.Selected;
            V5GameUi.Text(scoreRow.gameObject, "Score",
                song != null ? V6SelectViewLogic.FormatScore(song.BestScore) : "–", 56f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            var spacer = V5Ui.Node(scoreRow.gameObject, "Spacer");
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1f;
            var gradeChip = At(V5Ui.Panel(scoreRow.gameObject, "Grade", new Vector2(72f, 64f), V5Tokens.Key6, 8f),
                0f, 0f);
            var gradeText = V5GameUi.Text(gradeChip.gameObject, "Text",
                song != null && !string.IsNullOrEmpty(song.BestGrade) ? song.BestGrade : "–", 34f, V5Tokens.Text,
                TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(gradeText.GetComponent<RectTransform>(), 72f, 64f);

            // SCORE RANKING (528x300, $k2 r8)
            var ranking = At(V5Ui.Panel(column, "Score Ranking", new Vector2(528f, 300f), V5Tokens.Key2, 8f),
                16f, 152f);
            AddScrollCatcher(ranking, 1);
            var head = V5GameUi.Row(ranking.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            At(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 36f);
            V5GameUi.Text(head.gameObject, "Label", "SCORE RANKING", 18f, V5Tokens.TextDim);
            var headSpacer = V5Ui.Node(head.gameObject, "Spacer");
            var headSpacerLe = headSpacer.AddComponent<LayoutElement>();
            headSpacerLe.flexibleWidth = 1f;
            // ランキングの source タグは実データから決める (#221)。
            // ローカル曲に「オンライン」と表示するのは嘘なので、IsCloud をそのまま出す。
            var source = V5Ui.Panel(head.gameObject, "Source", new Vector2(96f, 28f), V5Tokens.Key4, 4f);
            var sourceText = V5GameUi.Text(source, "Text", RankingSourceLabel(song), 18f, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            Stretch(sourceText.GetComponent<RectTransform>(), 96f, 28f);

            var rows = data.Selected != null ? data.RankingFor(data.Selected) : null;
            if (rows == null || rows.Count == 0)
            {
                // 空状態も実データの状態を言う (固定の "NO DATA" で誤魔化さない)。
                var empty = V5GameUi.Text(ranking.gameObject, "No Data", RankingEmptyText(song), 24f,
                    V5Tokens.TextDim, TextAlignmentOptions.Left);
                At(empty, 24f, 92f, 496f, 44f, new Vector2(0f, 0.5f));
            }
            else
            {
                int first = rankingScroll;
                int last = Mathf.Min(rows.Count, first + RankingVisibleRows);
                for (int i = first; i < last; i++)
                {
                    BuildRankingRow(ranking, rows[i], i, 92f + 52f * (i - first));
                }
            }

            // PLAYERS (528x288, $k2 r8): IV6SelectDataSource にプレイヤー集計の
            // データ源が無い。実データを出せないので「データ無し」と正直に言う
            // (#221: 固定文字列を実データに見せかけない)。
            var players = At(V5Ui.Panel(column, "Players", new Vector2(528f, 288f), V5Tokens.Key2, 8f), 16f, 468f);
            var playersHead = V5GameUi.Row(players.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            At(playersHead.GetComponent<RectTransform>(), 8f, 8f, 496f, 36f);
            V5GameUi.Text(playersHead.gameObject, "Label", "PLAYERS", 18f, V5Tokens.TextDim);
            var playersSpacer = V5Ui.Node(playersHead.gameObject, "Spacer");
            var playersSpacerLe = playersSpacer.AddComponent<LayoutElement>();
            playersSpacerLe.flexibleWidth = 1f;
            V5GameUi.Text(playersHead.gameObject, "Mode", "累計スコア", 18f, V5Tokens.Text, TextAlignmentOptions.Right,
                FontStyles.Normal);
            var noPlayers = V5GameUi.Text(players.gameObject, "No Data", "データがありません", 24f,
                V5Tokens.TextDim, TextAlignmentOptions.Left);
            At(noPlayers, 24f, 60f, 496f, 44f, new Vector2(0f, 0.5f));
        }

        /// <summary>ランキングの source タグ表示。実データ (クラウド配信か) から決める。</summary>
        private static string RankingSourceLabel(V6SongEntry song)
        {
            if (song == null) return "–";
            return song.IsCloud ? "オンライン" : "ローカル";
        }

        /// <summary>ランキングが空のときの文言。ローカル曲はオンラインランキングが存在しない旨を言う。</summary>
        private string RankingEmptyText(V6SongEntry song)
        {
            if (song != null && !song.IsCloud)
            {
                return "ローカル曲のランキングはありません";
            }

            if (data.HasError && !string.IsNullOrEmpty(data.StatusMessage))
            {
                return data.StatusMessage;
            }

            return "ランキングのデータがありません";
        }

        private void BuildRankingRow(GameObject ranking, V6RankingRow row, int index, float y)
        {
            var line = At(V5Ui.Panel(ranking, "Score " + (index + 1), new Vector2(496f, 44f),
                row.IsYou ? V5Tokens.Key6 : V5Tokens.Key1, 8f), 8f, y);

            var pos = At(V5Ui.Panel(line, "Pos", new Vector2(36f, 36f), V5Tokens.Key4, 18f), 4f, 4f);
            var posText = V5GameUi.Text(pos, "Num", row.Position.ToString(), 22f,
                row.IsYou ? V5Tokens.Text : V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(posText.GetComponent<RectTransform>(), 36f, 36f);

            var name = V5GameUi.Text(line.gameObject, "Name", row.IsYou ? "YOU" : row.PlayerName, 22f, V5Tokens.Text);
            At(name, 52f, 0f, 140f, 44f, new Vector2(0f, 0.5f));

            var score = V5GameUi.Text(line.gameObject, "Score", V6SelectViewLogic.FormatScore(row.Score), 22f, V5Tokens.Text,
                TextAlignmentOptions.Right);
            At(score, 200f, 0f, 180f, 44f, new Vector2(0f, 0.5f));

            var grade = At(V5Ui.Panel(line, "Grade", new Vector2(56f, 36f), V5Tokens.Key4, 4f), 436f, 4f);
            var gradeText = V5GameUi.Text(grade.gameObject, "Text", string.IsNullOrEmpty(row.Grade) ? "–" : row.Grade, 22f,
                V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(gradeText.GetComponent<RectTransform>(), 56f, 36f);
        }

        // --- 設定 (wSjAm 右カラム差し替え。モーダル/Scrim は使わない) ---

        private void OpenSettings()
        {
            if (onOpenSettings != null)
            {
                onOpenSettings();
                return;
            }

            settingsOpen = true;
            Rebuild();
        }

        private void CloseSettings()
        {
            settingsOpen = false;
            Rebuild();
        }

        /// <summary>
        /// 右カラム (Ranking カラムの位置) を「このプレイの設定」パネルへ差し替える。
        /// design wSjAm: Head (24,8) fs26 w500、行 512×56 (背景 #2D2D2D r8) を
        /// 68 間隔で 3 行 (Key Guide / Lyrics / NoteSpeed)。
        /// </summary>
        private void BuildSettings()
        {
            var column = At(V5Ui.Panel(frame.gameObject, "Settings Column", new Vector2(RightW, ListH),
                Clear(), 0f), MainX + RightX, MainY);

            // 契約の w500 は既定フォント (NotoSansJP-Medium) 自身のウェイトなので Normal。
            var head = V5GameUi.Text(column.gameObject, "Head", "このプレイの設定", 26f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Normal);
            At(head, V6SelectViewLogic.SettingsHeadX, V6SelectViewLogic.SettingsHeadY,
                240f, 36f);

            BuildSettingsRow(column, 0, "Key Guide", PlaySession.KeyGuideLabel(PlaySession.KeyGuide),
                d => PlaySession.SetKeyGuide(PlaySession.NextKeyGuide(PlaySession.KeyGuide, d)));
            BuildSettingsRow(column, 1, "Lyrics", PlaySession.LyricsLabel(PlaySession.LyricsEnabled),
                d => PlaySession.SetLyricsEnabled(!PlaySession.LyricsEnabled));
            BuildSettingsRow(column, 2, "NoteSpeed", Mathf.RoundToInt(GManager.noteSpeed).ToString(),
                d =>
                {
                    float value = Mathf.RoundToInt(GManager.noteSpeed) + d;
                    GManager.noteSpeed = Mathf.Clamp(value, OptionSettings.NoteSpeedMin,
                        OptionSettings.NoteSpeedMax);
                    OptionSettings.Save();
                });
        }

        /// <summary>設定行 1 個分 (icon + label + ‹ › + 値)。位置は行 index から決める。</summary>
        private void BuildSettingsRow(GameObject column, int index, string label, string value, Action<int> adjust)
        {
            var rect = V6SelectViewLogic.SettingsRowRect(index);
            var row = At(V5Ui.Panel(column, "Set " + label, new Vector2(rect.W, rect.H),
                V5Tokens.Base, FlashBeat.UI.V6.Common.V6Tokens.RadiusMedium), rect.X, rect.Y);

            V6Icons.Create(row, "list-filter", 24f, V5Tokens.TextMuted);
            var icon = row.transform.Find("Icon");
            if (icon != null)
            {
                var iconRt = icon.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = new Vector2(12f, 0f);
            }

            var labelText = V5GameUi.Text(row.gameObject, "Label", label, 24f, V5Tokens.Text);
            At(labelText, 48f, 0f, 240f, rect.H, new Vector2(0f, 0.5f));

            var left = Button(row, "Prev", 40f, 40f, 8f, V5Tokens.Key4, () => adjust(-1));
            At(left, 300f, (rect.H - 40f) * 0.5f);
            V6Icons.Create(left, "chevron-left", 20f, V5Tokens.TextMuted);

            var valueText = V5GameUi.Text(row.gameObject, "Value", value, 24f, V5Tokens.Text,
                TextAlignmentOptions.Center);
            At(valueText, 352f, 0f, 96f, rect.H, new Vector2(0f, 0.5f));

            var right = Button(row, "Next", 40f, 40f, 8f, V5Tokens.Key4, () => adjust(1));
            At(right, 460f, (rect.H - 40f) * 0.5f);
            V6Icons.Create(right, "chevron-right", 20f, V5Tokens.TextMuted);
        }

        // --- 入力ヘルパ ---

        private void ScrollList(int delta)
        {
            // スクロール (ホイール / ボタン) は選択曲を delta だけ動かす操作。
            // スクロール位置は Rebuild で選択曲から常に再計算される (中央固定)。
            data.MoveSelection(delta);
            Rebuild();
        }

        internal void OnScroll(int target, float delta)
        {
            int step = delta > 0f ? -1 : 1;
            if (target == 0)
            {
                ScrollList(step);
            }
            else
            {
                rankingScroll = V6SelectViewLogic.ClampScroll(rankingScroll + step,
                    data.Selected != null ? data.RankingFor(data.Selected).Count : 0, RankingVisibleRows);
                Rebuild();
            }
        }

        private int SelectedIndex(IReadOnlyList<V6SongEntry> songs)
        {
            var selected = data.Selected;
            if (selected == null)
            {
                return -1;
            }

            for (int i = 0; i < songs.Count; i++)
            {
                if (songs[i].SongId == selected.SongId)
                {
                    return i;
                }
            }

            return -1;
        }

        private string SearchDisplayText()
        {
            string query = data.Query ?? "";
            if (searchFocused)
            {
                return query + "|";
            }

            return query.Length > 0 ? query : "タイプして検索";
        }

        private void SetJacket(GameObject thumbPanel, V6SongEntry song)
        {
            var img = thumbPanel.GetComponent<Image>();
            Sprite sprite = song == null ? null : V6JacketResources.LoadSprite(song.SongId, song.Title);

            if (sprite != null)
            {
                img.type = Image.Type.Simple;
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                // ジャケット無しは空枠 (AqFtx の Thumb と同じ $k3)。
                img.type = Image.Type.Sliced;
                img.sprite = V5Ui.RoundedSprite(4);
                img.color = V5Tokens.Key3;
            }
        }

        // --- uGUI ユーティリティ (design px 左上原点) ---

        /// <summary>
        /// キーキャップ (design 空間のフォントサイズと幅で自前で組む)。
        /// V5Ui.KeyCap は UISizeProfile.Current.FsS (解像度依存) をラベルに使うため、
        /// 実解像度でキャップの中で折り返していた。v6 は design px で決める。
        /// </summary>
        private static GameObject KeyCap(GameObject parent, string label, float fontSize, float width, float height)
        {
            var cap = V5Ui.Panel(parent, "KeyCap " + label, new Vector2(width, height), V5Tokens.ControlBg,
                V5Tokens.RadiusSmall);
            var tmp = V5GameUi.Text(cap, "Key", label, fontSize, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            SingleLine(tmp);
            var rt = tmp.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return cap;
        }

        /// <summary>折り返しを禁じて 1 行に収める (はみ出しても段落と重ならない)。</summary>
        private static void SingleLine(TextMeshProUGUI tmp)
        {
            if (tmp == null)
            {
                return;
            }

            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>長すぎる文字は 1 行に収めて … で省略する (親からはみ出さない)。</summary>
        private static void Ellipsis(TextMeshProUGUI tmp)
        {
            if (tmp == null)
            {
                return;
            }

            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
        }

        /// <summary>
        /// frame 左上原点の design px で配置し、配置した GameObject を返す
        /// (返り値をそのまま V5Ui.Panel / V5GameUi.Text の親にできる)。
        /// </summary>
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

        /// <summary>親 (row 等) の中央に stretch する (Row/Column の子として使う)。</summary>
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
            return MakeViewButton(go, w, h, onClick);
        }

        private static GameObject Button(Component parent, string name, float w, float h, float radius, Color bg,
            Action onClick)
        {
            var go = V5Ui.Panel(parent.gameObject, name, new Vector2(w, h), bg, radius);
            return MakeViewButton(go, w, h, onClick);
        }

        private static GameObject MakeViewButton(GameObject go, float w, float h, Action onClick)
        {
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

        private void AddScrollCatcher(GameObject target, int targetId)
        {
            var catcher = target.AddComponent<V6SelectScrollCatcher>();
            catcher.View = this;
            catcher.Target = targetId;
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
