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

        // --- 実チャート優先 (Issue #677) ---

        /// <summary>
        /// <see cref="V6OptionPreviewChart.BuildForCurrentSong"/> が
        /// 選択曲の実チャート (無ければ疑似チャート) を返し、
        /// 全ノーツ時刻がループ長に収まること (時刻畳み込みの前提)。
        /// </summary>
        [Test]
        public void PreviewChart_ForCurrentSong_KeepsNotesInsideTheLoop()
        {
            float loopSeconds;
            var notes = V6OptionPreviewChart.BuildForCurrentSong(out loopSeconds);
            Assert.Greater(notes.Length, 0, "チャートが空");
            Assert.Greater(loopSeconds, 0f);
            foreach (var note in notes)
            {
                Assert.GreaterOrEqual(note.Lane, 0);
                Assert.Less(note.Lane, Judge.LaneCount);
                // beatOffset で僅かに負になり得るので下限は課さない
                // (時刻畳み込みは負時刻も扱える)。
                Assert.Less(note.Time, loopSeconds,
                    "ノーツ時刻がループ長を超えている (畳み込みが破綻する)");
                Assert.GreaterOrEqual(note.HoldSeconds, 0f);
            }
        }

        // --- 実描画: RenderTexture に実際の画素が出る ---

        /// <summary>
        /// well の RawImage が貼る RT を専用カメラで 1 フレーム描き、
        /// 背景 (透明黒) 以外の画素が十分にあることを確認する。
        /// 「RT が存在する」だけでは表示成功とみなさない (Issue #579)。
        /// ガイド線・判定線は疑似時計に依らず常に描かれるので、
        /// ノーツの位相に関係なく画素が出るはず。
        /// </summary>
        [Test]
        public void GamePreview_RendersActualPixels()
        {
            var view = CreateView();

            var raw = (RectTransform)view.Root.Find("Main/Preview/Game Preview/Game Preview 3D");
            Assert.IsNotNull(raw, "Game Preview 3D が無い");
            var rt = raw.GetComponent<RawImage>().texture as RenderTexture;
            Assert.IsNotNull(rt, "RawImage が RenderTexture を持たない");

            var rigRoot = GameObject.Find("V6 Option Preview Rig");
            Assert.IsNotNull(rigRoot, "プレビュー rig が生成されていない");
            var cam = rigRoot.transform.Find("Preview Camera")?.GetComponent<Camera>();
            Assert.IsNotNull(cam, "プレビュー専用カメラが無い");

            // EditMode にはプレイヤーループが無いので明示的に 1 フレーム描く。
            cam.Render();

            RenderTexture previous = RenderTexture.active;
            Texture2D shot = null;
            try
            {
                RenderTexture.active = rt;
                shot = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                shot.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
                shot.Apply();
            }
            finally
            {
                RenderTexture.active = previous;
            }

            Assert.IsNotNull(shot, "RenderTexture から画素を読めない");
            var pixels = shot.GetPixels32();
            int lit = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                // 透明黒クリア (0,0,0,0) 以外 = 何かが描かれた画素。
                var p = pixels[i];
                if (p.r > 8 || p.g > 8 || p.b > 8 || p.a > 8)
                {
                    lit++;
                }
            }
            Assert.Greater(lit, 500,
                "プレビュー RT がほぼ空 (lit=" + lit + "/" + pixels.Length + ") - 3D シーンが描画されていない");

            Object.DestroyImmediate(shot);
        }
    }
}
