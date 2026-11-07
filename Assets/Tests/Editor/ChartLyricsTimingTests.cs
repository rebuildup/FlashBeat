using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 譜面 (Resources/&lt;曲名&gt;.json) の歌詞ブロックの不変条件。
///
/// 歌詞は旧形式の並列配列 (startTime / endTime / mondai / ...) で、
/// endTime は「その行を表示し終わる時刻」のはず。開始より前 (または同時) になっていると、
/// 実行時 (LyricsTimeline.ResolveEnd) はその値を採用せず次の行の開始時刻に
/// フォールバックする。フォールバックで動くため、譜面ファイルが壊れていることに
/// 気付けない。データ側で「開始 &lt; 終了」が成立していることを検査して、
/// 壊れたまま出荷されないようにする。
///
/// 検査対象は Resources 配下のすべての譜面 JSON (曲の数だけ増えると
/// 見落とすため、最初の 1 曲ではなく全ファイルを走査する)。
/// </summary>
public class ChartLyricsTimingTests
{
    /// <summary>譜面 JSON が置いてあるフォルダ (正となる Resources)。</summary>
    const string ChartRoot = "Assets/Game/Resources";

    /// <summary>Resources 配下の譜面 JSON を全部 (名前順で安定)。</summary>
    static List<string> ChartFiles()
    {
        var files = new List<string>();
        if (!Directory.Exists(ChartRoot)) return files;

        string[] found = Directory.GetFiles(ChartRoot, "*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(found, StringComparer.Ordinal);
        files.AddRange(found);
        return files;
    }

    [Test]
    public void EveryLyricsRowStartsBeforeItEnds()
    {
        Assert.IsTrue(Directory.Exists(ChartRoot), "the chart folder is missing: " + ChartRoot);

        var broken = new List<string>();
        foreach (string file in ChartFiles())
        {
            string name = Path.GetFileNameWithoutExtension(file);
            var chart = JsonUtility.FromJson<NoteEditor.DTO.MusicDTO.EditData>(File.ReadAllText(file));
            if (chart == null || chart.lyrics == null) continue;

            float[] starts = chart.lyrics.startTime;
            float[] ends = chart.lyrics.endTime;
            if (starts == null || ends == null) continue;

            // 長さの違う並列配列は短い方に合わせる (実行時と同じ解釈)。
            int count = starts.Length < ends.Length ? starts.Length : ends.Length;
            for (int i = 0; i < count; i++)
            {
                if (starts[i] < ends[i]) continue;
                broken.Add($"{name}[{i}] start={starts[i]} end={ends[i]}");
            }
        }

        Assert.IsEmpty(broken, "lyrics rows that do not start before they end: " + string.Join(", ", broken.ToArray()));
    }
}
