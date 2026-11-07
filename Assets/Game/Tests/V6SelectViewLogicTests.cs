using NUnit.Framework;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.UI.V6.Select.View.Tests
{
    /// <summary>V6SelectView の純ロジック (#221)。Unity API に依存しない部分のみ。</summary>
    public sealed class V6SelectViewLogicTests
    {
        // --- DesignScale ---

        [Test]
        public void DesignScale_Fhd_ReturnsUniformScale()
        {
            Assert.AreEqual(1920f / 1728f, V6SelectViewLogic.DesignScale(1920, 1080), 0.0001f);
        }

        [Test]
        public void DesignScale_2k_And_Mac_HeightIsBinding()
        {
            // 2560x1440 と 2560x1664 はどちらも高さ基準 (972 * 1.481 < 1728 * 1.481)。
            float expected = 1440f / 972f;
            Assert.AreEqual(expected, V6SelectViewLogic.DesignScale(2560, 1440), 0.0001f);
            Assert.AreEqual(expected, V6SelectViewLogic.DesignScale(2560, 1664), 0.0001f);
        }

        [Test]
        public void DesignScale_InvalidSize_FallsBackToOne()
        {
            Assert.AreEqual(1f, V6SelectViewLogic.DesignScale(0, 1080));
            Assert.AreEqual(1f, V6SelectViewLogic.DesignScale(1920, -1));
        }

        // --- スクロール位置計算 ---

        [Test]
        public void ClampScroll_KeepsWindowInsideRows()
        {
            Assert.AreEqual(0, V6SelectViewLogic.ClampScroll(-3, 26, 9));
            Assert.AreEqual(17, V6SelectViewLogic.ClampScroll(30, 26, 9));
            Assert.AreEqual(5, V6SelectViewLogic.ClampScroll(5, 26, 9));
        }

        [Test]
        public void ClampScroll_FewerRowsThanViewport_IsZero()
        {
            Assert.AreEqual(0, V6SelectViewLogic.ClampScroll(4, 5, 9));
            Assert.AreEqual(0, V6SelectViewLogic.ClampScroll(0, 0, 9));
        }

        [Test]
        public void ScrollToReveal_RevealsSelectedRow()
        {
            // 上へ出ている行は上端へ、下へ出ている行は下端へ。
            Assert.AreEqual(2, V6SelectViewLogic.ScrollToReveal(2, 5, 9));
            Assert.AreEqual(12, V6SelectViewLogic.ScrollToReveal(12, 5, 9));
            Assert.AreEqual(4, V6SelectViewLogic.ScrollToReveal(4, 5, 9));
        }

        // --- ドットインジケータ ---

        [Test]
        public void DotActiveIndex_ClampsToDots()
        {
            Assert.AreEqual(-1, V6SelectViewLogic.DotActiveIndex(0, 0));
            Assert.AreEqual(0, V6SelectViewLogic.DotActiveIndex(-1, 26));
            Assert.AreEqual(25, V6SelectViewLogic.DotActiveIndex(99, 26));
            Assert.AreEqual(4, V6SelectViewLogic.DotActiveIndex(4, 26));
        }

        // --- スコア表示 ---

        [Test]
        public void FormatScore_Unplayed_IsDash()
        {
            Assert.AreEqual("–", V6SelectViewLogic.FormatScore(-1));
        }

        [Test]
        public void FormatScore_GroupsThousands()
        {
            Assert.AreEqual("988,513", V6SelectViewLogic.FormatScore(988513));
            Assert.AreEqual("0", V6SelectViewLogic.FormatScore(0));
        }

        // --- 検索入力 ---

        [Test]
        public void ApplySearchInput_AppendsPrintableChars()
        {
            Assert.AreEqual("abc", V6SelectViewLogic.ApplySearchInput("", "abc"));
            Assert.AreEqual("まに", V6SelectViewLogic.ApplySearchInput("まに", "に"));
        }

        [Test]
        public void ApplySearchInput_BackspaceRemovesOneChar()
        {
            Assert.AreEqual("ま", V6SelectViewLogic.ApplySearchInput("まに", "\b"));
            Assert.AreEqual("", V6SelectViewLogic.ApplySearchInput("", "\b"));
        }

        [Test]
        public void ApplySearchInput_IgnoresControlChars()
        {
            Assert.AreEqual("ab", V6SelectViewLogic.ApplySearchInput("ab", "\n\u007f"));
        }

        [Test]
        public void ApplySearchInput_NullQuery_IsTreatedAsEmpty()
        {
            Assert.AreEqual("x", V6SelectViewLogic.ApplySearchInput(null, "x"));
        }

        // --- キーボード状態遷移 ---

        [Test]
        public void MapKey_ListFocus_MapsNavigation()
        {
            Assert.AreEqual(V6SelectAction.MoveUp, V6SelectViewLogic.MapKey(false, true, false, false, false, false));
            Assert.AreEqual(V6SelectAction.MoveDown, V6SelectViewLogic.MapKey(false, false, true, false, false, false));
            Assert.AreEqual(V6SelectAction.Confirm, V6SelectViewLogic.MapKey(false, false, false, true, false, false));
            Assert.AreEqual(V6SelectAction.Cancel, V6SelectViewLogic.MapKey(false, false, false, false, true, false));
            Assert.AreEqual(V6SelectAction.FocusSearch, V6SelectViewLogic.MapKey(false, false, false, false, false, true));
            Assert.AreEqual(V6SelectAction.None, V6SelectViewLogic.MapKey(false, false, false, false, false, false));
        }

        [Test]
        public void MapKey_SearchFocus_ConsumesConfirmAndCancel()
        {
            // 検索中の Enter/Esc/Tab はフォーカス解除のみ。確定 (Ready) や戻るを同時に発火させない。
            Assert.AreEqual(V6SelectAction.UnfocusSearch, V6SelectViewLogic.MapKey(true, false, false, true, false, false));
            Assert.AreEqual(V6SelectAction.UnfocusSearch, V6SelectViewLogic.MapKey(true, false, false, false, true, false));
            Assert.AreEqual(V6SelectAction.UnfocusSearch, V6SelectViewLogic.MapKey(true, false, false, false, false, true));
            Assert.AreEqual(V6SelectAction.None, V6SelectViewLogic.MapKey(true, true, true, false, false, false));
        }
    }
}
