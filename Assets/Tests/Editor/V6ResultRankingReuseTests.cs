using System.Collections.Generic;
using System.Reflection;
using FlashBeat.UI.V6.Result;
using FlashBeat.UI.V6.Select;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #415: Result 画面の曲別ランキング行が、データ更新時に
    /// 行 GameObject を破棄・再生成せずスロット単位で再利用することを固定する。
    ///
    /// rankingRows は EditMode では通信経路 (<c>FetchRanking</c>) が止まるため、
    /// reflection で private フィールドへ直接注入し、private の
    /// <c>RefreshRanking</c> を駆動する。検証対象は「同じ行数での再 Refresh で
    /// 行オブジェクトが同一実体のまま中身だけ更新される」ことと、
    /// 「行数が減ったとき余剰スロットだけが破棄される」こと。
    /// </summary>
    public class V6ResultRankingReuseTests
    {
        private GameObject parent;
        private V6ResultView view;

        [SetUp]
        public void SetUp()
        {
            parent = new GameObject("Parent", typeof(RectTransform));
            view = V6ResultView.Create(parent.GetComponent<RectTransform>(), () => { }, () => { }, () => { });
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

        private static object Field(object target, string name)
        {
            var f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, "field not found: " + name);
            return f.GetValue(target);
        }

        private static void InvokePrivate(object target, string name)
        {
            var m = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(m, "method not found: " + name);
            m.Invoke(target, null);
        }

        private List<V6RankingRow> RankingRows()
        {
            return (List<V6RankingRow>)Field(view, "rankingRows");
        }

        private RectTransform RowContainer()
        {
            return (RectTransform)Field(view, "songRankRows");
        }

        private static List<GameObject> RowObjects(RectTransform container)
        {
            var rows = new List<GameObject>();
            for (int i = 0; i < container.childCount; i++)
            {
                rows.Add(container.GetChild(i).gameObject);
            }

            return rows;
        }

        private static string RowText(GameObject row, string childPath)
        {
            Transform t = row.transform.Find(childPath);
            var tmp = t != null ? t.GetComponent<TextMeshProUGUI>() : null;
            return tmp != null ? tmp.text : null;
        }

        private static V6RankingRow Row(int pos, string name, int score, bool you = false)
        {
            return new V6RankingRow
            {
                Position = pos,
                PlayerName = name,
                Score = score,
                Grade = "A",
                IsYou = you,
            };
        }

        [Test]
        public void SecondRefresh_SameRowCount_ReusesRowObjects()
        {
            var rows = RankingRows();
            rows.Add(Row(1, "alice", 100000));
            rows.Add(Row(2, "bob", 90000));
            rows.Add(Row(3, "carol", 80000));
            InvokePrivate(view, "RefreshRanking");

            var first = RowObjects(RowContainer());
            Assert.AreEqual(3, first.Count, "初回 Refresh で 3 行できていない");

            // 同じ行数で中身だけ変えて再 Refresh (ランキング再取得の再現)。
            rows[0].PlayerName = "alice2";
            rows[0].Score = 999999;
            rows[1].IsYou = true;
            InvokePrivate(view, "RefreshRanking");

            var second = RowObjects(RowContainer());
            Assert.AreEqual(3, second.Count, "再 Refresh で行数が変わった");
            for (int i = 0; i < 3; i++)
            {
                Assert.AreSame(first[i], second[i],
                    "slot " + i + " の行 GameObject が再生成されている (Issue #415 の差分更新が効いていない)");
            }

            Assert.AreEqual("alice2", RowText(second[0], "Name"));
            Assert.AreEqual("YOU", RowText(second[1], "Name"), "IsYou の行が YOU 表示に更新されていない");
            // IsYou=true の行は Name が "YOU" になるので、元の名前ではなく
            // スコア側でデータ反映を確認する。
            Assert.AreEqual(FlashBeat.UI.V6.Result.V6ResultLogic.FormatScore(90000),
                RowText(second[1], "Value"));
        }

        [Test]
        public void ShrinkRefresh_DestroysOnlyExtraSlots()
        {
            var rows = RankingRows();
            rows.Add(Row(1, "alice", 100000));
            rows.Add(Row(2, "bob", 90000));
            rows.Add(Row(3, "carol", 80000));
            InvokePrivate(view, "RefreshRanking");

            var first = RowObjects(RowContainer());
            Assert.AreEqual(3, first.Count);

            rows.RemoveRange(1, 2); // 3 行 → 1 行
            InvokePrivate(view, "RefreshRanking");

            var after = RowObjects(RowContainer());
            Assert.AreEqual(1, after.Count, "縮小後に余剰行が残っている");
            Assert.AreSame(first[0], after[0], "残る行まで再生成されている");
            Assert.IsTrue(first[1] == null && first[2] == null,
                "余剰スロットの行が破棄されていない");
        }

        [Test]
        public void GrowRefresh_ReusesExistingAndAppendsNew()
        {
            var rows = RankingRows();
            rows.Add(Row(1, "alice", 100000));
            InvokePrivate(view, "RefreshRanking");

            var first = RowObjects(RowContainer());
            Assert.AreEqual(1, first.Count);

            rows.Add(Row(2, "bob", 90000));
            rows.Add(Row(3, "carol", 80000));
            InvokePrivate(view, "RefreshRanking");

            var after = RowObjects(RowContainer());
            Assert.AreEqual(3, after.Count);
            Assert.AreSame(first[0], after[0], "既存行が再生成されている");
            Assert.AreEqual("bob", RowText(after[1], "Name"));
            Assert.AreEqual("carol", RowText(after[2], "Name"));
        }
    }
}
