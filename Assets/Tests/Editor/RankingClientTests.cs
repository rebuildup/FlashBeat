using System;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;

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
            // ニックネーム上限は 16 文字 (契約: contracts/openapi.v1.yaml
            // ScoreSubmission.nickname の maxLength: 16)。以前の 12 文字は
            // 契約変更前の古い値で、16 文字は有効 / 17 文字は拒否。
            Assert.IsNull(RankingClient.ValidateNickname(null));
            Assert.IsNull(RankingClient.ValidateNickname(""));
            Assert.IsNull(RankingClient.ValidateNickname("   "));
            Assert.IsNull(RankingClient.ValidateNickname(new string('a', 17)));
            Assert.AreEqual(new string('a', 16), RankingClient.ValidateNickname(new string('a', 16)));
            Assert.AreEqual(new string('a', 12), RankingClient.ValidateNickname(new string('a', 12)));
        }

        // --- payload building ---

        [Test]
        public void BuildScoreRequestJson_ContainsSubmissionFields()
        {
            string json = RankingClient.BuildScoreRequestJson("test-song", 2, "なまえ", 123456, 100, 50, 10, 5, 77, 3);

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
            Assert.AreEqual(3, parsed.comboMilestones);
            Assert.AreEqual(Scoring.LatestVersion, parsed.scoringVersion);
        }

        [Test]
        public void BuildScoreRequestJson_RejectsInvalidNicknameOrSongId()
        {
            Assert.IsNull(RankingClient.BuildScoreRequestJson("test-song", 1, "", 1, 0, 0, 0, 0, 0, 0));
            // 17 文字は契約上限 (contracts/openapi.v1.yaml ScoreSubmission.nickname
            // maxLength: 16) を超えるので拒否、16 文字は通る。
            Assert.IsNull(RankingClient.BuildScoreRequestJson("test-song", 1, new string('a', 17), 1, 0, 0, 0, 0, 0, 0));
            string atLimit = RankingClient.BuildScoreRequestJson("test-song", 1, new string('a', 16), 1, 0, 0, 0, 0, 0, 0);
            Assert.IsNotNull(atLimit, "16 文字 (契約上限) のニックネームは受理される");
            Assert.AreEqual(new string('a', 16),
                JsonUtility.FromJson<RankingClient.ScoreRequest>(atLimit).nickname);
            Assert.IsNull(RankingClient.BuildScoreRequestJson("Bad_Song", 1, "abc", 1, 0, 0, 0, 0, 0, 0));
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
            // Issue #847: 実 API (contracts/openapi.v1.yaml) の leaderboard
            // entry は [nickname, score] のみで rank フィールドを持たない。
            // 以前の fixture は実在しない "rank":N を含んでいた。
            string json = "{\"songId\":\"test-song\",\"chartVersion\":2,\"entries\":[" +
                          "{\"nickname\":\"one\",\"score\":900000}," +
                          "{\"nickname\":\"two\",\"score\":100}]}";

            var entries = RankingClient.ParseLeaderboard(json);

            Assert.IsNotNull(entries);
            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual(0, entries[0].rank, "実 API は rank を送らないので 0 になる");
            Assert.AreEqual("one", entries[0].nickname);
            Assert.AreEqual(900000, entries[0].score);
            Assert.AreEqual(0, entries[1].rank);
            Assert.AreEqual(100, entries[1].score);
        }

        // --- ボード表示順位 (Issue #847) ---

        [Test]
        public void BoardPosition_RankMissing_FallsBackToBoardIndex()
        {
            // 実 API の entry は rank を持たない (常に 0) ので、表示順位は
            // ボード内の位置 (1 始まり) になる。
            var first = new RankingClient.LeaderboardEntry { nickname = "one", score = 900000 };
            var second = new RankingClient.LeaderboardEntry { nickname = "two", score = 100 };

            Assert.AreEqual(1, RankingClient.BoardPosition(first, 0));
            Assert.AreEqual(2, RankingClient.BoardPosition(second, 1));
        }

        [Test]
        public void BoardPosition_RankPresent_IsHonored()
        {
            // 万一 API が rank を返す場合はそれを優先する
            // (旧 RankingPanel.ShowBoard と同じ規則)。
            var entry = new RankingClient.LeaderboardEntry { rank = 7, nickname = "x", score = 1 };

            Assert.AreEqual(7, RankingClient.BoardPosition(entry, 0));
        }

        [Test]
        public void BoardPosition_NullEntry_UsesIndex()
        {
            Assert.AreEqual(3, RankingClient.BoardPosition(null, 2));
        }

        [Test]
        public void ParseLeaderboard_RejectsGarbageAcceptsEmpty()
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

        /// <summary>
        /// 出荷する Resources/CloudCatalogConfig.json が本番ホストを指し、有効であること。
        /// ローカル Worker (`http://localhost:5173`) での検証 (#134) の値を残したまま
        /// 候補を作ると、Register が本番に届かず「オフライン」になる。
        /// `RankingClient.Origin()` だけを見ると、config が無い / 壊れている / enabled=false の
        /// ときも本番へフォールバックして素通りする (カタログ側は config を直接読むため、
        /// その状態では公開カタログが読まれない)。よって資源そのものを読んで検査する。
        /// </summary>
        [Test]
        public void ShippedConfig_NamesTheProductionHostAndIsEnabled()
        {
            const string ProductionOrigin = "https://flashbeat.rebuildup.dev";

            TextAsset asset = Resources.Load<TextAsset>(RankingClient.ConfigResourceName);
            Assert.IsNotNull(asset, "Resources/CloudCatalogConfig.json が見つかりません");

            ShippedCloudCatalogConfig shipped = JsonUtility.FromJson<ShippedCloudCatalogConfig>(asset.text);
            Assert.IsNotNull(shipped, "Resources/CloudCatalogConfig.json を解釈できません");
            Assert.IsTrue(shipped.enabled, "CloudCatalogConfig.enabled が false です (公開カタログが読まれません)");
            Assert.AreEqual(ProductionOrigin, (shipped.baseUrl ?? string.Empty).Trim().TrimEnd('/'),
                "CloudCatalogConfig.baseUrl が本番ホストと一致しません (ローカル検証用の URL が残っていませんか)");
            Assert.AreEqual(ProductionOrigin, RankingClient.DefaultOrigin,
                "RankingClient.DefaultOrigin が本番ホストと一致しません");
        }

        // Resources/CloudCatalogConfig.json のうち、このテストが見る項目だけを持つ受け皿。
        [Serializable]
        class ShippedCloudCatalogConfig
        {
            public bool enabled;
            public string baseUrl;
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
