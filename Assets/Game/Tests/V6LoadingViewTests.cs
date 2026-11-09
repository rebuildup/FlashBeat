using NUnit.Framework;
using FlashBeat.UI.V6.Game.Loading;

namespace FlashBeat.V6HudTests
{
    /// <summary>
    /// v6 曲ローディング画面 (Issue #224) の映像面判断の EditMode テスト。
    ///
    /// 実 VideoPlayer には依存しない純ロジックだけを検証する:
    /// 「clip があるか / 準備完了か / エラーか / 画面が出ているか」→
    /// 「映像を出す / 背景だけ / 隠す」の 3 状態。
    ///
    /// 背景の意図 (review Finding B): ローディング中は
    /// 「映像が無い・準備できていない・再生エラー」のいずれでも
    /// 白い RawImage を出さず、背景 (fallback) だけを見せる。
    /// </summary>
    public class V6LoadingViewTests
    {
        [Test]
        public void Video_IsShownWhenClipReadyAndNoError()
        {
            Assert.AreEqual(
                V6LoadingVideoSurface.Video,
                V6LoadingVideoPolicy.Decide(true, true, true, false));
        }

        [Test]
        public void Background_IsShownWhenNoClip()
        {
            Assert.AreEqual(
                V6LoadingVideoSurface.Background,
                V6LoadingVideoPolicy.Decide(true, false, false, false));
        }

        [Test]
        public void Background_IsShownWhenNotPreparedYet()
        {
            // 準備前の RenderTexture は空 → 白い RawImage を出さない。
            Assert.AreEqual(
                V6LoadingVideoSurface.Background,
                V6LoadingVideoPolicy.Decide(true, true, false, false));
        }

        [Test]
        public void Background_IsShownOnVideoError()
        {
            Assert.AreEqual(
                V6LoadingVideoSurface.Background,
                V6LoadingVideoPolicy.Decide(true, true, true, true));
        }

        [Test]
        public void Hidden_IsShownWhenViewIsNotVisible()
        {
            // 非表示なら映像も背景も出さない (テクスチャを保持しない)。
            Assert.AreEqual(
                V6LoadingVideoSurface.Hidden,
                V6LoadingVideoPolicy.Decide(false, true, true, false));
            Assert.AreEqual(
                V6LoadingVideoSurface.Hidden,
                V6LoadingVideoPolicy.Decide(false, false, false, true));
        }
    }
}
