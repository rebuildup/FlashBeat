using FlashBeat.Gameplay;
using NUnit.Framework;

/// <summary>
/// Issue #844: VideoFrameAudit の suspect 判定 (IsSuspectScore) の契約。
///
/// 勾配エネルギーがローリング中央値から SuspectRatio 倍を超える
/// 「増大」(モザイク/ブロックのエッジ増加) と、1/SuspectRatio を下回る
/// 「低下」(黒矩形/平滑化) の両方向を suspect とする。
/// 基準サンプル不足・中央値がほぼ 0 のときは判定しない。
/// </summary>
[TestFixture]
public class VideoFrameAuditTests
{
    [Test]
    public void EnergyJump_IsSuspect()
    {
        Assert.IsTrue(VideoFrameAudit.IsSuspectScore(10f, 1f, VideoFrameAudit.MinBaselineSamples));
    }

    [Test]
    public void EnergyCollapse_IsSuspect()
    {
        // 黒矩形・モザイク平滑化などエネルギーが下がる破損も拾う (v2 で追加)。
        Assert.IsTrue(VideoFrameAudit.IsSuspectScore(0.2f, 1f, VideoFrameAudit.MinBaselineSamples));
    }

    [Test]
    public void NormalVariation_IsNotSuspect()
    {
        Assert.IsFalse(VideoFrameAudit.IsSuspectScore(2.5f, 1f, VideoFrameAudit.MinBaselineSamples));
        Assert.IsFalse(VideoFrameAudit.IsSuspectScore(0.5f, 1f, VideoFrameAudit.MinBaselineSamples));
    }

    [Test]
    public void InsufficientBaseline_IsNotSuspect()
    {
        Assert.IsFalse(VideoFrameAudit.IsSuspectScore(100f, 1f, VideoFrameAudit.MinBaselineSamples - 1));
    }

    [Test]
    public void NearZeroBaseline_IsNotSuspect()
    {
        // 暗いシーンで中央値が ~0 だと ratio が暴れるので判定しない。
        Assert.IsFalse(VideoFrameAudit.IsSuspectScore(1f, 1e-6f, VideoFrameAudit.MinBaselineSamples));
    }
}
