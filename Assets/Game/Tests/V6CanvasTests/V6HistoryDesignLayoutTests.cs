using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.UI.V6.History;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// History 画面 (design q9JZcH) の座標を design 実測値に固定する EditMode テスト。
    ///
    /// 2026-11-11 の design 突き合わせで見つかった 3 点:
    /// A. 履歴行スタックは上寄せではなく縦中央 (4 行 → リスト内 y 254/326/398/470)。
    /// B. 行内部が Pos x14 / Title x64 / Score x296 で design (12/60/292) から右へ 2〜4px ずれていた。
    /// C. ヘッダーの Sort チップ 132×44 @(840,26) と Count 右端 1680 が design
    ///    (104×29 @(864,33.5) / 右端 1656) より大きかった。
    /// いずれも design ノード q9JZcH の resolved bounds が正本。
    /// </summary>
    public sealed class V6HistoryDesignLayoutTests
    {
        private GameObject canvasGo;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6HistoryDesignLayoutTests Canvas", typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }
            canvasGo = null;
        }

        // --- データソース (実データのみ / サンプル値は作らない) ---

        private sealed class FakeSource : IV6SelectDataSource
        {
            public readonly List<V6SongEntry> Songs = new List<V6SongEntry>();
            public V6SongEntry SelectedValue;

            public IReadOnlyList<V6SongEntry> AllSongs { get { return Songs; } }
            public IReadOnlyList<V6SongEntry> FilteredSongs { get { return Songs; } }
            public string Query { get { return ""; } set { } }
            public V6SongEntry Selected { get { return SelectedValue; } }
            public bool IsLoading { get { return false; } }
            public bool HasError { get { return false; } }
            public string StatusMessage { get { return ""; } }
            public event System.Action Changed;

            public void SetQuery(string query) { }
            public void Select(int songId) { SelectedValue = Songs.Find(s => s.SongId == songId); }
            public void MoveSelection(int delta) { }
            public void RefreshAsync() { }
            public void CommitReady() { }
            public IReadOnlyList<V6RankingRow> RankingFor(V6SongEntry song)
            {
                return new List<V6RankingRow>();
            }
        }

        private static V6SongEntry Song(int id, string title, int score)
        {
            return new V6SongEntry
            {
                SongId = id,
                Title = title,
                Artist = "稲葉曇",
                Level = 8,
                Bpm = 274.0,
                NoteCount = 268,
                JacketResourcePath = "",
                BestScore = score,
                BestGrade = "C",
            };
        }

        private V6HistoryView CreateView(int playedSongCount)
        {
            var source = new FakeSource();
            for (int i = 1; i <= playedSongCount; i++)
            {
                source.Songs.Add(Song(i, "曲" + i, 10000 * i));
            }
            if (source.Songs.Count > 0)
            {
                source.SelectedValue = source.Songs[0];
            }
            return V6HistoryView.Create((RectTransform)canvasGo.transform,
                null, source, null, null);
        }

        /// <summary>名前一致の RectTransform を深さ優先で探す (最初の一致)。</summary>
        private static RectTransform Find(Transform root, string name)
        {
            var rects = root.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].name == name)
                {
                    return rects[i];
                }
            }

            Assert.Fail("RectTransform '" + name + "' not found");
            return null;
        }

        /// <summary>親の左上を原点とする design 座標 (y_down) に戻す。</summary>
        private static Vector2 DesignTopLeft(RectTransform rt)
        {
            return new Vector2(rt.anchoredPosition.x, -rt.anchoredPosition.y);
        }

        // --- A. 履歴行スタックの縦中央配置 ---

        [Test]
        public void HistoryFirstRowY_CentersShortStacks()
        {
            // design q9JZcH: 4 行は 254/326/398/470 (`(780 − n*72)/2 + 8`)。
            Assert.AreEqual(254f, V6HistoryLogic.HistoryFirstRowY(4), 0.01f, "design の 4 行");
            Assert.AreEqual(326f, V6HistoryLogic.HistoryFirstRowY(2), 0.01f, "2 行");
            Assert.AreEqual(362f, V6HistoryLogic.HistoryFirstRowY(1), 0.01f, "1 行");
            Assert.AreEqual(74f, V6HistoryLogic.HistoryFirstRowY(9), 0.01f, "収まる最大の 9 行");

            for (int n = 1; n <= V6HistoryLogic.HistoryMaxCenteredRows; n++)
            {
                float first = V6HistoryLogic.HistoryFirstRowY(n);
                float bottom = first + (n - 1) * V6HistoryLogic.HistoryRowStep + V6HistoryLogic.HistoryRowH;
                float topGap = first;
                float bottomGap = V6HistoryLogic.ListPanelH - bottom;
                // design の実測は「上余白 254 / 下余白 246」— 行スタックを +8 だけ下げて中央に置く。
                Assert.AreEqual(8f, topGap - bottomGap, 0.01f, "n=" + n + " は design の 8px 下げ中央");
                Assert.GreaterOrEqual(first, 0f, "n=" + n + " はリスト上端より下");
                Assert.LessOrEqual(bottom, V6HistoryLogic.ListPanelH, "n=" + n + " はリスト下端より上");
            }
        }

        [Test]
        public void HistoryFirstRowY_FallsBackToTopWhenOverflowing()
        {
            // 収まらない / 空のときはスクロール前提の上端 RowY0 (48)。
            Assert.AreEqual(V6HistoryLogic.HistoryRowY0, V6HistoryLogic.HistoryFirstRowY(10), 0.01f);
            Assert.AreEqual(V6HistoryLogic.HistoryRowY0, V6HistoryLogic.HistoryFirstRowY(29), 0.01f);
            Assert.AreEqual(V6HistoryLogic.HistoryRowY0, V6HistoryLogic.HistoryFirstRowY(0), 0.01f);
        }

        [Test]
        public void HistoryList_ShortStack_IsVerticallyCentered()
        {
            var view = CreateView(2);
            try
            {
                var first = Find(view.Root, "History 曲2"); // スコア順の先頭
                var second = Find(view.Root, "History 曲1");
                Assert.AreEqual(16f, DesignTopLeft(first).x, 0.01f, "行 x は design 16");
                Assert.AreEqual(V6HistoryLogic.HistoryFirstRowY(2), DesignTopLeft(first).y, 0.01f,
                    "2 行は design の縦中央 (326)");
                Assert.AreEqual(326f + V6HistoryLogic.HistoryRowStep, DesignTopLeft(second).y, 0.01f,
                    "2 行目は 72 ピッチ");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        // --- B. 行内部の座標 ---

        [Test]
        public void HistoryRowInternals_MatchDesign()
        {
            var view = CreateView(1);
            try
            {
                var row = Find(view.Root, "History 曲1");

                var pos = Find(row, "Pos");
                Assert.AreEqual(new Vector2(12f, 14f), DesignTopLeft(pos), "Pos (12,14)");
                Assert.AreEqual(new Vector2(36f, 36f), pos.sizeDelta, "Pos 36×36");

                var title = (RectTransform)Find(row, "Title").transform;
                Assert.AreEqual(new Vector2(60f, 15f), DesignTopLeft(title), "Title (60,15)");
                Assert.AreEqual(new Vector2(220f, 35f), title.sizeDelta, "Title 220×35");
                var titleTmp = title.GetComponent<TextMeshProUGUI>();
                Assert.AreEqual(24f, titleTmp.fontSize, "Title fs24");
                Assert.AreEqual(TextWrappingModes.NoWrap, titleTmp.textWrappingMode, "Title は 1 行");
                // fs24 h35 は行送り 34.75 との余裕が 0.25px しかない。margin 補正で確保する。
                float need = titleTmp.font.faceInfo.lineHeight / titleTmp.font.faceInfo.pointSize * 24f;
                Assert.GreaterOrEqual(title.sizeDelta.y - titleTmp.margin.y - titleTmp.margin.w, need - 0.01f,
                    "Title の実効高が行送り以上");

                var score = (RectTransform)Find(row, "Score").transform;
                Assert.AreEqual(new Vector2(292f, 15f), DesignTopLeft(score), "Score (292,15)");
                Assert.AreEqual(new Vector2(120f, 35f), score.sizeDelta, "Score 120×35");
                Assert.AreEqual(TextAlignmentOptions.Right, score.GetComponent<TextMeshProUGUI>().alignment,
                    "Score は右寄せ");

                var grade = Find(row, "Grade");
                Assert.AreEqual(new Vector2(424f, 14f), DesignTopLeft(grade), "Grade (424,14)");
                Assert.AreEqual(new Vector2(56f, 36f), grade.sizeDelta, "Grade 56×36");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        [Test]
        public void HistoryRowTitle_RealSongTitle_IsDrawn()
        {
            var view = CreateView(1);
            try
            {
                var title = (RectTransform)Find(view.Root, "History 曲1").Find("Title");
                var tmp = title.GetComponent<TextMeshProUGUI>();
                tmp.ForceMeshUpdate(true, true);
                Assert.AreEqual("曲1", tmp.text, "実データの曲名を表示する");
                Assert.Greater(tmp.textInfo.characterCount, 0, "曲名が描画される (0 行回帰なし)");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        // --- C. ヘッダー右 (Sort チップ / Count) ---

        [Test]
        public void HistoryHeader_BackButton_SitsAtDesignTopLeft()
        {
            // design q9JZcH `/wKQlf/SUD2i/zUcsi`: 戻るボタンはヘッダー (0,20) 56×56。
            // 旧実装は V5Ui.Node("Zone Back") + At で包んでいたため、Node 既定の
            // 100×100 の中央にボタンが寄り (22,20) へずれてタイトル (x72) に重なった。
            var view = CreateView(1);
            try
            {
                var back = Find(view.Root, "Back Button");
                Assert.AreEqual("Header", back.parent.name,
                    "戻るボタンは Header 直下に置く (100×100 Node に包まない)");
                Assert.AreEqual(new Vector2(0f, 20f), DesignTopLeft(back), "design (0,20)");
                Assert.AreEqual(new Vector2(56f, 56f), back.sizeDelta, "56×56");
                Assert.LessOrEqual(DesignTopLeft(back).x + back.sizeDelta.x, 72f,
                    "戻るボタンの右端はタイトル (x72) より左");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        [Test]
        public void HistoryHeader_SortChipAndCount_MatchDesign()
        {
            var view = CreateView(2);
            try
            {
                var sort = Find(view.Root, "Sort");
                Assert.AreEqual(new Vector2(864f, 33.5f), DesignTopLeft(sort), "Sort (864,33.5)");
                Assert.AreEqual(new Vector2(104f, 29f), sort.sizeDelta, "Sort 104×29");

                var sortText = (RectTransform)sort.Find("Text");
                Assert.AreEqual(new Vector2(12f, 0f), DesignTopLeft(sortText), "Sort Text (12,0)");
                Assert.AreEqual(new Vector2(80f, 29f), sortText.sizeDelta, "Sort Text 80×29");
                Assert.AreEqual(TextAlignmentOptions.Left, sortText.GetComponent<TextMeshProUGUI>().alignment,
                    "Sort Text 左揃え");

                var count = Find(view.Root, "Count");
                Assert.AreEqual(280f, count.sizeDelta.x, 0.01f, "Count w280");
                float rightEdge = count.anchoredPosition.x + count.sizeDelta.x;
                Assert.AreEqual(1656f, rightEdge, 0.01f, "Count 右端はヘッダー内 1656 (= ページ 1680)");
                Assert.AreEqual(48f, -count.anchoredPosition.y, 0.01f, "Count はヘッダー内で縦中央");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        [Test]
        public void HistoryHeader_CountLabel_IsRecordedSongCount()
        {
            Assert.AreEqual("記録 2曲", V6HistoryLogic.RecordedCountLabel(2));
            var view = CreateView(2);
            try
            {
                var count = Find(view.Root, "Count");
                Assert.AreEqual("記録 2曲", count.GetComponent<TextMeshProUGUI>().text);
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        // --- D. 中央パネルの曲名 (design f54HAH/DcLR3: Meta 内 (0,9) 512×50 fs48) ---

        [Test]
        public void HistoryCenterMeta_Title_MatchesDesign()
        {
            // 旧実装は sizeDelta だけ変えて既定アンカー (中央) のままで、design より
            // 13px 下がり Artist Row (y60) に食い込んでいた (r4 実機で重なって見えた)。
            var view = CreateView(1);
            try
            {
                var meta = Find(view.Root, "Meta");
                var title = (RectTransform)meta.Find("Title");
                Assert.AreEqual(new Vector2(0f, 9f), DesignTopLeft(title), "Title (0,9)");
                Assert.AreEqual(new Vector2(512f, 50f), title.sizeDelta, "Title 512×50");
                Assert.AreEqual(48f, title.GetComponent<TextMeshProUGUI>().fontSize, "Title fs48");

                var artistRow = (RectTransform)meta.Find("Artist Row");
                Assert.AreEqual(60f, DesignTopLeft(artistRow).y, 0.01f, "Artist Row は y60");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        [Test]
        public void SelectCenterMeta_Title_MatchesDesign()
        {
            var source = new FakeSource();
            source.Songs.Add(Song(1, "まにまに", 10000));
            source.SelectedValue = source.Songs[0];

            var view = V6SelectView.Create((RectTransform)canvasGo.transform, source, null, null, null);
            try
            {
                var meta = Find(view.Root, "Meta");
                var title = (RectTransform)meta.Find("Title");
                Assert.AreEqual(new Vector2(0f, 9f), DesignTopLeft(title), "Title (0,9)");
                Assert.AreEqual(new Vector2(512f, 50f), title.sizeDelta, "Title 512×50");
                Assert.AreEqual(60f, DesignTopLeft((RectTransform)meta.Find("Artist Row")).y, 0.01f,
                    "Artist Row は y60");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }
    }
}
