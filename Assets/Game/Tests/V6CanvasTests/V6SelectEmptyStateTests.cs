using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// 02b Select · 該当なし (design AqFtx) の空状態を design 実測値に固定する。
    /// 旧実装は音楽アイコン + Title h32/y52 + Hint y88、ランキングは長文の左寄せで、
    /// 正本 (虫めがね+x 32@y14 / Title (0,54) h35 / Hint (0,97) h29 / NO DATA 中央) と違っていた。
    /// </summary>
    public sealed class V6SelectEmptyStateTests
    {
        private GameObject canvasGo;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6SelectEmptyStateTests Canvas", typeof(Canvas));
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

        private sealed class EmptySource : IV6SelectDataSource
        {
            private readonly List<V6SongEntry> songs = new List<V6SongEntry>();

            public IReadOnlyList<V6SongEntry> AllSongs { get { return songs; } }
            public IReadOnlyList<V6SongEntry> FilteredSongs { get { return songs; } }
            public string Query { get { return ""; } set { } }
            public V6SongEntry Selected { get { return null; } }
            public bool IsLoading { get { return false; } }
            public bool HasError { get { return false; } }
            public string StatusMessage { get { return ""; } }
            public event System.Action Changed;

            public void SetQuery(string query) { }
            public void Select(int songId) { }
            public void MoveSelection(int delta) { }
            public void RefreshAsync() { }
            public void CommitReady() { }
            public IReadOnlyList<V6RankingRow> RankingFor(V6SongEntry song)
            {
                return new List<V6RankingRow>();
            }
        }

        private V6SelectView CreateView()
        {
            return V6SelectView.Create((RectTransform)canvasGo.transform, new EmptySource(), null, null, null);
        }

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

        private static Vector2 DesignTopLeft(RectTransform rt)
        {
            return new Vector2(rt.anchoredPosition.x, -rt.anchoredPosition.y);
        }

        [Test]
        public void EmptyNote_MatchesDesign()
        {
            var view = CreateView();
            try
            {
                var empty = Find(view.Root, "Empty Note");
                Assert.AreEqual(new Vector2(56f, 300f), DesignTopLeft(empty), "Empty Note (56,300)");
                Assert.AreEqual(new Vector2(448f, 140f), empty.sizeDelta, "448×140");

                var title = (RectTransform)empty.Find("Title");
                Assert.AreEqual(new Vector2(0f, 54f), DesignTopLeft(title), "Title (0,54)");
                Assert.AreEqual(new Vector2(448f, 35f), title.sizeDelta, "Title h35");
                var titleTmp = title.GetComponent<TextMeshProUGUI>();
                Assert.AreEqual(24f, titleTmp.fontSize);
                Assert.AreEqual(TextAlignmentOptions.Center, titleTmp.alignment);

                var hint = (RectTransform)empty.Find("Hint");
                Assert.AreEqual(new Vector2(0f, 97f), DesignTopLeft(hint), "Hint (0,97)");
                Assert.AreEqual(new Vector2(448f, 29f), hint.sizeDelta, "Hint h29");
                Assert.AreEqual(20f, hint.GetComponent<TextMeshProUGUI>().fontSize);
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        [Test]
        public void EmptyNoteIcon_IsSearchXAtDesignPosition()
        {
            var view = CreateView();
            try
            {
                var empty = Find(view.Root, "Empty Note");
                var icon = (RectTransform)empty.Find("Icon");
                Assert.AreEqual(new Vector2(32f, 32f), icon.sizeDelta, "Icon 32×32");
                Assert.AreEqual(0f, icon.anchoredPosition.x, 0.01f, "Icon は左右中央");
                Assert.AreEqual(-14f, icon.anchoredPosition.y, 0.01f, "Icon 上端は y14");
                Assert.AreEqual(new Vector2(0.5f, 1f), icon.anchorMin, "上端中央アンカー");
                Assert.AreEqual(new Vector2(0.5f, 1f), icon.pivot, "上端中央ピボット");

                var sprite = icon.GetComponent<Image>().sprite;
                Assert.IsNotNull(sprite, "Icon スプライトが解決される");
                Assert.AreEqual("search-x", sprite.name, "design は虫めがね+x (音楽アイコンではない)");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        [Test]
        public void RankingEmpty_IsCenteredNoDataAtDesignPosition()
        {
            var view = CreateView();
            try
            {
                var ranking = Find(view.Root, "Score Ranking");
                var empty = (RectTransform)ranking.Find("No Data");
                Assert.AreEqual("NO DATA", empty.GetComponent<TextMeshProUGUI>().text);
                Assert.AreEqual(new Vector2(8f, 155f), DesignTopLeft(empty), "xIoXm (8,155)");
                Assert.AreEqual(new Vector2(496f, 35f), empty.sizeDelta, "496×35");
                Assert.AreEqual(TextAlignmentOptions.Center, empty.GetComponent<TextMeshProUGUI>().alignment,
                    "中央揃え");
                Assert.AreEqual(24f, empty.GetComponent<TextMeshProUGUI>().fontSize);
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        [Test]
        public void PlayersEmpty_IsCenteredNoDataAtDesignPosition()
        {
            var view = CreateView();
            try
            {
                var players = Find(view.Root, "Players");
                var empty = (RectTransform)players.Find("No Data");
                Assert.AreEqual("NO DATA", empty.GetComponent<TextMeshProUGUI>().text);
                Assert.AreEqual(new Vector2(8f, 149f), DesignTopLeft(empty), "IL4N7 (8,149)");
                Assert.AreEqual(new Vector2(496f, 35f), empty.sizeDelta, "496×35");
                Assert.AreEqual(TextAlignmentOptions.Center, empty.GetComponent<TextMeshProUGUI>().alignment);
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }
    }
}
