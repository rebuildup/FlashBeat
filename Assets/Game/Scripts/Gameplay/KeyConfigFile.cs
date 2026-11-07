using System;
using System.IO;
using UnityEngine;

/// <summary>
/// キー設定 (レーン + メニュー操作) を 1 つの JSON ファイルへ保存する。
/// 保存先は Application.persistentDataPath 配下 (インストール フォルダの外。
/// アップデータがインストール フォルダを丸ごと差し替えても設定は消えない)。
/// ファイル名: FlashBeat-keyconfig.json
/// 形式: {"version":1,"lanes":["Alpha1,Q,A,Z",...],"actions":[{"action":"Confirm","keys":"Space"},...]}
/// lanes / actions は KeyBindings.ToJson() / KeyActions.ToJson() と同じ形式を格納する
/// (シリアライズ形式を二重管理しないため)。
/// 書き込みは一時ファイル経由で入れ替える (クラッシュ時も既存ファイルを壊さない)。
/// </summary>
public static class KeyConfigFile
{
    public const int Version = 1;
    public const string FileName = "FlashBeat-keyconfig.json";

    // レガシー (0.1.9 まで) の PlayerPrefs キー。移行元かつミラー先。
    private const string LegacyLanesPrefsKey = "FlashBeat.KeyBindings";
    private const string LegacyActionsPrefsKey = "FlashBeat.KeyActions";

    [Serializable]
    private class Document
    {
        public int version;
        public string[] lanes;
        public ActionEntry[] actions;
    }

    [Serializable]
    private class ActionEntry
    {
        public string action;
        public string keys;
    }

    // KeyBindings / KeyActions の既存 JSON 形式を取り出すための DTO (フィールド名を一致させる)。
    [Serializable]
    private class LanesData
    {
        public string[] lanes;
    }

    [Serializable]
    private class ActionsData
    {
        public ActionEntry[] actions;
    }

    private static string pathOverride;

    /// <summary>設定ファイルのフルパス (オペレーター向け表示に使う)。</summary>
    public static string FilePath
    {
        get
        {
            if (!string.IsNullOrEmpty(pathOverride)) { return pathOverride; }
            return Path.Combine(Application.persistentDataPath, FileName);
        }
    }

    /// <summary>テスト用: 保存先パスを差し替える。null で既定 (persistentDataPath) に戻す。</summary>
    public static void SetFilePathForTests(string path)
    {
        pathOverride = path;
    }

    // ---- レガシー PlayerPrefs との互換 ----
    // 読み込み: ファイルが無ければレガシー値を使い、見つかればファイルへ移行する (強制リセットしない)。
    // 保存: ファイルに加えてレガシー側にもミラーする (旧バージョンへダウングレードしても設定が残る)。
    // デリゲートはテストから差し替え可能 (EditMode テストが PlayerPrefs を汚さないように)。
    public static Func<string> LegacyLanesLoader = LoadLegacyLanesFromPrefs;
    public static Func<string> LegacyActionsLoader = LoadLegacyActionsFromPrefs;
    public static Action<string> LegacyLanesMirror = MirrorLanesToPrefs;
    public static Action<string> LegacyActionsMirror = MirrorActionsToPrefs;

    /// <summary>テスト用: レガシー読み書きフックを既定 (PlayerPrefs) に戻す。</summary>
    public static void ResetLegacyHooksForTests()
    {
        LegacyLanesLoader = LoadLegacyLanesFromPrefs;
        LegacyActionsLoader = LoadLegacyActionsFromPrefs;
        LegacyLanesMirror = MirrorLanesToPrefs;
        LegacyActionsMirror = MirrorActionsToPrefs;
    }

    private static string LoadLegacyLanesFromPrefs()
    {
        return PlayerPrefs.HasKey(LegacyLanesPrefsKey) ? PlayerPrefs.GetString(LegacyLanesPrefsKey) : null;
    }

    private static string LoadLegacyActionsFromPrefs()
    {
        return PlayerPrefs.HasKey(LegacyActionsPrefsKey) ? PlayerPrefs.GetString(LegacyActionsPrefsKey) : null;
    }

    private static void MirrorLanesToPrefs(string json)
    {
        PlayerPrefs.SetString(LegacyLanesPrefsKey, json);
        PlayerPrefs.Save();
    }

    private static void MirrorActionsToPrefs(string json)
    {
        PlayerPrefs.SetString(LegacyActionsPrefsKey, json);
        PlayerPrefs.Save();
    }

    /// <summary>ファイルからレーン部分を読む。ファイル (または部分) が無ければ null。</summary>
    public static string LoadLanesJson()
    {
        Document doc = ReadDocument();
        if (doc == null || doc.lanes == null || doc.lanes.Length == 0) { return null; }
        var wrapper = new LanesData();
        wrapper.lanes = doc.lanes;
        return JsonUtility.ToJson(wrapper);
    }

    /// <summary>ファイルからメニュー操作部分を読む。ファイル (または部分) が無ければ null。</summary>
    public static string LoadActionsJson()
    {
        Document doc = ReadDocument();
        if (doc == null || doc.actions == null || doc.actions.Length == 0) { return null; }
        var wrapper = new ActionsData();
        wrapper.actions = doc.actions;
        return JsonUtility.ToJson(wrapper);
    }

    /// <summary>レーン部分を保存する (操作部分は既存値を維持)。失敗時 false (例外は出さない)。</summary>
    public static bool SaveLanesJson(string lanesJson)
    {
        string[] lanes = ParseLanes(lanesJson);
        if (lanes == null) { return false; }
        return WriteDocument(lanes, ReadExistingActions());
    }

    /// <summary>メニュー操作部分を保存する (レーン部分は既存値を維持)。失敗時 false (例外は出さない)。</summary>
    public static bool SaveActionsJson(string actionsJson)
    {
        ActionEntry[] actions = ParseActions(actionsJson);
        if (actions == null) { return false; }
        return WriteDocument(ReadExistingLanes(), actions);
    }

    /// <summary>ファイルが無い場合にレガシー (PlayerPrefs) から読み、見つかればファイルへ移行する。</summary>
    public static string LoadOrMigrateLanes()
    {
        string fromFile = LoadLanesJson();
        if (!string.IsNullOrEmpty(fromFile)) { return fromFile; }

        Func<string> loader = LegacyLanesLoader;
        string legacy = (loader != null) ? loader() : null;
        if (!string.IsNullOrEmpty(legacy))
        {
            // アップデート後も設定が消えたように見えないよう、レガシー設定をファイルへ移行する。
            SaveLanesJson(legacy);
        }
        return legacy;
    }

    /// <summary>LoadOrMigrateLanes のメニュー操作版。</summary>
    public static string LoadOrMigrateActions()
    {
        string fromFile = LoadActionsJson();
        if (!string.IsNullOrEmpty(fromFile)) { return fromFile; }

        Func<string> loader = LegacyActionsLoader;
        string legacy = (loader != null) ? loader() : null;
        if (!string.IsNullOrEmpty(legacy))
        {
            SaveActionsJson(legacy);
        }
        return legacy;
    }

    private static Document ReadDocument()
    {
        try
        {
            string path = FilePath;
            if (!File.Exists(path)) { return null; }
            string json = File.ReadAllText(path);
            if (string.IsNullOrEmpty(json)) { return null; }
            Document doc = JsonUtility.FromJson<Document>(json);
            if (doc == null || doc.version != Version) { return null; } // 未来の形式は読まない
            return doc;
        }
        catch (Exception)
        {
            return null; // 壊れたファイルは無いものとして扱う (レガシー/既定値へフォールバック)
        }
    }

    private static string[] ReadExistingLanes()
    {
        Document doc = ReadDocument();
        return (doc != null) ? doc.lanes : null;
    }

    private static ActionEntry[] ReadExistingActions()
    {
        Document doc = ReadDocument();
        return (doc != null) ? doc.actions : null;
    }

    private static string[] ParseLanes(string lanesJson)
    {
        if (string.IsNullOrEmpty(lanesJson)) { return null; }
        try
        {
            LanesData data = JsonUtility.FromJson<LanesData>(lanesJson);
            return (data != null) ? data.lanes : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static ActionEntry[] ParseActions(string actionsJson)
    {
        if (string.IsNullOrEmpty(actionsJson)) { return null; }
        try
        {
            ActionsData data = JsonUtility.FromJson<ActionsData>(actionsJson);
            return (data != null) ? data.actions : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>一時ファイルに書いてから入れ替える (クラッシュでも既存ファイルを壊さない)。</summary>
    private static bool WriteDocument(string[] lanes, ActionEntry[] actions)
    {
        var doc = new Document();
        doc.version = Version;
        doc.lanes = (lanes != null) ? lanes : new string[0];
        doc.actions = (actions != null) ? actions : new ActionEntry[0];

        try
        {
            string path = FilePath;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) { Directory.CreateDirectory(dir); }

            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(doc));
            if (File.Exists(path))
            {
                File.Replace(tmp, path, null);
            }
            else
            {
                File.Move(tmp, path);
            }
            return true;
        }
        catch (Exception e)
        {
            // 保存に失敗してもゲームは継続する (設定はレガシー側に残っている場合がある)。
            Debug.LogWarning("キー設定ファイルの保存に失敗しました: " + FilePath + " (" + e.Message + ")");
            return false;
        }
    }
}

/// <summary>
/// KeyBindings / KeyActions の既定の保存先:
/// 設定ファイル (正本) + レガシー PlayerPrefs (移行元・ミラー先)。
/// </summary>
public sealed class KeyConfigFileStorage : KeyBindings.IStorage, KeyActions.IStorage
{
    string KeyBindings.IStorage.LoadRaw()
    {
        return KeyConfigFile.LoadOrMigrateLanes();
    }

    void KeyBindings.IStorage.SaveRaw(string json)
    {
        KeyConfigFile.SaveLanesJson(json);
        Action<string> mirror = KeyConfigFile.LegacyLanesMirror;
        if (mirror != null) { mirror(json); }
    }

    string KeyActions.IStorage.LoadRaw()
    {
        return KeyConfigFile.LoadOrMigrateActions();
    }

    void KeyActions.IStorage.SaveRaw(string json)
    {
        KeyConfigFile.SaveActionsJson(json);
        Action<string> mirror = KeyConfigFile.LegacyActionsMirror;
        if (mirror != null) { mirror(json); }
    }
}
