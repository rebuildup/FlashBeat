using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;

namespace FlashBeat.UI.V6.Select.View
{
    /// <summary>
    /// Select 画面 (#221) の v6 UI。design/flashbeat-ui.pen v6 の
    /// 02 Select (OENRv) / Panel 選択中の曲 (dRztp) / 02b 該当なし (AqFtx) を実行時に構築する。
    /// 02c 設定 (wSjAm) は簡易パネル (onOpenSettings 未指定時のフォールバック)。
    ///
    /// - データは IV6SelectDataSource 経由のみ (#222 が実装)。実カタログ/実保存スコア/実ランキングを
    ///   表示し、デザイン例の固定値 (サンプル曲名・"988,513" 等) は決して出さない。
    /// - 構築はコードのみ (シーン YAML / .meta は触らない)。Refresh で frame の全子を作り直す。
    /// - 寸法は design px (1728x972 基準)。frame を u = min(W/1728, H/972) 倍して
    ///   3 解像度 (FHD / 2K / Mac) でクリップしないようにする。色は V5Tokens を流用する
    ///   (v6 は新しい色トークンを追加していない)。
    /// - キー: ↑↓ 選択 / Enter・Space Ready (data.CommitReady) / Esc 戻る (onBack) /
    ///   Tab 検索フォーカス (フォーカス中は Input.inputString を SetQuery へ) / O 設定。
    /// - マウス: 行クリック選択 / ホイールスクロール / 戻る・設定・Ready・スクロールボタン。
    /// - SelectSceneManager は呼ばない。onReady / onBack / onOpenSettings は統合担当が渡す。
    /// </summary>
    public sealed class V6SelectView
    {
        // --- design px 定数 (PEN-EXTRACT.md §5) ---
        private const float FrameW = 1728f;
        private const float FrameH = 972f;

        private const float ListX = 0f, ListW = 560f, ListH = 780f;
        private const float RowX = 16f, RowY0 = 48f, RowW = 504f, RowH = 72f, RowStep = 76f;
        private const int ListVisibleRows = 9; // y48..y732 (732 / 76)
        private const int RankingVisibleRows = 4; // 300px パネルに 44px 行 + 8px 間隔で 4 行

        private const float CenterX = 572f, CenterW = 536f;
        private const float RightX = 1120f, RightW = 560f;

        private readonly IV6SelectDataSource data;
        private readonly Action onReady;
        private readonly Action onBack;
        private readonly Action onOpenSettings;

        private readonly Dictionary<string, Sprite> jacketCache = new Dictionary<string, Sprite>();

        private GameObject rootGo;
        private RectTransform frame;
        private GameObject settingsScrim;

        private float u = 1f;
        private int lastWidth;
        private int lastHeight;
        private int listScroll;
        private int rankingScroll;
        private bool searchFocused;
        private bool settingsOpen;
        private int settingsRow;

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
            view.Rebuild(false);
            return view;
        }

        public RectTransform Root => frame;

        /// <summary>data.Changed の再描画起点。選択行が見えるようにスクロールを追従させる。</summary>
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

        /// <summary>検索欄にフォーカスする (統合担当が画面表示時に呼べる)。</summary>
        public void FocusSearch()
        {
            searchFocused = true;
            Rebuild(false);
        }

        // --- 毎フレーム (V6SelectViewDriver から呼ばれる) ---

        internal void Tick()
        {
            if (rootGo == null || !rootGo.activeInHierarchy)
            {
                return;
            }

            UpdateScale();

            bool up = Input.GetKeyDown(KeyCode.UpArrow);
            bool down = Input.GetKeyDown(KeyCode.DownArrow);
            bool confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space);
            bool cancel = Input.GetKeyDown(KeyCode.Escape);
            bool toggleSearch = Input.GetKeyDown(KeyCode.Tab);
            var action = V6SelectViewLogic.MapKey(searchFocused, up, down, confirm, cancel, toggleSearch);

            switch (action)
            {
                case V6SelectAction.UnfocusSearch:
                    searchFocused = false;
                    Rebuild(false);
                    return;
                case V6SelectAction.MoveUp:
                    data.MoveSelection(-1);
                    return;
                case V6SelectAction.MoveDown:
                    data.MoveSelection(1);
                    return;
                case V6SelectAction.Confirm:
                    // Enter/Space = Ready (契約どおり data.CommitReady へ直行)。
                    data.CommitReady();
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
                case V6SelectAction.FocusSearch:
                    searchFocused = true;
                    Rebuild(false);
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

            if (Input.GetKeyDown(KeyCode.O))
            {
                OpenSettings();
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

            frame = V5Ui.Node(rootGo, "V6 Select Frame 1728x972").GetComponent<RectTransform>();
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(FrameW, FrameH);

            var driver = rootGo.AddComponent<V6SelectViewDriver>();
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

            // frame の全子を作り直す (V5 SelectView.Rebuild と同じ方針)。
            var children = new List<GameObject>();
            foreach (Transform child in frame.transform)
            {
                children.Add(child.gameObject);
            }

            for (int i = 0; i < children.Count; i++)
            {
                V5GameUi.DestroyNow(children[i]);
            }

            var songs = data.FilteredSongs;
            int selectIndex = SelectedIndex(songs);
            if (revealSelection)
            {
                listScroll = V6SelectViewLogic.ScrollToReveal(selectIndex, listScroll, ListVisibleRows);
            }

            listScroll = V6SelectViewLogic.ClampScroll(listScroll, songs.Count, ListVisibleRows);
            rankingScroll = V6SelectViewLogic.ClampScroll(rankingScroll,
                data.Selected != null ? data.RankingFor(data.Selected).Count : 0, RankingVisibleRows);

            BuildHeader(songs.Count);
            BuildFooter();
            BuildList(songs, selectIndex);
            BuildCenter();
            BuildRight();
            if (settingsOpen)
            {
                BuildSettings();
            }
        }

        // --- ヘッダー (x24 y0 1680x96) ---

        private void BuildHeader(int filteredCount)
        {
            var header = At(V5Ui.Panel(frame.gameObject, "Header", new Vector2(1680f, 96f), Clear(), 0f), 24f, 0f);

            // 戻るボタン 56x56 (#3A3A3A r8) + Esc キャプション
            var backZone = At(V5Ui.Node(header, "Zone Back"), 0f, 20f);
            var back = Button(backZone, "Back Button", 56f, 56f, 8f, V5Tokens.Key4, () =>
            {
                if (onBack != null) onBack();
            });
            var backCol = V5GameUi.Column(back, "Inner", 2f, TextAnchor.MiddleCenter);
            V5GameUi.Text(backCol.gameObject, "Icon", "←", 22f, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            V5GameUi.Text(backCol.gameObject, "Key", "Esc", 13f, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            Stretch(backCol.GetComponent<RectTransform>(), 56f, 56f);

            var title = V5GameUi.Text(header.gameObject, "Title", "選曲", 38f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            At(title, 72f, 20f, 300f, 56f);

            // 右上: 検索欄 / 件数 / 状態メッセージ / フィルターボタン
            var search = Button(header, "Search", 620f, 48f, 8f, V5Tokens.Overlay, FocusSearch);
            At(search, 0f, 24f);
            var queryText = V5GameUi.Text(search, "Query", SearchDisplayText(), 22f,
                searchFocused ? V5Tokens.Text : V5Tokens.TextDim, TextAlignmentOptions.Left);
            At(queryText, 16f, 0f, 480f, 48f, new Vector2(0f, 0.5f));
            var cap = V5Ui.KeyCap(search, "A-Z", new Vector2(56f, 30f));
            At(cap, 620f - 56f - 12f, 9f);

            var count = V5GameUi.Text(header.gameObject, "Count",
                $"{filteredCount} / {data.AllSongs.Count}", 22f, V5Tokens.TextMuted,
                TextAlignmentOptions.Right);
            At(count, 636f, 24f, 140f, 48f, new Vector2(0f, 0.5f));

            if (!string.IsNullOrEmpty(data.StatusMessage))
            {
                var status = V5GameUi.Text(header.gameObject, "Status", data.StatusMessage, 18f,
                    data.HasError ? V5Tokens.DangerLight : V5Tokens.TextDim, TextAlignmentOptions.Left);
                At(status, 0f, 0f, 620f, 22f, new Vector2(0f, 0.5f));
            }

            var filter = Button(header, "Filter Button", 48f, 48f, 8f, V5Tokens.Key4, FocusSearch);
            At(filter, 792f, 24f);
            var filterIcon = V5GameUi.Text(filter, "Icon", "≡", 24f, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            Stretch(filterIcon.GetComponent<RectTransform>(), 48f, 48f);
        }

        // --- フッター (x24 y876 1680x96) ---

        private void BuildFooter()
        {
            var footer = At(V5Ui.Panel(frame.gameObject, "Footer", new Vector2(1680f, 96f), Clear(), 0f), 24f, 876f);

            var legend = V5GameUi.Row(footer.gameObject, "Legend", 24f, TextAnchor.MiddleLeft);
            At(legend.GetComponent<RectTransform>(), 0f, 30f, 620f, 36f);
            V5Ui.KeyCap(legend.gameObject, "Space", new Vector2(76f, 32f));
            V5GameUi.Text(legend.gameObject, "L1", "決定", 20f, V5Tokens.TextMuted);
            V5Ui.KeyCap(legend.gameObject, "↑↓", new Vector2(56f, 32f));
            V5GameUi.Text(legend.gameObject, "L2", "曲送り", 20f, V5Tokens.TextMuted);
            V5Ui.KeyCap(legend.gameObject, "A-Z", new Vector2(56f, 32f));
            V5GameUi.Text(legend.gameObject, "L3", "検索", 20f, V5Tokens.TextMuted);
            V5Ui.KeyCap(legend.gameObject, "Tab", new Vector2(56f, 32f));
            V5GameUi.Text(legend.gameObject, "L4", "絞り込み", 20f, V5Tokens.TextMuted);

            // 右下: 設定 (secondary) + Ready (primary)。design 座標のまま右詰め。
            var settingsBtn = Button(footer, "Button Settings", 197f, 56f, 8f, V5Tokens.ControlBg, OpenSettings);
            At(settingsBtn, 358f, 20f);
            var settingsRow = V5GameUi.Row(settingsBtn, "Inner", 10f, TextAnchor.MiddleCenter);
            Stretch(settingsRow.GetComponent<RectTransform>(), 197f, 56f);
            V5GameUi.Text(settingsRow.gameObject, "Icon", "⚙", 24f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            V5GameUi.Text(settingsRow.gameObject, "Label", "設定", 30f, V5Tokens.Text, TextAlignmentOptions.Center,
                FontStyles.Normal);

            var ready = Button(footer, "Op Ready", 273f, 56f, 8f, V5Tokens.ButtonPrimaryBg, () =>
            {
                if (onReady != null) onReady();
            });
            At(ready, 567f, 20f);
            var readyRow = V5GameUi.Row(ready, "Inner", 10f, TextAnchor.MiddleCenter);
            Stretch(readyRow.GetComponent<RectTransform>(), 273f, 56f);
            V5GameUi.Text(readyRow.gameObject, "Icon", "▶", 26f, V5Tokens.ButtonPrimaryLabel,
                TextAlignmentOptions.Center);
            V5GameUi.Text(readyRow.gameObject, "Label", "Ready", 30f, V5Tokens.ButtonPrimaryLabel,
                TextAlignmentOptions.Center, FontStyles.Normal);
            var readyCap = V5Ui.Panel(readyRow.gameObject, "Cap", new Vector2(84f, 32f), new Color(0f, 0f, 0f, 0.15f),
                4f);
            var readyCapText = V5GameUi.Text(readyCap, "Key", "Space", 20f, V5Tokens.ButtonPrimaryLabel,
                TextAlignmentOptions.Center);
            Stretch(readyCapText.GetComponent<RectTransform>(), 84f, 32f);
        }

        // --- 左列: 曲リスト (x0 w560 #131313CC r16) ---

        private void BuildList(IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            var list = At(V5Ui.Panel(frame.gameObject, "Song List", new Vector2(ListW, ListH),
                FromHex("#131313CC"), 16f), ListX, 0f);
            AddScrollCatcher(list, 0);

            var rows = V5Ui.Node(list, "Rows");
            At(rows.GetComponent<RectTransform>(), 0f, 0f, ListW, ListH);

            if (data.IsLoading)
            {
                // AqFtx のスケルトン行 (読み込み中)。
                for (int i = 0; i < ListVisibleRows; i++)
                {
                    var skeleton = At(V5Ui.Panel(rows, "Song Skeleton " + i, new Vector2(RowW, RowH),
                        V5Tokens.Key1, 8f), RowX, RowY0 + RowStep * i);
                    At(V5Ui.Panel(skeleton, "Level", new Vector2(44f, 44f), V5Tokens.Key3, 22f), 14f, 14f);
                    At(V5Ui.Panel(skeleton, "Thumb", new Vector2(96f, 54f), V5Tokens.Key3, 4f), 72f, 9f);
                    At(V5Ui.Panel(skeleton, "Texts", new Vector2(220f, 26f), V5Tokens.Key3, 4f), 182f, 14f);
                    At(V5Ui.Panel(skeleton, "Artist", new Vector2(120f, 18f), V5Tokens.Key3, 4f), 182f, 44f);
                    At(V5Ui.Panel(skeleton, "Grade", new Vector2(56f, 36f), V5Tokens.Key3, 4f), 432f, 18f);
                }
            }
            else if (songs.Count == 0)
            {
                // AqFtx: 該当なしの空状態。
                var empty = At(V5Ui.Panel(rows, "Empty Note", new Vector2(448f, 140f), V5Tokens.Key0, 8f), 56f, 300f);
                var col = V5GameUi.Column(empty.gameObject, "Inner", 8f, TextAnchor.MiddleCenter);
                Stretch(col.GetComponent<RectTransform>(), 448f, 140f);
                V5GameUi.Text(col.gameObject, "Icon", "♪", 32f, V5Tokens.TextDim, TextAlignmentOptions.Center);
                V5GameUi.Text(col.gameObject, "Title", "曲が見つかりません", 24f, V5Tokens.Text,
                    TextAlignmentOptions.Center, FontStyles.Normal);
                V5GameUi.Text(col.gameObject, "Hint", "検索語を変えるか フィルターを解除してください", 20f,
                    V5Tokens.TextMuted, TextAlignmentOptions.Center);
            }
            else
            {
                int first = listScroll;
                int last = Mathf.Min(songs.Count, first + ListVisibleRows);
                for (int i = first; i < last; i++)
                {
                    BuildSongRow(rows, songs[i], i, i == selectIndex, RowY0 + RowStep * (i - first));
                }
            }

            BuildScrollButton(list, "Scroll Up", 0f, "↑", -1);
            BuildScrollButton(list, "Scroll Down", 732f, "↓", 1);
            BuildDots(list, songs, selectIndex);
        }

        private void BuildSongRow(GameObject parent, V6SongEntry song, int index, bool selected, float y)
        {
            var row = At(V5Ui.Panel(parent, "Song " + song.Title, new Vector2(RowW, RowH),
                selected ? V5Tokens.Key6 : V5Tokens.Recessed2, 8f), RowX, y);
            var button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            int songId = song.SongId;
            button.onClick.AddListener(() => data.Select(songId));

            var levelBadge = At(V5Ui.Panel(row, "Level", new Vector2(44f, 44f), V5Tokens.Key4, 22f), 14f, 14f);
            var levelText = V5GameUi.Text(levelBadge.gameObject, "Num", song.Level > 0 ? song.Level.ToString() : "–", 22f,
                V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(levelText.GetComponent<RectTransform>(), 44f, 44f);

            var thumb = At(V5Ui.Panel(row, "Thumb", new Vector2(96f, 54f), V5Tokens.Key3, 4f), 72f, 9f);
            SetJacket(thumb, song);

            var texts = V5GameUi.Column(row, "Texts", 2f, TextAnchor.MiddleLeft);
            At(texts.GetComponent<RectTransform>(), 182f, 14f, 240f, 44f);
            V5GameUi.Text(texts.gameObject, "Title", song.Title, 26f, V5Tokens.Text, TextAlignmentOptions.Left,
                FontStyles.Normal);
            V5GameUi.Text(texts.gameObject, "Artist", song.Artist, 20f, V5Tokens.TextMuted);

            var grade = At(V5Ui.Panel(row, "Grade", new Vector2(56f, 36f), V5Tokens.Key4, 4f), 432f, 18f);
            var gradeText = V5GameUi.Text(grade.gameObject, "Text", string.IsNullOrEmpty(song.BestGrade) ? "–" : song.BestGrade,
                22f, V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(gradeText.GetComponent<RectTransform>(), 56f, 36f);
        }

        private void BuildScrollButton(GameObject list, string name, float y, string glyph, int delta)
        {
            var btn = Button(list, name, 140f, 44f, 8f, V5Tokens.Key4, () => ScrollList(delta));
            At(btn, 210f, y);
            var icon = V5GameUi.Text(btn, "Icon", glyph, 24f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(icon.GetComponent<RectTransform>(), 140f, 44f);
        }

        private void BuildDots(GameObject list, IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            var dots = V5Ui.Node(list, "Dot indicator");
            float gap = songs.Count > 60 ? 2f : 4f;
            float size = songs.Count > 60 ? 6f : 8f;
            float total = songs.Count * (size + gap) - gap;
            float y = Mathf.Max(16f, Mathf.Min(136f, (ListH - total) * 0.5f));
            At(dots.GetComponent<RectTransform>(), 536f, y, 24f, total);

            int active = V6SelectViewLogic.DotActiveIndex(selectIndex, songs.Count);
            for (int i = 0; i < songs.Count; i++)
            {
                float d = i == active ? size + 2f : size;
                var dot = At(V5Ui.Panel(dots, "Dot " + (i + 1), new Vector2(d, d),
                    i == active ? V5Tokens.Accent : FromHex("#FFFFFF26"), 5f), 0f, (size + gap) * i);
                Center(dot, 24f, d);
            }
        }

        // --- 中央列: 選択中の曲 (dRztp, $k2 r16) ---

        private void BuildCenter()
        {
            var panel = At(V5Ui.Panel(frame.gameObject, "Selected Song Panel", new Vector2(CenterW, ListH),
                V5Tokens.Key2, 16f), CenterX, 0f);
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
                title.rectTransform.sizeDelta = new Vector2(512f, 56f);
                var artistRow = V5GameUi.Row(meta, "Artist Row", 10f, TextAnchor.MiddleCenter);
                At(artistRow.GetComponent<RectTransform>(), 0f, 60f, 512f, 36f);
                var tag = V5Ui.Panel(artistRow.gameObject, "Tag No", new Vector2(64f, 30f), V5Tokens.Key4, 4f);
                var tagText = V5GameUi.Text(tag, "Text", "No." + song.SongId, 20f, V5Tokens.TextMuted,
                    TextAlignmentOptions.Center);
                Stretch(tagText.GetComponent<RectTransform>(), 64f, 30f);
                V5GameUi.Text(artistRow.gameObject, "Artist", song.Artist, 26f, V5Tokens.TextMuted);
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

            // Controls (skip-back / play / skip-forward): プレビュー再生の API が
            // IV6SelectDataSource に無いため装飾 (未実装は PR に記載)。
            var controls = V5Ui.Node(panel, "Controls");
            At(controls.GetComponent<RectTransform>(), 12f, 668f, 512f, 88f);
            BuildControlButton(controls, "skip-back", 56f, 56f, 28f, V5Tokens.Key1, "◀", 24f, 130f);
            BuildControlButton(controls, "play", 72f, 72f, 36f, V5Tokens.ButtonPrimaryBg, "▶", 32f, 220f);
            BuildControlButton(controls, "skip-forward", 56f, 56f, 28f, V5Tokens.Key1, "▶", 24f, 326f);
        }

        private void BuildStat(GameObject stats, float x, float y, string label, string value)
        {
            var cell = V5GameUi.Row(stats, "Stat " + label, 8f, TextAnchor.MiddleLeft);
            At(cell.GetComponent<RectTransform>(), x, y, 256f, 98f);
            cell.padding = new RectOffset(16, 16, 0, 0);
            V5GameUi.Text(cell.gameObject, "Label", label, 22f, V5Tokens.TextDim);
            var spacer = V5Ui.Node(cell.gameObject, "Spacer");
            var spacerRt = spacer.GetComponent<RectTransform>();
            spacerRt.sizeDelta = new Vector2(0f, 1f);
            var layoutElement = spacer.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            V5GameUi.Text(cell.gameObject, "Value", value, 32f, V5Tokens.Text, TextAlignmentOptions.Right,
                FontStyles.Normal);
        }

        private void BuildControlButton(GameObject controls, string name, float w, float h, float radius,
            Color bg, string glyph, float fs, float x)
        {
            var btn = Button(controls, "Btn " + name, w, h, radius, bg, null);
            At(btn, x, (88f - h) * 0.5f);
            var icon = V5GameUi.Text(btn, "Icon", glyph, fs,
                bg == V5Tokens.ButtonPrimaryBg ? V5Tokens.ButtonPrimaryLabel : V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            Stretch(icon.GetComponent<RectTransform>(), w, h);
        }

        // --- 右列: BEST / SCORE RANKING / PLAYERS ---

        private void BuildRight()
        {
            var column = V5Ui.Node(frame.gameObject, "Ranking Column");
            At(column.GetComponent<RectTransform>(), RightX, 0f, RightW, ListH);

            var best = At(V5Ui.Panel(column, "Best", new Vector2(528f, 112f), Clear(), 0f), 16f, 24f);
            var bestLabel = V5GameUi.Text(best.gameObject, "Label", "BEST", 22f, V5Tokens.TextDim);
            At(bestLabel, 0f, 0f, 528f, 22f);

            var scoreRow = V5GameUi.Row(best.gameObject, "Score Row", 12f, TextAnchor.MiddleLeft);
            At(scoreRow.GetComponent<RectTransform>(), 0f, 30f, 528f, 72f);
            var song = data.Selected;
            V5GameUi.Text(scoreRow.gameObject, "Score",
                song != null ? V6SelectViewLogic.FormatScore(song.BestScore) : "–", 56f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            var spacer = V5Ui.Node(scoreRow.gameObject, "Spacer");
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1f;
            var gradeChip = At(V5Ui.Panel(scoreRow.gameObject, "Grade", new Vector2(72f, 64f), V5Tokens.Key6, 8f),
                0f, 0f);
            var gradeText = V5GameUi.Text(gradeChip.gameObject, "Text",
                song != null && !string.IsNullOrEmpty(song.BestGrade) ? song.BestGrade : "–", 34f, V5Tokens.Text,
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
            var source = V5Ui.Panel(head.gameObject, "Source", new Vector2(96f, 28f), V5Tokens.Key4, 4f);
            var sourceText = V5GameUi.Text(source, "Text", "オンライン", 18f, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            Stretch(sourceText.GetComponent<RectTransform>(), 96f, 28f);

            var rows = data.Selected != null ? data.RankingFor(data.Selected) : null;
            if (rows == null || rows.Count == 0)
            {
                var empty = V5GameUi.Text(ranking.gameObject, "No Data", data.HasError
                    ? (string.IsNullOrEmpty(data.StatusMessage) ? "NO DATA" : data.StatusMessage)
                    : "NO DATA", 24f, V5Tokens.TextDim, TextAlignmentOptions.Left);
                At(empty, 24f, 92f, 496f, 44f, new Vector2(0f, 0.5f));
            }
            else
            {
                int first = rankingScroll;
                int last = Mathf.Min(rows.Count, first + RankingVisibleRows);
                for (int i = first; i < last; i++)
                {
                    BuildRankingRow(ranking, rows[i], i, 92f + 52f * (i - first));
                }
            }

            // PLAYERS (528x288, $k2 r8): IV6SelectDataSource にプレイヤー集計の
            // データ源が無いため空状態のみ (未実装は PR に記載)。
            var players = At(V5Ui.Panel(column, "Players", new Vector2(528f, 288f), V5Tokens.Key2, 8f), 16f, 468f);
            var playersHead = V5GameUi.Row(players.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            At(playersHead.GetComponent<RectTransform>(), 8f, 8f, 496f, 36f);
            V5GameUi.Text(playersHead.gameObject, "Label", "PLAYERS", 18f, V5Tokens.TextDim);
            var playersSpacer = V5Ui.Node(playersHead.gameObject, "Spacer");
            var playersSpacerLe = playersSpacer.AddComponent<LayoutElement>();
            playersSpacerLe.flexibleWidth = 1f;
            V5GameUi.Text(playersHead.gameObject, "Mode", "累計スコア", 18f, V5Tokens.Text, TextAlignmentOptions.Right,
                FontStyles.Normal);
            var noPlayers = V5GameUi.Text(players.gameObject, "No Data", "NO DATA", 24f, V5Tokens.TextDim);
            At(noPlayers, 24f, 60f, 496f, 44f, new Vector2(0f, 0.5f));
        }

        private void BuildRankingRow(GameObject ranking, V6RankingRow row, int index, float y)
        {
            var line = At(V5Ui.Panel(ranking, "Score " + (index + 1), new Vector2(496f, 44f),
                row.IsYou ? V5Tokens.Key6 : V5Tokens.Key1, 8f), 8f, y);

            var pos = At(V5Ui.Panel(line, "Pos", new Vector2(36f, 36f), V5Tokens.Key4, 18f), 4f, 4f);
            var posText = V5GameUi.Text(pos, "Num", row.Position.ToString(), 22f,
                row.IsYou ? V5Tokens.Text : V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(posText.GetComponent<RectTransform>(), 36f, 36f);

            var name = V5GameUi.Text(line.gameObject, "Name", row.IsYou ? "YOU" : row.PlayerName, 22f, V5Tokens.Text);
            At(name, 52f, 0f, 140f, 44f, new Vector2(0f, 0.5f));

            var score = V5GameUi.Text(line.gameObject, "Score", V6SelectViewLogic.FormatScore(row.Score), 22f, V5Tokens.Text,
                TextAlignmentOptions.Right);
            At(score, 200f, 0f, 180f, 44f, new Vector2(0f, 0.5f));

            var grade = At(V5Ui.Panel(line, "Grade", new Vector2(56f, 36f), V5Tokens.Key4, 4f), 436f, 4f);
            var gradeText = V5GameUi.Text(grade.gameObject, "Text", string.IsNullOrEmpty(row.Grade) ? "–" : row.Grade, 22f,
                V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(gradeText.GetComponent<RectTransform>(), 56f, 36f);
        }

        // --- 設定 (wSjAm 簡易パネル, onOpenSettings 未指定時のフォールバック) ---

        private void OpenSettings()
        {
            if (onOpenSettings != null)
            {
                onOpenSettings();
                return;
            }

            settingsOpen = true;
            settingsRow = 0;
            Rebuild(false);
        }

        private void CloseSettings()
        {
            settingsOpen = false;
            Rebuild(false);
        }

        private void BuildSettings()
        {
            settingsScrim = At(V5Ui.Panel(frame.gameObject, "Settings Scrim", new Vector2(FrameW, FrameH),
                FromHex("#00000099"), 0f), 0f, 0f);
            var button = settingsScrim.AddComponent<Button>();
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            button.onClick.AddListener(CloseSettings);

            var panel = At(V5Ui.Panel(settingsScrim, "Settings Panel", new Vector2(RightW, ListH),
                V5Tokens.Key2, 16f), RightX, 0f);
            var title = V5GameUi.Text(panel.gameObject, "Title", "このプレイの設定", 26f, V5Tokens.Text, TextAlignmentOptions.Left,
                FontStyles.Normal);
            At(title, 24f, 16f, 512f, 36f);

            BuildSettingsRow(panel, 0, 64f, "Key Guide", PlaySession.KeyGuideLabel(PlaySession.KeyGuide));
            BuildSettingsRow(panel, 1, 132f, "Lyrics", PlaySession.LyricsLabel(PlaySession.LyricsEnabled));
            BuildSettingsRow(panel, 2, 200f, "NoteSpeed",
                Mathf.RoundToInt(GManager.noteSpeed).ToString());

            var close = Button(panel, "Op Close", 197f, 56f, 8f, V5Tokens.Key4, CloseSettings);
            At(close, 339f, 700f);
            var closeRow = V5GameUi.Row(close, "Inner", 10f, TextAnchor.MiddleCenter);
            Stretch(closeRow.GetComponent<RectTransform>(), 197f, 56f);
            V5GameUi.Text(closeRow.gameObject, "Label", "閉じる", 30f, V5Tokens.Text, TextAlignmentOptions.Center);
            var cap = V5Ui.Panel(closeRow.gameObject, "Cap", new Vector2(48f, 32f), new Color(0f, 0f, 0f, 0.25f), 4f);
            var capText = V5GameUi.Text(cap, "Key", "Esc", 20f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(capText.GetComponent<RectTransform>(), 48f, 32f);
        }

        private void BuildSettingsRow(GameObject panel, int index, float y, string label, string value)
        {
            var row = At(V5Ui.Panel(panel, "Set " + label, new Vector2(512f, 56f),
                settingsRow == index ? V5Tokens.Key5 : FromHex("#2D2D2D"), 8f), 24f, y);

            var labelText = V5GameUi.Text(row.gameObject, "Label", label, 24f, V5Tokens.Text);
            At(labelText, 16f, 0f, 240f, 56f, new Vector2(0f, 0.5f));

            var left = Button(row, "Prev", 40f, 40f, 8f, V5Tokens.Key4, () => ChangeSetting(index, -1));
            At(left, 396f, 8f);
            var leftIcon = V5GameUi.Text(left, "Icon", "←", 20f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(leftIcon.GetComponent<RectTransform>(), 40f, 40f);

            var valueText = V5GameUi.Text(row.gameObject, "Value", value, 24f, V5Tokens.Text, TextAlignmentOptions.Center);
            At(valueText, 300f, 0f, 88f, 56f, new Vector2(0f, 0.5f));

            var right = Button(row, "Next", 40f, 40f, 8f, V5Tokens.Key4, () => ChangeSetting(index, 1));
            At(right, 444f, 8f);
            var rightIcon = V5GameUi.Text(right, "Icon", "→", 20f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(rightIcon.GetComponent<RectTransform>(), 40f, 40f);
        }

        /// <summary>設定行の値を実 API 経由で変える (V5 SelectView.ChangeReadyValue と同じ結線)。</summary>
        private void ChangeSetting(int row, int delta)
        {
            if (row == 0)
            {
                PlaySession.SetKeyGuide(PlaySession.NextKeyGuide(PlaySession.KeyGuide, delta));
            }
            else if (row == 1)
            {
                // Lyrics (Auto / Off) を 1 ステップ回す (V5 の LyricsModes と同じ順)。
                int index = PlaySession.LyricsEnabled ? 0 : 1;
                index = (index + delta % 2 + 2) % 2;
                PlaySession.SetLyricsEnabled(index == 0);
            }
            else
            {
                float value = Mathf.RoundToInt(GManager.noteSpeed) + delta;
                GManager.noteSpeed = Mathf.Clamp(value, OptionSettings.NoteSpeedMin, OptionSettings.NoteSpeedMax);
                OptionSettings.Save();
            }

            Rebuild(false);
        }

        // --- 入力ヘルパ ---

        private void ScrollList(int delta)
        {
            listScroll = V6SelectViewLogic.ClampScroll(listScroll + delta, data.FilteredSongs.Count,
                ListVisibleRows);
            Rebuild(false);
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
                Rebuild(false);
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

        private void SetJacket(GameObject thumbPanel, V6SongEntry song)
        {
            var img = thumbPanel.GetComponent<Image>();
            Sprite sprite = null;
            if (song != null && !string.IsNullOrEmpty(song.JacketResourcePath))
            {
                if (!jacketCache.TryGetValue(song.JacketResourcePath, out sprite) || sprite == null)
                {
                    var tex = Resources.Load<Texture2D>(song.JacketResourcePath);
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
                // ジャケット無しは空枠 (AqFtx の Thumb と同じ $k3)。
                img.type = Image.Type.Sliced;
                img.sprite = V5Ui.RoundedSprite(4);
                img.color = V5Tokens.Key3;
            }
        }

        // --- uGUI ユーティリティ (design px 左上原点) ---

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
