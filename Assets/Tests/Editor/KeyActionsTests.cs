using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class KeyActionsTests
{
    private class FakeStorage : KeyActions.IStorage
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

    private class FakeKeyBindingsStorage : KeyBindings.IStorage
    {
        public string LoadRaw()
        {
            return null;
        }

        public void SaveRaw(string json)
        {
        }
    }

    private FakeStorage storage;

    [SetUp]
    public void SetUp()
    {
        storage = new FakeStorage();
        KeyActions.SetStorage(storage);
        KeyActions.ResetToDefaults();
        KeyBindings.SetStorage(new FakeKeyBindingsStorage());
        KeyBindings.ResetToDefaults();
    }

    [TearDown]
    public void TearDown()
    {
        KeyActions.ResetToDefaults();
        KeyActions.SetStorage(null); // 既定の PlayerPrefs 保存先に戻す
        KeyBindings.ResetToDefaults();
        KeyBindings.SetStorage(null);
    }

    [Test]
    public void TestDefaultsMatchTodaysKeys()
    {
        Assert.AreEqual(new KeyCode[] { KeyCode.Space }, KeyActions.Get(KeyActions.KeyAction.Confirm));
        Assert.AreEqual(new KeyCode[] { KeyCode.Escape }, KeyActions.Get(KeyActions.KeyAction.Back));
        Assert.AreEqual(new KeyCode[] { KeyCode.R }, KeyActions.Get(KeyActions.KeyAction.Retry));
        Assert.AreEqual(new KeyCode[] { KeyCode.O }, KeyActions.Get(KeyActions.KeyAction.Option));
        Assert.AreEqual(new KeyCode[] { KeyCode.E }, KeyActions.Get(KeyActions.KeyAction.Exit));
        Assert.AreEqual(new KeyCode[] { KeyCode.R }, KeyActions.Get(KeyActions.KeyAction.MenuRetry));
        Assert.AreEqual(new KeyCode[] { KeyCode.E }, KeyActions.Get(KeyActions.KeyAction.MenuEnd));
        Assert.AreEqual(new KeyCode[] { KeyCode.Backspace }, KeyActions.Get(KeyActions.KeyAction.MenuRetire));
        Assert.AreEqual(new KeyCode[] { KeyCode.T }, KeyActions.Get(KeyActions.KeyAction.MenuSwitch));
        // タイトルのスタートだけは利便性のため Enter 系を追加 (Space は従来どおり)
        Assert.AreEqual(new KeyCode[] { KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter }, KeyActions.Get(KeyActions.KeyAction.Start));
        Assert.AreEqual(new KeyCode[] { KeyCode.Space }, KeyActions.Get(KeyActions.KeyAction.Play));
        Assert.AreEqual(new KeyCode[] { KeyCode.LeftArrow }, KeyActions.Get(KeyActions.KeyAction.NavPrev));
        Assert.AreEqual(new KeyCode[] { KeyCode.RightArrow }, KeyActions.Get(KeyActions.KeyAction.NavNext));
    }

    [Test]
    public void TestAllCoversEveryAction()
    {
        var defaults = new HashSet<KeyActions.KeyAction>(KeyActions.All);
        Assert.AreEqual(13, KeyActions.All.Length);
        Assert.IsTrue(defaults.Contains(KeyActions.KeyAction.Confirm));
        Assert.IsTrue(defaults.Contains(KeyActions.KeyAction.NavNext));
    }

    [Test]
    public void TestSetChangesBinding()
    {
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Confirm, new KeyCode[] { KeyCode.Keypad7 }));
        Assert.AreEqual(new KeyCode[] { KeyCode.Keypad7 }, KeyActions.Get(KeyActions.KeyAction.Confirm));
    }

    [Test]
    public void TestSaveLoadRoundTrip()
    {
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Confirm, new KeyCode[] { KeyCode.Keypad7 }));
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Retry, new KeyCode[] { KeyCode.Keypad8, KeyCode.Keypad9 }));
        Assert.IsTrue(KeyActions.Save());
        Assert.IsFalse(string.IsNullOrEmpty(storage.Raw));
        string saved = storage.Raw;

        KeyActions.ResetToDefaults(); // 既定へ戻すと保存先にも既定値が反映される
        Assert.AreEqual(new KeyCode[] { KeyCode.Space }, KeyActions.Get(KeyActions.KeyAction.Confirm));

        storage.Raw = saved; // 保存済みデータを戻して読み直す
        KeyActions.Load();
        Assert.AreEqual(new KeyCode[] { KeyCode.Keypad7 }, KeyActions.Get(KeyActions.KeyAction.Confirm));
        Assert.AreEqual(new KeyCode[] { KeyCode.Keypad8, KeyCode.Keypad9 }, KeyActions.Get(KeyActions.KeyAction.Retry));
    }

    [Test]
    public void TestGarbageJsonFallsBackToDefaults()
    {
        storage.Raw = "not json at all {{{";
        KeyActions.Load();
        Assert.AreEqual(new KeyCode[] { KeyCode.Space }, KeyActions.Get(KeyActions.KeyAction.Confirm));
        Assert.AreEqual(new KeyCode[] { KeyCode.R }, KeyActions.Get(KeyActions.KeyAction.MenuRetry));
    }

    [Test]
    public void TestUnknownActionAndKeysAreIgnored()
    {
        storage.Raw = "{\"actions\":[{\"action\":\"Bogus\",\"keys\":\"Keypad7\"},{\"action\":\"Confirm\",\"keys\":\"Bogus1,Bogus2\"},{\"action\":\"Retry\",\"keys\":\"Bogus,Keypad7\"}]}";
        KeyActions.Load();
        Assert.AreEqual(new KeyCode[] { KeyCode.Space }, KeyActions.Get(KeyActions.KeyAction.Confirm)); // 不明キーのみ → 既定値
        Assert.AreEqual(new KeyCode[] { KeyCode.Keypad7 }, KeyActions.Get(KeyActions.KeyAction.Retry)); // 不明キーは無視
    }

    [Test]
    public void TestLaneBoundKeyIsRejected()
    {
        // 既定で R はレーン 4 (index 3) に割り当て済み → 決定には使えない
        Assert.IsFalse(KeyActions.Set(KeyActions.KeyAction.Confirm, new KeyCode[] { KeyCode.R }));
        Assert.IsFalse(string.IsNullOrEmpty(KeyActions.LastError));
        Assert.AreEqual(new KeyCode[] { KeyCode.Space }, KeyActions.Get(KeyActions.KeyAction.Confirm));

        // レーンから外せば使えるようになる
        Assert.IsTrue(KeyBindings.ToggleKey(3, KeyCode.R));
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Confirm, new KeyCode[] { KeyCode.R }));
    }

    [Test]
    public void TestInGameActionRejectsNonReservedKeys()
    {
        // 演奏中に使う操作 (再生開始など) は予約キーのみ。Keypad7 は予約外 → 拒否。
        Assert.IsTrue(KeyBindings.IsReserved(KeyCode.Space));
        Assert.IsFalse(KeyBindings.IsReserved(KeyCode.Keypad7));

        Assert.IsFalse(KeyActions.Set(KeyActions.KeyAction.Play, new KeyCode[] { KeyCode.Keypad7 }));
        Assert.IsFalse(string.IsNullOrEmpty(KeyActions.LastError));
        Assert.AreEqual(new KeyCode[] { KeyCode.Space }, KeyActions.Get(KeyActions.KeyAction.Play));

        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Play, new KeyCode[] { KeyCode.Return }));
        Assert.AreEqual(new KeyCode[] { KeyCode.Return }, KeyActions.Get(KeyActions.KeyAction.Play));
    }

    [Test]
    public void TestMenuActionAcceptsFreeNonReservedKey()
    {
        // Keypad7 はレーンにも予約にも属さない → メニュー操作には使える
        Assert.IsFalse(KeyBindings.IsReserved(KeyCode.Keypad7));
        Assert.IsFalse(KeyActions.IsLaneBound(KeyCode.Keypad7));
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Option, new KeyCode[] { KeyCode.Keypad7 }));
        Assert.AreEqual(new KeyCode[] { KeyCode.Keypad7 }, KeyActions.Get(KeyActions.KeyAction.Option));
    }

    [Test]
    public void TestSetAllowsEmptyForEditingButSaveRefuses()
    {
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Confirm, new KeyCode[0]));
        Assert.AreEqual(0, KeyActions.Get(KeyActions.KeyAction.Confirm).Length);
        string before = storage.Raw; // SetUp の ResetToDefaults で既定値が入っている
        Assert.IsFalse(KeyActions.Save());
        Assert.AreEqual(before, storage.Raw); // 拒否されたら保存先は変わらない
    }

    [Test]
    public void TestEmptyActionInJsonFallsBackToDefault()
    {
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Confirm, new KeyCode[0]));
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Retry, new KeyCode[] { KeyCode.Keypad7 }));
        Assert.IsFalse(KeyActions.Save()); // 空操作があるため保存されない
        storage.Raw = "{\"actions\":[{\"action\":\"Confirm\",\"keys\":\"\"},{\"action\":\"Retry\",\"keys\":\"Keypad7\"}]}";
        KeyActions.Load();
        Assert.AreEqual(new KeyCode[] { KeyCode.Space }, KeyActions.Get(KeyActions.KeyAction.Confirm));
        Assert.AreEqual(new KeyCode[] { KeyCode.Keypad7 }, KeyActions.Get(KeyActions.KeyAction.Retry));
    }

    [Test]
    public void TestResetToDefaultsRestoresAll()
    {
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Exit, new KeyCode[] { KeyCode.Keypad7 }));
        KeyActions.ResetToDefaults();
        Assert.AreEqual(new KeyCode[] { KeyCode.E }, KeyActions.Get(KeyActions.KeyAction.Exit));
        Assert.AreEqual(string.Empty, KeyActions.LastError);
    }

    [Test]
    public void TestChangedEventFires()
    {
        int count = 0;
        System.Action handler = () => count++;
        KeyActions.Changed += handler;
        try
        {
            KeyActions.Set(KeyActions.KeyAction.Confirm, new KeyCode[] { KeyCode.Keypad7 });
            KeyActions.ResetToDefaults();
            Assert.AreEqual(2, count);
        }
        finally
        {
            KeyActions.Changed -= handler;
        }
    }

    [Test]
    public void TestSetDeduplicatesKeys()
    {
        Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Retry, new KeyCode[] { KeyCode.Keypad7, KeyCode.Keypad7 }));
        Assert.AreEqual(new KeyCode[] { KeyCode.Keypad7 }, KeyActions.Get(KeyActions.KeyAction.Retry));
    }
}
