using FlashBeat.UI.V5;
using NUnit.Framework;

/// <summary>
/// ポーズメニューの設定行 (Issue #167): NoteSpeed の増減は
/// Option 画面 / Select の Ready と同じ OptionSettings の範囲 (1..50) にクランプする。
/// 旧実装の固定 1..30 だと、40 で開いたポーズで 1 回 ←→ を押しただけで値が 30 まで飛んだ。
/// </summary>
public class PauseMenuViewTests
{
    [Test]
    public void StepFollowsTheOptionSettingsRange()
    {
        Assert.AreEqual(41f, PauseMenuView.NextNoteSpeed(40f, 1));
        Assert.AreEqual(39f, PauseMenuView.NextNoteSpeed(40f, -1));
        Assert.AreEqual(31f, PauseMenuView.NextNoteSpeed(30f, 1));
    }

    [Test]
    public void ClampsToTheSameRangeAsOptionAndReady()
    {
        Assert.AreEqual((int)OptionSettings.NoteSpeedMax, PauseMenuView.NextNoteSpeed(50f, 1));
        Assert.AreEqual((int)OptionSettings.NoteSpeedMin, PauseMenuView.NextNoteSpeed(1f, -1));
    }

    [Test]
    public void DoesNotJumpDownWhenOpenedAboveTheOldLimit()
    {
        // 監査で見つかった退行: 旧実装 (1..30) では 40 → 30 にジャンプしていた。
        float value = 40f;
        value = PauseMenuView.NextNoteSpeed(value, 1);
        Assert.AreEqual(41f, value);
        value = PauseMenuView.NextNoteSpeed(value, -1);
        Assert.AreEqual(40f, value);
    }

    [Test]
    public void RoundsToWholeSteps()
    {
        // Option 画面は 0.1 刻みなので小数になりうる。ポーズでは整数ステップで動かす。
        Assert.AreEqual(4f, PauseMenuView.NextNoteSpeed(3.4f, 1));
        Assert.AreEqual(2f, PauseMenuView.NextNoteSpeed(2.6f, -1));
    }
}
