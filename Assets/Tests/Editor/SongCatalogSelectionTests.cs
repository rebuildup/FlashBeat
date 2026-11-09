using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// カタログ (GManager) の不変条件 (Issue #222 の回帰テスト)。
///
/// Issue #222 の前提「45 曲」は古い: 現在カタログは 29 エントリで、
/// そのうち noSong (index 0) と ニセモノ (index 27 / 28) は
/// 譜面を持たないプレースホルダで、演奏不可 (GManager の unplayable) として
/// 選曲画面から出ない。そのため曲数そのものではなく、意味のある不変条件を固定する:
///
///   (a) 7 つの並列配列が同じ長さ  → ChartDataIntegrityTests が担保 (重複しない)
///   (b) GManager.Songs が並列配列と対応する (id / title)
///   (c) 選択できる曲 (演奏可能な曲) が 1 つ以上あり、
///       演奏不可なタイトルはプレースホルダだけ
///   (d) 演奏可能な曲には正本の Resources フォルダに譜面 JSON がある
/// </summary>
public class SongCatalogSelectionTests
{
    /// <summary>譜面 JSON が置いてある唯一の Resources フォルダ。</summary>
    const string CanonicalResources = "Game/Resources";

    static List<SongData> PlayableSongs()
    {
        var playable = new List<SongData>();
        foreach (var song in GManager.Songs)
        {
            if (song != null && !song.unplayable) playable.Add(song);
        }
        return playable;
    }

    static List<SongData> UnplayableSongs()
    {
        var unplayable = new List<SongData>();
        foreach (var song in GManager.Songs)
        {
            if (song != null && song.unplayable) unplayable.Add(song);
        }
        return unplayable;
    }

    [Test]
    public void TheSongsArrayMatchesTheParallelArrays()
    {
        Assert.AreEqual(GManager.SongName.Length, GManager.Songs.Length,
            "GManager.Songs は並列配列 (SongName) と同じ要素数である");

        for (int i = 0; i < GManager.Songs.Length; i++)
        {
            var song = GManager.Songs[i];
            Assert.IsNotNull(song, $"[{i}] の曲データが無い");
            Assert.AreEqual(i, song.id, $"[{i}] の id が並列配列の位置と一致しない");
            Assert.AreEqual(GManager.SongName[i], song.title, $"[{i}] の曲名が並列配列と一致しない");
        }
    }

    [Test]
    public void ThereIsAtLeastOneSelectableSong()
    {
        var playable = PlayableSongs();

        Assert.Greater(playable.Count, 0, "選択できる曲が 1 つも無い (カタログが壊れている)");
    }

    [Test]
    public void TheUnplayableSongsAreOnlyThePlaceholders()
    {
        // 譜面を持たないプレースホルダだけが演奏不可。
        // (noSong は index 0 の予約枠、ニセモノはクラウドカタログの残骸 #95)
        var expected = new List<string> { "noSong", "ニセモノ" };

        var unplayableTitles = new List<string>();
        foreach (var song in UnplayableSongs())
        {
            if (!unplayableTitles.Contains(song.title)) unplayableTitles.Add(song.title);
        }
        unplayableTitles.Sort();

        var expectedSorted = new List<string>(expected);
        expectedSorted.Sort();

        CollectionAssert.AreEqual(expectedSorted, unplayableTitles,
            "演奏不可なタイトルはプレースホルダだけ");
    }

    [Test]
    public void TheSelectableSongsAreUnique()
    {
        // 演奏可能な曲が 2 つ、同じ曲名になることはない
        // (同じ名前が 2 つあると選曲画面の選択が曖昧になる)。
        var titles = new List<string>();
        var duplicates = new List<string>();
        foreach (var song in PlayableSongs())
        {
            if (titles.Contains(song.title)) duplicates.Add(song.title);
            else titles.Add(song.title);
        }

        Assert.IsEmpty(duplicates, "選択可能な曲名が重複している: " + string.Join(", ", duplicates));
    }

    [Test]
    public void EverySelectableSongHasAChartInTheCanonicalResourcesFolder()
    {
        var missing = new List<string>();
        foreach (var song in PlayableSongs())
        {
            string path = Path.Combine(Application.dataPath, CanonicalResources, song.title + ".json");
            if (!File.Exists(path)) missing.Add(song.title);
        }

        Assert.IsEmpty(missing, "正本の Resources フォルダに譜面が無い曲: " + string.Join(", ", missing));
    }

    [Test]
    public void TheNumberOfSelectableSongsEqualsTheNumberOfPlayableTitles()
    {
        // 選曲画面に出す曲数 (演奏可能) と、並列配列から数えた演奏可能な曲名が
        // 一致すること (Spec: 曲一覧の定義が二重に存在しない)。
        int playableEntries = PlayableSongs().Count;

        var titles = new HashSet<string>();
        foreach (var song in GManager.Songs)
        {
            if (song != null && !song.unplayable) titles.Add(song.title);
        }

        Assert.AreEqual(playableEntries, titles.Count,
            "選択できる曲数と演奏可能な曲名の数が一致しない");
        Assert.Greater(playableEntries, 0, "選択できる曲が 1 つも無い");
    }
}
