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
    /// </summary>
    public sealed class V6HistoryView
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

        private readonly Dictionary<string, Sprite> jacketCache = new Dictionary<string, Sprite>();

        private GameObject rootGo;
        private RectTransform frame;

        private bool placed;
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

            // 画面全体を v6 背景 (契約 §3 bg #1B1B1B) で敷き、レガシーのクリーム色
            // 背景が透けないようにする。レターボックス枠は作らない (フレーム外も同色)。
            var bg = rootGo.AddComponent<Image>();
            bg.color = V5Tokens.Bg;
            bg.raycastTarget = true;

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
            BuildHeader(headerZone, history.Count);
            BuildFooter(footerZone);
            BuildList(mainZone, history, selectIndex);
            BuildCenter(mainZone, selected);
            BuildRight(mainZone, selected);
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

        private void BuildHeader(GameObject headerZone, int playedCount)
        {
            // 戻るボタン 56×56 (#3A3A3A r8) + Key fs16 (design: Back button / Key 'Esc' fs16)。
            // header 直下に design (0,20) で置く。V5Ui.Node + At で包むと Node の既定
            // 100×100 の中央にボタンが寄り、右へ 20px / 下へ 20px ずれてタイトルに
            // 重なる (r19 実機 q9JZcH 比較で判明)。確定キーはキー設定の正本から取る。
            var back = MakeButton(V5Ui.Panel(headerZone, "Back Button", new Vector2(56f, 56f), V5Tokens.Key4, 8f),
                () => TriggerBack());
            At(back, 0f, 20f);
            V6Controls.FillBackButton(back, ActionKeyHint.Primary(KeyActions.KeyAction.Back));

            var title = V5GameUi.Text(headerZone, "Title", "HISTORY", 38f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            At(title, 72f, 20f, 400f, 56f);

            // 右上: 並び替えチップ (design q9JZcH/Y8lcc: ヘッダー内 (864,33.5) 104×29 r8) + 件数
            var sortChip = MakeButton(V5Ui.Panel(headerZone, "Sort",
                new Vector2(V6HistoryLogic.HeaderSortW, V6HistoryLogic.HeaderSortH), V5Tokens.Key4, 8f),
                ToggleSort);
            At(sortChip, V6HistoryLogic.HeaderSortX, V6HistoryLogic.HeaderSortY);
            var sortText = V5GameUi.Text(sortChip, "Text", V6HistoryLogic.SortLabel(sort), 20f, V5Tokens.TextMuted,
                TextAlignmentOptions.Left);
            At(sortText, V6HistoryLogic.HeaderSortTextX, 0f, V6HistoryLogic.HeaderSortTextW,
                V6HistoryLogic.HeaderSortH);
            // design の文言 "スコア順" (4 文字 × fs20 = 80) がテキスト枠ちょうど。
            // 実装のもう一方のラベル "カタログ順" (5 文字 = 100) は枠を超えるため、
            // design の枠は変えずに最小 16px まで自動縮小してチップ内 (右 padding 12) に収める。
            sortText.textWrappingMode = TextWrappingModes.NoWrap;
            sortText.enableAutoSizing = true;
            sortText.fontSizeMax = 20f;
            sortText.fontSizeMin = 16f;

            // design: Count fs24、右端はヘッダー内 1656 (= ページ 1680、情報ゾーン右 padding 24 の内側)
            var count = V5GameUi.Text(headerZone, "Count", V6HistoryLogic.RecordedCountLabel(playedCount), 24f,
                V5Tokens.TextMuted, TextAlignmentOptions.Right);
            At(count, V6HistoryLogic.HeaderCountX, V6HistoryLogic.HeaderCountY, V6HistoryLogic.HeaderCountW,
                V6HistoryLogic.HeaderCountH, new Vector2(0f, 0.5f));
        }

        // --- フッター (共通フレームの Footer ゾーン: design (24,876) 1680×96) ---

        private void BuildFooter(GameObject footerZone)
        {
            // design (rw7zx): キー操作ゾーン (0,30)、Legend 間 gap16、Legend 内は
            // KeyCap (高36 / pad[0,10] / fs22) + gap10 + Label fs22 $text-muted。
            // design の凡例はこの 3 つだけ (q9JZcH フッター: ↑↓ 選択 / Space 決定 / Esc 戻る)。
            // S (並べ替え) は実装済みのキー操作だが design に凡例が無いため出さない。
            // (design の「凡例 (左下)」ゾーン (445,30) 240×36 は中身が空。)
            BuildKeyLegend(footerZone, new[]
            {
                new[] { "↑↓", "選択" },
                new[] { "Space", "決定" },
                new[] { "Esc", "戻る" }
            });

            // design (q9JZcH): 右下は「この曲へ」(567,20) 273×56 のみ。
            // 「設定」ゾーン (358,20) 197×56 は design で空のプレースホルダ (子ノード無し / fill 無し)
            // なのでボタンは描かない。O キーの設定遷移 (TriggerOpenSettings) はキーボード操作として残す。
            // icon は design の corner-down-left (旧: music で別アイコンだった)。
            GameObject gotoBtn = V6Controls.ButtonPrimary(footerZone, "この曲へ",
                new Vector2(V6HistoryLogic.FooterGotoButtonX, V6HistoryLogic.FooterButtonY),
                new Vector2(273f, V6HistoryLogic.FooterButtonH), "corner-down-left", "Space");
            ConfigureButton(gotoBtn, TriggerGotoSong);
        }

        /// <summary>
        /// フッター左下のキー凡例。design (q9JZcH Zone · キー操作 / Legend):
        /// zone gap 24、Legend 内 gap 8、KeyCap は h36 pad[0,8] fs22 $text-muted、Label fs22。
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

        // --- 左列: 履歴リスト (x0 w560 #131313CC r16) ---

        private void BuildList(GameObject mainZone, List<V6SongEntry> history, int selectIndex)
        {
            var list = At(V5Ui.Panel(mainZone, "History List", new Vector2(ListW, ListH),
                FromHex("#131313CC"), 16f), 0f, 0f);
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
                var emptyIcon = V6Icons.Create(col.gameObject, "music", 32f, V5Tokens.TextDim, "Icon");
                V5GameUi.Text(col.gameObject, "Title", "プレイ履歴がありません", 24f, V5Tokens.Text,
                    TextAlignmentOptions.Center);
                V5GameUi.Text(col.gameObject, "Hint", "曲をプレイすると記録がここに残ります", 20f,
                    V5Tokens.TextMuted, TextAlignmentOptions.Center);
            }
            else
            {
                int first = listScroll;
                int last = Mathf.Min(history.Count, first + ListVisibleRows);
                // design q9JZcH: 全行が収まるときはスタックを縦中央に置く (4 行 → 254)。
                // 収まらない / スクロール中のときは上端 RowY0 基準を保つ。
                float y0 = first == 0 ? V6HistoryLogic.HistoryFirstRowY(history.Count) : RowY0;
                for (int i = first; i < last; i++)
                {
                    BuildHistoryRow(rows, history[i], i + 1, i == selectIndex, y0 + RowStep * (i - first));
                }
            }
            BuildScrollButton(list, "Scroll Up", 0f, "chevron-up", -1);
            BuildScrollButton(list, "Scroll Down", 732f, "chevron-down", 1);
        }

        private void BuildHistoryRow(GameObject parent, V6SongEntry song, int pos, bool selected, float y)
        {
            var row = At(V5Ui.Panel(parent, "History " + song.Title, new Vector2(496f, RowH),
                selected ? V5Tokens.Key6 : V5Tokens.Recessed2, 8f), RowX, y);
            var button = MakeViewButton(row.gameObject, null).GetComponent<Button>();
            int songId = song.SongId;
            button.onClick.AddListener(() => SelectSong(songId));

            // design q9JZcH/F676m/BuztA: Pos (12,14) 36×36 r18
            var posBadge = At(V5Ui.Panel(row, "Pos", new Vector2(36f, 36f), V5Tokens.Key4, 18f),
                V6HistoryLogic.HistoryPosX, V6HistoryLogic.HistoryPosY);
            var posText = V5GameUi.Text(posBadge.gameObject, "PosText", pos.ToString(), 22f,
                pos == 1 ? V5Tokens.Text : V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(posText.GetComponent<RectTransform>(), 36f, 36f);

            // design: Title (60,15) 220×35 fs24 左揃え 1 行
            var title = V5GameUi.Text(row.gameObject, "Title", song.Title, 24f, V5Tokens.Text);
            At(title, V6HistoryLogic.HistoryTitleX, V6HistoryLogic.HistoryTitleY,
                V6HistoryLogic.HistoryTitleW, V6HistoryLogic.HistoryInnerH);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
            // fs24 の行送りは 34.75 で design h35 との余裕が 0.25px しかないため、
            // フォント差で 0 行にならないよう実効高を margin で確保する。
            V5Ui.FitEllipsisToLineHeight(title, V6HistoryLogic.HistoryInnerH);

            // design (BuztA 等) の Score は区切りなしの生の数値 ("987410")。
            // design: Score (292,15) 120×35 fs24 右揃え
            var score = V5GameUi.Text(row.gameObject, "Score", song.BestScore.ToString(), 24f,
                V5Tokens.Text, TextAlignmentOptions.Right);
            At(score, V6HistoryLogic.HistoryScoreX, V6HistoryLogic.HistoryScoreY,
                V6HistoryLogic.HistoryScoreW, V6HistoryLogic.HistoryInnerH);
            score.textWrappingMode = TextWrappingModes.NoWrap;
            score.overflowMode = TextOverflowModes.Ellipsis;
            V5Ui.FitEllipsisToLineHeight(score, V6HistoryLogic.HistoryInnerH);

            // design: Grade (424,14) 56×36 r4
            var grade = At(V5Ui.Panel(row, "Grade", new Vector2(56f, 36f), V5Tokens.Key4, 4f),
                V6HistoryLogic.HistoryGradeX, V6HistoryLogic.HistoryGradeY);
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

        // --- 中央列: 選択中の曲 (dRztp と同じ構造) ---

        private void BuildCenter(GameObject mainZone, V6SongEntry song)
        {
            var panel = At(V5Ui.Panel(mainZone, "Selected Song Panel", new Vector2(CenterW, ListH),
                V5Tokens.Key2, 16f), CenterX, 0f);
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
                // design q9JZcH 中央 選択中の曲/f54HAH/DcLR3: Title は Meta 内 (0,9) 512×50。
                // sizeDelta だけ変えると既定アンカー (中央) のままで 13px 下がり、
                // Artist Row (y60) に食い込む (r4 実機で重なって見えた)。
                At(title, 0f, 9f, 512f, 50f);
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

            // Controls (skip-back / play / skip-forward): 実在する操作だけに結線する
            // (V6SelectView と同じ規約。プレビュー再生のような新しい操作は作らない)。
            // skip は履歴リストの曲送り (MoveSelection)、play はフッターの
            // 「この曲へ」と同じ経路 (TriggerGotoSong)。
            var controls = V5Ui.Node(panel, "Controls");
            At(controls.GetComponent<RectTransform>(), 12f, 668f, 512f, 88f);
            // design (dRztp Controls): gap20 で中央寄せ → 56+20+72+20+56 = 224、開始 x = (512−224)/2 = 144
            BuildControlButton(controls, "skip-back", 144f, 56f, 28f, 32f, () => MoveSelection(-1));
            BuildControlButton(controls, "play", 210f, 72f, 36f, 36f, TriggerGotoSong);
            BuildControlButton(controls, "skip-forward", 292f, 56f, 28f, 32f, () => MoveSelection(1));
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

        /// <summary>中央パネルの Controls 行のボタン 1 個分。実操作へ結線する (onClick null は作らない)。</summary>
        private void BuildControlButton(GameObject controls, string name, float x, float size, float radius,
            float iconSize, Action onClick)
        {
            // design (dRztp Controls): skip 56×56 r28 $k4 / play 72×72 r36 #EDEDED、
            // icon は skip 系 32 $text-muted、play 36 #1B1B1B。
            bool isPlay = name == "play";
            GameObject btn = V6Controls.IconButton(controls, name,
                new Vector2(x, (88f - size) * 0.5f), new Vector2(size, size),
                isPlay ? V5Tokens.ButtonPrimaryBg : V5Tokens.Key4, radius,
                iconSize, isPlay ? V5Tokens.ButtonPrimaryLabel : V5Tokens.TextMuted);
            ConfigureButton(btn, onClick);
        }

        // --- 右列: プレイヤー情報 / プレイ情報 / 曲ランキング情報 ---

        private void BuildRight(GameObject mainZone, V6SongEntry song)
        {
            // Main ゾーン内 (design: 記録情報 (列5-6) = Main 内 (1120,0) 560×780)
            GameObject column = V5Ui.Node(mainZone, "Record Column");
            At(column, RightX, 0f, ListW, ListH);

            BuildPlayerInfo(column, song);
            BuildPlayInfo(column, song);
            BuildSongRanking(column, song);
        }

        /// <summary>プレイヤー情報 (528x176 $k2): 累計 / プレイ済み / S以上 (Issue #239 で順位セルは削除)。</summary>
        private void BuildPlayerInfo(GameObject column, V6SongEntry song)
        {
            List<V6SongEntry> history = V6HistoryLogic.HistoryList(data.AllSongs, sort);
            var panel = At(V5Ui.Panel(column, "Player Info", new Vector2(528f, 176f), V5Tokens.Key2, 8f), 16f, 24f);

            var head = V5GameUi.Row(panel.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            At(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 36f);
            // 子の幅は TMP の preferredWidth に従わせる。childControlWidth=false だと
            // 既定の矩形幅 (200) のままヘッダーの右外へはみ出す。
            head.childControlWidth = true;
            V5GameUi.Text(head.gameObject, "Title", "プレイヤー情報", 18f, V5Tokens.TextDim);
            var spacer = V5Ui.Node(head.gameObject, "Spacer");
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1f;

            string nickname = RankingClient.LoadNickname();
            V5GameUi.Text(head.gameObject, "Name",
                string.IsNullOrEmpty(nickname) ? "YOU" : nickname, 22f, V5Tokens.Text,
                TextAlignmentOptions.Right);

            BuildInfoCell(panel, 8f, 52f, "累計", V6SelectViewLogic.FormatScore(V6HistoryLogic.TotalScore(history)));
            // design (YuQgv value "4 / 26") は「記録のある曲数 / 全曲数」。
            BuildInfoCell(panel, 272f, 52f, "プレイ曲", history.Count + " / " + data.AllSongs.Count);
            BuildInfoCell(panel, 8f, 116f, "S以上", V6HistoryLogic.CountGradeAtLeastS(history).ToString());
            // design (xnuYO) は 4 番目のセル「順位」("–位 / –人") を持つ。
            // 総合順位を返す API はまだ無いので、design と同じく未確定表示 "–位 / –人" を出す。
            // (以前はセルごと削除していたが、それは design 差分だったので戻した。)
            BuildInfoCell(panel, 272f, 116f, "順位", "–位 / –人");
        }

        /// <summary>プレイ情報 (528x220 $k2): 直前のプレイの SCORE と判定内訳。</summary>
        private void BuildPlayInfo(GameObject column, V6SongEntry song)
        {
            var panel = At(V5Ui.Panel(column, "Play Info", new Vector2(528f, 220f), V5Tokens.Key2, 8f), 16f, 216f);

            var head = V5GameUi.Row(panel.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            At(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 36f);
            // 子の幅は TMP の preferredWidth に従わせる (SCORE ラベル + 値が右へはみ出さない)。
            head.childControlWidth = true;
            V5GameUi.Text(head.gameObject, "Title", "プレイ情報", 18f, V5Tokens.TextDim);
            var spacer = V5Ui.Node(head.gameObject, "Spacer");
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1f;
            V5GameUi.Text(head.gameObject, "Score Label", "SCORE", 18f, V5Tokens.TextDim, TextAlignmentOptions.Right);
            V5GameUi.Text(head.gameObject, "Score Value", LastPlayValid(song)
                ? V6SelectViewLogic.FormatScore(GManager.score) : "–", 24f, V5Tokens.Text,
                TextAlignmentOptions.Right, FontStyles.Bold);

            bool valid = LastPlayValid(song);
            BuildJudgeCell(panel, 20f, 52f, "PERFECT", valid ? GManager.perfect.ToString() : "–");
            BuildJudgeCell(panel, 189f, 52f, "GREAT", valid ? GManager.great.ToString() : "–");
            BuildJudgeCell(panel, 358f, 52f, "BAD", valid ? GManager.bad.ToString() : "–");
            BuildJudgeCell(panel, 20f, 140f, "MISS", valid ? GManager.miss.ToString() : "–");
            // design (d0bZbJ) は 5 番目のセル「HOLD」("0") を持つ。現行エンジンの譜面は
            // すべて通常ノーツ (chart の type は 1 のみ) で HOLD の集計は存在しないため、
            // プレイが有効なときの真の値は常に 0。design と同じ "0" を出す。
            BuildJudgeCell(panel, 189f, 140f, "HOLD", valid ? "0" : "–");
            // design: MAX COMBO の value のみ fs24
            BuildJudgeCell(panel, 358f, 140f, "MAX COMBO",
                valid ? V6HistoryLogic.ComboLabel(GManager.maxCombo, song != null ? song.NoteCount : 0) : "–",
                24f);
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
            // 子の幅は TMP の preferredWidth に従わせる (曲名チップが右端を越えない)。
            head.childControlWidth = true;
            V5GameUi.Text(head.gameObject, "Title", "曲ランキング", 18f, V5Tokens.TextDim);
            var headSpacer = V5Ui.Node(head.gameObject, "Spacer");
            var headSpacerLe = headSpacer.AddComponent<LayoutElement>();
            headSpacerLe.flexibleWidth = 1f;
            if (song != null)
            {
                var songChip = V5GameUi.Row(head.gameObject, "Song", 6f, TextAnchor.MiddleCenter);
                // 親 Head は childControlWidth=true。Image 単体は preferredWidth を持たないため、
                // 40x24 を LayoutElement で明示しないとサムネイルが幅 0 に潰れる。
                songChip.childControlWidth = true;
                songChip.childControlHeight = false;
                var thumb = At(V5Ui.Panel(songChip.gameObject, "Thumb", new Vector2(40f, 24f), V5Tokens.Key3, 4f), 0f, 0f);
                var thumbLe = thumb.AddComponent<LayoutElement>();
                thumbLe.preferredWidth = 40f;
                thumbLe.preferredHeight = 24f;
                SetJacket(thumb, song);
                V5GameUi.Text(songChip.gameObject, "Text", song.Title, 18f, V5Tokens.Text);
            }

            List<V6RankingRow> rows = SongRankingRows(song);
            if (rows.Count == 0)
            {
                // 空 / 取得中 / 失敗 / 対象外を区別して出す (Issue #239)。
                // cloud 曲はデータソースの取得状態、ローカル曲は記録の有無のみ。
                string message = V6RankingLogic.SongRankingEmptyMessage(
                    song, data.IsLoading, data.HasError, data.StatusMessage);
                var empty = V5GameUi.Text(panel.gameObject, "No Data", message,
                    24f, V5Tokens.TextDim, TextAlignmentOptions.Left);
                At(empty, 24f, 60f, 496f, 44f, new Vector2(0f, 0.5f));
                return;
            }

            int first = songRankingScroll;
            int last = Mathf.Min(rows.Count, first + SongRankingVisibleRows);
            for (int i = first; i < last; i++)
            {
                BuildRankingRow(panel, rows[i], i - first);
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

        /// <summary>
        /// 選択中の曲のランキング行。行は共通部品 V6Lists.RankRow (design 実測:
        /// Pos 36×36 / Name w160 / Value w180 右寄せ / Grade 56×36。正本 Hbxqf/T8C1Hu、
        /// 契約 §5 の w140 は Select のランキング列の値) を使い回し、寸法は design
        /// (曲ランキング情報: Score·k = (8, 60+60k) 496×52) をそのまま使う。
        /// </summary>
        private void BuildRankingRow(GameObject panel, V6RankingRow row, int visibleIndex)
        {
            V6Lists.RankRow(
                panel,
                row.Position.ToString(),
                row.IsYou ? "YOU" : row.PlayerName,
                V6SelectViewLogic.FormatScore(row.Score),
                string.IsNullOrEmpty(row.Grade) ? "–" : row.Grade,
                new Vector2(V6HistoryLogic.SongRankRowX, V6HistoryLogic.SongRankRowTop(visibleIndex)),
                new Vector2(V6HistoryLogic.SongRankRowW, V6HistoryLogic.SongRankRowH),
                row.IsYou);
        }

        /// <summary>プレイヤー情報 / プレイ情報の小セル (label 左 / value 右)。</summary>
        private void BuildInfoCell(GameObject panel, float x, float y, string label, string value)
        {
            var cell = At(V5Ui.Panel(panel, "Stat " + label, new Vector2(248f, 60f), V5Tokens.Key1, 8f), x, y);

            var labelText = V5GameUi.Text(cell.gameObject, "Label", label, 18f, V5Tokens.TextDim);
            At(labelText, 12f, 0f, 100f, 60f, new Vector2(0f, 0.5f));

            var valueText = V5GameUi.Text(cell.gameObject, "Value", value, 24f, V5Tokens.Text,
                TextAlignmentOptions.Right);
            At(valueText, 116f, 0f, 120f, 60f, new Vector2(0f, 0.5f));
        }

        /// <summary>
        /// プレイ情報の判定セル (design: 150×80 $k1、label fs18 上 / value fs32 下。
        /// MAX COMBO の value のみ fs24)。
        /// </summary>
        private void BuildJudgeCell(GameObject panel, float x, float y, string label, string value,
            float valueFs = 32f)
        {
            var cell = At(V5Ui.Panel(panel, "Judge " + label, new Vector2(150f, 80f), V5Tokens.Key1, 8f), x, y);
            var col = V5GameUi.Column(cell, "Inner", 2f, TextAnchor.MiddleCenter);
            Stretch(col.GetComponent<RectTransform>(), 150f, 80f);
            V5GameUi.Text(col.gameObject, "Label", label, 18f, V5Tokens.TextDim, TextAlignmentOptions.Center);
            V5GameUi.Text(col.gameObject, "Value", value, valueFs, V5Tokens.Text, TextAlignmentOptions.Center);
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
            return At((RectTransform)node.transform, x, y, new Vector2(w, h));
        }

        private static GameObject At(RectTransform rt, float x, float y, Vector2 size)
        {
            V6Layout.Place(rt, new Vector2(x, y), size);
            return rt.gameObject;
        }

        private static GameObject At(Component node, float x, float y, float w, float h, Vector2 pivot)
        {
            // pivot を変えても design 上の左上 (x, y) を維持する。
            // pivot は矩形の上端から (1 − pivot.y) * h 下・左端から p.x * w 右にあるので、
            // その点の design 座標を V6Layout.ToLocal で RectTransform 座標へ変換する。
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
                View.UpdatePlacement();
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
