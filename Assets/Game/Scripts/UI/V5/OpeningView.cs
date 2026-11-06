using UnityEngine;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// Issue #120: OpeningScene の v5 表示。v5 には Opening の画面フレームが無いため
    /// 01 Title の部品 (背景 + エッジ装飾 + ロゴ) で構成する (報告事項)。
    /// 自動遷移タイマーは OpeningSceneManager 側に残す。
    /// </summary>
    public sealed class OpeningView : MonoBehaviour
    {
        /// <summary>
        /// Canvas (ScreenSpaceOverlay, Constant Pixel Size x1) ごと新規に構築する。
        /// </summary>
        public static OpeningView Build()
        {
            var canvasGo = new GameObject("V5 Opening Canvas", typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasConfigurator.Configure(canvas);

            var root = V5Ui.Node(canvasGo, "Root");
            V5Ui.Fill(root.GetComponent<RectTransform>(), 0f);
            var view = root.AddComponent<OpeningView>();

            V5Decor.BuildBackdrop(root);
            V5Decor.BuildEdgeDeco(root);

            // 中央ロゴ: 幅は画面幅の 2/3 (fhd で 1280) にし、高さは設計比率から算出。
            var p = UISizeProfile.Current;
            float logoW = Mathf.Min(Screen.width * (1280f / 1920f), 1280f * (p.FsM / 30f));
            float logoH = logoW / V5Decor.LogoAspect;
            V5Decor.BuildLogo(root,
                new Vector2((Screen.width - logoW) / 2f, -(Screen.height - logoH) / 2f),
                new Vector2(logoW, logoH));
            return view;
        }
    }
}
