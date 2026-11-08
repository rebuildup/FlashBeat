using System.Collections.Generic;
using NUnit.Framework;
using FlashBeat.UI.V6.History;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;

namespace FlashBeat.UI.V6.History.Tests
{
    /// <summary>
    /// v6 履歴画面の純ロジックの検証。実データの形 (V6SongEntry) をそのまま使う。
    /// </summary>
    public class V6HistoryLogicTests
    {
        static V6SongEntry Song(int id, string title, int bestScore, string grade)
        {
            return new V6SongEntry
            {
                SongId = id,
                Title = title,
                BestScore = bestScore,
                BestGrade = grade
            };
        }

        [Test]
        public void Played_ExcludesUnplayedSongs()
        {
            var songs = new List<V6SongEntry>
            {
                Song(1, "a", 900000, "A"),
                Song(2, "b", -1, ""),
                Song(3, "c", 0, ""),
            };

            List<V6SongEntry> played = V6HistoryLogic.Played(songs);

            // BestScore 0 は「未プレイ扱い」(ScoreStore が 0 を記録しない) なので除外。
            Assert.AreEqual(1, played.Count);
            Assert.AreEqual(1, played[0].SongId);
        }

        [Test]
        public void Sort_ScoreMode_OrdersByScoreDescending_TiesKeepCatalogOrder()
        {
            var played = new List<V6SongEntry>
            {
                Song(3, "c", 800000, "B"),
                Song(1, "a", 980000, "S"),
                Song(2, "b", 800000, "B"),
            };

            V6HistoryLogic.Sort(played, V6HistoryLogic.HistorySort.Score);

            Assert.AreEqual(new[] { 1, 2, 3 }, new[] { played[0].SongId, played[1].SongId, played[2].SongId });
        }

        [Test]
        public void Sort_CatalogMode_OrdersBySongId()
        {
            var played = new List<V6SongEntry>
            {
                Song(5, "e", 700000, "C"),
                Song(2, "b", 990000, "SS"),
            };

            V6HistoryLogic.Sort(played, V6HistoryLogic.HistorySort.Catalog);

            Assert.AreEqual(2, played[0].SongId);
            Assert.AreEqual(5, played[1].SongId);
        }

        [Test]
        public void NextSort_TogglesBetweenScoreAndCatalog()
        {
            Assert.AreEqual(V6HistoryLogic.HistorySort.Catalog,
                V6HistoryLogic.NextSort(V6HistoryLogic.HistorySort.Score));
            Assert.AreEqual(V6HistoryLogic.HistorySort.Score,
                V6HistoryLogic.NextSort(V6HistoryLogic.HistorySort.Catalog));
        }

        [Test]
        public void TotalScore_SumsRecordedScores()
        {
            var played = new List<V6SongEntry>
            {
                Song(1, "a", 900000, "A"),
                Song(2, "b", 950000, "S"),
                Song(3, "c", 0, ""),
            };

            Assert.AreEqual(1850000, V6HistoryLogic.TotalScore(played));
        }

        [Test]
        public void CountGradeAtLeastS_CountsSAndSS()
        {
            var played = new List<V6SongEntry>
            {
                Song(1, "a", 1000000, "SS"),
                Song(2, "b", 950000, "S"),
                Song(3, "c", 900000, "A"),
            };

            Assert.AreEqual(2, V6HistoryLogic.CountGradeAtLeastS(played));
        }

        [Test]
        public void HasLastPlay_AllZeroMeansNoPlay()
        {
            Assert.IsFalse(V6HistoryLogic.HasLastPlay(0, 0, 0, 0, 0));
            Assert.IsTrue(V6HistoryLogic.HasLastPlay(0, 0, 0, 0, 1));
            Assert.IsTrue(V6HistoryLogic.HasLastPlay(123, 0, 0, 0, 0));
        }

        [Test]
        public void ComboLabel_UnknownNoteCountShowsDash()
        {
            Assert.AreEqual("–", V6HistoryLogic.ComboLabel(246, 0));
            Assert.AreEqual("246/296", V6HistoryLogic.ComboLabel(246, 296));
        }

        [Test]
        public void PlaysLabel_ShowsCount()
        {
            Assert.AreEqual("0 plays", V6HistoryLogic.PlaysLabel(0));
            Assert.AreEqual("4 plays", V6HistoryLogic.PlaysLabel(4));
        }
    }
}
