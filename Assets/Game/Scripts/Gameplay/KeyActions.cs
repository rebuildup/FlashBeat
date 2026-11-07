using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// メニュー操作 (決定/戻る/リトライ/前後移動…) のキー割り当てを管理する静的クラス。
/// 各 SceneManager が Input.GetKeyDown(KeyCode.X) を直接読まないよう、この表に集約する。
/// 保存は PlayerPrefs キー "FlashBeat.KeyActions" (JSON)。
/// レーン (KeyBindings) と同じく IStorage を注入できるため、ロジック部分は単体テスト可能。
/// </summary>
public static class KeyActions
{
    /// <summary>集約される操作の一覧。</summary>
    public enum KeyAction
    {
        Confirm,    // 決定 (選曲で曲を選ぶ / リザルトから選曲へ)
        Back,       // 戻る (選曲→タイトル / ポーズメニューを開く・閉じる)
        Retry,      // リトライ (リザルト)
        Option,     // オプション (タイトル)
        Exit,       // 終了 (タイトル)
        MenuRetry,  // ポーズメニュー: リトライ
        MenuEnd,    // ポーズメニュー: 終了
        MenuRetire, // ポーズメニュー: リタイア
        MenuSwitch, // ポーズメニュー: パネル切替
        Start,      // スタート (タイトル)
        Play,       // 再生開始 (演奏・タイプシーンの Space)
        NavPrev,    // 前の曲 (選曲の矢印。レーンキーと併用)
        NavNext     // 次の曲 (選曲の矢印。レーンキーと併用)
    }

    /// <summary>保存先の抽象化。テストではインメモリ実装を注入する。</summary>
    public interface IStorage
    {
        string LoadRaw();
        void SaveRaw(string json);
    }

    private class PlayerPrefsStorage : IStorage
    {
        public const string PrefsKey = "FlashBeat.KeyActions";

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

    /// <summary>既定表: 各操作の既定キー。今日の挙動を exactly 再現する
    /// (Start だけはタイトルの利便性のため Return/KeypadEnter を追加)。順序は表示順。</summary>
    private static readonly Dictionary<KeyAction, KeyCode[]> Defaults = new Dictionary<KeyAction, KeyCode[]>
    {
        { KeyAction.Confirm, new KeyCode[] { KeyCode.Space } },
        { KeyAction.Back, new KeyCode[] { KeyCode.Escape } },
        { KeyAction.Retry, new KeyCode[] { KeyCode.R } },
        { KeyAction.Option, new KeyCode[] { KeyCode.O } },
        { KeyAction.Exit, new KeyCode[] { KeyCode.E } },
        { KeyAction.MenuRetry, new KeyCode[] { KeyCode.R } },
        { KeyAction.MenuEnd, new KeyCode[] { KeyCode.E } },
        { KeyAction.MenuRetire, new KeyCode[] { KeyCode.Backspace } },
        { KeyAction.MenuSwitch, new KeyCode[] { KeyCode.T } },
        { KeyAction.Start, new KeyCode[] { KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter } },
        { KeyAction.Play, new KeyCode[] { KeyCode.Space } },
        { KeyAction.NavPrev, new KeyCode[] { KeyCode.LeftArrow } },
        { KeyAction.NavNext, new KeyCode[] { KeyCode.RightArrow } }
    };

    /// <summary>操作の日本語名 (キー設定 UI の行ラベル)。</summary>
    public static string GetDisplayName(KeyAction action)
    {
        switch (action)
        {
            case KeyAction.Confirm: return "決定";
            case KeyAction.Back: return "戻る";
            case KeyAction.Retry: return "リトライ (リザルト)";
            case KeyAction.Option: return "オプション";
            case KeyAction.Exit: return "終了 (タイトル)";
            case KeyAction.MenuRetry: return "メニュー: リトライ";
            case KeyAction.MenuEnd: return "メニュー: 終了";
            case KeyAction.MenuRetire: return "メニュー: リタイア";
            case KeyAction.MenuSwitch: return "メニュー: パネル切替";
            case KeyAction.Start: return "スタート (タイトル)";
            case KeyAction.Play: return "再生開始";
            case KeyAction.NavPrev: return "前の曲";
            case KeyAction.NavNext: return "次の曲";
            default: return action.ToString();
        }
    }

    /// <summary>レーンキーが生きている場面 (演奏中のポーズ開閉、再生待ち) で使う操作。
    /// これらは誤爆防止のため予約キーのみ割り当て可能 (既定値は例外なくこの規則に一致する)。</summary>
    public static bool IsInGameAction(KeyAction action)
    {
        switch (action)
        {
            case KeyAction.Back:
            case KeyAction.Play:
                return true;
            default:
                return false;
        }
    }

    /// <summary>全操作を固定順で列挙する (キー設定 UI の行順)。</summary>
    public static readonly KeyAction[] All = new KeyAction[]
    {
        KeyAction.Confirm,
        KeyAction.Back,
        KeyAction.Retry,
        KeyAction.Option,
        KeyAction.Exit,
        KeyAction.MenuRetry,
        KeyAction.MenuEnd,
        KeyAction.MenuRetire,
        KeyAction.MenuSwitch,
        KeyAction.Start,
        KeyAction.Play,
        KeyAction.NavPrev,
        KeyAction.NavNext
    };

    private static IStorage storage = new PlayerPrefsStorage();
    private static Dictionary<KeyAction, KeyCode[]> current;
    private static bool loaded;

    /// <summary>割り当てが変わったときに発火する。ヘルプ表示などが更新するために使う。</summary>
    public static event Action Changed;

    /// <summary>最後に拒否された理由 (UI のメッセージ表示用)。成功時は空。</summary>
    public static string LastError { get; private set; }

    /// <summary>保存先を差し替える (テスト用)。null で既定の PlayerPrefs に戻す。呼ぶと未ロード状態に戻る。</summary>
    public static void SetStorage(IStorage value)
    {
        storage = (value != null) ? value : new PlayerPrefsStorage();
        loaded = false;
    }

    /// <summary>指定操作の現在のキー配列。未ロードなら保存/既定値から読み込む。</summary>
    public static KeyCode[] Get(KeyAction action)
    {
        EnsureLoaded();
        KeyCode[] keys;
        return current.TryGetValue(action, out keys) ? keys : new KeyCode[0];
    }

    /// <summary>指定操作のキーがこのフレームで押されたか (KeyBindings の注入フック経由)。</summary>
    public static bool IsDown(KeyAction action)
    {
        KeyCode[] keys = Get(action);
        for (int i = 0; i < keys.Length; i++)
        {
            if (KeyBindings.GetKeyDownOrInjected(keys[i])) { return true; }
        }
        return false;
    }

    /// <summary>キーがレーンのどれかに割り当てられているか。</summary>
    public static bool IsLaneBound(KeyCode key)
    {
        KeyCode[][] lanes = KeyBindings.Current;
        for (int lane = 0; lane < lanes.Length; lane++)
        {
            KeyCode[] keys = lanes[lane];
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] == key) { return true; }
            }
        }
        return false;
    }

    /// <summary>操作のキーをまとめて設定する (編集中の一時状態。Save は空操作を拒否する)。
    /// 規則: レーンキーが生きている場面で使う操作 (IsInGameAction) は予約キーのみ。
    /// それ以外の操作は、レーンに割り当てられていないキーなら何でも可。
    /// 1 つでも規則違反のキーがあれば全体を拒否する (false, LastError に理由)。</summary>
    public static bool Set(KeyAction action, KeyCode[] keys)
    {
        LastError = string.Empty;
        EnsureLoaded();
        var cleaned = new List<KeyCode>();
        var seen = new HashSet<KeyCode>();
        bool inGame = IsInGameAction(action);
        if (keys != null)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                KeyCode key = keys[i];
                if (key == KeyCode.None || !seen.Add(key)) continue;
                if (KeyBindings.IsReserved(key))
                {
                    if (!inGame) { cleaned.Add(key); continue; }
                    // 予約キーはレーンに割り当てられないので、演奏中の操作にも安全。
                    cleaned.Add(key);
                    continue;
                }
                if (inGame)
                {
                    // レーンキーが生きている場面で押す操作は、誤爆を避けるため予約キーのみ。
                    LastError = "演奏中に使う操作は Space/Enter/Esc/BS/矢印/F キーのみ割り当てできます";
                    return false;
                }
                if (IsLaneBound(key))
                {
                    LastError = "そのキーはレーンに割り当てられています (レーン設定で外してから割り当ててください)";
                    return false;
                }
                cleaned.Add(key);
            }
        }

        var copy = new Dictionary<KeyAction, KeyCode[]>(current);
        copy[action] = cleaned.ToArray();
        current = copy;
        RaiseChanged();
        return true;
    }

    /// <summary>既定のキー割り当てに戻す。</summary>
    public static void ResetToDefaults()
    {
        current = CloneDefaults();
        LastError = string.Empty;
        RaiseChanged();
    }

    /// <summary>現在の割り当てを保存先に書き込む。キーが空の操作がある場合は保存しない (false)。</summary>
    public static bool Save()
    {
        if (storage == null) { return false; }
        EnsureLoaded();
        foreach (KeyValuePair<KeyAction, KeyCode[]> pair in current)
        {
            if (pair.Value.Length == 0) { return false; } // キーの無い操作のまま保存しない
        }

        try
        {
            storage.SaveRaw(ToJson());
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

    /// <summary>現在の割り当てを JSON にする (操作名 → キー名のカンマ区切り)。</summary>
    public static string ToJson()
    {
        EnsureLoaded();
        var data = new ActionsData();
        data.actions = new List<ActionEntry>(current.Count);
        foreach (KeyAction action in All)
        {
            KeyCode[] keys;
            if (!current.TryGetValue(action, out keys)) { continue; }
            data.actions.Add(new ActionEntry
            {
                action = action.ToString(),
                keys = string.Join(",", Array.ConvertAll(keys, key => key.ToString()))
            });
        }
        return JsonUtility.ToJson(data);
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
        public List<ActionEntry> actions;
    }

    private static void EnsureLoaded()
    {
        if (loaded) { return; }
        loaded = true;

        string raw = (storage != null) ? storage.LoadRaw() : null;
        current = Parse(raw);
    }

    /// <summary>JSON を解析する。壊れていれば既定値を返す。操作単位の不正はその操作の既定値にフォールバックする。</summary>
    private static Dictionary<KeyAction, KeyCode[]> Parse(string raw)
    {
        Dictionary<KeyAction, KeyCode[]> result = CloneDefaults();
        if (string.IsNullOrEmpty(raw)) { return result; }

        try
        {
            var data = JsonUtility.FromJson<ActionsData>(raw);
            if (data == null || data.actions == null) { return result; }

            for (int i = 0; i < data.actions.Count; i++)
            {
                ActionEntry entry = data.actions[i];
                if (entry == null || string.IsNullOrEmpty(entry.action)) { continue; }
                KeyAction action;
                if (!Enum.TryParse(entry.action, out action) || !Defaults.ContainsKey(action)) { continue; }
                KeyCode[] keys = ParseKeys(entry.keys);
                if (keys.Length > 0) { result[action] = keys; } // 空/不正は既定値のまま
            }
            return result;
        }
        catch (Exception)
        {
            return CloneDefaults();
        }
    }

    private static KeyCode[] ParseKeys(string raw)
    {
        var keys = new List<KeyCode>();
        var seen = new HashSet<KeyCode>();
        if (!string.IsNullOrEmpty(raw))
        {
            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                KeyCode key;
                if (Enum.TryParse(parts[i].Trim(), out key)
                    && key != KeyCode.None
                    && seen.Add(key))
                {
                    keys.Add(key);
                }
            }
        }
        return keys.ToArray();
    }

    private static Dictionary<KeyAction, KeyCode[]> CloneDefaults()
    {
        var result = new Dictionary<KeyAction, KeyCode[]>(Defaults.Count);
        foreach (KeyValuePair<KeyAction, KeyCode[]> pair in Defaults)
        {
            result[pair.Key] = (KeyCode[])pair.Value.Clone();
        }
        return result;
    }

    private static void RaiseChanged()
    {
        var handler = Changed;
        if (handler != null) { handler(); }
    }
}
