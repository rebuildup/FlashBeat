using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V5;

public class ShortcutKeyCapTests
{
    [Test]
    public void KeyNameUsesShortLabels()
    {
        Assert.AreEqual("Space", ShortcutKeyCap.KeyName(KeyCode.Space));
        Assert.AreEqual("Esc", ShortcutKeyCap.KeyName(KeyCode.Escape));
        Assert.AreEqual("Tab", ShortcutKeyCap.KeyName(KeyCode.Tab));
        Assert.AreEqual("Enter", ShortcutKeyCap.KeyName(KeyCode.Return));
        Assert.AreEqual("←", ShortcutKeyCap.KeyName(KeyCode.LeftArrow));
        Assert.AreEqual("BS", ShortcutKeyCap.KeyName(KeyCode.Backspace));
        Assert.AreEqual("R", ShortcutKeyCap.KeyName(KeyCode.R));
        Assert.AreEqual("F2", ShortcutKeyCap.KeyName(KeyCode.F2));
        Assert.AreEqual(string.Empty, ShortcutKeyCap.KeyName(KeyCode.None));
    }

    [Test]
    public void KeysTextJoinsWithSlashAndDropsDuplicates()
    {
        Assert.AreEqual("Space / Enter",
            ShortcutKeyCap.KeysText(new[] { KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter }));
        Assert.AreEqual(string.Empty, ShortcutKeyCap.KeysText(new KeyCode[0]));
        Assert.AreEqual(string.Empty, ShortcutKeyCap.KeysText(null));
    }

    [Test]
    public void CapIsBuiltWithMeasuredWidth()
    {
        var parent = new GameObject("Parent", typeof(RectTransform));
        try
        {
            var cap = ShortcutKeyCap.Create(parent.GetComponent<RectTransform>(), "Tab", 18f);
            Assert.IsNotNull(cap);
            var rt = cap.GetComponent<RectTransform>();
            Assert.Greater(rt.sizeDelta.x, 0f);
            Assert.AreEqual(ShortcutKeyCap.CapHeight, rt.sizeDelta.y, 0.01f);
            Assert.AreEqual(1, parent.transform.childCount);
        }
        finally
        {
            Object.DestroyImmediate(parent);
        }
    }

    [Test]
    public void AttachToControlPlacesCapInsideRightEdge()
    {
        var control = new GameObject("Control", typeof(RectTransform));
        try
        {
            var rt = control.GetComponent<RectTransform>();
            var cap = ShortcutKeyCap.AttachToControl(rt, "F5", 18f, 12f);
            Assert.IsNotNull(cap);
            var capRt = cap.GetComponent<RectTransform>();
            Assert.AreEqual(1f, capRt.anchorMax.x, 0.001f);
            Assert.AreEqual(-12f, capRt.anchoredPosition.x, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(control);
        }
    }

    [Test]
    public void AttachToControlReplacesPreviousCap()
    {
        var control = new GameObject("Control", typeof(RectTransform));
        try
        {
            var rt = control.GetComponent<RectTransform>();
            ShortcutKeyCap.AttachToControl(rt, "F5", 18f);
            ShortcutKeyCap.AttachToControl(rt, "Esc", 18f);
            Assert.AreEqual(1, control.transform.childCount);
        }
        finally
        {
            Object.DestroyImmediate(control);
        }
    }
}
