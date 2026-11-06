using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 8 レーン分のキー割り当てを管理する静的クラス (正本)。
/// 判定 (Judge) とキーガイド (KeyGuideUI) はこの値を参照する。
/// 保存は PlayerPrefs キー "FlashBeat.KeyBindings" (JSON)。
/// 保存先は IStorage として注入できるため、ロジック部分は単体テスト可能。
/// </summary>
public static class KeyBindings
{
    /// <summary>保存先の抽象化。テストではインメモリ実装を注入する。</summary>
    public interface IStorage
    {
        string LoadRaw();
        void SaveRaw(string json);
    }

    private class PlayerPrefsStorage : IStorage
    {
        public const string PrefsKey = "FlashBeat.KeyBindings";

        public string LoadRaw()
        {
            return PlayerPrefs.HasKey(PrefsKey) ? PlayerPrefs.GetString(PrefsKey) : null;
        }

        public void SaveRaw(string json)
        {
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
        }
    }

    // 正本: 8 レーンの既定キー (Judge.LaneKeys と同じ構成)。
    // v0.1.2 では Judge.LaneKeys が private のため、ここに独自に持つ。
    private static readonly KeyCode[][] DefaultKeys = new KeyCode[][]
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

    private static IStorage storage = new PlayerPrefsStorage();
    private static bool usingDefaultStorage = true;
    private static KeyCode[][] current;
    private static bool loaded;

    /// <summary>割り当てが変わったときに発火する。KeyGuideUI などが表示を更新するために使う。</summary>
    public static event Action Changed;

    /// <summary>保存先を差し替える (テスト用)。null で既定の PlayerPrefs に戻す。呼ぶと未ロード状態に戻る。</summary>
    public static void SetStorage(IStorage value)
    {
        storage = (value != null) ? value : new PlayerPrefsStorage();
        usingDefaultStorage = storage is PlayerPrefsStorage;
        loaded = false;
    }

    /// <summary>レーン数 (常に 8)。</summary>
    public static int LaneCount
    {
        get { return DefaultKeys.Length; }
    }

    /// <summary>現在の割り当て (レーン数 x 可変長キー配列)。未ロードなら保存/既定値から読み込む。</summary>
    public static KeyCode[][] Current
    {
        get
        {
            EnsureLoaded();
            return current;
        }
    }

    /// <summary>指定レーンのキー配列。範囲外は空配列。</summary>
    public static KeyCode[] GetKeys(int lane)
    {
        KeyCode[][] table = Current;
        if (lane < 0 || lane >= table.Length) return EmptyKeys;
        return table[lane];
    }

    private static readonly KeyCode[] EmptyKeys = new KeyCode[0];

    /// <summary>レーンのキーをまとめて設定する。他レーンの同キーは移動になる。
    /// 空配列で呼ぶとレーンを空にする (編集中の一時状態。Save は空レーンを拒否する)。
    /// 移動によって移動元のレーンが空になる場合は拒否する (false)。</summary>
    public static bool SetLane(int lane, KeyCode[] keys)
    {
        KeyCode[][] table = Current;
        if (lane < 0 || lane >= table.Length) return false;

        var cleaned = new List<KeyCode>();
        var seen = new HashSet<KeyCode>();
        if (keys != null)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                KeyCode key = keys[i];
                if (key == KeyCode.None || IsReserved(key) || !seen.Add(key)) continue;
                cleaned.Add(key);
            }
        }

        KeyCode[][] copy = Clone(table);
        copy[lane] = EmptyKeys; // wholesale replace of this lane (empty input empties the lane)
        for (int i = 0; i < cleaned.Count; i++)
        {
            KeyCode key = cleaned[i];
            int other = FindLaneIn(copy, key);
            if (other >= 0 && other != lane)
            {
                copy[other] = Remove(copy[other], key);
            }
            if (!Contains(copy[lane], key))
            {
                copy[lane] = Append(copy[lane], key);
            }
        }

        // 移動元 (対象レーン以外) が空になる変更は拒否する
        for (int l = 0; l < copy.Length; l++)
        {
            if (l != lane && copy[l].Length == 0) return false;
        }

        current = copy;
        RaiseChanged();
        return true;
    }

    /// <summary>キーをレーンに割り当てる/解除する。キーは最大 1 レーンにのみ属する (割り当てると移動になる)。
    /// レーンが空になる変更は拒否する (false)。予約キーは拒否する。</summary>
    public static bool ToggleKey(int lane, KeyCode key)
    {
        KeyCode[][] table = Current;
        if (lane < 0 || lane >= table.Length) return false;
        if (key == KeyCode.None || IsReserved(key)) return false;

        int existingLane = FindLaneIn(table, key);
        if (existingLane == lane)
        {
            if (table[lane].Length <= 1) return false; // レーンを空にできない
            table[lane] = Remove(table[lane], key);
        }
        else if (existingLane >= 0)
        {
            if (table[existingLane].Length <= 1) return false; // 移動元が空になる
            table[existingLane] = Remove(table[existingLane], key);
            table[lane] = Append(table[lane], key);
        }
        else
        {
            table[lane] = Append(table[lane], key);
        }
        RaiseChanged();
        return true;
    }

    /// <summary>既定のキー割り当てに戻す。</summary>
    public static void ResetToDefaults()
    {
        current = CloneDefaults();
        RaiseChanged();
    }

    /// <summary>現在の割り当てを保存先に書き込む。空のレーンがある場合は保存しない (false)。</summary>
    public static bool Save()
    {
        if (storage == null) return false;

        KeyCode[][] table = Current;
        for (int lane = 0; lane < table.Length; lane++)
        {
            if (table[lane].Length == 0) return false; // レーンを空のまま保存しない
        }

        string json = ToJson();
        try
        {
            storage.SaveRaw(json);
            return true;
        }
        catch (Exception)
        {
            return false; // 保存に失敗しても gameplay は継続する
        }
    }

    /// <summary>保存先から再読み込みする。不正データは既定値にフォールバックする。</summary>
    public static void Load()
    {
        loaded = false;
        EnsureLoaded();
        RaiseChanged();
    }

    /// <summary>キー設定 UI で使ってはいけないキー (UI 操作・システム系) を判定する。</summary>
    public static bool IsReserved(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.None:
            case KeyCode.Escape:
            case KeyCode.Tab:
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
            case KeyCode.Space:
            case KeyCode.Backspace:
            case KeyCode.UpArrow:
            case KeyCode.DownArrow:
            case KeyCode.LeftArrow:
            case KeyCode.RightArrow:
            case KeyCode.Mouse0:
            case KeyCode.Mouse1:
            case KeyCode.Mouse2:
            case KeyCode.Mouse3:
            case KeyCode.Mouse4:
            case KeyCode.Mouse5:
            case KeyCode.Mouse6:
            case KeyCode.F1:
            case KeyCode.F2:
            case KeyCode.F3:
            case KeyCode.F4:
            case KeyCode.F5:
            case KeyCode.F6:
            case KeyCode.F7:
            case KeyCode.F8:
            case KeyCode.F9:
            case KeyCode.F10:
            case KeyCode.F11:
            case KeyCode.F12:
            case KeyCode.F13:
            case KeyCode.F14:
            case KeyCode.F15:
                return true;
            default:
                return false;
        }
    }

    /// <summary>現在の割り当てを JSON にする (KeyCode 名のカンマ区切り x 8 レーン)。</summary>
    public static string ToJson()
    {
        KeyCode[][] table = Current;
        var data = new BindingData();
        data.lanes = new List<string>(table.Length);
        for (int i = 0; i < table.Length; i++)
        {
            data.lanes.Add(string.Join(",", Array.ConvertAll(table[i], key => key.ToString())));
        }
        return JsonUtility.ToJson(data);
    }

    [Serializable]
    private class BindingData
    {
        public List<string> lanes;
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        string raw = (storage != null) ? storage.LoadRaw() : null;
        if (string.IsNullOrEmpty(raw) && usingDefaultStorage)
        {
            raw = ReadStreamingAssetsOverride();
        }
        current = Parse(raw);
    }

    /// <summary>保存データが無いときだけ読む運用者上書きファイル (StreamingAssets/keybindings.json)。</summary>
    private static string ReadStreamingAssetsOverride()
    {
        try
        {
            string path = Path.Combine(Application.streamingAssetsPath, "keybindings.json");
            if (!File.Exists(path)) return null;
            return File.ReadAllText(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>JSON を解析する。壊れていれば null を返す。レーン単位の不正は既定値にフォールバックする。</summary>
    private static KeyCode[][] Parse(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return CloneDefaults();

        try
        {
            var data = JsonUtility.FromJson<BindingData>(raw);
            if (data == null || data.lanes == null || data.lanes.Count != DefaultKeys.Length)
            {
                return CloneDefaults();
            }

            var result = new KeyCode[DefaultKeys.Length][];
            var seen = new HashSet<KeyCode>();
            for (int lane = 0; lane < DefaultKeys.Length; lane++)
            {
                result[lane] = ParseLane(data.lanes[lane], lane, seen);
            }
            return result;
        }
        catch (Exception)
        {
            return CloneDefaults();
        }
    }

    private static KeyCode[] ParseLane(string raw, int lane, HashSet<KeyCode> seen)
    {
        var keys = new List<KeyCode>();
        if (!string.IsNullOrEmpty(raw))
        {
            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                KeyCode key;
                if (Enum.TryParse(parts[i].Trim(), out key)
                    && key != KeyCode.None
                    && !IsReserved(key)
                    && seen.Add(key))
                {
                    keys.Add(key);
                }
            }
        }
        if (keys.Count == 0) return CloneLane(DefaultKeys[lane]); // 不正レーンは既定値へ
        return keys.ToArray();
    }

    private static int FindLaneIn(KeyCode[][] table, KeyCode key)
    {
        for (int lane = 0; lane < table.Length; lane++)
        {
            if (Contains(table[lane], key)) return lane;
        }
        return -1;
    }

    private static bool Contains(KeyCode[] keys, KeyCode key)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] == key) return true;
        }
        return false;
    }

    private static KeyCode[] Append(KeyCode[] keys, KeyCode key)
    {
        var result = new KeyCode[keys.Length + 1];
        Array.Copy(keys, result, keys.Length);
        result[keys.Length] = key;
        return result;
    }

    private static KeyCode[] Remove(KeyCode[] keys, KeyCode key)
    {
        var result = new KeyCode[keys.Length - 1];
        int index = 0;
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] == key) continue;
            result[index] = keys[i];
            index++;
        }
        return result;
    }

    private static KeyCode[][] Clone(KeyCode[][] table)
    {
        var result = new KeyCode[table.Length][];
        for (int i = 0; i < table.Length; i++)
        {
            result[i] = CloneLane(table[i]);
        }
        return result;
    }

    private static KeyCode[] CloneLane(KeyCode[] keys)
    {
        var result = new KeyCode[keys.Length];
        Array.Copy(keys, result, keys.Length);
        return result;
    }

    private static KeyCode[][] CloneDefaults()
    {
        return Clone(DefaultKeys);
    }

    private static void RaiseChanged()
    {
        var handler = Changed;
        if (handler != null) handler();
    }
}
