using NUnit.Framework;
using FlashBeat.Gameplay;
using FlashBeat.Tests.Editor;
using NoteEditor.Model;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #383: 譜面の fadeOut / startTiming / beatOffset の既定値・クランプ・
    /// フェード計算、および NoteEditor の JSON 往復で失われないこと。
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
        public void Normalize_ClampsStartTimingAndBeatOffset()
        {
            Assert.AreEqual(10f, ChartTiming.Normalize(0f, 99f, 0f, 0f).StartTiming, 1e-4f);
            Assert.AreEqual(0f, ChartTiming.Normalize(0f, -5f, 0f, 0f).StartTiming, 1e-4f);
            Assert.AreEqual(2f, ChartTiming.Normalize(0f, 0f, 99f, 0f).BeatOffset, 1e-4f);
            Assert.AreEqual(-2f, ChartTiming.Normalize(0f, 0f, -99f, 0f).BeatOffset, 1e-4f);
        }

        [Test]
        public void Normalize_RejectsNaN()
        {
            var timing = ChartTiming.Normalize(float.NaN, float.PositiveInfinity, float.NaN, float.NaN);
            Assert.AreEqual(2f, timing.FadeOut, 1e-4f);
            Assert.AreEqual(0f, timing.StartTiming, 1e-4f);
            Assert.AreEqual(0f, timing.BeatOffset, 1e-4f);
            Assert.AreEqual(0f, timing.MediaDuration, 1e-4f);
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
