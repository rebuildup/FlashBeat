using FlashBeat.UI.V6.Game.Hud;
using FlashBeat.UI.V6.Game.Playfield;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests
{
    /// <summary>
    /// 2026-11-12 のオーナー指摘に対応する表示ロジックのテスト。
    ///
    /// - 判定は判定線の近くから立ち上がる (叩いた手応えを判定線の位置で返す)。
    /// - コンボもスコアと同じ時間割でポップする。
    /// - 歌詞スロットは文字に合わせて大きさが決まる (箱だけ大きくしない)。
    /// </summary>
    public class HudFeedbackTests
    {
        // --- 判定ポップアップ (Issue #432) ---

        [Test]
        public void VerdictPopupStartsAtTheJudgeLineAndBouncesUp()
        {
            const float drop = 72f;
            // 出た瞬間は判定線の位置 (静止位置より drop だけ下)。
            Assert.AreEqual(drop, V6HudLogic.VerdictPopupOffset(0f, drop), 0.001f);

            // 途中で静止位置 (0) を通過する。
            Assert.AreEqual(0f, V6HudLogic.VerdictPopupOffset(V6HudLogic.VerdictPopupSeconds * 0.5f, drop), 0.05f);

            // 行き過ぎて上 (負) へ出てから、最後は静止位置へ戻る。
            float min = float.MaxValue;
            for (float t = 0f; t <= V6HudLogic.VerdictPopupSeconds; t += 0.005f)
            {
                min = Mathf.Min(min, V6HudLogic.VerdictPopupOffset(t, drop));
            }
            Assert.Less(min, 0f, "静止位置を行き過ぎて上へ弾ける");

            Assert.AreEqual(0f, V6HudLogic.VerdictPopupOffset(V6HudLogic.VerdictPopupSeconds, drop), 0.001f);
            Assert.AreEqual(0f, V6HudLogic.VerdictPopupOffset(1f, drop), 0.001f);
        }

        // --- ポップ・パンチ (Issue #434) ---

        [Test]
        public void PunchScalePopsUpAndSettles()
        {
            Assert.AreEqual(1f, V6HudLogic.PunchScale(-1f, 1.1f, 0.05f, 0.2f), 0.001f);
            Assert.Greater(V6HudLogic.PunchScale(0.02f, 1.1f, 0.05f, 0.2f), 1f, "立ち上がりで拡大する");
            Assert.AreEqual(1f, V6HudLogic.PunchScale(0.2f, 1.1f, 0.05f, 0.2f), 0.001f,
                "全体時間を過ぎたら等倍へ戻る");
            Assert.AreEqual(1f, V6HudLogic.PunchScale(1f, 1.1f, 0.05f, 0.2f), 0.001f);
        }

        [Test]
        public void ApproachConvergesToTarget()
        {
            float v = 0f;
            for (int i = 0; i < 120; i++)
            {
                v = V6HudLogic.Approach(v, 100f, 1f / 60f, 10f);
            }

            Assert.AreEqual(100f, v, 0.5f, "指数 tween は目標へ収束する");
        }

        [Test]
        public void VerdictFadesOutAtTheEndOfItsLife()
        {
            Assert.AreEqual(1f, V6HudLogic.VerdictAlpha(10f), 0.001f, "寿命の前半は不透明");
            Assert.AreEqual(0.5f, V6HudLogic.VerdictAlpha(V6HudLogic.VerdictFadeSeconds * 0.5f), 0.01f);
            Assert.AreEqual(0f, V6HudLogic.VerdictAlpha(0f), 0.001f);
            Assert.AreEqual(0f, V6HudLogic.VerdictAlpha(-1f), 0.001f);
        }

        // --- コンボのポップ (Issue #434: attack を短くして遅れて見えないように) ---

        [Test]
        public void ComboPopAttacksFasterThanScore()
        {
            Assert.Less(V6HudLogic.ComboPopAttack, V6HudLogic.ScorePopAttack,
                "コンボの立ち上がりはスコアより速い");
            Assert.AreEqual(1f, V6HudLogic.PunchScale(V6HudLogic.ComboPopTotal,
                V6HudLogic.ComboPopPeak, V6HudLogic.ComboPopAttack, V6HudLogic.ComboPopTotal), 0.001f);
        }

        // --- 歌詞スロット (Issue #433) ---

        [Test]
        public void LyricBoxIsSizedToItsTextNotToADesignBlanket()
        {
            // design の 220 (3 行構成) より、本文 1 行 + Start Prompt の高さが小さい。
            float boxHeight = V6HudLogic.LyricContentHeight(48f);

            Assert.Less(boxHeight, V6HudLogic.LyricDesignHeight,
                "箱は design の空白を含めた高さより小さく、文字に合わせる");
            Assert.Greater(boxHeight, 48f, "歌詞 1 行は必ず収まる");
        }

        [Test]
        public void LyricBoxKeepsRoomForTheLyricRowAndPrompt()
        {
            // Issue #433: 歌詞本文 1 行 + Start Prompt (KeyCap 36) が収まる高さ。
            float height = V6HudLogic.LyricContentHeight(48f);
            float lyricLine = 48f * 1.2f;
            float expected = Mathf.Max(lyricLine, V6HudLogic.ReadyPromptCapHeight)
                + V6HudLogic.LyricPaddingY * 2f;

            Assert.AreEqual(expected, height, 0.001f);
            Assert.GreaterOrEqual(height, V6HudLogic.ReadyPromptCapHeight + V6HudLogic.LyricPaddingY * 2f,
                "Start Prompt (KeyCap) が収まる");
        }

        [Test]
        public void LyricWidthIsTheFrameColumnSpan()
        {
            // Issue #433: 幅は共有フレームのメイン領域 6 列グリッド中央 4 列
            // = 1120 を 1:1 で使う (高さ合わせで拡大しない)。
            var size = V6HudLogic.LyricSize(48f);

            Assert.AreEqual(V6HudLogic.LyricDesignWidth, size.x, 0.01f);
        }

        [Test]
        public void LargerTextMakesABiggerBox()
        {
            float small = V6HudLogic.LyricContentHeight(36f);
            float large = V6HudLogic.LyricContentHeight(48f);

            Assert.Greater(large, small, "フォントが大きければ箱も大きくなる");
        }
    }
}
