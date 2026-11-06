using NUnit.Framework;

/// <summary>
/// Contract of the loading overlay display logic (issue #113): the overlay only
/// appears for loads that actually take time, stays visible for a minimum time
/// once shown, and maps the async scene load progress (which stops at 0.9 while
/// activation is blocked) onto a full 0..100% bar.
/// </summary>
public class LoadingOverlayLogicTests
{
    [Test]
    public void OverlayDoesNotAppearForShortLoads()
    {
        Assert.IsFalse(LoadingOverlayLogic.ShouldBecomeVisible(0.0));
        Assert.IsFalse(LoadingOverlayLogic.ShouldBecomeVisible(LoadingOverlayLogic.ShowDelayMs - 1.0));
    }

    [Test]
    public void OverlayAppearsAfterTheShowDelay()
    {
        Assert.IsTrue(LoadingOverlayLogic.ShouldBecomeVisible(LoadingOverlayLogic.ShowDelayMs));
        Assert.IsTrue(LoadingOverlayLogic.ShouldBecomeVisible(10000.0));
    }

    [Test]
    public void OverlayStaysForTheMinimumDisplayTime()
    {
        Assert.IsFalse(LoadingOverlayLogic.CanHide(0.0));
        Assert.IsFalse(LoadingOverlayLogic.CanHide(LoadingOverlayLogic.MinDisplayMs - 1.0));
        Assert.IsTrue(LoadingOverlayLogic.CanHide(LoadingOverlayLogic.MinDisplayMs));
    }

    [Test]
    public void AsyncProgressIsMappedToAFullBar()
    {
        Assert.AreEqual(0f, LoadingOverlayLogic.MapProgress(0f));
        Assert.AreEqual(0.5f, LoadingOverlayLogic.MapProgress(0.45f), 0.0001f);
        // allowSceneActivation=false stops at 0.9 - that is 100% for the player.
        Assert.AreEqual(1f, LoadingOverlayLogic.MapProgress(0.9f), 0.0001f);
        Assert.AreEqual(1f, LoadingOverlayLogic.MapProgress(1f));
    }

    [Test]
    public void ProgressIsClampedToTheValidRange()
    {
        Assert.AreEqual(0f, LoadingOverlayLogic.MapProgress(-0.2f));
        Assert.AreEqual(1f, LoadingOverlayLogic.MapProgress(1.5f));
    }

    [Test]
    public void SceneNamesAreTranslatedToJapaneseScreenNames()
    {
        Assert.AreEqual("ゲーム", LoadingOverlayLogic.SceneLabel("GameScene"));
        Assert.AreEqual("選曲", LoadingOverlayLogic.SceneLabel("SelectScene"));
        Assert.AreEqual("リザルト", LoadingOverlayLogic.SceneLabel("ResultScene"));
        Assert.AreEqual("タイトル", LoadingOverlayLogic.SceneLabel("TitleScene"));
        Assert.AreEqual("オープニング", LoadingOverlayLogic.SceneLabel("Opening"));
    }

    [Test]
    public void UnknownSceneNamesAreShownAsIs()
    {
        Assert.AreEqual("CustomScene", LoadingOverlayLogic.SceneLabel("CustomScene"));
        Assert.AreEqual("", LoadingOverlayLogic.SceneLabel(null));
        Assert.AreEqual("", LoadingOverlayLogic.SceneLabel(""));
    }
}
