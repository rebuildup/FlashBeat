using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Result;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// Result 画面の design 再現検証 (2026-11-11 再現品質レビュー、正本 zcSHh)。
    /// - 楽曲情報 (KKIxx (0,636) 840×120): Thumb (16,28) 112×63 /
    ///   Title (144,16) / Artist (144,66) / Stat ×4 (348/468/588/708, 20) 116×64
    ///   (Label (0,0) h26 / Value (0,28) h43)
    /// - 曲別ランキング (PdoPy (856,24) 528×412): Head (8,8) 512×36 の Title (12,5) h26、
    ///   Song (20,52) fs30、行 (8,92+52k) 496×44 = 共通部品 Chlqd / jnIY9
    /// - 行内部: Pos (12,4) 36×36 / Name (60,6) 140×32 / Value (212,6) 180×32 右寄せ /
    ///   Grade (404,4) 56×36
    /// </summary>
    public sealed class V6ResultDesignLayoutTests
    {
        private GameObject canvasGo;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6ResultDesignLayoutTests Canvas", typeof(Canvas));
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

        /// <summary>pivot に依らない矩形の左上 (design 座標)。V6ResultView の At(pivot 付き) は
        /// anchoredPosition に pivot 補正を掛けるため、左上は anchoredPosition と pivot から戻す。</summary>
        private static Vector2 DesignPos(Transform t)
        {
            var rt = (RectTransform)t;
            return new Vector2(
                rt.anchoredPosition.x - rt.pivot.x * rt.sizeDelta.x,
                -rt.anchoredPosition.y - (1f - rt.pivot.y) * rt.sizeDelta.y);
        }

        private static void AssertRect(Transform t, float x, float y, float w, float h, string label)
        {
            Assert.IsNotNull(t, label + " not found");
            var rt = (RectTransform)t;
            Assert.AreEqual(x, DesignPos(t).x, 0.01f, label + " x");
            Assert.AreEqual(y, DesignPos(t).y, 0.01f, label + " y");
            Assert.AreEqual(w, rt.sizeDelta.x, 0.01f, label + " width");
            Assert.AreEqual(h, rt.sizeDelta.y, 0.01f, label + " height");
        }

        private static TextMeshProUGUI TextOf(Transform t, string path)
        {
            Transform child = t.Find(path);
            Assert.IsNotNull(child, "child not found: " + path + " under " + t.name);
            var tmp = child.GetComponent<TextMeshProUGUI>();
            Assert.IsNotNull(tmp, path + " must be a TMP text");
            return tmp;
        }

        private V6ResultView CreateView()
        {
            return V6ResultView.Create((RectTransform)canvasGo.transform, null, null, null);
        }

        // --- 楽曲情報バンド (KKIxx) ---

        [Test]
        public void SongInfoBand_MatchesDesignCoordinates()
        {
            var view = CreateView();
            try
            {
                Transform band = view.Root.Find("Main/Song Info");
                Assert.IsNotNull(band, "Main/Song Info");

                AssertRect(band.Find("Thumb"), 16f, 28f, 112f, 63f, "Thumb");
                AssertRect(band.Find("Title"), 144f, 16f, V6ResultLogic.SongTitleW, 36f, "Title");
                AssertRect(band.Find("Artist"), 144f, 66f, V6ResultLogic.SongArtistW, 33f, "Artist");

                float[] statX = { 348f, 468f, 588f, 708f };
                string[] labels = { "BPM", "NOTES", "LEVEL", "曲長" };
                for (int i = 0; i < statX.Length; i++)
                {
                    Transform cell = band.Find("Stat " + labels[i]);
                    AssertRect(cell, statX[i], 20f, 116f, 64f, "Stat " + labels[i]);
                    AssertRect(cell.Find("Label"), 0f, 0f, 116f, 26f, "Label " + i);
                    AssertRect(cell.Find("Value"), 0f, 28f, 116f, 43f, "Value " + i);
                }

                // 曲名・作家名は Stat 列 (x348) の手前で止まり、縦の衝突を horizontal 分割で解く。
                Assert.LessOrEqual(V6ResultLogic.SongTitleX + V6ResultLogic.SongTitleW, statX[0],
                    "Title must end before the first stat column");
                Assert.LessOrEqual(V6ResultLogic.SongArtistX + V6ResultLogic.SongArtistW, statX[0],
                    "Artist must end before the first stat column");

                // 実データは design のサンプルより長いので 1 行固定 + ellipsis。
                var title = TextOf(band, "Title");
                Assert.AreEqual(TextWrappingModes.NoWrap, title.textWrappingMode, "Title must not wrap");
                Assert.AreEqual(TextOverflowModes.Ellipsis, title.overflowMode, "Title must ellipsize");
                var artist = TextOf(band, "Artist");
                Assert.AreEqual(TextWrappingModes.NoWrap, artist.textWrappingMode, "Artist must not wrap");
                Assert.AreEqual(TextOverflowModes.Ellipsis, artist.overflowMode, "Artist must ellipsize");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        // --- 曲別ランキング (PdoPy) ---

        [Test]
        public void SongRankingPanel_HeadAndSongTitle_MatchDesign()
        {
            var view = CreateView();
            try
            {
                Transform panel = view.Root.Find("Main/Song Ranking");
                Assert.IsNotNull(panel, "Main/Song Ranking");

                AssertRect(panel, V6ResultLogic.RankX, 24f, 528f, 412f, "panel");
                Transform head = panel.Find("Head");
                AssertRect(head, 8f, 8f, 512f, 36f, "Head");
                AssertRect(head.Find("Title"), 12f, 5f, V6ResultLogic.RankHeadTitleW, 26f, "Head Title");

                // design Lo2pD: 見出し下の曲名 (20,52) fs30。150 は hug 幅なので実装は行幅まで。
                var song = TextOf(panel, "Song");
                AssertRect(song.transform, 20f, 52f, 484f, 43f, "Song");
                Assert.AreEqual(30f, song.fontSize, 0.01f, "Song fs");
                Assert.AreEqual(TextWrappingModes.NoWrap, song.textWrappingMode, "Song must not wrap");
                Assert.AreEqual(TextOverflowModes.Ellipsis, song.overflowMode, "Song must ellipsize");

                // 空状態は Song 見出しの下 (行領域の中央) に置き、曲名と重ならない。
                Transform empty = panel.Find("No Data");
                Assert.IsNotNull(empty, "No Data");
                Assert.GreaterOrEqual(DesignPos(empty).y,
                    V6ResultLogic.SongRankTitleY + V6ResultLogic.SongRankTitleH, "empty state below Song");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        // --- コンパクトなランキング行 (Chlqd = jnIY9) ---

        [Test]
        public void CompactRankRow_MatchesDesignInternals()
        {
            var parent = new GameObject("Row Parent", typeof(RectTransform), typeof(Canvas));
            try
            {
                var row = V6Lists.CompactRankRow(parent, "1", "AKI", "1,000,000", "SS",
                    new Vector2(8f, 92f));
                var t = row.transform;

                AssertRect(t, 8f, 92f, 496f, 44f, "row");
                AssertRect(t.Find("Pos"), 12f, 4f, 36f, 36f, "Pos");
                AssertRect(t.Find("Name"), 60f, 6f, 140f, 32f, "Name");
                AssertRect(t.Find("Value"), 212f, 6f, 180f, 32f, "Value");
                AssertRect(t.Find("Grade"), 404f, 4f, 56f, 36f, "Grade");

                Assert.AreEqual(TextAlignmentOptions.Right, TextOf(t, "Value").alignment, "Value right aligned");
                Assert.AreEqual(22f, TextOf(t, "Name").fontSize, 0.01f, "Name fs");

                // 選択行 (YOU) は背景 $k6 + Pos 数字 $text、非選択は $k1 + $text-muted。
                Assert.AreEqual(V6Tokens.Key1, row.GetComponent<Image>().color, "unselected bg");
                Assert.AreEqual(V6Tokens.TextMuted, TextOf(t, "Pos/PosText").color, "unselected Pos");
                var selected = V6Lists.CompactRankRow(parent, "2", "YOU", "988,513", "S",
                    new Vector2(8f, 144f), selected: true);
                Assert.AreEqual(V6Tokens.Key6, selected.GetComponent<Image>().color, "selected bg");
                Assert.AreEqual(V6Tokens.Text, TextOf(selected.transform, "Pos/PosText").color, "selected Pos");

                // 右端 404 + 56 = 460、行幅 496 まで 36 の余白 (design 実測)。
                Assert.AreEqual(V6Lists.CompactRankRowW,
                    V6Lists.CompactRankGradeX + V6Lists.CompactRankGradeW + 36f, "right padding");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        // --- 登録モーダル (05b lqHaN) ---

        [Test]
        public void RegisterModal_MatchesDesignInternals()
        {
            // design: Dialog (444,231) 840×510、Head (28,28) 180×43 fs30、
            // Name Prompt (404,38) 380×29、Name Field (28,112) 784×84、
            // Summary (28,264) 784×140、Buttons (28,432) 784×50。
            // 旧実装は Head/Prompt を pivot(0,0.5) で置き、design の上端より 20px 上に描いていた。
            var view = CreateView();
            try
            {
                var modal = view.Root.Find("Modal 05b");
                Assert.IsNotNull(modal, "modal root");
                var dialog = modal.Find("Dialog");
                AssertRect(dialog, V6ResultLogic.ModalDialogX, V6ResultLogic.ModalDialogY,
                    V6ResultLogic.ModalDialogW, V6ResultLogic.ModalDialogH, "Dialog");

                AssertRect(dialog.Find("Head"), 28f, 28f, 180f, 43f, "Head");
                Assert.AreEqual(30f, TextOf(dialog, "Head").fontSize, 0.01f, "Head fs30");
                AssertRect(dialog.Find("Name Prompt"), 404f, 38f, 380f, 29f, "Name Prompt");
                AssertRect(dialog.Find("Name Field"), 28f, 112f, 784f, 84f, "Name Field");
                AssertRect(dialog.Find("Summary"), 28f, 264f, 784f, 140f, "Summary");
                AssertRect(dialog.Find("Buttons"), 28f, 432f, 784f, 50f, "Buttons");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }
    }
}
