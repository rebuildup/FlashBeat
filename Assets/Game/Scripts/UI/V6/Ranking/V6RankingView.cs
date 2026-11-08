using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
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
    /// - PLAYERS はプレイヤー総合ランキングの API が既存に無いので空状態
    ///   (NO DATA) のみ。未実装は PR に記載する。
    /// - キー操作は統合担当が結線する (Input は直接読まない):
    ///   MoveSelection / ToggleSource / TriggerBack / TriggerOpenSettings / TriggerGotoSong。
    /// </summary>
    public sealed class V6RankingView
    {
        /// <summary>現在生成されている v6 ランキング画面 (ランキングシーンに 1 個だけ)。</summary>
        public static V6RankingView Current { get; private set; }

        // --- design px 定数 (S1TZD の実測値) ---
        private const float FrameW = 1728f;
        private const float FrameH = 972f;

        private const float MainX = 24f, MainY = 96f;
        private const float ListW = 560f, ListH = 780f;
        private const float RowX = 16f, RowY0 = 48f, RowH = 72f, RowStep = 76f;
        private const int ListVisibleRows = 9; // y48..y656 (+72 = 728 <= 780)
        private const float RankingX = 584f, RankingW = 512f, RankingH = 732f;
        private const int RankingVisibleRows = 10; // (732-60) / 64
        private const float PlayersX = 1144f, PlayersW = 512f;

        private readonly IV6SelectDataSource data;
        private readonly Action onBack;
        private readonly Action onOpenSettings;
        private readonly Action onGotoSong;

        private readonly Dictionary<string, Sprite> jacketCache = new Dictionary<string, Sprite>();

        private GameObject rootGo;
        private RectTransform frame;

        private float u = 1f;
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

        /// <summary>SCORE RANKING のセグメント (Local / Online) を切り替える。</summary>
        public void ToggleSource()
        {
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

            frame = V5Ui.Node(rootGo, "V6 Ranking Frame 1728x972").GetComponent<RectTransform>();
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(FrameW, FrameH);

            var driver = rootGo.AddComponent<V6RankingViewDriver>();
            driver.View = this;
            UpdateScale();
        }

        internal void UpdateScale()
        {
            int w = Screen.width;
            int h = Screen.height;
            if (w == lastWidth && h == lastHeight)
            {
                return;
            }

            lastWidth = w;
            lastHeight = h;
            u = V6SelectViewLogic.DesignScale(w, h);
            if (frame != null)
            {
                frame.localScale = new Vector3(u, u, 1f);
            }
        }

        private void Rebuild(bool revealSelection)
        {
            if (rootGo == null)
            {
                return;
            }

            UpdateScale();

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
            rankingScroll = V6SelectViewLogic.ClampScroll(rankingScroll, RankingRows().Count, RankingVisibleRows);

            BuildHeader(data.Selected);
            BuildFooter();
            BuildSongList(songs, selectIndex);
            BuildScoreRanking(data.Selected);
            BuildPlayers();
        }

        // --- ヘッダー (x24 y0 1680x96) ---

        private void BuildHeader(V6SongEntry song)
        {
            var header = At(V5Ui.Panel(frame.gameObject, "Header", new Vector2(1680f, 96f), Clear(), 0f), 24f, 0f);

            // 戻るボタン 56x56 (#3A3A3A r8) + Esc キャプション
            var backZone = At(V5Ui.Node(header, "Zone Back"), 0f, 20f);
            var back = MakeButton(V5Ui.Panel(backZone, "Back Button", new Vector2(56f, 56f), V5Tokens.Key4, 8f),
                () => TriggerBack());
            var backCol = V5GameUi.Column(back, "Inner", 2f, TextAnchor.MiddleCenter);
            Stretch(backCol.GetComponent<RectTransform>(), 56f, 56f);
            var backIcon = V6Icons.Create(backCol.gameObject, "chevron-left", 22f, V5Tokens.TextMuted, "Icon");
            V5GameUi.Text(backCol.gameObject, "Key", "Esc", 13f, V5Tokens.TextMuted, TextAlignmentOptions.Center);

            var title = V5GameUi.Text(header.gameObject, "Title", "RANKING", 38f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            At(title, 72f, 20f, 400f, 56f);

            // 右上: 曲情報 (ジャケット + 曲名/作家 + LEVEL / BPM / NOTES)。右詰め。
            bool hasSong = song != null;
            var thumb = At(V5Ui.Panel(header, "Thumb", new Vector2(64f, 36f), V5Tokens.Key3, 4f), 924f, 30f);
            if (hasSong)
            {
                SetJacket(thumb, song);
            }

            var songZone = V5Ui.Node(header, "Song");
            At(songZone.GetComponent<RectTransform>(), 1012f, 10f, 320f, 76f);
            var songTitle = V5GameUi.Text(songZone, "Title", hasSong ? song.Title : "–", 30f, V5Tokens.Text,
                TextAlignmentOptions.Left);
            songTitle.textWrappingMode = TextWrappingModes.NoWrap;
            var songArtist = V5GameUi.Text(songZone, "Artist", hasSong ? song.Artist : "", 22f, V5Tokens.TextMuted,
                TextAlignmentOptions.Left);
            songArtist.textWrappingMode = TextWrappingModes.NoWrap;
            At(songArtist, 0f, 40f, 320f, 32f);

            At(V5Ui.Panel(header, "Divider", new Vector2(1f, 44f), FromHex("#FFFFFF26"), 0f), 1356f, 26f);

            BuildHeaderStat(header, 1380f, "LEVEL", hasSong && song.Level > 0 ? song.Level.ToString() : "–");
            BuildHeaderStat(header, 1490f, "BPM", hasSong && song.Bpm > 0
                ? Mathf.RoundToInt((float)song.Bpm).ToString() : "–");
            BuildHeaderStat(header, 1600f, "NOTES", hasSong && song.NoteCount > 0 ? song.NoteCount.ToString() : "–");
        }

        /// <summary>ヘッダー右の統計 (100x62: label 上 / value 下)。</summary>
        private void BuildHeaderStat(GameObject header, float x, string label, string value)
        {
            var cell = V5Ui.Node(header, "Stat " + label);
            At(cell.GetComponent<RectTransform>(), x, 17f, 100f, 62f);
            var col = V5GameUi.Column(cell, "Inner", 2f, TextAnchor.MiddleLeft);
            Stretch(col.GetComponent<RectTransform>(), 100f, 62f);
            V5GameUi.Text(col.gameObject, "Label", label, 16f, V5Tokens.TextDim, TextAlignmentOptions.Left);
            V5GameUi.Text(col.gameObject, "Value", value, 24f, V5Tokens.Text, TextAlignmentOptions.Left,
                FontStyles.Bold);
        }

        // --- フッター (x24 y876 1680x96) ---

        private void BuildFooter()
        {
            var footer = At(V5Ui.Panel(frame.gameObject, "Footer", new Vector2(1680f, 96f), Clear(), 0f), 24f, 876f);

            var legend = V5GameUi.Row(footer.gameObject, "Legend", 24f, TextAnchor.MiddleLeft);
            At(legend.GetComponent<RectTransform>(), 0f, 30f, 680f, 36f);
            V6Ui.KeyCap(legend.gameObject, "↑↓", new Vector2(56f, 32f));
            V5GameUi.Text(legend.gameObject, "L1", "曲送り", 20f, V5Tokens.TextMuted);
            V6Ui.KeyCap(legend.gameObject, "Space", new Vector2(76f, 32f));
            V5GameUi.Text(legend.gameObject, "L2", "決定", 20f, V5Tokens.TextMuted);
            V6Ui.KeyCap(legend.gameObject, "Tab", new Vector2(56f, 32f));
            V5GameUi.Text(legend.gameObject, "L3", "切替", 20f, V5Tokens.TextMuted);
            V6Ui.KeyCap(legend.gameObject, "Esc", new Vector2(56f, 32f));
            V5GameUi.Text(legend.gameObject, "L4", "戻る", 20f, V5Tokens.TextMuted);

            // 右下: 設定 (secondary) + この曲へ (primary)。design 座標のまま右詰め。
            var settingsBtn = MakeButton(V5Ui.Panel(footer, "Button Settings", new Vector2(197f, 56f),
                V5Tokens.ControlBg, 8f), TriggerOpenSettings);
            At(settingsBtn, 1198f, 20f);
            var settingsRow = V5GameUi.Row(settingsBtn, "Inner", 10f, TextAnchor.MiddleCenter);
            Stretch(settingsRow.GetComponent<RectTransform>(), 197f, 56f);
            var settingsIcon = V6Icons.Create(settingsRow.gameObject, "settings", 24f, V5Tokens.TextMuted, "Icon");
            V5GameUi.Text(settingsRow.gameObject, "Label", "設定", 30f, V5Tokens.Text, TextAlignmentOptions.Center);

            var gotoBtn = MakeButton(V5Ui.Panel(footer, "Op この曲へ", new Vector2(273f, 56f),
                V5Tokens.ButtonPrimaryBg, 8f), TriggerGotoSong);
            At(gotoBtn, 1407f, 20f);
            var gotoRow = V5GameUi.Row(gotoBtn, "Inner", 10f, TextAnchor.MiddleCenter);
            Stretch(gotoRow.GetComponent<RectTransform>(), 273f, 56f);
            var gotoIcon = V6Icons.Create(gotoRow.gameObject, "music", 26f, V5Tokens.ButtonPrimaryLabel, "Icon");
            V5GameUi.Text(gotoRow.gameObject, "Label", "この曲へ", 30f, V5Tokens.ButtonPrimaryLabel,
                TextAlignmentOptions.Center);
            var cap = V5Ui.Panel(gotoRow.gameObject, "Cap", new Vector2(84f, 32f), new Color(0f, 0f, 0f, 0.15f), 4f);
            var capText = V5GameUi.Text(cap, "Key", "Space", 20f, V5Tokens.ButtonPrimaryLabel, TextAlignmentOptions.Center);
            Stretch(capText.GetComponent<RectTransform>(), 84f, 32f);
        }

        // --- 左列: 曲リスト (x0 w560 #131313CC r16) ---

        private void BuildSongList(IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            var list = At(V5Ui.Panel(frame.gameObject, "Song List", new Vector2(ListW, ListH),
                FromHex("#131313CC"), 16f), MainX + 0f, MainY);
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
            var row = At(V5Ui.Panel(parent, "Song " + song.Title, new Vector2(504f, RowH),
                selected ? V5Tokens.Key6 : V5Tokens.Recessed2, 8f), RowX, y);
            var button = MakeButton(row, null).GetComponent<Button>();
            int songId = song.SongId;
            button.onClick.AddListener(() => data.Select(songId));

            var levelBadge = At(V5Ui.Panel(row, "Level", new Vector2(44f, 44f), V5Tokens.Key4, 22f), 14f, 14f);
            var levelText = V5GameUi.Text(levelBadge.gameObject, "LevelText",
                song.Level > 0 ? song.Level.ToString() : "–", 22f, V5Tokens.Text, TextAlignmentOptions.Center);
            Stretch(levelText.GetComponent<RectTransform>(), 44f, 44f);

            var thumb = At(V5Ui.Panel(row, "Thumb", new Vector2(96f, 54f), V5Tokens.Key3, 4f), 72f, 9f);
            SetJacket(thumb, song);

            var texts = V5GameUi.Column(row, "Texts", 2f, TextAnchor.MiddleLeft);
            At(texts.GetComponent<RectTransform>(), 182f, 14f, 240f, 44f);
            V5GameUi.Text(texts.gameObject, "Title", song.Title, 26f, V5Tokens.Text, TextAlignmentOptions.Left);
            V5GameUi.Text(texts.gameObject, "Composer", song.Artist, 20f, V5Tokens.TextMuted);

            var grade = At(V5Ui.Panel(row, "Grade", new Vector2(56f, 36f), V5Tokens.Key4, 4f), 432f, 18f);
            var gradeText = V5GameUi.Text(grade.gameObject, "GradeText",
                string.IsNullOrEmpty(song.BestGrade) ? "–" : song.BestGrade, 22f, V5Tokens.Text,
                TextAlignmentOptions.Center);
            Stretch(gradeText.GetComponent<RectTransform>(), 56f, 36f);
        }

        private void BuildScrollButton(GameObject list, string name, float y, string iconSprite, int delta)
        {
            var btn = MakeButton(V5Ui.Panel(list, name, new Vector2(140f, 44f), V5Tokens.Key4, 8f),
                () => ScrollList(delta));
            At(btn, 210f, y);
            var icon = V6Icons.Create(btn, iconSprite, 24f, V5Tokens.TextMuted, "Icon");
            if (icon != null)
            {
                Stretch(icon.rectTransform, 140f, 44f);
            }
        }

        /// <summary>
        /// ドットインジケータ。全曲数の分だけ (x=536, y=136 から縦 gap 4、
        /// 選択中だけ 10px の $accent)。V6SelectView.BuildDots と同じ表現。
        /// </summary>
        private void BuildDots(GameObject list, IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            var dots = V5Ui.Node(list, "Dot indicator");
            int count = songs.Count;
            float gap = 4f;
            float size = 8f;
            float total = count > 0 ? count * (size + gap) - gap : 0f;
            At(dots.GetComponent<RectTransform>(), 536f, 136f, 24f, total);

            int active = V6SelectViewLogic.DotActiveIndex(selectIndex, count);
            for (int i = 0; i < count; i++)
            {
                float d = i == active ? size + 2f : size;
                var dot = At(V5Ui.Panel(dots, "Dot " + (i + 1), new Vector2(d, d),
                    i == active ? V5Tokens.Accent : FromHex("#FFFFFF26"), 5f), 0f, (size + gap) * i);
                Center(dot, 24f, d);
            }
        }

        // --- 中列: SCORE RANKING (512x732 $k2) ---

        private void BuildScoreRanking(V6SongEntry song)
        {
            var panel = At(V5Ui.Panel(frame.gameObject, "Score Ranking", new Vector2(RankingW, RankingH),
                V5Tokens.Key2, 16f), MainX + RankingX, MainY + 24f);
            AddScrollCatcher(panel, 1);

            var head = V5GameUi.Row(panel.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            At(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 40f);
            V5GameUi.Text(head.gameObject, "Label", "SCORE RANKING", 18f, V5Tokens.TextDim);

            if (song != null)
            {
                var songChip = V5GameUi.Row(head.gameObject, "Song", 6f, TextAnchor.MiddleCenter);
                var thumb = At(V5Ui.Panel(songChip.gameObject, "Thumb", new Vector2(40f, 24f), V5Tokens.Key3, 4f), 0f, 0f);
                SetJacket(thumb, song);
                var songText = V5GameUi.Text(songChip.gameObject, "Text", song.Title, 18f, V5Tokens.Text);
                songText.textWrappingMode = TextWrappingModes.NoWrap;
            }

            var headSpacer = V5Ui.Node(head.gameObject, "Spacer");
            var headSpacerLe = headSpacer.AddComponent<LayoutElement>();
            headSpacerLe.flexibleWidth = 1f;

            // Local / Online セグメント (design: 有効側が $k4 + $text、無効側が $k1 + $text-muted)。
            BuildSegment(head.gameObject, "Local", V6RankingLogic.RankingSource.Local);
            BuildSegment(head.gameObject, "Online", V6RankingLogic.RankingSource.Online);

            IReadOnlyList<V6RankingRow> rows = RankingRows();
            if (rows.Count == 0)
            {
                string message = V6RankingLogic.EmptyMessage(source, data.IsLoading, data.HasError, data.StatusMessage);
                var empty = V5GameUi.Text(panel.gameObject, "No Data", message, 24f, V5Tokens.TextDim,
                    TextAlignmentOptions.Left);
                At(empty, 24f, 60f, 496f, 44f, new Vector2(0f, 0.5f));

                // 取得失敗時は再試行ボタン (Online のみ。Local は通信しない)。
                if (source == V6RankingLogic.RankingSource.Online && data.HasError && !data.IsLoading)
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
                BuildRankingRow(panel, rows[i], i, 60f + 64f * (i - first));
            }
        }

        private void BuildSegment(GameObject head, string label, V6RankingLogic.RankingSource target)
        {
            bool active = source == target;
            var chip = MakeButton(V5Ui.Panel(head, "Segment " + label, new Vector2(72f, 32f),
                active ? V5Tokens.Key4 : V5Tokens.Key1, 4f), ToggleSource);
            var text = V5GameUi.Text(chip, "Text", V6RankingLogic.SourceLabel(target), 18f,
                active ? V5Tokens.Text : V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(text.GetComponent<RectTransform>(), 72f, 32f);
        }

        /// <summary>表示するランキング行 (Local = 実保存の自分のベスト / Online = 実 API)。</summary>
        private IReadOnlyList<V6RankingRow> RankingRows()
        {
            V6SongEntry song = data.Selected;
            if (source == V6RankingLogic.RankingSource.Local)
            {
                return V6RankingLogic.LocalRows(song);
            }
            return data.RankingFor(song);
        }

        private void BuildRankingRow(GameObject panel, V6RankingRow row, int index, float y)
        {
            var line = At(V5Ui.Panel(panel, "Score " + (index + 1), new Vector2(496f, 56f),
                row.IsYou ? V5Tokens.Key6 : V5Tokens.Recessed2, 8f), 8f, y);

            var pos = At(V5Ui.Panel(line, "Pos", new Vector2(36f, 36f), V5Tokens.Key4, 18f), 10f, 10f);
            var posText = V5GameUi.Text(pos, "PosText", row.Position.ToString(), 22f,
                row.IsYou ? V5Tokens.Text : V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(posText.GetComponent<RectTransform>(), 36f, 36f);

            var name = V5GameUi.Text(line.gameObject, "Name", row.IsYou ? "YOU" : row.PlayerName, 22f, V5Tokens.Text);
            At(name, 58f, 0f, 160f, 56f, new Vector2(0f, 0.5f));

            var score = V5GameUi.Text(line.gameObject, "Score", V6SelectViewLogic.FormatScore(row.Score), 22f,
                V5Tokens.Text, TextAlignmentOptions.Right);
            At(score, 230f, 0f, 180f, 56f, new Vector2(0f, 0.5f));

            var grade = At(V5Ui.Panel(line, "Grade", new Vector2(56f, 36f), V5Tokens.Key4, 4f), 426f, 10f);
            var gradeText = V5GameUi.Text(grade.gameObject, "GradeText",
                string.IsNullOrEmpty(row.Grade) ? "–" : row.Grade, 22f, V5Tokens.Text, TextAlignmentOptions.Center);
            Stretch(gradeText.GetComponent<RectTransform>(), 56f, 36f);
        }

        // --- 右列: PLAYERS (512x732 $k2) ---

        /// <summary>
        /// PLAYERS パネル。プレイヤー総合ランキング (累計スコア) を返す既存 API が
        /// 無いため、ヘッドと空状態のみ (未実装は PR に記載)。
        /// </summary>
        private void BuildPlayers()
        {
            var panel = At(V5Ui.Panel(frame.gameObject, "Players", new Vector2(PlayersW, RankingH),
                V5Tokens.Key2, 16f), MainX + PlayersX, MainY + 24f);

            var head = V5GameUi.Row(panel.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            At(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 40f);
            V5GameUi.Text(head.gameObject, "Label", "PLAYERS", 18f, V5Tokens.TextDim);
            var headSpacer = V5Ui.Node(head.gameObject, "Spacer");
            var headSpacerLe = headSpacer.AddComponent<LayoutElement>();
            headSpacerLe.flexibleWidth = 1f;
            var chevronLeft = V5Ui.Panel(head.gameObject, "Arrow chevron-left", new Vector2(32f, 32f),
                V5Tokens.Key4, 8f);
            var chevronLeftIcon = V6Icons.Create(chevronLeft, "chevron-left", 20f, V5Tokens.TextMuted, "Icon");
            if (chevronLeftIcon != null)
            {
                Stretch(chevronLeftIcon.rectTransform, 32f, 32f);
            }
            V5GameUi.Text(head.gameObject, "Metric", "累計スコア", 18f, V5Tokens.Text, TextAlignmentOptions.Center);
            var chevronRight = V5Ui.Panel(head.gameObject, "Arrow chevron-right", new Vector2(32f, 32f),
                V5Tokens.Key4, 8f);
            var chevronRightIcon = V6Icons.Create(chevronRight, "chevron-right", 20f, V5Tokens.TextMuted, "Icon");
            if (chevronRightIcon != null)
            {
                Stretch(chevronRightIcon.rectTransform, 32f, 32f);
            }

            var noData = V5GameUi.Text(panel.gameObject, "No Data", "NO DATA", 24f, V5Tokens.TextDim,
                TextAlignmentOptions.Center);
            At(noData, 8f, 300f, 496f, 44f, new Vector2(0f, 0.5f));
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
            rankingScroll = V6SelectViewLogic.ClampScroll(rankingScroll + step, RankingRows().Count, RankingVisibleRows);
            Rebuild(false);
        }

        // --- ジャケット ---

        /// <summary>Resources.Load 用に拡張子を取り除く ("a/b.jpg" → "a/b")。</summary>
        private static string ResourcePathWithoutExtension(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }
            int dot = path.LastIndexOf('.');
            int slash = path.LastIndexOf('/');
            if (dot > 0 && dot > slash)
            {
                return path.Substring(0, dot);
            }
            return path;
        }

        private void SetJacket(GameObject thumbPanel, V6SongEntry song)
        {
            var img = thumbPanel.GetComponent<Image>();
            Sprite sprite = null;
            if (song != null && !string.IsNullOrEmpty(song.JacketResourcePath))
            {
                if (!jacketCache.TryGetValue(song.JacketResourcePath, out sprite) || sprite == null)
                {
                    var tex = Resources.Load<Texture2D>(ResourcePathWithoutExtension(song.JacketResourcePath));
                    sprite = tex == null ? null : Sprite.Create(tex,
                        new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    jacketCache[song.JacketResourcePath] = sprite;
                }
            }

            if (sprite != null)
            {
                img.type = Image.Type.Simple;
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                // ジャケット無しは空枠 ($k3)。
                img.type = Image.Type.Sliced;
                img.sprite = V5Ui.RoundedSprite(4);
                img.color = V5Tokens.Key3;
            }
        }

        // --- uGUI ユーティリティ (design px 左上原点) ---

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
            var rt = node.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            return node.gameObject;
        }

        private static GameObject At(Component node, float x, float y, float w, float h)
        {
            At(node, x, y);
            var rt = node.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
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

        private static void Center(GameObject node, float parentW, float size)
        {
            var rt = node.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(parentW * 0.5f, 0f);
            rt.sizeDelta = new Vector2(size, size);
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

        private void AddScrollCatcher(GameObject target, int targetId)
        {
            var catcher = target.AddComponent<V6RankingScrollCatcher>();
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

    /// <summary>解像度変化の追従だけを行うホスト (キー入力は読まない)。</summary>
    internal sealed class V6RankingViewDriver : MonoBehaviour
    {
        internal V6RankingView View;

        private void Update()
        {
            if (View != null)
            {
                View.UpdateScale();
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
