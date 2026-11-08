using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using FlashBeat.UI.V5;
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
    ///   (未プレイ / 別曲を選択中) 場合は "–"。HOLD は既存に集計が無いので常に "–"。
    /// - キー操作は統合担当が結線する (Input は直接読まない):
    ///   MoveSelection / ToggleSort / TriggerBack / TriggerOpenSettings / TriggerGotoSong。
    /// - SelectSceneManager は呼ばない。onBack / onOpenSettings / onGotoSong は統合担当が渡す。
    /// </summary>
    public sealed class V6HistoryView
    {
        /// <summary>現在生成されている v6 履歴画面 (履歴シーンに 1 個だけ)。</summary>
        public static V6HistoryView Current { get; private set; }

        // --- design px 定数 (q9JZcH の実測値) ---
        private const float FrameW = 1728f;
        private const float FrameH = 972f;

        // design Frame (1728x972) の Main ゾーンは (24, 96)。
        private const float MainX = 24f, MainY = 96f;
        private const float ListW = 560f, ListH = 780f;
        private const float RowX = 16f, RowY0 = 48f, RowH = 64f, RowStep = 72f;
        private const int ListVisibleRows = 10; // y48..y696 (+64 = 760 <= 780)
        private const float CenterX = 572f, CenterW = 536f;
        private const float RightX = 1120f, RightW = 560f;
        private const int SongRankingVisibleRows = 4; // 曲ランキング情報 528x304 に 52px 行が 4 行

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
        private int songRankingScroll;
        private V6HistoryLogic.HistorySort sort = V6HistoryLogic.HistorySort.Score;
        private int selectedSongId = -1;

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

        /// <summary>検証用: 表示中の履歴件数。</summary>
        public int VisibleHistoryCount
        {
            get { return V6HistoryLogic.HistoryList(data.AllSongs, sort).Count; }
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

        private void SelectSong(int songId)
        {
            if (selectedSongId == songId)
            {
                return;
            }
            selectedSongId = songId;
            // オンラインランキングの取得 (cloud 曲) と Changed 通知はデータソース側。
            data.Select(songId);
            Rebuild(false);
        }

        private void BuildRoot(RectTransform parent)
        {
            rootGo = V5Ui.Node(parent.gameObject, "V6 History View");
            var rt = rootGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            frame = V5Ui.Node(rootGo, "V6 History Frame 1728x972").GetComponent<RectTransform>();
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(FrameW, FrameH);

            var driver = rootGo.AddComponent<V6HistoryViewDriver>();
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

            BuildHeader(history.Count);
            BuildFooter();
            BuildList(history, selectIndex);
            BuildCenter(selected);
            BuildRight(selected);
        }

        // --- ヘッダー (x24 y0 1680x96) ---

        private void BuildHeader(int playedCount)
        {
            var header = At(V5Ui.Panel(frame.gameObject, "Header", new Vector2(1680f, 96f), Clear(), 0f), 24f, 0f);

            // 戻るボタン 56x56 (#3A3A3A r8) + Esc キャプション
            var backZone = At(V5Ui.Node(header, "Zone Back"), 0f, 20f);
            var back = MakeButton(V5Ui.Panel(backZone, "Back Button", new Vector2(56f, 56f), V5Tokens.Key4, 8f),
                () => TriggerBack());
            var backCol = V5GameUi.Column(back, "Inner", 2f, TextAnchor.MiddleCenter);
            Stretch(backCol.GetComponent<RectTransform>(), 56f, 56f);
            V5GameUi.Text(backCol.gameObject, "Icon", "←", 22f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            V5GameUi.Text(backCol.gameObject, "Key", "Esc", 13f, V5Tokens.TextMuted, TextAlignmentOptions.Center);

            var title = V5GameUi.Text(header.gameObject, "Title", "HISTORY", 38f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            At(title, 72f, 20f, 400f, 56f);

            // 右上: 並び順チップ (クリックでトグル) + 件数
            var sortChip = MakeButton(V5Ui.Panel(header, "Sort", new Vector2(132f, 44f), V5Tokens.Key4, 8f), ToggleSort);
            At(sortChip, 840f, 26f);
            var sortText = V5GameUi.Text(sortChip, "Text", V6HistoryLogic.SortLabel(sort), 20f, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            Stretch(sortText.GetComponent<RectTransform>(), 132f, 44f);

            var count = V5GameUi.Text(header.gameObject, "Count", V6HistoryLogic.PlaysLabel(playedCount), 22f,
                V5Tokens.TextMuted, TextAlignmentOptions.Right);
            At(count, 1400f, 24f, 280f, 48f, new Vector2(0f, 0.5f));
        }

        // --- フッター (x24 y876 1680x96) ---

        private void BuildFooter()
        {
            var footer = At(V5Ui.Panel(frame.gameObject, "Footer", new Vector2(1680f, 96f), Clear(), 0f), 24f, 876f);

            var legend = V5GameUi.Row(footer.gameObject, "Legend", 24f, TextAnchor.MiddleLeft);
            At(legend.GetComponent<RectTransform>(), 0f, 30f, 620f, 36f);
            V6Ui.KeyCap(legend.gameObject, "↑↓", new Vector2(56f, 32f));
            V5GameUi.Text(legend.gameObject, "L1", "選択", 20f, V5Tokens.TextMuted);
            V6Ui.KeyCap(legend.gameObject, "Space", new Vector2(76f, 32f));
            V5GameUi.Text(legend.gameObject, "L2", "決定", 20f, V5Tokens.TextMuted);
            V6Ui.KeyCap(legend.gameObject, "Esc", new Vector2(56f, 32f));
            V5GameUi.Text(legend.gameObject, "L3", "戻る", 20f, V5Tokens.TextMuted);

            // 右下: 設定 (secondary) + この曲へ (primary)。design 座標のまま右詰め。
            var settingsBtn = MakeButton(V5Ui.Panel(footer, "Button Settings", new Vector2(197f, 56f),
                V5Tokens.ControlBg, 8f), TriggerOpenSettings);
            At(settingsBtn, 1198f, 20f);
            var settingsRow = V5GameUi.Row(settingsBtn, "Inner", 10f, TextAnchor.MiddleCenter);
            Stretch(settingsRow.GetComponent<RectTransform>(), 197f, 56f);
            V5GameUi.Text(settingsRow.gameObject, "Icon", "⚙", 24f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            V5GameUi.Text(settingsRow.gameObject, "Label", "設定", 30f, V5Tokens.Text, TextAlignmentOptions.Center);

            var gotoBtn = MakeButton(V5Ui.Panel(footer, "Op この曲へ", new Vector2(273f, 56f),
                V5Tokens.ButtonPrimaryBg, 8f), TriggerGotoSong);
            At(gotoBtn, 1407f, 20f);
            var gotoRow = V5GameUi.Row(gotoBtn, "Inner", 10f, TextAnchor.MiddleCenter);
            Stretch(gotoRow.GetComponent<RectTransform>(), 273f, 56f);
            V5GameUi.Text(gotoRow.gameObject, "Icon", "♪", 26f, V5Tokens.ButtonPrimaryLabel, TextAlignmentOptions.Center);
            V5GameUi.Text(gotoRow.gameObject, "Label", "この曲へ", 30f, V5Tokens.ButtonPrimaryLabel,
                TextAlignmentOptions.Center);
            var cap = V5Ui.Panel(gotoRow.gameObject, "Cap", new Vector2(84f, 32f), new Color(0f, 0f, 0f, 0.15f), 4f);
            var capText = V5GameUi.Text(cap, "Key", "Space", 20f, V5Tokens.ButtonPrimaryLabel, TextAlignmentOptions.Center);
            Stretch(capText.GetComponent<RectTransform>(), 84f, 32f);
        }

        // --- 左列: 履歴リスト (x0 w560 #131313CC r16) ---

        private void BuildList(List<V6SongEntry> history, int selectIndex)
        {
            var list = At(V5Ui.Panel(frame.gameObject, "History List", new Vector2(ListW, ListH),
                FromHex("#131313CC"), 16f), MainX + 0f, MainY);
            AddScrollCatcher(list, 0);

            var rows = V5Ui.Node(list, "Rows");
            At(rows.GetComponent<RectTransform>(), 0f, 0f, ListW, ListH);

            if (history.Count == 0)
            {
                // 空状態: プレイ記録がまだ無い (design に History 空状態は無いので
                // Select の空状態 (AqFtx) と同じ表現に合わせる)。
                var empty = At(V5Ui.Panel(rows, "Empty Note", new Vector2(448f, 140f), V5Tokens.Key0, 8f), 56f, 300f);
                var col = V5GameUi.Column(empty.gameObject, "Inner", 8f, TextAnchor.MiddleCenter);
                Stretch(col.GetComponent<RectTransform>(), 448f, 140f);
                V5GameUi.Text(col.gameObject, "Icon", "♪", 32f, V5Tokens.TextDim, TextAlignmentOptions.Center);
                V5GameUi.Text(col.gameObject, "Title", "プレイ履歴がありません", 24f, V5Tokens.Text,
                    TextAlignmentOptions.Center);
                V5GameUi.Text(col.gameObject, "Hint", "曲をプレイすると記録がここに残ります", 20f,
                    V5Tokens.TextMuted, TextAlignmentOptions.Center);
            }
            else
            {
                int first = listScroll;
                int last = Mathf.Min(history.Count, first + ListVisibleRows);
                for (int i = first; i < last; i++)
                {
                    BuildHistoryRow(rows, history[i], i + 1, i == selectIndex, RowY0 + RowStep * (i - first));
                }
            }

            BuildScrollButton(list, "Scroll Up", 0f, "↑", -1);
            BuildScrollButton(list, "Scroll Down", 732f, "↓", 1);
        }

        private void BuildHistoryRow(GameObject parent, V6SongEntry song, int pos, bool selected, float y)
        {
            var row = At(V5Ui.Panel(parent, "History " + song.Title, new Vector2(496f, RowH),
                selected ? V5Tokens.Key6 : V5Tokens.Recessed2, 8f), RowX, y);
            var button = MakeViewButton(row.gameObject, null).GetComponent<Button>();
            int songId = song.SongId;
            button.onClick.AddListener(() => SelectSong(songId));

            var posBadge = At(V5Ui.Panel(row, "Pos", new Vector2(36f, 36f), V5Tokens.Key4, 18f), 14f, 14f);
            var posText = V5GameUi.Text(posBadge.gameObject, "PosText", pos.ToString(), 22f,
                pos == 1 ? V5Tokens.Text : V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(posText.GetComponent<RectTransform>(), 36f, 36f);

            var title = V5GameUi.Text(row.gameObject, "Title", song.Title, 24f, V5Tokens.Text);
            At(title, 64f, 0f, 220f, RowH, new Vector2(0f, 0.5f));

            var score = V5GameUi.Text(row.gameObject, "Score", V6SelectViewLogic.FormatScore(song.BestScore), 24f,
                V5Tokens.Text, TextAlignmentOptions.Right);
            At(score, 296f, 0f, 120f, RowH, new Vector2(0f, 0.5f));

            var grade = At(V5Ui.Panel(row, "Grade", new Vector2(56f, 36f), V5Tokens.Key4, 4f), 424f, 14f);
            var gradeText = V5GameUi.Text(grade.gameObject, "GradeText",
                string.IsNullOrEmpty(song.BestGrade) ? "–" : song.BestGrade, 22f, V5Tokens.Text,
                TextAlignmentOptions.Center);
            Stretch(gradeText.GetComponent<RectTransform>(), 56f, 36f);
        }

        private void BuildScrollButton(GameObject list, string name, float y, string glyph, int delta)
        {
            var btn = MakeButton(V5Ui.Panel(list, name, new Vector2(140f, 44f), V5Tokens.Key4, 8f),
                () => ScrollList(delta));
            At(btn, 210f, y);
            var icon = V5GameUi.Text(btn, "Icon", glyph, 24f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(icon.GetComponent<RectTransform>(), 140f, 44f);
        }

        // --- 中央列: 選択中の曲 (dRztp と同じ構造) ---

        private void BuildCenter(V6SongEntry song)
        {
            var panel = At(V5Ui.Panel(frame.gameObject, "Selected Song Panel", new Vector2(CenterW, ListH),
                V5Tokens.Key2, 16f), MainX + CenterX, MainY);
            bool hasSong = song != null;

            var hero = At(V5Ui.Panel(panel, "Hero", new Vector2(512f, 288f), V5Tokens.Key3, 8f), 12f, 24f);
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
            // データソースに無いため装飾 (未実装は PR に記載、V6SelectView と同じ扱い)。
            var controls = V5Ui.Node(panel, "Controls");
            At(controls.GetComponent<RectTransform>(), 12f, 668f, 512f, 88f);
            BuildControlButton(controls, "skip-back", 0f, 56f, 56f, 28f, "◀", 24f);
            BuildControlButton(controls, "play", 220f, 72f, 72f, 36f, "▶", 32f);
            BuildControlButton(controls, "skip-forward", 326f, 56f, 56f, 28f, "▶", 24f);
        }

        /// <summary>
        /// 統計セル (BPM / NOTES / LEVEL / BEST)。LayoutGroup ではなく絶対位置で置く
        /// (V6SelectView.BuildStat と同じ実装)。
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
                TextAlignmentOptions.Right);
            var valueRt = valueText.GetComponent<RectTransform>();
            valueRt.anchorMin = new Vector2(1f, 0.5f);
            valueRt.anchorMax = new Vector2(1f, 0.5f);
            valueRt.pivot = new Vector2(1f, 0.5f);
            valueRt.anchoredPosition = new Vector2(-16f, 0f);
            valueRt.sizeDelta = new Vector2(200f, 44f);
        }

        private void BuildControlButton(GameObject controls, string name, float x, float w, float h, float radius,
            string glyph, float fs)
        {
            var btn = MakeButton(V5Ui.Panel(controls, "Btn " + name, new Vector2(w, h), V5Tokens.Key4, radius), null);
            At(btn, x, (88f - h) * 0.5f);
            var icon = V5GameUi.Text(btn, "Icon", glyph, fs, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            Stretch(icon.GetComponent<RectTransform>(), w, h);
        }

        // --- 右列: プレイヤー情報 / プレイ情報 / 曲ランキング情報 ---

        private void BuildRight(V6SongEntry song)
        {
            var column = V5Ui.Node(frame.gameObject, "Record Column");
            At(column.GetComponent<RectTransform>(), MainX + RightX, MainY, RightW, ListH);

            BuildPlayerInfo(column, song);
            BuildPlayInfo(column, song);
            BuildSongRanking(column, song);
        }

        /// <summary>プレイヤー情報 (528x176 $k2): 累計 / プレイ曲 / S以上 / 順位。</summary>
        private void BuildPlayerInfo(GameObject column, V6SongEntry song)
        {
            List<V6SongEntry> history = V6HistoryLogic.HistoryList(data.AllSongs, sort);
            var panel = At(V5Ui.Panel(column, "Player Info", new Vector2(528f, 176f), V5Tokens.Key2, 8f), 16f, 24f);

            var head = V5GameUi.Row(panel.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            At(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 36f);
            V5GameUi.Text(head.gameObject, "Title", "プレイヤー情報", 18f, V5Tokens.TextDim);
            var spacer = V5Ui.Node(head.gameObject, "Spacer");
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1f;

            string nickname = RankingClient.LoadNickname();
            V5GameUi.Text(head.gameObject, "Name",
                string.IsNullOrEmpty(nickname) ? "YOU" : nickname, 18f, V5Tokens.Text,
                TextAlignmentOptions.Right);

            BuildInfoCell(panel, 8f, 52f, "累計", V6SelectViewLogic.FormatScore(V6HistoryLogic.TotalScore(history)));
            BuildInfoCell(panel, 272f, 52f, "プレイ曲", history.Count + " / " + data.AllSongs.Count);
            BuildInfoCell(panel, 8f, 116f, "S以上",
                V6HistoryLogic.CountGradeAtLeastS(history).ToString());
            // 自分の総合順位は既存 API (オンライン) に存在しないので空表示。
            BuildInfoCell(panel, 272f, 116f, "順位", "–位 / –人");
        }

        /// <summary>プレイ情報 (528x220 $k2): 直前のプレイの SCORE と判定内訳。</summary>
        private void BuildPlayInfo(GameObject column, V6SongEntry song)
        {
            var panel = At(V5Ui.Panel(column, "Play Info", new Vector2(528f, 220f), V5Tokens.Key2, 8f), 16f, 216f);

            var head = V5GameUi.Row(panel.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            At(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 36f);
            V5GameUi.Text(head.gameObject, "Title", "プレイ情報", 18f, V5Tokens.TextDim);
            var spacer = V5Ui.Node(head.gameObject, "Spacer");
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1f;
            V5GameUi.Text(head.gameObject, "Score Label", "SCORE", 16f, V5Tokens.TextDim, TextAlignmentOptions.Right);
            V5GameUi.Text(head.gameObject, "Score Value", LastPlayValid(song)
                ? V6SelectViewLogic.FormatScore(GManager.score) : "–", 24f, V5Tokens.Text,
                TextAlignmentOptions.Right, FontStyles.Bold);

            bool valid = LastPlayValid(song);
            BuildJudgeCell(panel, 20f, 52f, "PERFECT", valid ? GManager.perfect.ToString() : "–");
            BuildJudgeCell(panel, 189f, 52f, "GREAT", valid ? GManager.great.ToString() : "–");
            BuildJudgeCell(panel, 358f, 52f, "BAD", valid ? GManager.bad.ToString() : "–");
            BuildJudgeCell(panel, 20f, 140f, "MISS", valid ? GManager.miss.ToString() : "–");
            // HOLD は既存の集計 (GManager / Judge) に成功ホールド数が無いので常に "–"。
            BuildJudgeCell(panel, 189f, 140f, "HOLD", "–");
            BuildJudgeCell(panel, 358f, 140f, "MAX COMBO", valid
                ? V6HistoryLogic.ComboLabel(GManager.maxCombo, song != null ? song.NoteCount : 0)
                : "–");
        }

        /// <summary>
        /// 直前のプレイを実数で出せるか。GManager の play 静的フィールドは
        /// 「最後にプレイした曲」のものなので、選択中の曲がその曲
        /// (GManager.songID) のときだけ出す。次の遷移 (ResetSession) で消える。
        /// </summary>
        private static bool LastPlayValid(V6SongEntry song)
        {
            return song != null && song.SongId == GManager.songID
                && V6HistoryLogic.HasLastPlay(GManager.score, GManager.perfect, GManager.great, GManager.bad, GManager.miss);
        }

        /// <summary>曲ランキング情報 (528x304 $k2): 選択中の曲のランキング 4 行。</summary>
        private void BuildSongRanking(GameObject column, V6SongEntry song)
        {
            var panel = At(V5Ui.Panel(column, "Song Ranking", new Vector2(528f, 304f), V5Tokens.Key2, 8f), 16f, 452f);
            AddScrollCatcher(panel, 1);

            var head = V5GameUi.Row(panel.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            At(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 36f);
            V5GameUi.Text(head.gameObject, "Title", "曲ランキング", 18f, V5Tokens.TextDim);
            var headSpacer = V5Ui.Node(head.gameObject, "Spacer");
            var headSpacerLe = headSpacer.AddComponent<LayoutElement>();
            headSpacerLe.flexibleWidth = 1f;
            if (song != null)
            {
                var songChip = V5GameUi.Row(head.gameObject, "Song", 6f, TextAnchor.MiddleCenter);
                var thumb = At(V5Ui.Panel(songChip.gameObject, "Thumb", new Vector2(40f, 24f), V5Tokens.Key3, 4f), 0f, 0f);
                SetJacket(thumb, song);
                V5GameUi.Text(songChip.gameObject, "Text", song.Title, 18f, V5Tokens.Text);
            }

            List<V6RankingRow> rows = SongRankingRows(song);
            if (rows.Count == 0)
            {
                var empty = V5GameUi.Text(panel.gameObject, "No Data",
                    data.IsLoading ? "ランキングを取得しています…" : "NO DATA", 24f, V5Tokens.TextDim,
                    TextAlignmentOptions.Left);
                At(empty, 24f, 60f, 496f, 44f, new Vector2(0f, 0.5f));
                return;
            }

            int first = songRankingScroll;
            int last = Mathf.Min(rows.Count, first + SongRankingVisibleRows);
            for (int i = first; i < last; i++)
            {
                BuildRankingRow(panel, rows[i], i, 60f + 60f * (i - first));
            }
        }

        /// <summary>
        /// 選択中の曲のランキング行。cloud 曲はオンライン取得結果
        /// (data.RankingFor)、ローカル曲 / 未取得はローカルの自分のベスト 1 行。
        /// </summary>
        private List<V6RankingRow> SongRankingRows(V6SongEntry song)
        {
            IReadOnlyList<V6RankingRow> fetched = data.RankingFor(song);
            if (fetched != null && fetched.Count > 0)
            {
                return new List<V6RankingRow>(fetched);
            }
            return V6RankingLogic.LocalRows(song);
        }

        private void BuildRankingRow(GameObject panel, V6RankingRow row, int index, float y)
        {
            var line = At(V5Ui.Panel(panel, "Score " + (index + 1), new Vector2(496f, 52f),
                row.IsYou ? V5Tokens.Key6 : V5Tokens.Recessed2, 8f), 8f, y);

            var pos = At(V5Ui.Panel(line, "Pos", new Vector2(36f, 36f), V5Tokens.Key4, 18f), 8f, 8f);
            var posText = V5GameUi.Text(pos, "PosText", row.Position.ToString(), 22f,
                row.IsYou ? V5Tokens.Text : V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(posText.GetComponent<RectTransform>(), 36f, 36f);

            var name = V5GameUi.Text(line.gameObject, "Name", row.IsYou ? "YOU" : row.PlayerName, 22f, V5Tokens.Text);
            At(name, 56f, 0f, 160f, 52f, new Vector2(0f, 0.5f));

            var score = V5GameUi.Text(line.gameObject, "Score", V6SelectViewLogic.FormatScore(row.Score), 22f,
                V5Tokens.Text, TextAlignmentOptions.Right);
            At(score, 228f, 0f, 180f, 52f, new Vector2(0f, 0.5f));

            var grade = At(V5Ui.Panel(line, "Grade", new Vector2(56f, 36f), V5Tokens.Key4, 4f), 424f, 8f);
            var gradeText = V5GameUi.Text(grade.gameObject, "GradeText",
                string.IsNullOrEmpty(row.Grade) ? "–" : row.Grade, 22f, V5Tokens.Text, TextAlignmentOptions.Center);
            Stretch(gradeText.GetComponent<RectTransform>(), 56f, 36f);
        }

        /// <summary>プレイヤー情報 / プレイ情報の小セル (label 左 / value 右)。</summary>
        private void BuildInfoCell(GameObject panel, float x, float y, string label, string value)
        {
            var cell = At(V5Ui.Panel(panel, "Stat " + label, new Vector2(248f, 60f), V5Tokens.Key1, 8f), x, y);

            var labelText = V5GameUi.Text(cell.gameObject, "Label", label, 16f, V5Tokens.TextDim);
            At(labelText, 12f, 0f, 100f, 60f, new Vector2(0f, 0.5f));

            var valueText = V5GameUi.Text(cell.gameObject, "Value", value, 24f, V5Tokens.Text,
                TextAlignmentOptions.Right);
            At(valueText, 116f, 0f, 120f, 60f, new Vector2(0f, 0.5f));
        }

        /// <summary>プレイ情報の判定セル (label 上 / value 下)。</summary>
        private void BuildJudgeCell(GameObject panel, float x, float y, string label, string value)
        {
            var cell = At(V5Ui.Panel(panel, "Judge " + label, new Vector2(150f, 80f), V5Tokens.Key1, 8f), x, y);
            var col = V5GameUi.Column(cell, "Inner", 4f, TextAnchor.MiddleCenter);
            Stretch(col.GetComponent<RectTransform>(), 150f, 80f);
            V5GameUi.Text(col.gameObject, "Label", label, 14f, V5Tokens.TextDim, TextAlignmentOptions.Center);
            V5GameUi.Text(col.gameObject, "Value", value, 26f, V5Tokens.Text, TextAlignmentOptions.Center,
                FontStyles.Bold);
        }

        // --- 入力ヘルパ (マウスのみ。キーは統合担当が結線する) ---

        private void ScrollList(int delta)
        {
            int count = V6HistoryLogic.HistoryList(data.AllSongs, sort).Count;
            listScroll = V6SelectViewLogic.ClampScroll(listScroll + delta, count, ListVisibleRows);
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
            V6SongEntry song = SongById(selectedSongId);
            int count = SongRankingRows(song).Count;
            songRankingScroll = V6SelectViewLogic.ClampScroll(songRankingScroll + step, count, SongRankingVisibleRows);
            Rebuild(false);
        }

        private V6SongEntry SongById(int songId)
        {
            IReadOnlyList<V6SongEntry> all = data.AllSongs;
            if (all == null)
            {
                return null;
            }
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].SongId == songId)
                {
                    return all[i];
                }
            }
            return null;
        }

        private static int IndexOfSong(List<V6SongEntry> songs, int songId)
        {
            if (songs == null)
            {
                return -1;
            }
            for (int i = 0; i < songs.Count; i++)
            {
                if (songs[i] != null && songs[i].SongId == songId)
                {
                    return i;
                }
            }
            return -1;
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

        private static GameObject MakeButton(GameObject go, Action onClick)
        {
            return MakeViewButton(go, onClick);
        }

        private static GameObject MakeViewButton(GameObject go, Action onClick)
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
            var catcher = target.AddComponent<V6HistoryScrollCatcher>();
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
    internal sealed class V6HistoryViewDriver : MonoBehaviour
    {
        internal V6HistoryView View;

        private void Update()
        {
            if (View != null)
            {
                View.UpdateScale();
            }
        }
    }

    /// <summary>ホイールスクロールの受け皿。</summary>
    internal sealed class V6HistoryScrollCatcher : MonoBehaviour, IScrollHandler
    {
        internal V6HistoryView View;
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
