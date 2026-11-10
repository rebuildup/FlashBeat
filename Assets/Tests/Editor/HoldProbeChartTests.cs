using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;

namespace FlashBeat.Tests.Editor
{
    // Issue #278: HOLD (ロングノーツ) 入り譜面の実機検証 (Issue #59 の残り受入条件)。
    //
    // ここに固定する内容:
    //   1. 差し込む譜面 JSON が **クラウド譜面の封筒** の形をしている
    //      (NotesManager.ParseChart は固定済み JSON を CloudCatalog.ParseChartResponse
    //      に通すので、素の Data JSON だと読めない)。
    //   2. HOLD (type = 2) が endNum 付きで入っており、保持時間が意図した
    //      秒数になる (LongNote.EndSeconds の式)。
    //   3. PlaySessionSnapshot.ChartJsonOverride による差し替えが、
    //      **既存挙動を一切変えない** 前提下で効くこと。
    public class HoldProbeChartTests
    {
        [SetUp]
        public void SetUp()
        {
            PlaySessionSnapshot.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            PlaySessionSnapshot.Clear();
        }

        static SongData LocalSong()
        {
            return new SongData(1, "ツイッターランド", "作曲者", "e_qQEU_uGjw", 120, 5, 130, 44f);
        }

        // --- 譜面 JSON の形 ------------------------------------------------------

        [Test]
        public void ProbeChart_ParsesAsAChartResponse()
        {
            Data chart = CloudCatalog.ParseChartResponse(HoldProbeChart.Json);

            Assert.IsNotNull(chart, "封筒つきの JSON として読めない");
            Assert.AreEqual(HoldProbeChart.ChartName, chart.name);
            Assert.AreEqual(HoldProbeChart.Bpm, chart.BPM);
            Assert.AreEqual(HoldProbeChart.MaxBlock, chart.maxBlock);
            Assert.IsNotNull(chart.notes);
            Assert.AreEqual(HoldProbeChart.TotalNotes, chart.notes.Length);
        }

        [Test]
        public void ProbeChart_HasTheExpectedTapAndHoldCounts()
        {
            Data chart = CloudCatalog.ParseChartResponse(HoldProbeChart.Json);
            Assert.IsNotNull(chart);

            int taps = 0;
            int holds = 0;
            for (int i = 0; i < chart.notes.Length; i++)
            {
                Note note = chart.notes[i];
                if (note.type == LongNote.TYPE_HOLD)
                {
                    holds++;
                    Assert.Greater(note.endNum, note.num, "HOLD には終端 (endNum) が必要");
                }
                else if (note.type == LongNote.TYPE_TAP)
                {
                    taps++;
                }
                Assert.AreEqual(1, note.LPB, "LPB 1 前提 (num = 0.5 秒)");
                Assert.That(note.block, Is.InRange(0, 7), "レーンは 0..7");
            }

            Assert.AreEqual(HoldProbeChart.TapCount, taps);
            Assert.AreEqual(HoldProbeChart.HoldCount, holds);
        }

        [Test]
        public void ProbeChart_HoldsAreOnTheExpectedLanes()
        {
            Data chart = CloudCatalog.ParseChartResponse(HoldProbeChart.Json);
            Assert.IsNotNull(chart);

            var holdLanes = new System.Collections.Generic.List<int>();
            for (int i = 0; i < chart.notes.Length; i++)
            {
                if (chart.notes[i].type == LongNote.TYPE_HOLD) { holdLanes.Add(chart.notes[i].block); }
            }

            Assert.AreEqual(HoldProbeChart.HoldLanes.Length, holdLanes.Count);
            for (int i = 0; i < holdLanes.Count; i++)
            {
                Assert.AreEqual(HoldProbeChart.HoldLanes[i], holdLanes[i]);
            }
        }

        [Test]
        public void ProbeChart_HoldTimesMatchTheIntendedSeconds()
        {
            Data chart = CloudCatalog.ParseChartResponse(HoldProbeChart.Json);
            Assert.IsNotNull(chart);

            float[] expectedHeads = { 8f, 20f, 32f };
            float[] expectedEnds = { 13f, 24f, 35f };
            int found = 0;
            for (int i = 0; i < chart.notes.Length; i++)
            {
                Note note = chart.notes[i];
                if (note.type != LongNote.TYPE_HOLD) { continue; }
                Assert.Less(found, expectedHeads.Length);
                Assert.AreEqual(expectedHeads[found], NotesManager.NoteTimeSeconds(chart.BPM, note), 0.01f);
                Assert.AreEqual(expectedEnds[found], LongNote.EndSeconds(chart.BPM, note), 0.01f);
                found++;
            }
            Assert.AreEqual(expectedHeads.Length, found);
        }

        [Test]
        public void ProbeChart_IsNotAProductChart()
        {
            // カタログにも Resources にも入れていない (差し替えだけが使う)。
            Assert.IsNull(Resources.Load<TextAsset>(HoldProbeChart.ChartName),
                "Resources に HOLD _probe の譜面进来了らカタログ整合が壊れる");
        }

        // --- 差し替え (ChartJsonOverride) ----------------------------------------

        [Test]
        public void Override_Null_KeepsTheExistingBehaviour()
        {
            PlaySessionSnapshot.ChartJsonOverride = null;

            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(LocalSong(), 0, null);

            Assert.IsNotNull(session);
            Assert.IsNull(session.ChartJson, "差し替えが無いときは固定 JSON を持たない (従来どおり)");
            Assert.IsFalse(PlaySessionSnapshot.TryGetChartJson(session.Song, out _));
        }

        [Test]
        public void Override_Set_SubstitutesTheChartForALocalSong()
        {
            PlaySessionSnapshot.ChartJsonOverride = HoldProbeChart.Json;

            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(LocalSong(), 0, null);

            Assert.IsNotNull(session);
            Assert.AreEqual(HoldProbeChart.Json, session.ChartJson);
            Assert.IsTrue(PlaySessionSnapshot.TryGetChartJson(session.Song, out string json));
            Assert.AreEqual(HoldProbeChart.Json, json);

            // NotesManager.ParseChart はこの JSON を ParseChartResponse に通す。
            Data chart = CloudCatalog.ParseChartResponse(json);
            Assert.IsNotNull(chart, "差し込んだ JSON が読めない");
            Assert.AreEqual(HoldProbeChart.TotalNotes, chart.notes.Length);
        }

        [Test]
        public void Override_ReplacesAnExplicitChartJson()
        {
            // 差し替えは **リモート取得済みの譜面より優先** する。
            // (macOS 実機 run 1: 差し替えが負けたため商品譜面が再生され、
            //  検証譜面ではなくカタログの譜面が実測されていた)
            const string explicitChart = "{\"songId\":\"who\",\"chartVersion\":1,\"chart\":{" +
                                        "\"name\":\"Who\",\"maxBlock\":8,\"BPM\":128,\"offset\":0," +
                                        "\"notes\":[{\"type\":1,\"num\":0,\"block\":3,\"LPB\":4,\"endNum\":0}]}}";
            PlaySessionSnapshot.ChartJsonOverride = HoldProbeChart.Json;

            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(LocalSong(), 1, explicitChart);

            Assert.IsNotNull(session);
            Assert.AreEqual(HoldProbeChart.Json, session.ChartJson,
                "差し替えは取得済みの譜面より優先する (差し替えが負けると検証谱面が再生されない)");
        }

        [Test]
        public void Clear_ResetsTheChartJsonOverride()
        {
            PlaySessionSnapshot.ChartJsonOverride = HoldProbeChart.Json;

            PlaySessionSnapshot.Clear();

            Assert.IsNull(PlaySessionSnapshot.ChartJsonOverride,
                "セッションを破棄したら差し替えも残さない (次のプレイへ持ち越さない)");
        }

        [Test]
        public void Override_SurvivesRepeatedBeginCalls()
        {
            // リトライ (ResetSession → Begin) や遷移中の再固定で差し替えが
            // 失われると、譜面が Resources 側に戻ってしまう。
            PlaySessionSnapshot.ChartJsonOverride = HoldProbeChart.Json;

            PlaySessionSnapshot first = PlaySessionSnapshot.Begin(LocalSong(), 0, null);
            PlaySessionSnapshot second = PlaySessionSnapshot.Begin(LocalSong(), 0, null);

            Assert.AreEqual(HoldProbeChart.Json, first.ChartJson);
            Assert.AreEqual(HoldProbeChart.Json, second.ChartJson);
        }
    }
}
