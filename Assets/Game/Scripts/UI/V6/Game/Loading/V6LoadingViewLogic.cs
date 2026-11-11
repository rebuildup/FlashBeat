using UnityEngine;

namespace FlashBeat.UI.V6.Game.Loading
{
    /// <summary>
    /// v6 曲ローディング画面 (Issue #224, design `g4Q3F`) の純ロジック (EditMode テスト対象)。
    /// design に進捗表現が無い (メイン空) ので、進捗の表現 (確定/不定バー、文言、
    /// ジャケットの対応表) をここに置く。
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

        /// <summary>不定インジケータの往復周期 (秒)。v5 GameLoadingView と同じ。</summary>
        public const float CycleSeconds = 1.6f;

        /// <summary>
        /// 不定インジケータの塗りの x (0..trackW-fillW)。往復 (0 → 端 → 0)。
        /// </summary>
        public static float IndeterminateX(float time, float trackW, float fillW)
        {
            if (trackW <= 0f || fillW <= 0f)
            {
                return 0f;
            }

            float range = trackW - fillW;
            if (range <= 0f)
            {
                return 0f;
            }

            float phase = (time % CycleSeconds) / CycleSeconds;
            float tri = phase < 0.5f ? phase * 2f : 2f - phase * 2f;
            return tri * range;
        }

        /// <summary>ロード中の見出し。</summary>
        public static string LoadingHeadline()
        {
            return "準備中…";
        }

        /// <summary>ロード中の補足 (進捗不明)。メディア準備は段階名しか持たない。</summary>
        public static string LoadingDetail()
        {
            return "動画と音声を読み込んでいます";
        }

        // --- 実進捗 (Issue #224) ---

        /// <summary>
        /// 進捗値が「既知」か。null は不明 (不定インジケータにフォールバック)。
        /// 0 は既知 (準備が始まったばかり) — 不明扱いにしない。
        /// </summary>
        public static bool HasProgress(float? progress)
        {
            return progress.HasValue;
        }

        /// <summary>確定バーの塗り幅 (0..trackW)。進捗は 0..1 にクランプ。</summary>
        public static float DeterminateFillWidth(float progress, float trackW)
        {
            return Mathf.Clamp01(progress) * trackW;
        }

        /// <summary>進捗のパーセント表記 (0 → "0%", 0.42 → "42%", 1 → "100%")。</summary>
        public static string ProgressPercentText(float progress)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f) + "%";
        }

        /// <summary>進捗が既知のときの補足テキスト。数値の正体 (譜面準備) も示す。</summary>
        public static string ProgressDetail(float progress)
        {
            return "譜面を準備中… " + ProgressPercentText(progress);
        }

        // --- ジャケット (Issue #224) ---

        /// <summary>
        /// ジャケットを 56×56 の枠に mode:"cover" で埋めるときの切り出し矩形
        /// (テクスチャ座標、原点は左下)。中心を正方形に取る。
        /// design N3HPJ は cover 指定なので、16:9 素材 (320×180) を
        /// そのまま入れて帯にしない。素材が正方形なら全面を返す。
        /// </summary>
        public static Rect CoverCropRect(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            float side = Mathf.Min(width, height);
            return new Rect((width - side) * 0.5f, (height - side) * 0.5f, side, side);
        }

        /// <summary>
        /// カタログ index → ジャケットの Resources 相対パス (拡張子なし)。
        /// 1..26 はすべて画像あり (22 = 灰Φ倶楽部も含む)。表に無い index は空文字
        /// (呼び出し側は「無し」を正直に表示する)。
        /// 解決は <see cref="V6JacketResources"/> に一本化している。
        /// </summary>
        public static string JacketResourcePathOf(int songId)
        {
            return V6JacketResources.PathOf(songId);
        }
    }
}
