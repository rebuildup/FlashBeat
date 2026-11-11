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

    // --- ポーズ中の GameScene (v6 ポーズの「キー設定」行 / F2) ---

    [Test]
    public void PausedGameSceneAllowsTheKeyConfig()
    {
        // v6 ポーズ (uDNC0) の Key Layout 行と「キー設定」行は、演奏を止めた
        // 状態でキー設定を開く。ポーズ中だけ GameScene を許可する。
        FlashBeat.Gameplay.PauseGate.Enter();
        try
        {
            Assert.IsTrue(KeyConfigUI.IsAllowedNow("GameScene"));
        }
        finally
        {
            FlashBeat.Gameplay.PauseGate.HardReset();
        }
    }

    [Test]
    public void PlayingGameSceneDoesNotAllowTheKeyConfig()
    {
        FlashBeat.Gameplay.PauseGate.HardReset();
        Assert.IsFalse(KeyConfigUI.IsAllowedNow("GameScene"));
    }

    [Test]
    public void OtherScenesAreUnaffectedByPause()
    {
        // ポーズ状態は GameScene にだけ効く (Result / Select は従来どおり不可)。
        FlashBeat.Gameplay.PauseGate.Enter();
        try
        {
            Assert.IsFalse(KeyConfigUI.IsAllowedNow("ResultScene"));
            Assert.IsFalse(KeyConfigUI.IsAllowedNow("SelectScene"));
            Assert.IsTrue(KeyConfigUI.IsAllowedNow("OptionScene"));
        }
        finally
        {
            FlashBeat.Gameplay.PauseGate.HardReset();
        }
    }
}
