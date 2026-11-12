using System.Collections.Generic;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;
using FlashBeat.UI.V6.Select.View;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #399: 選曲画面の全面 Rebuild が「1 キー移動ごとに画面全体の
    /// GameObject / Graphic / TMP を作り直している」ことを **測って** 固定する。
    ///
    /// ここで測るのは時間 (ms) ではなく **生成・破棄されたオブジェクト数**。
    /// EditMode の 1 フレームでは GC も描画も走らないため、フレーム時間の
    /// 代用にはならない。Profiler の代わりに「構造の量」を数えるのが目的で、
    /// 数値は改善前後の比較にだけ使う (絶対値を性能の証明にしない)。
    ///
    /// このテストは Rebuild の**現状を記録**する。差分更新へ移した後は
    /// <see cref="AssertIncrementalUpdate"/> 側の期待値に切り替える。
    /// </summary>
    public class SelectRebuildCostTests
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

        private GameObject canvasGo;
        private FakeDataSource data;

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
                    Artist = "Artist",
                    Level = 10,
                    BestGrade = "A",
                    JacketResourcePath = "",
                });
            }

            data.SelectedValue = data.Songs[0];
        }

        [TearDown]
        public void TearDown()
        {
            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }
        }

        private V6SelectView CreateView()
        {
            return V6SelectView.Create(canvasGo.GetComponent<RectTransform>(), data, () => { }, () => { }, () => { });
        }

        /// <summary>ビュー配下のオブジェクト数 (破棄予約を含まない、いま階層にある数)。</summary>
        private static int CountObjects(Transform root)
        {
            if (root == null) return 0;
            int n = 1;
            for (int i = 0; i < root.childCount; i++)
            {
                n += CountObjects(root.GetChild(i));
            }

            return n;
        }

        /// <summary>
        /// 1 回の選択変更で何個の GameObject が作り直されるか。
        /// 全面 Rebuild なら全ゾーン相当、差分更新なら数個で済む。
        /// </summary>
        [Test]
        public void ChangingSelection_RecordsHowMuchIsRebuilt()
        {
            var view = CreateView();
            int before = CountObjects(view.Root);
            int fullRebuildsBefore = view.RebuildCountForAudit;

            data.Select(2);
            int after = CountObjects(view.Root);

            // Issue #399: 変動ゾーンだけを作り直すので、総数は同じでも
            // 生成される GameObject は骨格 (ヘッダー静的部分/フッター) の分だけ減る。
            Assert.Greater(before, 50, "選曲画面のツリーが想定より小さい (計測の前提が崩れている)");
            Assert.LessOrEqual(after, before,
                "差分更新でツリーが増えている (作り直しの取りこぼし)");
            Assert.AreEqual(1, view.IncrementalUpdateCountForAudit);
            Assert.AreEqual(fullRebuildsBefore, view.RebuildCountForAudit,
                "選択変更で画面全体を作り直している");
        }

        /// <summary>
        /// 検索 1 文字でツリー全体を作り直していることを記録する。
        /// </summary>
        [Test]
        public void TypingOneCharacter_RebuildsTheWholeTree()
        {
            var view = CreateView();
            int before = CountObjects(view.Root);

            data.SetQuery("a");
            int after = CountObjects(view.Root);

            Assert.AreEqual(before, after, 0, "全面 Rebuild の総数は同じ");
            Assert.Greater(before, 50);
        }

        /// <summary>
        /// 公開 API で「作り直した回数」を観測できるようにする。
        /// 差分更新の検証はこの計測点を使う。
        /// </summary>
        [Test]
        public void RebuildCount_IsObservable()
        {
            var view = CreateView();
            int before = view.RebuildCountForAudit;

            data.Select(3);
            data.Select(4);

            Assert.AreEqual(2, view.IncrementalUpdateCountForAudit,
                "データ変更 2 回は差分更新 2 回として観測される");
            Assert.AreEqual(before, view.RebuildCountForAudit,
                "骨格が揃っているのに画面全体を作り直している");
        }
    }
}
