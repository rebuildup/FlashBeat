using System.IO;
using FlashBeat.Gameplay;
using FlashBeat.UI;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// KeyGuideUI の Tab 開閉が、ポーズ相と 1 フレームのオーバーレイ境界で
/// 正しく抑止されることを検証する (Issue #360)。
/// 実キー入力は EditMode で再現できないため、判定シーム
/// <see cref="KeyGuideUI.CanHandleTabInput"/> と LateUpdate の配線を分けて固定する。
/// </summary>
public class KeyGuideInputTests
{
    private int frame;
    private float savedStepTime;
    private float savedStartTime;

    private static string ReadSource(string relativePath)
    {
        return File.ReadAllText(Path.Combine(Application.dataPath, relativePath));
    }

    [SetUp]
    public void SetUp()
    {
        frame = 100;
        savedStepTime = GManager.step_time;
        savedStartTime = GManager.StartTime;
        SceneInputGate.FrameSource = () => frame;
        SceneInputGate.ResetForTests();
        PauseGate.HardReset();
    }

    [TearDown]
    public void TearDown()
    {
        PauseGate.HardReset();
        GManager.step_time = savedStepTime;
        GManager.StartTime = savedStartTime;
        SceneInputGate.FrameSource = () => Time.frameCount;
        SceneInputGate.ResetForTests();
    }

    [Test]
    public void TabInputIsAllowedOutsidePause()
    {
        Assert.IsTrue(KeyGuideUI.CanHandleTabInput());
    }

    [Test]
    public void TabInputIsSuppressedWhilePauseGateIsActive()
    {
        PauseGate.Enter();
        Assert.IsTrue(PauseGate.IsPaused);
        Assert.IsFalse(KeyGuideUI.CanHandleTabInput());

        PauseGate.Exit();
        Assert.IsFalse(PauseGate.IsPaused);
        Assert.IsTrue(KeyGuideUI.CanHandleTabInput());
    }

    [Test]
    public void TabInputIsSuppressedOnBlockedFrame()
    {
        SceneInputGate.BlockThisFrame();
        Assert.IsFalse(KeyGuideUI.CanHandleTabInput());

        frame++;
        Assert.IsTrue(KeyGuideUI.CanHandleTabInput());
    }

    [Test]
    public void TabInputRequiresAllowedFrameAndNoPause()
    {
        PauseGate.Enter();
        SceneInputGate.BlockThisFrame();
        Assert.IsFalse(KeyGuideUI.CanHandleTabInput());

        frame++;
        Assert.IsFalse(KeyGuideUI.CanHandleTabInput());

        PauseGate.Exit();
        Assert.IsTrue(KeyGuideUI.CanHandleTabInput());
    }

    [Test]
    public void KeyGuideReadsTabInLateUpdateBehindThePauseGate()
    {
        string src = ReadSource("Game/Scripts/UI/KeyGuideUI.cs");
        Assert.IsTrue(src.Contains("private void LateUpdate()"),
            "Tab 入力は LateUpdate で読む (Update 順に依存しないため)");
        Assert.IsFalse(src.Contains("private void Update()"),
            "KeyGuideUI に Update の Tab 経路を残さない");
        Assert.IsTrue(src.Contains("Input.GetKeyDown(KeyCode.Tab)"),
            "Tab の押下判定が無い");
        Assert.IsTrue(src.Contains("FlashBeat.Gameplay.PauseGate.IsPaused"),
            "ポーズ相の抑止が無い");
        Assert.IsTrue(src.Contains("FlashBeat.UI.SceneInputGate.InputAllowed"),
            "オーバーレイ境界の抑止が無い");
        Assert.IsTrue(src.Contains("CanHandleTabInput()"),
            "LateUpdate が CanHandleTabInput を通っていない");
    }

    [Test]
    public void PauseConsumesMenuSwitchOnlyWhileOpen()
    {
        string src = ReadSource("Game/Scripts/UI/V6/V6GameRuntime.cs");
        int openGuard = src.IndexOf("if (!pause.IsOpen) return;", System.StringComparison.Ordinal);
        int menuSwitch = src.IndexOf("KeyActions.IsDown(KeyActions.KeyAction.MenuSwitch)", System.StringComparison.Ordinal);
        Assert.Greater(openGuard, -1, "ポーズ非表示中は MenuSwitch を読まないガードが無い");
        Assert.Greater(menuSwitch, -1, "MenuSwitch の読み取りが無い");
        Assert.Less(openGuard, menuSwitch,
            "MenuSwitch は pause.IsOpen のガードより後で読むこと");
    }
}
