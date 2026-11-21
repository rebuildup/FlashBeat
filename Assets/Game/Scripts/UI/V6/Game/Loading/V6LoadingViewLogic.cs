using UnityEngine;

namespace FlashBeat.UI.V6.Game.Loading
{
    /// <summary>
    /// v6 曲ローディング画面 (Issue #224, design `g4Q3F`) の純ロジック (EditMode テスト対象)。
    /// design のメインゾーンは空で進捗・エラーの可視表現を持たない (Issue #606)。
    /// ここに残るのはフレーム配置・フッター凡例の寸法・ジャケット対応だけ。
    /// 進捗値そのものの管理は <see cref="GameLoadingProgress"/> と
    /// <see cref="V6LoadingView.SetLoading"/> が担う (表示はしない)。
    /// </summary>
    public static class V6LoadingViewLogic
    {
        /// <summary>design 上の共通フレーム寸法 (契約 §4 / V6Frame と同じ)。</summary>
        public const float FrameW = 1728f;
        public const float FrameH = 972f;

        /// <summary>
        /// 共通フレーム 1728×972 を 1:1 (等倍) で画面に置くときの左上原点 (design px)。
        /// ポーズと同じ式 (<see cref="FlashBeat.UI.V6.Game.Pause.V6PauseViewLogic.FrameOrigin"/> と同値):
        /// ((w-1728)/2, (h-972)/2) の切捨て — design の "Resolution Check" の配置
        /// (FHD (96,54) / 2K (416,234) / MacBook (416,346))。等比拡大はしない。
        /// 画面がフレームより小さいときは (0,0)。
        /// </summary>
        public static Vector2 FrameOrigin(int width, int height)
        {
            float x = Mathf.Max(0f, Mathf.Floor((width - FrameW) / 2f));
            float y = Mathf.Max(0f, Mathf.Floor((height - FrameH) / 2f));
            return new Vector2(x, y);
        }

        // --- フッター凡例 (design g4Q3F) ---

        /// <summary>
        /// フッター凡例 (Esc「戻る」) の左端 (design フレーム座標)。
        /// 正本は 決定 / 曲送り を 0×0 の項目として据え置き、項目間 gap16 で
        /// 各 16 ずつ進めるため、戻る はゾーンローカル x32 = フレーム x56 から始まる
        /// (design 画素: 2x 書き出しの Cap が x112..221 = フレーム 56..110.5)。
        /// 旧実装はフレーム x24 に置いていた (32px 左)。
        /// </summary>
        public const float FooterLegendX = 56f;

        /// <summary>凡例の上端 (フッターゾーン y876 内在り)。design 30。</summary>
        public const float FooterLegendY = 30f;

        /// <summary>凡例 KeyCap の高さ。design 36 (Title の凡例と同じ別設計、他画面は 32)。</summary>
        public const float FooterLegendCapHeight = 36f;

        /// <summary>KeyCap と Label の間隔。design 10 (他画面は 8)。</summary>
        public const float FooterLegendInnerGap = 10f;

        /// <summary>項目間の間隔。design 16 (他画面は 24)。</summary>
        public const float FooterLegendItemGap = 16f;

        /// <summary>凡例の文字サイズ。design fs22。</summary>
        public const float FooterLegendFontSize = 22f;

        // --- ジャケット (Issue #224) ---

        /// <summary>
        /// ジャケットを 56×56 の枠に mode:"cover" で埋めるときの切り出し矩形
        /// (テクスチャ座標、原点は左下)。中心を正方形に取る。
        /// design N3HPJ は cover 指定なので、16:9 素材 (320×180) を
        /// そのまま入れて帯にしない。素材が正方形なら全面を返す。
        /// 正本は <see cref="FlashBeat.UI.V6.V6JacketProvider.CoverCropRect"/>。
        /// </summary>
        public static Rect CoverCropRect(int width, int height)
        {
            return V6JacketProvider.CoverCropRect(width, height);
        }
    }
}
