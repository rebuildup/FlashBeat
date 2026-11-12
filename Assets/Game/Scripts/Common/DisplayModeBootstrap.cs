using System.Collections;
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

        /// <summary>
        /// 描画面がディスプレイ全体を被覆しているか。Screen が報告する表示モードが
        /// RequiredMode でも、macOS では実ウィンドウがメニューバー分を除いた
        /// 作業領域サイズのまま残ることがある (#369 Mac 実機)。モード値ではなく
        /// ピクセル被覆で判定する。
        /// </summary>
        public static bool FillsDisplay(int screenWidth, int screenHeight, int displayWidth, int displayHeight)
        {
            if (displayWidth <= 0 || displayHeight <= 0)
            {
                return true; // 情報が無い環境では補正しない (誤ってトグルしない)
            }

            return screenWidth >= displayWidth && screenHeight >= displayHeight;
        }

        /// <summary>
        /// 保存済みの解像度がディスプレイのネイティブサイズと違うか。
        /// Unity は終了時にウィンドウサイズを保存し、次回起動でそれを復元するため、
        /// 一度ウィンドウ表示で終了した端末では全画面のはずが作業領域サイズで
        /// 起動する (#369)。ネイティブより小さい保存値だけを古い値として扱う。
        /// </summary>
        public static bool SavedResolutionIsStale(int width, int height, int displayWidth, int displayHeight)
        {
            if (width <= 0 || height <= 0 || displayWidth <= 0 || displayHeight <= 0)
            {
                return false;
            }

            return width < displayWidth || height < displayHeight;
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
            PurgeStaleResolution();

            // macOS 実機 (#369): Screen.fullScreenMode が FullScreenWindow を返しても
            // 実ウィンドウはメニューバー分を除いた作業領域サイズのままで、メニューバーが
            // 見えることがある。モードの代入だけでは Unity 側の状態が変わらないため、
            // 最初のフレーム後に被覆を測り、足りなければ Windowed → FullScreenWindow の
            // 遷移を強制する。
            var go = new GameObject("DisplayModeBootstrap");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<DisplayModeBootstrapDriver>();
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

        /// <summary>
        /// ネイティブより小さい保存解像度を消し、ネイティブ解像度を使う設定へ戻す。
        /// Unity が終了時に保存し直すため、起動のたびに確認する。
        /// </summary>
        private static void PurgeStaleResolution()
        {
            int width = PlayerPrefs.GetInt("Screenmanager Resolution Width", 0);
            int height = PlayerPrefs.GetInt("Screenmanager Resolution Height", 0);
            int displayWidth = Display.main.systemWidth;
            int displayHeight = Display.main.systemHeight;
            if (!DisplayModePolicy.SavedResolutionIsStale(width, height, displayWidth, displayHeight))
            {
                return;
            }

            PlayerPrefs.DeleteKey("Screenmanager Resolution Width");
            PlayerPrefs.DeleteKey("Screenmanager Resolution Height");
            PlayerPrefs.SetInt("Screenmanager Resolution Use Native", 1);
            PlayerPrefs.Save();
            Debug.Log(Prefix + " event=purge-resolution stored=" + width + "x" + height
                + ", display=" + displayWidth + "x" + displayHeight);
        }
    }

    /// <summary>
    /// 最初のフレーム後に全画面のピクセル被覆を確かめ、必要なら遷移をやり直す
    /// (Issue #369)。Unity は Windowed 以外のモードを要求しても、実ウィンドウが
    /// 作業領域サイズのまま残ると要求モードをそのまま報告するため、遷移を発生
    /// させるには一度ウィンドウ表示へ戻す必要がある。
    /// </summary>
    internal sealed class DisplayModeBootstrapDriver : MonoBehaviour
    {
        private const string Prefix = "[FullscreenBootstrap]";

        private IEnumerator Start()
        {
            // 起動直後のウィンドウ生成・復元が終わってから測る。
            yield return null;
            LogReport("first-frame");
            if (Covered())
            {
                LogReport("covered");
                yield break;
            }

            // macOS は「報告モードは FullScreenWindow なのに実ウィンドウは作業領域サイズ」
            // という状態を作る。ウィンドウは既にボーダーレスなので、
            // 旧 API の Screen.fullScreen でメニューバーの非表示と再レイアウトを要求する
            // (Windowed を経由するとタイトルバーが付いて戻らない)。
            Debug.Log(Prefix + " event=request-legacy-fullscreen");
            Screen.fullScreen = true;
            yield return new WaitForSecondsRealtime(0.5f);
            LogReport("after-legacy-0.5s");
            yield return new WaitForSecondsRealtime(1.5f);
            LogReport("after-legacy-2s");

            if (!Covered())
            {
                Debug.Log(Prefix + " event=request-fullscreen-mode");
                Screen.fullScreenMode = DisplayModePolicy.RequiredMode;
                yield return new WaitForSecondsRealtime(0.5f);
                LogReport("after-mode-0.5s");
                yield return new WaitForSecondsRealtime(1.5f);
                LogReport("after-mode-2s");
            }

            LogReport(Covered() ? "covered" : "still-uncovered");
        }

        private static bool Covered()
        {
            return DisplayModePolicy.FillsDisplay(Screen.width, Screen.height,
                Display.main.systemWidth, Display.main.systemHeight);
        }

        private static void LogReport(string tag)
        {
            Debug.Log(Prefix + " event=" + tag
                + ", screen=" + Screen.width + "x" + Screen.height
                + ", display=" + Display.main.systemWidth + "x" + Display.main.systemHeight
                + ", fullScreen=" + Screen.fullScreen
                + ", fullScreenMode=" + Screen.fullScreenMode
                + ", safeArea=" + Screen.safeArea.width + "x" + Screen.safeArea.height);
        }
    }
}
