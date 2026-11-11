using FlashBeat.UI.V6.Title;
using NUnit.Framework;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #373: History / Ranking の「戻る」は入口へ戻る。
    /// Title から開いたら Title、選曲画面から開いたら選曲画面。
    /// </summary>
    public class SelectBackTargetTests
    {
        [Test]
        public void FromTitle_ReturnsToTitle()
        {
            Assert.IsTrue(SelectSceneManager.ShouldReturnToTitle(V6TitleLinks.EntryScreen.History));
            Assert.IsTrue(SelectSceneManager.ShouldReturnToTitle(V6TitleLinks.EntryScreen.Ranking));
        }

        [Test]
        public void FromSelect_StaysInSelect()
        {
            Assert.IsFalse(SelectSceneManager.ShouldReturnToTitle(V6TitleLinks.EntryScreen.None));
        }
    }
}
