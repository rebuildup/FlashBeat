using System.Reflection;
using FlashBeat.UI;
using FlashBeat.UI.V6.Game.Hud;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #871: 歌詞パネルの通常プレイ経路テスト。
    ///
    /// これまでの HUD テストは GameSceneManager.InputJson を直接差し込み、
    /// <see cref="ChartLyrics.Resolve"/> / <see cref="PlaySessionSnapshot"/> /
    /// <see cref="CloudChartHolder"/> を通っていなかった。実機で「枠は出るが
    /// 本文が出ない」が残った原因を切り分けるため、ここでは
    /// 譜面 JSON → ChartLyrics.Resolve → LyricsResult/InputJson →
    /// LyricsTimeline → V6GameHudView の実経路を通す。
    /// </summary>
    public class ChartLyricsPathTests
    {
        private const string ChartJsonWithLyrics =
            "{\"songId\":\"remote-1\",\"chartVersion\":2,\"chart\":{" +
            "\"lyrics\":{" +
            "\"startTime\":[2.0,6.0],\"endTime\":[4.0,8.0]," +
            "\"mondai\":[\"こんにちは\",\"またね\"]," +
            "\"furigana\":[],\"romaji\":[]}}}";

        private const string ChartJsonNoLyrics =
            "{\"songId\":\"remote-1\",\"chartVersion\":2,\"chart\":{\"notes\":[]}}";

        private GameObject canvasGo;
        private V6GameHudView view;
        private bool originalPlayed;
        private float originalStepTime;
        private bool originalLyricsEnabled;

        [SetUp]
        public void SetUp()
        {
            ChartLyrics.Invalidate();
            PlaySessionSnapshot.Clear();
            CloudChartHolder.Clear();
            originalPlayed = GManager.played;
            originalStepTime = GManager.step_time;
            originalLyricsEnabled = PlaySession.LyricsEnabled;
            PlaySession.SetLyricsEnabled(true);

            canvasGo = new GameObject("ChartLyricsPathTests Canvas", typeof(Canvas));
            var canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            view = V6GameHudView.Create(canvasRect);
        }

        [TearDown]
        public void TearDown()
        {
            GManager.played = originalPlayed;
            GManager.step_time = originalStepTime;
            PlaySession.SetLyricsEnabled(originalLyricsEnabled);
            ChartLyrics.Invalidate();
            PlaySessionSnapshot.Clear();
            CloudChartHolder.Clear();
            if (canvasGo != null) Object.DestroyImmediate(canvasGo);
            view = null;
            canvasGo = null;
        }

        private static SongData RemoteSong(int chartVersion)
        {
            var song = new SongData(1, "remote song", "artist", "videoId", 120, 5, 100, 60f)
            {
                isRemote = true,
                remoteId = "remote-1",
                chartVersion = chartVersion,
            };
            return song;
        }

        /// <summary>
        /// 通常プレイ経路その 1: プレイセッションに固定された譜面 JSON から
        /// 歌詞が解決され、HUD が本文を表示する。
        /// </summary>
        [Test]
        public void PlayPath_SnapshotChartJson_LyricsReachHud()
        {
            SongData song = RemoteSong(2);
            PlaySessionSnapshot.Begin(song, 2, ChartJsonWithLyrics);

            // GameSceneManager.Start / Load と同じ解決順。
            SongData resolved = PlaySessionSnapshot.ResolveSong(song.id);
            ChartLyricsResult result = ChartLyrics.Resolve(resolved);

            Assert.AreEqual(ChartLyricsState.HasLyrics, result.State);
            Assert.IsNotNull(result.Lyrics);
            Assert.AreEqual("こんにちは", result.Lyrics.Mondai[0]);

            // GameSceneManager.Load と同じ代入 (inputJson = LyricsResult.Lyrics)。
            GameObject sourceGo = CreateSource(result);
            try
            {
                GManager.played = true;
                GManager.step_time = 3f; // 2.0-4.0 の表示区間の中
                view.Refresh();

                Assert.AreEqual("こんにちは", LyricTextOf(view).text,
                    "実経路で解決した歌詞が HUD の本文に出る");
                Assert.IsTrue(LyricBoxOf(view).activeSelf, "歌詞データありなので枠も出る");

                GManager.step_time = 10f; // 全行の区間外
                view.Refresh();
                Assert.AreEqual("", LyricTextOf(view).text, "区間外は本文を空にする");
                Assert.IsTrue(LyricBoxOf(view).activeSelf, "区間外でも枠は維持する");
            }
            finally
            {
                Object.DestroyImmediate(sourceGo);
            }
        }

        /// <summary>
        /// 選択行の chartVersion が CloudChartHolder の保持版とずれていても、
        /// 保持されている実際の譜面から歌詞を解決する (Issue #871)。
        /// 以前は TryGet (版一致必須) のみで Pending に留まり、
        /// 枠だけ出て本文が永久に空になり得た。
        /// </summary>
        [Test]
        public void PlayPath_HeldChartVersionDrift_StillResolvesLyrics()
        {
            // スナップショット無し (例: セッション境界をまたいだ再入)。
            SongData song = RemoteSong(chartVersion: 5); // 行は v5 を指す
            CloudChartHolder.Hold("remote-1", 2, ChartJsonWithLyrics); // 実際の保持は v2

            ChartLyricsResult result = ChartLyrics.Resolve(song);

            Assert.AreEqual(ChartLyricsState.HasLyrics, result.State,
                "保持版 (v2) が行の版 (v5) と違っても、保持譜面から歌詞を解決する");
        }

        /// <summary>
        /// リモート譜面が未取得の間は Pending (枠は保留扱いで出す)。
        /// 後から Hold されたら再解決で HasLyrics へ進む
        /// (RefreshPendingLyrics が毎フレーム呼ぶ経路と同じ)。
        /// </summary>
        [Test]
        public void PlayPath_RemoteChartArrivesLate_PendingThenHasLyrics()
        {
            SongData song = RemoteSong(2);

            ChartLyricsResult pending = ChartLyrics.Resolve(song);
            Assert.AreEqual(ChartLyricsState.Pending, pending.State);
            Assert.IsNull(pending.Lyrics);

            // 譜面到着 → GameSceneManager.RefreshPendingLyrics と同じ再解決。
            CloudChartHolder.Hold("remote-1", 2, ChartJsonWithLyrics);
            ChartLyricsResult arrived = ChartLyrics.Resolve(song);

            Assert.AreEqual(ChartLyricsState.HasLyrics, arrived.State);
            Assert.AreEqual("こんにちは", arrived.Lyrics.Mondai[0]);
        }

        /// <summary>歌詞ブロックを持たない譜面は NoLyrics 確定 (Pending にしない)。</summary>
        [Test]
        public void PlayPath_ChartWithoutLyrics_IsNoLyrics()
        {
            SongData song = RemoteSong(2);
            PlaySessionSnapshot.Begin(song, 2, ChartJsonNoLyrics);

            ChartLyricsResult result = ChartLyrics.Resolve(
                PlaySessionSnapshot.ResolveSong(song.id));

            Assert.AreEqual(ChartLyricsState.NoLyrics, result.State);
        }

        /// <summary>
        /// GameSceneManager.Load が行う代入 (LyricsResult + inputJson) を
        /// 再現した供給源を作る。HUD は LyricsResult.State を優先参照する。
        /// </summary>
        private static GameObject CreateSource(ChartLyricsResult result)
        {
            var go = new GameObject("ChartLyricsPathTests Source", typeof(GameSceneManager));
            var manager = go.GetComponent<GameSceneManager>();

            var jsonField = typeof(GameSceneManager).GetField("inputJson",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(jsonField, "GameSceneManager.inputJson が見つからない");
            jsonField.SetValue(manager, result.Lyrics);

            // LyricsResult { get; private set; } のバッキングフィールドへ直接書く
            // (Load() は private でシーン依存のため、ここでは確定済みの結果を置く)。
            var resultField = typeof(GameSceneManager).GetField("<LyricsResult>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(resultField, "GameSceneManager.LyricsResult が見つからない");
            resultField.SetValue(manager, result);
            return go;
        }

        private static TextMeshProUGUI LyricTextOf(V6GameHudView target)
        {
            var field = typeof(V6GameHudView).GetField("lyricText",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "V6GameHudView.lyricText が見つからない");
            return (TextMeshProUGUI)field.GetValue(target);
        }

        private static GameObject LyricBoxOf(V6GameHudView target)
        {
            var field = typeof(V6GameHudView).GetField("lyricBox",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "V6GameHudView.lyricBox が見つからない");
            return (GameObject)field.GetValue(target);
        }
    }
}
