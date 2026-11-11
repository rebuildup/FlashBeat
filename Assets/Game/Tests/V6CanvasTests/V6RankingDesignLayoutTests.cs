using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Ranking;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// Ranking / 曲リスト行の design 再現検証 (2026-11-11 再現品質レビュー)。
    /// 正本は Pencil design ノードの resolved bounds (flashbeat-v6-nodes.json):
    /// - A 曲行: mcCOI (Ranking) / Odv71 (Select) = 504×72、Level (12,14) /
    ///   Thumb (68,9) / Texts (176,2) 248×69 / Grade (436,18)
    /// - B ランキング行: Hbxqf (496×56) / T8C1Hu (496×52)、Pos 12 / Name 60 w160 /
    ///   Value 232 w180 / Grade 424 w56、縦中央寄せ
    /// - C PLAYERS 行: tcgRX = 同座標 + Plays 424 w56 fs20 右寄せ
    /// - D SCORE RANKING ヘッド: H8yC4 (8,8) 496×40、Title (12,7) 139×26 /
    ///   Song チップ (159,4) 159×32 (Text (48,0) 111×32) / Segment (335,0) 156×40
    ///   (Local (4,4) / Online (80,4) 72×32)。パネル qICtu/smhrU は $k2 r8。
    /// - E PLAYERS ヘッド: g2307 (8,8) 496×40、Title (12,7) 74×26 /
    ///   Metric (354,7) 90×26。本文は空状態「データがありません」。
    /// - F ドットレール: vQc4K (536,136) 幅10 縦 gap4 (ピッチ 12)、
    ///   ドット 8×8 / 選択 10×10、x はレール中央 ((10-d)/2)。
    /// </summary>
    public sealed class V6RankingDesignLayoutTests
    {
        // --- テスト用データソース (ビューが使うメンバーだけを実値で返す) ---

        private sealed class FakeDataSource : IV6SelectDataSource
        {
            public readonly List<V6SongEntry> Songs = new List<V6SongEntry>();
            public readonly List<V6RankingRow> OnlineRows = new List<V6RankingRow>();
            public V6SongEntry SelectedValue;

            public IReadOnlyList<V6SongEntry> AllSongs { get { return Songs; } }
            public IReadOnlyList<V6SongEntry> FilteredSongs { get { return Songs; } }
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

            public IReadOnlyList<V6RankingRow> RankingFor(V6SongEntry song)
            {
                return song != null && song.IsCloud ? OnlineRows : new List<V6RankingRow>();
            }
        }

        private GameObject canvasGo;
        private FakeDataSource data;

        [SetUp]
        public void SetUp()
        {
            data = new FakeDataSource();
            canvasGo = new GameObject("V6RankingDesignLayoutTests Canvas", typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }
            canvasGo = null;
            data = null;
        }

        // --- ヘルパ ---

        /// <summary>design 親内の左上座標 (pivot 補正込み。anchor (0,1) 前提)。</summary>
        private static Vector2 DesignPos(Transform t)
        {
            var rt = (RectTransform)t;
            return new Vector2(
                rt.anchoredPosition.x - rt.pivot.x * rt.sizeDelta.x,
                -rt.anchoredPosition.y - (1f - rt.pivot.y) * rt.sizeDelta.y);
        }

        private static void AssertRect(Transform t, float x, float y, float w, float h, string label)
        {
            var rt = (RectTransform)t;
            Assert.AreEqual(x, DesignPos(t).x, 0.01f, label + " x");
            Assert.AreEqual(y, DesignPos(t).y, 0.01f, label + " y");
            Assert.AreEqual(w, rt.sizeDelta.x, 0.01f, label + " width");
            Assert.AreEqual(h, rt.sizeDelta.y, 0.01f, label + " height");
        }

        private static TextMeshProUGUI TextOf(Transform t, string childName)
        {
            Transform child = t.Find(childName);
            Assert.IsNotNull(child, "child not found: " + childName + " under " + t.name);
            var tmp = child.GetComponent<TextMeshProUGUI>();
            Assert.IsNotNull(tmp, childName + " must be a TMP text");
            return tmp;
        }

        private static V6SongEntry MakeSong(int songId, string title, bool isCloud, int bestScore = -1)
        {
            return new V6SongEntry
            {
                SongId = songId,
                Title = title,
                Artist = "Artist",
                Level = 10,
                JacketResourcePath = "",
                BestScore = bestScore,
                BestGrade = bestScore > 0 ? "S" : "",
                Bpm = 120.0,
                NoteCount = 500,
                IsCloud = isCloud,
            };
        }

        private V6RankingView CreateView()
        {
            return V6RankingView.Create((RectTransform)canvasGo.transform, null, data, null, null);
        }

        // --- A. 曲リスト行 (mcCOI 504×72) ---

        [Test]
        public void SongRow_MatchesDesignInternals()
        {
            var parent = new GameObject("Row Parent", typeof(RectTransform), typeof(Canvas));
            try
            {
                var row = V6Lists.SongRow(parent, "Title", "Composer", "12", "S",
                    new Vector2(16f, 48f), new Vector2(504f, 72f));
                var t = row.transform;

                AssertRect(t, 16f, 48f, 504f, 72f, "row");
                AssertRect(t.Find("Level"), 12f, 14f, 44f, 44f, "Level");
                AssertRect(t.Find("Thumb"), 68f, 9f, 96f, 54f, "Thumb");
                AssertRect(t.Find("Title"), 176f, 2f, 248f, 38f, "Title");
                AssertRect(t.Find("Composer"), 176f, 42f, 248f, 29f, "Composer");
                AssertRect(t.Find("Grade"), 436f, 18f, 56f, 36f, "Grade");

                // 曲名・作家名は 1 行固定 + Ellipsis (2 行折り返しで行内が破綻しない)。
                var title = TextOf(t, "Title");
                Assert.AreEqual(TextWrappingModes.NoWrap, title.textWrappingMode, "Title must not wrap");
                Assert.AreEqual(TextOverflowModes.Ellipsis, title.overflowMode, "Title must ellipsize");
                Assert.AreEqual(26f, title.fontSize, 0.01f, "Title fs");
                var composer = TextOf(t, "Composer");
                Assert.AreEqual(TextWrappingModes.NoWrap, composer.textWrappingMode, "Composer must not wrap");
                Assert.AreEqual(TextOverflowModes.Ellipsis, composer.overflowMode, "Composer must ellipsize");
                Assert.AreEqual(20f, composer.fontSize, 0.01f, "Composer fs");

                // ジャケット枠 / グレード枠は呼び出し側ヘルパで取得できる。
                Assert.AreSame(t.Find("Thumb").gameObject, V6Lists.Thumb(row));
                Assert.AreSame(t.Find("Grade").gameObject, V6Lists.Grade(row));

                // 選択行は背景 $k6、非選択は $k1。
                var selected = V6Lists.SongRow(parent, "T", "C", "1", "S",
                    new Vector2(16f, 124f), new Vector2(504f, 72f), selected: true);
                Assert.AreEqual(V6Tokens.Key1, row.GetComponent<Image>().color);
                Assert.AreEqual(V6Tokens.Key6, selected.GetComponent<Image>().color);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void SongRow_Constants_MatchDesignValues()
        {
            Assert.AreEqual(504f, V6Lists.SongRowW, "row w");
            Assert.AreEqual(72f, V6Lists.SongRowH, "row h");
            Assert.AreEqual(12f, V6Lists.SongLevelX, "Level x");
            Assert.AreEqual(14f, V6Lists.SongLevelY, "Level y");
            Assert.AreEqual(68f, V6Lists.SongThumbX, "Thumb x");
            Assert.AreEqual(9f, V6Lists.SongThumbY, "Thumb y");
            Assert.AreEqual(176f, V6Lists.SongTextsX, "Texts x");
            Assert.AreEqual(2f, V6Lists.SongTextsY, "Texts y");
            Assert.AreEqual(248f, V6Lists.SongTextsW, "Texts w");
            Assert.AreEqual(38f, V6Lists.SongTitleH, "Title rect h (fs26 の行送り以上を確保)");
            Assert.AreEqual(29f, V6Lists.SongComposerH, "Composer rect h");
            Assert.AreEqual(436f, V6Lists.SongGradeX, "Grade x");
            Assert.AreEqual(18f, V6Lists.SongGradeY, "Grade y");

            // design (padding [0,12] gap 12) の水平スタック:
            // Level 12 → Thumb 68 → Texts 176 → Grade 436、右端 492 + padding 12 = 504。
            float gap = V6Lists.RowGap;
            Assert.AreEqual(12f, gap, "design gap 12");
            Assert.AreEqual(V6Lists.RowPaddingX + V6Lists.SongLevelSize + gap, V6Lists.SongThumbX, "Level → Thumb");
            Assert.AreEqual(V6Lists.SongThumbX + V6Lists.SongThumbW + gap, V6Lists.SongTextsX, "Thumb → Texts");
            Assert.AreEqual(V6Lists.SongTextsX + V6Lists.SongTextsW + gap, V6Lists.SongGradeX, "Texts → Grade");
            Assert.AreEqual(V6Lists.SongGradeX + V6Lists.SongGradeW + V6Lists.RowPaddingX,
                V6Lists.SongRowW, "right padding closes the row width");
        }

        // --- B/C. ランキング行・プレイヤー行 (Hbxqf / T8C1Hu / tcgRX) ---

        [Test]
        public void RankRow_MatchesDesignInternals_At56And52()
        {
            var parent = new GameObject("Row Parent", typeof(RectTransform), typeof(Canvas));
            try
            {
                // Hbxqf: 496×56、Pos y10 / Name y12 / Grade y10。
                var row56 = V6Lists.RankRow(parent, "1", "AKI", "1,000,000", "S",
                    new Vector2(8f, 60f), new Vector2(496f, 56f));
                var t56 = row56.transform;
                AssertRect(t56.Find("Pos"), 12f, 10f, 36f, 36f, "Pos@56");
                AssertRect(t56.Find("Name"), 60f, 12f, 160f, 32f, "Name@56");
                AssertRect(t56.Find("Value"), 232f, 12f, 180f, 32f, "Value@56");
                AssertRect(t56.Find("Grade"), 424f, 10f, 56f, 36f, "Grade@56");
                Assert.AreEqual(TextAlignmentOptions.Right, TextOf(t56, "Value").alignment, "Value is right aligned");

                // T8C1Hu: 496×52、Pos y8 / Name y10 / Grade y8 (内部 x は同じ)。
                var row52 = V6Lists.RankRow(parent, "2", "RIN", "900,000", "A",
                    new Vector2(8f, 60f), new Vector2(496f, 52f));
                var t52 = row52.transform;
                AssertRect(t52.Find("Pos"), 12f, 8f, 36f, 36f, "Pos@52");
                AssertRect(t52.Find("Name"), 60f, 10f, 160f, 32f, "Name@52");
                AssertRect(t52.Find("Value"), 232f, 10f, 180f, 32f, "Value@52");
                AssertRect(t52.Find("Grade"), 424f, 8f, 56f, 36f, "Grade@52");

                // Pos の数字色: 選択行 $text / 非選択 $text-muted。
                var selected = V6Lists.RankRow(parent, "1", "YOU", "1", "S",
                    new Vector2(8f, 124f), new Vector2(496f, 56f), selected: true);
                Assert.AreEqual(V6Tokens.TextMuted, TextOf(t56, "Pos/Pos").color, "unselected Pos");
                Assert.AreEqual(V6Tokens.Text, TextOf(selected.transform, "Pos/Pos").color, "selected Pos");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void PlayerRow_MatchesDesignInternals()
        {
            var parent = new GameObject("Row Parent", typeof(RectTransform), typeof(Canvas));
            try
            {
                // tcgRX: 496×56、RankRow と同座標 + Plays (424,中央) 56×29 fs20 右寄せ。
                var row = V6Lists.PlayerRow(parent, "1", "AKI", "4,120,880", "12曲",
                    new Vector2(8f, 60f), new Vector2(496f, 56f));
                var t = row.transform;
                AssertRect(t.Find("Pos"), 12f, 10f, 36f, 36f, "Pos");
                AssertRect(t.Find("Name"), 60f, 12f, 160f, 32f, "Name");
                AssertRect(t.Find("Value"), 232f, 12f, 180f, 32f, "Value");
                AssertRect(t.Find("Plays"), 424f, 13.5f, 56f, 29f, "Plays");
                var plays = TextOf(t, "Plays");
                Assert.AreEqual(TextAlignmentOptions.Right, plays.alignment, "Plays right aligned");
                Assert.AreEqual(20f, plays.fontSize, 0.01f, "Plays fs20");
                Assert.AreEqual(V6Tokens.TextDim, plays.color, "Plays uses $text-dim");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void RankRowConstants_MatchDesignValues()
        {
            Assert.AreEqual(496f, V6Lists.RankRowW, "row w");
            Assert.AreEqual(56f, V6Lists.RankRowH, "row h (Hbxqf。History は 52 を size で渡す)");
            Assert.AreEqual(36f, V6Lists.RankPosSize, "Pos size");
            Assert.AreEqual(160f, V6Lists.RankNameW, "Name w (design 160。doc §5 の 140 は Select の行の値)");
            Assert.AreEqual(180f, V6Lists.RankValueW, "Value w");
            Assert.AreEqual(56f, V6Lists.RankGradeW, "Grade w");
            Assert.AreEqual(22f, V6Lists.RankFs, "row fs");
            Assert.AreEqual(56f, V6Lists.PlayerPlaysW, "Plays w");
            Assert.AreEqual(20f, V6Lists.PlayerPlaysFs, "Plays fs");
        }

        // --- D. SCORE RANKING ヘッド (H8yC4) / パネル角丸 (qICtu r8) ---

        [Test]
        public void ScoreRankingPanelAndHead_MatchDesign()
        {
            data.Songs.Add(MakeSong(1, "Song 1", isCloud: true, bestScore: 100000));
            data.Select(1);
            data.OnlineRows.Add(new V6RankingRow
            {
                Position = 1,
                PlayerName = "Player",
                Score = 100000,
                Grade = "S",
                IsYou = false,
            });

            var view = CreateView();
            try
            {
                Transform panel = view.Root.Find("Main/Score Ranking");
                Assert.IsNotNull(panel, "Score Ranking panel");
                AssertRect(panel, 584f, 24f, 512f, 732f, "panel");
                Assert.AreEqual("V5Rounded8", panel.GetComponent<Image>().sprite.name,
                    "panel corner radius must be 8 (design qICtu, not 16)");

                Transform head = panel.Find("Head");
                Assert.IsNotNull(head, "Head");
                AssertRect(head, 8f, 8f, 496f, 40f, "head");

                var label = TextOf(head, "Label");
                Assert.AreEqual("SCORE RANKING", label.text, "head title");
                // design qICtu は 1 行。折り返すと "SCORE" / "RANKING" の 2 行になる (r21 実機)。
                Assert.AreEqual(TextWrappingModes.NoWrap, label.textWrappingMode,
                    "SCORE RANKING は 1 行固定 (design どおり)");
                AssertRect(label.transform, 12f, 7f, 139f, 26f, "head title rect");

                // Song チップ (159,4) 159×32 / Text (48,0) 111×32 1 行固定 ellipsis。
                Transform chip = head.Find("Song");
                Assert.IsNotNull(chip, "Song chip must exist when a song is selected");
                AssertRect(chip, 159f, 4f, 159f, 32f, "Song chip");
                var chipText = TextOf(chip, "Text");
                AssertRect(chipText.transform, 48f, 0f, 111f, 32f, "chip text");
                Assert.AreEqual(TextWrappingModes.NoWrap, chipText.textWrappingMode, "chip text 1 line");
                Assert.AreEqual(TextOverflowModes.Ellipsis, chipText.overflowMode, "chip text ellipsis");

                // Segment (335,0) 156×40 / Local (4,4) 72×32 / Online (80,4) 72×32。
                Transform segment = head.Find("Segment");
                Assert.IsNotNull(segment, "Segment");
                AssertRect(segment, 335f, 0f, 156f, 40f, "Segment");
                AssertRect(segment.Find("Segment Local"), 4f, 4f, 72f, 32f, "Local chip");
                AssertRect(segment.Find("Segment Online"), 80f, 4f, 72f, 32f, "Online chip");

                // 行は V6Lists.RankRow (496×56, y=60+64k)。
                Transform rankRow = panel.Find("RankRow 1");
                Assert.IsNotNull(rankRow, "rank row built by V6Lists.RankRow");
                AssertRect(rankRow, 8f, 60f, 496f, 56f, "rank row");
            }
            finally
            {
                view.Dispose();
            }
        }

        [Test]
        public void ScoreRankingHead_NoSong_DropsChipButKeepsSegmentPosition()
        {
            data.Songs.Add(MakeSong(1, "Song 1", isCloud: false));

            var view = CreateView();
            try
            {
                Transform head = view.Root.Find("Main/Score Ranking/Head");
                Assert.IsNotNull(head, "Head");
                Assert.IsNull(head.Find("Song"), "no song selected → no Song chip");
                AssertRect(head.Find("Segment"), 335f, 0f, 156f, 40f, "Segment stays at design x");

                // ローカル曲 (spec 167): Online 非表示でも Local は (4,4) のまま。
                Transform segment = head.Find("Segment");
                Assert.IsNull(segment.Find("Segment Online"), "local song hides Online chip");
                AssertRect(segment.Find("Segment Local"), 4f, 4f, 72f, 32f, "Local chip keeps (4,4)");
            }
            finally
            {
                view.Dispose();
            }
        }

        // --- E. PLAYERS ヘッド (g2307) / 空状態 ---

        [Test]
        public void PlayersHead_MatchesDesign_AndUsesHonestEmptyState()
        {
            data.Songs.Add(MakeSong(1, "Song 1", isCloud: false));

            var view = CreateView();
            try
            {
                Transform panel = view.Root.Find("Main/Players");
                Assert.IsNotNull(panel, "Players panel");
                AssertRect(panel, 1144f, 24f, 512f, 732f, "panel");
                Assert.AreEqual("V5Rounded8", panel.GetComponent<Image>().sprite.name,
                    "panel corner radius must be 8 (design smhrU, not 16)");

                Transform head = panel.Find("Head");
                Assert.IsNotNull(head, "Head");
                AssertRect(head, 8f, 8f, 496f, 40f, "head");

                var title = TextOf(head, "Title");
                Assert.AreEqual("PLAYERS", title.text, "head title");
                Assert.AreEqual(TextWrappingModes.NoWrap, title.textWrappingMode,
                    "PLAYERS は 1 行固定 (折り返すと PLAYER / S に割れる)");
                AssertRect(title.transform, 12f, 7f, 74f, 26f, "title rect");
                Assert.AreEqual(V6Tokens.TextDim, title.color, "title uses $text-dim");

                // design の見出し構成 = Title + 右寄せ「累計スコア」(矢印は描かない)。
                var metric = TextOf(head, "Metric");
                Assert.AreEqual("累計スコア", metric.text, "metric label");
                Assert.AreEqual(TextWrappingModes.NoWrap, metric.textWrappingMode,
                    "累計スコアは 1 行固定");
                AssertRect(metric.transform, 354f, 7f, 90f, 26f, "metric rect");
                Assert.AreEqual(V6Tokens.Text, metric.color, "metric uses $text");

                // 左右矢印 (qPD75/BUt9A) は操作先が無いので描かない。
                foreach (Transform child in head)
                {
                    Assert.IsTrue(child.name == "Title" || child.name == "Metric",
                        "unexpected PLAYERS head child (arrows must not be drawn): " + child.name);
                }

                // 本文は Select と同じ正直な空状態。実装都合の文言は出さない。
                var texts = new List<string>();
                foreach (TMP_Text t in panel.GetComponentsInChildren<TMP_Text>(true))
                {
                    texts.Add(t.text);
                }
                Assert.Contains("データがありません", texts);
                Assert.IsFalse(texts.Contains("プレイヤー別の集計には対応していません"),
                    "implementation-convenience message must be removed");
            }
            finally
            {
                view.Dispose();
            }
        }

        [Test]
        public void PanelHeadings_AreRenderedOnOneLine()
        {
            // r21 実機で "SCORE RANKING" が "SCORE" / "RANKING"、"PLAYERS" が
            // "PLAYER" / "S" の 2 行に折り返していた。design (qICtu / smhrU) は 1 行。
            // NoWrap 指定だけでなく、実際に 1 行で組まれることまで固定する。
            data.Songs.Add(MakeSong(1, "Song 1", isCloud: false));

            var view = CreateView();
            try
            {
                Transform scoreHead = view.Root.Find("Main/Score Ranking/Head");
                Transform playersHead = view.Root.Find("Main/Players/Head");
                Assert.IsNotNull(scoreHead, "Score Ranking Head");
                Assert.IsNotNull(playersHead, "Players Head");

                TMP_Text[] headings =
                {
                    TextOf(scoreHead, "Label"),
                    TextOf(playersHead, "Title"),
                    TextOf(playersHead, "Metric"),
                };
                foreach (TMP_Text heading in headings)
                {
                    heading.ForceMeshUpdate(true, true);
                    Assert.Greater(heading.textInfo.characterCount, 0,
                        heading.name + " が描画される (" + heading.text + ")");
                    Assert.AreEqual(1, heading.textInfo.lineCount,
                        heading.name + " は design どおり 1 行で描画される (" + heading.text + ")");
                }
            }
            finally
            {
                view.Dispose();
            }
        }

        // --- F. ドットレール (vQc4K) ---

        [Test]
        public void DotRail_MatchesDesignOffsets_NoOverlap()
        {
            data.Songs.Add(MakeSong(1, "Song 1", isCloud: false));
            data.Songs.Add(MakeSong(2, "Song 2", isCloud: false));
            data.Songs.Add(MakeSong(3, "Song 3", isCloud: false));
            data.Select(2);

            var view = CreateView();
            try
            {
                Transform rail = view.Root.Find("Main/Song List/Dot indicator");
                Assert.IsNotNull(rail, "Dot indicator");
                AssertRect(rail, 536f, 136f, V6SelectViewLogic.DotActiveSize,
                    V6SelectViewLogic.DotBottom(data.Songs.Count, 0f), "rail");

                int activeIndex = 1; // data.Select(2) → 2 曲目 (index 1) が選択
                var ys = new List<float>();
                for (int i = 0; i < data.Songs.Count; i++)
                {
                    Transform dot = rail.Find("Dot " + (i + 1));
                    Assert.IsNotNull(dot, "Dot " + (i + 1));
                    Vector2 p = DesignPos(dot);
                    var size = ((RectTransform)dot).sizeDelta;
                    float expectedSize = i == activeIndex
                        ? V6SelectViewLogic.DotActiveSize
                        : V6SelectViewLogic.DotSize;

                    // x はレール中央 ((10-d)/2)、y はピッチ 12 で積む (重ならない)。
                    Assert.AreEqual(expectedSize, size.x, 0.01f, "Dot " + (i + 1) + " size");
                    Assert.AreEqual(expectedSize, size.y, 0.01f, "Dot " + (i + 1) + " size h");
                    Assert.AreEqual((V6SelectViewLogic.DotActiveSize - size.x) * 0.5f, p.x, 0.01f,
                        "Dot " + (i + 1) + " x centered in rail");
                    Assert.AreEqual(12f * i, p.y, 0.01f, "Dot " + (i + 1) + " y pitch 12");
                    ys.Add(p.y);
                }

                // 全ドットが同座標に潰れていない (レビュー指摘 3 の回帰防止)。
                for (int i = 1; i < ys.Count; i++)
                {
                    Assert.Greater(ys[i], ys[i - 1], "dot y must strictly increase");
                }
            }
            finally
            {
                view.Dispose();
            }
        }

        // --- 曲リスト行が画面側の写経を経ずに共通部品で組まれていること ---

        [Test]
        public void SongList_RowsAreBuiltByCommonSongRow()
        {
            data.Songs.Add(MakeSong(1, "A Long Song Name That Would Wrap", isCloud: false));
            data.Songs.Add(MakeSong(2, "Song 2", isCloud: false));
            data.Select(1);

            var view = CreateView();
            try
            {
                Transform rows = view.Root.Find("Main/Song List/Rows");
                Assert.IsNotNull(rows, "Rows container");
                Transform row = rows.Find("SongRow A Long Song Name That Would Wrap");
                Assert.IsNotNull(row, "row built by V6Lists.SongRow (naming + design internals)");

                AssertRect(row, 16f, 48f, 504f, 72f, "row");
                AssertRect(row.Find("Level"), 12f, 14f, 44f, 44f, "Level");
                AssertRect(row.Find("Thumb"), 68f, 9f, 96f, 54f, "Thumb");
                AssertRect(row.Find("Title"), 176f, 2f, 248f, 38f, "Title");
                AssertRect(row.Find("Composer"), 176f, 42f, 248f, 29f, "Composer");
                AssertRect(row.Find("Grade"), 436f, 18f, 56f, 36f, "Grade");

                // 選択行の背景は $k6 (design 選択行)。
                Assert.AreEqual(V6Tokens.Key6, row.GetComponent<Image>().color, "selected row background");
                // クリック→選択の結線は維持されている。
                Assert.IsNotNull(row.GetComponent<Button>(), "row must stay clickable");
            }
            finally
            {
                view.Dispose();
            }
        }

        // --- ヘッダー右の曲情報 (w4siL) ---

        [Test]
        public void Header_BackButton_SitsAtDesignTopLeft()
        {
            // design S1TZD `/wKQlf/SUD2i/zUcsi`: 戻るボタンはヘッダー (0,20) 56×56。
            // 旧実装は V5Ui.Node("Zone Back") + At で包んでいたため、Node 既定の
            // 100×100 の中央にボタンが寄り (22,20) へずれてタイトル (x72) に重なった。
            var view = CreateView();
            try
            {
                Transform back = view.Root.Find("Header/Back Button");
                Assert.IsNotNull(back, "Header/Back Button は Header 直下 (Node に包まない)");
                AssertRect(back, 0f, 20f, 56f, 56f, "Back Button");
                Assert.LessOrEqual(DesignPos(back).x + ((RectTransform)back).sizeDelta.x, 72f,
                    "戻るボタンの右端はタイトル (x72) より左");
            }
            finally
            {
                view.Dispose();
            }
        }

        [Test]
        public void Header_SongInfo_MatchesDesignCoordinates()
        {
            // design S1TZD 情報ゾーン w4siL (header 内 (840,0) 840×96):
            // Thumb (864,30) 64×36 / Song (944,10) 320×76、その中に
            // Title (0,1) 320×43 fs30 / Artist (0,46) 320×29 fs20。
            var view = CreateView();
            try
            {
                Transform thumb = view.Root.Find("Header/Thumb");
                AssertRect(thumb, 864f, 30f, 64f, 36f, "header Thumb");

                Transform songZone = view.Root.Find("Header/Song");
                Assert.IsNotNull(songZone, "Header/Song");
                AssertRect(songZone, 944f, 10f, 320f, 76f, "Song zone");
                AssertRect(songZone.Find("Title"), 0f, 1f, 320f, 43f, "Song Title");
                AssertRect(songZone.Find("Artist"), 0f, 46f, 320f, 29f, "Song Artist");

                var title = TextOf(songZone, "Title");
                Assert.AreEqual(30f, title.fontSize, 0.01f, "Title fs");
                Assert.AreEqual(TextWrappingModes.NoWrap, title.textWrappingMode, "Title must not wrap");
                Assert.AreEqual(TextOverflowModes.Ellipsis, title.overflowMode, "Title must ellipsize");
                var artist = TextOf(songZone, "Artist");
                Assert.AreEqual(20f, artist.fontSize, 0.01f, "Artist fs");
            }
            finally
            {
                view.Dispose();
            }
        }
    }
}
