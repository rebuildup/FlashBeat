using System;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Title;

/// <summary>
/// v6 タイトル (Issue #239) の実データ表示ロジックの EditMode テスト。
///
/// 表示値 (曲名 / アーティスト / ベストスコア) が
///  - 実データ (カタログ + 保存済みベストスコア) だけから作られる
///  - design 例の固定値や、未プレイの 0 点を記録として出さない
/// ことを固定する。
/// </summary>
public class V6TitleRuntimeTests
{
    // --- IndexOfBestRecord (自己ベストの選択) ---

    [Test]
    public void IndexOfBestRecord_NullScores_HasNoRecord()
    {
        Assert.AreEqual(-1, V6TitleLogic.IndexOfBestRecord(null));
    }

    [Test]
    public void IndexOfBestRecord_NoScoreAboveZero_IsUnplayed()
    {
        // 0 点は未プレイ (保存ストアは 0 点を記録しない)。
        Assert.AreEqual(-1, V6TitleLogic.IndexOfBestRecord(new[] { 0, 0, 0 }));
    }

    [Test]
    public void IndexOfBestRecord_PicksTheHighestScore()
    {
        Assert.AreEqual(3, V6TitleLogic.IndexOfBestRecord(new[] { 0, 120, 40, 900, 700 }));
    }

    [Test]
    public void IndexOfBestRecord_TiedScores_KeepsTheCatalogOrder()
    {
        Assert.AreEqual(1, V6TitleLogic.IndexOfBestRecord(new[] { 0, 500, 500 }));
    }

    // --- BestRecord (曲名 / アーティスト / ハイスコア) ---

    [Test]
    public void BestRecord_UsesTheCatalogRowAndItsScore()
    {
        var songs = new[]
        {
            new SongData(0, "noSong", "", "", 0, 0, 0, 0f),
            new SongData(1, "曲A", "作家A", "", 0, 0, 0, 0f),
            new SongData(2, "曲B", "作家B", "", 0, 0, 0, 0f),
        };

        V6TitleLogic.V6TitleRecord record = V6TitleLogic.BestRecord(songs, new[] { 0, 10, 250 });

        Assert.IsTrue(record.HasRecord);
        Assert.AreEqual(2, record.SongIndex);
        Assert.AreEqual("曲B", record.Title);
        Assert.AreEqual("作家B", record.Artist);
        Assert.AreEqual(250, record.Score);
    }

    [Test]
    public void BestRecord_WithoutAnyScore_HasNoRecord()
    {
        var songs = new[]
        {
            new SongData(0, "noSong", "", "", 0, 0, 0, 0f),
            new SongData(1, "曲A", "作家A", "", 0, 0, 0, 0f),
        };

        V6TitleLogic.V6TitleRecord record = V6TitleLogic.BestRecord(songs, new[] { 0, 0 });

        Assert.IsFalse(record.HasRecord);
        Assert.AreEqual(-1, record.SongIndex);
        Assert.AreEqual(0, record.Score);
    }

    [Test]
    public void BestRecord_NullInput_HasNoRecord()
    {
        V6TitleLogic.V6TitleRecord record = V6TitleLogic.BestRecord(null, null);

        Assert.IsFalse(record.HasRecord);
        Assert.AreEqual(-1, record.SongIndex);
    }

    // --- ScoreText (スコアの表示) ---

    [Test]
    public void ScoreText_SeparatesThousands()
    {
        Assert.AreEqual("1,234,567", V6TitleLogic.ScoreText(1234567));
        Assert.AreEqual("980", V6TitleLogic.ScoreText(980));
    }

    [Test]
    public void ScoreText_UnplayedScore_ShowsNothing()
    {
        // 未プレイの 0 点を「0 点」と表示しない。
        Assert.AreEqual("", V6TitleLogic.ScoreText(0));
        Assert.AreEqual("", V6TitleLogic.ScoreText(-5));
    }

    // --- MoveIndex (タブ選択の折り返し) ---

    [Test]
    public void MoveIndex_WrapsAtBothEnds()
    {
        Assert.AreEqual(3, V6TitleLogic.MoveIndex(0, -1, 4));
        Assert.AreEqual(0, V6TitleLogic.MoveIndex(3, 1, 4));
        Assert.AreEqual(2, V6TitleLogic.MoveIndex(1, 1, 4));
        Assert.AreEqual(0, V6TitleLogic.MoveIndex(1, -1, 4));
    }

    [Test]
    public void MoveIndex_WithoutTabs_HasNoSelection()
    {
        Assert.AreEqual(-1, V6TitleLogic.MoveIndex(0, 1, 0));
    }

    [Test]
    public void MoveIndex_OutOfRangeSelection_StartsFromTheFirstTab()
    {
        // 選択が範囲外なら先頭 (0) から数える。
        Assert.AreEqual(1, V6TitleLogic.MoveIndex(9, 1, 3));
        Assert.AreEqual(1, V6TitleLogic.MoveIndex(-1, 1, 3));
    }

    // --- Issue #239: タブの KeyCap 表記と poll 対象キーの一致 ---

    [Test]
    public void HistoryTabKey_MatchesPolledKeyCode()
    {
        // KeyCap 凡例と TitleSceneManager が poll するキーがずれると
        // 「表示されているが押せないキー」になる。単一ソースの対応を固定する。
        Assert.IsTrue(Enum.TryParse(V6TitleLogic.HistoryTabKey, out KeyCode historyKey),
            "HistoryTabKey は KeyCode 名であること");
        Assert.AreEqual(V6TitleLogic.HistoryTabKeyCode, historyKey);
    }

    [Test]
    public void RankingTabKey_MatchesPolledKeyCode()
    {
        Assert.IsTrue(Enum.TryParse(V6TitleLogic.RankingTabKey, out KeyCode parsed),
            "RankingTabKey は KeyCode 名であること");
        Assert.AreEqual(V6TitleLogic.RankingTabKeyCode, parsed);
    }
}
