using NUnit.Framework;
using FlashBeat.UI.V6.Game.Pause;

namespace FlashBeat.V6HudTests
{
    /// <summary>
    /// v6 ポーズの実データ結線 (Issue #224) の EditMode テスト。
    /// Data タブに追加した演奏状況行 (スコア / コンボ / 判定 / 進捗) の
    /// 表示式と、統合フラグ撤去後の不変条件 (Issue #740 Stage B) を検証する。
    /// ビュー (uGUI 生成) は含まない。
    /// </summary>
    public class V6PauseRuntimeTests
    {
        // --- 統合フラグ (Issue #740 Stage B で撤去。v6 ポーズは無条件) ---

        [Test]
        public void EnabledFlag_IsRemoved()
        {
            // V6PauseView.Enabled は Stage B で畳んだ。復活すると
            // 「フラグ off で v6 ポーズが作られない」旧フォールバック経路が
            // 復活するので、フィールドが無いことを固定する。
            Assert.IsNull(typeof(V6PauseView).GetField("Enabled",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static),
                "V6PauseView.Enabled が残っている (Stage B で撤去したはず)。");
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
