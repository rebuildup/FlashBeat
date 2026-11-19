using NUnit.Framework;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #740: 旧 V5 SelectViewLogic から Songs 層へ移設した
    /// <see cref="SongScoreLogic"/> の純ロジック (グレード判定 / 曲長表記) のテスト。
    /// v5 Select 画面のフィルタ / ソート / スクロール窓は v6 側
    /// (V6SelectDataLogic / V6SelectViewLogic) が担うため対象外。
    /// </summary>
    public class SongScoreLogicTests
    {
        // --- ScoreMax ---

        [Test]
        public void ScoreMax_IsDesignFullScore()
        {
            Assert.AreEqual(1000000, SongScoreLogic.ScoreMax);
        }

        // --- GradeOf (記録なしは null) ---

        [Test]
        public void GradeOf_DesignValues_Match()
        {
            Assert.AreEqual("SS", SongScoreLogic.GradeOf(1000000));
            Assert.AreEqual("S", SongScoreLogic.GradeOf(987410));
            Assert.AreEqual("A", SongScoreLogic.GradeOf(921880));
            Assert.AreEqual("B", SongScoreLogic.GradeOf(842300));
            Assert.AreEqual("C", SongScoreLogic.GradeOf(742000));
        }

        [Test]
        public void GradeOf_NoRecord_ReturnsNull()
        {
            Assert.IsNull(SongScoreLogic.GradeOf(0));
            Assert.IsNull(SongScoreLogic.GradeOf(-1));
        }

        // --- DurationText ("m:ss") ---

        [Test]
        public void DurationText_FormatsMinutesAndSeconds()
        {
            Assert.AreEqual("1:29", SongScoreLogic.DurationText(89f));
            Assert.AreEqual("0:05", SongScoreLogic.DurationText(5.4f));
            Assert.IsNull(SongScoreLogic.DurationText(0f));
        }
    }
}
