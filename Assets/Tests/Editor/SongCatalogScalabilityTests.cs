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
///   4. ドットレールが **要件の 45 曲** までパネル内に収まり、
///      レール容量を超える曲数では **表示窓** で頭打ちになって
///      選択曲が常に見えること (Issue #222 の窓付きレール)
///   5. ReadyAvailable が絞り込み後リストに対して判定される
///
/// ドットレールは 1 曲 1 ドットの design (AqFtx / OENRv) を保ちつつ、
/// 曲数がレール容量を超えるときは選択曲を中心とする表示窓で対応する
/// (`V6SelectViewLogic.DotRailCapacity` / `DotRailWindowStart`)。
/// 容量以内の曲数では従来どおり全ドットを表示する (実カタログ 29 曲の
/// 外観は変わらない)。
///
/// 少数件での同じ関数の挙動 (ClampScroll / ScrollToReveal / DotActiveIndex /
/// ReadyAvailable / Filter) は `Assets/Game/Tests/V6SelectViewLogicTests.cs` と
/// `V6SelectDataLogicTests.cs` が既に固定している。このファイルは **件数を 45 件以上に
/// したときの性質** だけを見る (小さい件数の重複は意図的: 件数依存の退行を検出する)。
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

            // 先頭 / 末尾では負値・上限超えを許すが、クランプ後も選択行は見えている
            int clamped = V6SelectViewLogic.ClampScroll(scroll, rows, visible);
            Assert.GreaterOrEqual(index, clamped, "行 " + index + " が窓の上で見えない");
            Assert.Less(index, clamped + visible, "行 " + index + " が窓の下で見えない");

            // 窓がカタログ内に収まる index では、選択行の上下に同じ行数が見える
            // (窓の中央に来ている)。端では上記のとおり「見えている」ことだけを要求する。
            bool windowInside = index >= visible / 2 && index <= rows - 1 - visible / 2;
            if (windowInside)
            {
                int above = index - clamped;
                int below = clamped + visible - 1 - index;
                Assert.AreEqual(above, below,
                    "行 " + index + " が窓の中央に来ていない (上 " + above + " 行 / 下 " + below + " 行)");
            }
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

    /// <summary>
    /// ドットレールの実配置での下端。`V6SelectView.UpdateDots` はリストパネル内の
    /// `DotRailTopY(visible)` に、高さ `DotBottom(visible, 0f)` のコンテナを置く
    /// (Issue #427 の上下中央寄せ + Issue #222 の表示窓)。
    /// </summary>
    private static float DotRailBottom(int count)
    {
        int visible = V6SelectViewLogic.DotRailVisibleCount(count);
        return V6SelectViewLogic.DotRailTopY(visible) + V6SelectViewLogic.DotBottom(visible, 0f);
    }

    [Test]
    public void DotRail_FitsInsideListPanel_ForRequired45Songs()
    {
        // #222 の要件は 45 曲。実配置 (上下中央寄せ) でパネル内に収まること
        Assert.LessOrEqual(DotRailBottom(45), V6SelectViewLogic.ListH,
            "45 曲のドットレール (" + DotRailBottom(45) + ") がリストパネル ("
            + V6SelectViewLogic.ListH + ") からはみ出す");
    }

    [Test]
    public void DotRail_FitsInsideListPanel_ForAnyCatalogSize()
    {
        // Issue #222: 窓付きレール導入後は、曲数がレール容量を超えても
        // 表示ドット数が容量で頭打ちになるため、任意の曲数でパネル内に収まる。
        int[] counts = { 0, 1, 9, 54, 55, 60, 65, 66, 100, 200 };
        foreach (int count in counts)
        {
            Assert.LessOrEqual(DotRailBottom(count), V6SelectViewLogic.ListH,
                count + " 曲のドットレールがリストパネルからはみ出す");
        }
    }

    [Test]
    public void DotRail_Capacity_IsTheMaxDotsThatFitThePanel()
    {
        int capacity = V6SelectViewLogic.DotRailCapacity();

        // 容量ちょうどは収まり、1 個多いと収まらない (実配置で検証)。
        Assert.LessOrEqual(V6SelectViewLogic.DotBottom(capacity, 0f), V6SelectViewLogic.ListH);
        Assert.Greater(V6SelectViewLogic.DotBottom(capacity + 1, 0f), V6SelectViewLogic.ListH);
        Assert.GreaterOrEqual(capacity, 45, "要件の 45 曲は全ドット表示できること");
    }

    [Test]
    public void DotRailWindow_WithinCapacity_ShowsAllDots()
    {
        int capacity = V6SelectViewLogic.DotRailCapacity();

        foreach (int count in new[] { 0, 1, 9, 54, capacity })
        {
            Assert.AreEqual(0, V6SelectViewLogic.DotRailWindowStart(count, 0),
                count + " 曲 (容量以内) で窓が動いた");
            Assert.AreEqual(count < capacity ? count : capacity,
                V6SelectViewLogic.DotRailVisibleCount(count), count + " 曲の表示数");
        }
    }

    [Test]
    public void DotRailWindow_ActiveSongStaysVisible_AtBoundaries()
    {
        int capacity = V6SelectViewLogic.DotRailCapacity();
        foreach (int count in new[] { 66, 100, 200 })
        {
            // 先頭 / 中央 / 末尾の選択で、アクティブなドットが必ず窓内にある。
            foreach (int active in new[] { 0, count / 2, count - 1 })
            {
                int start = V6SelectViewLogic.DotRailWindowStart(count, active);
                int visible = V6SelectViewLogic.DotRailVisibleCount(count);
                int local = V6SelectViewLogic.DotRailActiveLocal(active, start, visible);

                Assert.GreaterOrEqual(start, 0, "窓先頭が負");
                Assert.LessOrEqual(start, count - visible, "窓先頭が末尾を超える");
                Assert.GreaterOrEqual(local, 0, "アクティブ " + active + " が窓の上で見えない");
                Assert.Less(local, visible, "アクティブ " + active + " が窓の下で見えない");
            }
        }
    }

    [Test]
    public void DotRailWindow_CentersSelection_AndClampsAtEdges()
    {
        int capacity = V6SelectViewLogic.DotRailCapacity();
        const int count = 100;

        // 中央の選択は窓の中央に来る
        int mid = V6SelectViewLogic.DotRailWindowStart(count, 50);
        Assert.AreEqual(50 - capacity / 2, mid);

        // 先頭 / 末尾では窓が端に留まる
        Assert.AreEqual(0, V6SelectViewLogic.DotRailWindowStart(count, 0));
        Assert.AreEqual(count - capacity, V6SelectViewLogic.DotRailWindowStart(count, count - 1));

        // 選択が無いときは先頭側の窓
        Assert.AreEqual(0, V6SelectViewLogic.DotRailWindowStart(count, -1));
    }

    [Test]
    public void DotRailWindow_ScrollsWithSelection_WithoutRebuildNeed()
    {
        // 選択を 1 曲ずつ末尾まで動かしても、表示ドット数は容量で一定、
        // アクティブなドットは常に窓内 (連続したスクロール追従)。
        const int count = 100;
        int capacity = V6SelectViewLogic.DotRailCapacity();
        int visible = V6SelectViewLogic.DotRailVisibleCount(count);
        Assert.AreEqual(capacity, visible);

        for (int active = 0; active < count; active++)
        {
            int start = V6SelectViewLogic.DotRailWindowStart(count, active);
            int local = V6SelectViewLogic.DotRailActiveLocal(active, start, visible);
            Assert.GreaterOrEqual(local, 0);
            Assert.Less(local, visible);
        }
    }

    [Test]
    public void DotRail_SpacingIsDotSizePlusGap_AndActiveIndexClamps()
    {
        float[] offsets = V6SelectViewLogic.DotOffsets(SyntheticSongCount, 0f);
        Assert.AreEqual(SyntheticSongCount, offsets.Length);
        for (int i = 1; i < offsets.Length; i++)
        {
            Assert.AreEqual(V6SelectViewLogic.DotSize + V6SelectViewLogic.DotGap, offsets[i] - offsets[i - 1],
                "ドット間隔 (i=" + i + ")");
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
