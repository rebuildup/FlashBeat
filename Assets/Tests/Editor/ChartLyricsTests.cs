using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests.Editor
{
    // Issue #789: 歌詞パネルが「歌詞データがあるのに出ない」不具合の根本原因は、
    // GameSceneManager.LoadTextData がローカル Resources しか読まず、
    // クラウドカタログ曲 (リモート) の固定譜面 JSON にある lyrics ブロックを
    // 一度も参照していなかったこと。ChartLyrics はノーツ生成と同じ優先順
    // (PlaySessionSnapshot → CloudChartHolder) で歌詞を解決し、
    // 「未到着 / なし確定 / あり」を明示する。Issue #823 でローカル
    // Resources 経路は撤去済み。
    public class ChartLyricsTests
    {
        [SetUp]
        public void SetUp()
        {
            PlaySessionSnapshot.Clear();
            CloudChartHolder.Clear();
            ChartLyrics.Invalidate();
        }

        [TearDown]
        public void TearDown()
        {
            PlaySessionSnapshot.Clear();
            CloudChartHolder.Clear();
            ChartLyrics.Invalidate();
        }

        static SongData RemoteSong()
        {
            return new SongData(1, "リモート曲", "artist", "abcdefghijk", 120, 5, 100, 60f)
            {
                isRemote = true,
                remoteId = "remote-song-1",
                chartVersion = 3,
            };
        }

        static SongData LocalSong(string title)
        {
            return new SongData(10, title, "artist", "abcdefghijk", 120, 5, 100, 60f);
        }

        /// <summary>API エンベロープ形の譜面 JSON (lyrics あり)。</summary>
        const string EnvelopeWithLyrics =
            "{\"songId\":\"remote-song-1\",\"chartVersion\":3,\"chart\":{" +
            "\"name\":\"リモート曲\",\"maxBlock\":8,\"BPM\":120,\"offset\":0," +
            "\"notes\":[{\"type\":1,\"num\":0,\"block\":0,\"LPB\":4,\"endNum\":0}]," +
            "\"lyrics\":{\"startTime\":[1.0,5.0],\"endTime\":[3.0,7.0]," +
            "\"mondai\":[\"あ\",\"い\"],\"furigana\":[\"あ\",\"い\"],\"romaji\":[\"a\",\"i\"]}" +
            "}}";

        /// <summary>API エンベロープ形の譜面 JSON (lyrics なし)。</summary>
        const string EnvelopeWithoutLyrics =
            "{\"songId\":\"remote-song-1\",\"chartVersion\":3,\"chart\":{" +
            "\"name\":\"リモート曲\",\"maxBlock\":8,\"BPM\":120,\"offset\":0," +
            "\"notes\":[{\"type\":1,\"num\":0,\"block\":0,\"LPB\":4,\"endNum\":0}]" +
            "}}";

        [Test]
        public void RemoteChartWithLyrics_ResolvesHasLyrics()
        {
            SongData song = RemoteSong();
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(song, 3, EnvelopeWithLyrics);

            ChartLyricsResult result = ChartLyrics.Resolve(session.Song);

            Assert.AreEqual(ChartLyricsState.HasLyrics, result.State);
            Assert.IsNotNull(result.Lyrics);
            Assert.AreEqual(new[] { "あ", "い" }, result.Lyrics.Mondai);
            Assert.AreEqual(new[] { 1f, 5f }, result.Lyrics.StartTime);
        }

        [Test]
        public void RemoteChartWithoutLyrics_ResolvesNoLyrics()
        {
            SongData song = RemoteSong();
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(song, 3, EnvelopeWithoutLyrics);

            ChartLyricsResult result = ChartLyrics.Resolve(session.Song);

            Assert.AreEqual(ChartLyricsState.NoLyrics, result.State);
            Assert.IsNotNull(result.Lyrics, "なし確定でも Lyrics は空データを返す (未到着と区別)");
            Assert.AreEqual(0, result.Lyrics.Mondai.Length);
        }

        [Test]
        public void RemoteChartJsonWithEmptyMondai_ResolvesNoLyrics()
        {
            const string json =
                "{\"songId\":\"remote-song-1\",\"chartVersion\":3,\"chart\":{" +
                "\"name\":\"リモート曲\",\"maxBlock\":8,\"BPM\":120,\"offset\":0," +
                "\"notes\":[{\"type\":1,\"num\":0,\"block\":0,\"LPB\":4,\"endNum\":0}]," +
                "\"lyrics\":{\"mondai\":[]}" +
                "}}";
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(RemoteSong(), 3, json);

            ChartLyricsResult result = ChartLyrics.Resolve(session.Song);

            Assert.AreEqual(ChartLyricsState.NoLyrics, result.State);
        }

        [Test]
        public void RemoteSongWithoutChart_StaysPending()
        {
            SongData song = RemoteSong();
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(song, 0, null);

            ChartLyricsResult result = ChartLyrics.Resolve(session.Song);

            Assert.AreEqual(ChartLyricsState.Pending, result.State);
            Assert.IsNull(result.Lyrics, "未到着は null (データなし確定と区別)");
        }

        [Test]
        public void Pending_IsNotCached_SoLateChartArrivalResolves()
        {
            // Issue #789: 譜面未到着で Pending を引いたあと、同じ
            // (セッション, 曲) のまま CloudChartHolder に譜面が届いても、
            // 古い Pending がキャッシュに残ってはいけない。
            SongData song = RemoteSong();
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(song, 0, null);

            Assert.AreEqual(ChartLyricsState.Pending,
                ChartLyrics.Resolve(session.Song).State);

            // 到着を模倣: ホルダーへ譜面を保持させて再解決する。
            CloudChartHolder.Hold("remote-song-1", 3, EnvelopeWithLyrics);
            ChartLyricsResult arrived = ChartLyrics.Resolve(session.Song);

            Assert.AreEqual(ChartLyricsState.HasLyrics, arrived.State,
                "Pending がキャッシュされると到着後も歌詞が永遠に出ない");
            Assert.AreEqual("あ", arrived.Lyrics.Mondai[0]);
        }

        [Test]
        public void RetryWithNewSession_ResolvesFromNewSnapshot()
        {
            // リトライ (ResetSession → Begin) は新しいスナップショットを作る。
            // 前セッションの解決結果が混線しないことを確認する。
            SongData song = RemoteSong();
            PlaySessionSnapshot first = PlaySessionSnapshot.Begin(song, 0, null);
            Assert.AreEqual(ChartLyricsState.Pending,
                ChartLyrics.Resolve(first.Song).State);

            PlaySessionSnapshot second = PlaySessionSnapshot.Begin(song, 3, EnvelopeWithLyrics);
            ChartLyricsResult result = ChartLyrics.Resolve(second.Song);

            Assert.AreEqual(ChartLyricsState.HasLyrics, result.State);
        }

        [Test]
        public void RemoteSongViaCloudChartHolder_ResolvesLyrics()
        {
            // スナップショットが無くてもホルダーの保持譜面から解決できる
            // (GameScene 到達前の経路・リトライ直後の確認用)。
            CloudChartHolder.Hold("remote-song-1", 3, EnvelopeWithLyrics);

            ChartLyricsResult result = ChartLyrics.Resolve(RemoteSong());

            Assert.AreEqual(ChartLyricsState.HasLyrics, result.State);
            Assert.AreEqual("あ", result.Lyrics.Mondai[0]);
        }

        [Test]
        public void LocalSong_ResolvesNoLyrics()
        {
            // Issue #823: ローカル実曲 Resources の譜面は撤去した。
            // リモート化も固定もされていない曲は「歌詞なし確定」。
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(LocalSong("チュートリアル"), 0, null);

            ChartLyricsResult result = ChartLyrics.Resolve(session.Song);

            Assert.AreEqual(ChartLyricsState.NoLyrics, result.State);
        }

        [Test]
        public void LocalSongWithoutLyrics_ResolvesNoLyrics()
        {
            // Resources に存在しないタイトルも同じく「歌詞なし確定」。
            // ローカルフォールバックは無いのでエラーログも出ない。
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(LocalSong("存在しない曲名xyz"), 0, null);

            ChartLyricsResult result = ChartLyrics.Resolve(session.Song);

            Assert.AreEqual(ChartLyricsState.NoLyrics, result.State);
        }

        // --- Issue #846: mondai はあるが表示区間を構築できない歌詞 ------
        //
        // v0.2.5 実機指摘: mondai 非空だけで HasLyrics にすると、startTime
        // 欠落・配列不揃い・再生範囲外のデータで「枠だけ出て本文が永久に
        // 空」になる。HasLyrics は LyricsTimeline.Build が再生範囲と重なる
        // 表示区間つきの行を 1 つ以上構築できるときだけ。

        /// <summary>mondai はあるが startTime が無い譜面 JSON。</summary>
        const string EnvelopeMondaiWithoutStartTime =
            "{\"songId\":\"remote-song-1\",\"chartVersion\":3,\"chart\":{" +
            "\"name\":\"リモート曲\",\"maxBlock\":8,\"BPM\":120,\"offset\":0," +
            "\"notes\":[{\"type\":1,\"num\":0,\"block\":0,\"LPB\":4,\"endNum\":0}]," +
            "\"lyrics\":{\"endTime\":[3.0,7.0],\"mondai\":[\"あ\",\"い\"]}" +
            "}}";

        [Test]
        public void MondaiWithoutStartTime_ResolvesNoLyrics()
        {
            // startTime が無いと LyricsTimeline.Build は 0 行を返す。
            // 旧仕様では mondai 非空 = HasLyrics で枠だけが出続けた。
            PlaySessionSnapshot session =
                PlaySessionSnapshot.Begin(RemoteSong(), 3, EnvelopeMondaiWithoutStartTime);

            ChartLyricsResult result = ChartLyrics.Resolve(session.Song);

            Assert.AreEqual(ChartLyricsState.NoLyrics, result.State,
                "表示区間を1行も構築できない歌詞は「なし確定」(Issue #846)");
            Assert.AreEqual(2, result.Lyrics.Mondai.Length,
                "Lyrics データ自体は保持する (本文が出ないだけで捨てない)");
        }

        [Test]
        public void RaggedStartTime_KeepsHasLyricsWhenALineIsDisplayable()
        {
            // mondai 2 行に対し startTime が 1 要素だけの不揃いデータ。
            // 短い方に合わせて 1 行は構築できるので HasLyrics のまま
            // (本文が出る行がある以上、枠を消す理由がない)。
            const string json =
                "{\"songId\":\"remote-song-1\",\"chartVersion\":3,\"chart\":{" +
                "\"name\":\"リモート曲\",\"maxBlock\":8,\"BPM\":120,\"offset\":0," +
                "\"notes\":[{\"type\":1,\"num\":0,\"block\":0,\"LPB\":4,\"endNum\":0}]," +
                "\"lyrics\":{\"startTime\":[1.0],\"endTime\":[3.0]," +
                "\"mondai\":[\"あ\",\"い\"]}" +
                "}}";
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(RemoteSong(), 3, json);

            ChartLyricsResult result = ChartLyrics.Resolve(session.Song);

            Assert.AreEqual(ChartLyricsState.HasLyrics, result.State);

            // HUD と同じ経路で行を構築し、表示区間のある行が 1 つあること。
            var lines = FlashBeat.UI.LyricsTimeline.Build(
                result.Lyrics.StartTime, result.Lyrics.EndTime,
                result.Lyrics.Mondai, null, null);
            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual("あ", lines[0].Text);
        }

        [Test]
        public void LyricLinesOutsidePlayRange_ResolveNoLyrics()
        {
            // 全行の区間が曲長 (RemoteSong = 60 秒) の外にあるデータ。
            // 再生中は 1 行も表示されないので「枠だけ出て永久に空」になる。
            const string json =
                "{\"songId\":\"remote-song-1\",\"chartVersion\":3,\"chart\":{" +
                "\"name\":\"リモート曲\",\"maxBlock\":8,\"BPM\":120,\"offset\":0," +
                "\"notes\":[{\"type\":1,\"num\":0,\"block\":0,\"LPB\":4,\"endNum\":0}]," +
                "\"lyrics\":{\"startTime\":[200.0,210.0],\"endTime\":[205.0,215.0]," +
                "\"mondai\":[\"あ\",\"い\"]}" +
                "}}";
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(RemoteSong(), 3, json);

            ChartLyricsResult result = ChartLyrics.Resolve(session.Song);

            Assert.AreEqual(ChartLyricsState.NoLyrics, result.State,
                "再生範囲と重ならない歌詞区間だけでは HasLyrics にしない");
        }

        [Test]
        public void LyricLineOverlappingPlayRangeBoundary_KeepsHasLyrics()
        {
            // 曲長をまたぐ区間 (50-70 秒、曲長 60 秒) は再生中に表示される
            // ので HasLyrics。境界判定が厳しすぎて実データを落とさないこと。
            const string json =
                "{\"songId\":\"remote-song-1\",\"chartVersion\":3,\"chart\":{" +
                "\"name\":\"リモート曲\",\"maxBlock\":8,\"BPM\":120,\"offset\":0," +
                "\"notes\":[{\"type\":1,\"num\":0,\"block\":0,\"LPB\":4,\"endNum\":0}]," +
                "\"lyrics\":{\"startTime\":[50.0],\"endTime\":[70.0]," +
                "\"mondai\":[\"おわり\"]}" +
                "}}";
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(RemoteSong(), 3, json);

            ChartLyricsResult result = ChartLyrics.Resolve(session.Song);

            Assert.AreEqual(ChartLyricsState.HasLyrics, result.State);
        }

        [Test]
        public void UnknownSongDuration_AnyDisplayableLineKeepsHasLyrics()
        {
            // 曲長不明 (duration <= 0) の曲は範囲チェックをしない。
            // 行が構築できれば HasLyrics (範囲外と誤判定しない)。
            SongData song = new SongData(1, "リモート曲", "artist", "abcdefghijk", 120, 5, 100, 0f)
            {
                isRemote = true,
                remoteId = "remote-song-1",
                chartVersion = 3,
            };
            const string json =
                "{\"songId\":\"remote-song-1\",\"chartVersion\":3,\"chart\":{" +
                "\"name\":\"リモート曲\",\"maxBlock\":8,\"BPM\":120,\"offset\":0," +
                "\"notes\":[{\"type\":1,\"num\":0,\"block\":0,\"LPB\":4,\"endNum\":0}]," +
                "\"lyrics\":{\"startTime\":[200.0],\"endTime\":[205.0]," +
                "\"mondai\":[\"とおい\"]}" +
                "}}";
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(song, 3, json);

            ChartLyricsResult result = ChartLyrics.Resolve(session.Song);

            Assert.AreEqual(ChartLyricsState.HasLyrics, result.State,
                "曲長不明では行の有無だけで判定する");
        }

        [Test]
        public void ResolvedLyrics_FlowThroughTimelineToDisplayableLine()
        {
            // 通常経路の契約確認: ChartLyrics.Resolve → LyricsTimeline.Build
            // → IndexAt が、HUD (V6HudLyrics) の本文表示が依存する
            // 「区間内では行が見つかり、区間外では -1」になること。
            PlaySessionSnapshot session =
                PlaySessionSnapshot.Begin(RemoteSong(), 3, EnvelopeWithLyrics);

            ChartLyricsResult result = ChartLyrics.Resolve(session.Song);
            Assert.AreEqual(ChartLyricsState.HasLyrics, result.State);

            var lines = FlashBeat.UI.LyricsTimeline.Build(
                result.Lyrics.StartTime, result.Lyrics.EndTime,
                result.Lyrics.Mondai, null, null);
            Assert.IsTrue(FlashBeat.UI.LyricsTimeline.HasLines(lines));

            Assert.AreEqual(0, FlashBeat.UI.LyricsTimeline.IndexAt(lines, 2f),
                "1 行目の区間 (1-3 秒) では 1 行目");
            Assert.AreEqual(1, FlashBeat.UI.LyricsTimeline.IndexAt(lines, 6f),
                "2 行目の区間 (5-7 秒) では 2 行目");
            Assert.AreEqual(-1, FlashBeat.UI.LyricsTimeline.IndexAt(lines, 4f),
                "行間 (3-5 秒) では本文なし (枠は #734 どおり出続ける)");
        }

        [Test]
        public void RawChartDocument_AlsoResolvesLyrics()
        {
            // ChartJsonOverride 等で生のチャート文書 (エンベロープ無し) が
            // 固定された場合も lyrics を読める。
            const string raw =
                "{\"name\":\"raw\",\"maxBlock\":8,\"BPM\":120,\"offset\":0," +
                "\"notes\":[{\"type\":1,\"num\":0,\"block\":0,\"LPB\":4,\"endNum\":0}]," +
                "\"lyrics\":{\"startTime\":[0.5],\"endTime\":[2.0],\"mondai\":[\"う\"]}" +
                "}";
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(LocalSong("チュートリアル"), 0, raw);

            ChartLyricsResult result = ChartLyrics.Resolve(session.Song);

            Assert.AreEqual(ChartLyricsState.HasLyrics, result.State);
            Assert.AreEqual("う", result.Lyrics.Mondai[0]);
        }
    }
}
