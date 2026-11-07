using System;
using System.Text;
using UnityEngine;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// Development ビルド / エディタ限定の F3 ウォッチャ。
    /// ネストクラスの MonoBehaviour はシリアライズの挙動が環境依存のためトップレベルに置く。
    /// </summary>
    internal sealed class FullscreenDiagnosticsWatcher : MonoBehaviour
    {
        private const string Prefix = "[FullscreenDiagnostics]";
        private const KeyCode DumpKey = KeyCode.F3;

        private void Update()
        {
            if (Input.GetKeyDown(DumpKey))
            {
                FullscreenDiagnostics.Log("F3");
                FullscreenDiagnostics.LogCanvases("F3");
                CaptureScreenshot();
            }
        }

        private static void CaptureScreenshot()
        {
            var path = "FullscreenDiagnostics_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".png";
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log(Prefix + " screenshot=" + path);
        }
    }

    /// <summary>
    /// 全画面 / 画面サイズ診断 (Issue #169 T4)。
    /// PlayerSettings の値 (fullscreenMode など) と実行時の実挙動の差異を
    /// 実機で客観測定するための手段。設定値の断定はせず、測定結果だけをログへ出す。
    ///
    /// - 起動時に 1 回、[RuntimeInitializeOnLoadMethod] で実効状態を 1 行ログ出力。
    /// - Development ビルド / エディタ限定で F3 押下時に再出力 + スクリーンショット保存。
    /// 実機評価ではこのログの Screen.width/height と各 Display の systemWidth/Height を
    /// 比較することで「全画面ピクセル被覆」を確認できる。
    /// </summary>
    public static class FullscreenDiagnostics
    {
        private const string Prefix = "[FullscreenDiagnostics]";

        /// <summary>起動時に 1 回、実効状態をログ出力する。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void LogAtStartup()
        {
            Log("startup");
        }

        /// <summary>
        /// 現在の実効状態を 1 行でログ出力する。
        /// tag は呼び出し元を識別する短いラベル (startup / F3 など)。
        /// </summary>
        public static void Log(string tag)
        {
            Debug.Log(BuildReport(tag));
        }

        /// <summary>
        /// 現在の実効状態を 1 行の文字列にまとめる。
        /// ログ解析しやすいよう key=value をカンマ区切りで並べる (JSON 風)。
        /// </summary>
        public static string BuildReport(string tag)
        {
            var sb = new StringBuilder();
            sb.Append(Prefix).Append(" event=").Append(tag);
            sb.Append(", screen=").Append(Screen.width).Append("x").Append(Screen.height);
            sb.Append(", currentResolution=").Append(Screen.currentResolution.width)
                .Append("x").Append(Screen.currentResolution.height)
                .Append("@").Append(Screen.currentResolution.refreshRateRatio.value).Append("Hz");
            sb.Append(", fullScreen=").Append(Screen.fullScreen);
            sb.Append(", fullScreenMode=").Append(Screen.fullScreenMode);
            sb.Append(", dpi=").Append(Screen.dpi.ToString("0.#"));
            sb.Append(", safeArea=").Append(Screen.safeArea.x).Append(",").Append(Screen.safeArea.y)
                .Append(",").Append(Screen.safeArea.width).Append("x").Append(Screen.safeArea.height);
            var displays = Display.displays;
            sb.Append(", displays=").Append(displays.Length);
            for (int i = 0; i < displays.Length; i++)
            {
                var d = displays[i];
                sb.Append(", display").Append(i)
                    .Append("=system:").Append(d.systemWidth).Append("x").Append(d.systemHeight)
                    .Append(",rendering:").Append(d.renderingWidth).Append("x").Append(d.renderingHeight)
                    .Append(",active:").Append(d.active);
            }

            sb.Append(", quality=").Append(QualitySettings.resolutionScalingFixedDPIFactor);
            sb.Append(", time=").Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            return sb.ToString();
        }

        /// <summary>各 Canvas の worldRect を 1 行でログ出力する (raycast 死活の確認用)。</summary>
        public static void LogCanvases(string tag)
        {
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var sb = new StringBuilder();
            sb.Append(Prefix).Append(" event=canvases:").Append(tag);
            sb.Append(", count=").Append(canvases.Length);
            for (int i = 0; i < canvases.Length; i++)
            {
                var c = canvases[i];
                var rt = c.rectTransform;
                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                sb.Append(", canvas").Append(i).Append("=\"").Append(c.name)
                    .Append("\" enabled=").Append(c.gameObject.activeInHierarchy)
                    .Append(" order=").Append(c.sortingOrder)
                    .Append(" mode=").Append(c.renderMode)
                    .Append(" raycaster=").Append(c.GetComponent<UnityEngine.UI.GraphicRaycaster>() != null)
                    .Append(" worldRect=min(").Append(corners[0].x.ToString("0.#")).Append(",").Append(corners[0].y.ToString("0.#"))
                    .Append(") max(").Append(corners[2].x.ToString("0.#")).Append(",").Append(corners[2].y.ToString("0.#")).Append(")");
            }

            Debug.Log(sb.ToString());
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateF3Watcher()
        {
            var go = new GameObject("FullscreenDiagnosticsWatcher");
            go.AddComponent<FullscreenDiagnosticsWatcher>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }
#endif
    }
}
