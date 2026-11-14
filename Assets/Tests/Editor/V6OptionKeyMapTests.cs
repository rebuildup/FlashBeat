using FlashBeat.UI.V6.Option;
using NUnit.Framework;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #375: Option 画面のキー入力 → アクション写像
    /// (<see cref="V6OptionLogic.MapKey"/>) を EditMode で固定する。
    ///
    /// 検証すること:
    /// - 上下キーが行移動 (MoveUp / MoveDown)、左右キーが選択行の値調整
    ///   (AdjustPrev / AdjustNext) に写像されること。
    /// - Enter / Space が Confirm、Esc が Cancel、Tab / Shift+Tab がタブ送りに
    ///   写像されること。
    /// - 確認クリアモーダル中は Confirm / Cancel だけが通り、移動・調整・タブ
    ///   送りが遮断されること。
    /// </summary>
    public class V6OptionKeyMapTests
    {
        private static V6OptionInput Keys(bool modal = false, bool up = false, bool down = false,
            bool left = false, bool right = false, bool confirm = false, bool cancel = false,
            bool tabNext = false, bool tabPrev = false)
        {
            return new V6OptionInput
            {
                ModalOpen = modal,
                Up = up,
                Down = down,
                Left = left,
                Right = right,
                Confirm = confirm,
                Cancel = cancel,
                TabNext = tabNext,
                TabPrev = tabPrev,
            };
        }

        [Test]
        public void ArrowKeysMapToRowMoveAndValueAdjust()
        {
            Assert.AreEqual(V6OptionAction.MoveUp, V6OptionLogic.MapKey(Keys(up: true)));
            Assert.AreEqual(V6OptionAction.MoveDown, V6OptionLogic.MapKey(Keys(down: true)));
            Assert.AreEqual(V6OptionAction.AdjustNext, V6OptionLogic.MapKey(Keys(right: true)));
            Assert.AreEqual(V6OptionAction.AdjustPrev, V6OptionLogic.MapKey(Keys(left: true)));
        }

        [Test]
        public void ConfirmCancelAndTabMapToTheirActions()
        {
            Assert.AreEqual(V6OptionAction.Confirm, V6OptionLogic.MapKey(Keys(confirm: true)));
            Assert.AreEqual(V6OptionAction.Cancel, V6OptionLogic.MapKey(Keys(cancel: true)));
            Assert.AreEqual(V6OptionAction.NextTab, V6OptionLogic.MapKey(Keys(tabNext: true)));
            Assert.AreEqual(V6OptionAction.PrevTab, V6OptionLogic.MapKey(Keys(tabPrev: true)));
        }

        [Test]
        public void NoKeyMapsToNone()
        {
            Assert.AreEqual(V6OptionAction.None, V6OptionLogic.MapKey(Keys()));
        }

        [Test]
        public void ModalOpenOnlyAllowsConfirmAndCancel()
        {
            Assert.AreEqual(V6OptionAction.Confirm, V6OptionLogic.MapKey(Keys(modal: true, confirm: true)));
            Assert.AreEqual(V6OptionAction.Cancel, V6OptionLogic.MapKey(Keys(modal: true, cancel: true)));
            Assert.AreEqual(V6OptionAction.None, V6OptionLogic.MapKey(Keys(modal: true, up: true)));
            Assert.AreEqual(V6OptionAction.None, V6OptionLogic.MapKey(Keys(modal: true, right: true)));
            Assert.AreEqual(V6OptionAction.None, V6OptionLogic.MapKey(Keys(modal: true, tabNext: true)));
        }

        [Test]
        public void PriorityOrderIsMoveThenAdjustThenConfirm()
        {
            // 複数キー同時押しは上→下→右→左→確定の順で最初のものが勝つ。
            Assert.AreEqual(V6OptionAction.MoveUp,
                V6OptionLogic.MapKey(Keys(up: true, down: true)));
            Assert.AreEqual(V6OptionAction.AdjustNext,
                V6OptionLogic.MapKey(Keys(right: true, confirm: true)));
        }
    }
}
