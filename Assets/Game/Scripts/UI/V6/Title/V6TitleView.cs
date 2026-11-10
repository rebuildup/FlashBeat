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
            if (zone == null)
            {
                return;
            }

            var children = new List<GameObject>();
            foreach (Transform child in zone)
            {
                children.Add(child.gameObject);
            }

            for (int i = 0; i < children.Count; i++)
            {
                V5GameUi.DestroyNow(children[i]);
            }
        }

        // --- ヘッダー (24,0) 1680x96 (V6Frame の Header ゾーン内) ---

        private void BuildHeader(UpdateFlowModel model)
        {
            var header = headerZone.gameObject;

            // 左上: 戻る (design zUcsi/zCQBY): 56×56 #3A3A3A r8、
            // arrow-left アイコン 24×24 + 実キー (design 既定 "Esc") fs16 を縦に重ねる。
            var back = V6Controls.Panel(header, "Back button", V6TitleLogic.BackButtonPos,
                V6TitleLogic.BackButtonSize, V6Tokens.Key4, V6Tokens.RadiusMedium);
            MakeButton(back, onExit);
            // 縦積み (design: gap2 中央寄せ): アイコン 24 + gap2 + Key 16 = 42、上下 (56−42)/2 = 7。
            var backCol = V6Layout.Column(back, "Inner", new Vector2(0f, 7f), new Vector2(56f, 42f), 2f);
            var backIcon = V6Icons.Create(backCol, "arrow-left", 24f, V6Tokens.TextMuted, "Icon");
            string exitKey = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Exit);
            var backKey = V6Controls.Text(backCol, "Key", string.IsNullOrEmpty(exitKey) ? "Esc" : exitKey,
                new Vector2(0f, 26f), new Vector2(56f, 16f), 16f, V6Tokens.TextMuted, TextAlignmentOptions.Center);
            V6Layout.StackVertical(backCol.GetComponent<RectTransform>(), 2f, backIcon, backKey);

            // タイトル文字 (design aqq3q/ci867): (72,20) fs38 w700 $text。
            V6Controls.Text(header, "Title", "FlashBeat", V6TitleLogic.HeaderTitlePos,
                V6TitleLogic.HeaderTitleSize, V6TitleLogic.HeaderTitleFontSize, V6Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);

            // 更新ボタン (更新があるときだけ)。design の右上には無いが、更新の導線は
            // Title にしか無いので、同じヘッダー帯の左側に置く (右側は曲情報 / 状態)。
            if (model != null && V6TitleLogic.ShowUpdateButton(model.Phase))
            {
                var update = V6Controls.ButtonSecondary(header,
                    V6TitleLogic.UpdateButtonLabel(model.Phase, model.DownloadProgress),
                    new Vector2(340f, 20f), new Vector2(460f, 56f), null, "F3");

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

            // 右上: 曲情報 (design "Zone ・曲情報 (右上)" WUQeP = Header ローカル (1080,0) 300×96)。
            // 中身は実データだけ: カタログの曲名 / アーティストと、その曲のベストスコア。
            // 記録が 1 件も無いときは数値を出さず「まだ記録がありません」とだけ書く。
            var songZone = V6Controls.Panel(header, "Zone Song", V6TitleLogic.SongInfoZonePos,
                V6TitleLogic.HeaderZoneSize, Clear(), 0f);
            V6TitleLogic.V6TitleRecord record = BestRecordNow();
            V6Controls.Text(songZone, "Record Label", "自己ベスト", new Vector2(0f, 12f), new Vector2(160f, 24f),
                16f, V6Tokens.TextMuted, TextAlignmentOptions.Left);
            if (record.HasRecord)
            {
                V6Controls.Text(songZone, "Record Score", V6TitleLogic.ScoreText(record.Score),
                    new Vector2(120f, 12f), new Vector2(160f, 28f), 22f, V6Tokens.Text,
                    TextAlignmentOptions.Right, FontStyles.Bold);
                V6Controls.Text(songZone, "Song Title", record.Title, new Vector2(0f, 42f), new Vector2(300f, 30f),
                    24f, V6Tokens.Text, TextAlignmentOptions.Left);
                V6Controls.Text(songZone, "Song Artist", record.Artist, new Vector2(0f, 72f), new Vector2(300f, 24f),
                    16f, V6Tokens.TextMuted, TextAlignmentOptions.Left);
            }
            else
            {
                V6Controls.Text(songZone, "No Record", V6TitleLogic.NoRecordText, new Vector2(0f, 42f),
                    new Vector2(300f, 28f), 20f, V6Tokens.TextDim, TextAlignmentOptions.Left);
            }

            // 右上: 状態 (design "Zone ・状態 (右上)" l26KMD = Header ローカル (1380,0) 300×96)。
            // カタログ状態 (曲数 + クラウド / ローカル) とバージョン (実データ)。
            var stateZone = V6Controls.Panel(header, "Zone State", V6TitleLogic.StateZonePos,
                V6TitleLogic.HeaderZoneSize, Clear(), 0f);
            int songCount = V6TitleLogic.PlayableSongCount(GManager.ActiveSongs);
            V6Controls.Text(stateZone, "Catalog Status",
                V6TitleLogic.CatalogStatusText(GManager.HasRemoteCatalog, songCount), new Vector2(0f, 20f),
                new Vector2(300f, 28f), 18f, V6Tokens.TextDim, TextAlignmentOptions.Left);
            V6Controls.Text(stateZone, "Version", V6TitleLogic.VersionLabel(Application.version),
                new Vector2(0f, 50f), new Vector2(140f, 24f), 16f, V6Tokens.TextDim, TextAlignmentOptions.Left);
            if (model != null && V6TitleLogic.ShowNewVersion(model.Phase, model.LatestVersion))
            {
                var arrowGo = V6Layout.Row(stateZone, "Arrow", new Vector2(140f, 50f), new Vector2(24f, 24f), 0f);
                V6Icons.Create(arrowGo, "chevron-right", 16f, V6Tokens.TextDim, "Icon");
                V6Controls.Text(stateZone, "New Version", V6TitleLogic.VersionLabel(model.LatestVersion),
                    new Vector2(164f, 50f), new Vector2(136f, 24f), 16f, V6Tokens.AccentLight,
                    TextAlignmentOptions.Left, FontStyles.Bold);
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
                logoImage.sprite = Sprite.Create(logoTex,
                    new Rect(0f, 0f, logoTex.width, logoTex.height),
                    new Vector2(0.5f, 0.5f), 100f);
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

            BuildUpdateNotice(model);
        }

        /// <summary>
        /// 「アップデートあり」通知 (issue #118)。
        /// 更新が実際に利用できるときだけ出し、「更新して再起動」ヘッダーボタン
        /// (操作) とは別 element として示す (状態の表示と操作の表示を分ける)。
        /// 旧 (v5) の更新通知ピルは v6 モードで隠されるため (V6UiMode.HideLegacyUpdateNotice)、
        /// v6 タイトルが自前で描く。
        /// </summary>
        private void BuildUpdateNotice(UpdateFlowModel model)
        {
            if (!V6TitleLogic.ShowUpdateAvailableNotice(model))
            {
                return;
            }

            var main = mainZone.gameObject;
            string title = V6TitleLogic.UpdateNoticeTitle(model.Phase, model.LatestVersion,
                model.DownloadProgress);
            string hint = V6TitleLogic.UpdateNoticeHint(model.Phase);

            var banner = V6Controls.Panel(main, "Update Notice", V6TitleLogic.UpdateNoticePos,
                V6TitleLogic.UpdateNoticeSize, V6Tokens.Overlay, V6Tokens.RadiusMedium);
            // 背景パネルはクリックを透過させる (下のロゴ画像などに干渉しない)。
            var bannerImage = banner.GetComponent<Image>();
            if (bannerImage != null)
            {
                bannerImage.raycastTarget = false;
            }

            // 左端のアクセント帯 (通知だと分かる目印)。
            var accent = V6Controls.Panel(banner, "Update Notice Accent", Vector2.zero,
                new Vector2(V6TitleLogic.UpdateNoticeAccentWidth, V6TitleLogic.UpdateNoticeSize.y),
                V6Tokens.Accent, V6Tokens.RadiusSmall);
            var accentImage = accent.GetComponent<Image>();
            if (accentImage != null)
            {
                accentImage.raycastTarget = false;
            }

            // 見出し (「アップデートあり」+ 版番号)。
            float titleWidth = V6Layout.EstimateTextWidth(title, V6TitleLogic.UpdateNoticeTitleFontSize);
            V6Controls.Text(banner, "Update Notice Title", title,
                new Vector2(V6TitleLogic.UpdateNoticePadX, 13f),
                new Vector2(titleWidth, 30f), V6TitleLogic.UpdateNoticeTitleFontSize,
                V6Tokens.AccentLight, TextAlignmentOptions.Left, FontStyles.Bold);

            // 補足 (操作の説明 / 「押さなければ更新しません」)。
            if (!string.IsNullOrEmpty(hint))
            {
                float hintX = V6TitleLogic.UpdateNoticePadX + titleWidth + V6TitleLogic.UpdateNoticeGap;
                float hintWidth = V6TitleLogic.UpdateNoticeSize.x - hintX - V6TitleLogic.UpdateNoticePadX;
                V6Controls.Text(banner, "Update Notice Hint", hint, new Vector2(hintX, 15f),
                    new Vector2(hintWidth, 26f), V6TitleLogic.UpdateNoticeHintFontSize,
                    V6Tokens.TextMuted, TextAlignmentOptions.Left);
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

            // ラベル (design fs26 w500、縦中央)。アイコンの右 (padding + icon + gap) から。
            float labelX = V6TitleLogic.TabPaddingX + V6TitleLogic.TabIconSize + V6TitleLogic.TabGap;
            var labelColor = selected ? V6Tokens.Text : V6Tokens.TextMuted;
            V6Controls.Text(tabGo, "Label", tab.Label,
                new Vector2(labelX, (V6TitleLogic.TabSize.y - 34f) * 0.5f), new Vector2(160f, 34f),
                V6TitleLogic.TabLabelFontSize, labelColor, TextAlignmentOptions.Left);

            // 右端の KeyCap (design: fs22、padding [0,8]。共通部品は契約の高さ 36 / padding [0,10])。
            if (!string.IsNullOrEmpty(tab.Key))
            {
                float keyW = Mathf.Ceil(V6Ui.LabelAdvanceEm(tab.Key) * V6Controls.KeyCapFontSize)
                    + V6Controls.KeyCapPaddingX * 2f;
                V6Controls.KeyCap(tabGo, tab.Key,
                    new Vector2(V6TitleLogic.TabSize.x - V6TitleLogic.TabPaddingX - keyW,
                        (V6TitleLogic.TabSize.y - V6Tokens.KeyCapHeight) * 0.5f),
                    new Vector2(keyW, V6Tokens.KeyCapHeight));
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
                var legend = V6Layout.Row(footer, "Legend", V6TitleLogic.LegendPos,
                    new Vector2(840f, V6Tokens.KeyCapHeight), V6TitleLogic.LegendGap);
                float x = 0f;
                string startKey = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Start);
                x = AddLegendItem(legend, x, startKey, "決定");
                x = AddLegendItem(legend, x, "↑↓", "曲送り");
                string exitKey = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Exit);
                AddLegendItem(legend, x, exitKey, "戻る");
            }

            // 右下の操作ボタン (design T8HuQS):
            // 補助操作 (1198,20) 197×56 = Button Secondary (settings / 設定 / O)、
            // 主操作 (1407,20) 273×56 = Button Primary (START / Space)。
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
        /// キー凡例 1 個 (design HI6YE): KeyCap (fs22、高さ 36) + Label fs22 $text-muted。
        /// 戻り値は次のアイテムの x オフセット (KeyCap + 内部 gap + Label + アイテム間 gap)。
        /// </summary>
        private static float AddLegendItem(GameObject legend, float x, string key, string label)
        {
            if (string.IsNullOrEmpty(key))
            {
                return x;
            }

            float keyW = Mathf.Ceil(V6Ui.LabelAdvanceEm(key) * V6Controls.KeyCapFontSize)
                + V6Controls.KeyCapPaddingX * 2f;
            V6Controls.KeyCap(legend, key, new Vector2(x, 0f), new Vector2(keyW, V6Tokens.KeyCapHeight));

            float labelX = x + keyW + V6TitleLogic.LegendInnerGap;
            float labelW = Mathf.Ceil(V6Ui.LabelAdvanceEm(label) * V6TitleLogic.LegendFontSize);
            V6Controls.Text(legend, "Legend Label", label, new Vector2(labelX, 0f),
                new Vector2(labelW, V6Tokens.KeyCapHeight), V6TitleLogic.LegendFontSize,
                V6Tokens.TextMuted, TextAlignmentOptions.Left);

            return labelX + labelW + V6TitleLogic.LegendGap;
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

            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
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
