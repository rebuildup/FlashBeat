using UnityEngine;

namespace FlashBeat.UI
{
    /// <summary>
    /// 全画面オーバーレイ (KeyConfigUI / ResultRankingOverlay) と背面シーンの
    /// キー入力の二重処理を防ぐ共有ゲート (Issue #167)。
    ///
    /// オーバーレイは開いたフレーム・閉じたフレームに <see cref="BlockThisFrame"/> を呼ぶ。
    /// 背面の v5 View (SelectView / OptionView / ResultView / KeyGuideUI) は
    /// Update の先頭で <see cref="InputAllowed"/> を見て、-block されているフレームは
    /// 入力を捨てる。これがないと、Esc でオーバーレイを閉じた同じフレームに
    /// 背面のシーンが同じ Esc を処理して意図しない遷移 (Title への退避など) が起きる。
    ///
    /// 「開いている間」の入力遮断は各オーバーレイの SuspendScene
    /// (背面の Behaviour を enabled=false) が担う。このゲートはその境目の
    /// 1 フレームを埋めるだけなので、状態はフレーム番号のみ。
    /// </summary>
    public static class SceneInputGate
    {
        private static int blockedThroughFrame = -1;

        /// <summary>フレーム番号の供給源。EditMode テストで差し替える。</summary>
        public static System.Func<int> FrameSource = () => Time.frameCount;

        /// <summary>このフレームの背面シーンへのキー入力を止める (オーバーレイの開閉時に呼ぶ)。</summary>
        public static void BlockThisFrame()
        {
            blockedThroughFrame = Mathf.Max(blockedThroughFrame, FrameSource());
        }

        /// <summary>背面の View がキー入力を処理してよいか (Update の先頭で見る)。</summary>
        public static bool InputAllowed
        {
            get { return FrameSource() > blockedThroughFrame; }
        }

        /// <summary>テスト間の状態を戻す。</summary>
        public static void ResetForTests()
        {
            blockedThroughFrame = -1;
        }
    }
}
