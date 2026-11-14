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
    public sealed class V6RankingView
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

        // --- ヘッダー (共通フレームの Header ゾーン: design (24,0) 1680×96) ---

        /// <summary>
        /// Issue #415: ヘッダーは骨格を保持し、ジャケット・曲名・アーティスト・
        /// 統計値 (LEVEL/BPM/NOTES) だけを既存要素へ適用する。
        /// </summary>
        private void EnsureHeader(GameObject headerZone, V6SongEntry song)
        {
            if (headerZone.transform.childCount == 0)
            {
                CreateHeaderScaffold(headerZone, song);
            }

            ApplyHeader(song);
        }

        private void ApplyHeader(V6SongEntry song)
        {
            bool hasSong = song != null;
            if (headerThumbGo != null)
            {
                SetJacket(headerThumbGo, song);
            }

            if (headerTitleText != null) headerTitleText.text = hasSong ? song.Title : "–";
            if (headerArtistText != null) headerArtistText.text = hasSong ? song.Artist : "";

            if (headerStatValues.Count == 3)
            {
                headerStatValues[0].text = hasSong && song.Level > 0 ? song.Level.ToString() : "–";
                headerStatValues[1].text = hasSong && song.Bpm > 0
                    ? Mathf.RoundToInt((float)song.Bpm).ToString() : "–";
                headerStatValues[2].text = hasSong && song.NoteCount > 0 ? song.NoteCount.ToString() : "–";
            }
        }

        private void CreateHeaderScaffold(GameObject headerZone, V6SongEntry song)
        {
            // 戻るボタン + タイトルは共通部品 (Issue #385)。design S1TZD:
            // header (0,20) 56×56 / Title (72,20) 400×56 fs38。
            // 確定キーはキー設定の正本から取る。
            V6Header.BackButton(headerZone, "Back Button",
                ActionKeyHint.Primary(KeyActions.KeyAction.Back), () => TriggerBack());
            V6Header.Title(headerZone, "RANKING");

            // 右上: 曲情報 (ジャケット + 曲名/作家 + LEVEL / BPM / NOTES)。
            // design (S1TZD 情報ゾーン w4siL): header 内 x840 / w840 / padding [0,24] /
            // gap16 の横並び。Thumb 64×36 → Song 320×76 → Divider 1×44 →
            // Stat 100×62 ×3 がこの順で並ぶ (x は gap16 を挟んだ実測値)。
            // 曲バッジ部 (Thumb + Song 列) は共通部品 (Issue #385 Phase 1.2)。
            bool hasSong = song != null;
            var badge = V6SongBadge.Create(headerZone, new V6SongBadge.Spec
            {
                ThumbPos = new Vector2(864f, 30f),
                ThumbSize = new Vector2(64f, 36f),
                ThumbColor = V5Tokens.Key3,
                ColumnName = "Song",
                ColumnPos = new Vector2(944f, 10f),
                ColumnSize = new Vector2(320f, 76f),
                // design w4siL/Song: Title (0,1) 320×43 fs30 / Artist (0,46) 320×29 fs20。
                // 旧実装は Title に Rect を与えておらず、既定の 100×100 中央寄せで
                // design より右下へずれていた (1 行固定 + ellipsis で列内に収める)。
                Title = new V6SongBadge.Line
                {
                    Pos = new Vector2(0f, 1f), Size = new Vector2(320f, 43f), FontSize = 30f,
                    Color = V5Tokens.Text, Text = hasSong ? song.Title : "–",
                    // design h43 < 行送り (fs30 × 1.448 = 43.44) のため、Ellipsis が 0 行に
                    // なって曲名が消える (r3 実機で発生)。margin 補正で実効高を確保する。
                    FitLineHeight = true,
                },
                Sub = new V6SongBadge.Line
                {
                    Name = "Artist", Pos = new Vector2(0f, 46f), Size = new Vector2(320f, 29f),
                    FontSize = 20f, Color = V5Tokens.TextMuted, Text = hasSong ? song.Artist : "",
                    FitLineHeight = true,
                },
            });
            headerThumbGo = badge.Thumb;
            headerTitleText = badge.Title;
            headerArtistText = badge.Sub;
            if (hasSong)
            {
                SetJacket(headerThumbGo, song);
            }

            V6Layout.Place(V5Ui.Panel(headerZone, "Divider", new Vector2(1f, 44f), V5Tokens.FromHex("#FFFFFF26"), 0f), 1280f, 26f);

            headerStatValues.Add(BuildHeaderStat(headerZone, 1297f, "LEVEL", ""));
            headerStatValues.Add(BuildHeaderStat(headerZone, 1413f, "BPM", ""));
            headerStatValues.Add(BuildHeaderStat(headerZone, 1529f, "NOTES", ""));
        }

        /// <summary>ヘッダー右の統計 (100x62: label 上 / value 下)。値テキストを返す (差分更新で書き換えるため)。</summary>
        private TextMeshProUGUI BuildHeaderStat(GameObject header, float x, string label, string value)
        {
            var cell = V5Ui.Node(header, "Stat " + label);
            V6Layout.Place(cell.GetComponent<RectTransform>(), x, 17f, 100f, 62f);
            var col = V5GameUi.Column(cell, "Inner", 2f, TextAnchor.MiddleLeft);
            // 子の幅は TMP の preferredWidth に従わせる。childControlWidth=false のままだと
            // 既定の矩形幅 (200) のまま 100 幅のセルから右へはみ出す。
            col.childControlWidth = true;
            V6Layout.StretchCenter(col.GetComponent<RectTransform>(), 100f, 62f);
            V5GameUi.Text(col.gameObject, "Label", label, 16f, V5Tokens.TextDim, TextAlignmentOptions.Left);
            return V5GameUi.Text(col.gameObject, "Value", value, 24f, V5Tokens.Text, TextAlignmentOptions.Left,
                FontStyles.Bold);
        }

        // --- フッター (共通フレームの Footer ゾーン: design (24,876) 1680×96) ---

        /// <summary>
        /// Issue #415: フッターは状態で変わらないので、中身が無いときだけ作る。
        /// </summary>
        private void EnsureFooter(GameObject footerZone)
        {
            if (footerZone.transform.childCount > 0)
            {
                return;
            }

            BuildFooter(footerZone);
        }

        private void BuildFooter(GameObject footerZone)
        {
            // design (S1TZD フッター): キー凡例は 4 項目 (↑↓ 曲送り / Space 決定 /
            // Tab 切替 / Esc 戻る)。項目間 gap24 / KeyCap padding [0,8] / Cap-Label gap8。
            // 切替は Ranking 画面では Tab (SelectSceneManager.HandleRankingInput が
            // Tab と S の両方を受ける)。F5 再試行は design に凡例が無いので出さない。
            V6Legend.KeyLegend(footerZone, new[]
            {
                new[] { "↑↓", "曲送り" },
                new[] { "Space", "決定" },
                new[] { "Tab", "切替" },
                new[] { "Esc", "戻る" }
            });

            // design (S1TZD 操作ボタンゾーン): 右下は「この曲へ」(567,20) 273×56 のみ。
            // 「補助操作」ゾーン (358,20) 197×56 は design で子ノードを持たない
            // 空プレースホルダなのでボタンは描かない (O キーの設定遷移は残す)。
            GameObject gotoBtn = V6Controls.ButtonPrimary(footerZone, "この曲へ",
                new Vector2(V6RankingLogic.FooterGotoButtonX, V6RankingLogic.FooterButtonY),
                new Vector2(273f, V6RankingLogic.FooterButtonH), "corner-down-left", "Space");
            V6Button.Configure(gotoBtn, TriggerGotoSong);
        }

        // --- 左列: 曲リスト (x0 w560 #131313CC r16) ---

        /// <summary>
        /// Issue #415: リストは骨格 (パネル・Rows コンテナ) を保持し、行はスロット単位で
        /// 既存オブジェクトを更新する。空状態 ↔ 曲表示のモード変化のときだけ Rows を作り直す。
        /// </summary>
        private void EnsureSongList(GameObject mainZone, IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            GameObject list = listGo != null ? listGo : mainZone.transform.Find("Song List")?.gameObject;
            if (list == null)
            {
                list = CreateListScaffold(mainZone);
            }

            UpdateListRows(list, songs, selectIndex);
            UpdateDots(list);
        }

        private GameObject CreateListScaffold(GameObject mainZone)
        {
            var list = V6Layout.Place(V5Ui.Panel(mainZone, "Song List", new Vector2(ListW, ListH),
                V5Tokens.FromHex("#131313CC"), 16f), 0f, 0f);
            AddScrollCatcher(list, 0);

            var rows = V5Ui.Node(list, "Rows");
            V6Layout.Place(rows.GetComponent<RectTransform>(), 0f, 0f, ListW, ListH);
            listGo = list;
            rowsGo = rows;

            BuildScrollButton(list, "Scroll Up", 0f, "chevron-up", -1);
            BuildScrollButton(list, "Scroll Down", 732f, "chevron-down", 1);
            return list;
        }

        private void UpdateListRows(GameObject list, IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            GameObject rows = rowsGo != null ? rowsGo : list.transform.Find("Rows")?.gameObject;
            if (rows == null)
            {
                rows = V5Ui.Node(list, "Rows");
                V6Layout.Place(rows.GetComponent<RectTransform>(), 0f, 0f, ListW, ListH);
                rowsGo = rows;
            }

            string mode = songs.Count == 0 ? "empty" : "rows";
            if (mode != listMode)
            {
                // モード変化 (曲表示 ↔ 空状態) は骨格変更扱い。Rows の中だけを作り直す。
                V6Hierarchy.ClearChildren(rows);
                listRowObjects.Clear();
                listMode = mode;
                if (mode == "empty")
                {
                    var empty = V5GameUi.Text(rows.gameObject, "No Songs", "曲がありません", 24f, V5Tokens.TextDim,
                        TextAlignmentOptions.Center);
                    V6Layout.StretchCenter(empty.GetComponent<RectTransform>(), ListW, ListH);
                    return;
                }
            }

            int first = listScroll;
            for (int slot = 0; slot < ListVisibleRows; slot++)
            {
                int i = first + slot;
                bool valid = i >= 0 && i < songs.Count;
                float y = RowY0 + RowStep * slot;
                GameObject row = slot < listRowObjects.Count ? listRowObjects[slot] : null;
                if (row == null)
                {
                    if (!valid)
                    {
                        if (slot < listRowObjects.Count) listRowObjects[slot] = null;
                        continue;
                    }

                    row = CreateSongRow(rows, songs[i], i == selectIndex, y);
                    if (slot < listRowObjects.Count) listRowObjects[slot] = row;
                    else listRowObjects.Add(row);
                    continue;
                }

                if (!valid)
                {
                    V6Hierarchy.DetachAndDestroy(row);
                    listRowObjects[slot] = null;
                    continue;
                }

                UpdateSongRow(row, songs[i], i == selectIndex, y);
            }

            while (listRowObjects.Count > ListVisibleRows)
            {
                int tail = listRowObjects.Count - 1;
                V6Hierarchy.DetachAndDestroy(listRowObjects[tail]);
                listRowObjects.RemoveAt(tail);
            }
        }

        private GameObject CreateSongRow(GameObject parent, V6SongEntry song, bool selected, float y)
        {
            // 行の内部座標は共通部品 V6Lists.SongRow に一本化 (design mcCOI 実測値)。
            var row = V6Lists.SongRow(parent, song.Title, song.Artist,
                song.Level > 0 ? song.Level.ToString() : "–", song.BestGrade,
                new Vector2(RowX, y), new Vector2(RowW, RowH), selected);
            var button = V6Button.Make(row, null).GetComponent<Button>();
            int songId = song.SongId;
            button.onClick.AddListener(() => data.Select(songId));

            SetJacket(V6Lists.Thumb(row), song, V6Tokens.Key2);
            return row;
        }

        /// <summary>既存の曲行を新しい曲データで書き換える (行オブジェクトは再利用)。</summary>
        private void UpdateSongRow(GameObject row, V6SongEntry song, bool selected, float y)
        {
            var rt = row.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(RowX, -y);
            }

            var bg = row.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = selected ? V6Tokens.Key6 : V6Tokens.Key1;
            }

            V6Hierarchy.SetChildText(row, "Level/Level", song.Level > 0 ? song.Level.ToString() : "–");
            V6Hierarchy.SetChildText(row, "Title", song.Title);
            V6Hierarchy.SetChildText(row, "Composer", song.Artist);
            V6Hierarchy.SetChildText(row, "Grade/Grade", string.IsNullOrEmpty(song.BestGrade) ? "–" : song.BestGrade);
            row.name = "SongRow " + song.Title;

            var button = row.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                int songId = song.SongId;
                button.onClick.AddListener(() => data.Select(songId));
            }

            SetJacket(V6Lists.Thumb(row), song, V6Tokens.Key2);
        }

        /// <summary>
        /// ドットインジケータの増減分更新。件数が変わったときだけ作り直し、
        /// 選択の移動では既存ドットの色とサイズだけを差分適用する。
        /// </summary>
        private void UpdateDots(GameObject list)
        {
            IReadOnlyList<V6SongEntry> songs = data.AllSongs;
            int count = songs == null ? 0 : songs.Count;
            if (dotsGo == null || dotsCount != count)
            {
                // 件数変化 = 骨格変更。ドットのみ作り直す。
                GameObject old = list.transform.Find("Dot indicator")?.gameObject;
                V6Hierarchy.DetachAndDestroy(old);

                dotImages.Clear();
                dotRects.Clear();
                dotsGo = CreateDots(list, songs, count);
                dotsCount = count;
            }

            int selectIndex = -1;
            if (data.Selected != null && songs != null)
            {
                for (int i = 0; i < songs.Count; i++)
                {
                    if (songs[i] != null && songs[i].SongId == data.Selected.SongId) { selectIndex = i; break; }
                }
            }
            int active = V6SelectViewLogic.DotActiveIndex(selectIndex, count);
            float[] offsets = V6SelectViewLogic.DotOffsets(count, 0f);
            for (int i = 0; i < count; i++)
            {
                bool isActive = i == active;
                float d = isActive ? V6SelectViewLogic.DotActiveSize : V6SelectViewLogic.DotSize;
                if (i < dotImages.Count && dotImages[i] != null)
                {
                    // Issue #427: 選択中は v6 の選択色 (Key6)。
                    dotImages[i].color = isActive ? V6Tokens.Key6 : V5Tokens.FromHex("#FFFFFF26");
                }

                if (i < dotRects.Count && dotRects[i] != null)
                {
                    dotRects[i].sizeDelta = new Vector2(d, d);
                    dotRects[i].anchoredPosition = new Vector2(V6SelectViewLogic.DotRectX(d), -offsets[i]);
                }
            }
        }

        private GameObject CreateDots(GameObject list, IReadOnlyList<V6SongEntry> songs, int count)
        {
            var dots = V5Ui.Node(list, "Dot indicator");
            float total = V6SelectViewLogic.DotBottom(count, 0f);
            // Issue #427: Select と同じくレールをリストパネルの上下中央に置く。
            V6Layout.Place(dots.GetComponent<RectTransform>(), V6SelectViewLogic.DotX, V6SelectViewLogic.DotRailTopY(count),
                V6SelectViewLogic.DotActiveSize, total);

            float[] offsets = V6SelectViewLogic.DotOffsets(count, 0f);
            for (int i = 0; i < count; i++)
            {
                float d = V6SelectViewLogic.DotSize;
                var dot = V5Ui.Panel(dots, "Dot " + (i + 1), new Vector2(d, d),
                    V5Tokens.FromHex("#FFFFFF26"), V6SelectViewLogic.DotRadius);
                V6Layout.Place(dot, V6SelectViewLogic.DotRectX(d), offsets[i]);
                dotImages.Add(dot.GetComponent<Image>());
                dotRects.Add(dot.GetComponent<RectTransform>());
            }

            return dots;
        }

        private void BuildScrollButton(GameObject list, string name, float y, string iconSprite, int delta)
        {
            // design: Scroll Up/Down は 140×44 $k4 r8 + icon 24×24 ($text-muted)
            GameObject btn = V6Controls.IconButton(list, iconSprite, new Vector2(210f, y),
                new Vector2(140f, 44f), V5Tokens.Key4, V6Tokens.RadiusMedium, 24f, V5Tokens.TextMuted);
            V6Button.Configure(btn, () => ScrollList(delta));
        }

        // --- 中列: SCORE RANKING (512x732 $k2) ---

        /// <summary>
        /// Issue #415: SCORE RANKING は骨格を保持する。曲の有無 / Online セグメントの
        /// 出し分けが変わったときだけパネルを作り直す (骨格変更扱い)。
        /// それ以外はジャケット・セグメント色・ランキング行 (既存 RankRow) を差分更新する。
        /// </summary>
        private void EnsureScoreRanking(GameObject mainZone, V6SongEntry song)
        {
            bool hasSong = song != null;
            bool onlineAvailable = ShowOnlineSegmentForSelected();
            GameObject panel = scorePanelGo != null ? scorePanelGo
                : mainZone.transform.Find("Score Ranking")?.gameObject;
            if (panel == null)
            {
                CreateScoreRankingScaffold(mainZone, song);
                ApplyScoreRanking(mainZone, song);
                return;
            }

            if (scoreHasSong != hasSong || scoreOnlineShown != onlineAvailable)
            {
                V6Hierarchy.DetachAndDestroy(panel);
                scorePanelGo = null;
                songChipThumbGo = null;
                songChipText = null;
                segmentObjects.Clear();
                noDataText = null;
                retryGo = null;
                rankRowObjects.Clear();
                CreateScoreRankingScaffold(mainZone, song);
            }

            ApplyScoreRanking(mainZone, song);
        }

        private GameObject CreateScoreRankingScaffold(GameObject mainZone, V6SongEntry song)
        {
            rankRowObjects.Clear();
            segmentObjects.Clear();
            songChipThumbGo = null;
            songChipText = null;
            bool hasSong = song != null;
            scoreHasSong = hasSong;
            bool onlineAvailable = ShowOnlineSegmentForSelected();
            scoreOnlineShown = onlineAvailable;

            // design (qICtu 実測): SCORE RANKING は Main 内 (584,24) 512×732 $k2 r8。
            var panel = V6Layout.Place(V5Ui.Panel(mainZone, "Score Ranking", new Vector2(RankingW, RankingH),
                V5Tokens.Key2, V6Tokens.RadiusMedium), RankingX, RankingY);
            AddScrollCatcher(panel, 1);
            scorePanelGo = panel;

            // design (H8yC4 実測): Head (8,8) 496×40。子は自動レイアウトに任せず
            // design の絶対座標で置く (自動幅だと曲名チップが Segment に重なり、
            // Online が画面外へ切れていた)。
            var head = V5Ui.Node(panel.gameObject, "Head");
            V6Layout.Place(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 40f);

            // Title "SCORE RANKING" (12,7) 139×26 fs18 $text-dim。
            // design は 1 行。V5GameUi.Text の既定は折り返し有効で、この書体だと
            // "SCORE RANKING" が 139px に収まらず "SCORE" / "RANKING" の 2 行に
            // 割れていた (r21 実機) ため NoWrap を明示する。
            var headTitle = V5GameUi.Text(head.gameObject, "Label", "SCORE RANKING", 18f, V5Tokens.TextDim);
            headTitle.textWrappingMode = TextWrappingModes.NoWrap;
            V6Layout.Place(headTitle, 12f, 7f, 139f, 26f);

            if (song != null)
            {
                // design (wIE4t): Song チップ (159,4) 159×32 =
                // Thumb (0,4) 40×24 r4 + Text (48,0) 111×32 fs22 1 行固定 ellipsis。
                // 曲バッジ (タイトルのみ) は共通部品 (Issue #385 Phase 1.2)。
                var songChip = V5Ui.Node(head.gameObject, "Song");
                V6Layout.Place(songChip.GetComponent<RectTransform>(), 159f, 4f, 159f, 32f);
                var chipBadge = V6SongBadge.Create(songChip, new V6SongBadge.Spec
                {
                    ThumbPos = new Vector2(0f, 4f),
                    ThumbSize = new Vector2(40f, 24f),
                    ThumbColor = V5Tokens.Key3,
                    Title = new V6SongBadge.Line
                    {
                        Name = "Text", Pos = new Vector2(48f, 0f), Size = new Vector2(111f, 32f),
                        FontSize = 22f, Color = V5Tokens.Text, Text = song.Title,
                        FitLineHeight = true,
                    },
                });
                songChipThumbGo = chipBadge.Thumb;
                songChipText = chipBadge.Title;
            }

            // Online セグメントは cloud 曲だけ (spec 167: ローカル曲では非表示)。
            // 非表示の間は表示を Local に寄せる (見えないセグメントへの切替で
            // 本体が空になることを防ぐ)。

            // design (NEbbi): Segment (335,0) 156×40 $k0 r8。子は Local (4,4) /
            // Online (80,4) を 72×32 r4 で絶対配置 (Song が無くても位置は固定)。
            var segment = V6Layout.Place(V5Ui.Panel(head.gameObject, "Segment", new Vector2(156f, 40f), V5Tokens.Key0, 8f),
                335f, 0f);

            segmentObjects.Add(BuildSegment(segment, "Local", V6RankingLogic.RankingSource.Local, 4f));
            if (onlineAvailable)
            {
                segmentObjects.Add(BuildSegment(segment, "Online", V6RankingLogic.RankingSource.Online, 80f));
            }

            // 空状態テキストと再試行ボタンは常に作っておき、有無だけ setActive で切り替える。
            noDataText = V5GameUi.Text(panel.gameObject, "No Data", "", 24f, V5Tokens.TextDim,
                TextAlignmentOptions.Left);
            V6Layout.Place(noDataText, 24f, 60f, 496f, 44f, new Vector2(0f, 0.5f));

            var retry = V6Button.Make(V5Ui.Panel(panel, "Retry", new Vector2(160f, 48f), V5Tokens.Key4, 8f),
                RetryFetch);
            V6Layout.Place(retry, 176f, 140f);
            var retryRow = V5GameUi.Row(retry, "Inner", 8f, TextAnchor.MiddleCenter);
            V6Layout.StretchCenter(retryRow.GetComponent<RectTransform>(), 160f, 48f);
            V6Icons.Create(retryRow.gameObject, "skip-back", 22f, V5Tokens.TextMuted, "Icon");
            V5GameUi.Text(retryRow.gameObject, "Label", "再試行", 24f, V5Tokens.Text, TextAlignmentOptions.Center);
            retryGo = retry;
            return panel;
        }

        /// <summary>SCORE RANKING の変動値 (ジャケット/セグメント色/行/空状態/再試行) を適用する。</summary>
        private void ApplyScoreRanking(GameObject mainZone, V6SongEntry song)
        {
            GameObject panel = scorePanelGo != null ? scorePanelGo
                : mainZone.transform.Find("Score Ranking")?.gameObject;
            if (panel == null)
            {
                return;
            }

            if (songChipText != null) songChipText.text = song != null ? song.Title : "";
            if (songChipThumbGo != null && song != null) SetJacket(songChipThumbGo, song);

            // セグメントの有効色を実効ソースに合わせて塗り直す。
            V6RankingLogic.RankingSource effectiveSource = EffectiveSource();
            UpdateSegmentChip(segmentObjects.Count > 0 ? segmentObjects[0] : null,
                effectiveSource == V6RankingLogic.RankingSource.Local);
            UpdateSegmentChip(segmentObjects.Count > 1 ? segmentObjects[1] : null,
                effectiveSource == V6RankingLogic.RankingSource.Online);

            IReadOnlyList<V6RankingRow> rows = RankingRows(effectiveSource);
            bool hasRows = rows.Count > 0;
            if (noDataText != null)
            {
                if (hasRows)
                {
                    noDataText.gameObject.SetActive(false);
                }
                else
                {
                    // 未選択のときは「記録がありません」ではなく未選択であることを出す
                    // (Issue #239)。空データと未選択を同じ文言にしない。
                    noDataText.text = song == null
                        ? V6RankingLogic.NoSongSelectedMessage()
                        : V6RankingLogic.EmptyMessage(effectiveSource, data.IsLoading, data.HasError, data.StatusMessage);
                    noDataText.gameObject.SetActive(true);
                }
            }

            // 取得失敗時は再試行ボタン (Online のみ。Local は通信しない)。
            if (retryGo != null)
            {
                retryGo.SetActive(!hasRows
                    && effectiveSource == V6RankingLogic.RankingSource.Online
                    && data.HasError && !data.IsLoading);
            }

            int first = rankingScroll;
            for (int slot = 0; slot < RankingVisibleRows; slot++)
            {
                int i = first + slot;
                bool valid = hasRows && i < rows.Count;
                GameObject row = slot < rankRowObjects.Count ? rankRowObjects[slot] : null;
                float y = V6RankingLogic.RankRowTop(slot);
                if (!valid)
                {
                    if (row != null)
                    {
                        V6Hierarchy.DetachAndDestroy(row);
                        rankRowObjects[slot] = null;
                    }

                    continue;
                }

                if (row == null)
                {
                    row = CreateRankingRow(panel, rows[i], slot);
                    if (slot < rankRowObjects.Count) rankRowObjects[slot] = row;
                    else rankRowObjects.Add(row);
                }
                else
                {
                    UpdateRankingRow(row, rows[i], y);
                }
            }

            while (rankRowObjects.Count > RankingVisibleRows)
            {
                int last = rankRowObjects.Count - 1;
                V6Hierarchy.DetachAndDestroy(rankRowObjects[last]);
                rankRowObjects.RemoveAt(last);
            }
        }

        private static void UpdateSegmentChip(GameObject chip, bool active)
        {
            if (chip == null)
            {
                return;
            }

            var bg = chip.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = active ? V5Tokens.Key4 : V5Tokens.Key1;
            }

            Transform text = chip.transform.Find("Text");
            var tmp = text != null ? text.GetComponent<TextMeshProUGUI>() : null;
            if (tmp != null)
            {
                tmp.color = active ? V5Tokens.Text : V5Tokens.TextMuted;
            }
        }

        private GameObject CreateRankingRow(GameObject panel, V6RankingRow row, int visibleIndex)
        {
            return V6Lists.RankRow(
                panel,
                row.Position.ToString(),
                row.IsYou ? "YOU" : row.PlayerName,
                V6SelectViewLogic.FormatScore(row.Score),
                string.IsNullOrEmpty(row.Grade) ? "–" : row.Grade,
                new Vector2(V6RankingLogic.RankRowX, V6RankingLogic.RankRowTop(visibleIndex)),
                new Vector2(V6RankingLogic.RankRowW, V6RankingLogic.RankRowH),
                row.IsYou);
        }

        /// <summary>既存のランキング行を新しい行データで書き換える (行オブジェクトは再利用)。</summary>
        private static void UpdateRankingRow(GameObject row, V6RankingRow dataRow, float y)
        {
            var rt = row.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(V6RankingLogic.RankRowX, -y);
            }

            var bg = row.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = dataRow.IsYou ? V5Tokens.Key6 : V5Tokens.Key1;
            }

            V6Hierarchy.SetChildText(row, "Pos/Pos", dataRow.Position.ToString());
            V6Hierarchy.SetChildText(row, "Name", dataRow.IsYou ? "YOU" : dataRow.PlayerName);
            V6Hierarchy.SetChildText(row, "Value", V6SelectViewLogic.FormatScore(dataRow.Score));
            V6Hierarchy.SetChildText(row, "Grade/Grade", string.IsNullOrEmpty(dataRow.Grade) ? "–" : dataRow.Grade);

            Transform posText = row.transform.Find("Pos/Pos");
            var posTmp = posText != null ? posText.GetComponent<TextMeshProUGUI>() : null;
            if (posTmp != null)
            {
                posTmp.color = dataRow.IsYou ? V5Tokens.Text : V5Tokens.TextMuted;
            }
        }

        private GameObject BuildSegment(GameObject segment, string label, V6RankingLogic.RankingSource target, float x)
        {
            // 有効判定は実効ソース基準 (Online 非対象の曲は Local 表示のため)。
            bool active = EffectiveSource() == target;
            // design (NEbbi): Local (4,4) / Online (80,4) を 72×32 r4 で絶対配置。
            // Online が無い曲でも Local の位置は (4,4) のまま (自動レイアウトで詰めない)。
            var chip = V6Button.Make(V5Ui.Panel(segment, "Segment " + label, new Vector2(72f, 32f),
                active ? V5Tokens.Key4 : V5Tokens.Key1, 4f), ToggleSource);
            V6Layout.Place(chip, x, 4f);
            // design (S1TZD Segment): text fs20
            var text = V5GameUi.Text(chip, "Text", V6RankingLogic.SourceLabel(target), 20f,
                active ? V5Tokens.Text : V5Tokens.TextMuted, TextAlignmentOptions.Center);
            V6Layout.StretchCenter(text.GetComponent<RectTransform>(), 72f, 32f);
            return chip;
        }

        /// <summary>選択中の曲での実効ソース (Online セグメント非表示の曲は Local)。</summary>
        private V6RankingLogic.RankingSource EffectiveSource()
        {
            return ShowOnlineSegmentForSelected()
                ? source
                : V6RankingLogic.RankingSource.Local;
        }

        /// <summary>選択中の曲で Online セグメントを出すか。</summary>
        private bool ShowOnlineSegmentForSelected()
        {
            return V6RankingLogic.ShowOnlineSegment(data.Selected);
        }

        /// <summary>表示するランキング行 (Local = 実保存の自分のベスト / Online = 実 API)。</summary>
        private IReadOnlyList<V6RankingRow> RankingRows(V6RankingLogic.RankingSource rankingSource)
        {
            V6SongEntry song = data.Selected;
            if (rankingSource == V6RankingLogic.RankingSource.Local)
            {
                return V6RankingLogic.LocalRows(song);
            }
            return data.RankingFor(song);
        }

        // --- 右列: PLAYERS (512x732 $k2) ---

        /// <summary>
        /// PLAYERS パネル。design (smhrU 実測): $k2 r8 / ヘッド g2307 (8,8) 496×40 =
        /// Title "PLAYERS" (12,7) 74×26 fs18 $text-dim + 右寄せの Metric「累計スコア」
        /// (354,7) 90×26 fs18 $text。プレイヤー別 (曲横断) の集計 API はこのリポジトリに
        /// 無いため本文は Select と同じ正直な空状態「データがありません」。
        /// design の左右矢印 (qPD75/BUt9A) は操作先が無い死んだボタンなので描かない
        /// (Issue #239 の判断を維持)。
        /// </summary>
        /// <summary>
        /// Issue #415: PLAYERS パネルは状態で変わらないので、中身が無いときだけ作る。
        /// </summary>
        private void EnsurePlayers(GameObject mainZone)
        {
            GameObject panel = mainZone.transform.Find("Players")?.gameObject;
            if (panel != null)
            {
                return;
            }

            BuildPlayers(mainZone);
        }

        private void BuildPlayers(GameObject mainZone)
        {
            var panel = V6Layout.Place(V5Ui.Panel(mainZone, "Players", new Vector2(PlayersW, RankingH),
                V5Tokens.Key2, V6Tokens.RadiusMedium), PlayersX, 24f);

            var head = V5Ui.Node(panel.gameObject, "Head");
            V6Layout.Place(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 40f);

            // design の見出しは 1 行固定 (折り返すと "PLAYER" / "S" に割れる — r21 実機)。
            var headTitle = V5GameUi.Text(head, "Title", "PLAYERS", 18f, V5Tokens.TextDim);
            headTitle.textWrappingMode = TextWrappingModes.NoWrap;
            V6Layout.Place(headTitle, 12f, 7f, 74f, 26f);

            var metric = V5GameUi.Text(head, "Metric", "累計スコア", 18f, V5Tokens.Text,
                TextAlignmentOptions.Right, FontStyles.Normal);
            metric.textWrappingMode = TextWrappingModes.NoWrap;
            V6Layout.Place(metric, 354f, 7f, 90f, 26f);

            var empty = V5GameUi.Text(panel.gameObject, "NoData", "データがありません",
                24f, V5Tokens.TextDim, TextAlignmentOptions.Center);
            V6Layout.Place(empty, 8f, 300f, 496f, 44f, new Vector2(0f, 0.5f));
        }

        // --- 入力ヘルパ (マウスのみ。キーは統合担当が結線する) ---

        private void ScrollList(int delta)
        {
            listScroll = V6SelectViewLogic.ClampScroll(listScroll + delta, data.AllSongs.Count, ListVisibleRows);
            ApplyVolatile(false);
        }

        internal void OnScroll(int target, float delta)
        {
            int step = delta > 0f ? -1 : 1;
            if (target == 0)
            {
                ScrollList(step);
                return;
            }
            rankingScroll = V6SelectViewLogic.ClampScroll(rankingScroll + step, RankingRows(EffectiveSource()).Count, RankingVisibleRows);
            ApplyVolatile(false);
        }

        // --- ジャケット ---

        private void SetJacket(GameObject thumbPanel, V6SongEntry song, Color? fallback = null)
        {
            // Issue #398 / #385: ジャケットの Sprite は V6JacketProvider が所有し、
            // 適用手順は V6SongBadge.ApplyJacket に一本化 (History と同じ画像を
            // ここで別実体にしない)。
            V6SongBadge.ApplyJacket(thumbPanel,
                song != null ? song.SongId : 0, song != null ? song.Title : null, fallback);
        }

        private void AddScrollCatcher(GameObject target, int targetId)
        {
            var catcher = target.AddComponent<V6RankingScrollCatcher>();
            catcher.View = this;
            catcher.Target = targetId;
        }
    }

    /// <summary>解像度変化の追従だけを行うホスト (キー入力は読まない)。</summary>
    internal sealed class V6RankingViewDriver : MonoBehaviour
    {
        internal V6RankingView View;

        private void Update()
        {
            if (View != null)
            {
                View.UpdatePlacement();
            }
        }
    }

    /// <summary>ホイールスクロールの受け皿。</summary>
    internal sealed class V6RankingScrollCatcher : MonoBehaviour, IScrollHandler
    {
        internal V6RankingView View;
        internal int Target;

        public void OnScroll(PointerEventData eventData)
        {
            if (View != null)
            {
                View.OnScroll(Target, eventData.scrollDelta.y);
            }
        }
    }
}
