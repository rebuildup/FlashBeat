using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FlashBeat.Tests.Editor
{
    // Issue #789: 歌詞パネルが「歌詞データがあるのに出ない」不具合の根本原因は、
    // GameSceneManager.LoadTextData がローカル Resources しか読まず、
    // クラウドカタログ曲 (リモート) の固定譜面 JSON にある lyrics ブロックを
    // 一度も参照していなかったこと。ChartLyrics はノーツ生成と同じ優先順
    // (PlaySessionSnapshot → CloudChartHolder → Resources) で歌詞を解決し、
    // 「未到着 / なし確定 / あり」を明示する。
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
        public void LocalSong_UsesResourcesChart()
        {
            // チュートリアルは合成 fixture で歌詞を持つローカル譜面。
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(LocalSong("チュートリアル"), 0, null);

            ChartLyricsResult result = ChartLyrics.Resolve(session.Song);

            Assert.AreEqual(ChartLyricsState.HasLyrics, result.State);
            Assert.Greater(result.Lyrics.Mondai.Length, 0);
        }

        [Test]
        public void LocalSongWithoutLyrics_ResolvesNoLyrics()
        {
            // Resources に存在しないタイトル = 歌詞データなし確定。
            // LoadTextData は見つからない曲名に Error を出す (想定内)。
            LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("No text data for"));
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(LocalSong("存在しない曲名xyz"), 0, null);

            ChartLyricsResult result = ChartLyrics.Resolve(session.Song);

            Assert.AreEqual(ChartLyricsState.NoLyrics, result.State);
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
