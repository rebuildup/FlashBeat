using System.Collections.Generic;
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

        // V6SelectDataSource.JacketResourcePathOf と同じ対応表。
        // Select/** は編集不可のため Result 側 (V6ResultView) と同じく写しを置く。
        // 画像が無い曲 (22 など) は空文字を返し、固定画像を流用しない。
        static readonly Dictionary<int, string> JacketFiles = new Dictionary<int, string>
        {
            { 1, "01-rosen.jpg" }, { 2, "02-mani.jpg" }, { 3, "03-who.jpg" }, { 4, "04-tweet.jpg" },
            { 5, "05-shuumatsu.jpg" }, { 6, "06-euta.jpg" }, { 7, "07-chartreuse.jpg" }, { 8, "08-psyched.jpg" },
            { 9, "09-dogbite.jpg" }, { 10, "10-tutorial.jpg" }, { 11, "11-kyoukiranmai.jpg" },
            { 12, "12-poison.jpg" }, { 13, "13-selfie.jpg" }, { 14, "14-4thsmile.jpg" },
            { 15, "15-calamity.jpg" }, { 16, "16-panopticon.jpg" }, { 17, "17-guru.jpg" }, { 18, "18-abun.jpg" },
            { 19, "19-mushin.jpg" }, { 20, "20-haruarashi.jpg" }, { 21, "21-bibibibi.jpg" },
            { 23, "23-override.jpg" }, { 24, "24-igaku.jpg" }, { 25, "25-tekkyu.jpg" }, { 26, "26-ikken.jpg" },
        };

        /// <summary>
        /// カタログ index → ジャケットの Resources 相対パス。
        /// 対応する画像が無い曲は空文字 (呼び出し側は「無し」を正直に表示する)。
        /// </summary>
        public static string JacketResourcePathOf(int songId)
        {
            string file;
            if (JacketFiles.TryGetValue(songId, out file))
            {
                return "V6/select/jacket/" + file;
            }

            return "";
        }
    }
}
