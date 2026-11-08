using NUnit.Framework;
using FlashBeat.UI.V6.Result;

namespace FlashBeat.UI.V6.Result.Tests
{
    /// <summary>登録モーダル (Issue #240) の純ロジック。検証 / 結果区分 / 状態メッセージ。</summary>
    public sealed class V6ResultSubmitTests
    {
        private const int MaxLength = RankingClient.NicknameMaxLength;

        // --- ValidateNicknameInput (空 / 長すぎ / 使用文字) ---

        [Test]
        public void ValidateNickname_AcceptsTrimmedName()
        {
            Assert.AreEqual(V6ResultLogic.NicknameError.None,
                V6ResultLogic.ValidateNicknameInput("  abc  ", MaxLength));
        }

        [Test]
        public void ValidateNickname_RejectsEmptyAfterTrim()
        {
            Assert.AreEqual(V6ResultLogic.NicknameError.Empty,
                V6ResultLogic.ValidateNicknameInput("   ", MaxLength));
            Assert.AreEqual(V6ResultLogic.NicknameError.Empty,
                V6ResultLogic.ValidateNicknameInput(null, MaxLength));
        }

        [Test]
        public void ValidateNickname_BoundaryLength()
        {
            Assert.AreEqual(V6ResultLogic.NicknameError.None,
                V6ResultLogic.ValidateNicknameInput(new string('a', MaxLength), MaxLength));
            Assert.AreEqual(V6ResultLogic.NicknameError.TooLong,
                V6ResultLogic.ValidateNicknameInput(new string('a', MaxLength + 1), MaxLength));
        }

        [Test]
        public void ValidateNickname_RejectsControlCharacters()
        {
            Assert.AreEqual(V6ResultLogic.NicknameError.InvalidChar,
                V6ResultLogic.ValidateNicknameInput("a\u0001b", MaxLength));
        }

        [Test]
        public void ValidateNickname_AcceptsJapanese()
        {
            Assert.AreEqual(V6ResultLogic.NicknameError.None,
                V6ResultLogic.ValidateNicknameInput("なまえ", MaxLength));
        }

        // --- ClassifySubmitResult (openapi.v1.yaml: 201 / 429 / 422) ---

        [Test]
        public void Classify_Ok_IsSuccess()
        {
            Assert.AreEqual(V6ResultLogic.SubmitOutcome.Success,
                V6ResultLogic.ClassifySubmitResult(true, 201, null));
        }

        [Test]
        public void Classify_TransportFailure_IsOffline()
        {
            Assert.AreEqual(V6ResultLogic.SubmitOutcome.Offline,
                V6ResultLogic.ClassifySubmitResult(false, 0, null));
            Assert.AreEqual(V6ResultLogic.SubmitOutcome.Offline,
                V6ResultLogic.ClassifySubmitResult(false, 0, RankingClient.UnavailableError));
        }

        [Test]
        public void Classify_429_IsDuplicate()
        {
            Assert.AreEqual(V6ResultLogic.SubmitOutcome.Duplicate,
                V6ResultLogic.ClassifySubmitResult(false, 429, "HTTP/1.1 429"));
        }

        [Test]
        public void Classify_OtherHttp_IsRejected()
        {
            Assert.AreEqual(V6ResultLogic.SubmitOutcome.Rejected,
                V6ResultLogic.ClassifySubmitResult(false, 422, "HTTP/1.1 422"));
            Assert.AreEqual(V6ResultLogic.SubmitOutcome.Rejected,
                V6ResultLogic.ClassifySubmitResult(false, 500, "HTTP/1.1 500"));
        }

        // --- SubmitStatusMessage (失敗時は再送を案内、偽の成功を出さない) ---

        [Test]
        public void StatusMessage_DistinguishesOutcomes()
        {
            Assert.IsNotEmpty(V6ResultLogic.SubmitStatusMessage(
                V6ResultLogic.SubmitOutcome.Pending, 0));
            Assert.IsNotEmpty(V6ResultLogic.SubmitStatusMessage(
                V6ResultLogic.SubmitOutcome.Success, 0));
            StringAssert.Contains("再送", V6ResultLogic.SubmitStatusMessage(
                V6ResultLogic.SubmitOutcome.Duplicate, 429));
            StringAssert.Contains("オフライン", V6ResultLogic.SubmitStatusMessage(
                V6ResultLogic.SubmitOutcome.Offline, 0));
            StringAssert.Contains("HTTP 422", V6ResultLogic.SubmitStatusMessage(
                V6ResultLogic.SubmitOutcome.Rejected, 422));
        }
    }
}
