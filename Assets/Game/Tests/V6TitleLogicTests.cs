using FlashBeat.Update;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Title;

/// <summary>
/// V6TitleLogic (Title v6 の表示文 / 表示条件) のテスト。
/// 実データ (GManager / UpdateFlowModel) から作る文言が、design 例の固定値に
/// 落ちないことを固定する。
/// </summary>
public class V6TitleLogicTests
{
    // --- FrameOrigin (1:1 固定中央配置) ---
    // 旧 DesignScale (min(w/1728, h/972) の等比拡大) の期待値テストは廃止:
    // design フレームは固定解像度で、引き伸ばしは一切しない (Owner 指示による契約変更)。
    // 正本: design POJwG "Resolution Check · Title" の実配置
    // (FHD 1920×1080 → (96,54) / 2K 2560×1440 → (416,234) / Mac 2560×1664 → (416,346))。

    [Test]
    public void FrameOrigin_Fhd_MatchesDesignPlacement()
    {
        Assert.AreEqual(new Vector2(96f, 54f), V6TitleLogic.FrameOrigin(1920, 1080));
    }

    [Test]
    public void FrameOrigin_2k_And_Mac_MatchDesignPlacement()
    {
        Assert.AreEqual(new Vector2(416f, 234f), V6TitleLogic.FrameOrigin(2560, 1440));
        Assert.AreEqual(new Vector2(416f, 346f), V6TitleLogic.FrameOrigin(2560, 1664));
    }

    [Test]
    public void FrameOrigin_IsCenteredAtEveryResolution()
    {
        // (w−1728)/2, (h−972)/2 の切り捨て (design の定義)。
        Assert.AreEqual(new Vector2(96f, 54f), V6TitleLogic.FrameOrigin(1921, 1081));
        Assert.AreEqual(new Vector2(416f, 346f), V6TitleLogic.FrameOrigin(2561, 1665));
    }

    [Test]
    public void FrameOrigin_InvalidSize_ReturnsZero()
    {
        Assert.AreEqual(Vector2.zero, V6TitleLogic.FrameOrigin(0, 1080));
        Assert.AreEqual(Vector2.zero, V6TitleLogic.FrameOrigin(1920, -1));
    }

    // --- PlayableSongCount ---

    [Test]
    public void PlayableSongCount_NullArray_IsZero()
    {
        Assert.AreEqual(0, V6TitleLogic.PlayableSongCount(null));
    }

    [Test]
    public void PlayableSongCount_SkipsPlaceholderAndUnplayable()
    {
        var songs = new[]
        {
            new SongData(0, "noSong", "", "", 0, 0, 0, 0f),                 // index 0 は常にプレースホルダ
            new SongData(1, "A", "", "", 0, 0, 0, 0f),
            new SongData(2, "B", "", "", 0, 0, 0, 0f) { unplayable = true }, // 譜面のない曲は数えない
            new SongData(3, "C", "", "", 0, 0, 0, 0f),
        };

        Assert.AreEqual(2, V6TitleLogic.PlayableSongCount(songs));
    }

    // --- CatalogStatusText ---

    [Test]
    public void CatalogStatusText_ReportsCountAndRemoteState()
    {
        StringAssert.Contains("3", V6TitleLogic.CatalogStatusText(true, 3));
        StringAssert.Contains("クラウド", V6TitleLogic.CatalogStatusText(true, 3));
        StringAssert.Contains("ローカル", V6TitleLogic.CatalogStatusText(false, 3));
    }

    // --- VersionLabel ---

    [Test]
    public void VersionLabel_PrefixesV_AndEmptyStaysEmpty()
    {
        Assert.AreEqual("v1.2.3", V6TitleLogic.VersionLabel("1.2.3"));
        Assert.AreEqual("", V6TitleLogic.VersionLabel(null));
        Assert.AreEqual("", V6TitleLogic.VersionLabel(""));
    }

    // --- 更新ボタン (UpdateFlowModel の実状態に対応) ---

    [Test]
    public void ShowUpdateButton_OnlyWhenUpdateFlowIsActive()
    {
        Assert.IsFalse(V6TitleLogic.ShowUpdateButton(UpdateFlowPhase.Idle));
        Assert.IsFalse(V6TitleLogic.ShowUpdateButton(UpdateFlowPhase.Checking));
        Assert.IsTrue(V6TitleLogic.ShowUpdateButton(UpdateFlowPhase.Available));
        Assert.IsTrue(V6TitleLogic.ShowUpdateButton(UpdateFlowPhase.Downloading));
        Assert.IsTrue(V6TitleLogic.ShowUpdateButton(UpdateFlowPhase.Ready));
        Assert.IsFalse(V6TitleLogic.ShowUpdateButton(UpdateFlowPhase.Applying));
    }

    [Test]
    public void UpdateButtonLabel_DownloadingShowsProgress_OthersShowDesignLabel()
    {
        StringAssert.Contains("37%", V6TitleLogic.UpdateButtonLabel(UpdateFlowPhase.Downloading, 0.37f));
        // 進捗は 0..1 にクランプする (異常値で変な文言を出さない)。
        StringAssert.Contains("100%", V6TitleLogic.UpdateButtonLabel(UpdateFlowPhase.Downloading, 1.5f));
        Assert.AreEqual("更新して再起動", V6TitleLogic.UpdateButtonLabel(UpdateFlowPhase.Available, 0f));
        Assert.AreEqual("更新して再起動", V6TitleLogic.UpdateButtonLabel(UpdateFlowPhase.Ready, 0f));
    }

    [Test]
    public void ShowNewVersion_RequiresKnownLatestVersion()
    {
        Assert.IsFalse(V6TitleLogic.ShowNewVersion(UpdateFlowPhase.Available, null));
        Assert.IsFalse(V6TitleLogic.ShowNewVersion(UpdateFlowPhase.Available, ""));
        Assert.IsTrue(V6TitleLogic.ShowNewVersion(UpdateFlowPhase.Available, "1.2.3"));
        Assert.IsFalse(V6TitleLogic.ShowNewVersion(UpdateFlowPhase.Idle, "1.2.3"));
    }
}
