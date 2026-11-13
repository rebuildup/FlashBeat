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

        /// <summary>実データから表示を更新する (統合担当 / data.Changed から呼ぶ)。</summary>
        public void Refresh()
        {
            Rebuild(true);
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
            Rebuild(false);
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

        // --- 内部 ---

        private void OnDataChanged()
        {
            if (rootGo == null || !rootGo.activeInHierarchy)
            {
                return;
            }
            Rebuild(true);
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

        private void Rebuild(bool revealSelection)
        {
            if (rootGo == null)
            {
                return;
            }

            UpdatePlacement();

            // 共通フレーム (V6Frame) の stroke / モーダル層は残し、
            // Header / Main / Footer の各ゾーンの中身を作り直す。
            ClearChildren(Zone("Header"));
            ClearChildren(Zone("Footer"));
            ClearChildren(Zone("Main"));

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
            BuildHeader(headerZone, data.Selected);
            BuildFooter(footerZone);
            BuildSongList(mainZone, songs, selectIndex);
            BuildScoreRanking(mainZone, data.Selected);
            BuildPlayers(mainZone);
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

        private static void ClearChildren(GameObject go)
        {
            Common.V6Hierarchy.ClearChildren(go);
        }

        // --- ヘッダー (共通フレームの Header ゾーン: design (24,0) 1680×96) ---

        private void BuildHeader(GameObject headerZone, V6SongEntry song)
        {
            // 戻るボタンは共通部品 (Issue #370)。design S1TZD: header (0,20) 56×56。
            // 確定キーはキー設定の正本から取る。
            V6Controls.BackButton(headerZone, "Back Button", new Vector2(0f, 20f),
                ActionKeyHint.Primary(KeyActions.KeyAction.Back), () => TriggerBack());

            var title = V5GameUi.Text(headerZone, "Title", "RANKING", 38f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            At(title, 72f, 20f, 400f, 56f);

            // 右上: 曲情報 (ジャケット + 曲名/作家 + LEVEL / BPM / NOTES)。
            // design (S1TZD 情報ゾーン w4siL): header 内 x840 / w840 / padding [0,24] /
            // gap16 の横並び。Thumb 64×36 → Song 320×76 → Divider 1×44 →
            // Stat 100×62 ×3 がこの順で並ぶ (x は gap16 を挟んだ実測値)。
            bool hasSong = song != null;
            var thumb = At(V5Ui.Panel(headerZone, "Thumb", new Vector2(64f, 36f), V5Tokens.Key3, 4f),
                864f, 30f);
            if (hasSong)
            {
                SetJacket(thumb, song);
            }

            var songZone = V5Ui.Node(headerZone, "Song");
            At(songZone.GetComponent<RectTransform>(), 944f, 10f, 320f, 76f);
            // design w4siL/Song: Title (0,1) 320×43 fs30 / Artist (0,46) 320×29 fs20。
            // 旧実装は Title に Rect を与えておらず、既定の 100×100 中央寄せで
            // design より右下へずれていた (1 行固定 + ellipsis で列内に収める)。
            var songTitle = V5GameUi.Text(songZone, "Title", hasSong ? song.Title : "–", 30f, V5Tokens.Text,
                TextAlignmentOptions.Left);
            At(songTitle, 0f, 1f, 320f, 43f);
            songTitle.textWrappingMode = TextWrappingModes.NoWrap;
            songTitle.overflowMode = TextOverflowModes.Ellipsis;
            // design h43 < 行送り (fs30 × 1.448 = 43.44) のため、Ellipsis が 0 行に
            // なって曲名が消える (r3 実機で発生)。margin 補正で実効高を確保する。
            V5Ui.FitEllipsisToLineHeight(songTitle);
            var songArtist = V5GameUi.Text(songZone, "Artist", hasSong ? song.Artist : "", 20f,
                V5Tokens.TextMuted, TextAlignmentOptions.Left);
            At(songArtist, 0f, 46f, 320f, 29f);
            songArtist.textWrappingMode = TextWrappingModes.NoWrap;
            songArtist.overflowMode = TextOverflowModes.Ellipsis;
            V5Ui.FitEllipsisToLineHeight(songArtist);

            At(V5Ui.Panel(headerZone, "Divider", new Vector2(1f, 44f), FromHex("#FFFFFF26"), 0f), 1280f, 26f);

            BuildHeaderStat(headerZone, 1297f, "LEVEL", hasSong && song.Level > 0 ? song.Level.ToString() : "–");
            BuildHeaderStat(headerZone, 1413f, "BPM", hasSong && song.Bpm > 0
                ? Mathf.RoundToInt((float)song.Bpm).ToString() : "–");
            BuildHeaderStat(headerZone, 1529f, "NOTES", hasSong && song.NoteCount > 0 ? song.NoteCount.ToString() : "–");
        }

        /// <summary>ヘッダー右の統計 (100x62: label 上 / value 下)。</summary>
        private void BuildHeaderStat(GameObject header, float x, string label, string value)
        {
            var cell = V5Ui.Node(header, "Stat " + label);
            At(cell.GetComponent<RectTransform>(), x, 17f, 100f, 62f);
            var col = V5GameUi.Column(cell, "Inner", 2f, TextAnchor.MiddleLeft);
            // 子の幅は TMP の preferredWidth に従わせる。childControlWidth=false のままだと
            // 既定の矩形幅 (200) のまま 100 幅のセルから右へはみ出す。
            col.childControlWidth = true;
            Stretch(col.GetComponent<RectTransform>(), 100f, 62f);
            V5GameUi.Text(col.gameObject, "Label", label, 16f, V5Tokens.TextDim, TextAlignmentOptions.Left);
            V5GameUi.Text(col.gameObject, "Value", value, 24f, V5Tokens.Text, TextAlignmentOptions.Left,
                FontStyles.Bold);
        }

        // --- フッター (共通フレームの Footer ゾーン: design (24,876) 1680×96) ---

        private void BuildFooter(GameObject footerZone)
        {
            // design (S1TZD フッター): キー凡例は 4 項目 (↑↓ 曲送り / Space 決定 /
            // Tab 切替 / Esc 戻る)。項目間 gap24 / KeyCap padding [0,8] / Cap-Label gap8。
            // 切替は Ranking 画面では Tab (SelectSceneManager.HandleRankingInput が
            // Tab と S の両方を受ける)。F5 再試行は design に凡例が無いので出さない。
            BuildKeyLegend(footerZone, new[]
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
            ConfigureButton(gotoBtn, TriggerGotoSong);
        }

        /// <summary>
        /// フッター左下のキー凡例。design (S1TZD): KeyCap (h32 / pad[0,8] / fs22) +
        /// gap8 + Label fs22 $text-muted、項目間 gap24、zone 内 y=30 から
        /// (<see cref="V6Legend"/> が幅を TMP 実測で詰める)。
        /// </summary>
        private static void BuildKeyLegend(GameObject footer, string[][] items)
        {
            float x = 0f;
            for (int i = 0; i < items.Length; i++)
            {
                x = V6Legend.Item(footer, x, V6Legend.Y, items[i][0], items[i][1]);
            }
        }

        /// <summary>V6Controls が作った Button を操作可能にする (遷移なし・onClick 接続)。</summary>
        private static void ConfigureButton(GameObject go, Action onClick)
        {
            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }
        }

        // --- 左列: 曲リスト (x0 w560 #131313CC r16) ---

        private void BuildSongList(GameObject mainZone, IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            var list = At(V5Ui.Panel(mainZone, "Song List", new Vector2(ListW, ListH),
                FromHex("#131313CC"), 16f), 0f, 0f);
            AddScrollCatcher(list, 0);

            var rows = V5Ui.Node(list, "Rows");
            At(rows.GetComponent<RectTransform>(), 0f, 0f, ListW, ListH);

            if (songs.Count == 0)
            {
                var empty = V5GameUi.Text(rows.gameObject, "No Songs", "曲がありません", 24f, V5Tokens.TextDim,
                    TextAlignmentOptions.Center);
                Stretch(empty.GetComponent<RectTransform>(), ListW, ListH);
            }
            else
            {
                int first = listScroll;
                int last = Mathf.Min(songs.Count, first + ListVisibleRows);
                for (int i = first; i < last; i++)
                {
                    BuildSongRow(rows, songs[i], i == selectIndex, RowY0 + RowStep * (i - first));
                }
            }

            BuildScrollButton(list, "Scroll Up", 0f, "chevron-up", -1);
            BuildScrollButton(list, "Scroll Down", 732f, "chevron-down", 1);
            BuildDots(list, songs, selectIndex);
        }

        private void BuildSongRow(GameObject parent, V6SongEntry song, bool selected, float y)
        {
            // 行の内部座標は共通部品 V6Lists.SongRow に一本化 (design mcCOI 実測値。
            // この画面は Column レイアウトで曲名を折り返していたが、design は 1 行固定)。
            var row = V6Lists.SongRow(parent, song.Title, song.Artist,
                song.Level > 0 ? song.Level.ToString() : "–", song.BestGrade,
                new Vector2(RowX, y), new Vector2(RowW, RowH), selected);
            var button = MakeViewButton(row, null).GetComponent<Button>();
            int songId = song.SongId;
            button.onClick.AddListener(() => data.Select(songId));

            SetJacket(V6Lists.Thumb(row), song, V6Tokens.Key2);
        }

        private void BuildScrollButton(GameObject list, string name, float y, string iconSprite, int delta)
        {
            // design: Scroll Up/Down は 140×44 $k4 r8 + icon 24×24 ($text-muted)
            GameObject btn = V6Controls.IconButton(list, iconSprite, new Vector2(210f, y),
                new Vector2(140f, 44f), V5Tokens.Key4, V6Tokens.RadiusMedium, 24f, V5Tokens.TextMuted);
            ConfigureButton(btn, () => ScrollList(delta));
        }

        /// <summary>
        /// ドットインジケータ (design vQc4K (536,136) 幅10 縦 gap4 / ドット8×8
        /// #FFFFFF26・選択 10×10 $accent・ピッチ 12)。配置は Select と同じ
        /// <see cref="V6SelectViewLogic.DotOffsets"/> / <see cref="V6SelectViewLogic.DotRectX"/>
        /// を使う (y を親ローカルで積まないと全ドットが 1 点に重なる)。
        /// </summary>
        private void BuildDots(GameObject list, IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            var dots = V5Ui.Node(list, "Dot indicator");
            int count = songs.Count;
            float total = V6SelectViewLogic.DotBottom(count, 0f);
            // Issue #427: Select と同じくレールをリストパネルの上下中央に置く。
            At(dots.GetComponent<RectTransform>(), V6SelectViewLogic.DotX, V6SelectViewLogic.DotRailTopY(count),
                V6SelectViewLogic.DotActiveSize, total);

            int active = V6SelectViewLogic.DotActiveIndex(selectIndex, count);
            float[] offsets = V6SelectViewLogic.DotOffsets(count, 0f);
            for (int i = 0; i < count; i++)
            {
                float d = i == active ? V6SelectViewLogic.DotActiveSize : V6SelectViewLogic.DotSize;
                // Issue #427: 選択中は v6 の選択色 (Key6)。
                var dot = V5Ui.Panel(dots, "Dot " + (i + 1), new Vector2(d, d),
                    i == active ? V6Tokens.Key6 : FromHex("#FFFFFF26"), V6SelectViewLogic.DotRadius);
                At(dot, V6SelectViewLogic.DotRectX(d), offsets[i]);
            }
        }

        // --- 中列: SCORE RANKING (512x732 $k2) ---

        private void BuildScoreRanking(GameObject mainZone, V6SongEntry song)
        {
            // design (qICtu 実測): SCORE RANKING は Main 内 (584,24) 512×732 $k2 r8。
            var panel = At(V5Ui.Panel(mainZone, "Score Ranking", new Vector2(RankingW, RankingH),
                V5Tokens.Key2, V6Tokens.RadiusMedium), RankingX, RankingY);
            AddScrollCatcher(panel, 1);

            // design (H8yC4 実測): Head (8,8) 496×40。子は自動レイアウトに任せず
            // design の絶対座標で置く (自動幅だと曲名チップが Segment に重なり、
            // Online が画面外へ切れていた)。
            var head = V5Ui.Node(panel.gameObject, "Head");
            At(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 40f);

            // Title "SCORE RANKING" (12,7) 139×26 fs18 $text-dim。
            // design は 1 行。V5GameUi.Text の既定は折り返し有効で、この書体だと
            // "SCORE RANKING" が 139px に収まらず "SCORE" / "RANKING" の 2 行に
            // 割れていた (r21 実機) ため NoWrap を明示する。
            var headTitle = V5GameUi.Text(head.gameObject, "Label", "SCORE RANKING", 18f, V5Tokens.TextDim);
            headTitle.textWrappingMode = TextWrappingModes.NoWrap;
            At(headTitle, 12f, 7f, 139f, 26f);

            if (song != null)
            {
                // design (wIE4t): Song チップ (159,4) 159×32 =
                // Thumb (0,4) 40×24 r4 + Text (48,0) 111×32 fs22 1 行固定 ellipsis。
                var songChip = V5Ui.Node(head.gameObject, "Song");
                At(songChip.GetComponent<RectTransform>(), 159f, 4f, 159f, 32f);
                var thumb = At(V5Ui.Panel(songChip, "Thumb", new Vector2(40f, 24f), V5Tokens.Key3, 4f), 0f, 4f);
                SetJacket(thumb, song);
                var songText = V5GameUi.Text(songChip, "Text", song.Title, 22f, V5Tokens.Text);
                songText.textWrappingMode = TextWrappingModes.NoWrap;
                songText.overflowMode = TextOverflowModes.Ellipsis;
                At(songText, 48f, 0f, 111f, 32f);
                V5Ui.FitEllipsisToLineHeight(songText);
            }

            // Online セグメントは cloud 曲だけ (spec 167: ローカル曲では非表示)。
            // 非表示の間は表示を Local に寄せる (見えないセグメントへの切替で
            // 本体が空になることを防ぐ)。
            bool onlineAvailable = ShowOnlineSegmentForSelected();
            V6RankingLogic.RankingSource effectiveSource = EffectiveSource();

            // design (NEbbi): Segment (335,0) 156×40 $k0 r8。子は Local (4,4) /
            // Online (80,4) を 72×32 r4 で絶対配置 (Song が無くても位置は固定)。
            var segment = At(V5Ui.Panel(head.gameObject, "Segment", new Vector2(156f, 40f), V5Tokens.Key0, 8f),
                335f, 0f);

            BuildSegment(segment, "Local", V6RankingLogic.RankingSource.Local, 4f);
            if (onlineAvailable)
            {
                BuildSegment(segment, "Online", V6RankingLogic.RankingSource.Online, 80f);
            }

            IReadOnlyList<V6RankingRow> rows = RankingRows(effectiveSource);
            if (rows.Count == 0)
            {
                // 未選択のときは「記録がありません」ではなく未選択であることを出す
                // (Issue #239)。空データと未選択を同じ文言にしない。
                string message = song == null
                    ? V6RankingLogic.NoSongSelectedMessage()
                    : V6RankingLogic.EmptyMessage(effectiveSource, data.IsLoading, data.HasError, data.StatusMessage);
                var empty = V5GameUi.Text(panel.gameObject, "No Data", message, 24f, V5Tokens.TextDim,
                    TextAlignmentOptions.Left);
                At(empty, 24f, 60f, 496f, 44f, new Vector2(0f, 0.5f));

                // 取得失敗時は再試行ボタン (Online のみ。Local は通信しない)
                if (effectiveSource == V6RankingLogic.RankingSource.Online && data.HasError && !data.IsLoading)
                {
                    var retry = MakeButton(V5Ui.Panel(panel, "Retry", new Vector2(160f, 48f), V5Tokens.Key4, 8f),
                        RetryFetch);
                    At(retry, 176f, 140f);
                    var retryRow = V5GameUi.Row(retry, "Inner", 8f, TextAnchor.MiddleCenter);
                    Stretch(retryRow.GetComponent<RectTransform>(), 160f, 48f);
                    var retryIcon = V6Icons.Create(retryRow.gameObject, "skip-back", 22f, V5Tokens.TextMuted, "Icon");
                    V5GameUi.Text(retryRow.gameObject, "Label", "再試行", 24f, V5Tokens.Text, TextAlignmentOptions.Center);
                }
                return;
            }

            int first = rankingScroll;
            int last = Mathf.Min(rows.Count, first + RankingVisibleRows);
            for (int i = first; i < last; i++)
            {
                BuildRankingRow(panel, rows[i], i - first);
            }
        }

        private void BuildSegment(GameObject segment, string label, V6RankingLogic.RankingSource target, float x)
        {
            // 有効判定は実効ソース基準 (Online 非対象の曲は Local 表示のため)。
            bool active = EffectiveSource() == target;
            // design (NEbbi): Local (4,4) / Online (80,4) を 72×32 r4 で絶対配置。
            // Online が無い曲でも Local の位置は (4,4) のまま (自動レイアウトで詰めない)。
            var chip = MakeButton(V5Ui.Panel(segment, "Segment " + label, new Vector2(72f, 32f),
                active ? V5Tokens.Key4 : V5Tokens.Key1, 4f), ToggleSource);
            At(chip, x, 4f);
            // design (S1TZD Segment): text fs20
            var text = V5GameUi.Text(chip, "Text", V6RankingLogic.SourceLabel(target), 20f,
                active ? V5Tokens.Text : V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(text.GetComponent<RectTransform>(), 72f, 32f);
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

        /// <summary>
        /// ランキング行。行内訳 (Pos 36×36 / Name w160 / Value w180 右寄せ / Grade 56×36) は
        /// design 実測 (Hbxqf) = 共通部品 V6Lists.RankRow を使い回す。
        /// 行位置・寸法は design (S1TZD): Score·k = (8, 60+64k)、496×56。
        /// </summary>
        private void BuildRankingRow(GameObject panel, V6RankingRow row, int visibleIndex)
        {
            V6Lists.RankRow(
                panel,
                row.Position.ToString(),
                row.IsYou ? "YOU" : row.PlayerName,
                V6SelectViewLogic.FormatScore(row.Score),
                string.IsNullOrEmpty(row.Grade) ? "–" : row.Grade,
                new Vector2(V6RankingLogic.RankRowX, V6RankingLogic.RankRowTop(visibleIndex)),
                new Vector2(V6RankingLogic.RankRowW, V6RankingLogic.RankRowH),
                row.IsYou);
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
        private void BuildPlayers(GameObject mainZone)
        {
            var panel = At(V5Ui.Panel(mainZone, "Players", new Vector2(PlayersW, RankingH),
                V5Tokens.Key2, V6Tokens.RadiusMedium), PlayersX, 24f);

            var head = V5Ui.Node(panel.gameObject, "Head");
            At(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 40f);

            // design の見出しは 1 行固定 (折り返すと "PLAYER" / "S" に割れる — r21 実機)。
            var headTitle = V5GameUi.Text(head, "Title", "PLAYERS", 18f, V5Tokens.TextDim);
            headTitle.textWrappingMode = TextWrappingModes.NoWrap;
            At(headTitle, 12f, 7f, 74f, 26f);

            var metric = V5GameUi.Text(head, "Metric", "累計スコア", 18f, V5Tokens.Text,
                TextAlignmentOptions.Right, FontStyles.Normal);
            metric.textWrappingMode = TextWrappingModes.NoWrap;
            At(metric, 354f, 7f, 90f, 26f);

            var empty = V5GameUi.Text(panel.gameObject, "NoData", "データがありません",
                24f, V5Tokens.TextDim, TextAlignmentOptions.Center);
            At(empty, 8f, 300f, 496f, 44f, new Vector2(0f, 0.5f));
        }

        // --- 入力ヘルパ (マウスのみ。キーは統合担当が結線する) ---

        private void ScrollList(int delta)
        {
            listScroll = V6SelectViewLogic.ClampScroll(listScroll + delta, data.AllSongs.Count, ListVisibleRows);
            Rebuild(false);
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
            Rebuild(false);
        }

        // --- ジャケット ---

        private void SetJacket(GameObject thumbPanel, V6SongEntry song, Color? fallback = null)
        {
            var img = thumbPanel.GetComponent<Image>();
            // Issue #398: ジャケットの Sprite は V6JacketProvider が所有する。
            // History と同じ画像をここで別実体にしない (画面ごとに Sprite.Create すると
            // native が積み上がり、View を捨てても解放されない)。
            Sprite sprite = song == null
                ? null
                : V6JacketProvider.Local(song.SongId, song.Title);

            if (sprite != null)
            {
                img.type = Image.Type.Simple;
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                // ジャケット無しは空枠。曲行 (design mcCOI) は $k2、それ以外は $k3。
                img.type = Image.Type.Sliced;
                img.sprite = V5Ui.RoundedSprite(4);
                img.color = fallback ?? V5Tokens.Key3;
            }
        }

        // --- uGUI ユーティリティ (design 空間・左上原点。変換は V6Layout に集約) ---

        private static GameObject At(GameObject go, float x, float y)
        {
            return At(go.GetComponent<RectTransform>(), x, y);
        }

        private static GameObject At(GameObject go, float x, float y, float w, float h)
        {
            return At(go.GetComponent<RectTransform>(), x, y, w, h);
        }

        private static GameObject At(GameObject go, float x, float y, float w, float h, Vector2 pivot)
        {
            return At(go.GetComponent<RectTransform>(), x, y, w, h, pivot);
        }

        private static GameObject At(Component node, float x, float y)
        {
            var rt = (RectTransform)node.transform;
            V6Layout.Place(rt, new Vector2(x, y), rt.sizeDelta);
            return rt.gameObject;
        }

        private static GameObject At(Component node, float x, float y, float w, float h)
        {
            var rt = (RectTransform)node.transform;
            V6Layout.Place(rt, new Vector2(x, y), new Vector2(w, h));
            return rt.gameObject;
        }

        private static GameObject At(Component node, float x, float y, float w, float h, Vector2 pivot)
        {
            var rt = (RectTransform)node.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = pivot;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = V6Layout.ToLocal(new Vector2(
                x + pivot.x * w,
                y + (1f - pivot.y) * h));
            return rt.gameObject;
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

        /// <summary>Button を追加して操作可能にする (遷移なし)。</summary>
        private static GameObject MakeButton(GameObject go, Action onClick)
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

        private static GameObject MakeViewButton(GameObject go, Action onClick)
        {
            return MakeButton(go, onClick);
        }

        private void AddScrollCatcher(GameObject target, int targetId)
        {
            var catcher = target.AddComponent<V6RankingScrollCatcher>();
            catcher.View = this;
            catcher.Target = targetId;
        }

        private static Color FromHex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
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
