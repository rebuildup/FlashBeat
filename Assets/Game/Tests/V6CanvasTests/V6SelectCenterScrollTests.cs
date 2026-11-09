using NUnit.Framework;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.UI.V6.Select.View.Tests
{
    /// <summary>
    /// 選曲リスト「選択中の曲を常に中央に表示する」方式のテスト。
    /// - CenterScroll: スクロール位置は index - visibleRows/2 (クランプしない)。
    ///   リスト先頭 / 末尾ではスクロール値が負 / 範囲外になってもよく、
    ///   選択行のスロット位置 (index - scroll) は常に中央 (visibleRows/2)。
    /// - ただし index が範囲外 (-1 等) または rowCount &lt; visibleRows のときは既存挙動 (範囲内に収める)。
    /// </summary>
    public sealed class V6SelectCenterScrollTests
    {
        private const int VisibleRows = 9;

        // --- CenterScroll: 仕様どおりのスクロール値 ---

        [Test]
        public void CenterScroll_FirstIndex_NegativeScroll_SelectionAtCenter()
        {
            // index 0 でもクランプしない。scroll = -4 で選択行は中央スロット (4) に来る。
            int scroll = V6SelectViewLogic.CenterScroll(0, 30, VisibleRows);
            Assert.AreEqual(-VisibleRows / 2, scroll);
            Assert.AreEqual(VisibleRows / 2, 0 - scroll);
        }

        [Test]
        public void CenterScroll_MiddleIndex_PutsSelectionAtCenter()
        {
            // visibleRows / 2 = 4 行下にスクロールすると、選択行が中央 (5 行目) に来る。
            Assert.AreEqual(10, V6SelectViewLogic.CenterScroll(14, 30, VisibleRows));
        }

        [Test]
        public void CenterScroll_LastIndex_OverflowScroll_SelectionAtCenter()
        {
            // max scroll = 30 - 9 = 21 を超えてもよい。index 29 → scroll 25、選択行は中央スロット。
            int scroll = V6SelectViewLogic.CenterScroll(29, 30, VisibleRows);
            Assert.AreEqual(25, scroll);
            Assert.AreEqual(VisibleRows / 2, 29 - scroll);
        }

        [Test]
        public void CenterScroll_RowCountLessThanVisible_KeepsWithinRange()
        {
            // rowCount < visibleRows のときは既存挙動 (0 に収める)。
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

        /// <summary>有効 index の全範囲で、選択行が常に中央 (visibleRows / 2) に来る。</summary>
        [Test]
        public void CenterScroll_SelectionSitsAtCenter_ForEveryValidIndex()
        {
            int rowCount = 40;
            for (int index = 0; index < rowCount; index++)
            {
                int scroll = V6SelectViewLogic.CenterScroll(index, rowCount, VisibleRows);
                Assert.AreEqual(index - VisibleRows / 2, scroll, "index {0}", index);
                // 選択行のスロット位置は常に中央。
                Assert.AreEqual(VisibleRows / 2, index - scroll, "index {0}", index);
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

            // 下へ 1 つ: 選択 index は +1、スクロールは選択に追従して中央に来る (先頭付近は負値も許す)。
            source.MoveSelection(1);
            int index = SelectedIndex(source);
            Assert.AreEqual(1, index);
            int scroll = V6SelectViewLogic.CenterScroll(index, count, VisibleRows);
            Assert.AreEqual(index - VisibleRows / 2, scroll);
            Assert.AreEqual(VisibleRows / 2, index - scroll);

            // 中央付近でも同様に中央固定。
            source.Select(source.FilteredSongs[VisibleRows / 2].SongId);
            index = SelectedIndex(source);
            Assert.AreEqual(VisibleRows / 2, index);
            Assert.AreEqual(0, V6SelectViewLogic.CenterScroll(index, count, VisibleRows));
        }

        [Test]
        public void MoveSelectionAtEnd_KeepsSelectionAtEnd_AndSelectionAtCenter()
        {
            var source = new FlashBeat.UI.V6.Select.Data.V6SelectDataSource();
            int count = source.FilteredSongs.Count;
            Assume.That(count, Is.GreaterThan(VisibleRows));

            source.Select(source.FilteredSongs[count - 1].SongId);
            source.MoveSelection(1); // 端でさらに下 → 留まる
            int index = SelectedIndex(source);
            Assert.AreEqual(count - 1, index);
            int scroll = V6SelectViewLogic.CenterScroll(index, count, VisibleRows);
            // スクロールは範囲外 (max = count - VisibleRows) を超えてもよく、選択行は中央。
            Assert.AreEqual(index - VisibleRows / 2, scroll);
            Assert.AreEqual(VisibleRows / 2, index - scroll);

            source.MoveSelection(-1);
            index = SelectedIndex(source);
            Assert.AreEqual(count - 2, index);
            scroll = V6SelectViewLogic.CenterScroll(index, count, VisibleRows);
            Assert.AreEqual(index - VisibleRows / 2, scroll);
            Assert.AreEqual(VisibleRows / 2, index - scroll);
        }
    }
}
