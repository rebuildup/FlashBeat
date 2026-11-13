using System.Collections.Generic;
using FlashBeat.UI.V6.Ranking;
using FlashBeat.UI.V6.Select;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #415 (第2段階): Ranking 画面が選択変更・データ更新のたびに
    /// Header/Footer/Main の骨格と可視行を破棄・再生成せず、スロット単位で
    /// 再利用することを固定する。
    /// </summary>
    public class V6RankingViewReuseTests
    {
        private sealed class FakeDataSource : IV6SelectDataSource
        {
            public readonly List<V6SongEntry> Songs = new List<V6SongEntry>();
            public V6SongEntry SelectedValue;
            public readonly List<V6RankingRow> RankRows = new List<V6RankingRow>();
            private string query = "";

            public IReadOnlyList<V6SongEntry> AllSongs { get { return Songs; } }
            public IReadOnlyList<V6SongEntry> FilteredSongs { get { return Songs; } }
            public string Query { get { return query; } set { query = value; Changed?.Invoke(); } }
            public V6SongEntry Selected { get { return SelectedValue; } }
            public bool IsLoading { get { return false; } }
            public bool HasError { get { return false; } }
            public string StatusMessage { get { return ""; } }
            public event System.Action Changed;

            public void RaiseChanged()
            {
                Changed?.Invoke();
            }

            public void SetQuery(string value) { query = value; Changed?.Invoke(); }
            public void Select(int songId)
            {
                SelectedValue = Songs.Find(s => s.SongId == songId);
                Changed?.Invoke();
            }
            public void MoveSelection(int delta) { }
            public void RefreshAsync() { }
            public void CommitReady() { }
            public IReadOnlyList<V6RankingRow> RankingFor(V6SongEntry song) { return RankRows; }
        }

        private GameObject parent;
        private FakeDataSource data;
        private V6RankingView view;

        [SetUp]
        public void SetUp()
        {
            parent = new GameObject("Parent", typeof(RectTransform));
            data = new FakeDataSource();
            for (int i = 1; i <= 12; i++)
            {
                data.Songs.Add(new V6SongEntry
                {
                    SongId = i,
                    Title = "Song " + i,
                    Artist = "Artist " + i,
                    Level = 10,
                    BestGrade = "A",
                    // Online セグメントと data.RankingFor の行は cloud 曲だけに出る
                    // (V6RankingLogic.ShowOnlineSegment)。
                    IsCloud = true,
                });
            }

            data.RankRows.Add(new V6RankingRow { Position = 1, PlayerName = "alice", Score = 100000, Grade = "A" });
            data.RankRows.Add(new V6RankingRow { Position = 2, PlayerName = "bob", Score = 90000, Grade = "B" });
            data.SelectedValue = data.Songs[0];

            view = V6RankingView.Create(parent.GetComponent<RectTransform>(), () => { }, data);
        }

        [TearDown]
        public void TearDown()
        {
            if (view != null)
            {
                view.Dispose();
            }

            if (parent != null)
            {
                Object.DestroyImmediate(parent);
            }
        }

        private static List<GameObject> Children(Transform parent)
        {
            var list = new List<GameObject>();
            if (parent == null)
            {
                return list;
            }
            for (int i = 0; i < parent.childCount; i++)
            {
                list.Add(parent.GetChild(i).gameObject);
            }
            return list;
        }

        private Transform Find(Transform root, string path)
        {
            return root == null ? null : root.Find(path);
        }

        private List<GameObject> SongRows()
        {
            Transform rows = Find(view.Root, "Main");
            rows = Find(rows, "Song List");
            rows = Find(rows, "Rows");
            return Children(rows);
        }

        private List<GameObject> RankRows()
        {
            Transform panel = Find(view.Root, "Main");
            panel = Find(panel, "Score Ranking");
            // RankRow だけを拾う (Head / No Data / Retry は除く)。
            var rows = new List<GameObject>();
            if (panel == null)
            {
                return rows;
            }
            for (int i = 0; i < panel.childCount; i++)
            {
                if (panel.GetChild(i).name.StartsWith("RankRow"))
                {
                    rows.Add(panel.GetChild(i).gameObject);
                }
            }
            return rows;
        }

        private GameObject Scaffold(string path)
        {
            Transform t = Find(view.Root, path);
            return t != null ? t.gameObject : null;
        }

        [Test]
        public void SelectionChange_KeepsScaffoldAndReusesRows()
        {
            Assert.AreEqual(1, view.RebuildCountForAudit, "初回構築が Rebuild として数えられていない");

            var songRowsBefore = SongRows();
            var rankRowsBefore = RankRows();
            Assert.Greater(songRowsBefore.Count, 0, "曲リスト行が生成されていない");
            Assert.Greater(rankRowsBefore.Count, 0, "ランキング行が生成されていない");
            GameObject headerThumb = Scaffold("Header/Thumb");
            GameObject footerGoto = Scaffold("Footer/Button この曲へ");
            GameObject playersPanel = Scaffold("Main/Players");
            Assert.IsNotNull(headerThumb);
            Assert.IsNotNull(footerGoto);
            Assert.IsNotNull(playersPanel);

            data.Select(2);

            var songRowsAfter = SongRows();
            var rankRowsAfter = RankRows();
            Assert.AreEqual(songRowsBefore.Count, songRowsAfter.Count);
            for (int i = 0; i < songRowsBefore.Count; i++)
            {
                Assert.AreSame(songRowsBefore[i], songRowsAfter[i],
                    "slot " + i + " の曲リスト行が再生成されている (Issue #415)");
            }
            Assert.AreEqual(rankRowsBefore.Count, rankRowsAfter.Count);
            for (int i = 0; i < rankRowsBefore.Count; i++)
            {
                Assert.AreSame(rankRowsBefore[i], rankRowsAfter[i],
                    "slot " + i + " のランキング行が再生成されている (Issue #415)");
            }

            Assert.AreSame(headerThumb, Scaffold("Header/Thumb"), "ヘッダーが再生成されている");
            Assert.AreSame(footerGoto, Scaffold("Footer/Button この曲へ"), "フッターが再生成されている");
            Assert.AreSame(playersPanel, Scaffold("Main/Players"), "PLAYERS パネルが再生成されている");

            Assert.AreEqual(1, view.RebuildCountForAudit,
                "骨格が揃っているのに画面全体を作り直している");
            Assert.Greater(view.IncrementalUpdateCountForAudit, 0,
                "差分更新が計測されていない");
        }

        [Test]
        public void RankingDataChange_ReusesSameRankRowObjects()
        {
            var first = RankRows();
            Assert.AreEqual(2, first.Count);

            // ランキング再取得の再現: 同じ行数で中身だけ変えて Changed を発火する。
            data.RankRows[0].PlayerName = "alice2";
            data.RankRows[0].Score = 999999;
            data.RaiseChanged();

            var second = RankRows();
            Assert.AreEqual(2, second.Count);
            for (int i = 0; i < 2; i++)
            {
                Assert.AreSame(first[i], second[i], "Refresh でランキング行 " + i + " が再生成されている");
            }

            // 表示値も in-place 更新されていること (行同一性だけでは更新漏れを検出できない)。
            var nameTmp = second[0].transform.Find("Name")?.GetComponent<TMPro.TextMeshProUGUI>();
            var valueTmp = second[0].transform.Find("Value")?.GetComponent<TMPro.TextMeshProUGUI>();
            Assert.IsNotNull(nameTmp, "RankRow に Name テキストが無い");
            Assert.IsNotNull(valueTmp, "RankRow に Value テキストが無い");
            Assert.AreEqual("alice2", nameTmp.text, "名前の表示値が更新されていない");
            Assert.AreEqual(
                FlashBeat.UI.V6.Select.View.V6SelectViewLogic.FormatScore(999999),
                valueTmp.text, "スコアの表示値が更新されていない");

            Assert.AreEqual(1, view.RebuildCountForAudit);
        }
    }
}
