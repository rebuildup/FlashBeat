using UnityEngine;

/// <summary>
/// ゲーム中のキーガイドの表示モード (Issue #167)。
/// </summary>
public enum KeyGuideMode
{
    /// <summary>ガイドを出さない。</summary>
    Off,

    /// <summary>レーンごとに代表キー 1 個を出す (従来の下端ストリップ)。</summary>
    Compact,

    /// <summary>レーンごとに割り当てられたキーをすべて出す。</summary>
    Full,
}

/// <summary>
/// Select (Ready) で選んだ「このプレイの設定」を GameScene へ渡すセッション状態 (Issue #167)。
///
/// 旧実装は SelectView の private static 変数 (keyGuideMode / lyricsMode) に
/// 書き込むだけで GameScene 側は一切参照しないため、UI の値だけが変わる
/// 偽値になっていた。ここでは画面をまたいで有効なセッション状態として保持し、
/// GameHudView / GameSceneManager が実際に読む。
///
/// 設定値 (音量 / ノーツ速度 / 判定幅) の永続化は
/// <see cref="OptionSettings"/> (OptionSceneManager.cs) が担当する。
///
/// このクラスの 2 つの表示設定 (Key Guide / Lyrics) は **セッション設定**
/// (Issue #241 / issue-162 D-1 の訂正)。Option の Display タブで「このプレイの
/// 設定」として選び、Title へ戻った時点 (来場者の交代境界) で
/// <see cref="Reset"/> が既定値へ戻す。PlayerPrefs には保存しない
/// (音量 / NoteSpeed 等の永続設定は <see cref="OptionSettings"/> の担当)。
/// </summary>
public static class PlaySession
{
    /// <summary>旧永続化キー。#241 で保存をやめたが、ClearSaveData が残骸を消すために残す。</summary>
    public const string KeyGuideKey = "FlashBeat.KeyGuide";

    /// <summary>旧永続化キー。#241 で保存をやめたが、ClearSaveData が残骸を消すために残す。</summary>
    public const string LyricsKey = "FlashBeat.Lyrics";

    private static KeyGuideMode keyGuide = KeyGuideMode.Compact;
    private static bool lyricsEnabled = true;

    /// <summary>ゲーム中のキーガイドの表示モード。</summary>
    public static KeyGuideMode KeyGuide
    {
        get { return keyGuide; }
    }

    /// <summary>
    /// 歌詞パネルを出してよいか (Ready の Lyrics = Off で false)。
    /// Issue #734: これは「曲中のパネル枠を出すか」の固定スイッチ。
    /// ON なら歌詞データの有無・行間・間奏に関わらず枠を出しっぱなしにし、
    /// OFF なら曲中を通して隠す。かつての「歌詞がある行のときだけ出す
    /// (Auto)」意味論は廃止した — 枠の可視性は本文データから独立する。
    /// </summary>
    public static bool LyricsEnabled
    {
        get { return lyricsEnabled; }
    }

    /// <summary>キーガイドの表示モードを設定する (セッション内のみ)。</summary>
    public static void SetKeyGuide(KeyGuideMode mode)
    {
        keyGuide = mode;
    }

    /// <summary>歌詞パネルの表示可否を設定する (セッション内のみ)。
    /// Issue #734: 設定値はパネル枠の可視性を固定する (本文データには
    /// 依存しない)。Pause / Option の Display 設定からも同じ値を書く。</summary>
    public static void SetLyricsEnabled(bool enabled)
    {
        lyricsEnabled = enabled;
    }

    /// <summary>Ready 画面に出すキーガイドの表示文字列。</summary>
    public static string KeyGuideLabel(KeyGuideMode mode)
    {
        switch (mode)
        {
            case KeyGuideMode.Off: return "Off";
            case KeyGuideMode.Full: return "Full";
            default: return "Compact";
        }
    }

    /// <summary>次のキーガイドモード (Ready 画面の ← → ボタン用)。</summary>
    public static KeyGuideMode NextKeyGuide(KeyGuideMode mode, int delta)
    {
        KeyGuideMode[] modes = { KeyGuideMode.Compact, KeyGuideMode.Full, KeyGuideMode.Off };
        int index = System.Array.IndexOf(modes, mode);
        if (index < 0) index = 0;
        int next = (index + delta % modes.Length + modes.Length) % modes.Length;
        return modes[next];
    }

    /// <summary>
    /// 歌詞パネルの表示文字列 (Ready 画面 / Pause の Display タブ用)。
    /// "Auto" は旧来の表示名の名残 (Issue #734 以降は ON = 枠を常時表示)。
    /// 表示名の互換のため文字列は変えない。
    /// </summary>
    public static string LyricsLabel(bool enabled)
    {
        return enabled ? "Auto" : "Off";
    }

    /// <summary>
    /// セッション開始時の既定値へ戻す (既定 = Compact + 歌詞あり)。
    /// 来場者交代の導線 (Result → Title, ResultSceneManager.TitleScene)
    /// で呼ぶ。Title への通常進入 (Option → Title / Select → Title の
    /// 戻る操作) は同じ人なので呼ばず、値は保持される (Issue #241)。
    /// </summary>
    public static void Reset()
    {
        keyGuide = KeyGuideMode.Compact;
        lyricsEnabled = true;
    }
}
