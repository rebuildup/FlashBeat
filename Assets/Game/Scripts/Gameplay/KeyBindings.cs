using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 8 レーン分のキー割り当てを管理する静的クラス (正本)。
/// 判定 (Judge) とキーガイド (KeyGuideUI) はこの値を参照する。
/// 保存は設定ファイル FlashBeat-keyconfig.json (KeyConfigFile)。
/// レガシー PlayerPrefs に値があれば初回読み込み時にファイルへ移行する。
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

    // 正本 (single source of truth): 8 レーンの既定キー。
    // Judge.LaneKeys はこの配列へのエイリアスであり、判定 (Judge) と
    // キーガイド UI (KeyGuideUI / V5 / V6) はすべてここを参照する。
    // テーブルを二重に持つと UI と判定が静かにずれるため、ここ以外には定義しない。
    public static readonly KeyCode[][] DefaultKeys = new KeyCode[][]
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

    // 既定の保存先: 設定ファイル (正本) + レガシー PlayerPrefs (移行元・ミラー先)。
    private static readonly IStorage DefaultStorage = new KeyConfigFileStorage();
    private static IStorage storage = DefaultStorage;
    private static bool usingDefaultStorage = true;
    private static KeyCode[][] current;
    private static bool loaded;

    /// <summary>割り当てが変わったときに発火する。KeyGuideUI などが表示を更新するために使う。</summary>
    public static event Action Changed;

    // ---- Lane key helpers shared by every consumer of the lane keys (flash, menu navigation) ----
    // key -> frame in which it was injected by the -autotest runner; visible to Update in the NEXT frame only.
    private static readonly Dictionary<KeyCode, int> injectedDown = new Dictionary<KeyCode, int>();

    public static void InjectKeyDown(KeyCode key)
    {
        injectedDown[key] = Time.frameCount;
    }

    public static bool GetKeyDownOrInjected(KeyCode key)
    {
        int frame;
        return Input.GetKeyDown(key) || (injectedDown.TryGetValue(key, out frame) && Time.frameCount == frame + 1);
    }

    // ---- 検証ハーネス用の「押したまま」注入状態 -----------------------------
    // HOLD (ロングノーツ) の保持フェーズは「キーが **押されたまま** いるか」を
    // 毎フレーム見る。押下の 1 フレームだけInjectKeyDown しても保持できない。
    //
    // macOS の Unity legacy Input は CGEvent で送った合成キーイベントを
    // Input.GetKey として **見ない** ので、実キーの押下では保持を再現できない
    // (実測: 2026-11-10 macOS run 2)。そのため、既存の注入シームを拡張して
    // 保持状態も注入できるようにする。
    //
    // 製品ビルドでは誰も注入しないので、Input.GetKey と同じ結果になる。
    private static readonly HashSet<KeyCode> injectedHolds = new HashSet<KeyCode>();

    /// <summary>キーを「押したまま」にする (検証ハーネス専用)。</summary>
    public static void InjectKeyHold(KeyCode key)
    {
        if (key == KeyCode.None) { return; }
        injectedHolds.Add(key);
    }

    /// <summary>注入した「押したまま」を解除する (検証ハーネス専用)。</summary>
    public static void ReleaseInjectedKeyHold(KeyCode key)
    {
        injectedHolds.Remove(key);
    }

    /// <summary>注入した「押したまま」を全部解除する (検証ハーネス専用)。</summary>
    public static void ClearInjectedKeyHolds()
    {
        injectedHolds.Clear();
    }

    /// <summary>キーが押されているか (実キー + 注入された保持)。</summary>
    public static bool IsKeyHeld(KeyCode key)
    {
        if (key == KeyCode.None) { return false; }
        if (injectedHolds.Contains(key)) { return true; }
        return Input.GetKey(key);
    }

    /// <summary>そのレーンのキーが 1 つでも押されているか (保持判定用)。</summary>
    public static bool IsLaneKeyHeld(int lane)
    {
        KeyCode[] keys = GetKeys(lane);
        for (int i = 0; i < keys.Length; i++)
        {
            if (IsKeyHeld(keys[i])) { return true; }
        }
        return false;
    }

    /// <summary>True when any key bound to the lane (0-based) went down this frame.</summary>
    public static bool IsLaneKeyDown(int lane)
    {
        KeyCode[] keys = GetKeys(lane);
        for (int i = 0; i < keys.Length; i++)
        {
            if (GetKeyDownOrInjected(keys[i])) { return true; }
        }
        return false;
    }

    /// <summary>True when any key bound to lanes [fromLane, toLaneExclusive) went down this frame.</summary>
    public static bool IsAnyLaneKeyDown(int fromLane, int toLaneExclusive)
    {
        for (int lane = fromLane; lane < toLaneExclusive; lane++)
        {
            if (IsLaneKeyDown(lane)) { return true; }
        }
        return false;
    }

    /// <summary>保存先を差し替える (テスト用)。null で既定の設定ファイル保存先に戻す。呼ぶと未ロード状態に戻る。</summary>
    public static void SetStorage(IStorage value)
    {
        storage = (value != null) ? value : DefaultStorage;
        usingDefaultStorage = ReferenceEquals(storage, DefaultStorage);
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

    /// <summary>既定のキー割り当てに戻す。設定ファイルとミラーにも既定値を反映する。</summary>
    public static void ResetToDefaults()
    {
        current = CloneDefaults();
        RaiseChanged();
        Save();
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
