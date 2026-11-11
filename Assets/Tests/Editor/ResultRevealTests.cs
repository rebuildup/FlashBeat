using NUnit.Framework;
using FlashBeat.UI.V6.Result;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #384: Result の段階表示の時間割、カウントアップ、登録の状態機械。
    /// 実機の見え方 (点滅カーソル / IME 入力 / 登録チップ) は Mac 受入で確認する。
    /// </summary>
    public class ResultRevealTests
    {
        [Test]
        public void Alpha_IsZeroBeforeTheStageAndOneAfter()
        {
            Assert.AreEqual(0f, V6ResultRevealLogic.Alpha(0.1f, 0.3f, 0.12f), 1e-4f);
            Assert.AreEqual(1f, V6ResultRevealLogic.Alpha(0.5f, 0.3f, 0.12f), 1e-4f);
        }

        [Test]
        public void Alpha_RisesMonotonicallyAndEasesOut()
        {
            float previous = -1f;
            for (float t = 0.3f; t <= 0.42f; t += 0.02f)
            {
                float alpha = V6ResultRevealLogic.Alpha(t, 0.3f, 0.12f);
                Assert.GreaterOrEqual(alpha, previous);
                previous = alpha;
            }

            // EaseOut は中間点で半分より先に進む。
            Assert.Greater(V6ResultRevealLogic.Alpha(0.36f, 0.3f, 0.12f), 0.5f);
        }

        [Test]
        public void TrophyAndGradeScales_ReachOne()
        {
            Assert.AreEqual(V6ResultRevealLogic.TrophyScaleFrom,
                V6ResultRevealLogic.TrophyScale(0f), 1e-4f);
            Assert.AreEqual(1f, V6ResultRevealLogic.TrophyScale(1f), 1e-4f);
            Assert.AreEqual(V6ResultRevealLogic.GradeScaleFrom,
                V6ResultRevealLogic.GradeScale(V6ResultRevealLogic.GradeStart), 1e-4f);
            Assert.AreEqual(1f, V6ResultRevealLogic.GradeScale(1f), 1e-4f);
        }

        [Test]
        public void CountedScore_StartsAtZeroAndEndsAtTheFinalValue()
        {
            Assert.AreEqual(0, V6ResultRevealLogic.CountedScore(988513, 0f));
            Assert.AreEqual(0, V6ResultRevealLogic.CountedScore(988513, V6ResultRevealLogic.ScoreCountStart));
            Assert.AreEqual(988513, V6ResultRevealLogic.CountedScore(988513, 1.2f));
            Assert.AreEqual(988513, V6ResultRevealLogic.CountedScore(988513, 5f));
        }

        [Test]
        public void CountedScore_IsMonotonicDuringTheCount()
        {
            int previous = -1;
            for (float t = V6ResultRevealLogic.ScoreCountStart;
                t <= V6ResultRevealLogic.ScoreCountStart + V6ResultRevealLogic.ScoreCountDuration;
                t += 0.05f)
            {
                int value = V6ResultRevealLogic.CountedScore(1000000, t);
                Assert.GreaterOrEqual(value, previous);
                previous = value;
            }
        }

        [Test]
        public void Judgements_AreStaggeredBySixtyMilliseconds()
        {
            float t = V6ResultRevealLogic.JudgementStart + 0.03f;
            Assert.Greater(V6ResultRevealLogic.JudgementAlpha(t, 0), 0f);
            Assert.AreEqual(0f, V6ResultRevealLogic.JudgementAlpha(t, 1), 1e-4f);
            Assert.AreEqual(0f, V6ResultRevealLogic.JudgementAlpha(t, 4), 1e-4f);

            float last = V6ResultRevealLogic.JudgementStart
                + V6ResultRevealLogic.JudgementStep * (V6ResultRevealLogic.JudgementColumns - 1)
                + V6ResultRevealLogic.JudgementDuration;
            Assert.AreEqual(1f, V6ResultRevealLogic.JudgementAlpha(last, 4), 1e-4f);
        }

        [Test]
        public void Gauge_IsEmptyUntilItStartsAndFullAfter()
        {
            Assert.AreEqual(0f, V6ResultRevealLogic.GaugeProgress(1.4f), 1e-4f);
            Assert.AreEqual(1f, V6ResultRevealLogic.GaugeProgress(1.95f), 1e-4f);
        }

        [Test]
        public void RegisterStage_OpensAtTwoSecondsAndCompletesAfter()
        {
            Assert.IsFalse(V6ResultRevealLogic.ShouldShowRegister(1.99f));
            Assert.IsTrue(V6ResultRevealLogic.ShouldShowRegister(2f));
            Assert.IsFalse(V6ResultRevealLogic.IsComplete(V6ResultRevealLogic.TotalSeconds - 0.01f));
            Assert.IsTrue(V6ResultRevealLogic.IsComplete(V6ResultRevealLogic.TotalSeconds));
        }
    }

    /// <summary>登録送信の二重送信防止 (Issue #384)。</summary>
    public class RegistrationGateTests
    {
        [Test]
        public void Begin_StartsOnceUntilTheCallerFinishes()
        {
            var gate = new V6RegistrationGate();
            Assert.IsTrue(gate.Begin());
            Assert.IsTrue(gate.InFlight);
            Assert.IsFalse(gate.CanStart);
            Assert.IsFalse(gate.Begin());
        }

        [Test]
        public void Succeed_BlocksFurtherSubmissions()
        {
            var gate = new V6RegistrationGate();
            gate.Begin();
            gate.Succeed();

            Assert.IsTrue(gate.Succeeded);
            Assert.IsFalse(gate.InFlight);
            Assert.IsFalse(gate.CanStart);
            Assert.IsFalse(gate.Begin());
        }

        [Test]
        public void Fail_AllowsRetry()
        {
            var gate = new V6RegistrationGate();
            gate.Begin();
            gate.Fail();

            Assert.IsTrue(gate.Failed);
            Assert.IsFalse(gate.Succeeded);
            Assert.IsTrue(gate.CanStart);
            Assert.IsTrue(gate.Begin());
            Assert.IsFalse(gate.Failed);
        }
    }
}
