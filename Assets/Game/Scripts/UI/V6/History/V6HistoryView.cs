using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Ranking;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.UI.V6.History
{
    /// <summary>
    /// History 画面 (Pencil ノード q9JZcH「03 History · v6」) の v6 UI。
    /// design/flashbeat-ui.pen v6 をコードから uGUI として構築する (Prefab/Scene YAML は触らない)。
    ///
    /// - データは実データのみ: 曲カタログ・ベストスコア・オンラインランキングは
    ///   v6 選曲と同じ IV6SelectDataSource (V6SelectDataSource) 経由。
    ///   デザイン例の固定値 (サンプル曲名・"988,513" 等) は決して出さない。
    /// - 履歴リストは「ベストスコアが記録されている曲」だけを表示する。
    ///   1 件も無い場合は空状態 (プレイ履歴がありません) を出す。
    /// - プレイ情報パネルは直前のプレイ (GManager の play 静的フィールド) を
    ///   選択中の曲が最後にプレイした曲のときだけ実数で出す。データが無い
    ///   (未プレイ / 別曲を選択中) 場合は "–"。design (q9JZcH) の 6 セル
    ///   (PERFECT / GREAT / BAD / MISS / HOLD / MAX COMBO) をすべて描き、HOLD は
    ///   現行譜面に HOLD ノーツが無いため有効なプレイでは常に "0" (design 同値)。
    /// - キー操作は統合担当が結線する (Input は直接読まない):
    ///   MoveSelection / ToggleSort / TriggerBack / TriggerOpenSettings / TriggerGotoSong。
    /// - SelectSceneManager は呼ばない。onBack / onOpenSettings / onGotoSong は統合担当が渡す。
    ///
    /// Issue #385 Phase 2.6: 領域ごとに partial class へ分割した
    /// (ヘッダー/フッター = V6HistoryViewHeader.cs、履歴リスト = V6HistoryViewList.cs、
    /// 中央列 = V6HistoryViewCenter.cs、右列 = V6HistoryViewRight.cs、
    /// 入力ヘルパ = V6HistoryViewInput.cs、毎フレーム駆動 = V6HistoryViewDriver.cs)。
    /// このファイルはライフサイクル・公開操作 API・差分更新の骨格
    /// (ApplyVolatile / Rebuild / Apply / Zone)・配置・ジャケット/スクロール受け皿を持つ。
    /// </summary>
    public sealed partial class V6HistoryView
    {
        /// <summary>現在生成されている v6 履歴画面 (履歴シーンに 1 個だけ)。</summary>
        public static V6HistoryView Current { get; private set; }

        // --- design 定数 (design 空間・フレーム左上原点 / y 下向き) ---
        // 正本: flashbeat-v6-nodes.json (q9JZcH「03 History」/ 契約 §4)。
        // 値の単一ソースは V6HistoryLogic の design 定数群。
        private const float FrameW = V6HistoryLogic.FrameW;
        private const float FrameH = V6HistoryLogic.FrameH;

        // design Frame (1728x972) の Main ゾーンは (24, 96)。
        private const float MainX = V6HistoryLogic.MainX;
        private const float MainY = V6HistoryLogic.MainY;
        private const float ListW = V6HistoryLogic.ListPanelW;
        private const float ListH = V6HistoryLogic.ListPanelH;
        private const float RowX = V6HistoryLogic.HistoryRowX;
        private const float RowY0 = V6HistoryLogic.HistoryRowY0;
        private const float RowH = V6HistoryLogic.HistoryRowH;
        private const float RowStep = V6HistoryLogic.HistoryRowStep;
        private const int ListVisibleRows = V6HistoryLogic.HistoryVisibleRows;
        private const float CenterX = V6HistoryLogic.CenterPanelX;
        private const float CenterW = V6HistoryLogic.CenterPanelW;
        private const float RightX = V6HistoryLogic.RightColumnX;
        private const float RightPanelX = V6HistoryLogic.RightPanelX;
        private const float RightPanelW = V6HistoryLogic.RightPanelW;
        private const int SongRankingVisibleRows = V6HistoryLogic.SongRankVisibleRows;

        private readonly IV6SelectDataSource data;
        private readonly Action onBack;
        private readonly Action onOpenSettings;
        private readonly Action onGotoSong;

        private GameObject rootGo;
        private RectTransform frame;

        private bool placed;
        private int lastWidth;
        private int lastHeight;
        private int listScroll;
        private int songRankingScroll;
        private V6HistoryLogic.HistorySort sort = V6HistoryLogic.HistorySort.Score;
        private int selectedSongId = -1;

        // --- Issue #415: 骨格保持の差分更新用の参照 ---
        // Rebuild() はゾーンの中身を破棄して作り直す (骨格変更・初回のみ)。
        // 差分更新は以下の参照を使い回して、変わった部分だけを既存要素へ適用する。
        private TextMeshProUGUI headerSortText;
        private TextMeshProUGUI headerCountText;
        private GameObject historyListGo;
        private GameObject historyRowsGo;
        private string listMode = ""; // "", "empty", "rows"
        private readonly List<GameObject> historyRowObjects = new List<GameObject>();
        private GameObject centerGo;
        private bool? centerHasSong;
        private GameObject centerHeroGo;
        private TextMeshProUGUI centerTitleText;
        private TextMeshProUGUI centerArtistText;
        private TextMeshProUGUI centerTagText;
        private readonly List<TextMeshProUGUI> centerStatValues = new List<TextMeshProUGUI>();
        private GameObject rightGo;
        private TextMeshProUGUI infoNicknameText;
        private readonly List<TextMeshProUGUI> infoValueTexts = new List<TextMeshProUGUI>(); // 累計 / プレイ曲 / S以上 / 順位
        private TextMeshProUGUI playScoreValueText;
        private readonly List<TextMeshProUGUI> judgeValueTexts = new List<TextMeshProUGUI>(); // PERFECT GREAT BAD MISS HOLD MAX COMBO
        private GameObject songRankPanelGo;
        private bool? songRankHasSong;
        private GameObject songRankThumbGo;
        private TextMeshProUGUI songRankChipText;
        private TextMeshProUGUI songRankNoDataText;
        private readonly List<GameObject> songRankRowObjects = new List<GameObject>();

        /// <summary>Rebuild() で全参照を破棄済みオブジェクトと切り離す。</summary>
        private void ClearScaffoldRefs()
        {
            headerSortText = null;
            headerCountText = null;
            historyListGo = null;
            historyRowsGo = null;
            listMode = "";
            historyRowObjects.Clear();
            centerGo = null;
            centerHasSong = null;
            centerHeroGo = null;
            centerTitleText = null;
            centerArtistText = null;
            centerTagText = null;
            centerStatValues.Clear();
            rightGo = null;
            infoNicknameText = null;
            infoValueTexts.Clear();
            playScoreValueText = null;
            judgeValueTexts.Clear();
            songRankPanelGo = null;
            songRankHasSong = null;
            songRankThumbGo = null;
            songRankChipText = null;
            songRankNoDataText = null;
            songRankRowObjects.Clear();
        }

        private V6HistoryView(IV6SelectDataSource data, Action onBack, Action onOpenSettings, Action onGotoSong)
        {
            this.data = data;
            this.onBack = onBack;
            this.onOpenSettings = onOpenSettings;
            this.onGotoSong = onGotoSong;
        }

        /// <summary>
        /// View を生成する。統合担当から呼ぶ。
        /// dataSource を省略すると新規の V6SelectDataSource を作る
        /// (Select 画面と 1 個のデータソースを共有したい場合は渡す)。
        /// </summary>
        public static V6HistoryView Create(RectTransform parent, Action onBack,
            IV6SelectDataSource dataSource = null, Action onOpenSettings = null, Action onGotoSong = null)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            IV6SelectDataSource source = dataSource ?? new V6SelectDataSource();
            var view = new V6HistoryView(source, onBack, onOpenSettings, onGotoSong);
            view.BuildRoot(parent);
            source.Changed += view.OnDataChanged;
            view.Rebuild(false);
            Current = view;
            return view;
        }

        public RectTransform Root
        {
            get { return frame; }
        }

        /// <summary>差分更新の起点 (data.Changed / 統合担当からの呼び出し)。骨格があれば再利用する。</summary>
        public void Refresh()
        {
            ApplyVolatile(true);
        }

        public void SetVisible(bool visible)
        {
            if (rootGo != null)
            {
                rootGo.SetActive(visible);
            }
        }

        /// <summary>破棄。イベント購読を解除してから root を壊す。</summary>
        public void Dispose()
        {
            if (data != null)
            {
                data.Changed -= OnDataChanged;
            }
            if (rootGo != null)
            {
                V5GameUi.DestroyNow(rootGo);
            }
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }
        }

        // --- 統合担当が結線する操作 (Input は直接読まない) ---

        /// <summary>履歴リストの選択を delta 行だけ動かす (端では留まる)。</summary>
        public void MoveSelection(int delta)
        {
            List<V6SongEntry> history = V6HistoryLogic.HistoryList(data.AllSongs, sort);
            if (history.Count == 0)
            {
                return;
            }
            int current = IndexOfSong(history, selectedSongId);
            int target = current < 0
                ? (delta >= 0 ? 0 : history.Count - 1)
                : Mathf.Clamp(current + delta, 0, history.Count - 1);
            SelectSong(history[target].SongId);
        }

        /// <summary>並び順 (スコア順 / カタログ順) を切り替える。</summary>
        public void ToggleSort()
        {
            sort = V6HistoryLogic.NextSort(sort);
            ApplyVolatile(false);
        }

        /// <summary>戻る (統合担当が onBack で遷移を決める)。</summary>
        public void TriggerBack()
        {
            if (onBack != null)
            {
                onBack();
            }
        }

        /// <summary>設定ボタン (既存の設定経路は統合担当が渡す)。</summary>
        public void TriggerOpenSettings()
        {
            if (onOpenSettings != null)
            {
                onOpenSettings();
            }
        }

        /// <summary>「この曲へ」(選択中の曲を選曲へ送る。遷移は統合担当が決める)。</summary>
        public void TriggerGotoSong()
        {
            // 統合担当 (OnV6GotoSong) は共有データソースの Selected を選曲画面の選択として使う。
            // View のハイライトは Rebuild が決めるので、まだ一度も曲送りしていない
            // (= data.Select を呼んでいない) 状態では data.Selected が空のままになり、
            // 「この曲へ」で選曲画面に戻っても何も選ばれていない、という食い違いが起きる。
            // 押した時点でハイライト中の曲をデータ層へ反映してから遷移要求を出す。
            // (表示のたびに data を書き換えると #222 の「既定選択はしない」を壊すので、
            //  ここ = 明示操作のときだけ同期する。)
            if (selectedSongId >= 0)
            {
                data.Select(selectedSongId);
            }
            if (onGotoSong != null)
            {
                onGotoSong();
            }
        }

        /// <summary>検証用: 表示中の履歴件数。</summary>
        public int VisibleHistoryCount
        {
            get { return V6HistoryLogic.HistoryList(data.AllSongs, sort).Count; }
        }

        /// <summary>検証・計測用: 画面全体を作り直した回数。</summary>
        public int RebuildCountForAudit { get; private set; }

        /// <summary>検証・計測用: 変動部分だけを更新した回数。</summary>
        public int IncrementalUpdateCountForAudit { get; private set; }

        // --- 内部 ---

        private void OnDataChanged()
        {
            if (rootGo == null || !rootGo.activeInHierarchy)
            {
                return;
            }
            ApplyVolatile(true);
        }

        private void SelectSong(int songId)
        {
            if (selectedSongId == songId)
            {
                return;
            }
            selectedSongId = songId;
            // オンラインランキングの取得 (cloud 曲) と Changed 通知はデータソース側。
            data.Select(songId);
            ApplyVolatile(false);
        }

        private void BuildRoot(RectTransform parent)
        {
            rootGo = V5Ui.Node(parent.gameObject, "V6 History View");
            var rt = rootGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            // 背景は敷かない (Issue #373)。History / Ranking は SelectScene 内の
            // モード切替なので、選曲画面と同じ背景映像をそのまま見せる。
            // 不透明な Image を置くとシーンの VideoPlayer を覆い隠す。

            // 共通フレーム (rw7zx 1728×972 / 契約 §4) を使い回す。
            frame = V6Frame.Create(rootGo.transform, "V6 History Frame 1728x972").GetComponent<RectTransform>();
            // フレームは固定解像度 (1728×972) の等倍 (1:1) 配置。親 (画面) 左下を
            // 基準に design のフレーム原点へ置く。拡大縮小は行わない。
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.zero;
            frame.pivot = Vector2.zero;
            frame.sizeDelta = new Vector2(FrameW, FrameH);

            var driver = rootGo.AddComponent<V6HistoryViewDriver>();
            driver.View = this;
            UpdatePlacement();
        }

        /// <summary>
        /// フレームを 1:1 (等倍) で design のフレーム原点へ置く。
        /// 原点は純関数 V6HistoryLogic.FrameOriginX/Y (design「Resolution Check」表どおり、
        /// FHD (96,54) / 2K (416,234) / Mac (416,346))。
        /// </summary>
        internal void UpdatePlacement()
        {
            int w = Screen.width;
            int h = Screen.height;
            if (placed && w == lastWidth && h == lastHeight)
            {
                return;
            }
            placed = true;
            lastWidth = w;
            lastHeight = h;
            if (frame != null)
            {
                frame.localScale = Vector3.one; // 1:1 (引き伸ばしは行わない)
                frame.anchoredPosition = new Vector2(
                    V6HistoryLogic.FrameOriginX(w),
                    V6HistoryLogic.FrameOriginY(h));
            }
        }

        /// <summary>
        /// Issue #415: 骨格 (Header / Footer / Main の子) が残っていれば保持したまま、
        /// 変わった部分だけを既存要素へ適用する。骨格が無いときだけ全再構築する。
        /// </summary>
        private void ApplyVolatile(bool revealSelection)
        {
            if (rootGo == null)
            {
                return;
            }

            UpdatePlacement();

            // Header/Footer/Main の中身が 1 度も作られていない (初回) ときは全再構築。
            GameObject mainZone = Zone("Main");
            if (mainZone.transform.childCount == 0)
            {
                Rebuild(revealSelection);
                return;
            }

            Apply(revealSelection);
            IncrementalUpdateCountForAudit++;
        }

        /// <summary>全再構築 (骨格変更・初回)。参照はすべて破棄したうえで作り直す。</summary>
        private void Rebuild(bool revealSelection)
        {
            if (rootGo == null)
            {
                return;
            }

            UpdatePlacement();

            // 共通フレーム (V6Frame) の stroke / モーダル層は残し、
            // Header / Main / Footer の各ゾーンの中身を作り直す。
            V6Hierarchy.ClearChildren(Zone("Header"));
            V6Hierarchy.ClearChildren(Zone("Footer"));
            V6Hierarchy.ClearChildren(Zone("Main"));
            ClearScaffoldRefs();

            Apply(revealSelection);
            RebuildCountForAudit++;
        }

        /// <summary>ゾーンの中身を (骨格ありなら差分で・なければ新規に) 構築する。</summary>
        private void Apply(bool revealSelection)
        {
            List<V6SongEntry> history = V6HistoryLogic.HistoryList(data.AllSongs, sort);
            int selectIndex = IndexOfSong(history, selectedSongId);
            if (selectIndex < 0 && history.Count > 0)
            {
                // 初回 / 選択が外れた (記録が消えた等) ときは先頭を選ぶ。
                selectedSongId = history[0].SongId;
                selectIndex = 0;
            }
            if (revealSelection)
            {
                listScroll = V6SelectViewLogic.ScrollToReveal(selectIndex, listScroll, ListVisibleRows);
            }
            listScroll = V6SelectViewLogic.ClampScroll(listScroll, history.Count, ListVisibleRows);

            V6SongEntry selected = selectIndex >= 0 ? history[selectIndex] : null;
            int rankingRowCount = SongRankingRows(selected).Count;
            songRankingScroll = V6SelectViewLogic.ClampScroll(songRankingScroll, rankingRowCount, SongRankingVisibleRows);

            GameObject headerZone = Zone("Header");
            GameObject footerZone = Zone("Footer");
            GameObject mainZone = Zone("Main");
            EnsureHeader(headerZone, history.Count);
            EnsureFooter(footerZone);
            EnsureList(mainZone, history, selectIndex);
            EnsureCenter(mainZone, selected);
            EnsureRight(mainZone, selected);
        }

        /// <summary>V6Frame が作った共通ゾーン (Header / Main / Footer)。</summary>
        private GameObject Zone(string name)
        {
            Transform t = frame != null ? frame.Find(name) : null;
            if (t == null)
            {
                throw new InvalidOperationException("V6Frame zone not found: " + name);
            }
            return t.gameObject;
        }

        // --- ジャケット ---

        private void SetJacket(GameObject thumbPanel, V6SongEntry song)
        {
            // Issue #398 / #385: ジャケットの Sprite は V6JacketProvider が所有し、
            // 適用手順は V6SongBadge.ApplyJacket に一本化した
            // (ここで Sprite.Create して独自辞書に持つと、同じ画像が画面ごとに
            // 何枚も実体化され、View を破棄しても解放されない)。
            V6SongBadge.ApplyJacket(thumbPanel,
                song != null ? song.SongId : 0, song != null ? song.Title : null);
        }

        // --- uGUI ユーティリティは V6Layout / V6Button / V6Hierarchy に集約 (Issue #385) ---

        private void AddScrollCatcher(GameObject target, int targetId)
        {
            var catcher = target.AddComponent<V6HistoryScrollCatcher>();
            catcher.View = this;
            catcher.Target = targetId;
        }
    }

}
