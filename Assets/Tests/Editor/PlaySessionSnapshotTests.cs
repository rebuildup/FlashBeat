using NUnit.Framework;

namespace FlashBeat.Tests.Editor
{
    // Issue #183: 1 プレイの identity (songId + chartVersion + スナップショット) を
    // 遷移開始時に固定する。選曲 → リモート譜面取得 → GameScene 遷移の間に
    // カタログの更新 (ApplyRemoteCatalog / ClearRemoteCatalog) や選択変更
    // (GManager.songID) が入っても、使用する譜面と曲情報・版が変わらないことを
    // ここで固定する。遷移の開始処理は SelectSceneManager → CloudChartHolder.Hold
    // → GManager.ResetSession の順で走るので、その呼び出し列をそのまま再現する。
    public class PlaySessionSnapshotTests
    {
        const string ChartV1 = "{\"songId\":\"who\",\"chartVersion\":1,\"chart\":{" +
                               "\"name\":\"Who\",\"maxBlock\":8,\"BPM\":128,\"offset\":0," +
                               "\"notes\":[{\"type\":1,\"num\":0,\"block\":3,\"LPB\":4,\"endNum\":0}]}}";
        const string ChartV2 = "{\"songId\":\"who\",\"chartVersion\":2,\"chart\":{" +
                               "\"name\":\"Who\",\"maxBlock\":8,\"BPM\":128,\"offset\":0," +
                               "\"notes\":[{\"type\":1,\"num\":0,\"block\":3,\"LPB\":4,\"endNum\":0}," +
                               "{\"type\":1,\"num\":4,\"block\":5,\"LPB\":4,\"endNum\":0}]}}";

        const string CatalogV1 = "{\"songs\":[{\"id\":\"who\",\"title\":\"Who\",\"artist\":\"Azari\"," +
                                 "\"youtubeId\":\"8JXiXt0D6tw\",\"chartVersion\":1,\"noteCount\":139}]}";
        const string CatalogV2 = "{\"songs\":[{\"id\":\"who\",\"title\":\"Who\",\"artist\":\"Azari\"," +
                                 "\"youtubeId\":\"8JXiXt0D6tw\",\"chartVersion\":2,\"noteCount\":141}]}";

        int originalSongId;

        [SetUp]
        public void SetUp()
        {
            originalSongId = GManager.songID;
            GManager.songID = 1;
            PlaySessionSnapshot.Clear();
            CloudChartHolder.Clear();
            GManager.ClearRemoteCatalog();
        }

        [TearDown]
        public void TearDown()
        {
            PlaySessionSnapshot.Clear();
            CloudChartHolder.Clear();
            GManager.ClearRemoteCatalog();
            GManager.songID = originalSongId;
        }

        static SongData RemoteSong(int id, string remoteId, int chartVersion)
        {
            return new SongData(id, "テスト曲", "テスト作曲者", "dQw4w9WgXcQ", 120, 5, 300, 90f)
            {
                isRemote = true,
                remoteId = remoteId,
                chartVersion = chartVersion,
                noteCount = 300,
            };
        }

        // --- 固定の基本 ---------------------------------------------------------

        [Test]
        public void Begin_PinsSongVersionAndChartJson()
        {
            SongData row = RemoteSong(1, "test-song", 1);

            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(row, 1, ChartV1);

            Assert.AreSame(session, PlaySessionSnapshot.Current);
            Assert.AreEqual(1, session.SongId);
            Assert.AreEqual("test-song", session.RemoteId);
            Assert.AreEqual(1, session.ChartVersion);
            Assert.AreNotSame(row, session.Song, "曲情報は複製して固定する (行の置き換えの影響を受けない)");
            Assert.AreEqual(row.title, session.Song.title);
            Assert.AreEqual(row.videoId, session.Song.videoId);
            Assert.IsTrue(PlaySessionSnapshot.TryGetChartJson(session.Song, out string json));
            Assert.AreEqual(ChartV1, json);
        }

        [Test]
        public void Begin_PinsTheGivenVersionOverTheRowVersion()
        {
            // 行が新しい版 (v2) に更新されたあとでも、取得済みの版 (v1) を固定できる。
            SongData row = RemoteSong(1, "test-song", 2);

            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(row, 1, ChartV1);

            Assert.AreEqual(1, session.ChartVersion, "プレイする譜面の版で固定する");
            Assert.AreEqual(1, session.Song.chartVersion);
        }

        [Test]
        public void Begin_NullSongReturnsNullAndKeepsTheCurrentSession()
        {
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(RemoteSong(1, "test-song", 1), 1, ChartV1);

            Assert.IsNull(PlaySessionSnapshot.Begin(null, 1, ChartV1));
            Assert.AreSame(session, PlaySessionSnapshot.Current);
        }

        // --- 受け入れ条件: カタログ更新・選択変更が走っても使用譜面が変わらない -----

        [Test]
        public void CatalogUpdate_AfterBegin_DoesNotChangeThePinnedChart()
        {
            // 開始時に who v1 を確定済み。
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(RemoteSong(3, "who", 1), 1, ChartV1);

            // 待ち時間にカタログが再公開され、行 3 が v2 に更新される
            // (SelectSceneManager.RefreshCatalogOnce → ApplyRemoteCatalog と同じ)。
            SongData[] newer = CloudCatalog.ParseCatalog(CatalogV2);
            Assert.IsNotNull(newer);
            GManager.ApplyRemoteCatalog(newer);
            Assert.AreEqual(2, GManager.ActiveSongs[3].chartVersion, "前提: カタログの行は v2 に更新されている");

            // それでも使用する譜面・版・曲情報は開始時のままでなければならない。
            Assert.AreEqual(1, session.ChartVersion, "カタログの更新で版がずれてはいけない");
            Assert.IsTrue(PlaySessionSnapshot.TryGetChartJson(session.Song, out string json));
            Assert.AreEqual(ChartV1, json, "使用する譜面は開始時に固定したスナップショット");
            Assert.AreSame(session.Song, PlaySessionSnapshot.ResolveSong(GManager.songID));
        }

        [Test]
        public void CatalogClear_AfterBegin_DoesNotChangeThePinnedChart()
        {
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(RemoteSong(1, "test-song", 1), 1, ChartV1);

            // 待ち時間にカタログ取得が失敗してローカルへ戻される
            // (RefreshCatalogOnce の失敗パス → ClearRemoteCatalog)。
            GManager.ClearRemoteCatalog();

            Assert.AreEqual(1, session.ChartVersion);
            Assert.IsTrue(PlaySessionSnapshot.TryGetChartJson(session.Song, out string json));
            Assert.AreEqual(ChartV1, json, "カタログが消えても取得済みの譜面は変わらない");
            Assert.AreSame(session.Song, PlaySessionSnapshot.ResolveSong(GManager.songID));
        }

        [Test]
        public void SelectionChange_AfterBegin_DoesNotChangeThePinnedSongOrChart()
        {
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(RemoteSong(3, "who", 1), 1, ChartV1);

            // フェード中に選択が別の曲へ動く (SelectView.HandleInput と同じ効果)。
            GManager.songID = 7;

            Assert.AreSame(session.Song, PlaySessionSnapshot.ResolveSong(GManager.songID),
                "選択が動いても GameScene が読む曲は開始時に固定した曲");
            Assert.AreEqual(1, session.ChartVersion);

            // GameScene の読み込み時 (NotesManager.OnEnable と同じ呼び出し) に
            // songID は固定した曲の行へ戻る。
            session.RestoreSongId();
            Assert.AreEqual(3, GManager.songID);
        }

        [Test]
        public void Issue183Timeline_CatalogUpdateAndSelectionChange_KeepTheHeldChart()
        {
            // 1. 選曲画面に who v1 のカタログが出ていて、行 3 を選んで決定。
            GManager.ApplyRemoteCatalog(CloudCatalog.ParseCatalog(CatalogV1));
            GManager.songID = 3;
            Assert.AreEqual(1, GManager.GetSong(3).chartVersion, "前提: 行 3 は who v1");

            // 2. GET 待機中にカタログが v2 へ更新される。
            GManager.ApplyRemoteCatalog(CloudCatalog.ParseCatalog(CatalogV2));
            Assert.AreEqual(2, GManager.GetSong(3).chartVersion, "前提: 行 3 は v2 に置き換わった");

            // 3. GET が完了。取得したのは v1。Hold して遷移開始
            //    (SelectSceneManager.StartRemoteSong と同じ順序)。
            CloudChartHolder.Hold("who", 1, ChartV1);
            GManager.ResetSession();

            // 4. GameScene が読む曲と譜面: 取得済みの v1 で固定されている。
            PlaySessionSnapshot session = PlaySessionSnapshot.Current;
            Assert.IsNotNull(session);
            Assert.AreEqual(3, session.SongId);
            Assert.AreEqual("who", session.RemoteId);
            Assert.AreEqual(1, session.ChartVersion, "行が v2 になっていても、取得済みの版で固定する");
            Assert.IsTrue(PlaySessionSnapshot.TryGetChartJson(session.Song, out string chart));
            Assert.AreEqual(ChartV1, chart);

            // 5. さらにフェード中に選択が動き、カタログも消える。
            GManager.songID = 7;
            GManager.ClearRemoteCatalog();

            // 6. GameScene 読み込み時の復元 (NotesManager.OnEnable と同じ呼び出し)。
            session.RestoreSongId();
            SongData song = PlaySessionSnapshot.ResolveSong(GManager.songID);

            Assert.AreEqual(3, GManager.songID, "選択の動きはプレイ中の曲へは影響しない");
            Assert.AreSame(session.Song, song);
            Assert.IsTrue(PlaySessionSnapshot.TryGetChartJson(song, out string played));
            Assert.AreEqual(ChartV1, played, "使用する譜面は最後まで開始時に固定した v1");
        }

        // --- 遷移開始のフック (GManager.ResetSession) -----------------------------

        [Test]
        public void ResetSession_PinsTheHeldChartVersion_OverTheNewerCatalogRow()
        {
            // Issue #183 の時系列: 開始時に取得した v1 が Holder にあり、行は v2。
            GManager.ApplyRemoteCatalog(CloudCatalog.ParseCatalog(CatalogV2));
            Assert.AreEqual(2, GManager.ActiveSongs[3].chartVersion, "前提: 行は v2");

            CloudChartHolder.Hold("who", 1, ChartV1);
            GManager.songID = 3;

            GManager.ResetSession(); // SelectSceneManager.StartRemoteSong と同じ呼び出し

            PlaySessionSnapshot session = PlaySessionSnapshot.Current;
            Assert.IsNotNull(session);
            Assert.AreEqual(3, session.SongId);
            Assert.AreEqual("who", session.RemoteId);
            Assert.AreEqual(1, session.ChartVersion, "遷移開始時には取得済みの版で固定する");
            Assert.AreEqual(1, session.Song.chartVersion);
            Assert.IsTrue(PlaySessionSnapshot.TryGetChartJson(session.Song, out string json));
            Assert.AreEqual(ChartV1, json);
        }

        [Test]
        public void ResetSession_LocalSong_PinsTheRowAndIgnoresTheStaleHolder()
        {
            // 前のプレイでリモート曲の譜面を Holder に残したまま、ローカル曲を開始する。
            CloudChartHolder.Hold("who", 1, ChartV1);
            GManager.songID = 1; // ロストアンブレラ (ローカル譜面)

            GManager.ResetSession();

            PlaySessionSnapshot session = PlaySessionSnapshot.Current;
            Assert.IsNotNull(session);
            Assert.AreEqual(1, session.SongId);
            Assert.IsNull(session.RemoteId);
            Assert.AreEqual(0, session.ChartVersion);
            Assert.IsFalse(PlaySessionSnapshot.TryGetChartJson(session.Song, out _),
                "ローカル譜面のプレイに古いリモートのスナップショットを混ぜない");

            // そのあとカタログが適用されて行 1 がリモートに取られても、
            // このプレイの曲 (譜面ソース = Resources) は変わらない。
            GManager.ApplyRemoteCatalog(CloudCatalog.ParseCatalog(
                "{\"songs\":[{\"id\":\"lost-umbrella\",\"title\":\"ロストアンブレラ\",\"artist\":\"稲葉曇\"," +
                "\"youtubeId\":\"DeKLpgzh-qQ\",\"chartVersion\":5}]}"));
            Assert.AreEqual(5, GManager.ActiveSongs[1].chartVersion, "前提: 行 1 はリモート v5 に更新されている");

            SongData pinned = PlaySessionSnapshot.ResolveSong(GManager.songID);
            Assert.AreSame(session.Song, pinned);
            Assert.IsFalse(pinned.isRemote, "このプレイで使う曲は開始時のローカル曲のまま");
        }

        [Test]
        public void Retry_RePinsTheSameSessionContent()
        {
            // 1 回目の開始: who v1 で固定 (遷移開始と同じ順序)。
            GManager.ApplyRemoteCatalog(CloudCatalog.ParseCatalog(CatalogV1));
            CloudChartHolder.Hold("who", 1, ChartV1);
            GManager.songID = 3;
            GManager.ResetSession();
            PlaySessionSnapshot first = PlaySessionSnapshot.Current;
            Assert.IsNotNull(first);

            // リトライ (GameSceneManager.RetryGame → ResetSession): カタログも
            // Holder も変わっていないので、同じ内容で再固定される。
            GManager.ResetSession();

            PlaySessionSnapshot second = PlaySessionSnapshot.Current;
            Assert.IsNotNull(second);
            Assert.AreNotSame(first, second);
            Assert.AreEqual(first.SongId, second.SongId);
            Assert.AreEqual(first.ChartVersion, second.ChartVersion);
            Assert.AreEqual(first.Song.title, second.Song.title);
            Assert.IsTrue(PlaySessionSnapshot.TryGetChartJson(second.Song, out string json));
            Assert.AreEqual(ChartV1, json, "リトライは開始時と同じ譜面をプレイする");
        }

        [Test]
        public void ResetSession_WhenTheSongRowVanished_DoesNotPin()
        {
            // 遷移中にカタログが消えて行が無くなった (songID もクランプされた) 状況。
            // 固定できる identity がないので、セッションは作らず従来の読み方へ戻す。
            GManager.songID = 999;

            GManager.ResetSession();

            Assert.IsNull(PlaySessionSnapshot.Current);
            Assert.IsNull(PlaySessionSnapshot.ResolveSong(999), "範囲外の行は null (NotesManager は警告して諦める)");
        }

        // --- GameScene / ResultScene でのスナップショット優先 ----------------------

        [Test]
        public void GetSong_ReturnsTheLiveRow_OutsideThePlayScenes()
        {
            PlaySessionSnapshot.Begin(RemoteSong(3, "who", 1), 1, ChartV1);

            // 編集モード (アクティブシーンなし) では従来どおりカタログの行を返す。
            // 選曲画面では最新カタログを見せる、という挙動と同じ。
            Assert.AreSame(GManager.ActiveSongs[3], GManager.GetSong(3));
        }

        [Test]
        public void PinnedSongFor_PrefersTheSnapshot_OnlyInThePlayScenes()
        {
            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(RemoteSong(3, "who", 1), 1, ChartV1);

            // GameScene / ResultScene ではスナップショット (開始時の内容) を優先する。
            // ResultRankingOverlay.RankingSong が読む版が、実際にプレイした版と一致する。
            Assert.AreSame(session.Song, PlaySessionSnapshot.PinnedSongFor(3, true));
            Assert.IsNull(PlaySessionSnapshot.PinnedSongFor(3, false), "選曲画面では最新カタログを見せる");
            Assert.IsNull(PlaySessionSnapshot.PinnedSongFor(1, true), "他の曲の行は固定しない");

            // カタログの行が新しくなっても、プレイ中は開始時の版を読める。
            GManager.ApplyRemoteCatalog(CloudCatalog.ParseCatalog(CatalogV2));
            Assert.AreEqual(2, GManager.ActiveSongs[3].chartVersion, "前提: 行は v2");
            Assert.AreEqual(1, PlaySessionSnapshot.PinnedSongFor(3, true).chartVersion);
        }

        // --- 後片付け -------------------------------------------------------------

        [Test]
        public void Clear_RemovesTheSessionAndFallsBackToTheLiveCatalog()
        {
            PlaySessionSnapshot.Begin(RemoteSong(1, "test-song", 1), 1, ChartV1);
            Assert.IsNotNull(PlaySessionSnapshot.Current);

            PlaySessionSnapshot.Clear();

            Assert.IsNull(PlaySessionSnapshot.Current);
            Assert.AreSame(GManager.ActiveSongs[GManager.songID], PlaySessionSnapshot.ResolveSong(GManager.songID));
            Assert.IsFalse(PlaySessionSnapshot.TryGetChartJson(GManager.ActiveSongs[1], out _));
        }

        [Test]
        public void RestoreSongId_DoesNotRestoreAnIdOutsideTheShrunkCatalog()
        {
            // クラウド追加曲 (行 29 以降) を固定したあとでカタログが消えると
            // 行自体が存在しなくなる。そのときは songID を戻さない (範囲外を避ける)。
            SongData[] remote = CloudCatalog.ParseCatalog(
                "{\"songs\":[{\"id\":\"appended-song\",\"title\":\"追加曲\",\"artist\":\"作曲者\"," +
                "\"youtubeId\":\"dQw4w9WgXcQ\",\"chartVersion\":3}]}");
            GManager.ApplyRemoteCatalog(remote);
            int appendedId = GManager.ActiveSongs.Length - 1;
            Assert.GreaterOrEqual(appendedId, GManager.Songs.Length, "前提: 追加曲は固定カタログの外の行");

            PlaySessionSnapshot session = PlaySessionSnapshot.Begin(GManager.ActiveSongs[appendedId], 3, ChartV1);
            Assert.AreEqual(appendedId, session.SongId);

            GManager.ClearRemoteCatalog(); // 行が消える (ActiveSongs = 固定カタログ)
            GManager.songID = 1;

            session.RestoreSongId();

            Assert.AreEqual(1, GManager.songID, "現在のカタログに存在しない行へは戻さない");
        }
    }
}
