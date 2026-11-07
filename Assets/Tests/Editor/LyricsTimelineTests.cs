using System.Collections.Generic;
using FlashBeat.UI;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 歌詞同期 (Issue #30 §2) の純ロジックテスト。
///
/// 歌詞は動画 (VideoPlayer.time) と同じタイムラインで出し入れするため、
/// 固定するのは「どの行をどの時刻に出し始めるか/消すか」だけである。
/// 譜面の歌詞は旧形式の並列配列で、曲によって長さも endTime の有無も違うため、
/// 壊れたデータをどう補正するかも共に固定する。
/// </summary>
public class LyricsTimelineTests
{
    const float Delta = 0.001f;

    [Test]
    public void NoDataProducesNoLines()
    {
        Assert.AreEqual(0, LyricsTimeline.Build(null, null, null, null).Count);
        Assert.AreEqual(0, LyricsTimeline.Build(new float[0], new float[0], new string[0], null).Count);
    }

    [Test]
    public void LineUsesItsOwnEndTime()
    {
        var lines = LyricsTimeline.Build(
            new[] { 1f, 2f },
            new[] { 1.5f, 2.5f },
            new[] { "あ", "い" },
            null);

        Assert.AreEqual(2, lines.Count);
        Assert.AreEqual(1f, lines[0].Start, Delta);
        Assert.AreEqual(1.5f, lines[0].End, Delta);
        Assert.AreEqual(2f, lines[1].Start, Delta);
        Assert.AreEqual(2.5f, lines[1].End, Delta);
        Assert.AreEqual("あ", lines[0].Text);
        Assert.AreEqual("い", lines[1].Text);
    }

    [Test]
    public void MissingEndTimeEndsAtTheNextLine()
    {
        // endTime が無い譜面は、次の行が始まったら前の行を消す
        var lines = LyricsTimeline.Build(
            new[] { 1f, 2f },
            null,
            new[] { "あ", "い" },
            null);

        Assert.AreEqual(2, lines.Count);
        Assert.AreEqual(2f, lines[0].End, Delta);
    }

    [Test]
    public void MissingEndTimeWithoutNextLineUsesTheDefaultDuration()
    {
        var lines = LyricsTimeline.Build(
            new[] { 1f },
            new float[0],
            new[] { "あ" },
            null);

        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual(1f + LyricsTimeline.DefaultLineDuration, lines[0].End, Delta);
    }

    [Test]
    public void EndTimeBeforeStartIsNotUsed()
    {
        // 旧データには endTime が開始より前 (区間が1つずれている) 譜面がある。
        // その値をそのまま使うと歌詞が1フレームも出ないので、使わない。
        var lines = LyricsTimeline.Build(
            new[] { 1f, 2f },
            new[] { 0.5f, 2.5f },
            new[] { "あ", "い" },
            null);

        Assert.AreEqual(2, lines.Count);
        Assert.AreEqual(2f, lines[0].End, Delta);
        Assert.AreEqual(2.5f, lines[1].End, Delta);
    }

    [Test]
    public void DifferentArrayLengthsUseTheShorterOne()
    {
        var lines = LyricsTimeline.Build(
            new[] { 1f, 2f, 3f },
            new[] { 1.5f, 2.5f, 3.5f },
            new[] { "あ" },
            null);

        Assert.AreEqual(1, lines.Count);
    }

    [Test]
    public void FuriganaIsTakenFromItsOwnArray()
    {
        var lines = LyricsTimeline.Build(
            new[] { 1f },
            new[] { 2f },
            new[] { "あ" },
            new[] { "あ" });

        Assert.AreEqual("あ", lines[0].Furigana);
    }

    [Test]
    public void FuriganaIsNullWhenTheArrayIsMissing()
    {
        var lines = LyricsTimeline.Build(
            new[] { 1f },
            new[] { 2f },
            new[] { "あ" },
            null);

        Assert.IsNull(lines[0].Furigana);
    }

    [Test]
    public void NoLineBeforeTheFirstStartTime()
    {
        var lines = LyricsTimeline.Build(new[] { 1f }, new[] { 2f }, new[] { "あ" }, null);

        Assert.AreEqual(-1, LyricsTimeline.IndexAt(lines, 0.5f));
    }

    [Test]
    public void LineIsFoundWhileItIsOnScreen()
    {
        var lines = LyricsTimeline.Build(new[] { 1f }, new[] { 2f }, new[] { "あ" }, null);

        Assert.AreEqual(0, LyricsTimeline.IndexAt(lines, 1.2f));
    }

    [Test]
    public void LineIsOnScreenAtItsStartAndGoneAtItsEnd()
    {
        var lines = LyricsTimeline.Build(new[] { 1f }, new[] { 2f }, new[] { "あ" }, null);

        Assert.AreEqual(0, LyricsTimeline.IndexAt(lines, 1f));
        Assert.AreEqual(-1, LyricsTimeline.IndexAt(lines, 2f));
    }

    [Test]
    public void GapBetweenTwoLinesHasNoLine()
    {
        var lines = LyricsTimeline.Build(
            new[] { 1f, 5f },
            new[] { 2f, 6f },
            new[] { "あ", "い" },
            null);

        Assert.AreEqual(-1, LyricsTimeline.IndexAt(lines, 3f));
        Assert.AreEqual(1, LyricsTimeline.IndexAt(lines, 5f));
    }

    [Test]
    public void BlankLineIsFoundButHasNoText()
    {
        // 全角スペースだけの行は「ここで歌詞を消す」合図
        var lines = LyricsTimeline.Build(new[] { 1f }, new[] { 2f }, new[] { "　" }, null);

        Assert.AreEqual(0, LyricsTimeline.IndexAt(lines, 1.5f));
        Assert.IsFalse(lines[0].HasText);
    }

    [Test]
    public void TextWithLettersHasText()
    {
        var lines = LyricsTimeline.Build(new[] { 1f }, new[] { 2f }, new[] { "Let's Go!!" }, null);

        Assert.IsTrue(lines[0].HasText);
    }

    [Test]
    public void HasLinesIsFalseWithoutLyrics()
    {
        Assert.IsFalse(LyricsTimeline.HasLines(LyricsTimeline.Build(null, null, null, null)));
        Assert.IsTrue(LyricsTimeline.HasLines(LyricsTimeline.Build(new[] { 1f }, new[] { 2f }, new[] { "あ" }, null)));
    }

    /// <summary>
    /// 同梱の譜面が持つ新旧どちらの endTime 書き方でも、
    /// 「表示区間が必ず正になる」「行が順番に並ぶ」ことを固定する。
    /// </summary>
    [Test]
    public void EveryBundledChartProducesValidLines()
    {
        var broken = new List<string>();
        var seen = new HashSet<string>();

        foreach (var song in GManager.SongName)
        {
            if (!seen.Add(song)) continue;
            var asset = Resources.Load<TextAsset>(song);
            if (asset == null) continue;

            var chart = JsonUtility.FromJson<NoteEditor.DTO.MusicDTO.EditData>(asset.text);
            if (chart == null || chart.lyrics == null) continue;

            var lines = LyricsTimeline.Build(
                chart.lyrics.startTime,
                chart.lyrics.endTime,
                chart.lyrics.mondai,
                chart.lyrics.furigana);

            float previousStart = float.NegativeInfinity;
            foreach (var line in lines)
            {
                if (line.End <= line.Start) broken.Add($"{song}: line {line.Start} has no length");
                if (line.Start < previousStart) broken.Add($"{song}: line {line.Start} is out of order");
                previousStart = line.Start;
            }
        }

        Assert.AreEqual(0, broken.Count, string.Join(", ", broken));
    }

    /// <summary>
    /// endTime が開始より前の古い譜面 (Who) でも、歌詞は行の開始時刻に出る。
    /// </summary>
    [Test]
    public void ChartWithEndTimeBeforeStartStillShowsItsFirstLine()
    {
        var asset = Resources.Load<TextAsset>("Who");
        Assert.IsNotNull(asset, "the chart of Who must be part of the catalog");

        var chart = JsonUtility.FromJson<NoteEditor.DTO.MusicDTO.EditData>(asset.text);
        var lines = LyricsTimeline.Build(
            chart.lyrics.startTime,
            chart.lyrics.endTime,
            chart.lyrics.mondai,
            chart.lyrics.furigana);

        Assert.Greater(lines.Count, 0);
        int index = LyricsTimeline.IndexAt(lines, chart.lyrics.startTime[0]);
        Assert.GreaterOrEqual(index, 0, "the first line must be on screen at its start time");
        Assert.IsTrue(lines[index].HasText);
    }

    /// <summary>
    /// endTime が正しくある譜面 (GURU) では、開始と終了の時刻がそのまま使われる。
    /// </summary>
    [Test]
    public void ChartWithProperEndTimeUsesIt()
    {
        var asset = Resources.Load<TextAsset>("GURU");
        Assert.IsNotNull(asset, "the chart of GURU must be part of the catalog");

        var chart = JsonUtility.FromJson<NoteEditor.DTO.MusicDTO.EditData>(asset.text);
        var lines = LyricsTimeline.Build(
            chart.lyrics.startTime,
            chart.lyrics.endTime,
            chart.lyrics.mondai,
            chart.lyrics.furigana);

        Assert.Greater(lines.Count, 0);
        Assert.AreEqual(chart.lyrics.startTime[0], lines[0].Start, Delta);
        Assert.AreEqual(chart.lyrics.endTime[0], lines[0].End, Delta);
    }
}
