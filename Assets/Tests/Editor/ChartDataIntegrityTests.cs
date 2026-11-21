using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Integrity of the chart data contract.
///
/// Issue #823: 実曲の譜面 JSON は HEAD から撤去し、製品版の譜面は
/// クラウドカタログ経由でのみ供給される。かつてこのファイルが固定して
/// いた「同梱譜面の内容」(ノーツ数・歌詞ブロック・末尾時刻) は、
/// クラウド側のデータが正本になったため、ここでは次を固定する:
///
///   - Resources に実曲の譜面 JSON が残っていないこと (再混入の検出)
///   - クラウド譜面の封筒 → Data への変換と ChartValidator の契約
///   - ノーツ時刻の解釈式 (NoteTimeSeconds) が変わっていないこと
///     (合成 fixture で固定。実曲の値はクラウド正本側の責務)
///   - カタログ (GManager) の構造不変条件 (並列配列・プレースホルダ・
///     再生可能下限)
/// </summary>
public class ChartDataIntegrityTests
{
    /// <summary>
    /// Issue #823 の合成 fixture 曲 (権利クリア・カタログ非登録)。
    /// remoteId は "synthetic-" 接頭辞で本番カタログと混ざらない。
    /// </summary>
    const string FixtureRemoteId = "synthetic-integrity";

    /// <summary>Every song of the catalog, without the repeated placeholder.</summary>
    static IEnumerable<string> CatalogSongs()
    {
        var seen = new HashSet<string>();
        foreach (var song in GManager.SongName)
        {
            if (seen.Add(song)) yield return song;
        }
    }

    // --- Issue #823: ローカル譜面 JSON の撤去 -------------------------------

    /// <summary>
    /// カタログのどの曲名にも Resources の譜面 JSON (結合済み・旧形式の
    /// 両方) が残っていないこと。実曲譜面の再混入はライセンス退行なので、
    /// ここで検出する (CI の check-repo-integrity.sh も同じ経路を拒否する)。
    /// </summary>
    [Test]
    public void NoSongChartJSONRemainsInResources()
    {
        var remaining = new List<string>();
        foreach (var song in CatalogSongs())
        {
            if (Resources.Load<TextAsset>(song) != null) remaining.Add(song);
            if (Resources.Load<TextAsset>(song + "_text") != null) remaining.Add(song + "_text");
        }
        Assert.IsEmpty(remaining, "song chart JSON still in Resources: " + string.Join(", ", remaining));
    }

    // --- クラウド譜面の契約 -------------------------------------------------

    /// <summary>
    /// 合成 fixture の封筒 JSON が ParseChartResponse → ChartValidator を
    /// 通り、実際にプレイ可能な譜面になること。製品パス
    /// (SelectSceneManager.StartRemoteSong の検証) と同じ 2 段。
    /// </summary>
    [Test]
    public void ACloudChartEnvelopeParsesIntoAValidChart()
    {
        string json = SyntheticCharts.LyricsChartEnvelope(FixtureRemoteId);
        Data chart = CloudCatalog.ParseChartResponse(json);

        Assert.IsNotNull(chart, "the fixture envelope did not parse");
        Assert.AreEqual(SyntheticCharts.LyricsChartNoteCount, chart.notes.Length);
        Assert.AreEqual(120, chart.BPM);
        Assert.IsNull(ChartValidator.Validate(chart), "the fixture chart must pass the contract");
    }

    /// <summary>
    /// NotesManager.NoteTimeSeconds の解釈式 (60/BPM * num/LPB) を合成
    /// fixture で固定する。実曲譜面の「解釈が変わっていない」ことの
    /// 代わりに、時刻式そのものの回帰を防ぐ。
    /// </summary>
    [Test]
    public void TheNoteTimeFormulaIsPinnedByTheFixture()
    {
        Data chart = CloudCatalog.ParseChartResponse(SyntheticCharts.LyricsChartEnvelope(FixtureRemoteId));
        Assert.IsNotNull(chart);

        // BPM 120 / LPB 4 → 1 num = 0.125 秒。先頭ノーツ num=16 → 2.0 秒、
        // 末尾 num=240 → 30.0 秒。
        Assert.AreEqual(2.0f, NotesManager.NoteTimeSeconds(chart.BPM, chart.notes[0]), 0.001f);
        float last = NotesManager.NoteTimeSeconds(chart.BPM, chart.notes[chart.notes.Length - 1]);
        Assert.AreEqual(30.0f, last, 0.001f);
    }

    /// <summary>
    /// 合成 fixture を保持したリモート曲は、NotesManager と同じ経路
    /// (PlaySessionSnapshot → CloudChartHolder) で歌詞まで解決できること。
    /// Issue #823/#789 の「固定譜面 1 本からノーツと歌詞を読む」契約。
    /// </summary>
    [Test]
    public void AHeldCloudChartSuppliesNotesAndLyricsFromOneSource()
    {
        PlaySessionSnapshot.Clear();
        CloudChartHolder.Clear();
        ChartLyrics.Invalidate();
        try
        {
            string json = SyntheticCharts.LyricsChartEnvelope(FixtureRemoteId);
            CloudChartHolder.Hold(FixtureRemoteId, SyntheticCharts.ChartVersion, json);

            SongData song = new SongData(1, "合成曲", "fixture", "abcdefghijk", 120, 5, 15, 60f)
            {
                isRemote = true,
                remoteId = FixtureRemoteId,
                chartVersion = SyntheticCharts.ChartVersion,
            };
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(song, SyntheticCharts.ChartVersion, json);

            ChartLyricsResult lyrics = ChartLyrics.Resolve(session.Song);
            Assert.AreEqual(ChartLyricsState.HasLyrics, lyrics.State);
            Assert.AreEqual("僕を連れてって", lyrics.Lyrics.Mondai[0]);
        }
        finally
        {
            PlaySessionSnapshot.Clear();
            CloudChartHolder.Clear();
            ChartLyrics.Invalidate();
        }
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
}
