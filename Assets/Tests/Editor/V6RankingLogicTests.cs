using System.Collections.Generic;
using NUnit.Framework;
using FlashBeat.UI.V6.Ranking;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;

namespace FlashBeat.UI.V6.Ranking.Tests
{
    /// <summary>
    /// v6 ランキング画面の純ロジックの検証。実データの形 (V6SongEntry) をそのまま使う。
    /// </summary>
    public class V6RankingLogicTests
    {
        [Test]
        public void NextSource_TogglesBetweenLocalAndOnline()
        {
            Assert.AreEqual(V6RankingLogic.RankingSource.Online,
                V6RankingLogic.NextSource(V6RankingLogic.RankingSource.Local));
            Assert.AreEqual(V6RankingLogic.RankingSource.Local,
                V6RankingLogic.NextSource(V6RankingLogic.RankingSource.Online));
        }

        [Test]
        public void SourceLabel_MatchesDesign()
        {
            Assert.AreEqual("Local", V6RankingLogic.SourceLabel(V6RankingLogic.RankingSource.Local));
            Assert.AreEqual("Online", V6RankingLogic.SourceLabel(V6RankingLogic.RankingSource.Online));
        }

        [Test]
        public void LocalRows_PlayedSong_SingleYouRow()
        {
            var song = new V6SongEntry
            {
                SongId = 5,
                Title = "x",
                BestScore = 988513,
                BestGrade = "S"
            };

            List<V6RankingRow> rows = V6RankingLogic.LocalRows(song);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(1, rows[0].Position);
            Assert.AreEqual("YOU", rows[0].PlayerName);
            Assert.AreEqual(988513, rows[0].Score);
            Assert.AreEqual("S", rows[0].Grade);
            Assert.IsTrue(rows[0].IsYou);
        }

        [Test]
        public void LocalRows_UnplayedSong_Empty()
        {
            var song = new V6SongEntry { SongId = 1, BestScore = -1, BestGrade = "" };

            Assert.AreEqual(0, V6RankingLogic.LocalRows(song).Count);
            Assert.AreEqual(0, V6RankingLogic.LocalRows(null).Count);
        }

        [Test]
        public void EmptyMessage_ErrorShowsReason()
        {
            Assert.AreEqual("HTTP 500", V6RankingLogic.EmptyMessage(
                V6RankingLogic.RankingSource.Online, false, true, "HTTP 500"));
        }

        [Test]
        public void EmptyMessage_LoadingShowsProgress()
        {
            Assert.AreEqual("ランキングを取得しています…", V6RankingLogic.EmptyMessage(
                V6RankingLogic.RankingSource.Online, true, false, ""));
        }

        [Test]
        public void EmptyMessage_NoData()
        {
            Assert.AreEqual("NO DATA", V6RankingLogic.EmptyMessage(
                V6RankingLogic.RankingSource.Online, false, false, ""));
            // Local に記録が無いときはオンライン取得失敗の文言ではない。
            Assert.AreEqual("記録がありません", V6RankingLogic.EmptyMessage(
                V6RankingLogic.RankingSource.Local, false, false, ""));
        }
    }
}
