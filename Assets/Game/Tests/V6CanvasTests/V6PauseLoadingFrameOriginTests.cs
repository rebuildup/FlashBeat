using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Game.Loading;
using FlashBeat.UI.V6.Game.Pause;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// v6 ポーズ / 曲ローディングの共通フレーム配置 (純ロジック)。
    ///
    /// design "Resolution Check · Game (PAUSE)" (flashbeat-v6-nodes.json BrZMu) は、
    /// 共通フレーム `rw7zx` (1728×972) を **1:1 (等倍)** で各解像度に置いており、
    /// 原点は ((w-1728)/2, (h-972)/2) の切捨て:
    /// - FHD 1920×1080 → (96, 54)
    /// - 2K 2560×1440 → (416, 234)
    /// - MacBook Air M5 2560×1664 → (416, 346)
    /// かつての min(W/1728, H/972) 等比拡大 (引き伸ばし) は Owner 指示により契約変更
    /// (design は各解像度ごとにフレーム原点を固定しているので拡縮は起こらない)。
    /// </summary>
    public sealed class V6PauseLoadingFrameOriginTests
    {
        private static readonly int[][] DesignResolutions =
        {
            new[] { 1920, 1080 },   // FHD
            new[] { 2560, 1440 },   // 2K
            new[] { 2560, 1664 },   // MacBook Air M5
        };

        private static readonly float[][] ExpectedOrigins =
        {
            new[] { 96f, 54f },
            new[] { 416f, 234f },
            new[] { 416f, 346f },
        };

        [Test]
        public void PauseFrameOrigin_MatchesDesignResolutionCheck()
        {
            for (int i = 0; i < DesignResolutions.Length; i++)
            {
                int w = DesignResolutions[i][0];
                int h = DesignResolutions[i][1];
                var origin = V6PauseViewLogic.FrameOrigin(w, h);
                Assert.AreEqual(ExpectedOrigins[i][0], origin.x, 0.001f,
                    $"{w}x{h}: design のフレーム原点 x と一致しない");
                Assert.AreEqual(ExpectedOrigins[i][1], origin.y, 0.001f,
                    $"{w}x{h}: design のフレーム原点 y と一致しない");
            }
        }

        [Test]
        public void LoadingFrameOrigin_MatchesDesignResolutionCheck()
        {
            // ローディング (g4Q3F) も同じ `rw7zx` 共通フレームなので同じ配置規約。
            for (int i = 0; i < DesignResolutions.Length; i++)
            {
                int w = DesignResolutions[i][0];
                int h = DesignResolutions[i][1];
                var origin = V6LoadingViewLogic.FrameOrigin(w, h);
                Assert.AreEqual(ExpectedOrigins[i][0], origin.x, 0.001f, $"{w}x{h}: x");
                Assert.AreEqual(ExpectedOrigins[i][1], origin.y, 0.001f, $"{w}x{h}: y");
            }
        }

        [Test]
        public void PauseFrameOrigin_IsCenteredWithFloorDivision()
        {
            // ((w-1728)/2, (h-972)/2) の切捨て。奇数の余りは左上側に寄る。
            var origin = V6PauseViewLogic.FrameOrigin(1921, 1081);
            Assert.AreEqual(96f, origin.x, 0.001f);   // 193/2 = 96.5 → 96
            Assert.AreEqual(54f, origin.y, 0.001f);   // 109/2 = 54.5 → 54

            // フレームと同寸なら (0,0)。
            var same = V6PauseViewLogic.FrameOrigin(1728, 972);
            Assert.AreEqual(Vector2.zero, same);
        }

        [Test]
        public void FrameOrigin_ClampsToZero_WhenScreenSmallerThanFrame()
        {
            // フレームより小さい画面では負の原点にせず (0,0) に留める。
            var pause = V6PauseViewLogic.FrameOrigin(1280, 720);
            Assert.AreEqual(0f, pause.x, 0.001f);
            Assert.AreEqual(0f, pause.y, 0.001f);

            var loading = V6LoadingViewLogic.FrameOrigin(1280, 720);
            Assert.AreEqual(Vector2.zero, loading);

            // 異常値 (0 以下) も (0,0)。
            Assert.AreEqual(Vector2.zero, V6PauseViewLogic.FrameOrigin(0, 0));
            Assert.AreEqual(Vector2.zero, V6LoadingViewLogic.FrameOrigin(0, 0));
        }

        [Test]
        public void LoadingIndeterminateX_StaysWithinTrack()
        {
            // 不定インジケータはトラック (464px) からはみ出さない。
            for (int i = 0; i <= 32; i++)
            {
                float t = i * V6LoadingViewLogic.CycleSeconds / 32f;
                float x = V6LoadingViewLogic.IndeterminateX(t, 464f, 160f);
                Assert.GreaterOrEqual(x, 0f, $"t={t}");
                Assert.LessOrEqual(x, 464f - 160f, $"t={t}");
            }
        }
    }
}
