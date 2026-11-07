using UnityEngine;

namespace FlashBeat.UI.V5
{
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
    /// </summary>
    public static class PlaySession
    {
        private static KeyGuideMode keyGuide = KeyGuideMode.Compact;
        private static bool lyricsEnabled = true;

        /// <summary>ゲーム中のキーガイドの表示モード。</summary>
        public static KeyGuideMode KeyGuide
        {
            get { return keyGuide; }
        }

        /// <summary>歌詞パネルを出してよいか (Ready の Lyrics = Off で false)。</summary>
        public static bool LyricsEnabled
        {
            get { return lyricsEnabled; }
        }

        /// <summary>キーガイドの表示モードを設定する。</summary>
        public static void SetKeyGuide(KeyGuideMode mode)
        {
            keyGuide = mode;
        }

        /// <summary>歌詞パネルの表示可否を設定する。</summary>
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

        /// <summary>歌詞の表示文字列 (Ready 画面 / Pause の Display タブ用)。</summary>
        public static string LyricsLabel(bool enabled)
        {
            return enabled ? "Auto" : "Off";
        }

        /// <summary>セッション開始時の既定値へ戻す (既定 = Compact + 歌詞あり)。</summary>
        public static void Reset()
        {
            keyGuide = KeyGuideMode.Compact;
            lyricsEnabled = true;
        }
    }
}
