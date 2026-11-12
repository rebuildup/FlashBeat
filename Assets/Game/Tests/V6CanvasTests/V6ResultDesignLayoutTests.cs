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

        // --- 判定行 (EpX3e) ---

        [Test]
        public void JudgementsRow_MatchesDesignCoordinates()
        {
            var view = CreateView();
            try
            {
                // design EpX3e: (0,448) 840×140、セル 160×140 を 168 間隔。
                // 各セルは Label (0,0) 66×29 fs20 / Value (0,43) 83×70 fs48 の左上揃え。
                // 旧実装は VerticalLayoutGroup 任せで、判定行だけ design より約 11px 下に出ていた (r13)。
                Transform block = view.Root.Find("Main/Judgements");
                AssertRect(block, 0f, 448f, 840f, 140f, "Judgements block");

                string[] labels = { "Perfect", "Great", "Bad", "Miss", "Combo" };
                float[] xs = { 0f, 168f, 336f, 504f, 672f };
                for (int i = 0; i < labels.Length; i++)
                {
                    Transform cell = block.Find("J " + labels[i]);
                    AssertRect(cell, xs[i], 0f, 160f, 140f, "J " + labels[i]);
                    AssertRect(cell.Find("Label"), 0f, 0f, 160f, 29f, labels[i] + " Label");
                    Assert.AreEqual(20f, TextOf(cell, "Label").fontSize, 0.01f,
                        labels[i] + " Label fs20");
                    AssertRect(cell.Find("Value"), 0f, 43f, 160f, 70f, labels[i] + " Value");
                    Assert.AreEqual(48f, TextOf(cell, "Value").fontSize, 0.01f,
                        labels[i] + " Value fs48");
                    Assert.AreEqual(TextAlignmentOptions.TopLeft, TextOf(cell, "Label").alignment,
                        labels[i] + " Label is top-left (design)");
                    Assert.AreEqual(TextAlignmentOptions.TopLeft, TextOf(cell, "Value").alignment,
                        labels[i] + " Value is top-left (design)");
                }
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

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
                Assert.AreEqual(20f, TextOf(dialog, "Name Prompt").fontSize, 0.01f, "Prompt fs20");
                AssertRect(dialog.Find("Name Field"), 28f, 112f, 784f, 84f, "Name Field");
                AssertRect(dialog.Find("Name Field/Value"), 24f, 20f, 736f, 58f, "Name Field Value");
                Assert.AreEqual(40f, TextOf(dialog, "Name Field/Value").fontSize, 0.01f, "Value fs40");
                // design lqHaN は未入力の Name Field に "YOU" を $text-dim で置く
                // (v5 ResultView の Placeholder と同じ)。#384 で入力は TMP_InputField に
                // 任せ、空欄時は Placeholder テキストが "YOU" を出す。
                var placeholder = TextOf(dialog, "Name Field/Placeholder");
                Assert.AreEqual(V6ResultLogic.LocalNameDefault, placeholder.text,
                    "Name Field placeholder YOU");
                Assert.AreEqual(FlashBeat.UI.V5.V5Tokens.TextDim, placeholder.color,
                    "Name Field placeholder is $text-dim");
                var nameInput = dialog.Find("Name Field").GetComponent<TMP_InputField>();
                Assert.IsNotNull(nameInput, "Name Field is a TMP_InputField");
                Assert.AreSame(placeholder, nameInput.placeholder, "placeholder is wired");
                Assert.AreEqual(2, nameInput.caretWidth, "caret width 2");
                Assert.AreEqual(TMP_InputField.ContentType.Standard, nameInput.contentType,
                    "IME input is allowed");
                AssertRect(dialog.Find("Summary"), 28f, 264f, 784f, 140f, "Summary");
                AssertRect(dialog.Find("Buttons"), 28f, 432f, 784f, 50f, "Buttons");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        [Test]
        public void RegisterModal_InputHints_MatchDesign()
        {
            // design: Input Hints (28,208) 784×32 / Hint Cap (0,0) 104×32 /
            // Hint Text (128,4.5) fs16 / Legend · 移動 (509,3) KeyCap 57×26 + Label (65,0) /
            // Legend · 削除 (634,3) KeyCap 106×26 + Label (114,0)。
            var view = CreateView();
            try
            {
                Transform hints = view.Root.Find("Modal 05b/Dialog/Input Hints");
                Assert.IsNotNull(hints, "Input Hints");
                AssertRect(hints, 28f, 208f, 784f, 32f, "Input Hints");
                AssertRect(hints.Find("Hint Cap"), 0f, 0f, 104f, 32f, "Hint Cap");
                AssertRect(hints.Find("Hint Cap/Key"), 12f, 1.5f, 80f, 29f, "Hint Cap Key");
                AssertRect(hints.Find("Hint Text"), 128f, 4.5f, 200f, 23f, "Hint Text");

                Transform move = hints.Find("Legend 移動");
                Assert.IsNotNull(move, "Legend 移動");
                AssertRect(move, 509f, 3f, 101f, 26f, "Legend 移動");
                AssertRect(move.Find("KeyCap"), 0f, 0f, 57f, 26f, "移動 KeyCap");
                AssertRect(move.Find("Label"), 65f, 0f, 36f, 26f, "移動 Label");

                Transform erase = hints.Find("Legend 削除");
                Assert.IsNotNull(erase, "Legend 削除");
                AssertRect(erase, 634f, 3f, 150f, 26f, "Legend 削除");
                AssertRect(erase.Find("KeyCap"), 0f, 0f, 106f, 26f, "削除 KeyCap");
                AssertRect(erase.Find("Label"), 114f, 0f, 36f, 26f, "削除 Label");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        [Test]
        public void RegisterModal_Summary_MatchDesign()
        {
            // design: Thumb (16,20) 88×50 / Title (120,18) h38 fs26 / Artist (120,52) h26 fs18 /
            // Grade (704,38) 64×40 / Divider (16,94) 752×1 /
            // Record Label (16,104) h23 fs16 / Record Value (440,102) 180×29 fs20 右寄せ /
            // Record Note (640,104) 113×23 fs16 /
            // Score Group (400,26) 288×52: Label (91,14.5) 51×23 fs16 / Value (156,1.5) 132×49 fs34。
            var view = CreateView();
            try
            {
                Transform summary = view.Root.Find("Modal 05b/Dialog/Summary");
                Assert.IsNotNull(summary, "Summary");
                AssertRect(summary.Find("Thumb"), 16f, 20f, 88f, 50f, "Thumb");
                AssertRect(summary.Find("Title"), 120f, 18f, 568f, 38f, "Title");
                Assert.AreEqual(26f, TextOf(summary, "Title").fontSize, 0.01f, "Title fs26");
                AssertRect(summary.Find("Artist"), 120f, 52f, 568f, 26f, "Artist");
                Assert.AreEqual(18f, TextOf(summary, "Artist").fontSize, 0.01f, "Artist fs18");
                AssertRect(summary.Find("Grade"), 704f, 38f, 64f, 40f, "Grade");
                AssertRect(summary.Find("Divider"), 16f, 94f, 752f, 1f, "Divider");
                AssertRect(summary.Find("Record Label"), 16f, 104f, 300f, 23f, "Record Label");
                Assert.AreEqual(16f, TextOf(summary, "Record Label").fontSize, 0.01f, "Record Label fs16");
                AssertRect(summary.Find("Record Value"), 440f, 102f, 180f, 29f, "Record Value");
                Assert.AreEqual(TextAlignmentOptions.Right, TextOf(summary, "Record Value").alignment,
                    "Record Value right aligned");
                AssertRect(summary.Find("Record Note"), 640f, 104f, 113f, 23f, "Record Note");

                Transform score = summary.Find("Score Group");
                Assert.IsNotNull(score, "Score Group");
                AssertRect(score, 400f, 26f, 288f, 52f, "Score Group");
                AssertRect(score.Find("Score Label"), 91f, 14.5f, 51f, 23f, "Score Label");
                Assert.AreEqual(16f, TextOf(score, "Score Label").fontSize, 0.01f, "Score Label fs16");
                AssertRect(score.Find("Score Value"), 156f, 1.5f, 132f, 49f, "Score Value");
                Assert.AreEqual(34f, TextOf(score, "Score Value").fontSize, 0.01f, "Score Value fs34");
                Assert.AreEqual(TextAlignmentOptions.Left, TextOf(score, "Score Value").alignment,
                    "Score Value left aligned (design の text は alignment 未指定 = 既定)");
                Assert.AreEqual(TextWrappingModes.NoWrap, TextOf(score, "Score Value").textWrappingMode,
                    "Score Value must not wrap");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        [Test]
        public void RegisterModal_Buttons_MatchDesign()
        {
            // design: Buttons (28,432) 784×50 / Skip (318,0) 198×50 /
            // Register (532,0) 252×50。
            // Skip 内: Icon (24,13) 24 / Label (60,6) 54×38 fs26 / KeyCap (126,10.5) 48×29。
            // Register 内: Icon (18.5,13) 24 / Label (54.5,6) 101×38 fs26 /
            // KeyCap (167.5,10.5) 66×29。
            var view = CreateView();
            try
            {
                var buttons = view.Root.Find("Modal 05b/Dialog/Buttons");
                Assert.IsNotNull(buttons, "Buttons");
                var skip = buttons.Find("Button Skip");
                Assert.IsNotNull(skip, "Button Skip");
                AssertRect(skip, 318f, 0f, 198f, 50f, "Button Skip");
                AssertRect(skip.Find("Icon"), 24f, 13f, 24f, 24f, "Skip Icon");
                AssertRect(skip.Find("Label"), 60f, 6f, 54f, 38f, "Skip Label");
                Assert.AreEqual(26f, TextOf(skip, "Label").fontSize, 0.01f, "Skip fs26");
                AssertRect(skip.Find("KeyCap"), 126f, 10.5f, 48f, 29f, "Skip KeyCap");

                var register = buttons.Find("Button Register");
                Assert.IsNotNull(register, "Button Register");
                AssertRect(register, 532f, 0f, 252f, 50f, "Button Register");
                AssertRect(register.Find("Icon"), 18.5f, 13f, 24f, 24f, "Register Icon");
                AssertRect(register.Find("Label"), 54.5f, 6f, 101f, 38f, "Register Label");
                Assert.AreEqual(26f, TextOf(register, "Label").fontSize, 0.01f, "Register fs26");
                AssertRect(register.Find("KeyCap"), 167.5f, 10.5f, 66f, 29f, "Register KeyCap");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }
    }
}
