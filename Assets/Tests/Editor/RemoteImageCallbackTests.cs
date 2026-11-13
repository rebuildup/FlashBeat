using FlashBeat.UI;
using NUnit.Framework;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #417: Result 画面の遠隔画像コールバックが、自分が発行した要求と
    /// 現在の世代と一致する場合だけ適用されることを固定する。
    ///
    /// これ自体はロジック単体なので、シーン destroy 後の this == null 判定を
    /// テスト側では扱えない（MonoBehaviour の lifecycle 外）。ここで固定するのは
    /// 世代が一致する場合の true / 不一致の場合の false のみ。
    /// </summary>
    public class RemoteImageCallbackTests
    {
        [Test]
        public void CanApplyRemoteSprite_SameGenerationReturnsTrue()
        {
            Assert.IsTrue(ResultSceneManager.CanApplyRemoteSprite(capturedRequestId: 7, currentRequestId: 7));
        }

        [Test]
        public void CanApplyRemoteSprite_NewerCurrentGenerationReturnsFalse()
        {
            // 旧要求 (captured=3) が、更新後 (current=4) に届いた場合は無効。
            Assert.IsFalse(ResultSceneManager.CanApplyRemoteSprite(capturedRequestId: 3, currentRequestId: 4));
        }

        [Test]
        public void CanApplyRemoteSprite_OlderCurrentGenerationReturnsFalse()
        {
            // 現在の状態 (current=3) より新しい要求が届いても、発行時点の世代で
            // 判断する（外部から世代番号が逆になっても誤適用しない）。
            Assert.IsFalse(ResultSceneManager.CanApplyRemoteSprite(capturedRequestId: 4, currentRequestId: 3));
        }

        [Test]
        public void CanApplyRemoteSprite_ZeroGenerationStillWorks()
        {
            Assert.IsTrue(ResultSceneManager.CanApplyRemoteSprite(capturedRequestId: 0, currentRequestId: 0));
            Assert.IsFalse(ResultSceneManager.CanApplyRemoteSprite(capturedRequestId: 0, currentRequestId: 1));
        }
    }
}
