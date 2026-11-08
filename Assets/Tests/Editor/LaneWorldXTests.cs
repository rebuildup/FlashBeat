using NUnit.Framework;

/// <summary>
/// レーンのワールド配置の純粋ロジック (カメラ不要)。
/// レーン i の中心 x = i - 3.5 (8 レーン、隣接間隔 1) と、
/// 8 レーンの幅 (レーン 0 と 7 の中心間 = 7) を定数化した Judge.LaneWorldX で検証する。
/// </summary>
public class LaneWorldXTests
{
    [Test]
    public void LaneWorldX_IsLaneIndexMinus3p5_AndSpansEightLanes()
    {
        for (int i = 0; i < 8; i++)
        {
            Assert.AreEqual(i - 3.5f, Judge.LaneWorldX(i), 1e-4f, $"lane {i}");
        }
        Assert.AreEqual(7f, Judge.LaneWorldX(7) - Judge.LaneWorldX(0), 1e-4f,
            "8 レーンの幅 (両端の中心間隔) は 7 ワールド単位");
    }
}
