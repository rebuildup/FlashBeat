using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Result;

namespace FlashBeat.UI.V6.Result.Tests
{
    /// <summary>
    /// ローカル登録の契約 (Issue #240) の EditMode テスト。
    ///
    /// - ローカル (送信不可) 曲: v5 と同じ PlayerPrefs キー "V5PlayerName" に保存する。
    /// - ローカル (送信不可) 曲: オンライン語彙 (オフライン / 送信) の結果表示にならない。
    /// - オンライン曲: 既存の 4 区分 (201 / 429 / オフライン / その他 HTTP) が保たれる。
    ///
    /// 実際の RankingClient 通信 (RankingClient.PostScore / GetLeaderboard) は
    /// 一切呼ばない。判定は純ロジック (V6ResultLogic / V6ResultRegistration) だけを
    /// 経由し、PlayerPrefs への読み書きだけ行う。
    /// </summary>
    public sealed class V6ResultRegistrationTests
    {
        private const string LocalKey = V6ResultLogic.LocalPlayerNamePrefsKey;

        private string savedBefore;

        [SetUp]
        public void SetUp()
        {
            savedBefore = PlayerPrefs.GetString(LocalKey, string.Empty);
            V6LocalNameStore.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (string.IsNullOrEmpty(savedBefore))
            {
                V6LocalNameStore.Clear();
            }
            else
            {
                V6LocalNameStore.Save(savedBefore);
            }
        }

        // --- 方式判定 (送信対象か) ---

        [Test]
        public void ModeOf_LocalSong_IsLocal()
        {
            Assert.AreEqual(V6RegistrationMode.Local,
                V6ResultLogic.RegistrationMode(false, null, 0));
        }

        [Test]
        public void ModeOf_RemoteSongWithChart_IsOnline()
        {
            Assert.AreEqual(V6RegistrationMode.Online,
                V6ResultLogic.RegistrationMode(true, "abc-123", 3));
        }

        [Test]
        public void ModeOf_RemoteSongWithoutChartVersion_IsLocal()
        {
            // 譜面未取得のリモート行は送信対象ではない → ローカル扱い。
            Assert.AreEqual(V6RegistrationMode.Local,
                V6ResultLogic.RegistrationMode(true, "abc-123", 0));
        }

        [Test]
        public void ModeOf_RemoteSongWithInvalidSongId_IsLocal()
        {
            Assert.AreEqual(V6RegistrationMode.Local,
                V6ResultLogic.RegistrationMode(true, "INVALID ID", 3));
        }

        [Test]
        public void IsSubmittable_MatchesRegistrationMode()
        {
            Assert.IsFalse(V6ResultLogic.IsSubmittable(false, "abc-123", 3));
            Assert.IsFalse(V6ResultLogic.IsSubmittable(true, "abc-123", 0));
            Assert.IsTrue(V6ResultLogic.IsSubmittable(true, "abc-123", 3));
        }

        // --- ローカル保存 (PlayerPrefs "V5PlayerName") ---

        [Test]
        public void LocalSave_WritesV5PlayerNameKey()
        {
            Assert.AreEqual("V5PlayerName", LocalKey,
                "v5 の PlayerPrefs キーと一致していなければならない");

            V6LocalNameStore.Save(V6ResultLogic.NormalizeLocalName("player1"));
            PlayerPrefs.Save();

            Assert.AreEqual("PLAYER1", PlayerPrefs.GetString(LocalKey, string.Empty));
        }

        [Test]
        public void LocalSave_RoundTripsThroughStore()
        {
            string normalized = V6ResultLogic.NormalizeLocalName("abc-123");
            V6LocalNameStore.Save(normalized);

            Assert.AreEqual("ABC123", normalized);
            Assert.AreEqual("ABC123", V6LocalNameStore.Load());
        }

        [Test]
        public void LocalSave_NormalizesLikeV5()
        {
            // v5 の規則: 大文字化 + A-Z0-9 のみ + 最大 8 文字。
            Assert.AreEqual("ABC", V6ResultLogic.NormalizeLocalName("abc"));
            Assert.AreEqual("ABC123", V6ResultLogic.NormalizeLocalName("abc-123"));
            Assert.AreEqual("ABCDEFGH", V6ResultLogic.NormalizeLocalName("abcdefghij"));
            Assert.AreEqual("YOU", V6ResultLogic.NormalizeLocalName(""));
            Assert.AreEqual("YOU", V6ResultLogic.NormalizeLocalName(null));
        }

        [Test]
        public void LocalSave_MaxLengthIsEight()
        {
            Assert.AreEqual(8, V6ResultLogic.LocalNameMaxLength);
        }

        // --- ローカルはオンライン語彙を出さない ---

        [Test]
        public void LocalMessage_HasNoOnlineVocabulary()
        {
            string message = V6ResultLogic.LocalSaveMessage("PLAYER1");

            Assert.IsFalse(message.Contains("オフライン"), message);
            Assert.IsFalse(message.Contains("送信"), message);
            Assert.IsFalse(message.Contains("HTTP"), message);
            Assert.IsTrue(message.Contains("PLAYER1"), message);
        }

        [Test]
        public void LocalMessage_DefaultNameIsUsedWhenMissing()
        {
            string message = V6ResultLogic.LocalSaveMessage(null);
            Assert.IsTrue(message.Contains("YOU"), message);
            Assert.IsFalse(message.Contains("オフライン"), message);
        }

        // --- 初期値 (保存済み名の復元) ---

        [Test]
        public void InitialName_LocalMode_UsesSavedLocalName()
        {
            Assert.AreEqual("PLAYER1",
                V6ResultLogic.InitialName(
                    V6RegistrationMode.Local, "onlineName", "PLAYER1"));
        }

        [Test]
        public void InitialName_OnlineMode_PrefersOnlineNickname()
        {
            Assert.AreEqual("onlineName",
                V6ResultLogic.InitialName(
                    V6RegistrationMode.Online, "onlineName", "PLAYER1"));
        }

        [Test]
        public void InitialName_OnlineMode_FallsBackToLocalName()
        {
            Assert.AreEqual("PLAYER1",
                V6ResultLogic.InitialName(
                    V6RegistrationMode.Online, "", "PLAYER1"));
        }

        [Test]
        public void InitialName_EmptyWhenNothingSaved()
        {
            Assert.AreEqual(string.Empty,
                V6ResultLogic.InitialName(
                    V6RegistrationMode.Local, "", string.Empty));
        }

        // --- 譜面版 (pin) ---

        [Test]
        public void ResolveChartVersion_PrefersPinnedVersion()
        {
            Assert.AreEqual(7,
                V6ResultLogic.ResolveChartVersion(7, 12));
        }

        [Test]
        public void ResolveChartVersion_FallsBackToLiveVersionWhenNotPinned()
        {
            Assert.AreEqual(12,
                V6ResultLogic.ResolveChartVersion(0, 12));
        }

        // --- オンライン 4 区分が保たれている (既存テストの補強) ---

        [Test]
        public void OnlineSubmit_KeepsFourWayDistinction()
        {
            Assert.AreEqual(V6ResultLogic.SubmitOutcome.Success,
                V6ResultLogic.ClassifySubmitResult(true, 201, null));
            Assert.AreEqual(V6ResultLogic.SubmitOutcome.Duplicate,
                V6ResultLogic.ClassifySubmitResult(false, 429, null));
            Assert.AreEqual(V6ResultLogic.SubmitOutcome.Offline,
                V6ResultLogic.ClassifySubmitResult(false, 0, RankingClient.UnavailableError));
            Assert.AreEqual(V6ResultLogic.SubmitOutcome.Rejected,
                V6ResultLogic.ClassifySubmitResult(false, 422, null));
        }
    }
}
