using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.UI.V6.Ranking
{
    /// <summary>
    /// Ranking 画面 (Pencil ノード S1TZD「04 Ranking · v6」) の v6 UI。
    /// design/flashbeat-ui.pen v6 をコードから uGUI として構築する (Prefab/Scene YAML は触らない)。
    ///
    /// - データは実データのみ: 曲カタログ・ベストスコア・オンラインランキングは
    ///   v6 選曲と同じ IV6SelectDataSource (V6SelectDataSource) 経由
    ///   (RankingClient.GetLeaderboard)。デザイン例の固定値 (AKI/RIN 等の
    ///   サンプル名・"1,000,000" 等の仮スコア) は決して出さない。
    /// - 対象曲は左の曲リストで切り替える (実カタログ全曲)。
    /// - SCORE RANKING は Local (実保存の自分のベスト) / Online (実 API) を
    ///   セグメントで切り替える。取得失敗時はエラー表示 + 再試行ボタン。
    /// - PLAYERS は design の見出し (Title + 右寄せ「累計スコア」) を描き、本文は
    ///   正直な空状態「データがありません」のみ (プレイヤー別の集計 API が既存に無いため、
    ///   表示は実データのみ。Issue #239)。design の左右矢印は死んだボタンだったため削除。
    /// - キー操作は統合担当が結線する (Input は直接読まない):
    ///   MoveSelection / ToggleSource / TriggerBack / TriggerOpenSettings / TriggerGotoSong。
    /// </summary>
    public sealed partial class V6RankingView
    {
        /// <summary>現在生成されている v6 ランキング画面 (ランキングシーンに 1 個だけ)。</summary>
        public static V6RankingView Current { get; private set; }

        // --- design 定数 (design 空間・フレーム左上原点 / y 下向き) ---
        // 正本: flashbeat-v6-nodes.json (S1TZD「04 Ranking」/ 契約 §4)。
        // 値の単一ソースは V6RankingLogic の design 定数群。
        private const float FrameW = V6RankingLogic.FrameW;
        private const float FrameH = V6RankingLogic.FrameH;
        private const float MainX = V6RankingLogic.MainX;
        private const float MainY = V6RankingLogic.MainY;
        private const float ListW = V6RankingLogic.ListPanelW;
        private const float ListH = V6RankingLogic.ListPanelH;
        private const float RowX = V6RankingLogic.SongRowX;
        private const float RowY0 = V6RankingLogic.SongRowY0;
        private const float RowW = V6RankingLogic.SongRowW;
        private const float RowH = V6RankingLogic.SongRowH;
        private const float RowStep = V6RankingLogic.SongRowStep;
        private const int ListVisibleRows = V6RankingLogic.ListVisibleRows;
        private const float RankingX = V6RankingLogic.ScorePanelX;
        private const float RankingY = V6RankingLogic.ScorePanelY;
        private const float RankingW = V6RankingLogic.ScorePanelW;
        private const float RankingH = V6RankingLogic.ScorePanelH;
        private const int RankingVisibleRows = V6RankingLogic.RankVisibleRows;
        private const float PlayersX = V6RankingLogic.PlayersPanelX;
        private const float PlayersW = V6RankingLogic.ScorePanelW;

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
        private int rankingScroll;
        private V6RankingLogic.RankingSource source = V6RankingLogic.RankingSource.Online;

        // --- Issue #415: 骨格保持の差分更新用の参照 ---
        // Rebuild() はゾーンの中身を破棄して作り直す (骨格変更・初回のみ)。
        // 差分更新は以下の参照を使い回して、変わった部分だけを既存要素へ適用する。
        private GameObject headerThumbGo;
        private TextMeshProUGUI headerTitleText;
        private TextMeshProUGUI headerArtistText;
        private readonly List<TextMeshProUGUI> headerStatValues = new List<TextMeshProUGUI>(); // LEVEL BPM NOTES
        private GameObject listGo;
        private GameObject rowsGo;
        private string listMode = ""; // "", "empty", "rows"
        private readonly List<GameObject> listRowObjects = new List<GameObject>();
        private GameObject dotsGo;
        private int dotsCount = -1;
        private readonly List<Image> dotImages = new List<Image>();
        private readonly List<RectTransform> dotRects = new List<RectTransform>();
        private GameObject scorePanelGo;
        private bool? scoreHasSong;
        private bool? scoreOnlineShown;
        private GameObject songChipThumbGo;
        private TextMeshProUGUI songChipText;
        private readonly List<GameObject> segmentObjects = new List<GameObject>(); // 0=Local, 1=Online (無い場合は 1 要素)
        private TextMeshProUGUI noDataText;
        private GameObject retryGo;
        private readonly List<GameObject> rankRowObjects = new List<GameObject>();

        /// <summary>Rebuild() で全参照を破棄済みオブジェクトと切り離す。</summary>
        private void ClearScaffoldRefs()
        {
            headerThumbGo = null;
            headerTitleText = null;
            headerArtistText = null;
            headerStatValues.Clear();
            listGo = null;
            rowsGo = null;
            listMode = "";
            listRowObjects.Clear();
            dotsGo = null;
            dotsCount = -1;
            dotImages.Clear();
            dotRects.Clear();
            scorePanelGo = null;
            scoreHasSong = null;
            scoreOnlineShown = null;
            songChipThumbGo = null;
            songChipText = null;
            segmentObjects.Clear();
            noDataText = null;
            retryGo = null;
            rankRowObjects.Clear();
        }

        private V6RankingView(IV6SelectDataSource data, Action onBack, Action onOpenSettings, Action onGotoSong)
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
        public static V6RankingView Create(RectTransform parent, Action onBack,
            IV6SelectDataSource dataSource = null, Action onOpenSettings = null, Action onGotoSong = null)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            IV6SelectDataSource source = dataSource ?? new V6SelectDataSource();
            var view = new V6RankingView(source, onBack, onOpenSettings, onGotoSong);
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

        /// <summary>曲リストの選択を delta 行だけ動かす (端では留まる)。</summary>
        public void MoveSelection(int delta)
        {
            data.MoveSelection(delta);
        }

        /// <summary>
        /// SCORE RANKING のセグメント (Local / Online) を切り替える。
        /// Online セグメントの無い曲 (cloud 以外) は切替先が無いので何もしない。
        /// </summary>
        public void ToggleSource()
        {
            if (!ShowOnlineSegmentForSelected())
            {
                return;
            }
            source = V6RankingLogic.NextSource(source);
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
            if (onGotoSong != null)
            {
                onGotoSong();
            }
        }

        /// <summary>オンラインランキング / カタログの再取得 (再試行ボタンと同じ経路)。</summary>
        public void RetryFetch()
        {
            data.RefreshAsync();
        }

        /// <summary>検証用: 表示中の曲数。</summary>
        public int VisibleSongCount
        {
            get { return data.AllSongs.Count; }
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

        private void BuildRoot(RectTransform parent)
        {
            rootGo = V5Ui.Node(parent.gameObject, "V6 Ranking View");
            var rt = rootGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            // 背景は敷かない (Issue #373)。History / Ranking は SelectScene 内の
            // モード切替なので、選曲画面と同じ背景映像をそのまま見せる。
            // 不透明な Image を置くとシーンの VideoPlayer を覆い隠す。

            // 共通フレーム (rw7zx 1728×972 / 契約 §4) を使い回す。
            frame = V6Frame.Create(rootGo.transform, "V6 Ranking Frame 1728x972").GetComponent<RectTransform>();
            // フレームは固定解像度 (1728×972) の等倍 (1:1) 配置。親 (画面) 左下を
            // 基準に design のフレーム原点へ置く。拡大縮小は行わない。
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.zero;
            frame.pivot = Vector2.zero;
            frame.sizeDelta = new Vector2(FrameW, FrameH);

            var driver = rootGo.AddComponent<V6RankingViewDriver>();
            driver.View = this;
            UpdatePlacement();
        }

        /// <summary>
        /// フレームを等倍 (1:1) で design のフレーム原点へ置く。
        /// 原点は純関数 V6RankingLogic.FrameOriginX/Y (design「Resolution Check」表どおり:
        /// FHD (96,54) / 2K (416,234) / MacBook (416,346); floor((screen − 1728×972)/2))。
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
                    V6RankingLogic.FrameOriginX(w),
                    V6RankingLogic.FrameOriginY(h));
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
            IReadOnlyList<V6SongEntry> songs = data.AllSongs;
            int selectIndex = -1;
            if (data.Selected != null)
            {
                for (int i = 0; i < songs.Count; i++)
                {
                    if (songs[i] != null && songs[i].SongId == data.Selected.SongId)
                    {
                        selectIndex = i;
                        break;
                    }
                }
            }
            if (revealSelection)
            {
                listScroll = V6SelectViewLogic.ScrollToReveal(selectIndex, listScroll, ListVisibleRows);
            }
            listScroll = V6SelectViewLogic.ClampScroll(listScroll, songs.Count, ListVisibleRows);
            rankingScroll = V6SelectViewLogic.ClampScroll(rankingScroll, RankingRows(EffectiveSource()).Count, RankingVisibleRows);

            GameObject headerZone = Zone("Header");
            GameObject footerZone = Zone("Footer");
            GameObject mainZone = Zone("Main");
            EnsureHeader(headerZone, data.Selected);
            EnsureFooter(footerZone);
            EnsureSongList(mainZone, songs, selectIndex);
            EnsureScoreRanking(mainZone, data.Selected);
            EnsurePlayers(mainZone);
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

    }
}
