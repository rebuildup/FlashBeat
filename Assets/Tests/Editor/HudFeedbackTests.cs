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

        // --- 桁別ポップ (Issue #445) ---

        [Test]
        public void DigitPopIsShortAndSettles()
        {
            // オーナー指摘「効果が出てから消えるまでが無駄に長い」への対応で、
            // 桁ポップは 0.25 秒以内に必ず終わる。
            Assert.LessOrEqual(V6HudLogic.DigitPopTotal, 0.25f);
            Assert.Less(V6HudLogic.DigitPopAttack, V6HudLogic.DigitPopTotal);
            Assert.AreEqual(1f, V6HudLogic.PunchScale(V6HudLogic.DigitPopTotal,
                V6HudLogic.DigitPopPeak, V6HudLogic.DigitPopAttack, V6HudLogic.DigitPopTotal), 0.001f);
            Assert.Greater(V6HudLogic.PunchScale(0.02f,
                V6HudLogic.DigitPopPeak, V6HudLogic.DigitPopAttack, V6HudLogic.DigitPopTotal), 1f,
                "立ち上がりで拡大する");
        }

        [Test]
        public void VerdictLifeIsShort()
        {
            // Issue #445: 判定ポップアップは弾けてすぐ消える (全体 0.20s、
            // フェードはその末尾 0.10s)。かつては 0.8s 静止していた。
            Assert.LessOrEqual(V6HudLogic.VerdictTotalSeconds, 0.22f);
            Assert.LessOrEqual(V6HudLogic.VerdictFadeSeconds, 0.14f);
            Assert.Greater(V6HudLogic.VerdictPopupSeconds, 0f);
            Assert.Less(V6HudLogic.VerdictPopupSeconds, V6HudLogic.VerdictTotalSeconds,
                "出現モーションは見え時間より短い");
        }

        // --- 桁別アニメーションの差分 (Issue #445) ---

        [Test]
        public void ChangedCharFlags_OnlyChangedDigitsAnimate()
        {
            // オーナー例: 138 → 140 なら下 2 桁だけが動く。
            bool[] flags = V6HudLogic.ChangedCharFlags("138", "140");
            Assert.AreEqual(3, flags.Length);
            Assert.IsFalse(flags[0], "上の桁 (1) は変わらないので動かない");
            Assert.IsTrue(flags[1], "十の位 3→4 は動く");
            Assert.IsTrue(flags[2], "一の位 8→0 は動く");
        }

        [Test]
        public void ChangedCharFlags_SeparatorNeverAnimates()
        {
            // "999" → "1,000": ',' が新しく現れる位置は静的なのでアニメーションしない。
            bool[] flags = V6HudLogic.ChangedCharFlags("999", "1,000");
            Assert.AreEqual(',', "1,000"[1]);
            Assert.IsFalse(flags[1], "新しく現れた ',' はアニメーションしない");
            Assert.IsTrue(flags[0], "1 が現れた桁は動く");
        }

        [Test]
        public void ChangedCharFlags_RemovedCharsDoNotAnimate()
        {
            // 短くなった側 (消えた桁) はアニメーションしない。
            bool[] flags = V6HudLogic.ChangedCharFlags("100", "99");
            Assert.AreEqual(3, flags.Length);
            Assert.IsTrue(flags[0]);
            Assert.IsTrue(flags[1]);
            Assert.IsFalse(flags[2], "消えた桁は動かない");
        }

        [Test]
        public void ChangedCharFlags_SameString_HasNoChanges()
        {
            bool[] flags = V6HudLogic.ChangedCharFlags("123", "123");
            Assert.IsFalse(flags[0] || flags[1] || flags[2]);
        }

        [Test]
        public void ComboValueText_ZeroIsEmpty()
        {
            // Issue #445: コンボ 0 はコンボではないので表示自体を隠す。
            Assert.AreEqual("", V6HudLogic.ComboValueText(0));
            Assert.AreEqual("", V6HudLogic.ComboValueText(-1));
            Assert.AreEqual("1", V6HudLogic.ComboValueText(1));
            Assert.AreEqual("140", V6HudLogic.ComboValueText(140));
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
