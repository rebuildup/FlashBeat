using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Integrity of the bundled chart data.
///
/// The charts are the original performance data of the songs: if one of them
/// is missing, empty or read differently than before, the song cannot be
/// played the way it was charted. These tests make that visible instead of
/// leaving it to the notice of a player at the exhibition.
///
/// The catalog of GManager is the single source of the song list; the chart
/// files live in Resources/&lt;song&gt;.json.
/// </summary>
public class ChartDataIntegrityTests
{
    /// <summary>
    /// The placeholder at index 0 of the catalog. It has no chart by design.
    /// </summary>
    static readonly HashSet<string> SongsWithoutChart = new HashSet<string> { "noSong" };

    /// <summary>
    /// The cloud catalog placeholder: the chart exists, but it is empty
    /// (maxBlock 5, no notes). It is not a broken chart.
    /// </summary>
    static readonly HashSet<string> SongsWithoutNotes = new HashSet<string> { "ニセモノ" };

    /// <summary>
    /// Charts whose lyrics carry more lines than start times (the extra lines
    /// are never shown). The list is explicit so that a new deviation is
    /// noticed.
    /// </summary>
    static readonly HashSet<string> ChartsWithMoreLinesThanStartTimes = new HashSet<string>
    {
        "GURU",
    };

    /// <summary>
    /// Charts whose lyrics carry no end time (or a shorter one). The game only
    /// needs the line and its start time, so these are not broken - but the
    /// list is explicit so that a new deviation is noticed.
    /// </summary>
    static readonly HashSet<string> ChartsWithoutLyricsEndTime = new HashSet<string>
    {
        "ロストアンブレラ", "終焉逃避行", "エウタナシア", "チュートリアル", "セルフィー",
        "パノプティコン", "GURU", "阿吽のビーツ", "混沌ブギ", "春嵐", "ビビビビ",
        "灰Φ倶楽部", "オーバーライド", "イガク", "テレキャスタービーボーイ", "一龠",
    };

    /// <summary>
    /// Note count and the time of the last note of every chart, pinned so that
    /// a change in the way the tap charts are interpreted is noticed.
    /// </summary>
    static readonly Dictionary<string, (int notes, float lastNoteTime)> PinnedCharts = new Dictionary<string, (int, float)>
    {
        { "ロストアンブレラ", (268, 74.507f) },
        { "まにまに", (76, 34.138f) },
        { "Who", (139, 63.398f) },
        { "ツイッターランド", (130, 40.563f) },
        { "終焉逃避行", (296, 89.592f) },
        { "エウタナシア", (243, 74.531f) },
        { "Chartreuse", (343, 76.750f) },
        { "Psyched", (256, 76.900f) },
        { "Dogbite", (603, 118.000f) },
        { "チュートリアル", (53, 60.632f) },
        { "狂喜蘭舞", (360, 74.574f) },
        { "Poison", (82, 50.750f) },
        { "セルフィー", (254, 71.462f) },
        { "4th smile", (105, 61.714f) },
        { "Calamity", (111, 42.000f) },
        { "パノプティコン", (319, 112.457f) },
        { "GURU", (295, 69.674f) },
        { "阿吽のビーツ", (262, 55.340f) },
        { "混沌ブギ", (292, 81.158f) },
        { "春嵐", (315, 53.196f) },
        { "ビビビビ", (184, 72.343f) },
        { "灰Φ倶楽部", (275, 61.469f) },
        { "オーバーライド", (250, 59.191f) },
        { "イガク", (196, 69.574f) },
        { "テレキャスタービーボーイ", (163, 58.228f) },
        { "一龠", (191, 71.932f) },
        { "ニセモノ", (0, 0.000f) },
    };

    /// <summary>Every song of the catalog, without the repeated placeholder.</summary>
    static IEnumerable<string> CatalogSongs()
    {
        var seen = new HashSet<string>();
        foreach (var song in GManager.SongName)
        {
            if (seen.Add(song)) yield return song;
        }
    }

    static string ChartText(string song)
    {
        var asset = Resources.Load<TextAsset>(song);
        return asset == null ? null : asset.text;
    }

    static Data ChartData(string song)
    {
        var text = ChartText(song);
        return text == null ? null : JsonUtility.FromJson<Data>(text);
    }

    static NoteEditor.DTO.MusicDTO.LyricsDTO Lyrics(string song)
    {
        var text = ChartText(song);
        if (text == null) return null;
        var edit = JsonUtility.FromJson<NoteEditor.DTO.MusicDTO.EditData>(text);
        return edit == null ? null : edit.lyrics;
    }

    [Test]
    public void EverySongOfTheCatalogHasAChart()
    {
        var missing = new List<string>();
        foreach (var song in CatalogSongs())
        {
            if (SongsWithoutChart.Contains(song)) continue;
            if (ChartText(song) == null) missing.Add(song);
        }
        Assert.IsEmpty(missing, "no chart in Resources for: " + string.Join(", ", missing));
    }

    [Test]
    public void EveryChartHasAtLeastOneNote()
    {
        var empty = new List<string>();
        foreach (var song in CatalogSongs())
        {
            if (SongsWithoutChart.Contains(song) || SongsWithoutNotes.Contains(song)) continue;
            var chart = ChartData(song);
            if (chart == null || chart.notes == null || chart.notes.Length == 0) empty.Add(song);
        }
        Assert.IsEmpty(empty, "charts without notes: " + string.Join(", ", empty));
    }

    [Test]
    public void EveryChartHasALyricsBlock()
    {
        var missing = new List<string>();
        foreach (var song in CatalogSongs())
        {
            if (SongsWithoutChart.Contains(song)) continue;
            var lyrics = Lyrics(song);
            if (lyrics == null || lyrics.mondai == null || lyrics.startTime == null)
            {
                missing.Add(song);
                continue;
            }

            // The game shows the line that starts at startTime[i], so there have
            // to be at least as many lines as start times.
            if (lyrics.mondai.Length < lyrics.startTime.Length)
            {
                missing.Add(song + $" (mondai={lyrics.mondai.Length} startTime={lyrics.startTime.Length})");
            }
        }
        Assert.IsEmpty(missing, "lyrics are not consistent: " + string.Join(", ", missing));
    }

    [Test]
    public void TheLyricsWithMoreLinesThanStartTimesAreTheKnownOnes()
    {
        var unexpected = new List<string>();
        foreach (var song in CatalogSongs())
        {
            if (SongsWithoutChart.Contains(song)) continue;
            var lyrics = Lyrics(song);
            if (lyrics == null || lyrics.mondai == null || lyrics.startTime == null) continue;

            if (lyrics.mondai.Length > lyrics.startTime.Length && !ChartsWithMoreLinesThanStartTimes.Contains(song))
            {
                unexpected.Add(song);
            }
        }
        Assert.IsEmpty(unexpected, "charts with more lyrics lines than start times: " + string.Join(", ", unexpected));
    }

    [Test]
    public void TheLyricsWithoutEndTimeAreTheKnownOnes()
    {
        var unexpected = new List<string>();
        foreach (var song in CatalogSongs())
        {
            if (SongsWithoutChart.Contains(song)) continue;
            var lyrics = Lyrics(song);
            if (lyrics == null || lyrics.mondai == null) continue;

            bool endTimeMatches = lyrics.endTime != null && lyrics.endTime.Length == lyrics.mondai.Length;
            if (!endTimeMatches && !ChartsWithoutLyricsEndTime.Contains(song))
            {
                unexpected.Add(song);
            }
        }
        Assert.IsEmpty(unexpected, "charts with a missing lyrics end time: " + string.Join(", ", unexpected));
    }

    [Test]
    public void TheParallelArraysOfTheCatalogAreInLockstep()
    {
        int songs = GManager.SongName.Length;
        Assert.AreEqual(songs, GManager.Musician.Length, "Musician has a different length than SongName");
        Assert.AreEqual(songs, GManager.SongURL.Length, "SongURL has a different length than SongName");
        Assert.AreEqual(songs, GManager.SBPM.Length, "SBPM has a different length than SongName");
        Assert.AreEqual(songs, GManager.Slevel.Length, "Slevel has a different length than SongName");
        Assert.AreEqual(songs, GManager.Shit.Length, "Shit has a different length than SongName");
        Assert.AreEqual(songs, GManager.SongLong.Length, "SongLong has a different length than SongName");
    }

    // --- カタログの契約 (issue #58): 並列配列から Songs が組み上がること ----

    /// <summary>
    /// <see cref="GManager.Songs"/> は並列配列から 1 行ずつ組み上がる。
    /// id は配列の添字そのもの、タイトルは SongName そのもの。特に先頭は
    /// noSong プレースホルダ (id = 0、再生不可) でなければならない
    /// (実曲は id 1 以降。GManager.songID の初期値も 1)。
    /// </summary>
    [Test]
    public void TheSongsAreBuiltFromTheCatalogWithThePlaceholderAtZero()
    {
        Assert.AreEqual(GManager.SongName.Length, GManager.Songs.Length,
            "Songs has a different length than SongName");

        for (int i = 0; i < GManager.Songs.Length; i++)
        {
            Assert.AreEqual(i, GManager.Songs[i].id, $"Songs[{i}].id must be the array index");
            Assert.AreEqual(GManager.SongName[i], GManager.Songs[i].title, $"Songs[{i}].title must be SongName[{i}]");
        }

        SongData placeholder = GManager.Songs[0];
        Assert.AreEqual(0, placeholder.id, "the placeholder must stay at id 0");
        Assert.AreEqual("noSong", placeholder.title, "the placeholder must stay noSong");
        Assert.IsTrue(placeholder.unplayable, "the placeholder must not be playable");
    }

    /// <summary>
    /// UnplayableTitles (private) の全タイトルがカタログの実在する行へ解決し、
    /// その行が実際に再生不可としてマークされていること。タイトルの綴りが
    /// ずれると、再生できないはずの行が選曲に出てしまう (#95)。
    /// </summary>
    [Test]
    public void EveryUnplayableTitleResolvesToACatalogEntry()
    {
        var unplayableTitles = UnplayableTitles();
        Assert.IsNotEmpty(unplayableTitles, "UnplayableTitles must not be empty");

        var missing = new List<string>();
        foreach (var title in unplayableTitles)
        {
            bool resolved = false;
            for (int i = 0; i < GManager.SongName.Length; i++)
            {
                if (GManager.SongName[i] != title) continue;
                resolved = true;
                Assert.IsTrue(GManager.Songs[i].unplayable,
                    $"{title} (index {i}) is in UnplayableTitles but the row is playable");
            }
            if (!resolved) missing.Add(title);
        }
        Assert.IsEmpty(missing, "UnplayableTitles without a catalog entry: " + string.Join(", ", missing));
    }

    /// <summary>
    /// 選曲に出せる行 (unplayable = false) は、並列配列の行数から
    /// UnplayableTitles に当たった行を引いた数。既知のプレースホルダ
    /// (noSong とクラウドカタログの残骸) を引いても、なお十分な曲が
    /// 選べることを下限で固定する。
    /// </summary>
    [Test]
    public void TheCatalogKeepsAPlayableFloor()
    {
        var unplayableTitles = UnplayableTitles();
        int playable = 0;
        for (int i = 0; i < GManager.Songs.Length; i++)
        {
            bool marked = GManager.Songs[i].unplayable;
            Assert.AreEqual(unplayableTitles.Contains(GManager.SongName[i]), marked,
                $"Songs[{i}] ({GManager.SongName[i]}) differs from UnplayableTitles");
            if (!marked) playable++;
        }

        Assert.GreaterOrEqual(playable, 20,
            "the catalog lost playable songs - the song list is broken for the player");
    }

    /// <summary>UnplayableTitles は private。契約の対象なので reflection で読む。</summary>
    static HashSet<string> UnplayableTitles()
    {
        var field = typeof(GManager).GetField("UnplayableTitles",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(field, "GManager.UnplayableTitles not found");
        return (HashSet<string>)field.GetValue(null);
    }

    [Test]
    public void EveryNoteOfTheExistingChartsIsATapNote()
    {
        var other = new List<string>();
        foreach (var song in CatalogSongs())
        {
            if (SongsWithoutChart.Contains(song)) continue;
            var chart = ChartData(song);
            if (chart == null || chart.notes == null) continue;
            foreach (var note in chart.notes)
            {
                if (note.type != 1) other.Add($"{song} (type={note.type})");
            }
        }
        Assert.IsEmpty(other, "notes that are not tap notes: " + string.Join(", ", other));
    }

    [Test]
    public void TheExistingTapChartsAreStillInterpretedTheSameWay()
    {
        var changed = new List<string>();
        foreach (var pair in PinnedCharts)
        {
            var chart = ChartData(pair.Key);
            if (chart == null)
            {
                changed.Add(pair.Key + " (chart missing)");
                continue;
            }

            int notes = chart.notes == null ? 0 : chart.notes.Length;
            if (notes != pair.Value.notes)
            {
                changed.Add($"{pair.Key} (notes={notes}, expected {pair.Value.notes})");
                continue;
            }

            if (notes == 0) continue;

            float last = NotesManager.NoteTimeSeconds(chart.BPM, chart.notes[notes - 1]);
            if (Mathf.Abs(last - pair.Value.lastNoteTime) > 0.01f)
            {
                changed.Add($"{pair.Key} (last note at {last:F3}s, expected {pair.Value.lastNoteTime:F3}s)");
            }
        }
        Assert.IsEmpty(changed, "the interpretation of the tap charts changed: " + string.Join(", ", changed));
    }
}
