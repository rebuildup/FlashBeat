using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V5;

/// <summary>
/// Issue #148: v5 各画面が表示するキーキャップと実入力 (KeyActions) の一致を検証する。
/// 表示は ActionKeyHint.Primary(KeyActions.Get(action)) と一致しなければならず、
/// 既定 / カスタム (記号キー) / 空割当 のどの状態でも破れないこと。
/// </summary>
public class KeyHintConsistencyTests
{
    private sealed class MemoryStorage : KeyActions.IStorage
    {
        public string Raw;
        public string LoadRaw() { return Raw; }
        public void SaveRaw(string value) { Raw = value; }
    }

    /// <summary>レーン割り当ても既定で固定する (PlayerPrefs の残骸に左右されないため)。</summary>
    private sealed class MemoryLaneStorage : KeyBindings.IStorage
    {
        public string Raw;
        public string LoadRaw() { return Raw; }
        public void SaveRaw(string value) { Raw = value; }
    }

    [SetUp]
    public void SetUp()
    {
        KeyActions.SetStorage(new MemoryStorage());
        KeyActions.ResetToDefaults();
        KeyBindings.SetStorage(new MemoryLaneStorage());
    }

    [TearDown]
    public void TearDown()
    {
        KeyActions.SetStorage(null);
        KeyBindings.SetStorage(null);
    }

    // --- 既定バインド: 各画面の cap は実入力の代表キーと一致する ---

    [Test]
    public void DefaultBindings_TitleDecideCapShowsStartKey()
    {
        // 決定は KeyActions.Start (Space/Enter)。Enter ではなく設定値の先頭を表示する。
        Assert.AreEqual(ActionKeyHint.Primary(KeyActions.KeyAction.Start), TitleView.DecideCap());
        Assert.AreEqual("Space", TitleView.DecideCap());
    }

    [Test]
    public void DefaultBindings_BackCapsShowEscape()
    {
        Assert.AreEqual(ActionKeyHint.Primary(KeyActions.KeyAction.Back), SelectView.BackCap());
        Assert.AreEqual(ActionKeyHint.Primary(KeyActions.KeyAction.Back), OptionView.BackCap());
        Assert.AreEqual("Esc", SelectView.BackCap());
        Assert.AreEqual("Esc", OptionView.BackCap());
    }

    [Test]
    public void DefaultBindings_PauseActionCapsShowMenuKeys()
    {
        Assert.AreEqual("BS", PauseMenuView.RetireCap());
        Assert.AreEqual("R", PauseMenuView.RetryCap());
        Assert.AreEqual("Esc", PauseMenuView.ResumeCap());
    }

    [Test]
    public void DefaultBindings_ResultLegendCapsShowRetryAndConfirm()
    {
        Assert.AreEqual("R", ResultView.RetryLegendCap());
        Assert.AreEqual("Space", ResultView.DecideLegendCap());
    }

    // --- カスタムバインド (記号 / ファンクションキー): cap が追従する ---

    [Test]
    public void CustomSymbolBindings_AreReflectedInCaps()
    {
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Back, new[] { KeyCode.F8 }));
        Assert.AreEqual("F8", SelectView.BackCap());
        Assert.AreEqual("F8", OptionView.BackCap());
        Assert.AreEqual("F8", PauseMenuView.ResumeCap());

        // 記号キーは印字面 (') を表示する。Backslash はレーン 7 の割当キーのため
        // メニュー操作には割り当てできない (別テストで拒否を検証する)。
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.MenuRetire, new[] { KeyCode.Quote }));
        Assert.AreEqual("'", PauseMenuView.RetireCap());

        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Retry, new[] { KeyCode.F12 }));
        Assert.AreEqual("F12", ResultView.RetryLegendCap());
        Assert.AreEqual("F12", PauseMenuView.RetryCap());

        // Title の決定を Backspace に変えると短縮名 BS (幅固定 cap 内に収まる)。
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Start, new[] { KeyCode.Backspace }));
        Assert.AreEqual("BS", TitleView.DecideCap());
    }

    // --- レーンに割り当て済みのキーはメニュー操作に割り当てできない ---

    [Test]
    public void LaneBoundKey_IsRejectedAndCapKeepsPreviousValue()
    {
        KeyCode laneKey = KeyCode.Backslash;
        Assert.IsTrue(KeyActions.IsLaneBound(laneKey), "Backslash must be a lane key by default.");

        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.MenuRetire, new[] { KeyCode.Quote }));
        string before = PauseMenuView.RetireCap();

        Assert.IsFalse(KeyActions.Set(KeyActions.KeyAction.MenuRetire, new[] { laneKey }));
        Assert.IsNotEmpty(KeyActions.LastError);
        Assert.AreEqual(before, PauseMenuView.RetireCap());
        Assert.AreEqual("'", PauseMenuView.RetireCap());
    }

    // --- Result のアクションボタンの cap は index 順に一致する (取り違えない) ---

    [Test]
    public void ResultActionButtonCaps_FollowButtonOrder()
    {
        // 0 = Retry / 1 = Register (固定 Enter) / 2 = OK。
        string[] caps = ResultView.ActionButtonCaps();
        Assert.AreEqual(3, caps.Length);
        Assert.AreEqual(ResultView.RetryLegendCap(), caps[0]);
        Assert.AreEqual("Enter", caps[1]);
        Assert.AreEqual(ResultView.DecideLegendCap(), caps[2]);

        // カスタム割当でも入れ替わらない。
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Retry, new[] { KeyCode.F9 }));
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Confirm, new[] { KeyCode.F10 }));
        caps = ResultView.ActionButtonCaps();
        Assert.AreEqual("F9", caps[0]);
        Assert.AreEqual("F10", caps[2]);
    }

    // --- 空割当: 未割当マーカー (誤った固定表示をしない) ---

    [Test]
    public void EmptyBinding_ShowsUnassignedMarker()
    {
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Back, new KeyCode[0]));
        Assert.AreEqual("—", SelectView.BackCap());
        Assert.AreEqual("—", OptionView.BackCap());
        Assert.AreEqual("—", PauseMenuView.ResumeCap());

        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Retry, new KeyCode[0]));
        Assert.AreEqual("—", ResultView.RetryLegendCap());
    }

    // --- ActionKeyHint.All() は Get() のキー集合を欠落なく列挙する ---

    [Test]
    public void AllCaption_CoversEveryDistinctAcceptedKey()
    {
        foreach (KeyActions.KeyAction action in KeyActions.All)
        {
            KeyCode[] keys = KeyActions.Get(action);
            var distinct = new HashSet<KeyCode>();
            foreach (KeyCode key in keys)
            {
                if (key != KeyCode.None)
                {
                    distinct.Add(key);
                }
            }

            string all = ActionKeyHint.All(action);
            if (distinct.Count == 0)
            {
                Assert.AreEqual("—", all, action + ": unassigned action must show the marker.");
                continue;
            }

            string[] segments = all.Split(new[] { " / " }, StringSplitOptions.None);
            // Enter と keypad Enter は 1 ラベルに畳まれるため、畳み込み分を差し引いて比較する。
            int collapsed = 0;
            bool hasReturn = Array.IndexOf(keys, KeyCode.Return) >= 0;
            bool hasKeypadEnter = Array.IndexOf(keys, KeyCode.KeypadEnter) >= 0;
            if (hasReturn && hasKeypadEnter)
            {
                collapsed = 1;
            }

            Assert.AreEqual(distinct.Count - collapsed, segments.Length,
                action + ": All() must list every accepted key exactly once.");
        }
    }

    // --- 表示側の約束: cap 生成は必ず KeyActions 経由 (固定表を持たない) ---

    [Test]
    public void Caps_TrackBindingChangesImmediately()
    {
        // Scene を介さず静的アクセサ経由で再生成できる = Changed ハンドラも同じ経路で描き直せる。
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Confirm, new[] { KeyCode.Keypad0 }));
        // テンキーは "Num" 接頭辞。Enum 名 ("Keypad0") は 7 文字で固定幅 cap に収まらない。
        Assert.AreEqual("Num0", ResultView.DecideLegendCap());
        Assert.AreNotEqual(KeyCode.Keypad0.ToString(), ResultView.DecideLegendCap());
        Assert.AreEqual(ActionKeyHint.Primary(KeyActions.KeyAction.Confirm), ResultView.DecideLegendCap());
    }

    // --- 表示名の規則: テンキーは "Num" 接頭辞、記号キーは印字面 ---

    [Test]
    public void KeypadKeysUseCompactUnambiguousNames()
    {
        Assert.AreEqual("Num0", ActionKeyHint.KeyName(KeyCode.Keypad0));
        Assert.AreEqual("Num9", ActionKeyHint.KeyName(KeyCode.Keypad9));
        Assert.AreEqual("Num+", ActionKeyHint.KeyName(KeyCode.KeypadPlus));
        Assert.AreEqual("Num-", ActionKeyHint.KeyName(KeyCode.KeypadMinus));
        Assert.AreEqual("Num*", ActionKeyHint.KeyName(KeyCode.KeypadMultiply));
        Assert.AreEqual("Num/", ActionKeyHint.KeyName(KeyCode.KeypadDivide));
        Assert.AreEqual("Num.", ActionKeyHint.KeyName(KeyCode.KeypadPeriod));
        Assert.AreEqual("Enter", ActionKeyHint.KeyName(KeyCode.KeypadEnter));

        // 数字キー列のキーと混同しない。
        Assert.AreEqual("0", ActionKeyHint.KeyName(KeyCode.Alpha0));
        Assert.AreNotEqual(ActionKeyHint.KeyName(KeyCode.Alpha0), ActionKeyHint.KeyName(KeyCode.Keypad0));
    }
}
