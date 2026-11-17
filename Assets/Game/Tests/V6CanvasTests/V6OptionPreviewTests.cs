using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6.Option;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// Option 画面 Game Preview の 3D 化 (Issue #579) の検証。
    ///
    /// - well の中身が静止 2D 図形ではなく、専用カメラの RenderTexture を
    ///   貼った RawImage であること。
    /// - 疑似チャート (<see cref="V6OptionPreviewChart"/>) の時刻畳み込みと
    ///   NoteTiming 適用が本編と同じ式であること。
    /// </summary>
    public sealed class V6OptionPreviewTests
    {
        private GameObject canvasGo;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6OptionPreviewTests Canvas", typeof(RectTransform), typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            var view = V6OptionView.Current;
            if (view != null)
            {
                view.Dispose();
            }

            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }
            canvasGo = null;
        }

        private V6OptionView CreateView()
        {
            return V6OptionView.Create(canvasGo.GetComponent<RectTransform>(), () => { });
        }

        // --- 統合: well は RenderTexture を貼る RawImage ---

        [Test]
        public void GamePreview_UsesARenderTextureRawImage()
        {
            var view = CreateView();

            var raw = (RectTransform)view.Root.Find("Main/Preview/Game Preview/Game Preview 3D");
            Assert.IsNotNull(raw, "Game Preview well の中に 3D プレビュー用 RawImage が無い");

            var image = raw.GetComponent<RawImage>();
            Assert.IsNotNull(image, "Game Preview 3D は RawImage でなければならない");
            Assert.IsInstanceOf<RenderTexture>(image.texture,
                "静止 2D 図形ではなく、専用カメラの RenderTexture を表示する");
        }

        // --- 疑似チャート: 決定的で全レーンをカバーする ---

        [Test]
        public void PreviewChart_IsDeterministic_AndCoversEveryLane()
        {
            var a = V6OptionPreviewChart.Build();
            var b = V6OptionPreviewChart.Build();
            Assert.AreEqual(a.Length, b.Length);
            Assert.Greater(a.Length, Judge.LaneCount, "全レーンにノーツがある");

            var seen = new bool[Judge.LaneCount];
            foreach (var note in a)
            {
                Assert.GreaterOrEqual(note.Lane, 0);
                Assert.Less(note.Lane, Judge.LaneCount);
                Assert.GreaterOrEqual(note.Time, 0f);
                Assert.Less(note.Time, V6OptionPreviewChart.LoopSeconds);
                Assert.GreaterOrEqual(note.HoldSeconds, 0f);
                seen[note.Lane] = true;
            }
            for (int i = 0; i < seen.Length; i++)
            {
                Assert.IsTrue(seen[i], "lane " + i + " にノーツが無い");
            }

            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].Lane, b[i].Lane);
                Assert.AreEqual(a[i].Time, b[i].Time);
                Assert.AreEqual(a[i].HoldSeconds, b[i].HoldSeconds);
            }
        }

        // --- 時刻畳み込み: 差は (-Loop/2, +Loop/2] に収まる ---

        [Test]
        public void DeltaToNearest_FoldsIntoTheLoopRange()
        {
            float half = V6OptionPreviewChart.LoopSeconds * 0.5f;
            for (float media = -100f; media < 100f; media += 3.7f)
            {
                for (float chart = 0f; chart < V6OptionPreviewChart.LoopSeconds; chart += 5.3f)
                {
                    float delta = V6OptionPreviewChart.DeltaToNearest(chart, media);
                    Assert.Greater(delta, -half - 0.0001f, "delta too small");
                    Assert.LessOrEqual(delta, half + 0.0001f, "delta too large");
                }
            }
        }

        // --- NoteTiming: 本編の NoteTimeWithTiming と同じオフセット ---

        [Test]
        public void DeltaWithTiming_AppliesTheSameOffsetAsTheGame()
        {
            const float chart = 12.5f;
            const float media = 10f;
            const int timing = 7;

            float expected = V6OptionPreviewChart.DeltaToNearest(
                NotesManager.NoteTimeWithTiming(chart, timing), media);
            Assert.AreEqual(expected,
                V6OptionPreviewChart.DeltaWithTiming(chart, timing, media), 0.0001f,
                "NoteTiming は 0.01 秒単位で本編と同じ式に乗る");

            // timing = 0 なら素の時刻差と一致する。
            Assert.AreEqual(
                V6OptionPreviewChart.DeltaToNearest(chart, media),
                V6OptionPreviewChart.DeltaWithTiming(chart, 0, media), 0.0001f);
        }

        // --- z 換算: NotePositionZ と同じ (delta * zScale) ---

        [Test]
        public void PreviewZ_MatchesMediaTimelineNotePositionZ()
        {
            // rig の Tick が使う式 z = delta * CurrentZScale が本編の
            // NotePositionZ(noteTime, mediaTime, zUnitsPerSecond) と一致する
            // ことを、畳み込みの無い範囲で確認する (ループ端は DeltaToNearest
            // のテストが担保する)。
            float zScale = MediaTimeline.ZScaleFor(10f);
            float delta = 1.25f;
            Assert.AreEqual(delta * zScale,
                MediaTimeline.NotePositionZ(11.25f, 10f, zScale), 0.0001f);
        }
    }
}
