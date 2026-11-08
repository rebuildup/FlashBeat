using NUnit.Framework;
using FlashBeat.UI.V6.Game.Pause;

namespace FlashBeat.V6HudTests
{
    /// <summary>
    /// v6 ポーズの実データ結線 (Issue #224) の EditMode テスト。
    /// Data タブに追加した演奏状況行 (スコア / コンボ / 判定 / 進捗) の
    /// 表示式と、統合フラグ (V6PauseView.Enabled) の既定値を検証する。
    /// ビュー (uGUI 生成) は含まない。
    /// </summary>
    public class V6PauseRuntimeTests
    {
        // --- 統合フラグ (V6UiMode との連動は統合担当が行う。既定は v6 側を優先) ---

        [Test]
        public void Enabled_DefaultsToTrue()
        {
            Assert.IsTrue(V6PauseView.Enabled);
        }

        // --- Data タブの演奏状況行 (実データの表示式) ---

        [Test]
        public void FormatJudgements_JoinsFourCounters()
        {
            Assert.AreEqual("P12 G3 B0 M45",
                V6PauseViewLogic.FormatJudgements(12, 3, 0, 45));
        }

        [Test]
        public void FormatProgress_ShowsClearedOverTotal()
        {
            Assert.AreEqual("12 / 345", V6PauseViewLogic.FormatProgress(12, 345));
        }

        [Test]
        public void FormatProgress_ZeroNotes_ShowsZeroOverZero()
        {
            Assert.AreEqual("0 / 0", V6PauseViewLogic.FormatProgress(0, 0));
        }
    }
}
