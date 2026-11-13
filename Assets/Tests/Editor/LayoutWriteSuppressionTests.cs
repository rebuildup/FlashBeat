using FlashBeat.UI.V6;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #402: 変化していない UI へ同じ値を書き戻さない。
    ///
    /// 一次監査では「毎フレーム同じ値の setter を呼んでいる」構造は確認できたが、
    /// それが Canvas rebuild を起こすかは未計測だった。ここで固定するのは
    /// **同値代入をしないという規則** と、時間依存の表示は止めないこと。
    /// 実際の rebuild 回数の差は Profiler で別途測る (未計測を改善として書かない)。
    /// </summary>
    public class LayoutWriteSuppressionTests
    {
        private GameObject go;
        private RectTransform rt;
        private Image image;

        [SetUp]
        public void SetUp()
        {
            V6LayoutWrites.ResetCountersForAudit();
            go = new GameObject("target", typeof(RectTransform), typeof(Image));
            rt = go.GetComponent<RectTransform>();
            image = go.GetComponent<Image>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void SizeDelta_IsNotWrittenWhenTheValueIsUnchanged()
        {
            Assert.IsTrue(V6LayoutWrites.SizeDeltaIfChanged(rt, 100f, 20f));
            Assert.IsFalse(V6LayoutWrites.SizeDeltaIfChanged(rt, 100f, 20f),
                "同じ sizeDelta をもう一度書いている");

            Assert.IsTrue(V6LayoutWrites.SizeDeltaIfChanged(rt, 101f, 20f));
        }

        [Test]
        public void SizeDelta_ToleratesSubPixelNoise()
        {
            V6LayoutWrites.SizeDeltaIfChanged(rt, 100f, 20f);
            Assert.IsFalse(V6LayoutWrites.SizeDeltaIfChanged(rt, 100.005f, 20.005f),
                "0.01px 未満の揺れで毎フレーム dirty にしている");
        }

        [Test]
        public void AnchoredPosition_IsNotWrittenWhenTheValueIsUnchanged()
        {
            Assert.IsTrue(V6LayoutWrites.AnchoredPositionIfChanged(rt, 10f, -4f));
            Assert.IsFalse(V6LayoutWrites.AnchoredPositionIfChanged(rt, 10f, -4f));
            Assert.IsTrue(V6LayoutWrites.AnchoredPositionIfChanged(rt, 10f, -5f));
        }

        [Test]
        public void Alpha_IsNotWrittenWhenTheValueIsUnchanged()
        {
            image.color = new Color(1f, 1f, 1f, 0f);

            Assert.IsTrue(V6LayoutWrites.AlphaIfChanged(image, 0.5f));
            Assert.AreEqual(0.5f, image.color.a, 0.001f);

            Assert.IsFalse(V6LayoutWrites.AlphaIfChanged(image, 0.5f),
                "同じアルファを書き戻している (フェード終了後も毎フレーム dirty)");

            Assert.IsTrue(V6LayoutWrites.AlphaIfChanged(image, 0.25f));
        }

        [Test]
        public void Alpha_KeepsRgb()
        {
            image.color = new Color(0.2f, 0.4f, 0.6f, 1f);
            V6LayoutWrites.AlphaIfChanged(image, 0.3f);

            Assert.AreEqual(0.2f, image.color.r, 0.001f);
            Assert.AreEqual(0.4f, image.color.g, 0.001f);
            Assert.AreEqual(0.6f, image.color.b, 0.001f);
            Assert.AreEqual(0.3f, image.color.a, 0.001f);
        }

        [Test]
        public void Active_IsNotSetWhenTheStateIsUnchanged()
        {
            // 新しい GameObject は既に active。同値なら書かない。
            Assert.IsTrue(go.activeSelf);
            Assert.IsFalse(V6LayoutWrites.ActiveIfChanged(go, true), "同値の SetActive(true)");

            Assert.IsTrue(V6LayoutWrites.ActiveIfChanged(go, false));
            Assert.IsFalse(go.activeSelf);
            Assert.IsFalse(V6LayoutWrites.ActiveIfChanged(go, false), "同値の SetActive(false)");

            Assert.IsTrue(V6LayoutWrites.ActiveIfChanged(go, true));
            Assert.IsTrue(go.activeSelf);
        }

        [Test]
        public void Counters_TellSuppressedFromApplied()
        {
            V6LayoutWrites.SizeDeltaIfChanged(rt, 50f, 10f);
            V6LayoutWrites.SizeDeltaIfChanged(rt, 50f, 10f);
            V6LayoutWrites.SizeDeltaIfChanged(rt, 50f, 10f);

            Assert.AreEqual(1, V6LayoutWrites.AppliedWrites);
            Assert.AreEqual(2, V6LayoutWrites.SuppressedWrites);
        }

        [Test]
        public void NullTargetsAreIgnored()
        {
            Assert.IsFalse(V6LayoutWrites.SizeDeltaIfChanged(null, 1f, 1f));
            Assert.IsFalse(V6LayoutWrites.AnchoredPositionIfChanged(null, 1f, 1f));
            Assert.IsFalse(V6LayoutWrites.AlphaIfChanged(null, 1f));
            Assert.IsFalse(V6LayoutWrites.ActiveIfChanged(null, true));
        }

        /// <summary>
        /// 時間依存の表示 (歌詞・進捗・スコア・判定) の setter を、この規則で
        /// 「変化しないから」と消していないことを構造で固定する。
        /// </summary>
        [Test]
        public void TimeDependentHudUpdatesAreStillPresent()
        {
            string path = System.IO.Path.Combine(Application.dataPath,
                "Game/Scripts/UI/V6/Game/Hud/V6GameHudView.cs");
            string src = System.IO.File.ReadAllText(path);

            Assert.IsTrue(src.Contains("UpdateLyrics("), "歌詞の更新が消えている");
            Assert.IsTrue(src.Contains("UpdateLaneVerdicts("), "判定行の更新が消えている");
            Assert.IsTrue(src.Contains("UpdateValueStrips("), "スコア/コンボの桁別ポップが消えている");
            Assert.IsTrue(src.Contains("GManager.step_time"), "メディア時計の参照が消えている");
        }
    }
}
