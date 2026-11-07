using NUnit.Framework;

/// <summary>
/// KeyConfigUI のシーン許可 (Issue #167): F2 の競合解消。
/// Select 画面の F2 は v5 凡例どおり「履歴」(SelectView) が担うため、
/// キー設定オーバーレイは Title / Option でしか開けない。
/// </summary>
public class KeyConfigSceneAllowTests
{
    [Test]
    public void TitleAndOptionAllowTheKeyConfig()
    {
        Assert.IsTrue(KeyConfigUI.IsAllowedSceneName("TitleScene"));
        Assert.IsTrue(KeyConfigUI.IsAllowedSceneName("OptionScene"));
    }

    [Test]
    public void SelectDoesNotAllowTheKeyConfig()
    {
        // Select の F2 は履歴 (SelectView.EnterHistory) と競合するため開けない。
        Assert.IsFalse(KeyConfigUI.IsAllowedSceneName("SelectScene"));
    }

    [Test]
    public void GameplayScenesDoNotAllowTheKeyConfig()
    {
        Assert.IsFalse(KeyConfigUI.IsAllowedSceneName("GameScene"));
        Assert.IsFalse(KeyConfigUI.IsAllowedSceneName("ResultScene"));
        Assert.IsFalse(KeyConfigUI.IsAllowedSceneName("OpeningScene"));
    }

    [Test]
    public void UnknownSceneNamesAreRejected()
    {
        Assert.IsFalse(KeyConfigUI.IsAllowedSceneName(null));
        Assert.IsFalse(KeyConfigUI.IsAllowedSceneName(""));
        Assert.IsFalse(KeyConfigUI.IsAllowedSceneName("MakeFileScene"));
    }
}
