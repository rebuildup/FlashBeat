using UnityEngine;

namespace FlashBeat.Common
{
    /// <summary>
    /// 起動時の表示モードを決める純ロジック。Unity の PlayerPrefs に残る
    /// "Screenmanager Fullscreen mode" は ProjectSettings の fullscreenMode より優先されるため、
    /// 以前ウィンドウ表示で終了した端末では次回もウィンドウで起動する。
    /// その保存値を検出して補正するための判定だけをここに置き、テストから直接呼べるようにする。
    /// </summary>
    public static class DisplayModePolicy
    {
        /// <summary>常にボーダーレス全画面で起動する。排他全画面は使わない。</summary>
        public const FullScreenMode RequiredMode = FullScreenMode.FullScreenWindow;

        /// <summary>Unity が解像度・表示モードを保存する PlayerPrefs キー (先頭大文字・空白入り)。</summary>
        public const string StoredModeKey = "Screenmanager Fullscreen mode";

        /// <summary>保存値が古いときに消すキー。表示モードと解像度をまとめて既定へ戻す。</summary>
        public static readonly string[] StoredScreenKeys =
        {
            "Screenmanager Fullscreen mode",
            "Screenmanager Resolution Width",
            "Screenmanager Resolution Height",
            "Screenmanager Resolution Use Native",
        };

        /// <summary>現在のモードを RequiredMode へ直す必要があるか。</summary>
        public static bool NeedsCorrection(FullScreenMode current)
        {
            return current != RequiredMode;
        }

        /// <summary>
        /// PlayerPrefs に保存されたモード値が RequiredMode 以外か。
        /// Unity のシリアライズ値は ExclusiveFullScreen=0 / FullScreenWindow=1 /
        /// MaximizedWindow=2 / Windowed=3。
        /// </summary>
        public static bool StoredModeIsStale(int storedMode)
        {
            return storedMode != (int)RequiredMode;
        }
    }

    /// <summary>
    /// 起動直後に表示モードを FullScreenWindow へ揃える (Issue #369)。
    ///
    /// PlayerSettings の fullscreenMode は初回起動にしか効かず、以降は
    /// PlayerPrefs の "Screenmanager Fullscreen mode" が優先される。Mac 実機では
    /// この保存値が 2 (MaximizedWindow) のまま残り、ウィンドウ表示で起動していた。
    /// そこでシーン読み込み前にモードを明示し、古い保存値も消して次回以降の起動を揃える。
    ///
    /// Screen の解像度 API を呼んでウィンドウサイズを画面に合わせる擬似全画面は、
    /// ウィンドウ枠が残るため、このプロジェクトでは禁止している (#162 / #169)。
    /// </summary>
    public static class DisplayModeBootstrap
    {
        private const string Prefix = "[FullscreenBootstrap]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Apply()
        {
            if (Application.isEditor)
            {
                return;
            }

            var before = Screen.fullScreenMode;
            if (DisplayModePolicy.NeedsCorrection(before))
            {
                Screen.fullScreenMode = DisplayModePolicy.RequiredMode;
                Debug.Log(Prefix + " event=mode before=" + before
                    + " after=" + Screen.fullScreenMode);
            }

            PurgeStaleStoredMode();
        }

        /// <summary>
        /// 古い保存値があるときだけ、表示モードと解像度のキーを消して保存する。
        /// 毎回消すと Unity の保存と競合するため、RequiredMode 以外の値がある場合に限る。
        /// </summary>
        private static void PurgeStaleStoredMode()
        {
            if (!PlayerPrefs.HasKey(DisplayModePolicy.StoredModeKey))
            {
                return;
            }

            int stored = PlayerPrefs.GetInt(
                DisplayModePolicy.StoredModeKey, (int)DisplayModePolicy.RequiredMode);
            if (!DisplayModePolicy.StoredModeIsStale(stored))
            {
                return;
            }

            for (int i = 0; i < DisplayModePolicy.StoredScreenKeys.Length; i++)
            {
                PlayerPrefs.DeleteKey(DisplayModePolicy.StoredScreenKeys[i]);
            }

            PlayerPrefs.Save();
            Debug.Log(Prefix + " event=purge storedMode=" + stored);
        }
    }
}
