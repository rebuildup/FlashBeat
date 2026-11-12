using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// 02c Select · 設定 (design wSjAm) の右カラム「このプレイの設定」を行内部まで
    /// design 実測値に固定する。
    /// - Head (24,8) fs26 / 行 512×56 を 68 間隔で 3 行 / 行の角丸 8 / 背景 #2D2D2D
    /// - 行内: Icon (12,16) 24 / Label (48, 縦中央) fs24 /
    ///   Value は右端 448 に右寄せ (幅は TMP 実測) /
    ///   Arrow · chevron-left は値の左 - 52、chevron-right は (460,8)、いずれも 40×40。
    /// 旧実装は icon を x0 から描き、値を固定幅 96 の中央揃えにしていた。
    /// </summary>
    public sealed class V6SelectSettingsLayoutTests
    {
        private GameObject canvasGo;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6SelectSettingsLayoutTests Canvas", typeof(Canvas));
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

        /// <summary>pivot に依らない design 左上座標 (行内の Label/Value は pivot (0,0.5))。</summary>
        private static Vector2 DesignTopLeft(RectTransform rt)
        {
            return new Vector2(
                rt.anchoredPosition.x - rt.pivot.x * rt.sizeDelta.x,
                -rt.anchoredPosition.y - (1f - rt.pivot.y) * rt.sizeDelta.y);
        }

        private static void AssertTopLeft(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = (RectTransform)parent.Find(name);
            Assert.IsNotNull(rt, name + " not found under " + parent.name);
            Assert.AreEqual(new Vector2(x, y), DesignTopLeft(rt), name + " top-left");
            Assert.AreEqual(new Vector2(w, h), rt.sizeDelta, name + " size");
        }

        [Test]
        public void SettingsColumn_HeadAndRows_MatchDesign()
        {
            var view = V6SelectView.Create((RectTransform)canvasGo.transform, new EmptySource(), null, null, null);
            try
            {
                view.OpenQuickSettings();

                var column = (RectTransform)view.Root.Find("Settings Column");
                Assert.IsNotNull(column, "Settings Column");

                var head = (RectTransform)column.Find("Head");
                Assert.AreEqual(new Vector2(V6SelectViewLogic.SettingsHeadX, V6SelectViewLogic.SettingsHeadY),
                    DesignTopLeft(head), "Head (24,8)");
                Assert.AreEqual(26f, head.GetComponent<TextMeshProUGUI>().fontSize, 0.01f, "Head fs26");

                string[] labels = { "Key Guide", "Lyrics", "NoteSpeed" };
                for (int i = 0; i < labels.Length; i++)
                {
                    var row = (RectTransform)column.Find("Set " + labels[i]);
                    Assert.IsNotNull(row, "Set " + labels[i]);
                    var expected = V6SelectViewLogic.SettingsRowRect(i);
                    Assert.AreEqual(new Vector2(expected.X, expected.Y), DesignTopLeft(row),
                        labels[i] + " row position");

                    // Icon (12,16) 24×24。旧実装は中央寄せ (12, 中央) で左端が x0 だった。
                    AssertTopLeft(row, "Icon", 12f, 16f, 24f, 24f);

                    var label = (RectTransform)row.Find("Label");
                    Assert.AreEqual(48f, DesignTopLeft(label).x, labels[i] + " Label x48");
                    Assert.AreEqual(56f, label.sizeDelta.y, labels[i] + " Label h56");
                    Assert.AreEqual(24f, label.GetComponent<TextMeshProUGUI>().fontSize, 0.01f,
                        labels[i] + " Label fs24");
                    Assert.AreEqual(0f, DesignTopLeft(label).y, 0.01f,
                        labels[i] + " Label rect starts at the row top");
                    Assert.AreEqual(new Vector2(0f, 0.5f), label.pivot, labels[i] + " Label pivot (0,0.5)");

                    // Value は固定列 (x328, w120) の中央。左の chevron は x276。
                    // 行ごとに値の幅が変わっても矢印と値の中心は動かない (Issue #370)。
                    var value = (RectTransform)row.Find("Value");
                    Assert.AreEqual(V6SelectViewLogic.SettingsRowValueX, DesignTopLeft(value).x, 0.5f,
                        labels[i] + " Value x = 328");
                    Assert.AreEqual(V6SelectViewLogic.SettingsRowValueW, value.sizeDelta.x, 0.5f,
                        labels[i] + " Value w = 120");
                    Assert.AreEqual(448f, DesignTopLeft(value).x + value.sizeDelta.x, 0.5f,
                        labels[i] + " Value right edge = 448");
                    var valueTmp = value.GetComponent<TextMeshProUGUI>();
                    Assert.AreEqual(TextAlignmentOptions.Center, valueTmp.alignment,
                        labels[i] + " Value centered in its column");
                    Assert.AreEqual(TextWrappingModes.NoWrap, valueTmp.textWrappingMode,
                        labels[i] + " Value must not wrap");

                    // chevron-left は固定 (276,8) / chevron-right は (460,8)。
                    var prev = (RectTransform)row.Find("Prev");
                    Assert.AreEqual(V6SelectViewLogic.SettingsRowPrevX, DesignTopLeft(prev).x, 0.5f,
                        labels[i] + " Prev x = 276");
                    Assert.AreEqual(8f, DesignTopLeft(prev).y, labels[i] + " Prev y8");
                    Assert.AreEqual(new Vector2(40f, 40f), prev.sizeDelta, labels[i] + " Prev 40×40");

                    AssertTopLeft(row, "Next", 460f, 8f, 40f, 40f);
                }
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        [Test]
        public void CloseQuickSettings_RestoresTheRightColumn()
        {
            var view = V6SelectView.Create((RectTransform)canvasGo.transform, new EmptySource(), null, null, null);
            try
            {
                view.OpenQuickSettings();
                Assert.IsNotNull(view.Root.Find("Settings Column"), "settings open");
                view.CloseQuickSettings();
                Assert.IsNull(view.Root.Find("Settings Column"), "settings closed");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }
    }
}
