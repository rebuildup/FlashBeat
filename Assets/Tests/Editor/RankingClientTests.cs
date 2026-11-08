using System;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Festival ranking (#134): pure-logic coverage for RankingClient — payload
    /// building, response parsing, nickname validation, URL building and the
    /// PlayerPrefs persistence. The network paths themselves are exercised by
    /// the server tests (#133); offline behaviour is covered by the
    /// "unavailable" contract of RankingClient.Result.
    /// </summary>
    public class RankingClientTests
    {
        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(RankingClient.NicknamePrefsKey);
            PlayerPrefs.Save();
        }

        // --- nickname validation ---

        [Test]
        public void ValidateNickname_TrimsAndAcceptsAsciiAndKana()
        {
            Assert.AreEqual("abc", RankingClient.ValidateNickname("  abc  "));
            Assert.AreEqual("なまえ1", RankingClient.ValidateNickname("なまえ1"));
            Assert.AreEqual("a", RankingClient.ValidateNickname("a"));
        }

        [Test]
        public void ValidateNickname_RejectsEmptyOrNullAndTooLong()
        {
            Assert.IsNull(RankingClient.ValidateNickname(null));
            Assert.IsNull(RankingClient.ValidateNickname(""));
            Assert.IsNull(RankingClient.ValidateNickname("   "));
            Assert.IsNull(RankingClient.ValidateNickname(new string('a', 13)));
            Assert.IsNotNull(RankingClient.ValidateNickname(new string('a', 12)));
        }

        // --- payload building ---

        [Test]
        public void BuildScoreRequestJson_ContainsSubmissionFields()
        {
            string json = RankingClient.BuildScoreRequestJson("test-song", 2, "なまえ", 123456, 100, 50, 10, 5, 77);

            var parsed = JsonUtility.FromJson<RankingClient.ScoreRequest>(json);
            Assert.AreEqual("test-song", parsed.songId);
            Assert.AreEqual(2, parsed.chartVersion);
            Assert.AreEqual("なまえ", parsed.nickname);
            Assert.AreEqual(123456, parsed.score);
            Assert.AreEqual(100, parsed.perfect);
            Assert.AreEqual(50, parsed.great);
            Assert.AreEqual(10, parsed.bad);
            Assert.AreEqual(5, parsed.miss);
            Assert.AreEqual(77, parsed.maxCombo);
        }

        [Test]
        public void BuildScoreRequestJson_RejectsInvalidNicknameOrSongId()
        {
            Assert.IsNull(RankingClient.BuildScoreRequestJson("test-song", 1, "", 1, 0, 0, 0, 0, 0));
            Assert.IsNull(RankingClient.BuildScoreRequestJson("test-song", 1, new string('a', 13), 1, 0, 0, 0, 0, 0));
            Assert.IsNull(RankingClient.BuildScoreRequestJson("Bad_Song", 1, "abc", 1, 0, 0, 0, 0, 0));
        }

        // --- response parsing ---

        [Test]
        public void ParseScoreResponse_ReadsRank()
        {
            var response = RankingClient.ParseScoreResponse("{\"rank\":3}");
            Assert.IsNotNull(response);
            Assert.AreEqual(3, response.rank);
        }

        [Test]
        public void ParseScoreResponse_RejectsGarbageAndZeroRank()
        {
            Assert.IsNull(RankingClient.ParseScoreResponse(null));
            Assert.IsNull(RankingClient.ParseScoreResponse(""));
            Assert.IsNull(RankingClient.ParseScoreResponse("not json"));
            Assert.IsNull(RankingClient.ParseScoreResponse("{\"rank\":0}"));
        }

        [Test]
        public void ParseLeaderboard_ReadsEntries()
        {
            string json = "{\"songId\":\"test-song\",\"chartVersion\":2,\"entries\":[" +
                          "{\"rank\":1,\"nickname\":\"one\",\"score\":900000}," +
                          "{\"rank\":2,\"nickname\":\"two\",\"score\":100}]}";

            var entries = RankingClient.ParseLeaderboard(json);

            Assert.IsNotNull(entries);
            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual(1, entries[0].rank);
            Assert.AreEqual("one", entries[0].nickname);
            Assert.AreEqual(900000, entries[0].score);
            Assert.AreEqual(2, entries[1].rank);
            Assert.AreEqual(100, entries[1].score);
        }

        [Test]
        public void ParseLeaderboard_RejectsGarbageAndEmpty()
        {
            Assert.IsNull(RankingClient.ParseLeaderboard(null));
            Assert.IsNull(RankingClient.ParseLeaderboard("not json"));

            // スコアの無い曲 (entries が空配列) は「取得成功・0 件」であり失敗ではない。
            // v6 選曲画面が「取得失敗 (HTTP 200)」と表示しないために ParseLeaderboard が
            // 空配列を返す仕様 (commit f2e0fbb) に合わせている。ここでは **空配列** を期待する。
            var empty = RankingClient.ParseLeaderboard("{\"entries\":[]}");
            Assert.IsNotNull(empty, "entries が空配列のときは『取得成功・0 件』として空リストを返す");
            Assert.AreEqual(0, empty.Length);
        }

        // --- URL building ---

        [Test]
        public void ResolveOrigin_TrimsBaseUrlAndFallsBackToProduction()
        {
            Assert.AreEqual("https://example.com", RankingClient.ResolveOrigin(true, " https://example.com/ "));
            Assert.AreEqual(RankingClient.DefaultOrigin, RankingClient.ResolveOrigin(false, ""));
            Assert.AreEqual(RankingClient.DefaultOrigin, RankingClient.ResolveOrigin(true, null));
        }

        [Test]
        public void Urls_FollowTheOpenApiContract()
        {
            StringAssert.StartsWith(RankingClient.DefaultOrigin, RankingClient.ScoreUrl());
            StringAssert.EndsWith("/api/v1/scores", RankingClient.ScoreUrl());
            Assert.AreEqual(RankingClient.DefaultOrigin + "/api/v1/leaderboards/test-song?version=2",
                RankingClient.LeaderboardUrl("test-song", 2));
            Assert.IsNull(RankingClient.LeaderboardUrl("Bad", 1));
        }

        [Test]
        public void IsValidSongId_FollowsTheServerPattern()
        {
            Assert.IsTrue(RankingClient.IsValidSongId("test-song"));
            Assert.IsTrue(RankingClient.IsValidSongId("abc"));
            Assert.IsFalse(RankingClient.IsValidSongId("ab"));
            Assert.IsFalse(RankingClient.IsValidSongId(""));
            Assert.IsFalse(RankingClient.IsValidSongId("Test"));
            Assert.IsFalse(RankingClient.IsValidSongId("a_b!"));
        }

        // --- persistence ---

        [Test]
        public void Nickname_RoundTripsThroughPlayerPrefs()
        {
            RankingClient.SaveNickname("なまえ");
            Assert.AreEqual("なまえ", RankingClient.LoadNickname());

            RankingClient.SaveNickname("");
            Assert.AreEqual(string.Empty, RankingClient.LoadNickname());
        }
    }
}
