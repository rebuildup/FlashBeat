using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Result;
using FlashBeat.UI.V6.Ranking;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// 固定矩形 + Ellipsis の「0 行描画」回帰 (2026-11-11 実機 r3 で Result の曲名/作家名と
    /// Ranking ヘッダーの曲名が丸ごと消えた)。
    ///
    /// 契約: TMP は矩形の実効高 (rect 高 − margin 上下) が行送りを下回ると Ellipsis で
    /// 1 文字も描かない。NotoSansJP-Medium の行送りは faceInfo.lineHeight / pointSize
    /// = 69.504 / 48 = fs × 1.448。design 矩形は h36/fs34 (49.2 必要) や h43/fs30 (43.44 必要)
    /// のように境界をわずかに下回るため、design どおりの矩形では消える。
    /// 対処は <see cref="V5Ui.FitEllipsisToLineHeight"/> (実効高だけを margin で広げ、
    /// 垂直中央揃えなので描画位置は変えない)。
    /// </summary>
    public sealed class V6EllipsisFitTests
    {
        /// <summary>NotoSansJP-Medium SDF の行送り比 (faceInfo.lineHeight / pointSize)。</summary>
        private const float ExpectedLineHeightRatio = 1.448f;

        private GameObject canvasGo;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6EllipsisFitTests Canvas", typeof(Canvas));
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

        // --- ヘルパ ---

        private static float LineHeightRatio()
        {
            var font = TMP_Settings.defaultFontAsset;
            Assert.IsNotNull(font, "TMP_Settings.defaultFontAsset");
            Assert.Greater(font.faceInfo.pointSize, 0f, "pointSize");
            return font.faceInfo.lineHeight / font.faceInfo.pointSize;
        }

        private static float EffectiveHeight(TextMeshProUGUI tmp)
        {
            // TMP の margin は (left, top, right, bottom)。
            return tmp.rectTransform.rect.height - tmp.margin.y - tmp.margin.w;
        }

        private TextMeshProUGUI Make(string text, float fs, float w, float h, bool ellipsis)
        {
            var tmp = V5Ui.Text(canvasGo, "P", text, fs, Color.white);
            var rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(w, h);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = ellipsis ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
            tmp.ForceMeshUpdate(true, true);
            return tmp;
        }

        private static TextMeshProUGUI FindChild(Transform root, string parentName, string childName)
        {
            var texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].name == childName
                    && texts[i].transform.parent != null
                    && texts[i].transform.parent.name == parentName)
                {
                    return texts[i];
                }
            }

            Assert.Fail("TMP '" + parentName + "/" + childName + "' not found");
            return null;
        }

        // --- 1. フォントメトリクスの契約 ---

        [Test]
        public void FontAsset_LineHeightIsAbout1_448TimesFontSize()
        {
            // この比が変わると Ellipsis の必要高さの計算 (FitEllipsisToLineHeight) が崩れる。
            Assert.AreEqual(ExpectedLineHeightRatio, LineHeightRatio(), 0.002f,
                "NotoSansJP-Medium SDF の行送り比");
        }

        // --- 2. 症状そのものの固定 (補正なしでは 0 行、補正で 1 行) ---

        [Test]
        public void Ellipsis_DrawsNothingBelowLineHeight_AndFitRestoresOneLine()
        {
            // Result 曲名 (fs34 / h36)・曲別ランキング曲名 (fs30 / h43)・
            // Result 作家名 (fs23 / h33) は design 矩形が行送りを下回る実例。
            float[] sizes = { 34f, 30f, 23f };
            float[] heights = { 36f, 43f, 33f };
            float[] widths = { 204f, 484f, 204f };

            for (int i = 0; i < sizes.Length; i++)
            {
                float need = LineHeightRatio() * sizes[i];
                Assert.Less(heights[i], need, "前提: design 矩形は行送りより低い (fs" + sizes[i] + ")");

                var bare = Make("ロストアンブレラ", sizes[i], widths[i], heights[i], true);
                Assert.AreEqual(0, bare.textInfo.characterCount,
                    "補正なしの Ellipsis は 0 行になる (fs" + sizes[i] + " h" + heights[i] + ")");
                Object.DestroyImmediate(bare.gameObject);

                var fitted = Make("ロストアンブレラ", sizes[i], widths[i], heights[i], true);
                V5Ui.FitEllipsisToLineHeight(fitted);
                fitted.ForceMeshUpdate(true, true);
                Assert.Greater(fitted.textInfo.characterCount, 0,
                    "補正後は描画される (fs" + sizes[i] + ")");
                Assert.AreEqual(1, fitted.textInfo.lineCount, "1 行のまま (fs" + sizes[i] + ")");
                Assert.GreaterOrEqual(EffectiveHeight(fitted), need - 0.01f,
                    "実効高が行送り以上 (fs" + sizes[i] + ")");
                Object.DestroyImmediate(fitted.gameObject);
            }
        }

        [Test]
        public void Fit_IsNoOpWhenRectAlreadyFits()
        {
            var tmp = Make("ロストアンブレラ", 20f, 320f, 29f, true);
            V5Ui.FitEllipsisToLineHeight(tmp);
            Assert.AreEqual(0f, tmp.margin.y, 0.001f, "十分な高さなら margin は触らない");
            Assert.AreEqual(0f, tmp.margin.w, 0.001f, "十分な高さなら margin は触らない");
            Assert.Greater(tmp.textInfo.characterCount, 0);
        }

        [Test]
        public void Fit_KeepsRectSizeAndGlyphBaseline()
        {
            // margin 補正は「矩形サイズ」も「描画位置」も変えない
            // (矩形自体を高くすると中央揃えで下へずれるので不可)。
            var natural = Make("ロストアンブレラ", 34f, 204f, 36f, false);
            var fitted = Make("ロストアンブレラ", 34f, 204f, 36f, true);
            V5Ui.FitEllipsisToLineHeight(fitted);
            fitted.ForceMeshUpdate(true, true);

            Assert.AreEqual(36f, fitted.rectTransform.sizeDelta.y, 0.01f, "矩形高は design のまま");
            Assert.AreEqual(natural.textInfo.characterInfo[0].bottomLeft.y,
                fitted.textInfo.characterInfo[0].bottomLeft.y, 0.01f,
                "グリフのベースラインは自然描画と同一");
        }

        // --- 3. 実画面 (Result) ---

        [Test]
        public void ResultSongInfo_TitleAndArtist_AreDrawnWithRealData()
        {
            var view = V6ResultView.Create((RectTransform)canvasGo.transform, null, null, null);
            try
            {
                var title = FindChild(view.Root, "Song Info", "Title");
                var artist = FindChild(view.Root, "Song Info", "Artist");

                // 実データ (ロストアンブレラ / 稲葉曇) を流して描画できること。
                title.text = "ロストアンブレラ";
                artist.text = "稲葉曇";
                title.ForceMeshUpdate(true, true);
                artist.ForceMeshUpdate(true, true);

                Assert.Greater(title.textInfo.characterCount, 0, "Result 曲名が描画される");
                Assert.Greater(artist.textInfo.characterCount, 0, "Result 作家名が描画される");
                Assert.AreEqual(1, title.textInfo.lineCount, "曲名は 1 行");
                Assert.AreEqual(36f, title.rectTransform.sizeDelta.y, 0.01f, "design 矩形のまま");
                Assert.AreEqual(33f, artist.rectTransform.sizeDelta.y, 0.01f, "design 矩形のまま");
                Assert.GreaterOrEqual(EffectiveHeight(title), LineHeightRatio() * title.fontSize - 0.01f);
                Assert.GreaterOrEqual(EffectiveHeight(artist), LineHeightRatio() * artist.fontSize - 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        [Test]
        public void ResultSongRanking_SongHeader_IsDrawn()
        {
            var view = V6ResultView.Create((RectTransform)canvasGo.transform, null, null, null);
            try
            {
                var song = FindChild(view.Root, "Song Ranking", "Song");
                song.text = "ロストアンブレラ";
                song.ForceMeshUpdate(true, true);
                Assert.Greater(song.textInfo.characterCount, 0, "曲別ランキングの曲名が描画される");
                Assert.AreEqual(1, song.textInfo.lineCount);
                Assert.AreEqual(43f, song.rectTransform.sizeDelta.y, 0.01f, "design 矩形のまま");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        // --- 4. 実画面 (Ranking ヘッダー) ---

        private sealed class FakeSource : IV6SelectDataSource
        {
            public readonly System.Collections.Generic.List<V6SongEntry> Songs =
                new System.Collections.Generic.List<V6SongEntry>();
            public V6SongEntry SelectedValue;

            public System.Collections.Generic.IReadOnlyList<V6SongEntry> AllSongs { get { return Songs; } }
            public System.Collections.Generic.IReadOnlyList<V6SongEntry> FilteredSongs { get { return Songs; } }
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
            public System.Collections.Generic.IReadOnlyList<V6RankingRow> RankingFor(V6SongEntry song)
            {
                return new System.Collections.Generic.List<V6RankingRow>();
            }
        }

        [Test]
        public void RankingHeader_SongTitle_IsDrawn()
        {
            var source = new FakeSource();
            var song = new V6SongEntry
            {
                SongId = 1,
                Title = "ロストアンブレラ",
                Artist = "稲葉曇",
                Level = 8,
                Bpm = 274.0,
                NoteCount = 268,
                JacketResourcePath = "",
                BestScore = -1,
            };
            source.Songs.Add(song);
            source.SelectedValue = song;

            var view = V6RankingView.Create((RectTransform)canvasGo.transform, null, source, null, null);
            try
            {
                var title = FindChild(view.Root, "Song", "Title");
                var artist = FindChild(view.Root, "Song", "Artist");
                title.ForceMeshUpdate(true, true);
                artist.ForceMeshUpdate(true, true);

                Assert.AreEqual("ロストアンブレラ", title.text);
                Assert.Greater(title.textInfo.characterCount, 0, "ヘッダー曲名が描画される");
                Assert.Greater(artist.textInfo.characterCount, 0, "ヘッダー作家名が描画される");
                Assert.AreEqual(43f, title.rectTransform.sizeDelta.y, 0.01f, "design 矩形のまま");
                Assert.AreEqual(29f, artist.rectTransform.sizeDelta.y, 0.01f, "design 矩形のまま");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }
    }
}
