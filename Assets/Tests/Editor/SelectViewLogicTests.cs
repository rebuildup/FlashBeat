using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V5;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #121: Select 画面の純ロジック (グレード判定 / フィルタ・ソート /
    /// フォーマット / スクロール窓) のテスト。
    /// </summary>
    public class SelectViewLogicTests
    {
        private static SongData Song(int id, string title, string musician, int level)
        {
            return new SongData(id, title, musician, "vid" + id, 120, level, 100, 90f);
        }

        private static SongData[] Catalog()
        {
            // id 0 はプレースホルダ (GManager の規約)。実曲は 1 から。
            return new[]
            {
                Song(0, "noSong", "no", 10),
                Song(1, "Poison", "LeaF", 10),
                Song(2, "まにまに", "r-906", 13),
                Song(3, "4th smile", "LeaF", 5),
                Song(4, "Calamity", "LeaF", 7),
            };
        }

        // --- グレード判定 (デザイン 02/07 の実測値に整合) ---

        [Test]
        public void GradeOf_DesignReferenceScores_MatchDesignChips()
        {
            Assert.AreEqual("SS", SelectViewLogic.GradeOf(1000000));
            Assert.AreEqual("S", SelectViewLogic.GradeOf(987410));
            Assert.AreEqual("A", SelectViewLogic.GradeOf(921880));
            Assert.AreEqual("B", SelectViewLogic.GradeOf(842300));
            Assert.AreEqual("C", SelectViewLogic.GradeOf(742000));
        }

        [Test]
        public void GradeOf_NoRecord_ReturnsNull()
        {
            Assert.IsNull(SelectViewLogic.GradeOf(0));
            Assert.IsNull(SelectViewLogic.GradeOf(-1));
        }

        // --- フィルタ ---

        [Test]
        public void MatchesFilter_MatchesTitleOrArtistIgnoringCase()
        {
            var songs = Catalog();
            Assert.IsTrue(SelectViewLogic.MatchesFilter(songs[1], "poi"));
            Assert.IsTrue(SelectViewLogic.MatchesFilter(songs[1], "leaf"));
            Assert.IsFalse(SelectViewLogic.MatchesFilter(songs[2], "LeaF"));
            Assert.IsTrue(SelectViewLogic.MatchesFilter(songs[2], ""));
            Assert.IsFalse(SelectViewLogic.MatchesFilter(null, ""));
        }

        [Test]
        public void FilterIds_ExcludesPlaceholderAndAppliesAllFilters()
        {
            var songs = Catalog();
            var hiscores = new[] { 0, 903550, 0, 1000000, 0 };

            var all = SelectViewLogic.FilterIds(songs, "", SelectViewLogic.SortMode.Recent,
                SelectViewLogic.LevelMode.All, SelectViewLogic.StatusMode.All, hiscores);
            Assert.AreEqual(new[] { 1, 2, 3, 4 }, all.ToArray());

            var leaf = SelectViewLogic.FilterIds(songs, "LeaF", SelectViewLogic.SortMode.Recent,
                SelectViewLogic.LevelMode.All, SelectViewLogic.StatusMode.All, hiscores);
            Assert.AreEqual(new[] { 1, 3, 4 }, leaf.ToArray());

            var played = SelectViewLogic.FilterIds(songs, "", SelectViewLogic.SortMode.Recent,
                SelectViewLogic.LevelMode.All, SelectViewLogic.StatusMode.Played, hiscores);
            Assert.AreEqual(new[] { 1, 3 }, played.ToArray());

            var unplayed = SelectViewLogic.FilterIds(songs, "", SelectViewLogic.SortMode.Recent,
                SelectViewLogic.LevelMode.All, SelectViewLogic.StatusMode.Unplayed, hiscores);
            Assert.AreEqual(new[] { 2, 4 }, unplayed.ToArray());

            var mid = SelectViewLogic.FilterIds(songs, "", SelectViewLogic.SortMode.Recent,
                SelectViewLogic.LevelMode.Level6to10, SelectViewLogic.StatusMode.All, hiscores);
            Assert.AreEqual(new[] { 1, 4 }, mid.ToArray());
        }

        [Test]
        public void SortIds_LevelDescends_ScoreDescends()
        {
            var songs = Catalog();
            var hiscores = new[] { 0, 903550, 0, 1000000, 0 };

            var byLevel = new List<int> { 1, 2, 3, 4 };
            SelectViewLogic.SortIds(byLevel, SelectViewLogic.SortMode.Level, songs, hiscores);
            Assert.AreEqual(new[] { 2, 1, 4, 3 }, byLevel.ToArray());

            var byScore = new List<int> { 1, 2, 3, 4 };
            SelectViewLogic.SortIds(byScore, SelectViewLogic.SortMode.Score, songs, hiscores);
            Assert.AreEqual(new[] { 3, 1, 2, 4 }, byScore.ToArray());
        }

        // --- フォーマット ---

        [Test]
        public void ScoreText_UsesThousandsSeparators()
        {
            Assert.AreEqual("952,310", SelectViewLogic.ScoreText(952310));
            Assert.AreEqual("1,000,000", SelectViewLogic.ScoreText(1000000));
            Assert.AreEqual("0", SelectViewLogic.ScoreText(0));
        }

        [Test]
        public void DurationText_FormatsMinutesSeconds()
        {
            Assert.AreEqual("1:29", SelectViewLogic.DurationText(89f));
            Assert.AreEqual("0:05", SelectViewLogic.DurationText(5.4f));
            Assert.IsNull(SelectViewLogic.DurationText(0f));
        }

        // --- スクロール窓 ---

        [Test]
        public void ScrollOffset_KeepsSelectionVisible()
        {
            Assert.AreEqual(0, SelectViewLogic.ScrollOffset(5, 0, 8));
            Assert.AreEqual(0, SelectViewLogic.ScrollOffset(5, 4, 8));
            Assert.AreEqual(0, SelectViewLogic.ScrollOffset(10, 0, 8));
            Assert.AreEqual(0, SelectViewLogic.ScrollOffset(10, 2, 8));
            Assert.AreEqual(2, SelectViewLogic.ScrollOffset(10, 6, 8));
            Assert.AreEqual(2, SelectViewLogic.ScrollOffset(10, 9, 8));
            Assert.AreEqual(0, SelectViewLogic.ScrollOffset(0, 0, 8));
            Assert.AreEqual(0, SelectViewLogic.ScrollOffset(10, 3, 0));
        }
    }
}
