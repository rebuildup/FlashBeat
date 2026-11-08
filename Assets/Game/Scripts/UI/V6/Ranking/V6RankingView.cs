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
    /// - PLAYERS はプレイヤー総合ランキングの API が既存に無いので空状態
    ///   (NO DATA) のみ。未実装は PR に記載する。
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

        private readonly Dictionary<string, Sprite> jacketCache = new Dictionary<string, Sprite>();

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

            // 画面全体を v6 背景 (契約 §3 bg #1B1B1B) で敷き、レガシーのクリーム色背景が
            // 透けないようにする。レターボックス枠は作らない。
            var bg = rootGo.AddComponent<Image>();
            bg.color = V5Tokens.Bg;
            bg.raycastTarget = true;

            // 共通フレーム (rw7zx 1728×972 / 契約 §4) を使い回す。
            frame = V6Frame.Create(rootGo.transform, "V6 Ranking Frame").GetComponent<RectTransform>();
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
            rankingScroll = V6SelectViewLogic.ClampScroll(rankingScroll, RankingRows().Count, RankingVisibleRows);

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
            if (go == null)
            {
                return;
            }
            var children = new List<GameObject>();
            foreach (Transform child in go.transform)
            {
                children.Add(child.gameObject);
            }
            for (int i = 0; i < children.Count; i++)
            {
                V5GameUi.DestroyNow(children[i]);
            }
        }

        // --- ヘッダー (共通フレームの Header ゾーン: design (24,0) 1680×96) ---

        private void BuildHeader(GameObject headerZone, V6SongEntry song)
        {
            // 戻るボタン 56×56 (#3A3A3A r8) + Key fs16 (design: Back button / Key 'Esc' fs16)
            var backZone = At(V5Ui.Node(headerZone, "Zone Back"), 0f, 20f);
            var back = MakeButton(V5Ui.Panel(backZone, "Back Button", new Vector2(56f, 56f), V5Tokens.Key4, 8f),
                () => TriggerBack());
            var backCol = V5GameUi.Column(back, "Inner", 2f, TextAnchor.MiddleCenter);
            Stretch(backCol.GetComponent<RectTransform>(), 56f, 56f);
            V6Icons.Create(backCol.gameObject, "arrow-left", 24f, V5Tokens.TextMuted, "Icon");
            V5GameUi.Text(backCol.gameObject, "Key", "Esc", 16f, V5Tokens.TextMuted, TextAlignmentOptions.Center);

            var title = V5GameUi.Text(headerZone, "Title", "RANKING", 38f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            At(title, 72f, 20f, 400f, 56f);

            // 右上: 曲情報 (ジャケット + 曲名/作家 + LEVEL / BPM / NOTES)。
            // design (S1TZD): 情報ゾーンは header 内 x840..1680。統計は右端で揃え
            // (NOTES の右端 = header 内 x1680 = フレーム内 x1704)、frame 幅 1728 の外に出さない。
            bool hasSong = song != null;
            var thumb = At(V5Ui.Panel(headerZone, "Thumb", new Vector2(64f, 36f), V5Tokens.Key3, 4f),
                924f, 30f);
            if (hasSong)
            {
                SetJacket(thumb, song);
            }

            var songZone = V5Ui.Node(headerZone, "Song");
            At(songZone.GetComponent<RectTransform>(), 1012f, 10f, 320f, 76f);
            var songTitle = V5GameUi.Text(songZone, "Title", hasSong ? song.Title : "–", 30f, V5Tokens.Text,
                TextAlignmentOptions.Left);
            songTitle.textWrappingMode = TextWrappingModes.NoWrap;
            var songArtist = V5GameUi.Text(songZone, "Artist", hasSong ? song.Artist : "", 20f,
                V5Tokens.TextMuted, TextAlignmentOptions.Left);
            songArtist.textWrappingMode = TextWrappingModes.NoWrap;
            At(songArtist, 0f, 40f, 320f, 32f);

            At(V5Ui.Panel(headerZone, "Divider", new Vector2(1f, 44f), FromHex("#FFFFFF26"), 0f), 1356f, 26f);

            BuildHeaderStat(headerZone, 1360f, "LEVEL", hasSong && song.Level > 0 ? song.Level.ToString() : "–");
            BuildHeaderStat(headerZone, 1470f, "BPM", hasSong && song.Bpm > 0
                ? Mathf.RoundToInt((float)song.Bpm).ToString() : "–");
            BuildHeaderStat(headerZone, 1580f, "NOTES", hasSong && song.NoteCount > 0 ? song.NoteCount.ToString() : "–");
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

        // --- フッター (共通フレームの Footer ゾーン: design (24,876) 1680×96) ---

        private void BuildFooter(GameObject footerZone)
        {
            // design (rw7zx): キー操作ゾーン (0,30)、Legend 間 gap16、Legend 内は
            // KeyCap (高36 / pad[0,10] / fs22) + gap10 + Label fs22 $text-muted。
            BuildKeyLegend(footerZone, new[]
            {
                new[] { "↑↓", "曲送り" },
                new[] { "Space", "決定" },
                new[] { "Tab", "切替" },
                new[] { "Esc", "戻る" }
            });

            // 右下: 設定 (design: 補助操作 (840+358,20) 197×56, icon settings + Cap 'O')
            // と この曲へ (design: 主操作 (840+567,20) 273×56, icon + Cap 'Space')。
            GameObject settingsBtn = V6Controls.ButtonSecondary(footerZone, "設定",
                new Vector2(V6RankingLogic.FooterSettingsButtonX, V6RankingLogic.FooterButtonY),
                new Vector2(197f, V6RankingLogic.FooterButtonH), "settings", "O");
            ConfigureButton(settingsBtn, TriggerOpenSettings);
            TintButtonCap(settingsBtn, SecondaryCapColor);

            GameObject gotoBtn = V6Controls.ButtonPrimary(footerZone, "この曲へ",
                new Vector2(V6RankingLogic.FooterGotoButtonX, V6RankingLogic.FooterButtonY),
                new Vector2(273f, V6RankingLogic.FooterButtonH), "music", "Space");
            ConfigureButton(gotoBtn, TriggerGotoSong);
            TintButtonCap(gotoBtn, PrimaryCapColor);
        }

        /// <summary>design (rw7zx) の Secondary ボタン内 Cap の塗り (#00000040)。</summary>
        private static readonly Color SecondaryCapColor = new Color(0f, 0f, 0f, 0x40 / 255f);

        /// <summary>design (rw7zx / 契約 §4) の Primary ボタン内 KeyCap の塗り (#00000026)。</summary>
        private static readonly Color PrimaryCapColor = new Color(0f, 0f, 0f, 0x26 / 255f);

        /// <summary>
        /// フッター左下のキー凡例。design (rw7zx): 高さ 36 の KeyCap (fs22) + gap10 + Label fs22、
        /// 項目間 gap16、zone 内 y=30 から。
        /// </summary>
        private static void BuildKeyLegend(GameObject footer, string[][] items)
        {
            float fs = V6Controls.KeyCapFontSize;
            float x = 0f;
            for (int i = 0; i < items.Length; i++)
            {
                string key = items[i][0];
                string label = items[i][1];
                float capW = Mathf.Ceil(V6Ui.LabelAdvanceEm(key) * fs) + V6Controls.KeyCapPaddingX * 2f;
                V6Controls.KeyCap(footer, key, new Vector2(x, 30f), new Vector2(capW, V6Tokens.KeyCapHeight));
                x += capW + 10f;
                float labelW = Mathf.Max(1f, V6Layout.EstimateTextWidth(label, fs));
                var labelText = V5GameUi.Text(footer, "Legend Label " + label, label, fs, V5Tokens.TextMuted);
                At(labelText, x, 30f, labelW, V6Tokens.KeyCapHeight);
                x += labelW + (i < items.Length - 1 ? 16f : 0f);
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

        /// <summary>
        /// ボタン内 Cap の塗りを design 値 (#00000040 / #00000026) に合わせる。
        /// 共通部品 (V6Controls) は $k4 固定で編集禁止のため、呼び出し側で上書きする。
        /// </summary>
        private static void TintButtonCap(GameObject buttonGo, Color color)
        {
            for (int i = 0; i < buttonGo.transform.childCount; i++)
            {
                Transform child = buttonGo.transform.GetChild(i);
                if (child.name.StartsWith("KeyCap", StringComparison.Ordinal))
                {
                    var img = child.GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = color;
                    }
                }
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
            var row = V5Ui.Panel(parent, "Song " + song.Title, new Vector2(504f, RowH),
                selected ? V5Tokens.Key6 : V5Tokens.Recessed2, V6Tokens.RadiusMedium);
            V6Layout.Place(row.GetComponent<RectTransform>(), new Vector2(RowX, y), new Vector2(504f, RowH));
            var button = MakeViewButton(row, null).GetComponent<Button>();
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
            // design: Scroll Up/Down は 140×44 $k4 r8 + icon 24×24 ($text-muted)
            GameObject btn = V6Controls.IconButton(list, iconSprite, new Vector2(210f, y),
                new Vector2(140f, 44f), V5Tokens.Key4, V6Tokens.RadiusMedium, 24f, V5Tokens.TextMuted);
            ConfigureButton(btn, () => ScrollList(delta));
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

        private void BuildScoreRanking(GameObject mainZone, V6SongEntry song)
        {
            // design (S1TZD): SCORE RANKING は Main 内 (584,24) 512×732
            var panel = At(V5Ui.Panel(mainZone, "Score Ranking", new Vector2(RankingW, RankingH),
                V5Tokens.Key2, V6Tokens.RadiusLarge), RankingX, RankingY);
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
                BuildRankingRow(panel, rows[i], i - first);
            }
        }

        private void BuildSegment(GameObject head, string label, V6RankingLogic.RankingSource target)
        {
            bool active = source == target;
            var chip = MakeButton(V5Ui.Panel(head, "Segment " + label, new Vector2(72f, 32f),
                active ? V5Tokens.Key4 : V5Tokens.Key1, 4f), ToggleSource);
            // design (S1TZD Segment): text fs20
            var text = V5GameUi.Text(chip, "Text", V6RankingLogic.SourceLabel(target), 20f,
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

        /// <summary>
        /// ランキング行。行内訳 (Pos 36×36 / Name / Value 右寄せ / Grade 56×36) は
        /// 契約 §5 の Ranking 行仕様 = 共通部品 V6Lists.RankRow を使い回す。
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
        /// PLAYERS パネル。プレイヤー総合ランキング (累計スコア) を返す既存 API が
        /// 無いため、ヘッドと空状態のみ (未実装は PR に記載)。
        /// </summary>
        private void BuildPlayers(GameObject mainZone)
        {
            var panel = At(V5Ui.Panel(mainZone, "Players", new Vector2(PlayersW, RankingH),
                V5Tokens.Key2, V6Tokens.RadiusLarge), PlayersX, 24f);

            var head = V5GameUi.Row(panel.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            At(head, 8f, 8f, 496f, 40f);
            V5GameUi.Text(head.gameObject, "Title", "PLAYERS", 18f, V5Tokens.TextDim);
            var spacer = V5Ui.Node(head.gameObject, "Spacer");
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1f;
            MakeIconButton(head.gameObject, "chevron-left", 0f, 0f, 32f, V5Tokens.Key4, V6Tokens.RadiusMedium, 24f, V5Tokens.TextMuted);
            V5GameUi.Text(head.gameObject, "Metric", "累計スコア", 18f, V5Tokens.Text, TextAlignmentOptions.Center);
            MakeIconButton(head.gameObject, "chevron-right", 0f, 0f, 32f, V5Tokens.Key4, V6Tokens.RadiusMedium, 24f, V5Tokens.TextMuted);
            V5GameUi.Text(panel.gameObject, "NoData", "NO DATA", 24f, V5Tokens.TextDim, TextAlignmentOptions.Center);
            At(panel.transform.Find("NoData").gameObject, 8f, 300f, 496f, 44f, new Vector2(0f, 0.5f));
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

        /// <summary>アイコンのみのボタン (V6Controls.IconButton の薄いラッパ)。</summary>
        private static GameObject MakeIconButton(GameObject parent, string iconName, float x, float y,
            float size, Color bg, float radius, float iconSize, Color iconColor)
        {
            return V6Controls.IconButton(parent, iconName, new Vector2(x, y), new Vector2(size, size),
                bg, radius, iconSize, iconColor);
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

        /// <summary>Button を追加して操作可能にする (遷移なし)。</summary>
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
