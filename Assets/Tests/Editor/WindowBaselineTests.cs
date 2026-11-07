using FlashBeat.UI.V5;
using NUnit.Framework;

/// <summary>
/// 実Win64画面 / ウィンドウ基準 (Issue #162 → #167 Game D)。
///
/// FHD 1920x1080 と 2K 2560x1440 の 2 条件について、
/// - UISizeProfile の選択 (#169 が契約化した規則)
/// - SelectView の design スケール (u) がプロファイル倍率と一致すること
///   (16:9 の 2 条件では 2 つのスケーリング系統が一致する契約)
/// - TitleLayout がウィンドウに収まること
/// をコード側で固定する。実機確認 (Mac 2560x1664 を含む) はオーナーゲート。
/// </summary>
public class WindowBaselineTests
{
    /// <summary>ウィンドウ基準の 2 条件 (Win64)。</summary>
    private static readonly (int w, int h)[] Win64WindowConditions =
    {
        (1920, 1080),
        (2560, 1440),
    };

    [Test]
    public void ProfileMatchesTheTwoWin64Conditions()
    {
        Assert.AreEqual("fhd", UISizeProfile.Select(1920, 1080).Name);
        Assert.AreEqual("2k", UISizeProfile.Select(2560, 1440).Name);
    }

    [Test]
    public void SelectViewDesignScaleAgreesWithProfileAtBothConditions()
    {
        foreach (var (w, h) in Win64WindowConditions)
        {
            var profile = UISizeProfile.Select(w, h);
            float profileScale = profile.Margin / 48f; // 48 = fhd の Margin (基準)
            Assert.AreEqual(profileScale, SelectView.DesignScale(w, h), 0.01f,
                "16:9 の 2 条件では SelectView の u と UISizeProfile の倍率が一致するはず: " + w + "x" + h);
        }
    }

    [Test]
    public void SelectViewDesignScaleShrinksForSmallerWindowsWithoutClipping()
    {
        // ウィンドウを縮めても 0 < scale <= 1 で縮小される (クリップしない)。
        Assert.AreEqual(720f / 1080f, SelectView.DesignScale(1280, 720), 0.001f);
        Assert.AreEqual(0.75f, SelectView.DesignScale(1440, 900), 0.001f); // 16:10 は幅基準
        Assert.AreEqual(1600f / 1920f, SelectView.DesignScale(1600, 900), 0.001f);
    }

    [Test]
    public void SelectViewDesignScaleFallsBackToOneOnDegenerateSize()
    {
        Assert.AreEqual(1f, SelectView.DesignScale(0, 0));
        Assert.AreEqual(1f, SelectView.DesignScale(-1, 0));
    }

    [Test]
    public void TitleLayoutFitsBothWin64WindowConditions()
    {
        foreach (var (w, h) in Win64WindowConditions)
        {
            var r = TitleLayout.Compute(w, h, visibleRows: 4);
            Assert.LessOrEqual(r.Menu.yMax, h, "menu exceeds the window at " + w + "x" + h);
            Assert.LessOrEqual(r.Logo.xMax, w, "logo exceeds the window at " + w + "x" + h);
            Assert.LessOrEqual(r.BarV.xMax, w, "frame bar exceeds the window at " + w + "x" + h);
        }
    }

    [Test]
    public void PauseAndHudScaleFactorFollowsTheProfileAtBothConditions()
    {
        // GameHudView / PauseMenuView / GameReadyView / GameErrorView の共通倍率
        // (S = Margin / 48)。FHD で 1.0、2K で 1.333 (48 → 64)。
        Assert.AreEqual(1f, UISizeProfile.Select(1920, 1080).Margin / 48f, 0.001f);
        Assert.AreEqual(1.333f, UISizeProfile.Select(2560, 1440).Margin / 48f, 0.001f);
    }

    [Test]
    public void ResultAndOptionScaleFactorFollowsTheProfileAtBothConditions()
    {
        // ResultView / OptionView の共通倍率 (S(px) = px * FsM / 30)。
        // FHD で 1.0、2K で 1.333 (30 → 40)。
        Assert.AreEqual(1f, UISizeProfile.Select(1920, 1080).FsM / 30f, 0.001f);
        Assert.AreEqual(1.333f, UISizeProfile.Select(2560, 1440).FsM / 30f, 0.001f);
    }
}
