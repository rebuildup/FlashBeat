using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;
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

        // --- design uDNC0 (07b Game · v6 · Pause) の実配置値 ---
        // 正本は docs/design/v6/flashbeat-v6-nodes.json (uDNC0) と
        // design/exports/v6/screens/uDNC0-pause.png (描画)。

        [Test]
        public void HeaderBackButton_MatchesDesign()
        {
            // design Zone · 戻る: ヘッダー内 (0,20) 56×56 → ページ (24,20)。
            // 中身は arrow-left 24 + Key "Esc" fs16 (V6Controls.FillBackButton / 正本 zCQBY)。
            Assert.AreEqual(24f, V6PauseViewLogic.BackButtonX, 0.001f, "戻るボタン x (ページ座標)");
            Assert.AreEqual(20f, V6PauseViewLogic.BackButtonY, 0.001f, "戻るボタン y (ページ座標)");
            Assert.AreEqual(56f, V6PauseViewLogic.BackButtonSize, 0.001f, "戻るボタン寸法");
        }

        [Test]
        public void FooterOperationButtons_MatchDesign()
        {
            // Op · 選曲・リトライ: ページ (906,896) 520×56 =
            // 選曲へ 280 + gap16 + リトライ 220 (design auto-layout。
            // リトライは独立ボタンのまま左ボタンへ統合しない)。
            Assert.AreEqual(906f, V6PauseViewLogic.OpGroupX, 0.001f, "Op グループ x (ページ座標)");
            Assert.AreEqual(896f, V6PauseViewLogic.OpButtonsY, 0.001f, "操作ボタン y (ページ座標)");
            Assert.AreEqual(520f, V6PauseViewLogic.OpGroupW, 0.001f, "Op グループ幅");
            Assert.AreEqual(280f, V6PauseViewLogic.RetireButtonW, 0.001f, "選曲へ 幅");
            Assert.AreEqual(220f, V6PauseViewLogic.RetryButtonW, 0.001f, "リトライ 幅");
            Assert.AreEqual(16f, V6PauseViewLogic.OpGroupGap, 0.001f, "グループ内 gap");
            Assert.LessOrEqual(
                V6PauseViewLogic.RetireButtonW + V6PauseViewLogic.OpGroupGap + V6PauseViewLogic.RetryButtonW,
                V6PauseViewLogic.OpGroupW, "子ボタンがグループ幅 520 に収まらない");

            // Op · 再開: ページ (1438,896) 266×56。
            Assert.AreEqual(1438f, V6PauseViewLogic.ResumeButtonX, 0.001f, "再開 x (ページ座標)");
            Assert.AreEqual(266f, V6PauseViewLogic.ResumeButtonW, 0.001f, "再開 幅");

            // ボタンどうしは重ならない
            // (選曲へ右端 1186 < リトライ左端 1202 < リトライ右端 1422 < 再開左端 1438)。
            float retryLeft = V6PauseViewLogic.OpGroupX + V6PauseViewLogic.RetireButtonW
                + V6PauseViewLogic.OpGroupGap;
            float retryRight = retryLeft + V6PauseViewLogic.RetryButtonW;
            Assert.Less(retryRight, V6PauseViewLogic.ResumeButtonX, "リトライと再開が重なる");
            Assert.LessOrEqual(V6PauseViewLogic.OpGroupX + V6PauseViewLogic.OpGroupW,
                V6PauseViewLogic.ResumeButtonX, "Op グループと再開が重なる");
        }

        [Test]
        public void FooterLegendOrigin_MatchesDesign()
        {
            // design Zone · キー操作 (フッター内 (0,30)) → ページ (24,906)。
            // 全画面共通の「Zone · 凡例 (左下)」(フッター内 (445,30) 240×36) は
            // 中身が空の注記ゾーン (契約 §4 / uDNC0-pause.png の描画 / History の
            // 実装コメント) で、凡例項目はここに入らない。
            Assert.AreEqual(24f, V6PauseViewLogic.LegendX, 0.001f, "凡例 x (ページ座標)");
            Assert.AreEqual(906f, V6PauseViewLogic.LegendY, 0.001f, "凡例 y (ページ座標)");

            // 凡例の右端が操作ボタン群 (ページ x906) の手前に収まる
            // (V6Legend.Item の実測に使う幅の概算。x445 から始めると重なる)。
            float width = LegendWidthEstimate();
            Assert.LessOrEqual(V6PauseViewLogic.LegendX + width, V6PauseViewLogic.OpGroupX,
                $"凡例 (幅概算 {width}px) が操作ボタンに重なる");
        }

        [Test]
        public void PreviewKeyRows_MatchDesign()
        {
            // design プレビュー カード内の JIS キー一覧 (34×40):
            // 段ごとの上端 y = 356/400/444/488、段の x オフセット = 8/25/42/59
            // (x 間隔 36)、各段のキー数 = 13/12/12/11。
            Assert.AreEqual(new[] { 356f, 400f, 444f, 488f }, V6PauseViewLogic.JisKeyRowY,
                "キーキャップ段 y (カード内)");
            Assert.AreEqual(new[] { 8f, 25f, 42f, 59f }, V6PauseViewLogic.JisKeyRowX,
                "キーキャップ段 x (カード内)");
            Assert.AreEqual(new[] { 13, 12, 12, 11 },
                new[]
                {
                    V6PauseViewLogic.JisKeyRows[0].Length,
                    V6PauseViewLogic.JisKeyRows[1].Length,
                    V6PauseViewLogic.JisKeyRows[2].Length,
                    V6PauseViewLogic.JisKeyRows[3].Length,
                }, "各段のキー数");
        }

        /// <summary>
        /// 凡例 4 項目 (↑↓ 選択 / ←→ 変更 / Tab タブ切替 / Esc 再開) の幅概算。
        /// V6Legend.Item と同じ式 (KeyCap = ceil(em×fs) + pad×2、項内 gap8、
        /// 項間 gap24、fs22) を V6Ui.LabelAdvanceEm で計算する。
        /// </summary>
        private static float LegendWidthEstimate()
        {
            var items = new[]
            {
                new[] { "↑↓", "選択" },
                new[] { "←→", "変更" },
                new[] { "Tab", "タブ切替" },
                new[] { "Esc", "再開" },
            };

            float width = 0f;
            for (int i = 0; i < items.Length; i++)
            {
                float cap = Mathf.Ceil(V6Ui.LabelAdvanceEm(items[i][0]) * V6Legend.FontSize)
                    + V6Legend.CapPadX * 2f;
                float label = Mathf.Ceil(V6Ui.LabelAdvanceEm(items[i][1]) * V6Legend.FontSize);
                width += cap + V6Legend.InnerGap + label;
                if (i < items.Length - 1)
                {
                    width += V6Legend.ItemGap;
                }
            }

            return width;
        }
    }
}
