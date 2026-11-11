using UnityEngine;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// Issue #120: OpeningScene の v5 表示。v5 には Opening の画面フレームが無いため
    /// 01 Title の部品 (背景 + エッジ装飾 + ロゴ) で構成する (報告事項)。
    /// 自動遷移タイマーは OpeningSceneManager 側に残す。
    ///
    /// オープニング映像 (opening1.mp4) があるシーンでは <see cref="Build"/> に
    /// false を渡して不透明背景を省くこと。ScreenSpaceOverlay の Canvas は常に
    /// カメラ描画より手前なので、不透明背景は映像を完全に隠してしまう。
    /// </summary>
    public sealed class OpeningView : MonoBehaviour
    {
        /// <summary>映像が無い/長さ不明のときの従来の自動遷移待ち (秒)。</summary>
        public const float BaseAdvanceSeconds = 6.031f;

        /// <summary>映像の長さに合わせて延ばす場合の上限 (秒)。</summary>
        public const float MaxAdvanceSeconds = 12f;

        GameObject backdrop;
        GameObject decorations;

        /// <summary>背景 + エッジ装飾 + ロゴで構築する (従来どおり)。</summary>
        public static OpeningView Build()
        {
            return Build(withBackdrop: true);
        }

        /// <summary>
        /// Canvas (ScreenSpaceOverlay, Constant Pixel Size x1) ごと新規に構築する。
        /// withBackdrop = false はオープニング映像だけを表示する (Issue #371)。
        /// 背景・エッジ装飾・ロゴは映像に重ねない。映像が出せないときは
        /// <see cref="ShowBackdropFallback"/> が従来の装飾を後から足す。
        /// </summary>
        public static OpeningView Build(bool withBackdrop)
        {
            var canvasGo = new GameObject("V5 Opening Canvas", typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasConfigurator.Configure(canvas);

            var root = V5Ui.Node(canvasGo, "Root");
            V5Ui.Fill(root.GetComponent<RectTransform>(), 0f);
            var view = root.AddComponent<OpeningView>();

            if (withBackdrop)
            {
                view.backdrop = V5Decor.BuildBackdrop(root);
                view.decorations = view.BuildDecorations(root);
            }

            return view;
        }

        /// <summary>エッジ装飾と中央ロゴ。映像のみのときは生成しない。</summary>
        private GameObject BuildDecorations(GameObject root)
        {
            var group = V5Ui.Node(root, "Decorations");
            V5Ui.Fill(group.GetComponent<RectTransform>(), 0f);
            V5Decor.BuildEdgeDeco(group);

            // 中央ロゴ: 幅は画面幅の 2/3 (fhd で 1280) にし、高さは設計比率から算出。
            var p = UISizeProfile.Current;
            float logoW = Mathf.Min(Screen.width * (1280f / 1920f), 1280f * (p.FsM / 30f));
            float logoH = logoW / V5Decor.LogoAspect;
            V5Decor.BuildLogo(group,
                new Vector2((Screen.width - logoW) / 2f, -(Screen.height - logoH) / 2f),
                new Vector2(logoW, logoH));
            return group;
        }

        /// <summary>
        /// 映像の再生に失敗したときのフォールバック: 不透明背景と従来の装飾を後から足す
        /// (黒い画面のまま放置しない)。映像のみで構築したときも、ここで初めて
        /// ロゴとエッジ装飾を生成する (Issue #371)。
        /// </summary>
        public void ShowBackdropFallback()
        {
            if (backdrop == null)
            {
                backdrop = V5Decor.BuildBackdrop(gameObject);
            }
            backdrop.SetActive(true);

            if (decorations == null)
            {
                decorations = BuildDecorations(gameObject);
            }

            decorations.SetActive(true);
        }

        /// <summary>映像のみで構築したか (装飾がまだ無い)。診断とテスト用。</summary>
        public bool HasDecorations
        {
            get { return decorations != null; }
        }

        /// <summary>
        /// 自動遷移までの待ち時間 (純ロジック)。
        /// 映像がある場合は映像の長さと従来値の遅い方、ただし上限付き。
        /// 映像が無い / 長さが取得できない場合は従来値。
        /// </summary>
        public static float ResolveAutoAdvanceDelay(bool hasVideo, float videoLength)
        {
            if (!hasVideo || videoLength <= 0f)
            {
                return BaseAdvanceSeconds;
            }
            return Mathf.Clamp(videoLength, BaseAdvanceSeconds, MaxAdvanceSeconds);
        }
    }
}
