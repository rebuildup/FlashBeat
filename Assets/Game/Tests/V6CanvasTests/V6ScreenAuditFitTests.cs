using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// Issue #850: v6 画面監査 (<see cref="V6ScreenAudit"/>) に追加した
    /// 「テキスト・インク収まり」「兄弟 Selectable 重なり」判定式の固定。
    ///
    /// - <see cref="V6ScreenAudit.Intersection"/> / <see cref="V6ScreenAudit.OverlapsBeyond"/>
    /// - <see cref="V6ScreenAudit.ClassifyInkFit"/> (Inside / Partial / Outside)
    /// - <see cref="V6ScreenAudit.TryInkLocalRect"/> (実 TMP メッシュによる計測)
    ///
    /// 実機走査そのものはプレイヤー (-autotest v6screens) で行う。
    /// </summary>
    public sealed class V6ScreenAuditFitTests
    {
        private GameObject canvasGo;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6InkFitTests Canvas", typeof(RectTransform), typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }
            canvasGo = null;
        }

        // --- Intersection / OverlapsBeyond ---

        [Test]
        public void Intersection_Overlap_ReturnsSharedRegion()
        {
            var i = V6ScreenAudit.Intersection(
                new Rect(0f, 0f, 100f, 100f), new Rect(50f, 25f, 100f, 100f));
            Assert.AreEqual(50f, i.width, 0.001f);
            Assert.AreEqual(75f, i.height, 0.001f);
            Assert.AreEqual(50f, i.xMin, 0.001f);
            Assert.AreEqual(25f, i.yMin, 0.001f);
        }

        [Test]
        public void Intersection_Disjoint_ReturnsEmpty()
        {
            var i = V6ScreenAudit.Intersection(
                new Rect(0f, 0f, 100f, 100f), new Rect(200f, 0f, 100f, 100f));
            Assert.LessOrEqual(i.width, 0f);
            Assert.LessOrEqual(i.height, 0f);
        }

        [Test]
        public void Intersection_EdgeTouching_ReturnsEmpty()
        {
            // 辺が接するだけ (面積 0) は交差ではない。
            var i = V6ScreenAudit.Intersection(
                new Rect(0f, 0f, 100f, 100f), new Rect(100f, 0f, 100f, 100f));
            Assert.LessOrEqual(i.width, 0f);
        }

        [Test]
        public void OverlapsBeyond_RequiresBothAxesBeyondTolerance()
        {
            var a = new Rect(0f, 0f, 100f, 100f);
            // 十分な交差 → true。
            Assert.IsTrue(V6ScreenAudit.OverlapsBeyond(a, new Rect(50f, 50f, 100f, 100f),
                V6ScreenAudit.TolerancePx));
            // 横に 0.5px しか重ならない (縦は十分) → false。
            Assert.IsFalse(V6ScreenAudit.OverlapsBeyond(a, new Rect(99.5f, 0f, 100f, 100f),
                V6ScreenAudit.TolerancePx));
            // 縦に 0.5px しか重ならない → false。
            Assert.IsFalse(V6ScreenAudit.OverlapsBeyond(a, new Rect(0f, 99.5f, 100f, 100f),
                V6ScreenAudit.TolerancePx));
            // 離れている → false。
            Assert.IsFalse(V6ScreenAudit.OverlapsBeyond(a, new Rect(200f, 0f, 100f, 100f),
                V6ScreenAudit.TolerancePx));
        }

        // --- ClassifyInkFit ---

        [Test]
        public void ClassifyInkFit_InsideElement_IsInside()
        {
            var element = new Rect(-100f, -20f, 200f, 40f);
            Assert.AreEqual(V6ScreenAudit.ScreenClip.Inside,
                V6ScreenAudit.ClassifyInkFit(new Rect(-40f, -10f, 80f, 20f), element));
        }

        [Test]
        public void ClassifyInkFit_StraddlesEdge_IsPartial()
        {
            var element = new Rect(-100f, -20f, 200f, 40f);
            // 右端を 10px 越えるインク (ボタンより広いラベル等)。
            Assert.AreEqual(V6ScreenAudit.ScreenClip.Partial,
                V6ScreenAudit.ClassifyInkFit(new Rect(60f, -10f, 60f, 20f), element));
            // 要素をすっぽり内包するほど大きいインクも Partial (収まっていない)。
            Assert.AreEqual(V6ScreenAudit.ScreenClip.Partial,
                V6ScreenAudit.ClassifyInkFit(new Rect(-150f, -30f, 300f, 60f), element));
        }

        [Test]
        public void ClassifyInkFit_Disjoint_IsOutside()
        {
            var element = new Rect(-100f, -20f, 200f, 40f);
            // 割当の下へ完全に外れたインク (配置破綻)。
            Assert.AreEqual(V6ScreenAudit.ScreenClip.Outside,
                V6ScreenAudit.ClassifyInkFit(new Rect(-40f, -120f, 80f, 20f), element));
            // かすりもしない右外。
            Assert.AreEqual(V6ScreenAudit.ScreenClip.Outside,
                V6ScreenAudit.ClassifyInkFit(new Rect(150f, -10f, 80f, 20f), element));
        }

        [Test]
        public void ClassifyInkFit_TinyOverlapBelowTolerance_IsOutside()
        {
            var element = new Rect(-100f, -20f, 200f, 40f);
            // 交差が許容誤差 (1px) 以下しかない → Outside 扱い。
            Assert.AreEqual(V6ScreenAudit.ScreenClip.Outside,
                V6ScreenAudit.ClassifyInkFit(new Rect(100.5f, -10f, 80f, 20f), element));
        }

        // --- TryInkLocalRect + ClassifyInkFit (実 TMP メッシュ) ---

        /// <summary>V6Controls.Text で本番と同じフォント解決経路の TMP を作る。</summary>
        private TextMeshProUGUI MakeText(string name, string content, Vector2 size, float fontSize)
        {
            var tmp = V6Controls.Text(canvasGo, name, content,
                new Vector2(0f, 0f), size, fontSize, Color.white);
            return tmp;
        }

        [Test]
        public void TryInkLocalRect_FittingText_ReturnsMeasurableInk()
        {
            var tmp = MakeText("Fitting", "Hello", new Vector2(200f, 40f), 24f);
            Assert.IsTrue(V6ScreenAudit.TryInkLocalRect(tmp, out var ink),
                "fitting text must produce measurable ink");
            Assert.Greater(ink.width, 0f);
            Assert.Greater(ink.height, 0f);
            Assert.AreEqual(V6ScreenAudit.ScreenClip.Inside,
                V6ScreenAudit.ClassifyInkFit(ink, tmp.rectTransform.rect),
                "a label sized for its text must classify Inside");
        }

        [Test]
        public void TryInkLocalRect_OverflowingText_DoesNotClassifyInside()
        {
            // 幅 40px に fs24 の長い文字列 → インクは割当をはみ出す。
            var tmp = MakeText("Overflow", "VeryLongLabelText", new Vector2(40f, 40f), 24f);
            Assert.IsTrue(V6ScreenAudit.TryInkLocalRect(tmp, out var ink));
            var fit = V6ScreenAudit.ClassifyInkFit(ink, tmp.rectTransform.rect);
            Assert.AreNotEqual(V6ScreenAudit.ScreenClip.Inside, fit,
                "ink wider than its rect must not classify Inside");
            Assert.IsTrue(fit == V6ScreenAudit.ScreenClip.Partial
                || fit == V6ScreenAudit.ScreenClip.Outside,
                "overflow must classify as Partial or Outside, got " + fit);
        }

        [Test]
        public void TryInkLocalRect_EmptyText_IsUnmeasurable()
        {
            var tmp = MakeText("Empty", "", new Vector2(200f, 40f), 24f);
            Assert.IsFalse(V6ScreenAudit.TryInkLocalRect(tmp, out _),
                "empty text has no glyphs to measure");
        }

        [Test]
        public void TryInkLocalRect_Null_IsUnmeasurable()
        {
            Assert.IsFalse(V6ScreenAudit.TryInkLocalRect(null, out _));
        }
    }
}
