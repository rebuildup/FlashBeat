using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Select.View;
using NUnit.Framework;

/// <summary>
/// 実Win64画面 / ウィンドウ基準 (Issue #162 → #167 Game D → #740)。
///
/// FHD 1920x1080 と 2K 2560x1440 の 2 条件について、
/// - UISizeProfile の選択 (#169 が契約化した規則)
/// - v6 Select の design スケール (V6SelectViewLogic.DesignScale) が
///   design 1728x972 基準の等比縮小であること
/// をコード側で固定する。実機確認 (Mac 2560x1664 を含む) はオーナーゲート。
/// (v5 TitleLayout / SelectView は v6 固定フレーム化で削除済み — Issue #497 / #740)
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
    public void V6SelectDesignScale_FitsTheDesignFrameAtBothConditions()
    {
        // v6 design フレーム (1728x972) が実画面に収まる等比倍率。
        // 16:9 の 2 条件では 幅基準 = 高さ基準 で一致する。
        Assert.AreEqual(1920f / 1728f, V6SelectViewLogic.DesignScale(1920, 1080), 0.001f);
        Assert.AreEqual(2560f / 1728f, V6SelectViewLogic.DesignScale(2560, 1440), 0.001f);
    }

    [Test]
    public void V6SelectDesignScale_ShrinksForSmallerWindowsWithoutClipping()
    {
        // ウィンドウを縮めても 0 < scale <= 1 で縮小される (クリップしない)。
        // 16:10 は幅基準 (min(w/1728, h/972))。
        Assert.AreEqual(1280f / 1728f, V6SelectViewLogic.DesignScale(1280, 720), 0.001f);
        Assert.AreEqual(1440f / 1728f, V6SelectViewLogic.DesignScale(1440, 900), 0.001f);
        Assert.AreEqual(1600f / 1728f, V6SelectViewLogic.DesignScale(1600, 900), 0.001f);
    }

    [Test]
    public void V6SelectDesignScale_FallsBackToOneOnDegenerateSize()
    {
        Assert.AreEqual(1f, V6SelectViewLogic.DesignScale(0, 0));
        Assert.AreEqual(1f, V6SelectViewLogic.DesignScale(-1, 0));
    }

    [Test]
    public void HudScaleFactorFollowsTheProfileAtBothConditions()
    {
        // v5 HUD 系の共通倍率 (S = Margin / 48) のプロファイル選択契約。
        // FHD で 1.0、2K で 1.333 (48 → 64)。v6 HUD は design フレーム基準の
        // 別スケールを使うため、ここでは UISizeProfile 側の値だけを固定する。
        Assert.AreEqual(1f, UISizeProfile.Select(1920, 1080).Margin / 48f, 0.001f);
        Assert.AreEqual(1.333f, UISizeProfile.Select(2560, 1440).Margin / 48f, 0.001f);
    }

    [Test]
    public void FontScaleFactorFollowsTheProfileAtBothConditions()
    {
        // Result/Option 系の共通倍率 (S(px) = px * FsM / 30)。
        // FHD で 1.0、2K で 1.333 (30 → 40)。
        Assert.AreEqual(1f, UISizeProfile.Select(1920, 1080).FsM / 30f, 0.001f);
        Assert.AreEqual(1.333f, UISizeProfile.Select(2560, 1440).FsM / 30f, 0.001f);
    }
}
