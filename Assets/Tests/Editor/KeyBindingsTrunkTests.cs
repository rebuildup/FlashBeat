using NUnit.Framework;
using UnityEngine;

/// <summary>
/// trunk 専用の統合テスト (v0.1.2 パッチには含めない)。
/// KeyBindings の既定値が Judge.LaneKeys と一致すること、
/// KeyGuideUI が KeyBindings を表示することを確認する。
/// </summary>
public class KeyBindingsTrunkTests
{
    [TearDown]
    public void TearDown()
    {
        KeyBindings.ResetToDefaults();
    }

    [Test]
    public void TestKeyBindingsDefaultsMatchJudgeLaneKeys()
    {
        Assert.AreEqual(Judge.LaneKeys.Length, KeyBindings.Current.Length);
        for (int lane = 0; lane < Judge.LaneKeys.Length; lane++)
        {
            Assert.AreEqual(Judge.LaneKeys[lane], KeyBindings.GetKeys(lane),
                $"Lane {lane}: KeyBindings defaults diverge from Judge.LaneKeys.");
        }
    }

    [Test]
    public void TestKeyGuideLabelReflectsKeyBindings()
    {
        Assert.IsTrue(KeyBindings.ToggleKey(0, KeyCode.P)); // レーン 8 からレーン 1 へ移動
        string label = KeyGuideUI.BuildLaneLabel(0);
        Assert.IsTrue(label.Contains("P"), $"Lane 1 label '{label}' must reflect KeyBindings.");
        Assert.IsFalse(KeyGuideUI.BuildLaneLabel(7).Contains("P"), "Lane 8 label must not contain the moved key.");
    }

    [Test]
    public void TestKeyGuideRowRefreshesOnBindingChange()
    {
        var host = new GameObject("KeyGuideHost");
        try
        {
            KeyGuideUI guide = KeyGuideUI.Create(host.transform);
            string before = guide.GetRowLabel(0);
            Assert.IsNotNull(before);
            Assert.IsFalse(before.Contains("P"), "P must not be in lane 1 by default.");

            KeyBindings.ToggleKey(0, KeyCode.P); // Changed → 行が自動更新される
            string after = guide.GetRowLabel(0);
            Assert.IsTrue(after.Contains("P"), $"Lane 1 row '{after}' must refresh on KeyBindings.Changed.");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }
}
