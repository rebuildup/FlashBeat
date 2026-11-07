using System;
using System.Collections;
using System.Net;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FlashBeat.Tests.Editor
{
    // Cloud Catalog (#17): catalog parsing, fixed-catalog fallback, chart
    // version pinning and 404 handling. Network behaviour is exercised for
    // real against a loopback HTTP listener so UnityWebRequest paths (200 and
    // 404) are covered without touching the deployed worker.
    public class CloudCatalogTests
    {
        [TearDown]
        public void TearDown()
        {
            GManager.ClearRemoteCatalog();
            CloudChartHolder.Clear();
            PlaySessionSnapshot.Clear(); // Issue #183: ResetSession がセッションを固定するようになったので掃除する
        }

        // --- catalog parsing ------------------------------------------------

        [Test]
        public void ParseCatalog_MapsCatalogToSongDataWithPlaceholder()
        {
            string json = "{\"catalogVersion\":3,\"songs\":[" +
                          "{\"id\":\"test-song\",\"title\":\"テスト曲\",\"artist\":\"テスト作曲者\",\"youtubeId\":\"dQw4w9WgXcQ\"," +
                          "\"level\":5,\"bpm\":120,\"durationSec\":90.5,\"chartVersion\":2,\"noteCount\":300}]}";

            SongData[] songs = CloudCatalog.ParseCatalog(json);

            Assert.IsNotNull(songs);
            Assert.AreEqual(2, songs.Length);
            Assert.AreEqual("noSong", songs[0].title, "index 0 must stay the noSong placeholder");
            Assert.AreEqual(1, songs[1].id);
            Assert.AreEqual("テスト曲", songs[1].title);
            Assert.AreEqual("テスト作曲者", songs[1].musician);
            Assert.AreEqual("dQw4w9WgXcQ", songs[1].videoId);
            Assert.AreEqual(120, songs[1].bpm);
            Assert.AreEqual(5, songs[1].level);
            Assert.AreEqual(300, songs[1].totalHits);
            Assert.AreEqual(90.5f, songs[1].duration, 0.001f);
            Assert.IsTrue(songs[1].isRemote);
            Assert.AreEqual("test-song", songs[1].remoteId);
            Assert.AreEqual(2, songs[1].chartVersion, "chartVersion must be kept for pinning");
        }

        [Test]
        public void ParseCatalog_EmptySongsReturnsNull()
        {
            // The deployed worker currently returns exactly this shape before
            // #16 publishes data; the game must fall back to the fixed catalog.
            Assert.IsNull(CloudCatalog.ParseCatalog("{\"catalogVersion\":0,\"songs\":[]}"));
        }

        [Test]
        public void ParseCatalog_MalformedJsonReturnsNull()
        {
            Assert.IsNull(CloudCatalog.ParseCatalog("{not json"));
        }

        [Test]
        public void ParseCatalog_BlankOrNullReturnsNull()
        {
            Assert.IsNull(CloudCatalog.ParseCatalog(null));
            Assert.IsNull(CloudCatalog.ParseCatalog(""));
        }

        [Test]
        public void ParseCatalog_SkipsEntriesMissingRequiredFields()
        {
            string json = "{\"songs\":[" +
                          "{\"id\":\"Bad Id\",\"title\":\"space in id\",\"youtubeId\":\"dQw4w9WgXcQ\"}," +
                          "{\"id\":\"no-youtube\",\"title\":\"曲\",\"youtubeId\":\"short\"}," +
                          "{\"id\":\"good-song\",\"title\":\"正しい曲\",\"youtubeId\":\"dQw4w9WgXcQ\",\"chartVersion\":4}" +
                          "]}";

            SongData[] songs = CloudCatalog.ParseCatalog(json);

            Assert.IsNotNull(songs);
            Assert.AreEqual(2, songs.Length, "placeholder + the only valid entry");
            Assert.AreEqual("正しい曲", songs[1].title);
            Assert.AreEqual(4, songs[1].chartVersion);
        }

        [Test]
        public void ParseCatalog_AllEntriesInvalidReturnsNull()
        {
            string json = "{\"songs\":[{\"id\":\"X\",\"title\":\"\",\"youtubeId\":\"dQw4w9WgXcQ\"}]}";
            Assert.IsNull(CloudCatalog.ParseCatalog(json));
        }

        // --- fixed fallback semantics ----------------------------------------

        [Test]
        public void ActiveSongs_FallsBackToFixedCatalogByDefault()
        {
            Assert.IsFalse(GManager.HasRemoteCatalog);
            Assert.AreSame(GManager.Songs, GManager.ActiveSongs);
            Assert.AreEqual(GManager.Songs.Length - 1, GManager.totalSong);
        }

        [Test]
        public void ApplyRemoteCatalog_KeepsEverySongAndAddsTheCloudOnes()
        {
            SongData[] remote = CloudCatalog.ParseCatalog(
                "{\"songs\":[{\"id\":\"remote-one\",\"title\":\"リモート曲\",\"youtubeId\":\"dQw4w9WgXcQ\",\"chartVersion\":7}]}");
            Assert.IsNotNull(remote);

            GManager.ApplyRemoteCatalog(remote);

            Assert.IsTrue(GManager.HasRemoteCatalog);
            // Every song of the original data stays selectable; the cloud song
            // is added at the end of the list.
            Assert.AreEqual(GManager.Songs.Length + 1, GManager.ActiveSongs.Length);
            Assert.AreEqual(GManager.ActiveSongs.Length - 1, GManager.totalSong);
            Assert.AreEqual("ロストアンブレラ", GManager.GetSong(1).title);

            int added = GManager.ActiveSongs.Length - 1;
            Assert.AreEqual("リモート曲", GManager.GetSong(added).title);
            Assert.IsTrue(GManager.GetSong(added).isRemote);
        }

        [Test]
        public void ClearRemoteCatalog_RestoresFixedCatalog()
        {
            GManager.ApplyRemoteCatalog(CloudCatalog.ParseCatalog(
                "{\"songs\":[{\"id\":\"remote-one\",\"title\":\"リモート曲\",\"youtubeId\":\"dQw4w9WgXcQ\"}]}"));
            Assert.IsTrue(GManager.HasRemoteCatalog);

            GManager.ClearRemoteCatalog();

            Assert.IsFalse(GManager.HasRemoteCatalog);
            Assert.AreSame(GManager.Songs, GManager.ActiveSongs);
            Assert.AreEqual("ロストアンブレラ", GManager.GetSong(1).title);
        }

        // --- chart URL pinning ------------------------------------------------

        [Test]
        public void ChartUrl_PinsExactVersion()
        {
            string url = CloudCatalog.ChartUrl("https://example.workers.dev", "test-song", 7);
            Assert.AreEqual("https://example.workers.dev/api/v1/charts/test-song?version=7", url);
        }

        [Test]
        public void ChartUrl_TrimsTrailingSlash()
        {
            string url = CloudCatalog.ChartUrl("https://example.workers.dev/", "test-song", 1);
            Assert.IsFalse(url.Contains("//api"), url);
        }

        // --- chart response parsing -------------------------------------------

        [Test]
        public void ParseChartResponse_ExtractsChart()
        {
            string json = "{\"songId\":\"test-song\",\"chartVersion\":2,\"chart\":{" +
                          "\"name\":\"テスト曲\",\"maxBlock\":8,\"BPM\":120,\"offset\":0," +
                          "\"notes\":[{\"type\":1,\"num\":0,\"block\":3,\"LPB\":4}]}}";

            Data chart = CloudCatalog.ParseChartResponse(json);

            Assert.IsNotNull(chart);
            Assert.AreEqual("テスト曲", chart.name);
            Assert.AreEqual(120, chart.BPM);
            Assert.AreEqual(1, chart.notes.Length);
            Assert.AreEqual(3, chart.notes[0].block);
        }

        [Test]
        public void ParseChartResponse_MalformedReturnsNull()
        {
            Assert.IsNull(CloudCatalog.ParseChartResponse("{broken"));
        }

        [Test]
        public void ParseChartResponse_ErrorBodyReturnsNull()
        {
            // A 404 body (or any body without a usable chart) must not throw and
            // must not produce a chart; the caller warns and stays put.
            Assert.IsNull(CloudCatalog.ParseChartResponse("{\"error\":\"chart not found\"}"));
            Assert.IsNull(CloudCatalog.ParseChartResponse(null));
        }

        [Test]
        public void ParseChartResponse_EmptyNotesReturnsNull()
        {
            string json = "{\"songId\":\"test-song\",\"chartVersion\":1,\"chart\":{" +
                          "\"name\":\"空\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[]}}";
            Assert.IsNull(CloudCatalog.ParseChartResponse(json));
        }

        // --- chart snapshot holder ---------------------------------------------

        [Test]
        public void ChartHolder_RoundtripAndVersionMismatch()
        {
            CloudChartHolder.Hold("test-song", 7, "{\"chart\":{}}");

            Assert.IsTrue(CloudChartHolder.TryGet("test-song", 7, out string json));
            Assert.AreEqual("{\"chart\":{}}", json);

            Assert.IsFalse(CloudChartHolder.TryGet("test-song", 8, out _), "different version must not match");
            Assert.IsFalse(CloudChartHolder.TryGet("other-song", 7, out _), "different song must not match");
            Assert.IsFalse(CloudChartHolder.TryGet(null, 7, out _));
        }

        // --- config --------------------------------------------------------------

        [Test]
        public void ParseConfig_MissingFieldsUseDefaults()
        {
            // A config without other fields keeps the defaults (enabled, chartVersion 1).
            CloudCatalog.Config config = CloudCatalog.ParseConfig("{\"baseUrl\":\"https://x.example\"}");

            Assert.IsNotNull(config);
            Assert.IsTrue(config.enabled);
            Assert.AreEqual(1, config.chartVersion);
        }

        [Test]
        public void ParseConfig_BlankBaseUrlReturnsNull()
        {
            Assert.IsNull(CloudCatalog.ParseConfig("{\"baseUrl\":\"\"}"));
            Assert.IsNull(CloudCatalog.ParseConfig("{\"enabled\":false,\"baseUrl\":\"https://x.example\"}"));
        }

        [Test]
        public void LoadConfig_MissingResourceReturnsNull()
        {
            // With no CloudCatalogConfig resource the whole feature is inert and
            // the game runs on the fixed catalog only.
            TextAsset asset = Resources.Load<TextAsset>("CloudCatalogConfig_DefinitelyMissing");
            Assert.IsNull(asset, "precondition");
            Assert.IsNull(CloudCatalog.ParseConfig(null));
        }

        // --- real UnityWebRequest against a loopback listener ---------------------

        [UnityTest]
        public IEnumerator FetchText_ReportsBodyAndStatus_On200()
        {
            using (var server = new LocalHttpServer())
            {
                server.Status = 200;
                server.Body = "{\"catalogVersion\":1,\"songs\":[" +
                              "{\"id\":\"loop-song\",\"title\":\"ループ曲\",\"youtubeId\":\"dQw4w9WgXcQ\",\"chartVersion\":3}]}";
                server.Start();

                int status = 0;
                string body = null;
                yield return Pump(CloudCatalog.GetText(server.Url, (code, text) => { status = code; body = text; }));

                Assert.AreEqual(200, status);
                Assert.IsNotNull(body);
                SongData[] songs = CloudCatalog.ParseCatalog(body);
                Assert.IsNotNull(songs);
                Assert.AreEqual("ループ曲", songs[1].title);
                Assert.AreEqual(3, songs[1].chartVersion);
            }
        }

        [UnityTest]
        public IEnumerator FetchText_Reports404_ForMissingChart()
        {
            using (var server = new LocalHttpServer())
            {
                server.Status = 404;
                server.Body = "{\"error\":\"chart not found\"}";
                server.Start();

                int status = 0;
                string body = null;
                yield return Pump(CloudCatalog.GetText(server.Url, (code, text) => { status = code; body = text; }));

                Assert.AreEqual(404, status, "the 404 status must reach the caller");
                Assert.IsNull(body, "a non-200 response must not hand a body to the caller");

                // The caller-side 404 handling: no chart, no throw, fallback stays.
                Data chart = CloudCatalog.ParseChartResponse(body);
                Assert.IsNull(chart);
            }
        }

        static IEnumerator Pump(IEnumerator routine)
        {
            while (true)
            {
                if (!routine.MoveNext()) yield break;
                var async = routine.Current as UnityEngine.Networking.UnityWebRequestAsyncOperation;
                if (async != null)
                {
                    while (!async.isDone) yield return null;
                    continue;
                }
                yield return routine.Current;
            }
        }

        sealed class LocalHttpServer : IDisposable
        {
            HttpListener listener;
            Thread worker;

            public int Status = 200;
            public string Body = "{}";
            public string Url { get; private set; }

            public void Start()
            {
                int port;
                var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
                probe.Start();
                port = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();

                listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                listener.Start();
                Url = $"http://127.0.0.1:{port}/test";

                worker = new Thread(ServeOnce) { IsBackground = true };
                worker.Start();
            }

            void ServeOnce()
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    byte[] bytes = Encoding.UTF8.GetBytes(Body);
                    context.Response.StatusCode = Status;
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = bytes.Length;
                    context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    context.Response.OutputStream.Close();
                }
                catch (Exception)
                {
                    // listener stopped before serving: nothing to do in a test
                }
            }

            public void Dispose()
            {
                try { listener?.Stop(); } catch (Exception) { }
                try { listener?.Close(); } catch (Exception) { }
            }
        }
    }
}
