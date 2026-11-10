using System.Collections.Generic;
using NUnit.Framework;
using FlashBeat.UI.V6.History;
using FlashBeat.UI.V6.Ranking;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;

namespace FlashBeat.V6Tests
{
    /// <summary>
    /// v6 履歴 / ランキングの純ロジックテスト (Issue #223)。
    /// - V6HistoryLogic: Played / Sort / HistoryList / TotalScore / CountGradeAtLeastS / HasLastPlay / ComboLabel
    /// - V6RankingLogic: LocalRows / EmptyMessage / NextSource / SourceLabel
    /// Unity エンジンに依存しない純ロジックのみを検証する。
    /// </summary>
    public sealed class V6HistoryRankingTests
    {
        // --- テスト用の最小 V6SongEntry ファクトリ ---

        private static V6SongEntry MakeSong(int songId, string title, int bestScore = -1, string bestGrade = "", bool isCloud = false)
        {
            return new V6SongEntry
            {
                SongId = songId,
                Title = title,
                Artist = "Artist",
                Level = 10,
                JacketResourcePath = "",
                BestScore = bestScore,
                BestGrade = bestGrade,
                Bpm = 120.0,
                NoteCount = 500,
                IsCloud = isCloud,
            };
        }

        // =====================================================================
        // V6HistoryLogic
        // =====================================================================

        [Test]
        public void Played_OnlyReturnsSongsWithBestScoreGreaterThanZero()
        {
            var songs = new List<V6SongEntry>
            {
                MakeSong(1, "Played A", 100000, "S"),
                MakeSong(2, "Unplayed B", -1, ""),
                MakeSong(3, "Played C", 50000, "A"),
                MakeSong(4, "Zero Score", 0, ""), // 0 は未プレイ扱い
            };

            var played = V6HistoryLogic.Played(songs);

            Assert.AreEqual(2, played.Count);
            Assert.AreEqual(1, played[0].SongId);
            Assert.AreEqual(3, played[1].SongId);
        }

        [Test]
        public void Played_EmptyInput_ReturnsEmptyList()
        {
            var played = V6HistoryLogic.Played(null);
            Assert.AreEqual(0, played.Count);

            played = V6HistoryLogic.Played(new List<V6SongEntry>());
            Assert.AreEqual(0, played.Count);
        }

        [Test]
        public void Sort_ScoreDescending_ThenCatalogOrder()
        {
            var played = new List<V6SongEntry>
            {
                MakeSong(3, "C", 50000, "A"),
                MakeSong(1, "A", 100000, "S"),
                MakeSong(2, "B", 100000, "S"), // 同点: SongId 昇順
            };

            V6HistoryLogic.Sort(played, V6HistoryLogic.HistorySort.Score);

            Assert.AreEqual(1, played[0].SongId); // 100000, id=1
            Assert.AreEqual(2, played[1].SongId); // 100000, id=2
            Assert.AreEqual(3, played[2].SongId); // 50000
        }

        [Test]
        public void Sort_CatalogOrder_AscendingBySongId()
        {
            var played = new List<V6SongEntry>
            {
                MakeSong(3, "C", 50000, "A"),
                MakeSong(1, "A", 100000, "S"),
                MakeSong(2, "B", 100000, "S"),
            };

            V6HistoryLogic.Sort(played, V6HistoryLogic.HistorySort.Catalog);

            Assert.AreEqual(1, played[0].SongId);
            Assert.AreEqual(2, played[1].SongId);
            Assert.AreEqual(3, played[2].SongId);
        }

        [Test]
        public void HistoryList_CombinesPlayedAndSort()
        {
            var songs = new List<V6SongEntry>
            {
                MakeSong(1, "A", 100000, "S"),
                MakeSong(2, "B", -1, ""), // 未プレイ
                MakeSong(3, "C", 50000, "A"),
            };

            var history = V6HistoryLogic.HistoryList(songs, V6HistoryLogic.HistorySort.Score);

            Assert.AreEqual(2, history.Count);
            Assert.AreEqual(1, history[0].SongId);
            Assert.AreEqual(3, history[1].SongId);
        }

        [Test]
        public void TotalScore_SumsBestScoresOfPlayedSongs()
        {
            var played = new List<V6SongEntry>
            {
                MakeSong(1, "A", 100000, "S"),
                MakeSong(2, "B", 50000, "A"),
                MakeSong(3, "C", 25000, "B"),
            };

            int total = V6HistoryLogic.TotalScore(played);

            Assert.AreEqual(175000, total);
        }

        [Test]
        public void TotalScore_EmptyList_ReturnsZero()
        {
            Assert.AreEqual(0, V6HistoryLogic.TotalScore(new List<V6SongEntry>()));
            Assert.AreEqual(0, V6HistoryLogic.TotalScore(null));
        }

        [Test]
        public void CountGradeAtLeastS_CountsSAndSS()
        {
            var played = new List<V6SongEntry>
            {
                MakeSong(1, "A", 100000, "S"),
                MakeSong(2, "B", 980000, "SS"),
                MakeSong(3, "C", 50000, "A"),
                MakeSong(4, "D", 40000, "B"),
            };

            int count = V6HistoryLogic.CountGradeAtLeastS(played);

            Assert.AreEqual(2, count);
        }

        [Test]
        public void CountGradeAtLeastS_EmptyList_ReturnsZero()
        {
            Assert.AreEqual(0, V6HistoryLogic.CountGradeAtLeastS(new List<V6SongEntry>()));
            Assert.AreEqual(0, V6HistoryLogic.CountGradeAtLeastS(null));
        }

        [Test]
        public void HasLastPlay_ReturnsTrueWhenAnyStatPositive()
        {
            Assert.IsTrue(V6HistoryLogic.HasLastPlay(100, 0, 0, 0, 0));
            Assert.IsTrue(V6HistoryLogic.HasLastPlay(0, 10, 0, 0, 0));
            Assert.IsTrue(V6HistoryLogic.HasLastPlay(0, 0, 5, 0, 0));
            Assert.IsTrue(V6HistoryLogic.HasLastPlay(0, 0, 0, 3, 0));
            Assert.IsTrue(V6HistoryLogic.HasLastPlay(0, 0, 0, 0, 2));
        }

        [Test]
        public void HasLastPlay_ReturnsFalseWhenAllZero()
        {
            Assert.IsFalse(V6HistoryLogic.HasLastPlay(0, 0, 0, 0, 0));
        }

        [Test]
        public void ComboLabel_FormatsMaxOverTotal()
        {
            Assert.AreEqual("246/296", V6HistoryLogic.ComboLabel(246, 296));
        }

        [Test]
        public void ComboLabel_ReturnsDashWhenTotalUnknown()
        {
            Assert.AreEqual("–", V6HistoryLogic.ComboLabel(100, 0));
            Assert.AreEqual("–", V6HistoryLogic.ComboLabel(100, -1));
        }

        [Test]
        public void RecordedCountLabel_ShowsRecordedSongCount()
        {
            // 値は「記録のある曲数」でありプレイ回数ではない (Issue #239)。
            Assert.AreEqual("記録 0曲", V6HistoryLogic.RecordedCountLabel(0));
            Assert.AreEqual("記録 1曲", V6HistoryLogic.RecordedCountLabel(1));
            Assert.AreEqual("記録 42曲", V6HistoryLogic.RecordedCountLabel(42));
        }

        [Test]
        public void NextSort_TogglesBetweenScoreAndCatalog()
        {
            Assert.AreEqual(V6HistoryLogic.HistorySort.Catalog, V6HistoryLogic.NextSort(V6HistoryLogic.HistorySort.Score));
            Assert.AreEqual(V6HistoryLogic.HistorySort.Score, V6HistoryLogic.NextSort(V6HistoryLogic.HistorySort.Catalog));
        }

        [Test]
        public void SortLabel_ReturnsJapaneseLabels()
        {
            Assert.AreEqual("スコア順", V6HistoryLogic.SortLabel(V6HistoryLogic.HistorySort.Score));
            Assert.AreEqual("カタログ順", V6HistoryLogic.SortLabel(V6HistoryLogic.HistorySort.Catalog));
        }

        // =====================================================================
        // V6RankingLogic
        // =====================================================================

        [Test]
        public void LocalRows_ReturnsYouRowWhenBestScoreExists()
        {
            var song = MakeSong(1, "Test", 123456, "S");

            var rows = V6RankingLogic.LocalRows(song);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(1, rows[0].Position);
            Assert.AreEqual("YOU", rows[0].PlayerName);
            Assert.AreEqual(123456, rows[0].Score);
            Assert.AreEqual("S", rows[0].Grade);
            Assert.IsTrue(rows[0].IsYou);
        }

        [Test]
        public void LocalRows_ReturnsEmptyWhenNoBestScore()
        {
            var song = MakeSong(1, "Test", -1, "");

            var rows = V6RankingLogic.LocalRows(song);

            Assert.AreEqual(0, rows.Count);
        }

        [Test]
        public void LocalRows_ReturnsEmptyWhenSongIsNull()
        {
            var rows = V6RankingLogic.LocalRows(null);
            Assert.AreEqual(0, rows.Count);
        }

        [Test]
        public void EmptyMessage_ReturnsErrorMessageWhenHasError()
        {
            var msg = V6RankingLogic.EmptyMessage(V6RankingLogic.RankingSource.Online, false, true, "HTTP 500");
            Assert.AreEqual("HTTP 500", msg);

            // Local は通信状態と無関係なのでエラー文言を出さない (Issue #239)。
            msg = V6RankingLogic.EmptyMessage(V6RankingLogic.RankingSource.Local, false, true, "Error");
            Assert.AreEqual("記録がありません", msg);
        }

        [Test]
        public void EmptyMessage_Local_IgnoresLoadingAndError()
        {
            // カタログ取得中 / 取得失敗でも Local パネルは「記録がありません」のまま
            // (通信状態を Local の表示に流さない、Issue #239)。
            Assert.AreEqual("記録がありません",
                V6RankingLogic.EmptyMessage(V6RankingLogic.RankingSource.Local, true, false, ""));
            Assert.AreEqual("記録がありません",
                V6RankingLogic.EmptyMessage(V6RankingLogic.RankingSource.Local, false, true, "HTTP 500"));
            Assert.AreEqual("記録がありません",
                V6RankingLogic.EmptyMessage(V6RankingLogic.RankingSource.Local, true, true, "カタログを取得しています…"));
        }

        [Test]
        public void EmptyMessage_ReturnsLoadingWhenIsLoading()
        {
            var msg = V6RankingLogic.EmptyMessage(V6RankingLogic.RankingSource.Online, true, false, "");
            Assert.AreEqual("ランキングを取得しています…", msg);
        }

        [Test]
        public void EmptyMessage_ReturnsNoDataForOnlineWhenNoErrorNoLoading()
        {
            var msg = V6RankingLogic.EmptyMessage(V6RankingLogic.RankingSource.Online, false, false, "");
            Assert.AreEqual("NO DATA", msg);
        }

        [Test]
        public void EmptyMessage_ReturnsNoRecordForLocalWhenNoErrorNoLoading()
        {
            var msg = V6RankingLogic.EmptyMessage(V6RankingLogic.RankingSource.Local, false, false, "");
            Assert.AreEqual("記録がありません", msg);
        }

        [Test]
        public void NextSource_TogglesBetweenLocalAndOnline()
        {
            Assert.AreEqual(V6RankingLogic.RankingSource.Online, V6RankingLogic.NextSource(V6RankingLogic.RankingSource.Local));
            Assert.AreEqual(V6RankingLogic.RankingSource.Local, V6RankingLogic.NextSource(V6RankingLogic.RankingSource.Online));
        }

        [Test]
        public void SourceLabel_ReturnsEnglishLabels()
        {
            Assert.AreEqual("Local", V6RankingLogic.SourceLabel(V6RankingLogic.RankingSource.Local));
            Assert.AreEqual("Online", V6RankingLogic.SourceLabel(V6RankingLogic.RankingSource.Online));
        }
    }
}