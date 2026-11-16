using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Game.Playfield;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.V6PlayfieldTests
{
    /// <summary>
    /// v6 プレイフィールドのノーツ描画 (Issue #219) の決定的フィクスチャテスト。
    ///
    /// 幾何 (レーン中心線 / 判定線 / スケール) は <see cref="V6PlayfieldGeometryTests"/>
    /// がカバーする。ここでは <see cref="Notes"/> がメディア時計
    /// (<c>GManager.step_time</c>) とセッション固定の z スケールから
    /// uGUI Image (head / HOLD の body) を正しい位置へ置くこと、
    /// および v6 フラグ OFF (既定 = 3D) で従来のワールド移動に退化しない
    /// ことを検証する。
    ///
    /// タイミング契約: noteTime = 5.0s、noteSpeed = 10 →
    /// SessionZScale = ZScaleFor(10) = 8.61/1.6、approach = 1.6s。
    /// step_time = noteTime - remaining で remaining を直接制御する。
    /// </summary>
    public class V6PlayfieldNotesTests
    {
        private const float NoteTime = 5f;
        private const float NoteSpeed = 10f;
        private const float ApproachSeconds = 1.6f;
        private const int Lane = 3;
        private const float Epsilon = 0.001f;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private bool savedPlayfield;
        private bool savedStart;
        private float savedStepTime;
        private float savedNoteSpeed;

        [SetUp]
        public void SetUp()
        {
            savedPlayfield = V6UiMode.Playfield;
            savedStart = GManager.Start;
            savedStepTime = GManager.step_time;
            savedNoteSpeed = GManager.noteSpeed;

            MediaTimeline.ResetSession();
            MediaTimeline.BeginSession(NoteSpeed);
            PauseGate.HardReset();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Object.DestroyImmediate(spawned[i]);
                }
            }
            spawned.Clear();

            V6UiMode.Playfield = savedPlayfield;
            GManager.Start = savedStart;
            GManager.step_time = savedStepTime;
            GManager.noteSpeed = savedNoteSpeed;
            MediaTimeline.ResetSession();
            PauseGate.HardReset();
        }

        // --- helpers ---

        private GameObject Track(GameObject go)
        {
            spawned.Add(go);
            return go;
        }

        /// <summary>設計フレーム 1728×972 の親を持つ v6 プレイフィールドを作る。</summary>
        private V6PlayfieldView CreateView()
        {
            var parentGo = Track(new GameObject("PlayfieldParent", typeof(RectTransform)));
            var prt = parentGo.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.zero;
            prt.sizeDelta = new Vector2(V6LaneGeometry.DesignWidth, V6LaneGeometry.DesignHeight);
            return V6PlayfieldView.Create(prt);
        }

        /// <summary>
        /// NotesManager の生成と同じ配置でノーツを作る
        /// (x = lane - 3.5, z = noteTime * SessionZScale)。
        /// </summary>
        private Notes CreateNote(float holdSeconds = 0f)
        {
            var go = Track(new GameObject("Note " + Lane, typeof(Notes)));
            // lane は RoundToInt(x - LaneFirstCenterX)。lane=3, LaneFirstCenterX=-3.5 → x=-0.5。
            go.transform.position = new Vector3(
                Lane + Judge.LaneFirstCenterX,
                MediaTimeline.NotePlaneY,
                NoteTime * MediaTimeline.SessionZScale);

            var notes = go.GetComponent<Notes>();
            notes.holdSeconds = holdSeconds;
            return notes;
        }

        private static void InvokeLifecycle(Notes notes, string method)
        {
            MethodInfo m = typeof(Notes).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(m, "Notes." + method + " が見つからない");
            m.Invoke(notes, null);
        }

        private void StartPlay(float remainingSeconds)
        {
            GManager.Start = true;
            GManager.step_time = NoteTime - remainingSeconds;
        }

        // --- tests ---

        [Test]
        public void V6Note_RendersOnLaneAtMediaClock()
        {
            V6UiMode.Playfield = true;
            V6PlayfieldView view = CreateView();
            Notes notes = CreateNote();

            StartPlay(ApproachSeconds * 0.5f); // remaining = 0.8 → t01 = 0.5
            InvokeLifecycle(notes, "Start");
            InvokeLifecycle(notes, "Update");

            Assert.IsNotNull(notes.HeadImage, "v6 有効時は uGUI の head Image が作られる");
            Assert.IsTrue(notes.HeadImage.gameObject.activeSelf, "アプローチ窓内なので表示される");

            const float t = 0.5f;
            Assert.IsTrue(V6LaneGeometry.TryLanePoint(Lane, t, out Vector2 design, out _));
            Vector2 expected = view.DesignToLocal(design);
            Assert.AreEqual(expected.x, notes.HeadImage.rectTransform.anchoredPosition.x, Epsilon,
                "ノーツ中心はレーン中心線上 (x)");
            Assert.AreEqual(expected.y, notes.HeadImage.rectTransform.anchoredPosition.y, Epsilon,
                "ノーツ中心はレーン中心線上 (y)");

            float px = view.PixelsPerDesignPixel;
            Assert.AreEqual(V6LaneGeometry.NoteWidthAt(t) * px,
                notes.HeadImage.rectTransform.sizeDelta.x, Epsilon, "幅は t01 のレーン幅に比例");
            Assert.AreEqual(V6LaneGeometry.NoteThicknessAt(t) * px,
                notes.HeadImage.rectTransform.sizeDelta.y, Epsilon, "厚みは t01 のノーツ幅に比例");
        }

        [Test]
        public void V6HoldNote_DrawsBodyFromHeadToTail()
        {
            V6UiMode.Playfield = true;
            V6PlayfieldView view = CreateView();
            Notes notes = CreateNote(holdSeconds: 1f);

            StartPlay(ApproachSeconds * 0.5f); // remaining = 0.8、hold 1.0 → tail t01 = 1.0
            InvokeLifecycle(notes, "Start");
            InvokeLifecycle(notes, "Update");

            Assert.IsNotNull(notes.HeadImage);
            // head / body は Notes の子ではなく V6PlayfieldView.NoteLayer の子。
            Image body = view.NoteLayer.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(img => img.gameObject.name == "Body");
            Assert.IsNotNull(body, "HOLD ノーツは body Image を持つ");
            Assert.IsTrue(body.gameObject.activeSelf, "アプローチ窓内なので body も表示される");
            Assert.Greater(body.rectTransform.sizeDelta.x, 0f,
                "body は head→tail の線分長を持つ");
            Assert.AreEqual(notes.HeadImage.rectTransform.anchoredPosition,
                body.rectTransform.anchoredPosition, "body の起点は head と同じ");
        }

        [Test]
        public void V6Note_StaysHiddenOutsideApproachWindow()
        {
            V6UiMode.Playfield = true;
            CreateView();
            Notes notes = CreateNote();

            InvokeLifecycle(notes, "Start");

            // まだ奥 (t01 > 1): remaining > approach
            StartPlay(ApproachSeconds * 1.25f);
            InvokeLifecycle(notes, "Update");
            Assert.IsFalse(notes.HeadImage.gameObject.activeSelf,
                "t01 > 1 (まだ画面の奥) では描かない");

            // 判定線通過後 (t01 < 0)
            StartPlay(-0.1f);
            InvokeLifecycle(notes, "Update");
            Assert.IsFalse(notes.HeadImage.gameObject.activeSelf,
                "判定線を通過したノーツは描かない");
        }

        [Test]
        public void ThreeDDefault_KeepsWorldLaneMovement_WithoutUGuiImages()
        {
            // 既定 (Playfield = false) では v6 ビューが無く、Notes は
            // 従来どおりワールド z をメディア時計で動かす (3D レーン)。
            V6UiMode.Playfield = false;
            Notes notes = CreateNote();

            StartPlay(ApproachSeconds * 0.5f);
            InvokeLifecycle(notes, "Start");
            float zBefore = notes.transform.position.z;
            InvokeLifecycle(notes, "Update");
            float zAfter = notes.transform.position.z;

            float expectedZ = MediaTimeline.NotePositionZ(
                NoteTime, GManager.step_time, MediaTimeline.SessionZScale);
            Assert.AreEqual(expectedZ, zAfter, 0.01f,
                "3D 経路はワールド z を NotePositionZ で更新する");
            Assert.AreNotEqual(zBefore, zAfter, "メディア時計の経過で z が動く");
            Assert.IsNull(notes.HeadImage, "v6 無しでは uGUI Image を作らない");
        }

        [Test]
        public void V6FlagOn_HidesThe3DMesh_AndKeepsJudgementOnMediaClock()
        {
            // v6 有効時は 3D メッシュを隠し、判定は時刻ベースのまま
            // (コライダーを切っても Judge は noteTime で判定する)。
            V6UiMode.Playfield = true;
            CreateView();

            var noteGo = Track(new GameObject("NoteMesh", typeof(Notes)));
            var child = new GameObject("Mesh", typeof(MeshRenderer));
            child.transform.SetParent(noteGo.transform, false);
            spawned.Add(child);
            noteGo.transform.position = new Vector3(
                Lane + Judge.LaneFirstCenterX,
                MediaTimeline.NotePlaneY,
                NoteTime * MediaTimeline.SessionZScale);
            var notes = noteGo.GetComponent<Notes>();

            StartPlay(ApproachSeconds * 0.5f);
            InvokeLifecycle(notes, "Start");
            InvokeLifecycle(notes, "Update");

            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            Assert.IsFalse(renderer.enabled, "v6 有効時は 3D メッシュを描かない");
            Assert.IsNotNull(notes.HeadImage, "代わりに uGUI Image が出る");
        }
    }
}
