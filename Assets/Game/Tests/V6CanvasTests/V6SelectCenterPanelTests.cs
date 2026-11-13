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
    /// 02 Select · 中央パネル (design dRztp) の組版 (Issue #374)。
    ///
    /// Meta は「縦 gap4・中央揃え」の 512×100。Title (0,8.5) 512×50、
    /// Artist 行 (0,62.5) 512×29 — 50 + 4 + 29 = 83 が 100 の中央に来る。
    /// Issue #427: 行コンテナは廃止し、No. タグは左端絶対配置・作曲者名は
    /// 512 全幅の中央揃えで、両者の中心軸のずれを構造的に無くした。
    /// </summary>
    public sealed class V6SelectCenterPanelTests
    {
        private GameObject canvasGo;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6SelectCenterPanelTests Canvas", typeof(Canvas));
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

        private sealed class OneSongSource : IV6SelectDataSource
        {
            private readonly List<V6SongEntry> songs = new List<V6SongEntry>();

            public OneSongSource()
            {
                songs.Add(new V6SongEntry
                {
                    SongId = 5,
                    Title = "終焉逃避行",
                    Artist = "柊マグネタイト",
                    Level = 14,
                    Bpm = 147.0,
                    NoteCount = 296,
                    JacketResourcePath = "",
                    BestScore = 988513,
                    BestGrade = "A",
                });
            }

            public IReadOnlyList<V6SongEntry> AllSongs { get { return songs; } }
            public IReadOnlyList<V6SongEntry> FilteredSongs { get { return songs; } }
            public string Query { get { return ""; } set { } }
            public V6SongEntry Selected { get { return songs[0]; } }
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

        [Test]
        public void Meta_TitleAndArtistRow_SitAtTheDesignCoordinates()
        {
            var view = V6SelectView.Create((RectTransform)canvasGo.transform, new OneSongSource(),
                null, null, null);
            try
            {
                RectTransform meta = Find(view.Root.transform, "Meta");
                Assert.AreEqual(512f, meta.sizeDelta.x, 0.01f, "Meta width");
                Assert.AreEqual(100f, meta.sizeDelta.y, 0.01f, "Meta height");

                RectTransform title = Find(meta, "Title");
                Assert.AreEqual(new Vector2(0f, 8.5f), DesignTopLeft(title), "Title (0, 8.5)");
                Assert.AreEqual(new Vector2(512f, 50f), title.sizeDelta, "Title 512×50");
                Assert.AreEqual(TextAlignmentOptions.Center, title.GetComponent<TextMeshProUGUI>().alignment,
                    "Title is centered");
                Assert.AreEqual(48f, title.GetComponent<TextMeshProUGUI>().fontSize, 0.01f, "Title fs48");

                RectTransform tag = Find(meta, "Tag No");
                Assert.AreEqual(new Vector2(0f, 62.5f), DesignTopLeft(tag), "Tag No (0, 62.5)");
                Assert.AreEqual(new Vector2(63f, 29f), tag.sizeDelta, "Tag No 63×29");

                RectTransform artist = Find(meta, "Artist");
                Assert.AreEqual(new Vector2(0f, 62.5f), DesignTopLeft(artist), "Artist (0, 62.5)");
                Assert.AreEqual(new Vector2(512f, 29f), artist.sizeDelta, "Artist 512×29");
                Assert.AreEqual(TextAlignmentOptions.Center,
                    artist.GetComponent<TextMeshProUGUI>().alignment, "Artist is centered");

                // 縦 gap4・全体 83 が 100 の中央。
                float gap = 62.5f - (8.5f + 50f);
                Assert.AreEqual(4f, gap, 0.01f, "Title と Artist 行の間は 4");
                Assert.AreEqual((100f - (50f + gap + 29f)) * 0.5f, 8.5f, 0.01f,
                    "Title と Artist 行の組は Meta の上下中央");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        [Test]
        public void ArtistRow_TagAtLeft_AndComposerCentredAcrossFullWidth()
        {
            var view = V6SelectView.Create((RectTransform)canvasGo.transform, new OneSongSource(),
                null, null, null);
            try
            {
                RectTransform meta = Find(view.Root.transform, "Meta");

                // No. タグは行の左端 (63×29)、作曲者名は 512 全幅で中央揃え。
                RectTransform tag = Find(meta, "Tag No");
                Assert.AreEqual(new Vector2(0f, 62.5f), DesignTopLeft(tag), "Tag No left edge");
                Assert.AreEqual(new Vector2(63f, 29f), tag.sizeDelta, "Tag No 63×29");

                RectTransform artist = Find(meta, "Artist");
                Assert.AreEqual(26f, artist.GetComponent<TextMeshProUGUI>().fontSize, 0.01f, "Artist fs26");
                Assert.AreEqual(TextAlignmentOptions.Center,
                    artist.GetComponent<TextMeshProUGUI>().alignment,
                    "作曲者名はタグ分の偏りなく中央");
                Assert.AreEqual(new Vector2(512f, 29f), artist.sizeDelta, "Artist 512×29");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
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

        /// <summary>親の左上を原点とする design 座標 (y_down)。</summary>
        private static Vector2 DesignTopLeft(RectTransform rt)
        {
            return new Vector2(
                rt.anchoredPosition.x - rt.pivot.x * rt.sizeDelta.x,
                -rt.anchoredPosition.y - (1f - rt.pivot.y) * rt.sizeDelta.y);
        }
    }
}
