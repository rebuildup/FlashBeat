using System;
using System.Collections.Generic;
using FlashBeat.Update;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Title
{
    /// <summary>
    /// Title 画面の v6 UI。design/flashbeat-ui.pen v6 の
    /// 01 Title · v6 (QJtTv) を実行時に構築する。
    ///
    /// - 共通フレームは V6Frame (1728×972、契約 §4) を使い、実画面へは
    ///   <see cref="V6TitleLogic.FrameOrigin"/> で 1:1 (等倍) 中央配置する。
    ///   引き伸ばし (等比スケール) はしない (design POJwG "Resolution Check · Title":
    ///   FHD (96,54) / 2K (416,234) / Mac (416,346))。フレーム外の画面外周には
    ///   不透明パネルを敷かない (design QJtTv の fill はゲーム自身の背景 = タイトル動画)。
    /// - design の Logo (CnRJj / assets/13.png) は
    ///   Assets/Game/Resources/V5/Common/13.png を描く (タイトル動画の上に重ねる)。
    /// - 実データ: 曲数 / カタログ状態は GManager (ActiveSongs / HasRemoteCatalog)、
    ///   右上の曲情報 (自己ベスト) は GManager のカタログと保存済みベストスコア
    ///   (GManager.BestScoreOf)、バージョンと更新状態は Application.version と
    ///   UpdateChecker.Model。design 例の固定値 (v1.1.0 → v1.2.0 等) は決して出さない。
    ///   更新ボタンは design には無いノードだが、更新の導線は Title にしか無いので
    ///   Header 帯の左側 (340,20) に置く (実機能のため残す)。
    /// - キーのキャップ (Space / O / Esc ...) は実際の割り当て (KeyActions) から取る。
    /// - 構築はコードのみ (シーン YAML / Prefab / .meta は触らない)。
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

        // --- design px 配置 (QJtTv 実測)。フレーム基準サイズと origin は V6TitleLogic。---

        private readonly Action onStart;
        private readonly Action onOption;
        private readonly Action onExit;
        private Action onHistory;
        private Action onRanking;

        /// <summary>タブ 1 件の表示情報 (ラベル / アイコンスプライト名 / 実キー / 操作)。</summary>
        private sealed class Tab
        {
            public string Name;
            public string IconSprite; // スプライト名 (Resources/Icons/ 以下)。空なら非表示。
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
        private Transform headerZone;
        private Transform mainZone;
        private Transform footerZone;

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
            // 旧 (v5) タイトル UI と更新通知バナーを隠す (v6 が自前で更新情報を
            // 描くので、旧側は二重表示になるだけ)。Create は v6 モード
            // (V6TitleView.Enabled) でしか呼ばれないので、ここで分岐する。
            V6UiMode.HideLegacyTitleUi();
            V6UiMode.HideLegacyUpdateNotice();
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

            PlaceFrame();

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

            // dismiss (F4) も表示状態に影響するのでキーに含める (消えた表示を
            // 再描画で復活させない)。
            return model.Phase + "|" + (model.LatestVersion ?? "") + "|"
                + Mathf.RoundToInt(model.DownloadProgress * 100f) + "|"
                + (model.DismissedForSession ? "dismissed" : "shown");
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

            // 画面外周には不透明パネルを敷かない: design QJtTv の fill は
            // assets/bg-anim3.png (mode=cover) 相当 = ゲーム自身の背景
            // (タイトル動画 VideoPlayer "Screen") が見えるままにする。

            // 共通フレーム (1728×972、Header/Main/Footer/Modal ゾーン + 外周付き)。
            frame = V6Frame.Create(rootGo.transform, "V6 Title Frame 1728x972").GetComponent<RectTransform>();
            headerZone = frame.transform.Find("Header");
            mainZone = frame.transform.Find("Main");
            footerZone = frame.transform.Find("Footer");

            var driver = rootGo.AddComponent<V6TitleViewDriver>();
            driver.View = this;
            PlaceFrame();
        }

        /// <summary>
        /// フレームを 1:1 (等倍) で中央配置する (引き伸ばしはしない)。
        /// origin = ((w−1728)/2, (h−972)/2) 切り捨て (V6TitleLogic.FrameOrigin、
        /// design POJwG "Resolution Check · Title" の実配置)。
        /// </summary>
        private void PlaceFrame()
        {
            if (frame == null)
            {
                return;
            }

            V6Layout.Place(frame, V6TitleLogic.FrameOrigin(Screen.width, Screen.height),
                new Vector2(V6TitleLogic.FrameW, V6TitleLogic.FrameH));
        }

        private void Rebuild()
        {
            if (rootGo == null)
            {
                return;
            }

            PlaceFrame();
            lastUpdateStateKey = UpdateStateKey();

            // 更新の実状態 (UpdateChecker.Model)。ヘッダー / メインの双方から参照する。
            UpdateFlowModel model = UpdateModel();

            // 各ゾーンの中身を作り直す (ゾーン自体 / Modal / 外周は V6Frame が所有)。
            ClearChildren(headerZone);
            ClearChildren(mainZone);
            ClearChildren(footerZone);

            BuildHeader(model);
            BuildTabList();
            BuildMain(model);
            BuildFooter();
        }

        private static void ClearChildren(Transform zone)
        {
            Common.V6Hierarchy.ClearChildren(zone);
        }

        // --- ヘッダー (24,0) 1680x96 (V6Frame の Header ゾーン内) ---

        private void BuildHeader(UpdateFlowModel model)
        {
            var header = headerZone.gameObject;

            // 左上: Exit (design CtA3V)。正本の Exit フレームは塗りなしで、
            // 中に 56×56 の Icon Button ($k4) + KeyCap + Label を載せる
            // (組み立ては V6TitleExit に集約。旧実装は 162×56 を 1 枚塗っていた)。
            V6TitleExit.Build(header, onExit);

            // タイトル文字 (design aqq3q/ci867) は正本で `enabled: false`
            // (Exit ボタン 162×56 と重なるため非表示)。既定では描かない。
            // 左上は戻るではなく Exit (V6TitleExit) なので戻るボタンは置かない。
            if (V6UiMode.TitleHeaderTitle)
            {
                V6Header.Title(header, "FlashBeat", V6TitleLogic.HeaderTitlePos,
                    V6TitleLogic.HeaderTitleSize, V6TitleLogic.HeaderTitleFontSize);
            }

            // 更新ボタン (更新があるときだけ)。design の右上には無いが、更新の導線は
            // Title にしか無いので、同じヘッダー帯の左側に置く (右側は曲情報 / 状態)。
            if (model != null && V6TitleLogic.ShowUpdateButton(model.Phase))
            {
                // 正本 D7gbLV: Header ローカル (1021,20) 355×56、$k4 r8。
                // アイコン refresh-cw (FMObZ は $accent-light)、KeyCap は半透明黒
                // (#00000040、ほかの $k4 Cap とは別)。
                var update = V6Controls.ButtonSecondary(header,
                    V6TitleLogic.UpdateButtonLabel(model.Phase, model.DownloadProgress),
                    V6TitleLogic.UpdateButtonPos, V6TitleLogic.UpdateButtonSize, "refresh-cw",
                    V6TitleLogic.UpdateButtonKey);
                TintChild(update, "Icon", V6Tokens.AccentLight);
                TintChild(update, "KeyCap " + V6TitleLogic.UpdateButtonKey, new Color32(0, 0, 0, 0x40));

                // 押したときに実行できる操作 (Available → DL / Ready → 適用) が
                // あるときだけ結線する。Downloading 中は進捗表示だけなので
                // **押しても何も起きないボタン**にせず、非活性にする。
                // dismiss (F4) 後も操作できる (通知を閉じた = 更新しない、ではない)。
                if (UpdateNoticePresenter.ResolveUserAction(model) != UpdateNoticeAction.None)
                {
                    WireClick(update, TriggerUpdate);
                }
                else
                {
                    var button = update.GetComponent<Button>();
                    if (button != null) { button.interactable = false; }
                }
            }

            // 右上: バージョン (design L6w6G = Header ローカル (1400,32) 280×32)。
            // 正本の要素は [現行版] [→] [新版] の 3 つだけで、曲情報 / カタログ状態 /
            // 自己ベストは設計に無い (v6 移植時に足していた非設計要素) ため描かない。
            var versionZone = V6Controls.Panel(header, "Version", V6TitleLogic.VersionZonePos,
                V6TitleLogic.VersionZoneSize, Clear(), 0f);
            bool showNewVersion = model != null &&
                V6TitleLogic.ShowNewVersion(model.Phase, model.LatestVersion);
            string currentVersion = V6TitleLogic.VersionLabel(Application.version);
            if (!string.IsNullOrEmpty(currentVersion))
            {
                // 版番号は桁数が変わるので、正本の右端 (矢印の手前 / zone 右端) に右揃えする。
                float rightEdge = showNewVersion
                    ? V6TitleLogic.VersionArrowPos.x - 8f
                    : V6TitleLogic.VersionRightEdge;
                V6Controls.Text(versionZone, "Version Current", currentVersion,
                    new Vector2(0f, V6TitleLogic.VersionCurrentPos.y),
                    new Vector2(rightEdge, V6TitleLogic.VersionCurrentSize.y),
                    V6TitleLogic.VersionFontSize, V6Tokens.TextDim, TextAlignmentOptions.Right);
            }

            if (showNewVersion)
            {
                V6Controls.Text(versionZone, "Version Arrow", "→", V6TitleLogic.VersionArrowPos,
                    V6TitleLogic.VersionArrowSize, V6TitleLogic.VersionArrowFontSize,
                    V6Tokens.TextDim, TextAlignmentOptions.Left);
                V6Controls.Text(versionZone, "Version New",
                    V6TitleLogic.VersionLabel(model.LatestVersion), V6TitleLogic.VersionNewPos,
                    V6TitleLogic.VersionNewSize, V6TitleLogic.VersionFontSize,
                    V6Tokens.AccentLight, TextAlignmentOptions.Left, FontStyles.Bold);
            }
        }

        /// <summary>
        /// 直下の子を名前で探して Image の色だけ差し替える (共通部品の既定色が
        /// 正本と違う 1 点だけを直すため。部品自体は他画面と共有する)。
        /// </summary>
        private static void TintChild(GameObject parent, string childName, Color color)
        {
            Transform child = parent.transform.Find(childName);
            if (child == null)
            {
                return;
            }

            var image = child.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }

        /// <summary>
        /// 自己ベストの読み出し (GManager のカタログ + 保存済みスコア)。
        /// 正本 QJtTv のヘッダーは [Exit] [更新] [バージョン] だけなので v6 Title では
        /// 表示しないが、選曲側の実装と共有するロジックとして残す。
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

        private void BuildMain(UpdateFlowModel model)
        {
            var main = mainZone.gameObject;

            // Logo (design CnRJj): Main ローカル (0,180) 1680×332、assets/13.png contain。
            // タイトル動画 (ゲーム自身の背景) の上に重ねて描く。
            var logo = new GameObject("Logo", typeof(RectTransform), typeof(Image));
            logo.transform.SetParent(main.transform, false);
            // 13.png は importer が textureType: Default (spriteMode: 0) のため
            // Resources.Load<Sprite> は常に null を返す (実機で白クアッドに
            // なっていた原因)。Texture2D で読んでスプライトを作る。
            var logoTex = Resources.Load<Texture2D>("V5/Common/13");
            var logoImage = logo.GetComponent<Image>();
            if (logoTex != null)
            {
                // Issue #401: タイトルへ戻るたび Sprite.Create していた。
                // 原 Texture から 1 枚だけ実体化して共有する。
                logoImage.sprite = V6Sprites.FromTexture(logoTex, "V6TitleLogo");
            }
            logoImage.preserveAspect = true;
            logoImage.raycastTarget = false;
            if (logoImage.sprite == null)
            {
                // スプライトが読めないとき (LFS 未取得のビルド等) は白クアッドを
                // 描かない: Image はスプライト無しで白く塗られるため、
                // タイトル動画 (ゲーム背景) が隠れてしまう。動画を見せる。
                logoImage.enabled = false;
            }
            V6Layout.Place(logo.GetComponent<RectTransform>(), V6TitleLogic.LogoPos, V6TitleLogic.LogoSize);

            // Controls · タブ (FHcxk): (280,552) 1120×48, $overlay r8, タブ各 280×48。
            // V6UiMode.TitleTabs が false の間は描かない (2026-11-08 オーナー指示:
            // 実機で中段のバーを消す。フッターの START / 設定ボタンが同じ操作を
            // 受け持つため二重表示だった)。tabList 自体は BuildTabList が作り続けるので
            // キー操作 (↑↓/Space/O) は無効化しない。
            if (V6UiMode.TitleTabs)
            {
                var tabs = V6Controls.Panel(main, "Tabs", V6TitleLogic.TabsPos, V6TitleLogic.TabsSize,
                    V6Tokens.Overlay, V6Tokens.RadiusMedium);
                for (int i = 0; i < tabList.Count; i++)
                {
                    BuildTab(tabs, tabList[i], i);
                }
            }

        }

        /// <summary>
        /// タブの一覧を作る。操作が設定されているものだけを並べる
        /// (押しても何も起きないボタンを作らない)。
        /// アイコン / ラベル / キーは design QJtTv のタブ 4 枚 (play/history/trophy/settings)。
        /// Start / Option のキーは実際の割り当て (KeyActions) から取る
        /// (design 値 Space / O は既定割り当てと同じ)。
        /// History / Ranking は KeyActions に割り当てが無いため固定キー
        /// (V6TitleLogic の単一ソース。TitleSceneManager が poll するキーと同じ)。
        /// </summary>
        private void BuildTabList()
        {
            tabList.Clear();
            tabList.Add(new Tab
            {
                Name = "Tab GameStart",
                IconSprite = "play",
                Label = "GameStart",
                Key = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Start),
                Action = onStart,
            });

            if (onHistory != null)
            {
                tabList.Add(new Tab
                {
                    Name = "Tab History",
                    IconSprite = "history",
                    Label = "History",
                    Key = V6TitleLogic.HistoryTabKey,
                    Action = onHistory,
                });
            }

            if (onRanking != null)
            {
                tabList.Add(new Tab
                {
                    Name = "Tab Ranking",
                    IconSprite = "trophy",
                    Label = "Ranking",
                    Key = V6TitleLogic.RankingTabKey,
                    Action = onRanking,
                });
            }

            tabList.Add(new Tab
            {
                Name = "Tab Option",
                IconSprite = "settings",
                Label = "Option",
                Key = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Option),
                Action = onOption,
            });

            if (selectedTab >= tabList.Count)
            {
                selectedTab = 0;
            }
        }

        /// <summary>
        /// タブ 1 枚 (design i6ziD 等): 280×48、gap8 padding[0,10]、
        /// アイコン 24×24 / Label fs26 w500 / 右端に KeyCap (fs22、高さ 36)。
        /// 選択中のタブだけ $k6 (#525252) 背景。
        /// </summary>
        private void BuildTab(GameObject tabs, Tab tab, int index)
        {
            bool selected = index == selectedTab;
            var tabGo = V6Controls.Panel(tabs, tab.Name,
                new Vector2(V6TitleLogic.TabSize.x * index, 0f), V6TitleLogic.TabSize,
                selected ? V6Tokens.Key6 : Clear(), 0f);
            MakeButton(tabGo, tab.Action);

            // アイコン (design 24×24、縦中央)。
            if (!string.IsNullOrEmpty(tab.IconSprite))
            {
                var iconColor = selected ? V6Tokens.Text : V6Tokens.TextMuted;
                var icon = V6Icons.Create(tabGo, tab.IconSprite, V6TitleLogic.TabIconSize, iconColor, "Icon");
                if (icon != null)
                {
                    var iconRt = icon.GetComponent<RectTransform>();
                    iconRt.anchorMin = new Vector2(0f, 0.5f);
                    iconRt.anchorMax = new Vector2(0f, 0.5f);
                    iconRt.pivot = new Vector2(0.5f, 0.5f);
                    iconRt.anchoredPosition = new Vector2(
                        V6TitleLogic.TabPaddingX + V6TitleLogic.TabIconSize * 0.5f, 0f);
                }
            }

            // ラベル (design fs26 w500、ノードは y5 / 高さ 38)。アイコンの右から。
            float labelX = V6TitleLogic.TabPaddingX + V6TitleLogic.TabIconSize + V6TitleLogic.TabGap;
            var labelColor = selected ? V6Tokens.Text : V6Tokens.TextMuted;
            V6Controls.Text(tabGo, "Label", tab.Label, new Vector2(labelX, 5f), new Vector2(160f, 38f),
                V6TitleLogic.TabLabelFontSize, labelColor, TextAlignmentOptions.Left);

            // 右端の KeyCap (design: 高さ 32・padding [0,8]、y8)。
            if (!string.IsNullOrEmpty(tab.Key))
            {
                float keyW = Mathf.Ceil(V6Ui.LabelAdvanceEm(tab.Key) * V6Controls.ButtonKeyFontSize)
                    + V6Controls.ButtonKeyCapPaddingX * 2f;
                V6Controls.KeyCap(tabGo, tab.Key,
                    new Vector2(V6TitleLogic.TabSize.x - V6TitleLogic.TabPaddingX - keyW,
                        (V6TitleLogic.TabSize.y - V6Controls.ButtonKeyCapHeight) * 0.5f),
                    new Vector2(keyW, V6Controls.ButtonKeyCapHeight), V6Controls.ButtonKeyCapPaddingX);
            }
        }

        // --- フッター (24,876) 1680x96 (V6Frame の Footer ゾーン内) ---

        private void BuildFooter()
        {
            var footer = footerZone.gameObject;

            // 左下: キー凡例 (design QQ4jM、gap16、3 個)。
            // V6UiMode.TitleLegend が false の間は描かない (2026-11-08 オーナー指示:
            // 実機で左下のヒント行を消す。フッター右下の START / 設定ボタンにも
            // キー表記があるため二重表示だった)。
            if (V6UiMode.TitleLegend)
            {
                // design QQ4jM: KeyCap h36 / pad[0,10] / 項内 gap10 / 項目間 gap16
                // (他画面の h32 / pad[0,8] / gap8 / gap24 とは別設計)。幅は TMP 実測。
                float x = 0f;
                string startKey = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Start);
                x = AddLegendItem(footer, x, startKey, "決定");
                x = AddLegendItem(footer, x, "↑↓", "選択");
                string exitKey = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Exit);
                AddLegendItem(footer, x, exitKey, "終了");
            }

            // 右下の操作ボタン (design T8HuQS) は正本で `enabled: false`。
            // タブ行 (GameStart / Option) とキー操作が同じ導線を持つので描かない。
            if (!V6UiMode.TitleFooterActions)
            {
                return;
            }

            string optionKey = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Option);
            var aux = V6Controls.ButtonSecondary(footer, "設定", V6TitleLogic.FooterAuxPos,
                V6TitleLogic.FooterAuxSize, "settings", string.IsNullOrEmpty(optionKey) ? "O" : optionKey);
            WireClick(aux, onOption);
            string primaryKey = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Start);
            var primary = V6Controls.ButtonPrimary(footer, "START", V6TitleLogic.FooterPrimaryPos,
                V6TitleLogic.FooterPrimarySize, "play", string.IsNullOrEmpty(primaryKey) ? "Space" : primaryKey);
            WireClick(primary, onStart);
        }

        /// <summary>
        /// キー凡例 1 個 (design QQ4jM): KeyCap (fs22 / h36 / pad[0,10]) + Label fs22 $text-muted
        /// を gap10 で並べ、項目間 gap16。戻り値は次の項目の x。
        /// </summary>
        private static float AddLegendItem(GameObject legend, float x, string key, string label)
        {
            return V6Legend.Item(legend, x, V6TitleLogic.LegendPos.y, key, label,
                V6Tokens.KeyCapHeight, V6Controls.KeyCapPaddingX, V6TitleLogic.LegendInnerGap,
                V6TitleLogic.LegendGap, V6TitleLogic.LegendFontSize, V6Tokens.Key4, V6Tokens.TextMuted);
        }

        // --- uGUI ユーティリティ (配置は V6Layout、部品は V6Controls に集約) ---

        private static GameObject MakeButton(GameObject go, Action onClick)
        {
            var button = go.GetComponent<Button>();
            if (button == null)
            {
                button = go.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                var nav = button.navigation;
                nav.mode = Navigation.Mode.None;
                button.navigation = nav;
            }

            V6ButtonPressFeedback.Attach(go);
            if (onClick != null)
            {
                // Title のボタンはすべて画面遷移 (Select / Option / 終了) を
                // 起こすので、押下リングが見えるだけ遅らせる。
                button.onClick.AddListener(() => V6ButtonPressFeedback.RunAfterFeedback(go, onClick));
            }

            return go;
        }

        /// <summary>V6Controls が作った Button にクリック時の処理を結線する。</summary>
        private static void WireClick(GameObject go, Action onClick)
        {
            MakeButton(go, onClick);
        }

        private static Color Clear()
        {
            return new Color(0f, 0f, 0f, 0f);
        }
    }
}
