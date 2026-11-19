using System.Collections.Generic;
using System.Reflection;
using FlashBeat.UI.V6.History;
using FlashBeat.UI.V6.Ranking;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;
using FlashBeat.UI.V6.Select.View;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #642: Select 画面を離れるとき、v6 の 3 ビュー
    /// (選曲 / 履歴 / ランキング) が Dispose され、static な Current と
    /// データソースの Changed 購読が残らないことを固定する。
    ///
    /// 3 ビューは生成時に共有データソースの Changed を購読し Current に
    /// 自分を登録する。SelectSceneManager が破棄しないと、シーンを出た後も
    /// 購読が残りデータ更新のたびに破棄済みビューへコールバックが飛ぶ
    /// (OptionSceneManager.OnDestroy と同じ契約、Issue #416 の先例)。
    /// </summary>
    public class SelectSceneDisposeTests
    {
        private sealed class FakeDataSource : IV6SelectDataSource
        {
            public readonly List<V6SongEntry> Songs = new List<V6SongEntry>();
            public V6SongEntry SelectedValue;
            private string query = "";

            public IReadOnlyList<V6SongEntry> AllSongs { get { return Songs; } }
            public IReadOnlyList<V6SongEntry> FilteredSongs { get { return Songs; } }
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

            public void SetQuery(string value) { query = value; }
            public void Select(int songId)
            {
                SelectedValue = Songs.Find(s => s.SongId == songId);
            }
            public void MoveSelection(int delta) { }
            public void RefreshAsync() { }
            public void CommitReady() { }
            public IReadOnlyList<V6RankingRow> RankingFor(V6SongEntry song)
            {
                return new List<V6RankingRow>();
            }
        }

        private GameObject canvasGo;
        private GameObject managerGo;
        private FakeDataSource data;
        private SelectSceneManager manager;
        private V6SelectView selectView;
        private V6HistoryView historyView;
        private V6RankingView rankingView;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("Select Canvas", typeof(RectTransform), typeof(Canvas));
            data = new FakeDataSource();
            data.Songs.Add(new V6SongEntry
            {
                SongId = 1, Title = "Song 1", Artist = "Artist", Level = 10,
                BestScore = 100000, BestGrade = "A", NoteCount = 100,
            });
            data.SelectedValue = data.Songs[0];

            var rect = canvasGo.GetComponent<RectTransform>();
            selectView = V6SelectView.Create(rect, data, () => { }, () => { }, () => { });
            historyView = V6HistoryView.Create(rect, () => { }, data);
            rankingView = V6RankingView.Create(rect, () => { }, data);

            Assert.IsNotNull(V6SelectView.Current);
            Assert.IsNotNull(V6HistoryView.Current);
            Assert.IsNotNull(V6RankingView.Current);

            // SelectSceneManager のビューは private フィールド。実際の
            // CreateV6SelectView を通さず (Start はシーン依存が重い)、
            // 生成済みビューを結線して OnDestroy 経路だけを検証する。
            managerGo = new GameObject("SelectSceneManager", typeof(SelectSceneManager));
            manager = managerGo.GetComponent<SelectSceneManager>();
            SetField("v6View", selectView);
            SetField("historyView", historyView);
            SetField("rankingView", rankingView);
        }

        [TearDown]
        public void TearDown()
        {
            // 念のため: 破棄漏れがあっても static Current と購読を残さない。
            if (selectView != null) selectView.Dispose();
            if (historyView != null) historyView.Dispose();
            if (rankingView != null) rankingView.Dispose();
            if (managerGo != null) Object.DestroyImmediate(managerGo);
            if (canvasGo != null) Object.DestroyImmediate(canvasGo);
        }

        void SetField(string name, object value)
        {
            FieldInfo field = typeof(SelectSceneManager).GetField(
                name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "SelectSceneManager." + name + " が見つからない");
            field.SetValue(manager, value);
        }

        [Test]
        public void DisposeV6Views_ClearsAllStaticCurrents()
        {
            manager.DisposeV6Views();

            Assert.IsNull(V6SelectView.Current, "V6SelectView.Current が残っている");
            Assert.IsNull(V6HistoryView.Current, "V6HistoryView.Current が残っている");
            Assert.IsNull(V6RankingView.Current, "V6RankingView.Current が残っている");
        }

        [Test]
        public void DisposeV6Views_UnsubscribesFromDataChanged()
        {
            manager.DisposeV6Views();

            // 購読が残っていれば破棄済みビューの OnDataChanged が呼ばれて
            // 例外か再生成が起きる。ここでは「通知しても壊れない」ことを確認する。
            Assert.DoesNotThrow(() => data.RaiseChanged());
        }

        [Test]
        public void DisposeV6Views_IsIdempotent()
        {
            manager.DisposeV6Views();
            Assert.DoesNotThrow(() => manager.DisposeV6Views(),
                "OnDestroy と手動呼び出しが重なっても安全であること");
            Assert.IsNull(V6SelectView.Current);
            Assert.IsNull(V6HistoryView.Current);
            Assert.IsNull(V6RankingView.Current);
        }
    }
}
