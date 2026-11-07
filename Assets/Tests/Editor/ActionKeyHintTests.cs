using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V5;

public class ActionKeyHintTests
{
    private sealed class MemoryStorage : KeyActions.IStorage
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
    }

    [TearDown]
    public void TearDown()
    {
        KeyActions.SetStorage(null);
    }

    [Test]
    public void DefaultStartDisplaysDistinctAcceptedKeys()
    {
        Assert.AreEqual("Space", ActionKeyHint.Primary(KeyActions.KeyAction.Start));
        Assert.AreEqual("Space / Enter", ActionKeyHint.All(KeyActions.KeyAction.Start));
    }

    [Test]
    public void UserBindingChangesAreReflectedInCaps()
    {
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Retry, new[] { KeyCode.F8 }));
        Assert.AreEqual("F8", ActionKeyHint.Primary(KeyActions.KeyAction.Retry));
        Assert.AreEqual("F8", ActionKeyHint.All(KeyActions.KeyAction.Retry));
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Back, new[] { KeyCode.Backspace }));
        Assert.AreEqual("BS", ActionKeyHint.Primary(KeyActions.KeyAction.Back));
        Assert.AreEqual("Backspace", ActionKeyHint.All(KeyActions.KeyAction.Back));
    }

    [Test]
    public void DisplayNamesAreCompactAndConsistent()
    {
        Assert.AreEqual("Enter", ActionKeyHint.KeyName(KeyCode.Return));
        Assert.AreEqual("Enter", ActionKeyHint.KeyName(KeyCode.KeypadEnter));
        Assert.AreEqual("Esc", ActionKeyHint.KeyName(KeyCode.Escape));
        Assert.AreEqual("←", ActionKeyHint.KeyName(KeyCode.LeftArrow));
        Assert.AreEqual("9", ActionKeyHint.KeyName(KeyCode.Alpha9));
        Assert.AreEqual(";", ActionKeyHint.KeyName(KeyCode.Semicolon));
    }

    [Test]
    public void EmptyTemporaryBindingHasExplicitUnassignedLabel()
    {
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Retry, new KeyCode[0]));
        Assert.AreEqual("—", ActionKeyHint.Primary(KeyActions.KeyAction.Retry));
        Assert.AreEqual("—", ActionKeyHint.All(KeyActions.KeyAction.Retry));
    }
}
