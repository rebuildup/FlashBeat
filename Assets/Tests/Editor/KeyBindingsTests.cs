using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class KeyBindingsTests
{
    private static readonly KeyCode[][] CanonicalKeys = new KeyCode[][]
    {
        new KeyCode[] { KeyCode.Alpha1, KeyCode.Q, KeyCode.A, KeyCode.Z },
        new KeyCode[] { KeyCode.Alpha2, KeyCode.W, KeyCode.S, KeyCode.X },
        new KeyCode[] { KeyCode.Alpha3, KeyCode.E, KeyCode.D, KeyCode.C },
        new KeyCode[] { KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.R, KeyCode.T, KeyCode.F, KeyCode.G, KeyCode.V, KeyCode.B },
        new KeyCode[] { KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Y, KeyCode.U, KeyCode.H, KeyCode.J, KeyCode.N, KeyCode.M },
        new KeyCode[] { KeyCode.Alpha8, KeyCode.I, KeyCode.K, KeyCode.Less, KeyCode.Comma },
        new KeyCode[] { KeyCode.Alpha9, KeyCode.O, KeyCode.L, KeyCode.Greater, KeyCode.Period },
        new KeyCode[] { KeyCode.Alpha0, KeyCode.Equals, KeyCode.Caret, KeyCode.Backslash, KeyCode.P, KeyCode.At, KeyCode.LeftBracket, KeyCode.Semicolon, KeyCode.Colon, KeyCode.RightBracket, KeyCode.Slash, KeyCode.Underscore, KeyCode.Minus, KeyCode.Asterisk, KeyCode.BackQuote }
    };

    private class FakeStorage : KeyBindings.IStorage
    {
        public string Raw;

        public string LoadRaw()
        {
            return Raw;
        }

        public void SaveRaw(string json)
        {
            Raw = json;
        }
    }

    private FakeStorage storage;

    [SetUp]
    public void SetUp()
    {
        storage = new FakeStorage();
        KeyBindings.SetStorage(storage);
        KeyBindings.ResetToDefaults();
    }

    [TearDown]
    public void TearDown()
    {
        KeyBindings.ResetToDefaults();
        KeyBindings.SetStorage(null); // 既定の PlayerPrefs 保存先に戻す
    }

    [Test]
    public void TestDefaultsMatchCanonicalKeymap()
    {
        Assert.AreEqual(CanonicalKeys.Length, KeyBindings.Current.Length);
        for (int lane = 0; lane < CanonicalKeys.Length; lane++)
        {
            Assert.AreEqual(CanonicalKeys[lane], KeyBindings.Current[lane],
                $"Lane {lane} default diverges from the canonical keymap.");
        }
    }

    [Test]
    public void TestGetKeysReturnsLaneKeys()
    {
        Assert.AreEqual(KeyBindings.Current[3], KeyBindings.GetKeys(3));
        Assert.AreEqual(0, KeyBindings.GetKeys(-1).Length);
        Assert.AreEqual(0, KeyBindings.GetKeys(8).Length);
    }

    [Test]
    public void TestKeyBelongsToOnlyOneLane()
    {
        // P は既定でレーン 8 (index 7)。レーン 1 (index 0) に割り当てると移動になる。
        Assert.IsTrue(KeyBindings.ToggleKey(0, KeyCode.P));
        Assert.IsTrue(Contains(KeyBindings.GetKeys(0), KeyCode.P));
        Assert.IsFalse(Contains(KeyBindings.GetKeys(7), KeyCode.P));
    }

    [Test]
    public void TestToggleKeyAddsAndRemoves()
    {
        Assert.IsFalse(Contains(KeyBindings.GetKeys(0), KeyCode.O));
        Assert.IsTrue(KeyBindings.ToggleKey(0, KeyCode.O));
        Assert.IsTrue(Contains(KeyBindings.GetKeys(0), KeyCode.O));
        Assert.IsTrue(KeyBindings.ToggleKey(0, KeyCode.O));
        Assert.IsFalse(Contains(KeyBindings.GetKeys(0), KeyCode.O));
    }

    [Test]
    public void TestToggleCannotEmptyLane()
    {
        // レーン 1 (index 0) は 4 キー。3 個外して残り 1 個の状態でさらに外すと拒否。
        Assert.IsTrue(KeyBindings.ToggleKey(0, KeyCode.Alpha1));
        Assert.IsTrue(KeyBindings.ToggleKey(0, KeyCode.Q));
        Assert.IsTrue(KeyBindings.ToggleKey(0, KeyCode.A));
        Assert.AreEqual(1, KeyBindings.GetKeys(0).Length);
        Assert.IsFalse(KeyBindings.ToggleKey(0, KeyCode.Z));
        Assert.IsTrue(Contains(KeyBindings.GetKeys(0), KeyCode.Z));
    }

    [Test]
    public void TestMoveRefusedWhenSourceLaneWouldEmpty()
    {
        // レーン 2 (index 1) を Z だけにしてから、Z をレーン 1 へ戻す移動は拒否される。
        Assert.IsTrue(KeyBindings.SetLane(1, new[] { KeyCode.Z })); // レーン 2 = Z のみ (Z はレーン 1 から移動、レーン 1 は 3 キー)
        Assert.IsTrue(Contains(KeyBindings.GetKeys(1), KeyCode.Z));
        Assert.IsFalse(KeyBindings.ToggleKey(0, KeyCode.Z)); // レーン 2 が空になるため拒否
        Assert.IsTrue(Contains(KeyBindings.GetKeys(1), KeyCode.Z));
    }

    [Test]
    public void TestJsonRoundTrip()
    {
        Assert.IsTrue(KeyBindings.ToggleKey(0, KeyCode.P));
        Assert.IsTrue(KeyBindings.ToggleKey(2, KeyCode.Minus));
        KeyBindings.Save();
        Assert.IsFalse(string.IsNullOrEmpty(storage.Raw));

        KeyBindings.ResetToDefaults();
        Assert.AreEqual(CanonicalKeys[0], KeyBindings.GetKeys(0));

        KeyBindings.Load();
        Assert.IsTrue(Contains(KeyBindings.GetKeys(0), KeyCode.P));
        Assert.IsFalse(Contains(KeyBindings.GetKeys(7), KeyCode.P));
        Assert.IsTrue(Contains(KeyBindings.GetKeys(2), KeyCode.Minus));
    }

    [Test]
    public void TestGarbageJsonFallsBackToDefaults()
    {
        storage.Raw = "not json at all {{{";
        KeyBindings.Load();
        for (int lane = 0; lane < CanonicalKeys.Length; lane++)
        {
            Assert.AreEqual(CanonicalKeys[lane], KeyBindings.GetKeys(lane));
        }
    }

    [Test]
    public void TestWrongLaneCountFallsBackToDefaults()
    {
        storage.Raw = "{\"lanes\":[\"Alpha1\",\"Alpha2\",\"Alpha3\",\"Alpha4\",\"Alpha5\",\"Alpha6\",\"Alpha7\"]}";
        KeyBindings.Load();
        for (int lane = 0; lane < CanonicalKeys.Length; lane++)
        {
            Assert.AreEqual(CanonicalKeys[lane], KeyBindings.GetKeys(lane));
        }
    }

    [Test]
    public void TestUnknownKeyNamesFallBackPerLane()
    {
        // レーン 1: 不明キーのみ → 既定値。レーン 2: 不明キーを含むが有効キーが残る。
        storage.Raw = "{\"lanes\":[\"Bogus1,Bogus2\",\"Alpha2,W,Bogus3,S,X\",\"Alpha3,E,D,C\",\"Alpha4,R,T,F,G,V,B\",\"Alpha5,Y,U,H,J,N,M\",\"Alpha6,I,K,Comma\",\"Alpha7,O,L,Period\",\"Alpha0,P,Backslash\"]}";
        KeyBindings.Load();
        Assert.AreEqual(CanonicalKeys[0], KeyBindings.GetKeys(0)); // 全部不明 → 既定値
        Assert.AreEqual(new KeyCode[] { KeyCode.Alpha2, KeyCode.W, KeyCode.S, KeyCode.X }, KeyBindings.GetKeys(1)); // 不明キーは無視
    }

    [Test]
    public void TestEmptyLaneInJsonFallsBackToDefaults()
    {
        storage.Raw = "{\"lanes\":[\"\",\"Alpha2,W,S,X\",\"Alpha3,E,D,C\",\"Alpha4,R,T,F,G,V,B\",\"Alpha5,Y,U,H,J,N,M\",\"Alpha6,I,K,Comma\",\"Alpha7,O,L,Period\",\"Alpha0,P,Backslash\"]}";
        KeyBindings.Load();
        Assert.AreEqual(CanonicalKeys[0], KeyBindings.GetKeys(0));
    }

    [Test]
    public void TestSaveRefusesEmptyLane()
    {
        Assert.IsTrue(KeyBindings.SetLane(0, new KeyCode[0])); // 編集中は空にできる
        Assert.AreEqual(0, KeyBindings.GetKeys(0).Length);
        Assert.IsFalse(KeyBindings.Save());
        Assert.IsTrue(string.IsNullOrEmpty(storage.Raw));
    }

    [Test]
    public void TestReservedKeysAreRejected()
    {
        KeyCode[] reserved =
        {
            KeyCode.Escape, KeyCode.Tab, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Space,
            KeyCode.Backspace, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.F1, KeyCode.F2, KeyCode.F12, KeyCode.Mouse0, KeyCode.Mouse2, KeyCode.None
        };
        foreach (var key in reserved)
        {
            Assert.IsTrue(KeyBindings.IsReserved(key), key + " must be reserved.");
            Assert.IsFalse(KeyBindings.ToggleKey(0, key), key + " must not be bindable.");
        }
    }

    [Test]
    public void TestSetLaneMovesKeysFromOtherLanes()
    {
        Assert.IsTrue(KeyBindings.SetLane(0, new KeyCode[] { KeyCode.Alpha1, KeyCode.Q, KeyCode.A, KeyCode.Z, KeyCode.O }));
        Assert.IsTrue(Contains(KeyBindings.GetKeys(0), KeyCode.O));
        Assert.IsFalse(Contains(KeyBindings.GetKeys(6), KeyCode.O));
        Assert.IsTrue(KeyBindings.GetKeys(6).Length > 0);
    }

    [Test]
    public void TestSetLaneRefusedWhenSourceLaneWouldEmpty()
    {
        // レーン 2 (index 1) の 4 キー全部をレーン 1 へ → レーン 2 が空になるため拒否。
        Assert.IsFalse(KeyBindings.SetLane(0, new KeyCode[] { KeyCode.Alpha1, KeyCode.Q, KeyCode.A, KeyCode.Z, KeyCode.Alpha2, KeyCode.W, KeyCode.S, KeyCode.X }));
        Assert.AreEqual(CanonicalKeys[0], KeyBindings.GetKeys(0));
    }

    [Test]
    public void TestChangedEventFires()
    {
        int count = 0;
        System.Action handler = () => count++;
        KeyBindings.Changed += handler;
        try
        {
            KeyBindings.ToggleKey(0, KeyCode.P);
            KeyBindings.ResetToDefaults();
            Assert.AreEqual(2, count);
        }
        finally
        {
            KeyBindings.Changed -= handler;
        }
    }

    private static bool Contains(KeyCode[] keys, KeyCode key)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] == key) return true;
        }
        return false;
    }
}
