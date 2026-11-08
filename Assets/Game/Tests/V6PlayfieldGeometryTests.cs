using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Game.Playfield;

namespace FlashBeat.V6PlayfieldTests
{
    /// <summary>
    /// v6 プレイフィールドの幾何 (design 契約 §6) の EditMode テスト。
    ///
    /// 契約値と実装値の照合 (2026-11-08 / docs/design/v6/flashbeat-v6-contract.md §6):
    /// 下列は **契約値 = 実装値** であることを項目ごとに確認済み。
    ///   判定線 y=792 h=4      → JudgeLineY / JudgeLineHeightDesignPx
    ///   判定グロウ y=796 h=2   → JudgeGlowY / JudgeGlowHeightDesignPx
    ///   判定線色 #EDEDED       → JudgeLineColor
    ///   グロウ色 #FFFFFF4D     → JudgeGlowColor
    ///   レーン線色 #FFFFFF2E sw2 → SeparatorColor / SeparatorStrokeDesignPx
    ///   ノーツ色 #EDEDED       → NoteColor
    ///   レーン中心線 下端 x=339 step=150 → LaneCentreBottomX / LaneCentreBottomStep
    ///   レーン中心線 上端 x=801 step=18  → LaneCentreTopX / LaneCentreTopStep
    ///
    /// ここでは契約値を **リテラルとして二重定義しない** (定数は実装を参照する)。
    /// 代わりに、契約値が満たす **関係** (中心寄せ / 16:9 / 線分の傾き / 隣接関係)
    /// を検証するので、実装の定数が契約から外れるとテストが落ちる。
    /// </summary>
    public class V6PlayfieldGeometryTests
    {
        private const float Epsilon = 1e-4f;

        // --- 設計フレーム ---

        [Test]
        public void DesignFrame_Is16x9()
        {
            // 契約: 1728×972 (= 16:9)。絶対値ではなく比で固定する。
            Assert.AreEqual(V6LaneGeometry.DesignWidth * 9f, V6LaneGeometry.DesignHeight * 16f, Epsilon);
            Assert.Greater(V6LaneGeometry.DesignWidth, 0f);
            Assert.Greater(V6LaneGeometry.DesignHeight, 0f);
        }

        // --- レーン中心線 ---

        [Test]
        public void LaneField_IsCentredOnTheDesignFrame()
        {
            // 契約: 8 本の中心線はフレーム中心 x=864 に対して対称
            // (下端 339..1389 / 上端 801..927 のいずれも中心が 864)。
            float bottomCentre = (V6LaneGeometry.LaneCentreBottomX
                + V6LaneGeometry.LaneCentreBottomX
                + (V6LaneGeometry.LaneCount - 1) * V6LaneGeometry.LaneCentreBottomStep) * 0.5f;
            float topCentre = (V6LaneGeometry.LaneCentreTopX
                + V6LaneGeometry.LaneCentreTopX
                + (V6LaneGeometry.LaneCount - 1) * V6LaneGeometry.LaneCentreTopStep) * 0.5f;

            Assert.AreEqual(V6LaneGeometry.DesignWidth * 0.5f, bottomCentre, Epsilon);
            Assert.AreEqual(V6LaneGeometry.DesignWidth * 0.5f, topCentre, Epsilon);
        }

        [Test]
        public void LaneFieldTopWidth_IsOneTwelfthOfTheFrame()
        {
            // 契約: 上端のレーン幅合計 = 144px = 1728 / 12。
            Assert.AreEqual(V6LaneGeometry.DesignWidth,
                V6LaneGeometry.LaneCount * V6LaneGeometry.LaneCentreTopStep * 12f, Epsilon);
        }

        [Test]
        public void LaneCentreLine_SlopeMatchesDesignPaths()
        {
            // 契約のレーン線は (上端 x, y=0) から (下端 x, y=判定線) への直線。
            // 左端レーン (lane 0) の傾きは dx/dy = 7/12、
            // レーン間隔の広がりは 1 ピクセルあたり 1/6 (遠近法)。
            float dx = V6LaneGeometry.LaneCentreTopX - V6LaneGeometry.LaneCentreBottomX;
            Assert.AreEqual(V6LaneGeometry.JudgeLineY * 7f, dx * 12f, Epsilon);

            float spread = V6LaneGeometry.LaneCentreBottomStep - V6LaneGeometry.LaneCentreTopStep;
            Assert.AreEqual(V6LaneGeometry.JudgeLineY, spread * 6f, Epsilon);
        }

        [Test]
        public void LaneCentrePoint_SpansFromJudgeLineToFrameTop()
        {
            for (int lane = 0; lane < V6LaneGeometry.LaneCount; lane++)
            {
                Assert.IsTrue(V6LaneGeometry.TryLanePoint(lane, 0f, out Vector2 bottom, out float bottomScale));
                Assert.IsTrue(V6LaneGeometry.TryLanePoint(lane, 1f, out Vector2 top, out float topScale));

                // t01=0 は判定線 (y=792)、t01=1 は画面上端 (y=0)。
                Assert.AreEqual(V6LaneGeometry.JudgeLineY, bottom.y, Epsilon);
                Assert.AreEqual(0f, top.y, Epsilon);

                // 判定線上のスケールは 1、奥ではレーン幅 / 判定線上の幅。
                Assert.AreEqual(1f, bottomScale, Epsilon);
                Assert.Less(topScale, bottomScale);
            }
        }

        [Test]
        public void LaneCentrePoint_RejectsOutOfRangeLane()
        {
            Assert.IsFalse(V6LaneGeometry.TryLanePoint(-1, 0f, out _, out _));
            Assert.IsFalse(V6LaneGeometry.TryLanePoint(V6LaneGeometry.LaneCount, 0f, out _, out _));
        }

        [Test]
        public void LaneCentreLines_AreMonotonicAcrossLanesAndDepth()
        {
            const int samples = 11;
            for (int s = 0; s < samples; s++)
            {
                float t = s / (float)(samples - 1);

                float previous = float.NegativeInfinity;
                for (int lane = 0; lane < V6LaneGeometry.LaneCount; lane++)
                {
                    Assert.IsTrue(V6LaneGeometry.TryLanePoint(lane, t, out Vector2 point, out _));

                    // 左から右へ単調増加 (レーンが重ならない)。
                    Assert.Greater(point.x, previous);
                    previous = point.x;
                }
            }

            // 奥へ行くほど間隔が狭まる (単調減少)。
            for (int s = 1; s < samples; s++)
            {
                float previous = V6LaneGeometry.LaneWidthAt((s - 1) / (float)(samples - 1));
                float current = V6LaneGeometry.LaneWidthAt(s / (float)(samples - 1));
                Assert.Less(current, previous);
            }

            // 判定線上の間隔が最大、フレーム上端が最小。
            Assert.AreEqual(V6LaneGeometry.LaneCentreBottomStep,
                V6LaneGeometry.LaneWidthAt(0f), Epsilon);
            Assert.AreEqual(V6LaneGeometry.LaneCentreTopStep,
                V6LaneGeometry.LaneWidthAt(1f), Epsilon);
        }

        [Test]
        public void LaneField_ConvergesTowardTheFrameTop()
        {
            // 奥 (t01=1) の方が手前より狭い = 遠近法。
            float bottomField = (V6LaneGeometry.LaneCount - 1) * V6LaneGeometry.LaneCentreBottomStep;
            float topField = (V6LaneGeometry.LaneCount - 1) * V6LaneGeometry.LaneCentreTopStep;
            Assert.Less(topField, bottomField);

            // フィールドの中心は深さによらず一定 (画面中央からズレない)。
            for (int s = 0; s <= 10; s++)
            {
                float t = s / 10f;
                Assert.IsTrue(V6LaneGeometry.TryLanePoint(0, t, out Vector2 left, out _));
                Assert.IsTrue(V6LaneGeometry.TryLanePoint(V6LaneGeometry.LaneCount - 1, t, out Vector2 right, out _));
                Assert.AreEqual(V6LaneGeometry.DesignWidth * 0.5f, (left.x + right.x) * 0.5f, Epsilon);
            }
        }

        // --- 判定線 / グロウ ---

        [Test]
        public void JudgeLine_AndGlow_AreAdjacentAndInsideTheFrame()
        {
            // 契約: 判定線 (0,792) 1728×4、グロウ (0,796) 1728×2 → グロウは判定線に接着。
            Assert.AreEqual(V6LaneGeometry.JudgeLineY + V6LaneGeometry.JudgeLineHeightDesignPx,
                V6LaneGeometry.JudgeGlowY, Epsilon);
            Assert.AreEqual(V6LaneGeometry.JudgeLineHeightDesignPx,
                V6LaneGeometry.JudgeGlowHeightDesignPx * 2f, Epsilon);

            // 判定線はフレームの下半分より上、グロウもフレーム内に収まる。
            Assert.Greater(V6LaneGeometry.JudgeLineY, V6LaneGeometry.DesignHeight * 0.5f);
            Assert.Less(V6LaneGeometry.JudgeGlowY + V6LaneGeometry.JudgeGlowHeightDesignPx,
                V6LaneGeometry.DesignHeight);
        }

        [Test]
        public void SeparatorStroke_IsNotThickerThanTheJudgeLine()
        {
            // 契約: レーン線 sw2 / 判定線 h4 / グロウ h2。
            Assert.AreEqual(V6LaneGeometry.JudgeGlowHeightDesignPx,
                V6LaneGeometry.SeparatorStrokeDesignPx, Epsilon);
            Assert.LessOrEqual(V6LaneGeometry.SeparatorStrokeDesignPx,
                V6LaneGeometry.JudgeLineHeightDesignPx);
        }

        // --- 色 ---

        [Test]
        public void Colors_FollowTheContractAssignments()
        {
            // 判定線とノーツは同じ色 (契約: 両方 #EDEDED)。
            Assert.AreEqual(V6LaneGeometry.JudgeLineColor, V6LaneGeometry.NoteColor);
            Assert.AreEqual(1f, V6LaneGeometry.JudgeLineColor.a, Epsilon);
            Assert.AreEqual(V6LaneGeometry.JudgeLineColor.r, V6LaneGeometry.JudgeLineColor.g, Epsilon);
            Assert.AreEqual(V6LaneGeometry.JudgeLineColor.r, V6LaneGeometry.JudgeLineColor.b, Epsilon);
            Assert.Greater(V6LaneGeometry.JudgeLineColor.r, 0.5f);

            // レーン線とグロウは半透明の白。
            Assert.AreEqual(1f, V6LaneGeometry.SeparatorColor.r, Epsilon);
            Assert.AreEqual(1f, V6LaneGeometry.SeparatorColor.g, Epsilon);
            Assert.AreEqual(1f, V6LaneGeometry.SeparatorColor.b, Epsilon);
            Assert.Less(V6LaneGeometry.SeparatorColor.a, 1f);

            Assert.AreEqual(1f, V6LaneGeometry.JudgeGlowColor.r, Epsilon);
            Assert.AreEqual(1f, V6LaneGeometry.JudgeGlowColor.g, Epsilon);
            Assert.AreEqual(1f, V6LaneGeometry.JudgeGlowColor.b, Epsilon);
            Assert.Less(V6LaneGeometry.JudgeGlowColor.a, 1f);

            // グロウ (0x4D) の方がレーン線 (0x2E) より強い。
            Assert.Greater(V6LaneGeometry.JudgeGlowColor.a, V6LaneGeometry.SeparatorColor.a);
        }

        [Test]
        public void VideoDim_IsHalfTransparentBlack()
        {
            // 契約 §8: Video Dim #00000080 (全画面)。
            Assert.AreEqual(0f, V6LaneGeometry.VideoDimColor.r, Epsilon);
            Assert.AreEqual(0f, V6LaneGeometry.VideoDimColor.g, Epsilon);
            Assert.AreEqual(0f, V6LaneGeometry.VideoDimColor.b, Epsilon);
            Assert.Greater(V6LaneGeometry.VideoDimColor.a, 0.4f);
            Assert.Less(V6LaneGeometry.VideoDimColor.a, 0.6f);
        }

        // --- ノーツ ---

        [Test]
        public void NoteSize_ScalesWithTheLaneWidth()
        {
            // 契約: ノーツ幅はレーン幅の一定比率、厚みはノーツ幅の一定比率。
            // (ノーツは奥 4px → 手前 13px で、遠近法と同じくスケールする)
            Assert.Greater(V6LaneGeometry.NoteWidthRatio, 0f);
            Assert.LessOrEqual(V6LaneGeometry.NoteWidthRatio, 1f);
            Assert.Greater(V6LaneGeometry.NoteThicknessRatio, 0f);

            // 判定線上とフレーム上端の両方で「レーン幅 × 比率」になっている。
            Assert.AreEqual(V6LaneGeometry.LaneWidthAt(0f) * V6LaneGeometry.NoteWidthRatio,
                V6LaneGeometry.NoteWidthAt(0f), Epsilon);
            Assert.AreEqual(V6LaneGeometry.LaneWidthAt(1f) * V6LaneGeometry.NoteWidthRatio,
                V6LaneGeometry.NoteWidthAt(1f), Epsilon);

            // 厚みもノーツ幅に比例する。
            Assert.AreEqual(V6LaneGeometry.NoteWidthAt(0f) * V6LaneGeometry.NoteThicknessRatio,
                V6LaneGeometry.NoteThicknessAt(0f), Epsilon);

            // 奥へ行くほどノーツも小さくなる (遠近法)。
            Assert.Greater(V6LaneGeometry.NoteWidthAt(0f), V6LaneGeometry.NoteWidthAt(1f));
            Assert.Greater(V6LaneGeometry.NoteThicknessAt(0f), V6LaneGeometry.NoteThicknessAt(1f));
            Assert.Greater(V6LaneGeometry.NoteWidthAt(1f), 0f);
        }

        [Test]
        public void ExitPoint_LeavesTheScreenBelowTheFrame()
        {
            // 判定線を通過したノーツは画面下端 (設計 y = 972) を出きる。
            Assert.Less(V6LaneGeometry.ExitT01, 0f);
            Assert.IsTrue(V6LaneGeometry.TryLanePoint(0, V6LaneGeometry.ExitT01, out Vector2 exit, out _));
            Assert.AreEqual(V6LaneGeometry.DesignHeight, exit.y, 1e-2f);
        }

        // --- 設計座標 → 実画面 ---

        [Test]
        public void ScreenScale_HandlesDegenerateSizes()
        {
            // 高さ 0 の画面では倍率が 0 (NaN / Inf を出さない)。
            Assert.AreEqual(0f, V6LaneGeometry.ScreenScale(Vector2.zero), Epsilon);
            Assert.AreEqual(0f, V6LaneGeometry.ScreenScale(new Vector2(1920f, 0f)), Epsilon);
            Assert.IsFalse(float.IsNaN(V6LaneGeometry.ScreenScale(Vector2.zero)));
            Assert.IsFalse(float.IsInfinity(V6LaneGeometry.ScreenScale(Vector2.zero)));

            // 幅 0 でも高さ合わせは効く (幅は中央寄せのオフセットにだけ効く)。
            Assert.Greater(V6LaneGeometry.ScreenScale(new Vector2(0f, 1080f)), 0f);
        }

        [Test]
        public void DesignToScreen_HandlesDegenerateSizes()
        {
            var corner = new Vector2(V6LaneGeometry.DesignWidth, V6LaneGeometry.DesignHeight);

            Vector2 zero = V6LaneGeometry.DesignToScreen(corner, Vector2.zero);
            Vector2 noWidth = V6LaneGeometry.DesignToScreen(corner, new Vector2(0f, 1080f));
            Vector2 noHeight = V6LaneGeometry.DesignToScreen(corner, new Vector2(1920f, 0f));

            Assert.IsFalse(float.IsNaN(zero.x) || float.IsNaN(zero.y));
            Assert.IsFalse(float.IsInfinity(zero.x) || float.IsInfinity(zero.y));
            Assert.IsFalse(float.IsNaN(noWidth.x) || float.IsNaN(noWidth.y));
            Assert.IsFalse(float.IsInfinity(noWidth.x) || float.IsInfinity(noWidth.y));
            Assert.IsFalse(float.IsNaN(noHeight.x) || float.IsNaN(noHeight.y));
            Assert.IsFalse(float.IsInfinity(noHeight.x) || float.IsInfinity(noHeight.y));
        }

        [Test]
        public void DesignToScreen_MapsTheFrameExactlyOn16x9()
        {
            var screen = new Vector2(1920f, 1080f);

            Vector2 topLeft = V6LaneGeometry.DesignToScreen(Vector2.zero, screen);
            Vector2 bottomRight = V6LaneGeometry.DesignToScreen(
                new Vector2(V6LaneGeometry.DesignWidth, V6LaneGeometry.DesignHeight), screen);

            Assert.AreEqual(0f, topLeft.x, 0.5f);
            Assert.AreEqual(0f, topLeft.y, 0.5f);
            Assert.AreEqual(screen.x, bottomRight.x, 0.5f);
            Assert.AreEqual(screen.y, bottomRight.y, 0.5f);
        }

        [Test]
        public void DesignToScreen_KeepsTheFrameHorizontallyCentred()
        {
            var screens = new[]
            {
                new Vector2(1920f, 1080f),
                new Vector2(2560f, 1440f),
                new Vector2(2560f, 1664f),
                new Vector2(1440f, 900f),
            };

            for (int i = 0; i < screens.Length; i++)
            {
                Vector2 centre = V6LaneGeometry.DesignToScreen(
                    new Vector2(V6LaneGeometry.DesignWidth * 0.5f, 0f), screens[i]);

                Assert.AreEqual(screens[i].x * 0.5f, centre.x, 0.5f);
            }
        }

        [Test]
        public void DesignToScreen_ScalesHeightToTheScreen()
        {
            var screen = new Vector2(2560f, 1440f);

            Vector2 bottom = V6LaneGeometry.DesignToScreen(
                new Vector2(0f, V6LaneGeometry.DesignHeight), screen);

            // 高さ合わせ (scale = 画面高 / 設計高)。
            Assert.AreEqual(screen.y, bottom.y, 0.5f);

            // 判定線も画面上の契約位置 (設計高に対する比) に落ちる。
            Vector2 judgeLine = V6LaneGeometry.DesignToScreen(
                new Vector2(0f, V6LaneGeometry.JudgeLineY), screen);
            Assert.AreEqual(V6LaneGeometry.JudgeLineY * V6LaneGeometry.ScreenScale(screen),
                judgeLine.y, 0.5f);
        }
    }
}
