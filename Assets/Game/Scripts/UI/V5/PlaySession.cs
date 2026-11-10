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
    ///
    /// このクラスの 2 つの表示設定 (Key Guide / Lyrics) は issue-162 D-1 で
    /// **永続化した**。従来は静的フィールドだけだったため、アプリを再起動すると
    /// 選んだ値が消えていた (Settings タブの値は即時保存されるのに、Display タブ
    /// だけが消える非対称)。保存先は
    /// <see cref="FlashBeat.Persistence.IDisplaySettingsStorage"/>
    /// (ゲーム = `PlayerPrefs`、テスト = メモリ)。
    /// </summary>
    public static class PlaySession
    {
        /// <summary>Key Guide モードの保存キー (他 Issue と衝突しない接頭辞)。</summary>
        public const string KeyGuideKey = "FlashBeat.KeyGuide";

        /// <summary>Lyrics 表示可否の保存キー。</summary>
        public const string LyricsKey = "FlashBeat.Lyrics";

        private static KeyGuideMode keyGuide = KeyGuideMode.Compact;
        private static bool lyricsEnabled = true;

        static FlashBeat.Persistence.IDisplaySettingsStorage storage =
            new FlashBeat.Persistence.PlayerPrefsDisplaySettingsStorage();

        /// <summary>
        /// 表示設定の保存先。ゲームは `PlayerPrefs`、テストはメモリ実装を入れる
        /// (テストが開発者のセーブデータを書き換えないため)。
        /// </summary>
        public static FlashBeat.Persistence.IDisplaySettingsStorage Storage
        {
            get { return storage; }
            set
            {
                storage = value != null
                    ? value
                    : (FlashBeat.Persistence.IDisplaySettingsStorage)
                        new FlashBeat.Persistence.PlayerPrefsDisplaySettingsStorage();
            }
        }

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

        /// <summary>キーガイドの表示モードを設定し、保存する。</summary>
        public static void SetKeyGuide(KeyGuideMode mode)
        {
            keyGuide = mode;
            Save();
        }

        /// <summary>歌詞パネルの表示可否を設定し、保存する。</summary>
        public static void SetLyricsEnabled(bool enabled)
        {
            lyricsEnabled = enabled;
            Save();
        }

        /// <summary>現在の表示設定を保存する (再起動後も残す, D-1)。</summary>
        public static void Save()
        {
            storage.SetInt(KeyGuideKey, (int)keyGuide);
            storage.SetInt(LyricsKey, lyricsEnabled ? 1 : 0);
            storage.Save();
        }

        /// <summary>
        /// 保存済みの表示設定を復元する (起動時に 1 回)。
        /// 未保存のときは既定値 (Compact + 歌詞自動) のままにする。
        /// 範囲外の保存値 (古い形式 / 手で書き換えられた値) は既定値へ寄せる。
        /// </summary>
        public static void Load()
        {
            if (storage.HasKey(KeyGuideKey))
            {
                int raw = storage.GetInt(KeyGuideKey, (int)keyGuide);
                keyGuide = IsDefinedKeyGuide(raw) ? (KeyGuideMode)raw : KeyGuideMode.Compact;
            }
            if (storage.HasKey(LyricsKey))
            {
                lyricsEnabled = storage.GetInt(LyricsKey, lyricsEnabled ? 1 : 0) != 0;
            }
        }

        /// <summary>保存値が KeyGuideMode として成立するか。</summary>
        public static bool IsDefinedKeyGuide(int value)
        {
            return System.Enum.IsDefined(typeof(KeyGuideMode), value);
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

        /// <summary>
        /// セッション開始時の既定値へ戻す (既定 = Compact + 歌詞あり)。
        /// 保存済みの値には触らない (プレイのやり直しで利用者の選択を消さない)。
        /// </summary>
        public static void Reset()
        {
            keyGuide = KeyGuideMode.Compact;
            lyricsEnabled = true;
        }
    }
}
