using System.Collections.Generic;
using NUnit.Framework;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;
using FlashBeat.UI.V6.Select.View;

/// <summary>
/// 選曲画面のスケーラビリティ回帰テスト (Issue #222)。
///
/// Issue #222 の受入条件「45 曲以上でも選曲が破綻しない」は、
/// **本番カタログの実曲数を 45 件まで増やすこと** ではなく、
/// 選曲機能が 45 件以上のカタログで正しく動くことの検証として扱う
/// (オーナー判断 2026-11-10: 実曲数を無理に増やさない)。
///
/// そのため、実カタログ (`GManager`) には触らず、**60 件の合成カタログ** を
/// その場で組み立てて、以下を固定する:
///
///   1. 検索フィルタ (部分一致 / 大小無視 / 順序保持 / 0 件) が件数によらず同じ規則で動く
///   2. スクロール計算 (ClampScroll / ScrollToReveal / CenterScroll) が
///      先頭から末尾まで全行を到達可能にする (どの行も「見えないまま」にならない)
///   3. 9 行の可視窓と行矩形が、60 件でもリストパネル内に収まる
///   4. ドットレール (1 曲 1 ドット) が 60 件でもパネル内に収まる
///   5. ReadyAvailable が絞り込み後リストに対して判定される
///
/// 実カタログ側の不変条件 (7 並列配列・演奏可能曲の譜面存在) は
/// `SongCatalogSelectionTests` / `ChartDataIntegrityTests` が担当する。
/// </summary>
public class SongCatalogScalabilityTests
{
    private const int SyntheticSongCount = 60;
    private const int AlphaEvery = 3; // 3 曲に 1 曲を Alpha 名義にする

    /// <summary>60 件の合成カタログ。SongId は 1..60、曲名は "Song 01".."Song 60"。</summary>
    private static List<V6SongEntry> BuildSyntheticCatalog()
    {
        var songs = new List<V6SongEntry>(SyntheticSongCount);
        for (int i = 0; i < SyntheticSongCount; i++)
        {
            int id = i + 1;
            songs.Add(new V6SongEntry
            {
                SongId = id,
                Title = "Song " + id.ToString("00"),
                Artist = (i % AlphaEvery == 0) ? "Alpha Artist" : "Beta Artist",
                Level = (id % 10) + 1,
                Bpm = 120 + id,
                NoteCount = 400 + id * 10,
            });
        }

        return songs;
    }

    private static int[] IdsOf(IReadOnlyList<V6SongEntry> songs)
    {
        var ids = new int[songs.Count];
        for (int i = 0; i < songs.Count; i++)
        {
            ids[i] = songs[i].SongId;
        }

        return ids;
    }

    [Test]
    public void SyntheticCatalog_HasAtLeast45Songs_AndIsWellFormed()
    {
        var songs = BuildSyntheticCatalog();

        Assert.GreaterOrEqual(songs.Count, 45, "スケーラビリティ検証には 45 件以上が必要 (#222)");
        Assert.AreEqual(SyntheticSongCount, songs.Count);

        // SongId は 1..60 で重複なし (実カタログの id=0 プレースホルダを模さない)
        var seen = new HashSet<int>();
        for (int i = 0; i < songs.Count; i++)
        {
            Assert.AreEqual(i + 1, songs[i].SongId);
            Assert.IsTrue(seen.Add(songs[i].SongId), "SongId が重複している: " + songs[i].SongId);
        }
    }

    [Test]
    public void Filter_EmptyQuery_KeepsAllSongsInCatalogOrder()
    {
        var songs = BuildSyntheticCatalog();

        List<V6SongEntry> filtered = V6SelectDataLogic.Filter(songs, string.Empty);

        Assert.AreEqual(SyntheticSongCount, filtered.Count);
        CollectionAssert.AreEqual(IdsOf(songs), IdsOf(filtered), "空クエリはカタログ順のまま全件");
        CollectionAssert.AreEqual(IdsOf(songs), IdsOf(V6SelectDataLogic.Filter(songs, null)), "null も空クエリ扱い");
    }

    [Test]
    public void Filter_ByTitlePrefix_ReturnsOnlyMatchingRange()
    {
        var songs = BuildSyntheticCatalog();

        List<V6SongEntry> filtered = V6SelectDataLogic.Filter(songs, "song 1");

        // "Song 10".."Song 19" の 10 件だけが部分一致する
        Assert.AreEqual(10, filtered.Count);
        for (int i = 0; i < filtered.Count; i++)
        {
            Assert.AreEqual(10 + i, filtered[i].SongId);
        }
    }

    [Test]
    public void Filter_IsCaseInsensitive_AtScale()
    {
        var songs = BuildSyntheticCatalog();

        List<V6SongEntry> lower = V6SelectDataLogic.Filter(songs, "song 1");
        List<V6SongEntry> upper = V6SelectDataLogic.Filter(songs, "SONG 1");

        CollectionAssert.AreEqual(IdsOf(lower), IdsOf(upper));
    }

    [Test]
    public void Filter_ByArtist_ReturnsEveryThirdSong()
    {
        var songs = BuildSyntheticCatalog();

        List<V6SongEntry> filtered = V6SelectDataLogic.Filter(songs, "alpha");

        Assert.AreEqual(SyntheticSongCount / AlphaEvery, filtered.Count);
        foreach (V6SongEntry song in filtered)
        {
            StringAssert.Contains("Alpha", song.Artist);
        }
    }

    [Test]
    public void Filter_NoMatch_ReturnsEmpty()
    {
        var songs = BuildSyntheticCatalog();

        Assert.AreEqual(0, V6SelectDataLogic.Filter(songs, "存在しない曲名").Count);
        Assert.AreEqual(0, V6SelectDataLogic.Filter(null, "song").Count);
    }

    [Test]
    public void Filter_EverySongIsReachable_ByItsOwnTitle()
    {
        var songs = BuildSyntheticCatalog();

        // 60 曲それぞれが、自分の曲名で 1 件以上ヒットする (検索で到達不能な曲を作らない)
        foreach (V6SongEntry song in songs)
        {
            List<V6SongEntry> filtered = V6SelectDataLogic.Filter(songs, song.Title);
            Assert.GreaterOrEqual(filtered.Count, 1, "曲名検索で 0 件: " + song.Title);

            bool contains = false;
            foreach (V6SongEntry candidate in filtered)
            {
                if (candidate.SongId == song.SongId) contains = true;
            }

            Assert.IsTrue(contains, "曲名検索の結果に自分が含まれない: " + song.Title);
        }
    }

    [Test]
    public void ClampScroll_StaysWithinRowCount()
    {
        int rows = SyntheticSongCount;
        int visible = V6SelectViewLogic.ListVisibleRows;

        Assert.AreEqual(0, V6SelectViewLogic.ClampScroll(-5, rows, visible));
        Assert.AreEqual(0, V6SelectViewLogic.ClampScroll(0, rows, visible));
        Assert.AreEqual(3, V6SelectViewLogic.ClampScroll(3, rows, visible));
        Assert.AreEqual(rows - visible, V6SelectViewLogic.ClampScroll(999, rows, visible));
        Assert.AreEqual(rows - visible, V6SelectViewLogic.ClampScroll(rows - visible, rows, visible));
    }

    [Test]
    public void ScrollToReveal_KeepsEveryRowVisible_DuringFullTraversal()
    {
        int rows = SyntheticSongCount;
        int visible = V6SelectViewLogic.ListVisibleRows;

        int scroll = 0;
        for (int index = 0; index < rows; index++)
        {
            int next = V6SelectViewLogic.ScrollToReveal(index, scroll, visible);
            next = V6SelectViewLogic.ClampScroll(next, rows, visible);

            Assert.GreaterOrEqual(index, next, "行 " + index + " が窓の上で見えない (scroll=" + next + ")");
            Assert.Less(index, next + visible, "行 " + index + " が窓の下で見えない (scroll=" + next + ")");

            scroll = next;
        }

        // 末尾まで到達すると最大スクロール位置で止まる
        Assert.AreEqual(rows - visible, scroll);
    }

    [Test]
    public void ScrollToReveal_IsIdempotent_WhenIndexIsAlreadyVisible()
    {
        int rows = SyntheticSongCount;
        int visible = V6SelectViewLogic.ListVisibleRows;

        for (int scroll = 0; scroll <= rows - visible; scroll++)
        {
            for (int index = scroll; index < scroll + visible; index++)
            {
                Assert.AreEqual(scroll, V6SelectViewLogic.ScrollToReveal(index, scroll, visible),
                    "見えている行 " + index + " (scroll=" + scroll + ") でスクロールが動いた");
            }
        }
    }

    [Test]
    public void ScrollToReveal_Upward_AlignsToTopEdge()
    {
        int visible = V6SelectViewLogic.ListVisibleRows;

        // 下までスクロールした状態から上へ戻るとき、対象行が窓の先頭に来る
        Assert.AreEqual(10, V6SelectViewLogic.ScrollToReveal(10, 40, visible));
        Assert.AreEqual(0, V6SelectViewLogic.ScrollToReveal(0, 40, visible));
    }

    [Test]
    public void CenterScroll_PutsSelectionInTheMiddleSlot_ForEverySong()
    {
        int rows = SyntheticSongCount;
        int visible = V6SelectViewLogic.ListVisibleRows;

        for (int index = 0; index < rows; index++)
        {
            int scroll = V6SelectViewLogic.CenterScroll(index, rows, visible);

            Assert.AreEqual(index - visible / 2, scroll, "行 " + index + " の中央寄せ位置");

            // 先頭 / 末尾では負値・上限超えを許すが、クランプ後も選択行は見えている
            int clamped = V6SelectViewLogic.ClampScroll(scroll, rows, visible);
            Assert.GreaterOrEqual(index, clamped);
            Assert.Less(index, clamped + visible);
        }
    }

    [Test]
    public void CenterScroll_OutOfRangeIndex_StaysInBounds()
    {
        int rows = SyntheticSongCount;
        int visible = V6SelectViewLogic.ListVisibleRows;

        Assert.AreEqual(0, V6SelectViewLogic.CenterScroll(-1, rows, visible));
        Assert.AreEqual(rows - visible, V6SelectViewLogic.CenterScroll(rows, rows, visible));
        Assert.AreEqual(0, V6SelectViewLogic.CenterScroll(0, 3, visible), "曲数 < 可視行数なら 0");
    }

    [Test]
    public void DotRail_FitsInsideListPanel_At60Songs()
    {
        float railBottom = V6SelectViewLogic.DotBottom(SyntheticSongCount, 0f);

        // ドットは 1 曲 1 つ。60 曲でもリストパネルの高さ (780) に収まる
        Assert.LessOrEqual(railBottom, V6SelectViewLogic.ListH,
            "60 曲のドットレール (" + railBottom + ") がリストパネル (" + V6SelectViewLogic.ListH + ") からはみ出す");

        float[] offsets = V6SelectViewLogic.DotOffsets(SyntheticSongCount, 0f);
        Assert.AreEqual(SyntheticSongCount, offsets.Length);
        for (int i = 1; i < offsets.Length; i++)
        {
            float step = offsets[i] - offsets[i - 1];
            Assert.GreaterOrEqual(step, V6SelectViewLogic.DotSize, "ドット間隔がドット径より狭い (i=" + i + ")");
        }

        // アクティブ index は範囲外でも端に留まる (60 曲の最後 / 先頭)
        Assert.AreEqual(SyntheticSongCount - 1, V6SelectViewLogic.DotActiveIndex(SyntheticSongCount - 1, SyntheticSongCount));
        Assert.AreEqual(0, V6SelectViewLogic.DotActiveIndex(-1, SyntheticSongCount));
        Assert.AreEqual(SyntheticSongCount - 1, V6SelectViewLogic.DotActiveIndex(999, SyntheticSongCount));
    }

    [Test]
    public void VisibleRows_FitInsideListPanel()
    {
        int visible = V6SelectViewLogic.ListVisibleRows;

        V6Rect lastRow = V6SelectViewLogic.ListRowRect(visible - 1);
        V6Rect frame = V6SelectViewLogic.ToFrame(V6SelectViewLogic.ListPanelRect(), lastRow);

        Assert.LessOrEqual(lastRow.Bottom, V6SelectViewLogic.ListH,
            "9 行目の下端 (" + lastRow.Bottom + ") がリストパネル (" + V6SelectViewLogic.ListH + ") を超える");
        Assert.LessOrEqual(frame.Bottom, V6SelectViewLogic.DesignH,
            "9 行目がフレーム (972) からはみ出す");
        Assert.LessOrEqual(frame.Right, V6SelectViewLogic.DesignW);
        Assert.IsTrue(V6SelectViewLogic.ListPanelRect().Contains(frame));
    }

    [Test]
    public void ReadyAvailable_FollowsFilteredList_AtScale()
    {
        var songs = BuildSyntheticCatalog();
        List<V6SongEntry> filtered = V6SelectDataLogic.Filter(songs, "song 1"); // id 10..19

        Assert.IsTrue(V6SelectViewLogic.ReadyAvailable(songs[9], filtered), "id 10 は絞り込み結果に含まれる");
        Assert.IsTrue(V6SelectViewLogic.ReadyAvailable(songs[18], filtered), "id 19 は絞り込み結果に含まれる");
        Assert.IsFalse(V6SelectViewLogic.ReadyAvailable(songs[0], filtered), "id 1 は絞り込み結果に含まれない");
        Assert.IsFalse(V6SelectViewLogic.ReadyAvailable(songs[19], filtered), "id 20 は絞り込み結果に含まれない");

        // 絞り込み結果の先頭を選んだ状態は常に確定可能
        Assert.IsTrue(V6SelectViewLogic.ReadyAvailable(filtered[0], filtered));
    }
}
