using NUnit.Framework;
using FlashBeat.Gameplay;
using FlashBeat.Tests.Editor;
using NoteEditor.Model;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #383: 譜面の fadeOut / startTiming / beatOffset の既定値・クランプ・
    /// 範囲外の既定値リセットと警告、フェード計算、および NoteEditor の JSON 往復。
    /// </summary>
    public class ChartTimingTests
    {
        [Test]
        public void Normalize_MissingFadeOut_UsesDefaultTwoSeconds()
        {
            var timing = ChartTiming.Normalize(0f, 0f, 0f, 0f);
            Assert.AreEqual(2f, timing.FadeOut, 1e-4f);
            Assert.AreEqual(0f, timing.StartTiming, 1e-4f);
            Assert.AreEqual(0f, timing.BeatOffset, 1e-4f);
        }

        [Test]
        public void Normalize_ClampsFadeOutToTenSeconds()
        {
            Assert.AreEqual(10f, ChartTiming.Normalize(99f, 0f, 0f, 0f).FadeOut, 1e-4f);
        }

        [Test]
        public void Normalize_ClampsFadeOutToQuarterOfMediaDuration()
        {
            // 20 秒の曲では fadeOut は 5 秒まで。
            Assert.AreEqual(5f, ChartTiming.Normalize(8f, 0f, 0f, 20f).FadeOut, 1e-4f);
        }

        [Test]
        public void Normalize_KeepsStartTimingAndBeatOffsetAtTheRangeEdges()
        {
            // 範囲の端は有効な値 (design 決定 2026-11-11 §6.1)。
            string warning;
            var timing = ChartTiming.Normalize(0f, 10f, -2f, 0f, out warning);
            Assert.AreEqual(10f, timing.StartTiming, 1e-4f);
            Assert.AreEqual(-2f, timing.BeatOffset, 1e-4f);
            Assert.IsNull(warning, "範囲内の値は警告しない");
        }

        [Test]
        public void Normalize_ResetsOutOfRangeStartTimingToDefaultAndWarns()
        {
            string warning;
            var timing = ChartTiming.Normalize(0f, 99f, 0f, 0f, out warning);
            Assert.AreEqual(0f, timing.StartTiming, 1e-4f, "範囲外は既定値へ戻す (クランプしない)");
            StringAssert.Contains("startTiming", warning);

            timing = ChartTiming.Normalize(0f, -5f, 0f, 0f, out warning);
            Assert.AreEqual(0f, timing.StartTiming, 1e-4f);
            StringAssert.Contains("startTiming", warning);
        }

        [Test]
        public void Normalize_ResetsOutOfRangeBeatOffsetToDefaultAndWarns()
        {
            string warning;
            var timing = ChartTiming.Normalize(0f, 0f, 2.5f, 0f, out warning);
            Assert.AreEqual(0f, timing.BeatOffset, 1e-4f);
            StringAssert.Contains("beatOffset", warning);

            timing = ChartTiming.Normalize(0f, 0f, -99f, 0f, out warning);
            Assert.AreEqual(0f, timing.BeatOffset, 1e-4f);
            StringAssert.Contains("beatOffset", warning);
        }

        [Test]
        public void Normalize_ReportsEveryOutOfRangeField()
        {
            string warning;
            ChartTiming.Normalize(0f, 99f, 99f, 0f, out warning);
            StringAssert.Contains("startTiming", warning);
            StringAssert.Contains("beatOffset", warning);
        }

        [Test]
        public void Normalize_RejectsNaN()
        {
            string warning;
            var timing = ChartTiming.Normalize(float.NaN, float.PositiveInfinity, float.NaN, float.NaN, out warning);
            Assert.AreEqual(2f, timing.FadeOut, 1e-4f);
            Assert.AreEqual(0f, timing.StartTiming, 1e-4f);
            Assert.AreEqual(0f, timing.BeatOffset, 1e-4f);
            Assert.AreEqual(0f, timing.MediaDuration, 1e-4f);
            Assert.IsNotNull(warning, "NaN / 無限も範囲外として警告する");
        }

        [Test]
        public void WithFallbackEnd_UsesLastNotePlusFadeOutPlusResultDelay()
        {
            // design 決定 2026-11-11 §6.2: 曲長不明の譜面は最終ノート + fadeOut + 0.8 秒を終端とする。
            var timing = ChartTiming.Normalize(0f, 0f, 0f, 0f).WithFallbackEnd(100f);
            float expected = 100f + ChartTiming.DefaultFadeOutSeconds + ChartTiming.ResultDelaySeconds;

            Assert.AreEqual(expected, timing.MediaDuration, 1e-4f);
            Assert.AreEqual(100f + ChartTiming.ResultDelaySeconds, timing.FadeStartTime(), 1e-4f,
                "フェードは最終ノートの 0.8 秒後 (終了表示) から始まる");
            Assert.AreEqual(0f, timing.FadeProgress(100f), 1e-4f, "最終ノートの時点ではまだフェードしない");
            Assert.AreEqual(1f, timing.FadeProgress(expected), 1e-4f);
        }

        [Test]
        public void WithFallbackEnd_KeepsAKnownMediaDuration()
        {
            var timing = ChartTiming.Normalize(2f, 0f, 0f, 100f).WithFallbackEnd(50f);
            Assert.AreEqual(100f, timing.MediaDuration, 1e-4f,
                "曲長が分かる譜面の終端はメディア長のまま");
        }

        [Test]
        public void WithFallbackEnd_EmptyChartEndsAfterTheFade()
        {
            var timing = ChartTiming.Normalize(0f, 0f, 0f, 0f).WithFallbackEnd(0f);
            Assert.AreEqual(ChartTiming.DefaultFadeOutSeconds + ChartTiming.ResultDelaySeconds,
                timing.MediaDuration, 1e-4f, "ノーツが無くても終端は有限 (プレイヤーを閉じ込めない)");
        }

        [Test]
        public void FadeProgress_StartsAtFadeOutBeforeTheEnd()
        {
            var timing = ChartTiming.Normalize(2f, 0f, 0f, 100f);
            Assert.AreEqual(0f, timing.FadeProgress(97.9f), 1e-4f);
            Assert.AreEqual(0.5f, timing.FadeProgress(99f), 1e-4f);
            Assert.AreEqual(1f, timing.FadeProgress(100f), 1e-4f);
            Assert.AreEqual(1f, timing.FadeProgress(120f), 1e-4f);
        }

        [Test]
        public void FadeProgress_WithoutDuration_StaysAtZero()
        {
            var timing = ChartTiming.Normalize(2f, 0f, 0f, 0f);
            Assert.AreEqual(0f, timing.FadeProgress(999f), 1e-4f);
        }
    }

    /// <summary>NoteEditor の保存形式で 3 フィールドが往復すること。</summary>
    public class ChartTimingSerializerTests
    {
        [SetUp]
        public void SetUp()
        {
            SingletonTestHelper.EnsureSingletons();
        }

        [TearDown]
        public void TearDown()
        {
            SingletonTestHelper.TeardownSingletons();
        }

        [Test]
        public void Serialize_IncludesTimingFields()
        {
            EditData.FadeOut.Value = 2.5f;
            EditData.StartTiming.Value = 1.5f;
            EditData.BeatOffset.Value = -0.3f;

            var json = EditDataSerializer.Serialize();
            StringAssert.Contains("\"fadeOut\":2.5", json);
            StringAssert.Contains("\"startTiming\":1.5", json);
            StringAssert.Contains("\"beatOffset\":-0.3", json);
        }

        [Test]
        public void Deserialize_RestoresTimingFields()
        {
            var json = "{\"name\":\"x\",\"maxBlock\":8,\"BPM\":120,\"offset\":0," +
                       "\"fadeOut\":3.0,\"startTiming\":2.0,\"beatOffset\":-0.25," +
                       "\"notes\":[],\"lyrics\":null}";
            EditDataSerializer.Deserialize(json);

            Assert.AreEqual(3f, EditData.FadeOut.Value, 1e-4f);
            Assert.AreEqual(2f, EditData.StartTiming.Value, 1e-4f);
            Assert.AreEqual(-0.25f, EditData.BeatOffset.Value, 1e-4f);
        }

        [Test]
        public void Deserialize_WithoutTimingFields_UsesZero()
        {
            var json = "{\"name\":\"x\",\"maxBlock\":8,\"BPM\":120,\"offset\":0," +
                       "\"notes\":[],\"lyrics\":null}";
            EditDataSerializer.Deserialize(json);

            Assert.AreEqual(0f, EditData.FadeOut.Value, 1e-4f);
            Assert.AreEqual(0f, EditData.StartTiming.Value, 1e-4f);
            Assert.AreEqual(0f, EditData.BeatOffset.Value, 1e-4f);
        }
    }
}
