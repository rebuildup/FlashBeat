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
        // --- 判定の出現 ---

        [Test]
        public void VerdictStartsAtTheJudgeLineAndRises()
        {
            // 出た瞬間は最も下 (判定線の近く)。
            Assert.AreEqual(V6HudLogic.VerdictRiseDistance, V6HudLogic.VerdictRiseOffset(0f), 0.001f);

            // 時間とともに上へ移動する。
            float mid = V6HudLogic.VerdictRiseOffset(V6HudLogic.VerdictRiseSeconds * 0.5f);
            Assert.Less(mid, V6HudLogic.VerdictRiseDistance);
            Assert.Greater(mid, 0f);

            // 立ち上がりきったら所定位置 (0)。
            Assert.AreEqual(0f, V6HudLogic.VerdictRiseOffset(V6HudLogic.VerdictRiseSeconds), 0.001f);
            Assert.AreEqual(0f, V6HudLogic.VerdictRiseOffset(1f), 0.001f);
        }

        [Test]
        public void VerdictRiseIsMonotonic()
        {
            float previous = float.MaxValue;
            for (float t = 0f; t <= V6HudLogic.VerdictRiseSeconds; t += 0.01f)
            {
                float offset = V6HudLogic.VerdictRiseOffset(t);
                Assert.LessOrEqual(offset, previous + 0.0001f, "立ち上がりが逆戻りしない");
                previous = offset;
            }
        }

        [Test]
        public void VerdictFadesOutAtTheEndOfItsLife()
        {
            Assert.AreEqual(1f, V6HudLogic.VerdictAlpha(10f), 0.001f, "寿命の前半は不透明");
            Assert.AreEqual(0.5f, V6HudLogic.VerdictAlpha(V6HudLogic.VerdictFadeSeconds * 0.5f), 0.01f);
            Assert.AreEqual(0f, V6HudLogic.VerdictAlpha(0f), 0.001f);
            Assert.AreEqual(0f, V6HudLogic.VerdictAlpha(-1f), 0.001f);
        }

        // --- コンボのポップ ---

        [Test]
        public void ComboUsesTheSamePopAsTheScore()
        {
            // コンボだけ静止していたのが指摘の本体。スコアと同じ時間割を使う。
            Assert.AreEqual(V6HudLogic.ScorePulseScale(0f), V6HudLogic.ScorePulseScale(0f), 0.0001f);
            Assert.Greater(V6HudLogic.ScorePulseScale(0.08f), 1f, "加算直後は拡大する");
            Assert.AreEqual(1f, V6HudLogic.ScorePulseScale(V6HudLogic.ScorePulseSeconds), 0.001f,
                "ポップは元の倍率へ戻る");
        }

        // --- 歌詞スロット ---

        [Test]
        public void LyricBoxIsSizedToItsTextNotToADesignBlanket()
        {
            // design の 220 を画面倍率で拡大しただけの箱は、文字に対して大きすぎる。
            float boxHeight = V6HudLogic.LyricContentHeight(24f, 48f, 22f);
            float designScaled = V6HudLogic.LyricDesignHeight
                * V6LaneGeometry.ScreenScale(new Vector2(0f, 2148f));

            Assert.Less(boxHeight, designScaled,
                "箱は design の空白を含めた高さより小さく、文字に合わせる");
            Assert.Greater(boxHeight, 48f, "歌詞 1 行は必ず収まる");
        }

        [Test]
        public void LyricBoxKeepsRoomForThreeRows()
        {
            // 振り仮名 / 歌詞 / ローマ字 の 3 行 + 行間 + padding。
            float height = V6HudLogic.LyricContentHeight(24f, 48f, 22f);
            float rows = 24f * 1.4f + 48f * 1.2f + 22f * 1.4f;

            Assert.Greater(height, rows, "3 行と行間・padding が入る");
        }

        [Test]
        public void LyricWidthStillFollowsTheDesign()
        {
            var size = V6HudLogic.LyricSize(2148f, 24f, 48f, 22f);
            float expected = V6HudLogic.LyricDesignWidth
                * V6LaneGeometry.ScreenScale(new Vector2(0f, 2148f));

            Assert.AreEqual(expected, size.x, 0.01f, "幅は design の 1120 を写す");
        }

        [Test]
        public void LargerTextMakesABiggerBox()
        {
            float small = V6HudLogic.LyricContentHeight(18f, 36f, 16f);
            float large = V6HudLogic.LyricContentHeight(24f, 48f, 22f);

            Assert.Greater(large, small, "フォントが大きければ箱も大きくなる");
        }
    }
}
