using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 読み込み中表示 (MediaLoadingOverlay) の EditMode テスト。
/// - 実行時に uGUI を組み立てるため、シーンには何も依存しない。
/// - 入力 (クリック) を妨げないこと、表示・非表示が正しいことを確かめる。
/// </summary>
public class MediaLoadingOverlayTests
{
    [TearDown]
    public void TearDown()
    {
        MediaLoadingOverlay.ClearForTests();
    }

    [Test]
    public void Ensure_CreatesTheOverlay()
    {
        MediaLoadingOverlay overlay = MediaLoadingOverlay.Ensure();
        Assert.IsNotNull(overlay, "the loading overlay must be created");
    }

    [Test]
    public void Ensure_ReturnsTheSameInstance()
    {
        MediaLoadingOverlay first = MediaLoadingOverlay.Ensure();
        MediaLoadingOverlay second = MediaLoadingOverlay.Ensure();
        Assert.AreSame(first, second, "the overlay must be a singleton per scene");
    }

    [Test]
    public void Show_ShowsTheTitleAndTheStatus()
    {
        MediaLoadingOverlay overlay = MediaLoadingOverlay.Ensure();
        overlay.Show("動画をダウンロード中…");
        Assert.IsTrue(overlay.IsVisible, "the overlay must be visible while loading");
        Assert.IsTrue(overlay.TitleText.Contains("読み込み中"), "the title must say 読み込み中, but was: " + overlay.TitleText);
        Assert.IsTrue(overlay.StatusText.Contains("ダウンロード"), "the status must show the phase, but was: " + overlay.StatusText);
    }

    [Test]
    public void Hide_HidesTheOverlay()
    {
        MediaLoadingOverlay overlay = MediaLoadingOverlay.Ensure();
        overlay.Show("準備中…");
        overlay.Hide();
        Assert.IsFalse(overlay.IsVisible, "the overlay must be hidden after the media is ready");
    }

    [Test]
    public void ShowError_ShowsTheReason()
    {
        MediaLoadingOverlay overlay = MediaLoadingOverlay.Ensure();
        overlay.ShowError("動画をダウンロードできませんでした");
        Assert.IsTrue(overlay.IsVisible, "the error must be visible");
        Assert.IsTrue(overlay.StatusText.Contains("ダウンロード"), "the error detail must be shown, but was: " + overlay.StatusText);
    }

    [Test]
    public void TheOverlayDoesNotBlockClicks()
    {
        MediaLoadingOverlay overlay = MediaLoadingOverlay.Ensure();
        overlay.Show("準備中…");
        var graphics = Resources.FindObjectsOfTypeAll<UnityEngine.UI.Graphic>();
        int checkedCount = 0;
        for (int i = 0; i < graphics.Length; i++)
        {
            var graphic = graphics[i];
            if (graphic == null) continue;
            if (!graphic.gameObject.activeInHierarchy) continue;
            if (graphic.transform.root == null) continue;
            if (graphic.transform.root.name != "MediaLoadingOverlay") continue;
            checkedCount++;
            Assert.IsFalse(graphic.raycastTarget, "the loading overlay must not swallow clicks: " + graphic.name);
        }
        Assert.Greater(checkedCount, 0, "the overlay must have graphics to check");
    }
}
