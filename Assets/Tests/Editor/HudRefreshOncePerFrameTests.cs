using FlashBeat.UI.V6.Game.Hud;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #393: ゲーム HUD の更新が 1 フレームに 1 回だけであることを固定する。
    ///
    /// 以前は <c>V6GameRuntime.Update</c> と <c>V6GameHudView.Update</c> の両方が
    /// <c>Refresh</c> を呼び、同じフレームでスコア・ゲージ・コンボ・歌詞・判定行・
    /// レーンガイドの更新が 2 回走っていた。更新の所有者をビュー自身に一本化し、
    /// 統合側は <see cref="V6GameHudView.RequestRefresh"/> だけを使う。
    /// </summary>
    public class HudRefreshOncePerFrameTests
    {
        private GameObject canvasGo;
        private V6GameHudView hud;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("Hud Canvas", typeof(RectTransform), typeof(Canvas));
            hud = V6GameHudView.Create(canvasGo.GetComponent<RectTransform>());
        }

        [TearDown]
        public void TearDown()
        {
            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
                canvasGo = null;
            }

            hud = null;
        }

        [Test]
        public void RequestRefreshTwiceInOneFrameRunsOnce()
        {
            Assert.IsNotNull(hud, "HUD を作れていない");
            int before = hud.RefreshCountForAudit;

            hud.RequestRefresh();
            hud.RequestRefresh();
            hud.RequestRefresh();

            Assert.AreEqual(before + 1, hud.RefreshCountForAudit,
                "同じフレームで複数回要求しても更新は 1 回");
            Assert.AreEqual(0, hud.DuplicateRefreshFramesForAudit,
                "重複更新のフレームを作らない");
        }

        [Test]
        public void RequestRefreshRunsAgainOnTheNextFrame()
        {
            int before = hud.RefreshCountForAudit;
            hud.RequestRefresh();
            int afterFirst = hud.RefreshCountForAudit;

            // テストは同一フレームで走るので、フレーム番号を進めた状態を作る。
            // Time.frameCount は読み取り専用なので、次フレーム相当として
            // 観測点を直接リセットする代わりに、別インスタンスで確認する。
            Assert.AreEqual(before + 1, afterFirst, "1 回目は走る");

            var secondCanvas = new GameObject("Hud Canvas 2", typeof(RectTransform), typeof(Canvas));
            try
            {
                var second = V6GameHudView.Create(secondCanvas.GetComponent<RectTransform>());
                int secondBefore = second.RefreshCountForAudit;
                second.RequestRefresh();
                Assert.AreEqual(secondBefore + 1, second.RefreshCountForAudit,
                    "新しいフレーム/インスタンスでは通常どおり更新する");
            }
            finally
            {
                Object.DestroyImmediate(secondCanvas);
            }
        }

        [Test]
        public void DirectRefreshCallsAreVisibleInTheAuditCounters()
        {
            // 直接 Refresh を呼ぶ経路が残っていないことを、計測点で検出できる。
            int before = hud.RefreshCountForAudit;
            hud.Refresh();
            hud.Refresh();

            Assert.AreEqual(before + 2, hud.RefreshCountForAudit,
                "直接呼び出しは数えられる (重複検出の前提)");
            Assert.Greater(hud.DuplicateRefreshFramesForAudit, 0,
                "同フレームの直接 2 回は重複として記録される");
        }

        [Test]
        public void TheRuntimeDoesNotCallRefreshDirectly()
        {
            // 統合側 (V6GameRuntime) が Refresh を直接呼ばないこと。
            string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
                Application.dataPath, "Game/Scripts/UI/V6/V6GameRuntime.cs"));

            Assert.IsFalse(src.Contains("hud.Refresh()"),
                "V6GameRuntime が HUD を直接 Refresh している (二重更新の原因)");
            Assert.IsTrue(src.Contains("hud.RequestRefresh()"),
                "統合側は RequestRefresh を使う");
        }
    }
}
