using System;
using System.Collections.Generic;
using FlashBeat.Update;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;

namespace FlashBeat.UI.V6.Title
{
    /// <summary>
    /// Title 画面の v6 UI。design/flashbeat-ui.pen v6 の
    /// Title · v6 (lyK9E) → 01 Title · v6 (QJtTv) を実行時に構築する。
    ///
    /// - 既存のタイトル (VideoPlayer "Screen" の動画) は消さない。この view は
    ///   動画の上に重ねる overlay で、背景は透明 (design の各 Zone は fill なし)。
    /// - design の Logo (CnRJj / assets/13.png) は描かない: タイトル動画自体が
    ///   ロゴを映しており、repo に 13.png は無い (上から重ねると二重に見える)。
    /// - 実データ: 曲数 / カタログ状態は GManager (ActiveSongs / HasRemoteCatalog)、
    ///   右上の曲情報 (自己ベスト) は GManager のカタログと保存済みベストスコア
    ///   (GManager.BestScoreOf)、バージョンと更新状態は Application.version と
    ///   UpdateChecker.Model。design 例の固定値 (v1.1.0 → v1.2.0 等) は決して出さない。
    /// - キーのキャップ (Space / O / E ...) は実際の割り当て (KeyActions) から取る。
    /// - 構築はコードのみ (シーン YAML / Prefab / .meta は触らない)。
    /// - 寸法は design px (1728x972 基準)。frame を u = min(W/1728, H/972) 倍して
    ///   3 解像度 (FHD / 2K / Mac) でクリップしないようにする
    ///   (v6 HUD / Select と同じ方式)。色は V5Tokens を流用する。
    /// - キー入力は読まない: 選択の移動 (MoveSelection) と決定 (ActivateSelected) は
    ///   既存の TitleSceneManager.Update (KeyActions) が受け持ち、遷移は
    ///   onStart / onOption / onExit コールバックで統合担当が渡す。
    ///   History / Ranking タブは操作が設定されているときだけ描く
    ///   (押しても何も起きないボタンを作らない)。
    /// </summary>
    public sealed class V6TitleView
    {
        /// <summary>現在生成されている v6 タイトル (タイトルシーンに 1 個だけ)。</summary>
        public static V6TitleView Current { get; private set; }

        /// <summary>
        /// v6 のタイトルを使うか (統合担当が切り替える。既定 true)。
        ///
        /// false のときは旧 (シーン配置) のタイトルをそのまま動かす。
        /// V6UiMode は統合担当が所有しているため、この画面固有のフラグは
        /// ここに持つ (V6ResultView.Enabled と同じ運用)。
        /// </summary>
        public static bool Enabled = true;

        // --- design px 定数 (QJtTv / lyK9e 実測)。フレーム基準サイズは V6TitleLogic。---

        // Header / Footer は (24, 0) / (24, 876) 1680x96。Main は (24, 96) 1680x780。
        private const float HeaderX = 24f, HeaderY = 0f, HeaderW = 1680f, HeaderH = 96f;
        private const float FooterX = 24f, FooterY = 876f, FooterW = 1680f, FooterH = 96f;

        // Controls · タブ (FHcxk): main-local (280, 552) 1120x48、4 タブ各 280x48。
        private const float TabsX = 280f, TabsY = 552f, TabsW = 1120f, TabsH = 48f;
        private const float TabW = 280f;

        private readonly Action onStart;
        private readonly Action onOption;
        private readonly Action onExit;
        private Action onHistory;
        private Action onRanking;

        /// <summary>タブ 1 件の表示情報 (ラベル / 実キー / 操作)。</summary>
        private sealed class Tab
        {
            public string Name;
            public string Icon;
            public string Label;
            public string Key;
            public Action Action;
        }

        /// <summary>今あるタブ (操作が設定されているものだけ)。</summary>
        private readonly List<Tab> tabList = new List<Tab>();

        /// <summary>キーボードで選択中のタブ。</summary>
        private int selectedTab;

        private GameObject rootGo;
        private RectTransform frame;

        private float u = 1f;
        private int lastWidth;
        private int lastHeight;

        // 更新状態の前回表示値 (変化したときだけ再描画する)。
        private string lastUpdateStateKey = "";

        private V6TitleView(Action onStart, Action onOption, Action onExit,
            Action onHistory, Action onRanking)
        {
            this.onStart = onStart;
            this.onOption = onOption;
            this.onExit = onExit;
            this.onHistory = onHistory;
            this.onRanking = onRanking;
        }

        /// <summary>View を生成する。統合担当 (TitleSceneManager 側) から呼ぶ。</summary>
        public static V6TitleView Create(RectTransform parent, Action onStart, Action onOption, Action onExit,
            Action onHistory = null, Action onRanking = null)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var view = new V6TitleView(onStart, onOption, onExit, onHistory, onRanking);
            view.BuildRoot(parent);
            view.Rebuild();
            Current = view;
            return view;
        }

        public RectTransform Root => frame;

        /// <summary>実データ (曲数 / カタログ状態 / 更新状態) から表示を作り直す。</summary>
        public void Refresh()
        {
            Rebuild();
        }

        /// <summary>
        /// History / Ranking タブの操作を設定する (統合担当が渡す)。
        /// null のタブは描かない: 押しても何も起きないボタンを作らない。
        /// </summary>
        public void SetPanelHandlers(Action history, Action ranking)
        {
            if (onHistory == history && onRanking == ranking)
            {
                return;
            }

            onHistory = history;
            onRanking = ranking;
            Rebuild();
        }

        /// <summary>タブ選択を delta 行だけ動かす (端で折り返す)。</summary>
        public void MoveSelection(int delta)
        {
            if (tabList.Count == 0)
            {
                return;
            }

            selectedTab = V6TitleLogic.MoveIndex(selectedTab, delta, tabList.Count);
            Rebuild();
        }

        /// <summary>選択中のタブを実行する (キーボードの決定)。</summary>
        public void ActivateSelected()
        {
            if (tabList.Count == 0)
            {
                return;
            }

            Tab tab = tabList[selectedTab];
            if (tab != null && tab.Action != null)
            {
                tab.Action();
            }
        }

        /// <summary>選択中のタブのラベル (テスト / 統合担当向け)。</summary>
        public string SelectedTabLabel
        {
            get
            {
                if (selectedTab < 0 || selectedTab >= tabList.Count)
                {
                    return "";
                }

                return tabList[selectedTab].Label;
            }
        }

        public void SetVisible(bool visible)
        {
            if (rootGo != null)
            {
                rootGo.SetActive(visible);
            }
        }

        /// <summary>生成したビューを破棄するとき呼ぶ (V6SelectView.Dispose と同じ契約)。</summary>
        public void Dispose()
        {
            if (rootGo != null)
            {
                V5GameUi.DestroyNow(rootGo);
            }

            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }
        }

        // --- 毎フレーム (V6TitleViewDriver から呼ばれる) ---

        internal void Tick()
        {
            if (rootGo == null || !rootGo.activeInHierarchy)
            {
                return;
            }

            UpdateScale();

            // 更新フロー (チェック / ダウンロード進捗 / 適用待ち) は時間で変わるので
            // 状態キーが変わったときだけ作り直す。
            string key = UpdateStateKey();
            if (key != lastUpdateStateKey)
            {
                Rebuild();
            }
        }

        // --- 更新フローの実データ (UpdateNoticeUI と同じ供給経路) ---

        private static UpdateFlowModel UpdateModel()
        {
            UpdateChecker.EnsureExists();
            var checker = UnityEngine.Object.FindAnyObjectByType<UpdateChecker>();
            return checker != null ? checker.Model : null;
        }

        private string UpdateStateKey()
        {
            UpdateFlowModel model = UpdateModel();
            if (model == null)
            {
                return "none";
            }

            return model.Phase + "|" + (model.LatestVersion ?? "") + "|"
                + Mathf.RoundToInt(model.DownloadProgress * 100f);
        }

        /// <summary>更新ボタンのクリック。UpdateNoticeUI.Trigger と同じ分岐
        /// (Available → ダウンロード、Ready → 適用) を既存 API で実行する。</summary>
        private static void TriggerUpdate()
        {
            var notice = UnityEngine.Object.FindAnyObjectByType<UpdateNoticeUI>();
            if (notice != null)
            {
                notice.Trigger();
            }
        }

        // --- 構築 ---

        private void BuildRoot(RectTransform parent)
        {
            rootGo = V5Ui.Node(parent.gameObject, "V6 Title View");
            var rt = rootGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            frame = V5Ui.Node(rootGo, "V6 Title Frame 1728x972").GetComponent<RectTransform>();
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(V6TitleLogic.FrameW, V6TitleLogic.FrameH);

            var driver = rootGo.AddComponent<V6TitleViewDriver>();
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
            u = V6TitleLogic.DesignScale(w, h);
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
            lastUpdateStateKey = UpdateStateKey();

            // frame の全子を作り直す (V6SelectView.Rebuild と同じ方針)。
            var children = new List<GameObject>();
            foreach (Transform child in frame.transform)
            {
                children.Add(child.gameObject);
            }

            for (int i = 0; i < children.Count; i++)
            {
                V5GameUi.DestroyNow(children[i]);
            }

            BuildHeader();
            BuildTabList();
            BuildMain();
            BuildFooter();
        }

        // --- ヘッダー (x24 y0 1680x96) ---

        private void BuildHeader()
        {
            var header = At(V5Ui.Panel(frame.gameObject, "Header", new Vector2(HeaderW, HeaderH), Clear(), 0f),
                HeaderX, HeaderY);

            // 左上: 終了 (icon button + 実キーのキャップ + "Exit")。design lyK9E の zUcsi 差し替え
            // (Back button → Exit)。タイトルテキスト (ci867) は design で enabled:false
            // (動画がロゴを映す) のため作らない。
            // アイコンは "×" (U+00D7)。"✕" (U+2715) は NotoSansJP に字形が無く豆腐になる
            // (v5 タイトルと同じ判断: フォントに無い字形は描かない)。
            var exit = At(V5GameUi.Row(header.gameObject, "Zone Exit", 12f, TextAnchor.MiddleLeft), 0f, 20f, 400f, 56f);
            var exitButton = Button(exit.gameObject, "Exit Button", 56f, 56f, 8f, V5Tokens.Key4, onExit);
            var exitIcon = V5GameUi.Text(exitButton, "Icon", "×", 32f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(exitIcon.GetComponent<RectTransform>(), 56f, 56f);
            string exitKey = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Exit);
            if (!string.IsNullOrEmpty(exitKey))
            {
                V6Ui.KeyCap(exit.gameObject, exitKey, new Vector2(44f, 36f));
            }
            var exitLabel = V5GameUi.Text(exit.gameObject, "Label", "Exit", 30f, V5Tokens.TextMuted,
                TextAlignmentOptions.Midline | TextAlignmentOptions.Left);
            exitLabel.rectTransform.sizeDelta = new Vector2(160f, 56f);

            UpdateFlowModel model = UpdateModel();

            // 更新ボタン (更新があるときだけ)。design の右上には無いが、更新の導線は
            // Title にしか無いので、同じヘッダー帯の左側に置く (右側は曲情報 / 状態)。
            if (model != null && V6TitleLogic.ShowUpdateButton(model.Phase))
            {
                var update = Button(header.gameObject, "Update Button", 460f, 56f, 8f, V5Tokens.Key4, TriggerUpdate);
                At(update, 340f, 20f, 460f, 56f);
                var updateRow = V5GameUi.Row(update, "Inner", 12f, TextAnchor.MiddleCenter);
                Stretch(updateRow.GetComponent<RectTransform>(), 460f, 56f);
                var updateLabel = V5GameUi.Text(updateRow.gameObject, "Label", V6TitleLogic.UpdateButtonLabel(model.Phase,
                    model.DownloadProgress), 26f, V5Tokens.Text, TextAlignmentOptions.Left);
                updateLabel.rectTransform.sizeDelta = new Vector2(360f, 34f);
                var updateSpacer = V5Ui.Node(updateRow.gameObject, "Spacer");
                var updateSpacerLe = updateSpacer.AddComponent<LayoutElement>();
                updateSpacerLe.flexibleWidth = 1f;
                var cap = V5Ui.Panel(updateRow.gameObject, "KeyCap F3", new Vector2(48f, 32f),
                    new Color(0f, 0f, 0f, 0.25f), 4f);
                var capText = V5GameUi.Text(cap, "Key", "F3", 22f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
                Stretch(capText.GetComponent<RectTransform>(), 48f, 32f);
            }

            // 右上: 曲情報 (design "Zone ・曲情報 (右上)" = 情報ゾーン内 x240 / 300x96)。
            // 中身は実データだけ: カタログの曲名 / アーティストと、その曲のベストスコア。
            // 記録が 1 件も無いときは数値を出さず「まだ記録がありません」とだけ書く。
            var songZone = At(V5Ui.Panel(header.gameObject, "Zone Song", new Vector2(300f, HeaderH), Clear(), 0f),
                1080f, 0f);
            V6TitleLogic.V6TitleRecord record = BestRecordNow();
            var recordLabel = V5GameUi.Text(songZone.gameObject, "Record Label", "自己ベスト", 16f, V5Tokens.TextMuted,
                TextAlignmentOptions.Left);
            At(recordLabel, 0f, 12f, 160f, 24f);
            if (record.HasRecord)
            {
                var score = V5GameUi.Text(songZone.gameObject, "Record Score", V6TitleLogic.ScoreText(record.Score),
                    22f, V5Tokens.Text, TextAlignmentOptions.Right, FontStyles.Bold);
                At(score, 140f, 12f, 160f, 28f);
                var songTitle = V5GameUi.Text(songZone.gameObject, "Song Title", record.Title, 24f, V5Tokens.Text,
                    TextAlignmentOptions.Left);
                At(songTitle, 0f, 42f, 300f, 30f);
                var artist = V5GameUi.Text(songZone.gameObject, "Song Artist", record.Artist, 16f, V5Tokens.TextMuted,
                    TextAlignmentOptions.Left);
                At(artist, 0f, 72f, 300f, 24f);
            }
            else
            {
                var empty = V5GameUi.Text(songZone.gameObject, "No Record", V6TitleLogic.NoRecordText, 20f,
                    V5Tokens.TextDim, TextAlignmentOptions.Left);
                At(empty, 0f, 42f, 300f, 28f);
            }

            // 右上: 状態 (design "Zone ・状態 (右上)" = 情報ゾーン内 x540 / 300x96)。
            // カタログ状態 (曲数 + クラウド / ローカル) とバージョン (実データ)。
            var stateZone = At(V5Ui.Panel(header.gameObject, "Zone State", new Vector2(300f, HeaderH), Clear(), 0f),
                1380f, 0f);
            int songCount = V6TitleLogic.PlayableSongCount(GManager.ActiveSongs);
            var status = V5GameUi.Text(stateZone.gameObject, "Catalog Status",
                V6TitleLogic.CatalogStatusText(GManager.HasRemoteCatalog, songCount), 18f, V5Tokens.TextDim,
                TextAlignmentOptions.Left);
            At(status, 0f, 20f, 300f, 28f);
            var currentVersion = V5GameUi.Text(stateZone.gameObject, "Version",
                V6TitleLogic.VersionLabel(Application.version), 16f, V5Tokens.TextDim, TextAlignmentOptions.Left);
            At(currentVersion, 0f, 50f, 140f, 24f);
            if (model != null && V6TitleLogic.ShowNewVersion(model.Phase, model.LatestVersion))
            {
                var arrow = V5GameUi.Text(stateZone.gameObject, "Arrow", "→", 16f, V5Tokens.TextDim,
                    TextAlignmentOptions.Left);
                At(arrow, 140f, 50f, 24f, 24f);
                var newVersion = V5GameUi.Text(stateZone.gameObject, "New Version",
                    V6TitleLogic.VersionLabel(model.LatestVersion), 16f, V5Tokens.AccentLight,
                    TextAlignmentOptions.Left, FontStyles.Bold);
                At(newVersion, 164f, 50f, 136f, 24f);
            }
        }

        /// <summary>
        /// 自己ベスト (実データ) を今読み直す。
        /// 曲名 / アーティストは GManager のカタログ、最高スコアは保存済みの
        /// ベストスコア (GManager.BestScoreOf) からだけ取る。
        /// </summary>
        private static V6TitleLogic.V6TitleRecord BestRecordNow()
        {
            SongData[] songs = GManager.ActiveSongs;
            if (songs == null)
            {
                return default(V6TitleLogic.V6TitleRecord);
            }

            int[] scores = new int[songs.Length];
            for (int i = 1; i < songs.Length; i++)
            {
                SongData song = songs[i];
                if (song == null || song.unplayable)
                {
                    continue;
                }

                scores[i] = GManager.BestScoreOf(i);
            }

            return V6TitleLogic.BestRecord(songs, scores);
        }


        // --- メイン (x24 y96 1680x780) ---

        private void BuildMain()
        {
            var main = At(V5Ui.Panel(frame.gameObject, "Main", new Vector2(1680f, 780f), Clear(), 0f), 24f, 96f);

            // Logo (CnRJj) は描かない (動画がロゴを映す。クラスコメント参照)。

            // Controls · タブ (FHcxk): (280, 552) 1120x48, $overlay r8, タブ各 280x48。
            var tabs = At(V5Ui.Panel(main.gameObject, "Tabs", new Vector2(TabsW, TabsH), V5Tokens.Overlay,
                V5Tokens.RadiusMedium), TabsX, TabsY);

            for (int i = 0; i < tabList.Count; i++)
            {
                BuildTab(tabs, tabList[i], i);
            }
        }

        /// <summary>
        /// タブの一覧を作る。操作が設定されているものだけを並べる
        /// (押しても何も起きないボタンを作らない)。
        /// キーのキャップは実際の割り当て (KeyActions) から取る。
        /// </summary>
        private void BuildTabList()
        {
            tabList.Clear();
            tabList.Add(new Tab
            {
                Name = "Tab GameStart",
                Icon = "▶",
                Label = "GameStart",
                Key = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Start),
                Action = onStart,
            });

            if (onHistory != null)
            {
                tabList.Add(new Tab
                {
                    Name = "Tab History",
                    Icon = "≡",
                    Label = "History",
                    Key = "",
                    Action = onHistory,
                });
            }

            if (onRanking != null)
            {
                tabList.Add(new Tab
                {
                    Name = "Tab Ranking",
                    Icon = "★",
                    Label = "Ranking",
                    Key = "",
                    Action = onRanking,
                });
            }

            // Option のアイコン "⚙" (U+2699) は NotoSansJP に字形が無い (豆腐になる) ので
            // 描かない (v5 タイトルと同じ判断)。ラベルだけで懂了。
            tabList.Add(new Tab
            {
                Name = "Tab Option",
                Icon = "",
                Label = "Option",
                Key = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Option),
                Action = onOption,
            });

            if (selectedTab >= tabList.Count)
            {
                selectedTab = 0;
            }
        }

        private void BuildTab(GameObject tabs, Tab tab, int index)
        {
            bool selected = index == selectedTab;
            var tabGo = At(V5Ui.Panel(tabs, tab.Name, new Vector2(TabW, TabsH),
                selected ? V5Tokens.Key6 : Clear(), 0f), TabW * index, 0f);
            MakeButton(tabGo, tab.Action);

            var row = V5GameUi.Row(tabGo, "Inner", 8f, TextAnchor.MiddleLeft);
            Stretch(row.GetComponent<RectTransform>(), TabW, TabsH);
            row.padding = new RectOffset(10, 10, 0, 0);
            if (!string.IsNullOrEmpty(tab.Icon))
            {
                var icon = V5GameUi.Text(row.gameObject, "Icon", tab.Icon, 24f,
                    selected ? V5Tokens.Text : V5Tokens.TextMuted, TextAlignmentOptions.Center);
                icon.rectTransform.sizeDelta = new Vector2(32f, 32f);
            }
            var label = V5GameUi.Text(row.gameObject, "Label", tab.Label, 26f,
                selected ? V5Tokens.Text : V5Tokens.TextMuted, TextAlignmentOptions.Left);
            label.rectTransform.sizeDelta = new Vector2(160f, 34f);
            var spacer = V5Ui.Node(row.gameObject, "Spacer");
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1f;
            if (!string.IsNullOrEmpty(tab.Key))
            {
                var cap = V5Ui.Panel(row.gameObject, "KeyCap " + tab.Key, new Vector2(88f, 34f),
                    new Color(0f, 0f, 0f, 0.25f), 4f);
                var capText = V5GameUi.Text(cap, "Key", tab.Key, 22f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
                Stretch(capText.GetComponent<RectTransform>(), 88f, 34f);
            }
        }

        // --- フッター (x24 y876 1680x96) ---

        private void BuildFooter()
        {
            var footer = At(V5Ui.Panel(frame.gameObject, "Footer", new Vector2(FooterW, FooterH), Clear(), 0f),
                FooterX, FooterY);

            // 左下: キー操作の凡例 (design QQ4jM, gap 16)。決定 / ↑↓ 選択 / 終了。
            // (lyK9E で RpQyz のラベルは「選択」、e6RJ2 は「E / 終了」に差し替え済み。)
            // キーの文字は実際の割り当て (KeyActions) から取る。
            var legend = At(V5GameUi.Row(footer.gameObject, "Legend", 16f, TextAnchor.MiddleLeft),
                0f, 30f, 840f, 36f);
            string startKey = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Start);
            if (!string.IsNullOrEmpty(startKey))
            {
                V5GameUi.LegendItem(legend.gameObject, startKey, "決定");
            }
            V5GameUi.LegendItem(legend.gameObject, "↑↓", "選択");
            string exitKey = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Exit);
            if (!string.IsNullOrEmpty(exitKey))
            {
                V5GameUi.LegendItem(legend.gameObject, exitKey, "終了");
            }

            // 右下の操作ボタン (T8HuQS) は design lyK9E で enabled:false (タブに統合済み)。
        }

        // --- uGUI ユーティリティ (design px 左上原点, V6SelectView と同じ) ---

        private static GameObject At(GameObject go, float x, float y, float w, float h)
        {
            return At(go.GetComponent<RectTransform>(), x, y, w, h);
        }

        private static GameObject At(GameObject go, float x, float y)
        {
            return At(go.GetComponent<RectTransform>(), x, y);
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

        /// <summary>pivot 指定版 (左上 (x, -y) を維持する)。</summary>
        private static GameObject At(Component node, float x, float y, float w, float h, Vector2 pivot)
        {
            At(node, x, y, w, h);
            var rt = node.GetComponent<RectTransform>();
            rt.pivot = pivot;
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
            return MakeButton(go, onClick);
        }

        private static GameObject MakeButton(GameObject go, Action onClick)
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

        private static Color Clear()
        {
            return new Color(0f, 0f, 0f, 0f);
        }
    }
}
