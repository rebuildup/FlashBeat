using NUnit.Framework;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.UI.V6.Select.View.Tests
{
    /// <summary>
    /// 選曲リスト「選択中の曲を常に中央に表示する」方式のテスト。
    /// - CenterScroll: index がリスト中央に来るスクロール位置 (端はクランプ)。
    /// - 選択移動 (±1) のあとスクロールが選択に追従すること (端の曲は端に寄る)。
    /// </summary>
    public sealed class V6SelectCenterScrollTests
    {
        private const int VisibleRows = 9;

        // --- CenterScroll: 仕様どおりのスクロール値 ---

        [Test]
        public void CenterScroll_FirstIndex_ClampsToTop()
        {
            Assert.AreEqual(0, V6SelectViewLogic.CenterScroll(0, 30, VisibleRows));
        }

        [Test]
        public void CenterScroll_MiddleIndex_PutsSelectionAtCenter()
        {
            // visibleRows / 2 = 4 行下にスクロールすると、選択行が中央 (5 行目) に来る。
            Assert.AreEqual(10, V6SelectViewLogic.CenterScroll(14, 30, VisibleRows));
        }

        [Test]
        public void CenterScroll_LastIndex_ClampsToBottom()
        {
            // max scroll = 30 - 9 = 21。index 29 だと 29 - 4 = 25 だが 21 に収める。
            Assert.AreEqual(21, V6SelectViewLogic.CenterScroll(29, 30, VisibleRows));
        }

        [Test]
        public void CenterScroll_RowCountLessThanVisible_ReturnsZero()
        {
            Assert.AreEqual(0, V6SelectViewLogic.CenterScroll(0, 5, VisibleRows));
            Assert.AreEqual(0, V6SelectViewLogic.CenterScroll(4, 5, VisibleRows));
        }

        [Test]
        public void CenterScroll_OutOfRangeIndex_FollowsExistingClamp()
        {
            // -1 は先頭扱い、範囲外は末尾扱い (既存 ClampScroll / DotActiveIndex と同じ規約)。
            Assert.AreEqual(0, V6SelectViewLogic.CenterScroll(-1, 30, VisibleRows));
            Assert.AreEqual(21, V6SelectViewLogic.CenterScroll(999, 30, VisibleRows));
            Assert.AreEqual(0, V6SelectViewLogic.CenterScroll(0, 0, VisibleRows));
        }

        /// <summary>クランプ領域以外では、選択行が常に中央 (visibleRows / 2) に来る。</summary>
        [Test]
        public void CenterScroll_SelectionSitsAtCenter_WheneverNotClamped()
        {
            int rowCount = 40;
            for (int index = VisibleRows / 2; index <= rowCount - VisibleRows + VisibleRows / 2; index++)
            {
                int scroll = V6SelectViewLogic.CenterScroll(index, rowCount, VisibleRows);
                Assert.AreEqual(index - VisibleRows / 2, scroll, "index {0}", index);
                Assert.GreaterOrEqual(scroll, 0);
                Assert.LessOrEqual(scroll, rowCount - VisibleRows);
            }
        }

        // --- 選択移動とスクロール追従 (実データソースで合成動作を確認) ---

        private static int SelectedIndex(FlashBeat.UI.V6.Select.IV6SelectDataSource source)
        {
            var selected = source.Selected;
            for (int i = 0; i < source.FilteredSongs.Count; i++)
            {
                if (source.FilteredSongs[i].SongId == selected.SongId)
                {
                    return i;
                }
            }
            return -1;
        }

        [Test]
        public void MoveSelectionThenCenterScroll_SelectionStepsByOneAndScrollFollows()
        {
            var source = new FlashBeat.UI.V6.Select.Data.V6SelectDataSource();
            int count = source.FilteredSongs.Count;
            Assume.That(count, Is.GreaterThanOrEqualTo(VisibleRows + 2));

            source.Select(source.FilteredSongs[0].SongId);

            // 下へ 1 つ: 選択 index は +1、スクロールは選択に追従して中央に来る。
            source.MoveSelection(1);
            int index = SelectedIndex(source);
            Assert.AreEqual(1, index);
            int scroll = V6SelectViewLogic.CenterScroll(index, count, VisibleRows);
            Assert.AreEqual(0, scroll); // index 1 は上端クランプ領域

            // 中央付近まで進むと中央固定になる。
            source.Select(source.FilteredSongs[VisibleRows / 2].SongId);
            index = SelectedIndex(source);
            Assert.AreEqual(VisibleRows / 2, index);
            Assert.AreEqual(0, V6SelectViewLogic.CenterScroll(index, count, VisibleRows));
            Assert.AreEqual(VisibleRows / 2, index - V6SelectViewLogic.CenterScroll(index, count, VisibleRows));
        }

        [Test]
        public void MoveSelectionAtEnd_KeepsSelectionAtEnd_AndClampsScrollToBottom()
        {
            var source = new FlashBeat.UI.V6.Select.Data.V6SelectDataSource();
            int count = source.FilteredSongs.Count;
            Assume.That(count, Is.GreaterThan(VisibleRows));

            source.Select(source.FilteredSongs[count - 1].SongId);
            source.MoveSelection(1); // 端でさらに下 → 留まる
            int index = SelectedIndex(source);
            Assert.AreEqual(count - 1, index);
            Assert.AreEqual(count - VisibleRows, V6SelectViewLogic.CenterScroll(index, count, VisibleRows));

            source.MoveSelection(-1);
            index = SelectedIndex(source);
            Assert.AreEqual(count - 2, index);
            Assert.AreEqual(index - VisibleRows / 2,
                V6SelectViewLogic.CenterScroll(index, count, VisibleRows));
        }
    }
}
