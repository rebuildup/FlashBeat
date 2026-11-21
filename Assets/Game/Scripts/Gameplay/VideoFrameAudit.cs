using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// Issue #844: 背景映像の断続的な破損フレーム (ブロックモザイク/黒矩形) を
    /// 実機 AutoTest で定量的に検出するための診断。
    ///
    /// 破損は「前後の正常フレームに挟まれた単発の異常フレーム」なので、
    /// スクリーンショットの目視ではなく <see cref="VideoPlayer.texture"/>
    /// (= VideoPlayer がデコードして材質へ流している実テクスチャ) を
    /// 低解像度で読み戻し、フレーム間の勾配エネルギーがローリング中央値から
    /// 大きく外れたサンプルを suspect として記録する。
    ///
    /// 判定は自己校正: 同じ映像・同じ区間の直近サンプルを基準にするので、
    /// 暗いシーンや激しいカットがあっても「この映像に対する異常」だけを拾う。
    /// 動画が正常でもシーン転換で一時的にスパイクしうるため、suspect は
    /// FAIL ではなく計測値として result / Player.log に残す。
    /// </summary>
    public static class VideoFrameAudit
    {
        /// <summary>読み戻し解像度 (横)。縦はアスペクト維持で丸める。</summary>
        public const int SampleWidth = 160;

        /// <summary>suspect とみなす勾配エネルギーの倍率 (ローリング中央値比)。</summary>
        public const float SuspectRatio = 3f;

        /// <summary>
        /// 連続 suspect を 1 イベントとして数える。シーン転換のような持続的な
        /// エネルギー変化は長いランになるのに対し、モザイク/黒矩形のような
        /// 単発破損は孤立した 1 サンプルのランになる。件数だけでなく
        /// 「孤立イベント数」を見ることで両者を区別する (Issue #844)。
        /// </summary>
        public const int IsolatedRunLength = 1;

        /// <summary>基準を作るのに必要な最小サンプル数。</summary>
        public const int MinBaselineSamples = 8;

        /// <summary>ローリング基準に使う直近サンプル数。</summary>
        public const int BaselineWindow = 24;

        static RenderTexture sampleRt;
        static Texture2D sampleTex;
        static readonly List<float> baseline = new List<float>();
        static int samples;
        static int suspects;
        static int suspectEvents;
        static int isolatedSuspectEvents;
        static int suspectRunLength;
        static float maxRatio;
        static float maxRatioAtMediaTime = -1f;
        static float minRatio = float.MaxValue;
        static float minRatioAtMediaTime = -1f;

        public static int Samples { get { return samples; } }
        public static int Suspects { get { return suspects; } }
        /// <summary>連続 suspect ランの数 (シーン転換は 1 イベントにまとまる)。</summary>
        public static int SuspectEvents { get { return suspectEvents; } }
        /// <summary>ラン長 1 の suspect イベント数 (単発破損の候補)。</summary>
        public static int IsolatedSuspectEvents { get { return isolatedSuspectEvents; } }
        public static float MaxRatio { get { return maxRatio; } }
        public static float MaxRatioAtMediaTime { get { return maxRatioAtMediaTime; } }
        /// <summary>最小 ratio (エネルギー低下型の破損 = 黒矩形/平滑化の検出用)。</summary>
        public static float MinRatio { get { return minRatio == float.MaxValue ? 0f : minRatio; } }
        public static float MinRatioAtMediaTime { get { return minRatioAtMediaTime; } }

        /// <summary>
        /// スコアと基準中央値から suspect 判定を返す純粋関数 (EditMode テスト用)。
        /// 増大 (モザイク/ブロックのエッジ増加) と低下 (黒矩形/平滑化) の両方向を見る。
        /// </summary>
        public static bool IsSuspectScore(float score, float baselineMedian, int baselineCount)
        {
            if (baselineCount < MinBaselineSamples || baselineMedian <= 1e-4f) return false;
            float ratio = score / baselineMedian;
            return ratio > SuspectRatio || ratio < 1f / SuspectRatio;
        }

        /// <summary>
        /// 1 サンプルを取り、基準との比を返す。読めないときは -1。
        /// 呼び出しはプレイ中の Update から (GPU リードバックを伴う)。
        /// </summary>
        public static float Sample(VideoPlayer video, float mediaTime, out bool suspect)
        {
            suspect = false;
            Texture tex = video != null ? video.texture : null;
            if (tex == null || !video.isPlaying) return -1f;

            float score = ReadGradientEnergy(tex);
            if (score < 0f) return -1f;

            samples++;
            float baselineMedian = MedianOfTail(baseline, BaselineWindow);
            float ratio = baselineMedian > 1e-4f ? score / baselineMedian : 0f;
            if (ratio > 0f)
            {
                if (ratio > maxRatio)
                {
                    maxRatio = ratio;
                    maxRatioAtMediaTime = mediaTime;
                }
                if (ratio < minRatio)
                {
                    minRatio = ratio;
                    minRatioAtMediaTime = mediaTime;
                }
            }

            bool isSuspect = IsSuspectScore(score, baselineMedian, baseline.Count);
            if (isSuspect)
            {
                suspects++;
                suspectRunLength++;
            }
            else if (suspectRunLength > 0)
            {
                // ラン終了: イベント確定。長さ 1 は単発破損の候補。
                suspectEvents++;
                if (suspectRunLength == IsolatedRunLength) isolatedSuspectEvents++;
                suspectRunLength = 0;
            }

            // 基準は suspect を含めて更新する (破損フレームが基準を壊すと
            // 以降の正常フレームまで suspect になるため、中央値ベースで
            // 少数の外れ値は押し出される)。
            baseline.Add(score);
            if (baseline.Count > BaselineWindow) baseline.RemoveAt(0);

            suspect = isSuspect;
            return ratio;
        }

        /// <summary>テクスチャを低解像度へ落として勾配エネルギーを返す。</summary>
        static float ReadGradientEnergy(Texture source)
        {
            if (sampleRt == null)
            {
                sampleRt = new RenderTexture(SampleWidth, Mathf.Max(2, SampleWidth / 2), 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                sampleRt.Create();
                sampleTex = new Texture2D(SampleWidth, sampleRt.height, TextureFormat.RGBA32, false);
            }

            Graphics.Blit(source, sampleRt);
            var prev = RenderTexture.active;
            RenderTexture.active = sampleRt;
            sampleTex.ReadPixels(new Rect(0f, 0f, sampleRt.width, sampleRt.height), 0, 0);
            sampleTex.Apply();
            RenderTexture.active = prev;

            Color32[] px = sampleTex.GetPixels32();
            int w = sampleTex.width;
            int h = sampleTex.height;
            if (w < 2 || h < 2 || px == null || px.Length < w * h) return -1f;

            // 輝度の近傍差分の平均。モザイク/ブロック破損は画素間の不連続を
            // 正常フレームより大きく増やす。
            double sum = 0.0;
            int count = 0;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int i = row + x;
                    float lum = Luminance(px[i]);
                    if (x + 1 < w) { sum += Mathf.Abs(lum - Luminance(px[i + 1])); count++; }
                    if (y + 1 < h) { sum += Mathf.Abs(lum - Luminance(px[i + w])); count++; }
                }
            }
            return count > 0 ? (float)(sum / count) : -1f;
        }

        static float Luminance(Color32 c)
        {
            return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        }

        static float MedianOfTail(List<float> values, int window)
        {
            if (values.Count == 0) return 0f;
            int start = Mathf.Max(0, values.Count - window);
            var copy = new List<float>(values.Count - start);
            for (int i = start; i < values.Count; i++) copy.Add(values[i]);
            copy.Sort();
            return copy[copy.Count / 2];
        }

        /// <summary>
        /// 終了時に未確定の suspect ランをイベントとして確定する。
        /// プレイ終了直前でランが閉じていない場合の取りこぼし防止。
        /// </summary>
        public static void FinalizeAudit()
        {
            if (suspectRunLength <= 0) return;
            suspectEvents++;
            if (suspectRunLength == IsolatedRunLength) isolatedSuspectEvents++;
            suspectRunLength = 0;
        }

        /// <summary>テスト間で状態を持ち越さないためのリセット (EditMode 用)。</summary>
        public static void ResetForAudit()
        {
            baseline.Clear();
            samples = 0;
            suspects = 0;
            suspectEvents = 0;
            isolatedSuspectEvents = 0;
            suspectRunLength = 0;
            maxRatio = 0f;
            maxRatioAtMediaTime = -1f;
            minRatio = float.MaxValue;
            minRatioAtMediaTime = -1f;
        }
    }
}
