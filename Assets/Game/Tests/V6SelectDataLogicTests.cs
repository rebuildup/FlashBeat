using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;

namespace FlashBeat.V5CanvasTests
{
    /// <summary>
    /// Issue #222: v6 選曲データの純ロジックと実データ結線の EditMode テスト。
    /// - 検索フィルタ / グレード判定 (V5 と同じ閾値)
    /// - 実カタログ (GManager.ActiveSongs) からの曲リスト構築 (noSong / unplayable の除外)
    /// - 譜面メタを実ファイル (Resources/&lt;曲名&gt;.json) から読むこと
    /// - 選択 / 移動の端挙動と CommitReady の確定 (遷移はしない)
    /// 通信 (RefreshAsync のクラウド取得) は EditMode では動かないので対象外。
    /// </summary>
    public class V6SelectDataLogicTests
    {
        int savedSongId;

        [SetUp]
        public void SetUp()
        {
            savedSongId = GManager.songID;
        }

        [TearDown]
        public void TearDown()
        {
            GManager.songID = savedSongId;
        }

        // --- 検索フィルタ ---------------------------------------------------------

        [Test]
        public void MatchesQuery_EmptyQuery_MatchesEverything()
        {
            Assert.IsTrue(V6SelectDataLogic.MatchesQuery("ロストアンブレラ", "稲葉曇", ""));
            Assert.IsTrue(V6SelectDataLogic.MatchesQuery("", null, ""));
        }

        [Test]
        public void MatchesQuery_MatchesTitleAndArtistPartially_IgnoringCase()
        {
            Assert.IsTrue(V6SelectDataLogic.MatchesQuery("Chartreuse", "t+pazolite", "chart"));
            Assert.IsTrue(V6SelectDataLogic.MatchesQuery("Chartreuse", "t+pazolite", "PAZO"));
            Assert.IsFalse(V6SelectDataLogic.MatchesQuery("Chartreuse", "t+pazolite", "稲葉"));
        }

        [Test]
        public void Filter_KeepsCatalogOrder_AndDropsNonMatches()
        {
            var songs = new List<V6SongEntry>
            {
                new V6SongEntry { SongId = 1, Title = "ロストアンブレラ", Artist = "稲葉曇" },
                new V6SongEntry { SongId = 2, Title = "まにまに", Artist = "r-906" },
                new V6SongEntry { SongId = 3, Title = "Who", Artist = "Azari" },
            };

            List<V6SongEntry> all = V6SelectDataLogic.Filter(songs, "");
            Assert.AreEqual(3, all.Count);

            List<V6SongEntry> byTitle = V6SelectDataLogic.Filter(songs, "まにまに");
            Assert.AreEqual(new[] { 2 }, byTitle.Select(s => s.SongId).ToArray());

            List<V6SongEntry> byArtist = V6SelectDataLogic.Filter(songs, "906");
            Assert.AreEqual(new[] { 2 }, byArtist.Select(s => s.SongId).ToArray());

            Assert.AreEqual(0, V6SelectDataLogic.Filter(songs, "存在しない曲名").Count);
        }

        // --- グレード (V5 と同じ閾値) ---------------------------------------------

        [Test]
        public void GradeOf_FollowsTheV5Thresholds()
        {
            Assert.AreEqual("SS", V6SelectDataLogic.GradeOf(V6SelectDataLogic.ScoreMax));
            Assert.AreEqual("S", V6SelectDataLogic.GradeOf(950000));
            Assert.AreEqual("A", V6SelectDataLogic.GradeOf(900000));
            Assert.AreEqual("B", V6SelectDataLogic.GradeOf(800000));
            Assert.AreEqual("C", V6SelectDataLogic.GradeOf(742000));
            Assert.AreEqual("", V6SelectDataLogic.GradeOf(0));
            Assert.AreEqual("", V6SelectDataLogic.GradeOf(-1));
        }

        // --- 実カタログからの構築 ---------------------------------------------------

        [Test]
        public void AllSongs_ExcludesNoSongAndUnplayable_AndSongIdsResolveInTheCatalog()
        {
            var source = new V6SelectDataSource();

            // index 0 (noSong) と unplayable (ニセモノ) はリストに出ない。
            Assert.IsFalse(source.AllSongs.Any(s => s.SongId == 0));
            Assert.IsFalse(source.AllSongs.Any(s => s.Title == "ニセモノ"));

            // SongId は GManager.ActiveSongs の行インデックスと一致する
            // (GetSong で同じ行に解決され、プレイ可能な行だけが載る)。
            Assert.Greater(source.AllSongs.Count, 0);
            foreach (V6SongEntry entry in source.AllSongs)
            {
                SongData row = GManager.GetSong(entry.SongId);
                Assert.IsNotNull(row, "SongId {0}", entry.SongId);
                Assert.IsFalse(row.unplayable, "SongId {0}", entry.SongId);
            }
        }

        [Test]
        public void AllSongs_RepairsChartMetaFromTheRealChartFile()
        {
            var source = new V6SelectDataSource();

            // 終焉逃避行 (index 5): 実譜面ファイル Resources/終焉逃避行.json は
            // BPM 147 / notes 296。デザインの統計 (dRztp) も 147 / 296。
            V6SongEntry entry = source.AllSongs.Single(s => s.Title == "終焉逃避行");
            Assert.AreEqual(147.0, entry.Bpm);
            Assert.AreEqual(296, entry.NoteCount);

            // ジャケットは v6 で全曲分を Resources/V6/select/jacket/ に置いている
            // (Pencil 設計の正式データ。ファイル名はカタログ index 順)。
            // Resources.Load は拡張子付きパスを受け付けないため、パスは拡張子なし。
            Assert.AreEqual("V6/select/jacket/05-shuumatsu", entry.JacketResourcePath);
            // 22 = 灰Φ倶楽部 も 22-haihai.jpg に対応する (サムネイル空欄の再発防止)。
            V6SongEntry hai = source.AllSongs.Single(s => s.Title == "灰Φ倶楽部");
            Assert.AreEqual("V6/select/jacket/22-haihai", hai.JacketResourcePath);
        }

        // --- 選択 / 移動 ------------------------------------------------------------

        [Test]
        public void Select_UnknownSongId_DoesNothing()
        {
            var source = new V6SelectDataSource();
            Assert.IsNull(source.Selected);

            source.Select(999999);
            Assert.IsNull(source.Selected);
        }

        [Test]
        public void MoveSelection_ClampsAtBothEnds()
        {
            var source = new V6SelectDataSource();
            int last = source.FilteredSongs[source.FilteredSongs.Count - 1].SongId;
            int first = source.FilteredSongs[0].SongId;

            // 未選択から下方向へ移動すると先頭行を選ぶ。
            source.MoveSelection(+100000);
            Assert.AreEqual(first, source.Selected.SongId);

            // 下端で留まる。
            source.MoveSelection(+100000);
            Assert.AreEqual(last, source.Selected.SongId);

            // 下端から 1 行戻れる。
            source.MoveSelection(-1);
            Assert.AreNotEqual(last, source.Selected.SongId);

            // 上端で留まる。
            source.MoveSelection(-100000);
            Assert.AreEqual(first, source.Selected.SongId);
        }

        [Test]
        public void SetQuery_NarrowsTheList_AndKeepsSelectionWhenHidden()
        {
            var source = new V6SelectDataSource();
            source.Select(5); // 終焉逃避行
            Assert.AreEqual(5, source.Selected.SongId);

            // 選択中の曲が絞り込み結果から消えても、Selected は AllSongs の行を
            // 指し続ける (詳細パネルが消えない)。
            source.SetQuery("稲葉曇");
            Assert.IsTrue(source.FilteredSongs.All(s => s.Artist == "稲葉曇"));
            Assert.Greater(source.FilteredSongs.Count, 0);
            Assert.AreEqual("終焉逃避行", source.Selected.Title);

            // 絞り込み結果にいない状態で移動すると、移動方向の端 (下なら先頭) から始める。
            source.SetQuery("LeaF");
            source.MoveSelection(+1);
            Assert.AreEqual(source.FilteredSongs[0].SongId, source.Selected.SongId);
        }

        // --- CommitReady (遷移はしない / 確定だけ) -----------------------------------

        [Test]
        public void CommitReady_WithoutSelection_ReportsErrorAndKeepsSongId()
        {
            var source = new V6SelectDataSource();
            int before = GManager.songID;

            source.CommitReady();

            Assert.IsTrue(source.HasError);
            Assert.IsFalse(string.IsNullOrEmpty(source.StatusMessage));
            Assert.AreEqual(before, GManager.songID, "遷移の確定はしない");
        }

        [Test]
        public void CommitReady_WithLocalSongSelected_FixesSongIdWithoutTransition()
        {
            var source = new V6SelectDataSource();
            source.Select(5); // 終焉逃避行 (ローカル譜面あり)
            GManager.songID = 1;

            source.CommitReady();

            Assert.IsFalse(source.HasError, source.StatusMessage);
            Assert.AreEqual(5, GManager.songID);
        }

        [Test]
        public void CommitReady_WhenSelectionHiddenByQuery_ReportsErrorAndKeepsSongId()
        {
            // 絞り込みで隠れた曲は Ready できない (#221/#222)。Selected は
            // 絞り込みをまたいで生き残るが、リストに見えない曲の開始は禁止。
            var source = new V6SelectDataSource();
            source.Select(5); // 終焉逃避行
            GManager.songID = 1;
            source.SetQuery("存在しない曲名XYZ"); // 全件がリストから消える (5 も隠れる)
            Assume.That(source.FilteredSongs, Is.Empty);

            source.CommitReady();

            Assert.IsTrue(source.HasError);
            Assert.IsFalse(string.IsNullOrEmpty(source.StatusMessage));
            Assert.AreEqual(1, GManager.songID, "隠れた曲で開始しない (遷移の確定もしない)");
        }

        // --- 監査フック (#374) -----------------------------------------------------

        [Test]
        public void SetAuditCatalogFetching_TogglesIsLoading_AndFiresChanged()
        {
            // 画面監査 (AutoTestV6Screens) がスケルトン/点滅なし経路を実機で
            // 再現するための口。実通信は行わず IsLoading だけを操作する。
            var source = new V6SelectDataSource();
            int changes = 0;
            source.Changed += () => changes++;

            Assert.IsFalse(source.IsLoading);
            source.SetAuditCatalogFetching(true);
            Assert.IsTrue(source.IsLoading);
            Assert.IsFalse(source.HasError);
            Assert.AreEqual("カタログを取得しています…", source.StatusMessage);

            source.SetAuditCatalogFetching(false);
            Assert.IsFalse(source.IsLoading);
            Assert.GreaterOrEqual(changes, 2, "取得中フラグの変化は Changed で通知する");
        }
    }
}
