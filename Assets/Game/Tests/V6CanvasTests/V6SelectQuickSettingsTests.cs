using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.UI.V6.Select.View.Tests
{
    /// <summary>
    /// v6 選曲画面の新純ロジック (設計 v6 契約 §4/§5)。
    /// - FrameOrigin: design frame 1728×972 を実画面に 1:1 で中央配置したときの
    ///   左上原点 ((w-1728)/2, (h-972)/2 の床切り)。引き伸ばしはしない。
    /// - ドットインジケータ: 右端レール x=536、開始 y=136、間隔 4、
    ///   アクティブ 10 / 非アクティブ 8、最下端がリスト高 (780) 内に収まる。
    /// - クイック設定行: 右カラム (幅 560) に 512×56 の行を 68 間隔で 3 行。
    /// </summary>
    public sealed class V6SelectQuickSettingsTests
    {
        private const int FhdW = 1920;
        private const int FhdH = 1080;
        private const int TwoKW = 2560;
        private const int TwoKH = 1440;
        private const int MacW = 2560;
        private const int MacH = 1664;

        // --- フレーム原点 (1:1 中央配置) ---

        [Test]
        public void FrameOrigin_Fhd_MatchesDesignTable()
        {
            V6SelectViewLogic.FrameOrigin(FhdW, FhdH, out int x, out int y);
            Assert.AreEqual(96, x, "FHD origin x = (1920-1728)/2 = 96");
            Assert.AreEqual(54, y, "FHD origin y = (1080-972)/2 = 54");
        }

        [Test]
        public void FrameOrigin_2k_MatchesDesignTable()
        {
            V6SelectViewLogic.FrameOrigin(TwoKW, TwoKH, out int x, out int y);
            Assert.AreEqual(416, x, "2K origin x = (2560-1728)/2 = 416");
            Assert.AreEqual(234, y, "2K origin y = (1440-972)/2 = 234");
        }

        [Test]
        public void FrameOrigin_Mac_MatchesDesignTable()
        {
            V6SelectViewLogic.FrameOrigin(MacW, MacH, out int x, out int y);
            Assert.AreEqual(416, x, "Mac origin x = (2560-1728)/2 = 416");
            Assert.AreEqual(346, y, "Mac origin y = (1664-972)/2 = 346");
        }

        [Test]
        public void FrameOrigin_OddSurplus_FloorsTowardNegativeInfinity()
        {
            // 1921-1728 = 193 → 96.5 → 96 (床切り)。1081-972 = 109 → 54.5 → 54。
            V6SelectViewLogic.FrameOrigin(1921, 1081, out int x, out int y);
            Assert.AreEqual(96, x);
            Assert.AreEqual(54, y);
        }

        [Test]
        public void FrameOrigin_SmallerThanFrame_IsNegative()
        {
            // 画面がフレームより小さいときは負 (はみ出しは許容、引き伸ばしはしない)。
            V6SelectViewLogic.FrameOrigin(1000, 500, out int x, out int y);
            Assert.AreEqual(-364, x);
            Assert.AreEqual(-236, y);
        }

        // --- ドットインジケータ ---

        [Test]
        public void DotOffsets_AreMonotonicallyIncreasingBySizePlusGap()
        {
            var offsets = V6SelectViewLogic.DotOffsets(26);
            Assert.AreEqual(26, offsets.Length);
            for (int i = 1; i < offsets.Length; i++)
            {
                Assert.AreEqual(V6SelectViewLogic.DotSize + V6SelectViewLogic.DotGap,
                    offsets[i] - offsets[i - 1], 0.0001f,
                    $"dot {i} center must sit {V6SelectViewLogic.DotSize + V6SelectViewLogic.DotGap}px below dot {i - 1}");
            }
        }

        [Test]
        public void DotActiveIndex_ClampsToRange()
        {
            Assert.AreEqual(0, V6SelectViewLogic.DotActiveIndex(-1, 26));
            Assert.AreEqual(25, V6SelectViewLogic.DotActiveIndex(26, 26));
            Assert.AreEqual(7, V6SelectViewLogic.DotActiveIndex(7, 26));
            Assert.AreEqual(-1, V6SelectViewLogic.DotActiveIndex(0, 0));
        }

        [Test]
        public void DotBottom_StaysInsideTheListPanel()
        {
            // 契約 §5: リスト高 780、開始 y=136。全曲数 (42 曲 = Hiscore 長) でも収まる。
            int count = 42;
            float bottom = V6SelectViewLogic.DotBottom(count, V6SelectViewLogic.DotY);
            Assert.LessOrEqual(bottom, V6SelectViewLogic.ListH, "最下端のドットがリスト高内に収まらない");
        }

        // --- クイック設定行 (右カラム) ---

        [Test]
        public void SettingsRowRect_ThreeRowsInsideTheRightColumn()
        {
            Assert.AreEqual(3, V6SelectViewLogic.SettingsRowCount);
            for (int i = 0; i < V6SelectViewLogic.SettingsRowCount; i++)
            {
                var rect = V6SelectViewLogic.SettingsRowRect(i);
                Assert.AreEqual(V6SelectViewLogic.SettingsRowW, rect.W, "行幅は契約どおり 512");
                Assert.AreEqual(V6SelectViewLogic.SettingsRowH, rect.H, "行高は契約どおり 56");
                Assert.GreaterOrEqual(rect.X, 0f, "行が右カラムの左にはみ出さない");
                Assert.LessOrEqual(rect.Right, V6SelectViewLogic.ListW, "行が右カラム幅 (560) 内に収まる");
                Assert.LessOrEqual(rect.Bottom, V6SelectViewLogic.ListH, "行がリスト高 (780) 内に収まる");
            }
        }

        [Test]
        public void SettingsRowRect_RowsDoNotOverlap()
        {
            for (int i = 1; i < V6SelectViewLogic.SettingsRowCount; i++)
            {
                var prev = V6SelectViewLogic.SettingsRowRect(i - 1);
                var current = V6SelectViewLogic.SettingsRowRect(i);
                Assert.GreaterOrEqual(current.Y - prev.Bottom, 1f, "設定行どうしが重なる");
            }
        }
    }
}
