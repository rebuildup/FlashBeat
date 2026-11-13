using System.Collections.Generic;
using FlashBeat.UI.V6.History;
using FlashBeat.UI.V6.Select;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #415 (第2段階): History 画面が選択変更・再 Refresh のたびに
    /// Header/Footer/Main の骨格と可視行を破棄・再生成せず、スロット単位で
    /// 再利用することを固定する。
    ///
    /// 検証するのは「同じ選択変更でヘッダー/一覧/右カラムの静的要素が
    /// 破棄・再生成されない」こと (Issue #415 受入条件) と、
    /// 行 GameObject が同一実体のまま中身だけ更新されること。
    /// </summary>
    public class V6HistoryViewReuseTests
    {
        private sealed class FakeDataSource : IV6SelectDataSource
        {
            public readonly List<V6SongEntry> Songs = new List<V6SongEntry>();
            public V6SongEntry SelectedValue;
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
            public IReadOnlyList<V6RankingRow> RankingFor(V6SongEntry song) { return new List<V6RankingRow>(); }
        }

        private GameObject parent;
        private FakeDataSource data;
        private V6HistoryView view;

        [SetUp]
        public void SetUp()
        {
            parent = new GameObject("Parent", typeof(RectTransform));
            data = new FakeDataSource();
            // HistoryList は BestScore > 0 の曲だけを出すので、全曲に記録を持たせる。
            for (int i = 1; i <= 4; i++)
            {
                data.Songs.Add(new V6SongEntry
                {
                    SongId = i,
                    Title = "Song " + i,
                    Artist = "Artist " + i,
                    Level = 10,
                    BestScore = 100000 * i,
                    BestGrade = "A",
                    NoteCount = 100,
                });
            }

            view = V6HistoryView.Create(parent.GetComponent<RectTransform>(), () => { }, data);
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

        private List<GameObject> HistoryRows()
        {
            Transform rows = Find(view.Root, "Main");
            rows = Find(rows, "History List");
            rows = Find(rows, "Rows");
            return Children(rows);
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

            var rowsBefore = HistoryRows();
            Assert.Greater(rowsBefore.Count, 0, "履歴行が生成されていない");
            GameObject headerSort = Scaffold("Header/Sort");
            GameObject footerGoto = Scaffold("Footer/Button この曲へ");
            GameObject centerPanel = Scaffold("Main/Selected Song Panel");
            GameObject rightColumn = Scaffold("Main/Record Column");
            Assert.IsNotNull(headerSort);
            Assert.IsNotNull(footerGoto);
            Assert.IsNotNull(centerPanel);
            Assert.IsNotNull(rightColumn);

            view.MoveSelection(1);

            var rowsAfter = HistoryRows();
            Assert.AreEqual(rowsBefore.Count, rowsAfter.Count, "選択変更で行数が変わった");
            for (int i = 0; i < rowsBefore.Count; i++)
            {
                Assert.AreSame(rowsBefore[i], rowsAfter[i],
                    "slot " + i + " の履歴行が再生成されている (Issue #415)");
            }

            Assert.AreSame(headerSort, Scaffold("Header/Sort"), "ヘッダーが再生成されている");
            Assert.AreSame(footerGoto, Scaffold("Footer/Button この曲へ"), "フッターが再生成されている");
            Assert.AreSame(centerPanel, Scaffold("Main/Selected Song Panel"), "中央パネルが再生成されている");
            Assert.AreSame(rightColumn, Scaffold("Main/Record Column"), "右カラムが再生成されている");

            Assert.AreEqual(1, view.RebuildCountForAudit,
                "骨格が揃っているのに画面全体を作り直している");
            Assert.Greater(view.IncrementalUpdateCountForAudit, 0,
                "差分更新が計測されていない");
        }

        [Test]
        public void RefreshTwice_ReusesSameRowObjects()
        {
            var first = HistoryRows();
            Assert.Greater(first.Count, 0);

            data.RaiseChanged();
            var second = HistoryRows();
            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreSame(first[i], second[i], "Refresh で行 " + i + " が再生成されている");
            }

            Assert.AreEqual(1, view.RebuildCountForAudit);
        }
    }
}
