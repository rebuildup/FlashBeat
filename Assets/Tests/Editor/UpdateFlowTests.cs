using NUnit.Framework;
using FlashBeat.Update;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// issue #118: 更新は明示式・任意。状態機械 (UpdateFlowModel) の遷移と不変条件を検証する。
    /// Model は纯粋クラス (IO を含まない) なのでネットワーク・ファイルなしで動く。
    /// </summary>
    public class UpdateFlowTests
    {
        private UpdateFlowModel model;

        [SetUp]
        public void SetUp()
        {
            model = new UpdateFlowModel();
        }

        [Test]
        public void StartCheck_FromIdle_TransitionsToChecking()
        {
            Assert.AreEqual(UpdateFlowPhase.Idle, model.Phase);
            model.StartCheck();
            Assert.AreEqual(UpdateFlowPhase.Checking, model.Phase);
        }

        [Test]
        public void StartCheck_Twice_StaysChecking()
        {
            model.StartCheck();
            model.StartCheck();
            Assert.AreEqual(UpdateFlowPhase.Checking, model.Phase);
        }

        [Test]
        public void CheckFinishedWithoutUpdate_ReturnsToIdle()
        {
            model.StartCheck();
            model.CheckFinishedWithoutUpdate();
            Assert.AreEqual(UpdateFlowPhase.Idle, model.Phase);
            Assert.IsFalse(model.HasAvailableUpdate);
        }

        [Test]
        public void CheckFinishedWithoutUpdate_FromIdle_IsNoOp()
        {
            model.CheckFinishedWithoutUpdate();
            Assert.AreEqual(UpdateFlowPhase.Idle, model.Phase);
        }

        [Test]
        public void CheckFoundNewVersion_SetsAvailableAndKeepsTagAndVersion()
        {
            model.StartCheck();
            model.CheckFoundNewVersion("v0.1.3-win64-abc1234", "0.1.3");
            Assert.AreEqual(UpdateFlowPhase.Available, model.Phase);
            Assert.IsTrue(model.HasAvailableUpdate);
            Assert.AreEqual("v0.1.3-win64-abc1234", model.LatestTag);
            Assert.AreEqual("0.1.3", model.LatestVersion);
        }

        [Test]
        public void CheckFoundNewVersion_FromIdle_IsNoOp()
        {
            model.CheckFoundNewVersion("v0.1.3-win64-abc1234", "0.1.3");
            Assert.AreEqual(UpdateFlowPhase.Idle, model.Phase);
            Assert.IsNull(model.LatestTag);
        }

        [Test]
        public void MarkStagedReady_FromAvailable_GoesReadyWithFullProgress()
        {
            model.StartCheck();
            model.CheckFoundNewVersion("v0.1.3-win64-abc1234", "0.1.3");
            model.MarkStagedReady();
            Assert.AreEqual(UpdateFlowPhase.Ready, model.Phase);
            Assert.AreEqual(1f, model.DownloadProgress);
        }

        [Test]
        public void MarkStagedReady_FromChecking_IsNoOp()
        {
            model.StartCheck();
            model.MarkStagedReady();
            Assert.AreEqual(UpdateFlowPhase.Checking, model.Phase);
        }

        [Test]
        public void FullHappyPath_IdleToApplying()
        {
            model.StartCheck();
            model.CheckFoundNewVersion("v0.1.3-win64-abc1234", "0.1.3");
            model.BeginDownload();
            Assert.AreEqual(UpdateFlowPhase.Downloading, model.Phase);
            model.SetDownloadProgress(0.4f);
            Assert.AreEqual(0.4f, model.DownloadProgress);
            model.DownloadSucceeded();
            Assert.AreEqual(UpdateFlowPhase.Ready, model.Phase);
            model.BeginApply();
            Assert.AreEqual(UpdateFlowPhase.Applying, model.Phase);
            Assert.IsTrue(model.UserConfirmed);
        }

        [Test]
        public void BeginDownload_FromIdleOrChecking_IsNoOp()
        {
            model.BeginDownload();
            Assert.AreEqual(UpdateFlowPhase.Idle, model.Phase);
            model.StartCheck();
            model.BeginDownload();
            Assert.AreEqual(UpdateFlowPhase.Checking, model.Phase);
        }

        [Test]
        public void DownloadFailed_ReturnsToAvailableWithMessageAndKeepsCurrentVersion()
        {
            model.StartCheck();
            model.CheckFoundNewVersion("v0.1.3-win64-abc1234", "0.1.3");
            model.BeginDownload();
            model.SetDownloadProgress(0.5f);
            model.DownloadFailed("ダウンロードに失敗しました。現行のまま使えます");
            Assert.AreEqual(UpdateFlowPhase.Available, model.Phase);
            Assert.AreEqual(0f, model.DownloadProgress);
            Assert.IsFalse(string.IsNullOrEmpty(model.Message));
            // 現行版が維持される = 更新候補の情報は保たれ、再試行 (BeginDownload) が可能
            Assert.AreEqual("v0.1.3-win64-abc1234", model.LatestTag);
            model.BeginDownload();
            Assert.AreEqual(UpdateFlowPhase.Downloading, model.Phase);
        }

        [Test]
        public void SetDownloadProgress_ClampsToUnitRange()
        {
            model.StartCheck();
            model.CheckFoundNewVersion("v0.1.3-win64-abc1234", "0.1.3");
            model.BeginDownload();
            model.SetDownloadProgress(-1f);
            Assert.AreEqual(0f, model.DownloadProgress);
            model.SetDownloadProgress(2f);
            Assert.AreEqual(1f, model.DownloadProgress);
        }

        [Test]
        public void SetDownloadProgress_OutsideDownloading_IsIgnored()
        {
            model.SetDownloadProgress(0.7f);
            Assert.AreEqual(0f, model.DownloadProgress);
        }

        [Test]
        public void BeginApply_FromReadyOnly()
        {
            model.StartCheck();
            model.CheckFoundNewVersion("v0.1.3-win64-abc1234", "0.1.3");
            model.BeginApply();
            Assert.AreEqual(UpdateFlowPhase.Available, model.Phase);
            Assert.IsFalse(model.UserConfirmed);

            model.BeginDownload();
            model.BeginApply();
            Assert.AreEqual(UpdateFlowPhase.Downloading, model.Phase);
            Assert.IsFalse(model.UserConfirmed);

            model.DownloadSucceeded();
            model.BeginApply();
            Assert.AreEqual(UpdateFlowPhase.Applying, model.Phase);
            Assert.IsTrue(model.UserConfirmed);
        }

        [Test]
        public void ApplyFailed_ReturnsToReadyWithMessage()
        {
            model.StartCheck();
            model.CheckFoundNewVersion("v0.1.3-win64-abc1234", "0.1.3");
            model.BeginDownload();
            model.DownloadSucceeded();
            model.BeginApply();
            model.ApplyFailed("更新スクリプトを起動できませんでした。現行のまま使えます");
            Assert.AreEqual(UpdateFlowPhase.Ready, model.Phase);
            Assert.IsFalse(model.UserConfirmed);
            Assert.IsFalse(string.IsNullOrEmpty(model.Message));
        }

        [Test]
        public void Dismiss_HidesNoticeForSessionButKeepsState()
        {
            model.StartCheck();
            model.CheckFoundNewVersion("v0.1.3-win64-abc1234", "0.1.3");
            model.BeginDownload();
            model.DownloadSucceeded();
            model.Dismiss();
            Assert.IsTrue(model.DismissedForSession);
            // staging・状態は保持: phase も Ready のまま、F3 相当の BeginApply は有効
            Assert.AreEqual(UpdateFlowPhase.Ready, model.Phase);
            model.BeginApply();
            Assert.AreEqual(UpdateFlowPhase.Applying, model.Phase);
        }

        [Test]
        public void ResetDismiss_ShowsNoticeAgain()
        {
            model.Dismiss();
            Assert.IsTrue(model.DismissedForSession);
            model.ResetDismiss();
            Assert.IsFalse(model.DismissedForSession);
        }

        /// <summary>不変条件: Checking / Available / Downloading / Ready のどこから自動的に
        /// Applying へ遷移する経路もない (適用は BeginApply = ユーザー確認のみ)。</summary>
        [Test]
        public void NeverAutoApplies_NoPathToApplyingWithoutBeginApply()
        {
            model.StartCheck();
            model.CheckFinishedWithoutUpdate();
            Assert.AreNotEqual(UpdateFlowPhase.Applying, model.Phase);

            model.StartCheck();
            model.CheckFoundNewVersion("v0.1.3-win64-abc1234", "0.1.3");
            Assert.AreNotEqual(UpdateFlowPhase.Applying, model.Phase);

            model.BeginDownload();
            model.SetDownloadProgress(1f);
            Assert.AreNotEqual(UpdateFlowPhase.Applying, model.Phase);

            model.DownloadSucceeded();
            Assert.AreNotEqual(UpdateFlowPhase.Applying, model.Phase);

            // ダウンロード失敗からも適用されない
            model.DownloadSucceeded();
            model.BeginApply();
            model.ApplyFailed("x");
            Assert.AreEqual(UpdateFlowPhase.Ready, model.Phase);
        }

        /// <summary>不変条件: OnApplicationQuit からの自動適用経路が存在しないことの構造的な保証。</summary>
        [Test]
        public void UpdateChecker_HasNoOnApplicationQuitHook()
        {
            const System.Reflection.BindingFlags all =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;
            Assert.IsNull(typeof(UpdateChecker).GetMethod("OnApplicationQuit", all),
                          "OnApplicationQuit からの自動適用は issue #118 で廃止されているはず");
        }

        /// <summary>不変条件: 自動適用の入口になりうる public メソッドは RequestApply / ApplyStagedUpdateNow
        /// (どちらもユーザー操作が前提) のみで、名前から OnApplicationQuit 相当の自動フックがない。</summary>
        [Test]
        public void UpdateChecker_PublicApplyEntries_AreExplicitOnly()
        {
            var methods = typeof(UpdateChecker).GetMethods(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly);
            foreach (var m in methods)
            {
                if (m.Name.StartsWith("On", System.StringComparison.Ordinal))
                {
                    Assert.Fail("自動フック相当の public メソッドが増えています: " + m.Name);
                }
            }
            Assert.IsNotNull(typeof(UpdateChecker).GetMethod("RequestApply"),
                             "RequestApply (ユーザー確認の入口) が必要");
            Assert.IsNotNull(typeof(UpdateChecker).GetMethod("RequestDownload"),
                             "RequestDownload (ユーザー確認の入口) が必要");
        }
    }
}
