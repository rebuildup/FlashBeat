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
        // 曲リストの design 定数の単一ソースは V6SelectViewLogic (History/Ranking ビューと同じ方式)。
        private const float ListW = V6SelectViewLogic.ListW;
        private const float ListH = V6SelectViewLogic.ListH;
        private const float RowX = V6SelectViewLogic.RowX;
        private const float RowY0 = V6SelectViewLogic.RowY0;
        private const float RowW = V6SelectViewLogic.RowW;
        private const float RowH = V6SelectViewLogic.RowH;
        private const float RowStep = V6SelectViewLogic.RowStep;
        private const int ListVisibleRows = V6SelectViewLogic.ListVisibleRows;
        private const int RankingVisibleRows = 4; // 300px パネルに 44px 行 + 8px 間隔で 4 行

        private const float CenterX = 572f, CenterW = 536f;
        private const float RightX = 1120f, RightW = 560f;

        private readonly IV6SelectDataSource data;
        private readonly Action onReady;
        private readonly Action onBack;
        private readonly Action onOpenSettings;

        private GameObject rootGo;

        // --- Issue #399: 骨格を作ったときの状態 (差分更新の可否判定) ---
        /// <summary>一度でも骨格を作ったか。</summary>
        private bool scaffoldBuilt;
        /// <summary>骨格を作ったときの設定パネル開閉。</summary>
        private bool scaffoldSettingsOpen;
        /// <summary>骨格を作ったときの画面幅 (解像度変更の検出)。</summary>
        private int frameWidthAtBuild;
        /// <summary>骨格を作ったときの画面高さ。</summary>
        private int frameHeightAtBuild;
        private RectTransform frame;
        private int listScroll;
        private int rankingScroll;
        private bool searchFocused;

        /// <summary>
        /// 検索欄が入力を受け取っているか。検索中は L / R を画面切替に使わない
        /// (select 画面の直上で <c>SelectSceneManager</c> が L / R を切替に読むため)。
        /// </summary>
        internal bool SearchFocused => searchFocused;

        /// <summary>「未プレイのみ」で絞り込む (design の Filter ボタン / F キー)。</summary>
        private bool unplayedOnly;
        private bool settingsOpen;

        // --- Issue #415 phase 4: 可変ゾーンの scaffold 保持 + 差分更新 ---
        // RefreshVolatile はゾーン (Header / Song List / Selected Song Panel /
        // Ranking Column / Settings Column) を破棄せず、構造が変わる部分だけを
        // 作り直し、変動値は既存の Image/TMP/行へ適用する。
        /// <summary>DisplaySongs の使い回しバッファ (Refresh ごとの List 確保を避ける)。</summary>
        private readonly List<V6SongEntry> displaySongs = new List<V6SongEntry>();
        /// <summary>曲リスト Rows の表示モード ("" / "skeleton" / "empty" / "rows")。</summary>
        private string listMode = "";
        /// <summary>曲リストの可視行スロット (ListVisibleRows 個まで)。</summary>
        private readonly List<GameObject> listRowObjects = new List<GameObject>();
        /// <summary>listRowObjects と同じ並びの曲 ID (行クリックの結線先)。</summary>
        private readonly List<int> listRowSongIds = new List<int>();
        /// <summary>SCORE RANKING の表示モード ("" / "rows" / "empty")。</summary>
        private string rankMode = "";
        /// <summary>ランキングの可視行スロット (RankingVisibleRows 個まで)。</summary>
        private readonly List<GameObject> rankRowObjects = new List<GameObject>();
        /// <summary>ランキング空表示 ("No Data" テキスト)。行表示中は null。</summary>
        private GameObject rankEmptyGo;
        /// <summary>ドットインジケータの構築済み曲数 (-1 = 未構築)。</summary>
        private int dotCount = -1;
        /// <summary>ドット y 座標のキャッシュ (曲数が変わったときだけ再計算)。</summary>
        private float[] dotOffsetsCache;
        /// <summary>ドットの Image / RectTransform (選択色・サイズの差分更新用)。</summary>
        private readonly List<Image> dotImages = new List<Image>();
        private readonly List<RectTransform> dotRects = new List<RectTransform>();
        /// <summary>選択曲パネルを作ったときの hasSong (Meta 構造モード)。</summary>
        private bool centerHasSong;

        /// <summary>フッターの Ready が最後に構築されたときの有効状態。</summary>
        private bool footerReadyEnabled;

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
            // Issue #399: 選択・検索・カタログ更新は「変わる部分だけ」を反映する。
            RefreshVolatile();
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
            RefreshVolatile();
        }

        /// <summary>検証・計測用: 画面全体を作り直した回数。</summary>
        public int RebuildCountForAudit { get; private set; }

        /// <summary>検証・計測用: 変動部分だけを更新した回数。</summary>
        public int IncrementalUpdateCountForAudit { get; private set; }

        /// <summary>
        /// Issue #399/#415: 変動部分 (ヘッダー・曲リスト・中央・右カラム) だけを更新する。
        ///
        /// フッターは状態で変わらないので保持する。Issue #415 phase 4 で
        /// ゾーン scaffold の保持 + 行スロット再利用へ移行し、選択・検索・
        /// ランキング・設定値の変化は既存の Image/TMP/行へ適用する。
        /// 構造が変わる操作 (クイック設定の開閉・解像度変更・選択曲 null↔非 null・
        /// リスト/ランキングの表示モード変化) だけ該当部分を作り直す。
        /// </summary>
        private void RefreshVolatile()
        {
            if (rootGo == null || !rootGo.activeInHierarchy) return;

            if (!scaffoldBuilt
                || settingsOpen != scaffoldSettingsOpen
                || frameWidthAtBuild != Screen.width
                || frameHeightAtBuild != Screen.height)
            {
                Rebuild();
                return;
            }

            ApplyVolatile();

            IncrementalUpdateCountForAudit++;
        }

        /// <summary>
        /// 骨格の意味が変わるときの全再構築。frame の全子を捨ててから
        /// <see cref="ApplyVolatile"/> で各ゾーンの scaffold と可変値を作る
        /// (RefreshVolatile と同じ経路なので、再構築直後も差分更新の状態が揃う)。
        /// </summary>
        private void ApplyVolatile()
        {
            var songs = DisplaySongs();
            int selectIndex = SelectedIndex(songs);
            // スクロール位置は design (OENRv) どおり上から 9 行を詰める。
            // CenterScroll の負値をそのまま描くと空スロットが下側にずれて行が
            // 下端へ寄る (r1実機 2026-11-11 の症状) ので ListScroll で 0 以上に
            // 丸める。選択 index ≥ 4 では選択行は 5 行目 (上に 4 行) に来る。
            listScroll = V6SelectViewLogic.ListScroll(selectIndex, songs.Count);
            rankingScroll = V6SelectViewLogic.ClampScroll(rankingScroll,
                data.Selected != null ? data.RankingFor(data.Selected).Count : 0, RankingVisibleRows);

            EnsureHeader(songs.Count);
            EnsureFooter();
            EnsureList(songs, selectIndex);
            EnsureCenter();
            if (settingsOpen)
            {
                // wSjAm: 右カラム (Ranking) を「このプレイの設定」へ差し替える。
                EnsureSettings();
            }
            else
            {
                EnsureRight();
            }
        }

        /// <summary>frame 直下の名前付きゾーンを取る (無ければ null)。</summary>
        private GameObject FindZone(string name)
        {
            Transform t = frame != null ? frame.Find(name) : null;
            return t != null ? t.gameObject : null;
        }

        /// <summary>全子を消したあとに差分更新の参照状態をリセットする。</summary>
        private void ResetVolatileState()
        {
            listMode = "";
            rankMode = "";
            rankEmptyGo = null;
            centerHasSong = false;
            listRowObjects.Clear();
            listRowSongIds.Clear();
            rankRowObjects.Clear();
            dotCount = -1;
            dotOffsetsCache = null;
            dotImages.Clear();
            dotRects.Clear();
        }

        /// <summary>frame 直下の名前付きゾーンを階層から外して破棄を予約する。</summary>
        private void DestroyZone(string name)
        {
            if (frame == null) return;
            Transform child = frame.Find(name);
            if (child == null) return;
            child.SetParent(null, false);
            FlashBeat.UI.V5.V5GameUi.DestroyNow(child.gameObject);
        }

        public void SetVisible(bool visible)
        {
            if (rootGo != null)
            {
                rootGo.SetActive(visible);
            }

            // 非表示中 (履歴/ランキング画面) のデータ変更は OnDataChanged が
            // 捨てるので、選曲画面へ戻った時点で差分更新して追いつかせる。
            if (visible)
            {
                RefreshVolatile();
            }
        }

        /// <summary>現在の検索語。画面監査 (AutoTestV6Screens) が復元に使う。</summary>
        public string Query => data != null ? data.Query : "";

        /// <summary>
        /// 検索語を設定する。キーボード入力と同じ data.SetQuery 経路で、空状態の
        /// 実機撮影など画面監査から呼ぶ。UI を勝手に変える機能ではない。
        /// </summary>
        public void SetQuery(string query)
        {
            if (data != null)
            {
                data.SetQuery(query);
            }
        }

        /// <summary>検索欄にフォーカスする (統合担当が画面表示時に呼べる)。</summary>
        public void FocusSearch()
        {
            searchFocused = true;
            // フォーカス表示 (Query の "|" と色) は UpdateHeader が適用する。
            RefreshVolatile();
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

        /// <summary>クイック設定を閉じる (画面監査 AutoTestV6Screens からも使う)。</summary>
        public void CloseQuickSettings()
        {
            settingsOpen = false;
            Rebuild();
        }

        /// <summary>表示する曲リスト (実データ + 未プレイのみトグル)。使い回しバッファ。</summary>
        private List<V6SongEntry> DisplaySongs()
        {
            displaySongs.Clear();
            IReadOnlyList<V6SongEntry> filtered = data.FilteredSongs;
            if (filtered != null)
            {
                for (int i = 0; i < filtered.Count; i++)
                {
                    V6SongEntry song = filtered[i];
                    if (song == null) continue;
                    if (unplayedOnly && song.BestScore >= 0) continue; // プレイ済みの曲を除く
                    displaySongs.Add(song);
                }
            }

            return displaySongs;
        }

        /// <summary>「未プレイのみ」絞り込みの切り替え。</summary>
        private void ToggleUnplayedFilter()
        {
            unplayedOnly = !unplayedOnly;
            // 表示曲・Ready 可否・フィルターボタン色は差分更新で追従する
            // (Ready が反転したときだけ EnsureFooter がフッターを作り直す)。
            RefreshVolatile();
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
                    RefreshVolatile();
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
            // ただし操作キー (既定 O = Option) が優先。検索シードを先に判定すると、
            // O を押すたびに検索語が入り、フッターの「設定 O」が動かなくなる。
            if (KeyActions.IsDown(KeyActions.KeyAction.Option)) // 既定 O (#142)
            {
                OpenSettings();
                return;
            }

            string typed = Input.inputString;
            char seed = V6SelectViewLogic.SearchSeed(typed);
            if (seed != '\0')
            {
                searchFocused = true;
                string seeded = V6SelectViewLogic.ApplySearchInput("", seed.ToString());
                if (data.Query == seeded)
                {
                    // 検索語が変わらないときは Changed が発火しないため、フォーカス表示を
                    // 反映する更新をここで 1 回だけ行う。変わるときは SetQuery →
                    // Changed → OnDataChanged が更新するので、二重に呼ばない。
                    RefreshVolatile();
                }
                else
                {
                    data.SetQuery(seeded);
                }

                return;
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

            RebuildCountForAudit++;
            scaffoldBuilt = true;
            scaffoldSettingsOpen = settingsOpen;
            frameWidthAtBuild = Screen.width;
            frameHeightAtBuild = Screen.height;

            // Issue #415 受入: 再生成が起きたときは理由をログへ残す
            // (通常の選択・検索・スクロールでは出ない。設定開閉・解像度変更・
            // 明示的な再構築でだけ出る)。
            Debug.Log($"[V6SelectView] rebuild #{RebuildCountForAudit} " +
                $"settingsOpen={settingsOpen} screen={Screen.width}x{Screen.height}");

            PlaceFrame();

            // frame の全子を作り直す (V5 SelectView.Rebuild と同じ方針)。
            // Issue #415: 破棄後は ApplyVolatile が各ゾーンの scaffold を
            // 作り直して可変値を適用する (RefreshVolatile と同じ経路)。
            Common.V6Hierarchy.ClearChildren(frame.gameObject);
            ResetVolatileState();
            ApplyVolatile();
        }

        // --- ヘッダー (x24 y0 1680x96) ---

        // 外周 stroke は描かない (Issue #374)。design OENRv は共有フレーム rw7zx の
        // 外周線を全ゾーンで #00000000 に置換しており、枠線は画面ごとに出さない
        // (docs/ui/v6-screen-contract.md §5)。四隅のガイド線として残さないこと。

        private void BuildHeader(int filteredCount)
        {
            var header = At(V5Ui.Panel(frame.gameObject, "Header", new Vector2(1680f, 96f), Clear(), 0f), 24f, 0f);

            // 戻るボタンは共通部品 (Issue #370)。設計 OENRv: ヘッダー左端 (フレーム (24,20))。
            var backRect = V6SelectViewLogic.HeaderBackButton();
            V6Controls.BackButton(header, "Back Button", new Vector2(backRect.X, backRect.Y),
                FlashBeat.UI.V5.ActionKeyHint.Primary(KeyActions.KeyAction.Back),
                () =>
                {
                    if (onBack != null) onBack();
                });

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
            // design OENRv `/W1K1R/sIkNa/FiU1X`: lucide "search" 20x20 @(12,14)。
            // これが無いと design の「虫めがね + タイプして検索」にならない。
            var searchIcon = V6Icons.Create(search, "search", 20f, V5Tokens.TextDim);
            if (searchIcon != null)
            {
                var iconRect = V6SelectViewLogic.HeaderSearchIcon();
                At(searchIcon, iconRect.X, iconRect.Y);
            }

            var queryRect = V6SelectViewLogic.HeaderSearchQuery();
            var queryText = V5GameUi.Text(search, "Query", SearchDisplayText(), 22f,
                searchFocused ? V5Tokens.Text : V5Tokens.TextDim, TextAlignmentOptions.Left);
            SingleLine(queryText);
            At(queryText, queryRect.X, queryRect.Y, queryRect.W, queryRect.H);
            var capRect = V6SelectViewLogic.HeaderSearchCap();
            // design `/W1K1R/sIkNa/mMt7c/T3nJjq`: Key fs22 / h32。高さ比 (22/36) に
            // 任せると fs19 まで縮むので、design の fs22 を明示する。
            var cap = KeyCap(search, "A-Z", V6SelectViewLogic.CapFontSize,
                capRect.W, capRect.H);
            At(cap, capRect.X, capRect.Y);

            var count = V5GameUi.Text(header.gameObject, "Count",
                $"{filteredCount} / {data.AllSongs.Count}", 22f, V5Tokens.TextMuted,
                TextAlignmentOptions.Right);
            var countRect = V6SelectViewLogic.HeaderCount();
            At(count, countRect.X, countRect.Y, countRect.W, countRect.H);

            // Issue #427: ヘッダーにはエラーだけを出す。取得中の「ランキングを
            // 取得しています」は SCORE RANKING パネル内に出す (空白地への浮き表示をやめる)。
            if (data.HasError && !string.IsNullOrEmpty(data.StatusMessage))
            {
                var status = V5GameUi.Text(header.gameObject, "Status", data.StatusMessage, 18f,
                    V5Tokens.DangerLight, TextAlignmentOptions.Left);
                At(status, 864f, 74f, 792f, 20f);
            }

            var filter = Button(header, "Filter Button", 48f, 48f, 8f,
                unplayedOnly ? V5Tokens.Key6 : V5Tokens.Key4, ToggleUnplayedFilter);
            var filterRect = V6SelectViewLogic.HeaderFilter();
            At(filter, filterRect.X, filterRect.Y);
            V6Icons.Create(filter, "list-filter", 24f, V5Tokens.TextMuted);
        }

        /// <summary>
        /// ヘッダー: scaffold が無ければ作り、可変値 (検索語・件数・状態メッセージ・
        /// フィルターボタン色) を既存要素へ適用する。
        /// </summary>
        private void EnsureHeader(int filteredCount)
        {
            var header = FindZone("Header");
            if (header == null)
            {
                BuildHeader(filteredCount);
                return;
            }

            UpdateHeader(header, filteredCount);
        }

        private void UpdateHeader(GameObject header, int filteredCount)
        {
            var queryT = header.transform.Find("Search/Query");
            var queryText = queryT != null ? queryT.GetComponent<TextMeshProUGUI>() : null;
            if (queryText != null)
            {
                queryText.text = SearchDisplayText();
                queryText.color = searchFocused ? V5Tokens.Text : V5Tokens.TextDim;
            }

            var countT = header.transform.Find("Count");
            var countText = countT != null ? countT.GetComponent<TextMeshProUGUI>() : null;
            if (countText != null)
            {
                countText.text = $"{filteredCount} / {data.AllSongs.Count}";
            }

            // Issue #427: ヘッダーの Status はエラー時だけ存在する可変要素。
            // 必要になったら作り、不要になったら外す (毎回の再生成はしない)。
            Transform statusT = header.transform.Find("Status");
            bool needStatus = data.HasError && !string.IsNullOrEmpty(data.StatusMessage);
            if (needStatus)
            {
                TextMeshProUGUI status;
                if (statusT == null)
                {
                    status = V5GameUi.Text(header, "Status", data.StatusMessage, 18f,
                        V5Tokens.DangerLight, TextAlignmentOptions.Left);
                    At(status, 864f, 74f, 792f, 20f);
                }
                else
                {
                    status = statusT.GetComponent<TextMeshProUGUI>();
                    if (status != null)
                    {
                        status.text = data.StatusMessage;
                    }
                }
            }
            else if (statusT != null)
            {
                FlashBeat.UI.V5.V5GameUi.DestroyNow(statusT.gameObject);
            }

            var filterT = header.transform.Find("Filter Button");
            var filterImage = filterT != null ? filterT.GetComponent<Image>() : null;
            if (filterImage != null)
            {
                filterImage.color = unplayedOnly ? V5Tokens.Key6 : V5Tokens.Key4;
            }
        }

        // --- フッター (x24 y876 1680x96) ---

        private void BuildFooter()
        {
            var footer = At(V5Ui.Panel(frame.gameObject, "Footer", new Vector2(1680f, 96f), Clear(), 0f), 24f, 876f);

            // 凡例 (design OENRv Zone · キー操作: y30 / 項目間 gap24 / 項内 gap8 /
            // KeyCap h32 pad[0,8] / Key・Label fs22)。幅は共通部品 V6Legend が TMP 実測で
            // 詰める (design 実測 x = 0 / 155 / 315 / 442、zone 幅 593)。
            // 確定キーはキー設定 (KeyActions) の正本から取る (#142 / #221)。
            string confirmKey = FlashBeat.UI.V5.ActionKeyHint.Primary(KeyActions.KeyAction.Confirm);
            var legendItems = V6SelectViewLogic.FooterLegend(confirmKey);
            float legendX = 0f;
            for (int i = 0; i < legendItems.Length; i++)
            {
                legendX = V6Legend.Item(footer, legendX, V6Legend.Y,
                    legendItems[i].Key, legendItems[i].Label);
            }

            // 右下: 設定 (secondary) + Ready (primary)。design 座標のまま右詰め。
            // 設定パネル展開中 (wSjAm) は「閉じる (Esc)」Secondary に差し替える。
            // 中身は正本 Prefab/Button Secondary と同じ中央揃え (共通部品に任せる)。
            var settingsRect = V6SelectViewLogic.FooterSettings(settingsOpen);
            string settingsKey = FlashBeat.UI.V5.ActionKeyHint.Primary(settingsOpen
                ? KeyActions.KeyAction.Back
                : KeyActions.KeyAction.Option);
            var settingsBtn = V6Controls.ButtonSecondary(footer,
                settingsOpen
                    ? V6SelectViewLogic.FooterSettingsOpenLabelText
                    : V6SelectViewLogic.FooterSettingsLabelText,
                new Vector2(settingsRect.X, settingsRect.Y),
                new Vector2(settingsRect.W, settingsRect.H),
                settingsOpen
                    ? V6SelectViewLogic.FooterSettingsOpenIconName
                    : V6SelectViewLogic.FooterSettingsIconName,
                settingsKey);
            settingsBtn.name = settingsOpen ? "Op Close" : "Button Settings";
            var settingsButton = settingsBtn.GetComponent<Button>();
            if (settingsButton != null)
            {
                settingsButton.transition = Selectable.Transition.None;
                var nav = settingsButton.navigation;
                nav.mode = Navigation.Mode.None;
                settingsButton.navigation = nav;
                if (settingsOpen)
                {
                    settingsButton.onClick.AddListener(CloseSettings);
                }
                else
                {
                    settingsButton.onClick.AddListener(OpenSettings);
                }
            }

            var readyRect = V6SelectViewLogic.FooterReady();
            // 絞り込みで隠れた選択は Ready できない (#221/#222)。無効のときは
            // 押しても反応しないよう、クリックを張らず弱い色で描画する。
            bool readyEnabled = ReadyAvailable();
            var ready = V6Controls.ButtonPrimary(footer, "Ready",
                new Vector2(readyRect.X, readyRect.Y),
                new Vector2(readyRect.W, readyRect.H), "play", confirmKey);
            ready.name = "Op Ready";
            var readyButton = ready.GetComponent<Button>();
            if (readyButton != null)
            {
                readyButton.transition = Selectable.Transition.None;
                var nav = readyButton.navigation;
                nav.mode = Navigation.Mode.None;
                readyButton.navigation = nav;
                if (readyEnabled)
                {
                    // Select→Game は画面遷移なので、押下リングが見えるだけ遅らせる。
                    // 遅延中に絞り込みで選択が隠れた場合に備え、実行直前に
                    // ReadyAvailable を再確認する (CodeRabbit #438)。
                    readyButton.onClick.AddListener(() =>
                    {
                        V6ButtonPressFeedback.RunAfterFeedback(ready, () =>
                        {
                            if (ReadyAvailable() && onReady != null)
                            {
                                onReady();
                            }
                        });
                    });
                }
                else
                {
                    // design に無効状態の見本は無いが、押せないことが分かるよう
                    // 背景を $k1、文字・アイコンを $text-dim に落とす (旧実装と同じ扱い)。
                    readyButton.interactable = false;
                    var bg = ready.GetComponent<Image>();
                    if (bg != null) bg.color = V5Tokens.Key1;
                    foreach (var graphic in ready.GetComponentsInChildren<Graphic>(true))
                    {
                        var tmp = graphic as TextMeshProUGUI;
                        if (tmp != null) tmp.color = V5Tokens.TextDim;
                        else if (graphic is Image && graphic.gameObject.name.StartsWith("Icon"))
                            graphic.color = V5Tokens.TextDim;
                    }
                }
            }
        }

        /// <summary>
        /// フッターは骨格として保持するが、Ready の有効/無効 (選択曲が絞り込みで
        /// 隠れるか) は表示に出るので、状態が変わったときだけフッターを作り直す。
        /// </summary>
        private void EnsureFooter()
        {
            bool ready = ReadyAvailable();
            if (FindZone("Footer") == null || ready != footerReadyEnabled)
            {
                DestroyZone("Footer");
                BuildFooter();
                footerReadyEnabled = ready;
            }
        }

        // --- 左列: 曲リスト (x0 w560 #131313CC r16) ---

        /// <summary>
        /// 曲リストゾーンが無いとき scaffold (パネル・Rows・スクロールボタン・
        /// ドット容器) を作り、続けて <see cref="UpdateListContent"/> で中身を載せる。
        /// </summary>
        private void EnsureList(IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            var list = FindZone("Song List");
            if (list == null)
            {
                BuildListScaffold();
                list = FindZone("Song List");
            }

            if (list == null)
            {
                return;
            }

            UpdateListContent(list, songs, selectIndex);
        }

        private void BuildListScaffold()
        {
            // 位置も design 定数 (frame ローカル (24,96) 560x780 = ListPanelRect) から取る。
            var list = At(V5Ui.Panel(frame.gameObject, "Song List", new Vector2(ListW, ListH),
                FromHex("#131313CC"), 16f), V6SelectViewLogic.ListX, V6SelectViewLogic.ListY);
            AddScrollCatcher(list, 0);

            var rows = V5Ui.Node(list, "Rows");
            At(rows.GetComponent<RectTransform>(), 0f, 0f, ListW, ListH);

            BuildScrollButton(list, "Scroll Up", false);
            BuildScrollButton(list, "Scroll Down", true);
            // ドット容器だけ先に作る。中身 (ドット個) は UpdateDots が曲数に合わせる。
            V5Ui.Node(list, "Dot indicator");
        }

        /// <summary>
        /// 曲リストの中身を現在の状態へ合わせる。skeleton / empty / rows の
        /// モードが変わったときだけ Rows の中身を作り直し、rows では可視行を
        /// スロット単位で再利用する。ドットは曲数が変わったときだけ再生成する。
        /// </summary>
        private void UpdateListContent(GameObject list, IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            var rowsT = list.transform.Find("Rows");
            if (rowsT == null)
            {
                return;
            }

            GameObject rows = rowsT.gameObject;
            string mode = V6SelectViewLogic.ShouldShowSkeleton(data.IsLoading, songs.Count) ? "skeleton"
                : songs.Count == 0 ? "empty" : "rows";
            if (mode != listMode)
            {
                Common.V6Hierarchy.ClearChildren(rows);
                listRowObjects.Clear();
                listRowSongIds.Clear();
                listMode = mode;
                if (mode == "skeleton")
                {
                    // AqFtx のスケルトン行 (初回の読み込み中だけ)。再取得では出さない。
                    for (int i = 0; i < ListVisibleRows; i++)
                    {
                        // スケルトンも design 行 (mcCOI) と同じ x 配置 (Level 12 / Thumb 68 /
                        // Texts 176 / Grade 436)。バーの寸法・y は AqFtx のプレースホルダのまま。
                        var skeleton = At(V5Ui.Panel(rows, "Song Skeleton " + i, new Vector2(RowW, RowH),
                            V5Tokens.Key1, 8f), RowX, RowY0 + RowStep * i);
                        At(V5Ui.Panel(skeleton, "Level", new Vector2(44f, 44f), V5Tokens.Key3, 22f), 12f, 14f);
                        At(V5Ui.Panel(skeleton, "Thumb", new Vector2(96f, 54f), V5Tokens.Key3, 4f), 68f, 9f);
                        At(V5Ui.Panel(skeleton, "Texts", new Vector2(220f, 26f), V5Tokens.Key3, 4f), 176f, 14f);
                        At(V5Ui.Panel(skeleton, "Artist", new Vector2(120f, 18f), V5Tokens.Key3, 4f), 176f, 44f);
                        At(V5Ui.Panel(skeleton, "Grade", new Vector2(56f, 36f), V5Tokens.Key3, 4f), 436f, 18f);
                    }
                }
                else if (mode == "empty")
                {
                    // AqFtx: 該当なしの空状態。3 要素は design の座標 (Icon 32 @y14 /
                    // Title 24 @(0,54) h35 / Hint 20 @(0,97) h29) に置く。
                    var empty = At(V5Ui.Panel(rows, "Empty Note", new Vector2(448f, 140f), V5Tokens.Key0, 8f), 56f, 300f);
                    // design UIN4a は「検索して該当なし」の虫めがね + x。音楽アイコンではない。
                    V6Icons.Create(empty, "search-x", 32f, V5Tokens.TextDim);
                    var emptyIcon = empty.transform.Find("Icon")?.gameObject;
                    if (emptyIcon != null)
                    {
                        var iconRt = emptyIcon.GetComponent<RectTransform>();
                        iconRt.anchorMin = new Vector2(0.5f, 1f);
                        iconRt.anchorMax = new Vector2(0.5f, 1f);
                        iconRt.pivot = new Vector2(0.5f, 1f);
                        iconRt.anchoredPosition = new Vector2(0f, -14f);
                        iconRt.sizeDelta = new Vector2(32f, 32f);
                    }
                    var emptyTitle = V5GameUi.Text(empty.gameObject, "Title", "曲が見つかりません", 24f, V5Tokens.Text,
                        TextAlignmentOptions.Center, FontStyles.Normal);
                    At(emptyTitle, 0f, 54f, 448f, 35f);
                    var emptyHint = V5GameUi.Text(empty.gameObject, "Hint", "検索語を変えるか フィルターを解除してください", 20f,
                        V5Tokens.TextMuted, TextAlignmentOptions.Center);
                    SingleLine(emptyHint);
                    At(emptyHint, 0f, 97f, 448f, 29f);
                }
            }

            if (mode == "rows")
            {
                // 先頭スロットから連続で描く (ClampScroll 済み)。選択行はその中で
                // ハイライトされるだけで、スロット位置は曲順どおり。
                // 範囲外の index は描かない (空行にする)。
                UpdateListRows(rows, songs, selectIndex);
            }

            // 正本 OENRv のリスト下端はスクロールボタンとドットのみ。
            // 「1 / 28」のような選択位置テキストは設計に無いので描かない。
            UpdateDots(list);
        }

        /// <summary>
        /// 可視行スロットを既存オブジェクトの再利用で更新する。
        /// スロット s は常に i = listScroll + s の曲を表示し、y もスロット固定
        /// (RowY0 + RowStep * s) なので、選択移動では中身の差し替えだけで済む。
        /// </summary>
        private void UpdateListRows(GameObject rows, IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            // スロット配列は常に ListVisibleRows 個に揃える
            // (途中の空スロットで index がずれないようにする)。
            while (listRowObjects.Count < ListVisibleRows)
            {
                listRowObjects.Add(null);
                listRowSongIds.Add(-1);
            }

            int first = listScroll;
            for (int slot = 0; slot < ListVisibleRows; slot++)
            {
                int i = first + slot;
                bool valid = i >= 0 && i < songs.Count;
                GameObject row = listRowObjects[slot];
                if (row == null)
                {
                    if (!valid)
                    {
                        continue;
                    }

                    row = BuildSongRow(rows, songs[i], slot, i == selectIndex, RowY0 + RowStep * slot);
                    listRowObjects[slot] = row;
                    listRowSongIds[slot] = songs[i].SongId;
                    continue;
                }

                if (!valid)
                {
                    FlashBeat.UI.V5.V5GameUi.DestroyNow(row);
                    listRowObjects[slot] = null;
                    listRowSongIds[slot] = -1;
                    continue;
                }

                // SongId が変わったスロットだけ行名 ("SongRow <title>") を
                // 付け直す。毎回連結すると Refresh ごとに文字列を確保する。
                bool identityChanged = listRowSongIds[slot] != songs[i].SongId;
                UpdateSongRow(row, songs[i], i == selectIndex, RowY0 + RowStep * slot,
                    identityChanged);
                listRowSongIds[slot] = songs[i].SongId;
            }

            while (listRowObjects.Count > ListVisibleRows)
            {
                int last = listRowObjects.Count - 1;
                GameObject tail = listRowObjects[last];
                if (tail != null) FlashBeat.UI.V5.V5GameUi.DestroyNow(tail);
                listRowObjects.RemoveAt(last);
                listRowSongIds.RemoveAt(listRowSongIds.Count - 1);
            }
        }

        /// <summary>既存の曲行を別の曲データで書き換える (行オブジェクトは再利用)。</summary>
        private void UpdateSongRow(GameObject row, V6SongEntry song, bool selected, float y,
            bool rename)
        {
            var rt = row.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(RowX, -y);
            }

            // 行の内部座標は共通部品 V6Lists.SongRow (design mcCOI) と同じ:
            // 背景は非選択 $k1 / 選択 $k6。
            var bg = row.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = selected ? V6Tokens.Key6 : V6Tokens.Key1;
            }

            SetChildText(row, "Level/Level", song.Level > 0 ? song.Level.ToString() : "–");
            SetChildText(row, "Title", song.Title);
            SetChildText(row, "Composer", song.Artist);
            SetChildText(row, "Grade/Grade",
                string.IsNullOrEmpty(song.BestGrade) ? "–" : song.BestGrade);
            if (rename)
            {
                row.name = "SongRow " + song.Title;
            }

            SetJacket(V6Lists.Thumb(row), song, V6Tokens.Key2);
        }

        /// <summary>path で辿った子の TMP テキストを書き換える (無ければ何もしない)。</summary>
        private static void SetChildText(GameObject root, string path, string value)
        {
            Transform t = root != null ? root.transform.Find(path) : null;
            var tmp = t != null ? t.GetComponent<TextMeshProUGUI>() : null;
            if (tmp != null)
            {
                tmp.text = value;
            }
        }

        /// <summary>
        /// 行スロットのクリック結線。スロットが今表示している曲を選ぶ
        /// (行オブジェクトを再利用するので、結線はスロット index に対して 1 回だけ張る)。
        /// </summary>
        private void SelectListSlot(int slot)
        {
            if (slot < 0 || slot >= listRowSongIds.Count)
            {
                return;
            }

            int songId = listRowSongIds[slot];
            if (songId >= 0)
            {
                data.Select(songId);
            }
        }

        private GameObject BuildSongRow(GameObject parent, V6SongEntry song, int slot, bool selected, float y)
        {
            // 行の内部座標は共通部品 V6Lists.SongRow に一本化 (design mcCOI/Odv71 実測値)。
            // この画面はクリック→選択・ジャケット・グレード文字の結線だけを行う。
            var row = V6Lists.SongRow(parent, song.Title, song.Artist,
                song.Level > 0 ? song.Level.ToString() : "–", song.BestGrade,
                new Vector2(RowX, y), new Vector2(RowW, RowH), selected);
            var button = row.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            V6ButtonPressFeedback.Attach(row);
            button.onClick.AddListener(() => SelectListSlot(slot));

            SetJacket(V6Lists.Thumb(row), song, V6Tokens.Key2);
            return row;
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
        ///
        /// Issue #415: ドット個は曲数が変わったときだけ作り直し、選択の移動では
        /// 色とサイズだけを既存ドットへ適用する。
        /// </summary>
        private void UpdateDots(GameObject list)
        {
            var dotsT = list.transform.Find("Dot indicator");
            if (dotsT == null)
            {
                return;
            }

            GameObject dots = dotsT.gameObject;
            IReadOnlyList<V6SongEntry> all = data.AllSongs;
            int count = all == null ? 0 : all.Count;

            int active = -1;
            if (data.Selected != null && all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != null && all[i].SongId == data.Selected.SongId) { active = i; break; }
                }
            }
            active = V6SelectViewLogic.DotActiveIndex(active, count);

            if (count != dotCount)
            {
                // 曲数 (カタログ構造) が変わったときだけドットを作り直す。
                Common.V6Hierarchy.ClearChildren(dots);
                dotImages.Clear();
                dotRects.Clear();
                dotOffsetsCache = V6SelectViewLogic.DotOffsets(count, 0f);
                float total = V6SelectViewLogic.DotBottom(count, 0f);
                // レール幅は design どおり最大ドット幅 (10)。ドットはレール中央に置く。
                // Issue #427: レールはリストパネルの上下中央 (design の DotY=136 は上寄りだった)。
                At(dots, V6SelectViewLogic.DotX, V6SelectViewLogic.DotRailTopY(count),
                    V6SelectViewLogic.DotActiveSize, total);
                for (int i = 0; i < count; i++)
                {
                    var dot = V5Ui.Panel(dots, "Dot " + (i + 1),
                        new Vector2(V6SelectViewLogic.DotSize, V6SelectViewLogic.DotSize),
                        FromHex("#FFFFFF26"), V6SelectViewLogic.DotRadius);
                    dotImages.Add(dot.GetComponent<Image>());
                    dotRects.Add(dot.GetComponent<RectTransform>());
                    // anchor/pivot を (0,1) に揃える (サイズ・色・位置は下のループが確定する)。
                    At(dot, V6SelectViewLogic.DotRectX(V6SelectViewLogic.DotSize),
                        dotOffsetsCache[i], V6SelectViewLogic.DotSize, V6SelectViewLogic.DotSize);
                }

                dotCount = count;
            }

            float[] offsets = dotOffsetsCache ?? (dotOffsetsCache = V6SelectViewLogic.DotOffsets(count, 0f));
            for (int i = 0; i < count && i < dotImages.Count; i++)
            {
                bool isActive = i == active;
                float d = isActive ? V6SelectViewLogic.DotActiveSize : V6SelectViewLogic.DotSize;
                // Issue #427: 選択中は v6 の選択色 (Key6)。design の水色 Accent は
                // v6 の選択表現と一致しないため使わない。
                var img = dotImages[i];
                if (img != null)
                {
                    img.color = isActive ? V6Tokens.Key6 : FromHex("#FFFFFF26");
                }

                var rt = dotRects[i];
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(d, d);
                    rt.anchoredPosition = new Vector2(V6SelectViewLogic.DotRectX(d), -offsets[i]);
                }
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
                // design dRztp: Meta は縦 gap4・中央揃え。Title (0,8.5) 512×50、
                // Artist Row (0,62.5) 512×29 — 50 + 4 + 29 = 83 が 100 の中で中央。
                // 長い曲名は折り返さず自動縮小で 1 行に収める (Artist 行との重なり防止)。
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.enableAutoSizing = true;
                title.fontSizeMin = 30f;
                title.fontSizeMax = 48f;
                At(title, 0f, 8.5f, 512f, 50f);
                // Issue #427: Title と同じ 512 幅・中央揃えにして、曲名と作曲者名の
                // 中心軸を構造的に一致させる。タグ (No.N) は左端に絶対配置し、
                // アーティスト名はパネル全幅で中央寄せする (行グループの中央揃えだと
                // タグ分だけ文字が右へずれて見えた)。
                var tag = At(V5Ui.Panel(meta, "Tag No", new Vector2(63f, 29f), V5Tokens.Key4, 4f),
                    0f, 62.5f);
                var tagText = V5GameUi.Text(tag, "Text", "No." + song.SongId, 20f, V5Tokens.TextMuted,
                    TextAlignmentOptions.Center);
                Stretch(tagText.GetComponent<RectTransform>(), 63f, 29f);
                var artistText = V5GameUi.Text(meta, "Artist", song.Artist, 26f, V5Tokens.TextMuted,
                    TextAlignmentOptions.Center);
                artistText.textWrappingMode = TextWrappingModes.NoWrap;
                artistText.overflowMode = TextOverflowModes.Ellipsis;
                artistText.enableAutoSizing = true;
                artistText.fontSizeMin = 18f;
                artistText.fontSizeMax = 26f;
                At(artistText, 0f, 62.5f, 512f, 29f);
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

            centerHasSong = hasSong;
        }

        /// <summary>
        /// 選択曲パネル: scaffold が無いか選択曲の有無 (Meta 構造) が変わったときだけ
        /// 作り直し、それ以外は <see cref="UpdateCenter"/> で可変値を適用する。
        /// </summary>
        private void EnsureCenter()
        {
            bool hasSong = data.Selected != null;
            var panel = FindZone("Selected Song Panel");
            if (panel == null || hasSong != centerHasSong)
            {
                DestroyZone("Selected Song Panel");
                BuildCenter();
                return;
            }

            UpdateCenter(panel);
        }

        /// <summary>中央パネルの可変値 (ジャケット・曲名/作曲者・統計・Ready 結線) を適用する。</summary>
        private void UpdateCenter(GameObject panel)
        {
            var song = data.Selected;
            if (song == null)
            {
                // スケルトン (選択曲なし) は静的。null↔非 null の切替はゾーン再構築で処理する。
                return;
            }

            var hero = panel.transform.Find("Hero");
            if (hero != null)
            {
                SetJacket(hero.gameObject, song);
            }

            SetChildText(panel, "Meta/Title", song.Title);
            SetChildText(panel, "Meta/Tag No/Text", "No." + song.SongId);
            SetChildText(panel, "Meta/Artist", song.Artist);
            SetChildText(panel, "Stats/Stat BPM/Value",
                song.Bpm > 0 ? Mathf.RoundToInt((float)song.Bpm).ToString() : "–");
            SetChildText(panel, "Stats/Stat NOTES/Value",
                song.NoteCount > 0 ? song.NoteCount.ToString() : "–");
            SetChildText(panel, "Stats/Stat LEVEL/Value",
                song.Level > 0 ? song.Level.ToString() : "–");
            SetChildText(panel, "Stats/Stat BEST/Value",
                V6SelectViewLogic.FormatScore(song.BestScore));

            // Controls の play (Ready) は絞り込みで選択が隠れると押せなくなる。
            // 結線の有無を毎回張り直して表示と実動作を一致させる。
            var play = panel.transform.Find("Controls/Btn play");
            var playButton = play != null ? play.GetComponent<Button>() : null;
            if (playButton != null)
            {
                playButton.onClick.RemoveAllListeners();
                if (ReadyAvailable())
                {
                    playButton.onClick.AddListener(() =>
                    {
                        if (onReady != null) onReady();
                    });
                }
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
            // スコアは桁数が増えても折り返さない (FHD で「992,30 / 7」と 2 行化した実機報告 #379)。
            // 幅を超えた分は自動縮小で 1 行に収める。
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            valueText.enableAutoSizing = true;
            valueText.fontSizeMin = 24f;
            valueText.fontSizeMax = 32f;
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

        /// <summary>
        /// 右カラム (Ranking): scaffold が無ければ作り、続けて
        /// <see cref="UpdateRight"/> で可変値 (BEST・source・ランキング行) を適用する。
        /// </summary>
        private void EnsureRight()
        {
            var column = FindZone("Ranking Column");
            if (column == null)
            {
                BuildRightScaffold();
                column = FindZone("Ranking Column");
            }

            if (column == null)
            {
                return;
            }

            UpdateRight(column);
        }

        /// <summary>右カラムの骨格。可変値 (Score/Grade/Source/行) は UpdateRight が適用する。</summary>
        private void BuildRightScaffold()
        {
            var column = V5Ui.Node(frame.gameObject, "Ranking Column");
            At(column.GetComponent<RectTransform>(), MainX + RightX, MainY, RightW, ListH);

            var best = At(V5Ui.Panel(column, "Best", new Vector2(528f, 112f), Clear(), 0f), 16f, 24f);
            var bestLabel = V5GameUi.Text(best.gameObject, "Label", "BEST", 22f, V5Tokens.TextDim);
            At(bestLabel, 0f, 0f, 528f, 22f);

            var scoreRow = V5GameUi.Row(best.gameObject, "Score Row", 12f, TextAnchor.MiddleLeft);
            At(scoreRow.GetComponent<RectTransform>(), 0f, 30f, 528f, 72f);
            var scoreText = V5GameUi.Text(scoreRow.gameObject, "Score", "–", 56f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            // BEST の大きな数値も折り返さない (#379)。childControlWidth=false のため
            // 幅は明示する: Score 400 + Spacer 0 + Grade 72 + gap 12x2 = 496 ≤ 528。
            // 幅を超えた分は自動縮小で 1 行に収める。
            scoreText.textWrappingMode = TextWrappingModes.NoWrap;
            scoreText.enableAutoSizing = true;
            scoreText.fontSizeMin = 40f;
            scoreText.fontSizeMax = 56f;
            scoreText.rectTransform.sizeDelta = new Vector2(400f, 72f);
            var spacer = V5Ui.Node(scoreRow.gameObject, "Spacer");
            spacer.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1f;
            var gradeChip = At(V5Ui.Panel(scoreRow.gameObject, "Grade", new Vector2(72f, 64f), V5Tokens.Key6, 8f),
                0f, 0f);
            var gradeText = V5GameUi.Text(gradeChip.gameObject, "Text", "–", 34f, V5Tokens.Text,
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
            var sourceText = V5GameUi.Text(source, "Text", "–", 18f, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            Stretch(sourceText.GetComponent<RectTransform>(), 96f, 28f);

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
            // design IL4N7: 中央の "NO DATA" ((8,149) 496×35)。
            var noPlayers = V5GameUi.Text(players.gameObject, "No Data", "NO DATA", 24f,
                V5Tokens.TextDim, TextAlignmentOptions.Center);
            At(noPlayers, 8f, 149f, 496f, 35f);
        }

        /// <summary>右カラムの可変値 (BEST・source タグ・ランキング行/空表示) を適用する。</summary>
        private void UpdateRight(GameObject column)
        {
            var song = data.Selected;
            SetChildText(column, "Best/Score Row/Score",
                song != null ? V6SelectViewLogic.FormatScore(song.BestScore) : "–");
            SetChildText(column, "Best/Score Row/Grade/Text",
                song != null && !string.IsNullOrEmpty(song.BestGrade) ? song.BestGrade : "–");
            SetChildText(column, "Score Ranking/Head/Source/Text", RankingSourceLabel(song));
            UpdateRankingRows(column, song);
        }

        /// <summary>
        /// SCORE RANKING の行スロットと空表示を現在のランキングへ合わせる。
        /// rows ↔ empty のモード変化のときだけ表示を入れ替え、行はスロット再利用する。
        /// </summary>
        private void UpdateRankingRows(GameObject column, V6SongEntry song)
        {
            var rankingT = column.transform.Find("Score Ranking");
            if (rankingT == null)
            {
                return;
            }

            GameObject ranking = rankingT.gameObject;
            var rows = song != null ? data.RankingFor(song) : null;
            bool hasRows = rows != null && rows.Count > 0;
            string mode = hasRows ? "rows" : "empty";
            if (mode != rankMode)
            {
                // モード変化: 旧モードの表示 (行スロット or 空テキスト) を片付ける。
                for (int i = 0; i < rankRowObjects.Count; i++)
                {
                    if (rankRowObjects[i] != null)
                    {
                        FlashBeat.UI.V5.V5GameUi.DestroyNow(rankRowObjects[i]);
                    }
                }

                rankRowObjects.Clear();
                Transform oldEmpty = ranking.transform.Find("No Data");
                if (oldEmpty != null)
                {
                    FlashBeat.UI.V5.V5GameUi.DestroyNow(oldEmpty.gameObject);
                }

                rankEmptyGo = null;
                rankMode = mode;
            }

            if (mode == "empty")
            {
                // Issue #427: 取得中はテキストではなく控えめな「…」をパネル内に出す。
                // 失敗時はヘッダーの StatusMessage (エラー) が理由を伝える。
                if (rankEmptyGo == null)
                {
                    bool loading = data.IsLoading;
                    var empty = V5GameUi.Text(ranking, "No Data",
                        loading ? "…" : "NO DATA", loading ? 40f : 24f,
                        V5Tokens.TextDim, TextAlignmentOptions.Center);
                    At(empty, 8f, 155f, 496f, 35f);
                    rankEmptyGo = empty.gameObject;
                }
                else
                {
                    // 取得中 ↔ 完了で文言とサイズが変わるので既存テキストへ適用する。
                    var emptyText = rankEmptyGo.GetComponent<TextMeshProUGUI>();
                    if (emptyText != null)
                    {
                        bool loading = data.IsLoading;
                        emptyText.text = loading ? "…" : "NO DATA";
                        emptyText.fontSize = loading ? 40f : 24f;
                    }
                }

                return;
            }

            // 行スロット配列は常に RankingVisibleRows 個に揃える
            // (曲リストと同じく、空スロットで index がずれないようにする)。
            while (rankRowObjects.Count < RankingVisibleRows)
            {
                rankRowObjects.Add(null);
            }

            int first = rankingScroll;
            for (int slot = 0; slot < RankingVisibleRows; slot++)
            {
                int i = first + slot;
                bool valid = i >= 0 && i < rows.Count;
                GameObject row = rankRowObjects[slot];
                if (row == null)
                {
                    if (!valid)
                    {
                        continue;
                    }

                    row = BuildRankingRow(ranking, rows[i], 92f + 52f * slot);
                    rankRowObjects[slot] = row;
                    continue;
                }

                if (!valid)
                {
                    FlashBeat.UI.V5.V5GameUi.DestroyNow(row);
                    rankRowObjects[slot] = null;
                    continue;
                }

                UpdateRankingRow(row, rows[i], 92f + 52f * slot);
            }

            while (rankRowObjects.Count > RankingVisibleRows)
            {
                int last = rankRowObjects.Count - 1;
                GameObject tail = rankRowObjects[last];
                if (tail != null) FlashBeat.UI.V5.V5GameUi.DestroyNow(tail);
                rankRowObjects.RemoveAt(last);
            }
        }

        /// <summary>既存のランキング行を別の行データで書き換える (行オブジェクトは再利用)。</summary>
        private void UpdateRankingRow(GameObject row, V6RankingRow dataRow, float y)
        {
            var rt = row.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(8f, -y);
            }

            // CompactRankRow (design jnIY9) と同じ配色: YOU 行は $k6、他は $k1。
            var bg = row.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = dataRow.IsYou ? V6Tokens.Key6 : V6Tokens.Key1;
            }

            SetChildText(row, "Pos/PosText", dataRow.Position.ToString());
            var posText = row.transform.Find("Pos/PosText");
            var posTmp = posText != null ? posText.GetComponent<TextMeshProUGUI>() : null;
            if (posTmp != null)
            {
                posTmp.color = dataRow.IsYou ? V6Tokens.Text : V6Tokens.TextMuted;
            }

            SetChildText(row, "Name", dataRow.IsYou ? "YOU" : dataRow.PlayerName);
            SetChildText(row, "Value", V6SelectViewLogic.FormatScore(dataRow.Score));
            SetChildText(row, "Grade/GradeText",
                string.IsNullOrEmpty(dataRow.Grade) ? "–" : dataRow.Grade);
            row.name = "Score " + dataRow.Position;
        }

        /// <summary>ランキングの source タグ表示。実データ (クラウド配信か) から決める。</summary>
        private static string RankingSourceLabel(V6SongEntry song)
        {
            if (song == null) return "–";
            return song.IsCloud ? "オンライン" : "ローカル";
        }

        private GameObject BuildRankingRow(GameObject ranking, V6RankingRow row, float y)
        {
            // 行内部は共通部品 (design jnIY9 496×44) が持つ。座標計算をここへ書き戻さない。
            return V6Lists.CompactRankRow(ranking, row.Position.ToString(),
                row.IsYou ? "YOU" : row.PlayerName, V6SelectViewLogic.FormatScore(row.Score),
                row.Grade, new Vector2(8f, y), row.IsYou);
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

        /// <summary>設定カラム: scaffold が無ければ作り、値表示だけを更新する。</summary>
        private void EnsureSettings()
        {
            var column = FindZone("Settings Column");
            if (column == null)
            {
                BuildSettings();
                return;
            }

            UpdateSettings(column);
        }

        /// <summary>クイック設定の値表示を現在値へ合わせる (行オブジェクトは再利用)。</summary>
        private void UpdateSettings(GameObject column)
        {
            SetChildText(column, "Set Key Guide/Value", PlaySession.KeyGuideLabel(PlaySession.KeyGuide));
            SetChildText(column, "Set Lyrics/Value", PlaySession.LyricsLabel(PlaySession.LyricsEnabled));
            SetChildText(column, "Set NoteSpeed/Value", Mathf.RoundToInt(GManager.noteSpeed).ToString());
        }

        /// <summary>
        /// 設定行 1 個分 (design wSjAm)。列は 3 行で完全に共通 (Issue #370):
        /// Icon 24 @12 / Label @48 / Arrow · chevron-left @276 / Value 120 @328 /
        /// Arrow · chevron-right @460。値は左右の矢印の幾何学的な中央 (x388) に置き、
        /// 値の文字数で列を動かさない。
        /// </summary>
        private void BuildSettingsRow(GameObject column, int index, string label, string value, Action<int> adjust)
        {
            var rect = V6SelectViewLogic.SettingsRowRect(index);
            var row = At(V5Ui.Panel(column, "Set " + label, new Vector2(rect.W, rect.H),
                V5Tokens.Base, FlashBeat.UI.V6.Common.V6Tokens.RadiusMedium), rect.X, rect.Y);

            var iconRect = V6SelectViewLogic.SettingsRowIcon();
            var iconImage = V6Icons.Create(row, "list-filter", iconRect.W, V5Tokens.TextMuted);
            if (iconImage != null)
            {
                PlaceTopLeft(iconImage.gameObject, iconRect.X, iconRect.Y, iconRect.W, iconRect.H);
            }

            var labelRect = V6SelectViewLogic.SettingsRowLabel();
            var labelText = V5GameUi.Text(row.gameObject, "Label", label, 24f, V5Tokens.Text);
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            At(labelText, labelRect.X, labelRect.Y, labelRect.W, labelRect.H, new Vector2(0f, 0.5f));

            // 値は固定幅 120 の中央揃え (design の Value フレーム)。矢印の位置は値に依存しない。
            var valueRect = V6SelectViewLogic.SettingsRowValue();
            var valueText = V5GameUi.Text(row.gameObject, "Value", value, 24f, V5Tokens.Text,
                TextAlignmentOptions.Center);
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            At(valueText, valueRect.X, valueRect.Y, valueRect.W, valueRect.H, new Vector2(0f, 0.5f));

            var prevRect = V6SelectViewLogic.SettingsRowPrev();
            var left = Button(row, "Prev", prevRect.W, prevRect.H, 8f, V5Tokens.Key4,
                () => { adjust(-1); RefreshVolatile(); });
            At(left, prevRect.X, prevRect.Y);
            V6Icons.Create(left, "chevron-left", 24f, V5Tokens.TextMuted);

            var nextRect = V6SelectViewLogic.SettingsRowNext();
            var right = Button(row, "Next", nextRect.W, nextRect.H, 8f, V5Tokens.Key4,
                () => { adjust(1); RefreshVolatile(); });
            At(right, nextRect.X, nextRect.Y);
            V6Icons.Create(right, "chevron-right", 24f, V5Tokens.TextMuted);
        }

        // --- 入力ヘルパ ---

        private void ScrollList(int delta)
        {
            // スクロール (ホイール / ボタン) は選択曲を delta だけ動かす操作。
            // スクロール位置は差分更新で選択曲から常に再計算される (中央固定)。
            data.MoveSelection(delta);
            RefreshVolatile();
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
                RefreshVolatile();
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

        private void SetJacket(GameObject thumbPanel, V6SongEntry song, Color? fallback = null)
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
                // ジャケット無しは空枠。曲行 (design mcCOI) は $k2、Hero は $k3 (AqFtx)。
                img.type = Image.Type.Sliced;
                img.sprite = V5Ui.RoundedSprite(4);
                img.color = fallback ?? V5Tokens.Key3;
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
            // anchoredPosition は pivot の位置を指す。pivot を (0,1) から変えたときも
            // 矩形の左上が (x, -y) に来るよう pivot 分を補正する (旧実装は補正が無く、
            // 設定行の Label/Value が行の上へ 28px ずれていた)。
            rt.anchoredPosition = new Vector2(x + pivot.x * w, -(y + (1f - pivot.y) * h));
            return node.gameObject;
        }

        /// <summary>中央寄せで作られたアイコンを design の左上座標へ置き直す。</summary>
        private static void PlaceTopLeft(GameObject go, float x, float y, float w, float h)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
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
            V6ButtonPressFeedback.Attach(go);
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
