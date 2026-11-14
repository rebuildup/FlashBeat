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
    ///
    /// Issue #385 Phase 2.4: 領域ごとに partial class へ分割した
    /// (キー入力・スクロール = V6SelectViewInput.cs、ヘッダー = V6SelectViewHeader.cs、
    /// フッター = V6SelectViewFooter.cs、曲リスト = V6SelectViewList.cs、
    /// 中央列 = V6SelectViewCenter.cs、右列 = V6SelectViewRight.cs、
    /// クイック設定 = V6SelectViewSettings.cs)。このファイルはライフサイクル・
    /// 公開 API・差分更新の骨格 (RefreshVolatile / ApplyVolatile)・frame 配置・
    /// 共有ユーティリティを持つ。
    /// </summary>
    public sealed partial class V6SelectView
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

        // --- 入力ヘルパ ---

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

        private void SetJacket(GameObject thumbPanel, V6SongEntry song, Color? fallback = null)
        {
            // Issue #385: ジャケットの適用手順は V6SongBadge.ApplyJacket に一本化
            // (V6JacketResources.LoadSprite は V6JacketProvider.Local と同一実体)。
            V6SongBadge.ApplyJacket(thumbPanel,
                song != null ? song.SongId : 0, song != null ? song.Title : null, fallback);
        }

        // --- uGUI ユーティリティ (design px 左上原点) ---

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

        // --- uGUI ユーティリティは V6Layout / V6Button / V6Hierarchy に集約 (Issue #385) ---

        private void AddScrollCatcher(GameObject target, int targetId)
        {
            var catcher = target.AddComponent<V6SelectScrollCatcher>();
            catcher.View = this;
            catcher.Target = targetId;
        }
    }
}
