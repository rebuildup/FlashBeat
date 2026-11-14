using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FlashBeat.Common
{
    /// <summary>
    /// macOS ネイティブ全画面 (Issue #369) への入口。
    ///
    /// Unity の FullScreenWindow は NSScreen.visibleFrame (メニューバーを除く
    /// 作業領域) サイズのボーダーレスウィンドウを作るため、Mac 実機では上部
    /// 約 76 px が描画面の外に残りメニューバーが見える (#369)。
    /// プラグインは `toggleFullScreen:` で macOS の全画面化を要求し、
    /// メニューバー/Dock の自動非表示を有効にしたうえで、Unity が
    /// ウィンドウを作業領域サイズへ戻そうとするのを
    /// `_setFrameCommon:display:fromServer:` で継続的に矯正する。
    /// プラグインが無い・失敗した場合は false を返し、
    /// 呼び出し側は従来のボーダーレス表示のまま継続する。
    /// </summary>
    internal static class NativeFullscreen
    {
        private const string LogPrefix = "[FullscreenBootstrap]";
        private static bool requested;

        [DllImport("FlashBeatNative", EntryPoint = "FBEnterNativeFullscreen")]
        private static extern void EnterNativeFullscreen();

        /// <summary>
        /// ネイティブ全画面への遷移を 1 回だけ要求する。
        /// autotest 実行時は Space 遷移が証跡取得を乱すため呼ばない。
        /// </summary>
        /// <returns>要求を出せたら true (実際に遷移したかは別)。</returns>
        public static bool TryEnter()
        {
            if (Application.platform != RuntimePlatform.OSXPlayer)
            {
                return false;
            }

            // 証跡・検証ハーネスの実行中は別 Space への遷移で画面が切り替わるため、
            // 決定的な計測を優先してネイティブ全画面は使わない。
            if (V6ScreenAudit.GetArg("-autotest") != null)
            {
                return false;
            }

            if (requested)
            {
                return true;
            }

            try
            {
                EnterNativeFullscreen();
                requested = true;
                Debug.Log(LogPrefix + " event=native-fullscreen-requested");
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }
    }
}
