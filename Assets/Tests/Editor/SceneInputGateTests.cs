using FlashBeat.UI;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// SceneInputGate (Issue #167): オーバーレイ (キー設定 / ランキング) の開いた・
/// 閉じたフレームに、背面の v5 View が同じキーを二重処理しないためのゲート。
/// フレーム番号は FrameSource で注入して検証する。
/// </summary>
public class SceneInputGateTests
{
    private int frame;

    [SetUp]
    public void SetUp()
    {
        frame = 100;
        SceneInputGate.FrameSource = () => frame;
        SceneInputGate.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        SceneInputGate.FrameSource = () => Time.frameCount;
        SceneInputGate.ResetForTests();
    }

    [Test]
    public void InputIsAllowedBeforeAnyOverlay()
    {
        Assert.IsTrue(SceneInputGate.InputAllowed);
    }

    [Test]
    public void BlockThisFrameBlocksTheSameFrame()
    {
        SceneInputGate.BlockThisFrame();
        Assert.IsFalse(SceneInputGate.InputAllowed);
    }

    [Test]
    public void InputIsAllowedAgainOnTheNextFrame()
    {
        SceneInputGate.BlockThisFrame();
        frame++;
        Assert.IsTrue(SceneInputGate.InputAllowed);
    }

    [Test]
    public void OpenAndCloseInTheSameFrameKeepBlockingThatFrame()
    {
        // 開いてすぐ閉じても、そのフレームの背面入力は止まったまま。
        SceneInputGate.BlockThisFrame();
        SceneInputGate.BlockThisFrame();
        Assert.IsFalse(SceneInputGate.InputAllowed);
        frame++;
        Assert.IsTrue(SceneInputGate.InputAllowed);
    }

    [Test]
    public void BlockingAnOlderFrameDoesNotExtendTheBlock()
    {
        SceneInputGate.BlockThisFrame();
        frame++;
        // 別のオーバーレイが古いフレーム番号で呼んでもブロックは延長されない。
        SceneInputGate.FrameSource = () => frame - 1;
        SceneInputGate.BlockThisFrame();
        SceneInputGate.FrameSource = () => frame;
        Assert.IsTrue(SceneInputGate.InputAllowed);
    }
}
