using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Game.Loading;

namespace FlashBeat.V6HudTests
{
    /// <summary>
    /// 譜面準備の実進捗の共有口 (Issue #224) の EditMode テスト。
    /// 静的状態なので各テストで ResetNotes してから検証する。
    /// </summary>
    public class GameLoadingProgressTests
    {
        [SetUp]
        public void SetUp()
        {
            GameLoadingProgress.ResetNotes();
        }

        [Test]
        public void Initially_Unknown()
        {
            Assert.IsFalse(GameLoadingProgress.HasNotesProgress);
            Assert.AreEqual(-1f, GameLoadingProgress.NotesProgress);
        }

        [Test]
        public void ReportNotes_ZeroStaysKnown()
        {
            // 0 は「準備が始まったばかり」の既知値。不明 (-1) に戻さない。
            GameLoadingProgress.ReportNotes(0f);
            Assert.IsTrue(GameLoadingProgress.HasNotesProgress);
            Assert.AreEqual(0f, GameLoadingProgress.NotesProgress, 1e-6f);
        }

        [Test]
        public void ReportNotes_ClampsToUnitRange()
        {
            GameLoadingProgress.ReportNotes(-0.5f);
            Assert.AreEqual(0f, GameLoadingProgress.NotesProgress, 1e-6f);

            GameLoadingProgress.ReportNotes(1.5f);
            Assert.AreEqual(1f, GameLoadingProgress.NotesProgress, 1e-6f);
        }

        [Test]
        public void ReportNotes_MonotonicValueIsLastReport()
        {
            GameLoadingProgress.ReportNotes(0.25f);
            GameLoadingProgress.ReportNotes(0.75f);
            Assert.AreEqual(0.75f, GameLoadingProgress.NotesProgress, 1e-6f);
        }

        [Test]
        public void ResetNotes_ReturnsToUnknown()
        {
            GameLoadingProgress.ReportNotes(0.9f);
            GameLoadingProgress.ResetNotes();
            Assert.IsFalse(GameLoadingProgress.HasNotesProgress);
        }
    }
}
