using System.Collections.Generic;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;
using FlashBeat.UI.V6.Select.View;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #415 (第4段階): Select 画面が選択変更・検索・再 Refresh のたびに
    /// Header/Footer/Song List/Selected Song Panel/Ranking Column の骨格と
    /// 可視行を破棄・再生成せず、スロット単位で再利用することを固定する。
    ///
    /// 検証するのは「同じデータ変更で各ゾーンの静的要素が破棄・再生成されない」
    /// こと (Issue #415 受入条件) と、行 GameObject が同一実体のまま
    /// 中身 (曲名・選択色・スコア) だけ更新されること。
    /// </summary>
    public class V6SelectViewReuseTests
    {
        private sealed class FakeDataSource : IV6SelectDataSource
        {
            public readonly List<V6SongEntry> Songs = new List<V6SongEntry>();
            public V6SongEntry SelectedValue;
            private string query = "";
            private IReadOnlyList<V6SongEntry> filtered;
            private IReadOnlyList<V6RankingRow> ranking = new List<V6RankingRow>();

            public IReadOnlyList<V6SongEntry> AllSongs { get { return Songs; } }
            public IReadOnlyList<V6SongEntry> FilteredSongs
            {
                get { return filtered ?? Songs; }
                set { filtered = value; }
            }
            public string Query { get { return query; } set { query = value; } }
            public V6SongEntry Selected { get { return SelectedValue; } }
            public bool IsLoading { get { return false; } }
            public bool HasError { get { return false; } }
            public string StatusMessage { get { return ""; } }
            public event System.Action Changed;

            public void RaiseChanged()
            {
                Changed?.Invoke();
            }

            public void SetQuery(string value)
            {
                query = value;
                Changed?.Invoke();
            }
            public void Select(int songId)
            {
                SelectedValue = Songs.Find(s => s.SongId == songId);
                Changed?.Invoke();
            }
            public void MoveSelection(int delta) { }
            public void RefreshAsync() { }
            public void CommitReady() { }
            public IReadOnlyList<V6RankingRow> RankingFor(V6SongEntry song) { return ranking; }
            public void SetRanking(IReadOnlyList<V6RankingRow> rows) { ranking = rows; }
        }

        private GameObject canvasGo;
        private FakeDataSource data;
        private V6SelectView view;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("Select Canvas", typeof(RectTransform), typeof(Canvas));
            data = new FakeDataSource();
            for (int i = 1; i <= 12; i++)
            {
                data.Songs.Add(new V6SongEntry
                {
                    SongId = i,
                    Title = "Song " + i,
                    Artist = "Artist " + i,
                    Level = 10 + i,
                    BestScore = 100000 * i,
                    BestGrade = i % 2 == 0 ? "A" : "B",
                    Bpm = 120 + i,
                    NoteCount = 100 * i,
                    JacketResourcePath = "",
                });
            }

            data.SelectedValue = data.Songs[0];
            view = V6SelectView.Create(canvasGo.GetComponent<RectTransform>(), data,
                () => { }, () => { }, () => { });
        }

        [TearDown]
        public void TearDown()
        {
            if (view != null)
            {
                view.Dispose();
            }

            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
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

        private GameObject Zone(string name)
        {
            Transform t = Find(view.Root, name);
            return t != null ? t.gameObject : null;
        }

        private List<GameObject> SongRows()
        {
            Transform rows = Find(view.Root, "Song List/Rows");
            return Children(rows);
        }

        private static string RowTitle(GameObject row)
        {
            var tmp = row != null ? row.transform.Find("Title")?.GetComponent<TMPro.TextMeshProUGUI>() : null;
            return tmp != null ? tmp.text : null;
        }

        [Test]
        public void SelectionChange_KeepsScaffoldAndReusesRows()
        {
            Assert.AreEqual(1, view.RebuildCountForAudit, "初回構築が Rebuild として数えられていない");

            var rowsBefore = SongRows();
            Assert.Greater(rowsBefore.Count, 0, "曲行が生成されていない");
            GameObject header = Zone("Header");
            GameObject footer = Zone("Footer");
            GameObject list = Zone("Song List");
            GameObject center = Zone("Selected Song Panel");
            GameObject right = Zone("Ranking Column");
            Assert.IsNotNull(header);
            Assert.IsNotNull(footer);
            Assert.IsNotNull(list);
            Assert.IsNotNull(center);
            Assert.IsNotNull(right);

            data.Select(5);

            var rowsAfter = SongRows();
            Assert.AreEqual(rowsBefore.Count, rowsAfter.Count, "選択変更で行数が変わった");
            for (int i = 0; i < rowsBefore.Count; i++)
            {
                Assert.AreSame(rowsBefore[i], rowsAfter[i],
                    "slot " + i + " の曲行が再生成されている (Issue #415)");
            }

            Assert.AreSame(header, Zone("Header"), "ヘッダーが再生成されている");
            Assert.AreSame(list, Zone("Song List"), "曲リスト scaffold が再生成されている");
            Assert.AreSame(center, Zone("Selected Song Panel"), "中央パネルが再生成されている");
            Assert.AreSame(right, Zone("Ranking Column"), "右カラムが再生成されている");
            // Ready の有効状態が変わらない限りフッターも保持される。
            Assert.AreSame(footer, Zone("Footer"), "フッターが不要に再生成されている");

            Assert.AreEqual(1, view.RebuildCountForAudit,
                "骨格が揃っているのに画面全体を作り直している");
            Assert.Greater(view.IncrementalUpdateCountForAudit, 0,
                "差分更新が計測されていない");

            // 選択状態の反映: 選択行だけが選択色で、曲名が選択曲と一致し、
            // 中央パネルの曲名・BEST が選択曲の値に更新されていること。
            var selected = data.Songs.Find(s => s.SongId == 5);
            GameObject selectedRow = null;
            foreach (var row in rowsAfter)
            {
                var bg = row.GetComponent<UnityEngine.UI.Image>();
                if (bg != null && bg.color == FlashBeat.UI.V5.V5Tokens.Key6)
                {
                    Assert.IsNull(selectedRow, "選択色の行が複数ある");
                    selectedRow = row;
                }
            }
            Assert.IsNotNull(selectedRow, "選択行 (Key6 背景) が見つからない");
            Assert.AreEqual(selected.Title, RowTitle(selectedRow),
                "選択行のタイトルが選択曲と一致しない");

            var centerTitle = Find(center.transform, "Meta/Title")
                ?.GetComponent<TMPro.TextMeshProUGUI>();
            Assert.IsNotNull(centerTitle, "中央パネルに曲名テキストが無い");
            Assert.AreEqual(selected.Title, centerTitle.text,
                "中央パネルの曲名が選択曲と一致しない");

            var bestValue = Find(center.transform, "Stats/Stat BEST/Value")
                ?.GetComponent<TMPro.TextMeshProUGUI>();
            Assert.IsNotNull(bestValue, "中央パネルに BEST 値が無い");
            Assert.AreEqual(V6SelectViewLogic.FormatScore(selected.BestScore), bestValue.text,
                "中央パネルの BEST が選択曲と一致しない");
        }

        [Test]
        public void RefreshTwice_ReusesSameRowObjects()
        {
            var first = SongRows();
            Assert.Greater(first.Count, 0);

            data.RaiseChanged();
            var second = SongRows();
            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreSame(first[i], second[i], "Refresh で行 " + i + " が再生成されている");
            }

            Assert.AreEqual(1, view.RebuildCountForAudit);
        }

        [Test]
        public void QueryChange_UpdatesHeaderAndKeepsRows()
        {
            var headerBefore = Zone("Header");
            var rowsBefore = SongRows();
            Assert.Greater(rowsBefore.Count, 0);

            data.SetQuery("Song 3");

            Assert.AreSame(headerBefore, Zone("Header"), "検索でヘッダーが再生成されている");
            var rowsAfter = SongRows();
            for (int i = 0; i < rowsBefore.Count; i++)
            {
                Assert.AreSame(rowsBefore[i], rowsAfter[i],
                    "検索で行 " + i + " が再生成されている");
            }

            var queryText = Find(headerBefore.transform, "Search/Query")
                ?.GetComponent<TMPro.TextMeshProUGUI>();
            Assert.IsNotNull(queryText, "検索欄の Query テキストが無い");
            Assert.AreEqual("Song 3", queryText.text,
                "検索語がヘッダーへ反映されていない");

            var countText = Find(headerBefore.transform, "Count")
                ?.GetComponent<TMPro.TextMeshProUGUI>();
            Assert.IsNotNull(countText, "件数テキストが無い");
            Assert.AreEqual("12 / 12", countText.text,
                "件数表示が更新されていない (フェイクの FilteredSongs は全件)");
        }

        [Test]
        public void RankingRows_AreReusedAcrossSelectionChange()
        {
            data.SetRanking(new List<V6RankingRow>
            {
                new V6RankingRow { Position = 1, PlayerName = "Alice", Score = 900000, Grade = "SS", IsYou = true },
                new V6RankingRow { Position = 2, PlayerName = "Bob", Score = 800000, Grade = "S", IsYou = false },
            });
            data.RaiseChanged();

            // 「Score Ranking」直下の子からランキング行 ("Score N") だけを拾う。
            var rankRowsBefore = new List<GameObject>();
            var rankingT = Find(Zone("Ranking Column").transform, "Score Ranking");
            for (int i = 0; i < rankingT.childCount; i++)
            {
                var child = rankingT.GetChild(i).gameObject;
                if (child.name.StartsWith("Score "))
                {
                    rankRowsBefore.Add(child);
                }
            }
            Assert.Greater(rankRowsBefore.Count, 0, "ランキング行が生成されていない");

            data.Select(3);
            data.RaiseChanged();

            var rankingT2 = Find(Zone("Ranking Column").transform, "Score Ranking");
            var rankRowsAfter = new List<GameObject>();
            for (int i = 0; i < rankingT2.childCount; i++)
            {
                var child = rankingT2.GetChild(i).gameObject;
                if (child.name.StartsWith("Score "))
                {
                    rankRowsAfter.Add(child);
                }
            }

            Assert.AreEqual(rankRowsBefore.Count, rankRowsAfter.Count,
                "選択変更でランキング行数が変わった");
            for (int i = 0; i < rankRowsBefore.Count; i++)
            {
                Assert.AreSame(rankRowsBefore[i], rankRowsAfter[i],
                    "ランキング行 " + i + " が再生成されている (Issue #415)");
            }

            // 行の中身は現在のランキングデータへ更新されている。
            var nameText = rankRowsAfter[0].transform.Find("Name")
                ?.GetComponent<TMPro.TextMeshProUGUI>();
            Assert.IsNotNull(nameText, "ランキング行に Name テキストが無い");
            Assert.AreEqual("YOU", nameText.text,
                "IsYou 行の表示名が更新されていない");
        }

        [Test]
        public void EmptyState_ShowsEmptyNoteWithoutRebuild()
        {
            var rowsBefore = SongRows();
            Assert.Greater(rowsBefore.Count, 0);
            var listBefore = Zone("Song List");

            data.FilteredSongs = new List<V6SongEntry>();
            data.RaiseChanged();

            Assert.AreSame(listBefore, Zone("Song List"),
                "空状態への遷移でリスト scaffold が再生成されている");
            var rows = Find(view.Root, "Song List/Rows");
            var empty = rows != null ? rows.Find("Empty Note") : null;
            Assert.IsNotNull(empty, "空状態 (Empty Note) が表示されていない");
            Assert.AreEqual(0, Children(rows).Count - 1,
                "空状態で曲行が残っている");

            // 空 → 行へ戻ると行スロットが再生成される (構造遷移は再構築が正しい)。
            data.FilteredSongs = null;
            data.RaiseChanged();
            Assert.Greater(SongRows().Count, 0, "行モードへ戻っても曲行が無い");
            Assert.AreEqual(1, view.RebuildCountForAudit,
                "リスト内側のモード遷移で画面全体を作り直している");
        }

        [Test]
        public void QuickSettings_TogglesRightColumn()
        {
            Assert.IsNotNull(Zone("Ranking Column"), "初期状態で Ranking Column が無い");
            Assert.IsNull(Zone("Settings Column"), "初期状態で Settings Column がある");

            view.OpenQuickSettings();

            Assert.IsNull(Zone("Ranking Column"), "設定表示で Ranking Column が残っている");
            Assert.IsNotNull(Zone("Settings Column"), "設定表示で Settings Column が無い");
            // 設定の開閉は右カラムの構造変更なので全体 Rebuild になる
            // (RefreshVolatile が scaffoldSettingsOpen の差を検出する)。
            Assert.AreEqual(2, view.RebuildCountForAudit,
                "設定を開いても Rebuild が計測されていない");

            view.CloseQuickSettings();

            Assert.IsNotNull(Zone("Ranking Column"), "設定を閉じても Ranking Column が戻らない");
            Assert.IsNull(Zone("Settings Column"), "設定を閉じても Settings Column が残っている");
            Assert.AreEqual(3, view.RebuildCountForAudit);
        }
    }
}
