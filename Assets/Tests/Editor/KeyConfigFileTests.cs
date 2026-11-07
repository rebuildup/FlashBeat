using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// KeyConfigFile (FlashBeat-keyconfig.json) のテスト。
/// 実ファイルや PlayerPrefs を触らないよう、パスとレガシーフックをテスト用に差し替える。
/// </summary>
public class KeyConfigFileTests
{
    private class MemoryStorage : KeyBindings.IStorage, KeyActions.IStorage
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

    [Serializable]
    private class LanesData
    {
        public string[] lanes;
    }

    [Serializable]
    private class ActionEntry
    {
        public string action;
        public string keys;
    }

    [Serializable]
    private class ActionsData
    {
        public ActionEntry[] actions;
    }

    private string path;

    [SetUp]
    public void SetUp()
    {
        path = Path.Combine(Path.GetTempPath(), "FlashBeatKeyConfigTests-" + Guid.NewGuid().ToString("N") + ".json");
        KeyConfigFile.SetFilePathForTests(path);
        KeyConfigFile.ResetLegacyHooksForTests();
    }

    [TearDown]
    public void TearDown()
    {
        KeyConfigFile.ResetLegacyHooksForTests();
        KeyConfigFile.SetFilePathForTests(null);
        TryDelete(path);
        TryDelete(path + ".tmp");
        KeyBindings.SetStorage(new MemoryStorage());
        KeyActions.SetStorage(new MemoryStorage());
    }

    private static string LanesJson(params string[] lanes)
    {
        var data = new LanesData();
        data.lanes = lanes;
        return JsonUtility.ToJson(data);
    }

    private static string ActionsJson(params ActionEntry[] entries)
    {
        var data = new ActionsData();
        data.actions = entries;
        return JsonUtility.ToJson(data);
    }

    private static string[] EightLanes(string lane0)
    {
        return new string[]
        {
            lane0,
            "Alpha2,W,S,X",
            "Alpha3,E,D,C",
            "Alpha4,R,T,F,G,V,B",
            "Alpha5,Y,U,H,J,N,M",
            "Alpha6,I,K,Comma",
            "Alpha7,O,L,Period",
            "Alpha0,P,Backslash"
        };
    }

    private static void TryDelete(string file)
    {
        try { File.Delete(file); } catch (Exception) { }
    }

    [Test]
    public void TestMissingFileReturnsNull()
    {
        Assert.IsNull(KeyConfigFile.LoadLanesJson());
        Assert.IsNull(KeyConfigFile.LoadActionsJson());
    }

    [Test]
    public void TestSaveWritesFileAndRoundTrips()
    {
        string lanes = LanesJson(EightLanes("Alpha9,Q,A,Z"));
        Assert.IsTrue(KeyConfigFile.SaveLanesJson(lanes));
        Assert.IsTrue(File.Exists(path));
        Assert.AreEqual(lanes, KeyConfigFile.LoadLanesJson());
        Assert.IsNull(KeyConfigFile.LoadActionsJson()); // まだ操作部分は無い
    }

    [Test]
    public void TestLanesAndActionsShareOneFile()
    {
        string lanes = LanesJson(EightLanes("Alpha1,Q,A,Z"));
        string actions = ActionsJson(new ActionEntry { action = "Confirm", keys = "Keypad7" });
        Assert.IsTrue(KeyConfigFile.SaveLanesJson(lanes));
        Assert.IsTrue(KeyConfigFile.SaveActionsJson(actions)); // レーン部分を壊さない
        Assert.AreEqual(lanes, KeyConfigFile.LoadLanesJson());
        Assert.AreEqual(actions, KeyConfigFile.LoadActionsJson());

        string raw = File.ReadAllText(path);
        StringAssert.Contains("\"version\":1", raw);
        StringAssert.Contains("Alpha1,Q,A,Z", raw);
        StringAssert.Contains("Confirm", raw);
    }

    [Test]
    public void TestCorruptFileReturnsNullWithoutThrowing()
    {
        File.WriteAllText(path, "not json at all {{{");
        Assert.IsNull(KeyConfigFile.LoadLanesJson());
        Assert.IsNull(KeyConfigFile.LoadActionsJson());
    }

    [Test]
    public void TestUnknownVersionIsIgnored()
    {
        File.WriteAllText(path, "{\"version\":99,\"lanes\":[\"Alpha1\"]}");
        Assert.IsNull(KeyConfigFile.LoadLanesJson());
    }

    [Test]
    public void TestFailedWriteReturnsFalseWithoutThrowing()
    {
        Directory.CreateDirectory(path); // 同名のディレクトリがあり書き込めない状況を作る
        Assert.IsFalse(KeyConfigFile.SaveLanesJson(LanesJson(EightLanes("Alpha1,Q,A,Z"))));
        Assert.IsFalse(KeyConfigFile.SaveActionsJson(ActionsJson(new ActionEntry { action = "Confirm", keys = "Space" })));
    }

    [Test]
    public void TestLegacyLanesAreMigratedIntoTheFile()
    {
        string legacy = LanesJson(EightLanes("Alpha9,Q,A,Z"));
        KeyConfigFile.LegacyLanesLoader = () => legacy;
        KeyConfigFile.LegacyActionsLoader = () => null;

        var storage = new KeyConfigFileStorage();
        Assert.AreEqual(legacy, ((KeyBindings.IStorage)storage).LoadRaw());
        Assert.IsTrue(File.Exists(path)); // レガシー値がファイルへ移行されている
        Assert.AreEqual(legacy, KeyConfigFile.LoadLanesJson());
    }

    [Test]
    public void TestLegacyActionsAreMigratedIntoTheFile()
    {
        string legacy = ActionsJson(new ActionEntry { action = "Confirm", keys = "Keypad7" });
        KeyConfigFile.LegacyActionsLoader = () => legacy;
        KeyConfigFile.LegacyLanesLoader = () => null;

        var storage = new KeyConfigFileStorage();
        Assert.AreEqual(legacy, ((KeyActions.IStorage)storage).LoadRaw());
        Assert.IsTrue(File.Exists(path));
        Assert.AreEqual(legacy, KeyConfigFile.LoadActionsJson());
    }

    [Test]
    public void TestFileWinsOverLegacy()
    {
        string inFile = LanesJson(EightLanes("Alpha9,Q,A,Z"));
        Assert.IsTrue(KeyConfigFile.SaveLanesJson(inFile));

        KeyConfigFile.LegacyLanesLoader = () => LanesJson(EightLanes("Alpha8,Q,A,Z"));
        var storage = new KeyConfigFileStorage();
        Assert.AreEqual(inFile, ((KeyBindings.IStorage)storage).LoadRaw()); // ファイル優先
        Assert.AreEqual(inFile, KeyConfigFile.LoadLanesJson()); // レガシーで上書きされない
    }

    [Test]
    public void TestSaveMirrorsToLegacyStorage()
    {
        string mirroredLanes = null;
        string mirroredActions = null;
        KeyConfigFile.LegacyLanesMirror = json => mirroredLanes = json;
        KeyConfigFile.LegacyActionsMirror = json => mirroredActions = json;

        string lanes = LanesJson(EightLanes("Alpha1,Q,A,Z"));
        string actions = ActionsJson(new ActionEntry { action = "Confirm", keys = "Space" });
        var storage = new KeyConfigFileStorage();
        ((KeyBindings.IStorage)storage).SaveRaw(lanes);
        ((KeyActions.IStorage)storage).SaveRaw(actions);

        Assert.AreEqual(lanes, mirroredLanes);
        Assert.AreEqual(actions, mirroredActions);
        Assert.AreEqual(lanes, KeyConfigFile.LoadLanesJson());
        Assert.AreEqual(actions, KeyConfigFile.LoadActionsJson());
    }

    [Test]
    public void TestResetToDefaultsPersistsFileAndMirror()
    {
        string mirroredLanes = null;
        string mirroredActions = null;
        KeyConfigFile.LegacyLanesMirror = json => mirroredLanes = json;
        KeyConfigFile.LegacyActionsMirror = json => mirroredActions = json;

        KeyBindings.SetStorage(new KeyConfigFileStorage());
        KeyActions.SetStorage(new KeyConfigFileStorage());
        try
        {
            Assert.IsTrue(KeyBindings.ToggleKey(0, KeyCode.P));
            KeyBindings.ResetToDefaults(); // 既定へ戻すとファイルとミラーにも反映される
            Assert.IsTrue(File.Exists(path));
            Assert.IsNotNull(KeyConfigFile.LoadLanesJson());
            Assert.IsNotNull(mirroredLanes);

            KeyActions.ResetToDefaults();
            Assert.IsNotNull(KeyConfigFile.LoadActionsJson());
            Assert.IsNotNull(mirroredActions);
        }
        finally
        {
            KeyBindings.SetStorage(new MemoryStorage());
            KeyActions.SetStorage(new MemoryStorage());
        }
    }

    [Test]
    public void TestResetWithIsolatedStorageDoesNotTouchRealFile()
    {
        // オペレーターの設定ファイルに見立てた番兵ファイル。分離ストレージを入れた状態で
        // ResetToDefaults しても内容が変わらないこと (セルフテストが設定を消さないこと) を保証する。
        string sentinel = "{\"version\":1,\"lanes\":[\"SENTINEL\"],\"actions\":[]}";
        File.WriteAllText(path, sentinel);

        var isolated = new MemoryStorage();
        KeyBindings.SetStorage(isolated);
        KeyActions.SetStorage(isolated);
        try
        {
            KeyBindings.ResetToDefaults();
            KeyActions.ResetToDefaults();
            Assert.AreEqual(sentinel, File.ReadAllText(path)); // 実ファイルは無傷
            Assert.IsFalse(string.IsNullOrEmpty(isolated.Raw)); // リセットは分離ストレージへ書かれている
        }
        finally
        {
            KeyBindings.SetStorage(new MemoryStorage());
            KeyActions.SetStorage(new MemoryStorage());
        }
    }
}
