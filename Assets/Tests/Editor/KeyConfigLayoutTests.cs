using NUnit.Framework;
using UnityEngine;

/// <summary>
/// KeyConfigUI の既定/カスタム判定 (IsDefaultLayout) の純粋ロジックのテスト (Issue #128)。
/// あわせて KeyConfigUI.DefaultLayout が KeyBindings の既定値と一致することを検出する
/// (KeyBindings.DefaultKeys は private のため、空の保存先から読んだ既定値と突き合わせる)。
/// </summary>
public class KeyConfigLayoutTests
{
    private static KeyCode[][] Table(KeyCode[][] lanes)
    {
        var copy = new KeyCode[lanes.Length][];
        for (int lane = 0; lane < lanes.Length; lane++)
        {
            copy[lane] = (KeyCode[])lanes[lane].Clone();
        }
        return copy;
    }

    private static readonly KeyCode[][] Defaults = KeyConfigUI.DefaultLayout;

    [Test]
    public void SameAsDefault_ReturnsTrue()
    {
        Assert.IsTrue(KeyConfigUI.IsDefaultLayout(Table(Defaults), Defaults));
        Assert.IsTrue(KeyConfigUI.IsDefaultLayout(Defaults, Defaults));
    }

    [Test]
    public void KeyOrderWithinLane_IsStillDefault()
    {
        // トグルで解除/再割り当てするとレーン内の順序が変わるが、意味的には既定のまま扱う
        var reordered = Table(Defaults);
        reordered[0] = new KeyCode[] { KeyCode.Q, KeyCode.A, KeyCode.Z, KeyCode.Alpha1 };
        Assert.IsTrue(KeyConfigUI.IsDefaultLayout(reordered, Defaults));
    }

    [Test]
    public void MovedKeyToAnotherLane_IsCustom()
    {
        var moved = Table(Defaults);
        moved[0] = new KeyCode[] { KeyCode.Alpha1, KeyCode.Q, KeyCode.A }; // Z を外す
        moved[1] = new KeyCode[] { KeyCode.Alpha2, KeyCode.W, KeyCode.S, KeyCode.X, KeyCode.Z }; // レーン2 へ
        Assert.IsFalse(KeyConfigUI.IsDefaultLayout(moved, Defaults));
    }

    [Test]
    public void ExtraKey_IsCustom()
    {
        var extra = Table(Defaults);
        extra[2] = new KeyCode[] { KeyCode.Alpha3, KeyCode.E, KeyCode.D, KeyCode.C, KeyCode.O };
        Assert.IsFalse(KeyConfigUI.IsDefaultLayout(extra, Defaults));
    }

    [Test]
    public void RemovedKey_IsCustom()
    {
        var removed = Table(Defaults);
        removed[4] = new KeyCode[] { KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Y, KeyCode.U, KeyCode.H, KeyCode.J, KeyCode.N };
        Assert.IsFalse(KeyConfigUI.IsDefaultLayout(removed, Defaults));
    }

    [Test]
    public void SwappedLanes_IsCustom()
    {
        var swapped = Table(Defaults);
        var lane0 = swapped[0];
        swapped[0] = swapped[1];
        swapped[1] = lane0;
        Assert.IsFalse(KeyConfigUI.IsDefaultLayout(swapped, Defaults));
    }

    [Test]
    public void NullOrLengthMismatch_ReturnsFalse()
    {
        Assert.IsFalse(KeyConfigUI.IsDefaultLayout(null, Defaults));
        Assert.IsFalse(KeyConfigUI.IsDefaultLayout(Defaults, null));
        Assert.IsFalse(KeyConfigUI.IsDefaultLayout(new KeyCode[0], Defaults));
    }

    [Test]
    public void KeyBindingsDefault_MatchesKeyConfigUIDefaultLayout()
    {
        // KeyConfigUI.DefaultLayout は KeyBindings.DefaultKeys の複製。ずれがあればここで検出する。
        var storage = new EmptyStorage();
        KeyBindings.SetStorage(storage);
        KeyBindings.ResetToDefaults();

        KeyCode[][] loaded = KeyBindings.Current;
        Assert.AreEqual(Defaults.Length, loaded.Length);
        for (int lane = 0; lane < Defaults.Length; lane++)
        {
            Assert.AreEqual(Defaults[lane], loaded[lane], $"Lane {lane} diverges from KeyBindings defaults.");
        }
    }

    [TearDown]
    public void TearDown()
    {
        KeyBindings.ResetToDefaults();
        KeyBindings.SetStorage(null); // 既定の PlayerPrefs 保存先に戻す
    }

    private class EmptyStorage : KeyBindings.IStorage
    {
        public string LoadRaw()
        {
            return null;
        }

        public void SaveRaw(string json)
        {
        }
    }
}
