using System.IO;
using NUnit.Framework;
using NoteEditor.Model;
using NoteEditor.Notes;
using EditorNote = NoteEditor.Notes.Note;
using NoteEditor.Presenter;
using UniRx;
using UnityEngine;

namespace FlashBeat.Tests.Editor
{
    // Issue #225 の独立レビュー B-1: 「ノーツ編集が未保存の変更として検知される」
    // ことがテストで固定されていなかった (テストが EditData.IsDirty を手代入していた)。
    //
    // ここでは IsDirty を直接代入しない。
    // 本番と同じ購読 (EditNotesPresenter.Init → NoteEditDirtyTracker) を通して
    // ノーツ編集操作を実際に行い、dirty が立つことを検証する。
    public class NoteEditorDirtyTrackingTests
    {
        string tempDir;
        GameObject audioGo;

        [SetUp]
        public void SetUp()
        {
            SingletonTestHelper.EnsureSingletons();
            audioGo = new GameObject("NoteEditorDirtyTrackingAudio");
            var source = audioGo.AddComponent<AudioSource>();
            typeof(Audio).GetField("source_", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(Audio.Instance, source);

            EditData.Notes.Clear();
            EditData.Name.Value = "";
            EditData.IsDirty.Value = false;

            tempDir = Path.Combine(Path.GetTempPath(), "flashbeat_dirty_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            SingletonTestHelper.TeardownSingletons();
            if (audioGo != null) UnityEngine.Object.DestroyImmediate(audioGo);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }

        static NotePosition Position(int num)
        {
            return new NotePosition(4, num, 0);
        }

        // EditData.Notes へ既存ノーツを入れる (削除・状態変更の要求が
        // 実機と同じ対象を指すようにするため)。
        static NoteObject AddExistingNote(NotePosition position)
        {
            var noteObject = new NoteObject();
            noteObject.SetState(new EditorNote(position, NoteTypes.Single));
            noteObject.Init();
            EditData.Notes.Add(position, noteObject);
            return noteObject;
        }

        // 本番と同じ購読を始める (Awake と同じ経路)。
        static EditNotesPresenter StartPresenter()
        {
            var presenter = EditNotesPresenter.Instance;
            presenter.Init();
            return presenter;
        }

        [Test]
        public void AddingNote_MarksSongAsDirty()
        {
            var presenter = StartPresenter();
            Assert.IsFalse(EditData.IsDirty.Value, "初期状態で dirty が立っています");

            presenter.RequestForAddNote.OnNext(new EditorNote(Position(1), NoteTypes.Single));

            Assert.IsTrue(EditData.IsDirty.Value, "ノーツ追加が dirty として検知されていません");
        }

        [Test]
        public void RemovingNote_MarksSongAsDirty()
        {
            var presenter = StartPresenter();
            var position = Position(2);
            AddExistingNote(position);
            Assert.IsFalse(EditData.IsDirty.Value);

            presenter.RequestForRemoveNote.OnNext(new EditorNote(position, NoteTypes.Single));

            Assert.IsTrue(EditData.IsDirty.Value, "ノーツ削除が dirty として検知されていません");
        }

        [Test]
        public void ChangingNoteStatus_MarksSongAsDirty()
        {
            var presenter = StartPresenter();
            var position = Position(3);
            AddExistingNote(position);
            Assert.IsFalse(EditData.IsDirty.Value);

            presenter.RequestForChangeNoteStatus.OnNext(new EditorNote(position, NoteTypes.Long));

            Assert.IsTrue(EditData.IsDirty.Value, "ノーツ状態変更が dirty として検知されていません");
        }

        // 検知された dirty は保存で落ちる (保存=未保存変更なし)。
        [Test]
        public void SavingAfterNoteEdit_ClearsDirty()
        {
            var presenter = StartPresenter();
            EditData.Name.Value = "DirtySong";
            presenter.RequestForAddNote.OnNext(new EditorNote(Position(4), NoteTypes.Single));
            Assert.IsTrue(EditData.IsDirty.Value);

            ChartFileWriter.SaveCurrentSong(tempDir);

            Assert.IsFalse(EditData.IsDirty.Value, "保存後に dirty が残っています");
        }

        // NoteEditDirtyTracker 単体の契約 (どの編集操作でも dirty が立つ)。
        [Test]
        public void DirtyTracker_MarksDirtyOnEveryEditOperation()
        {
            var isDirty = new ReactiveProperty<bool>(false);
            var added = new Subject<EditorNote>();
            var removed = new Subject<EditorNote>();
            var changed = new Subject<EditorNote>();

            var tracker = new NoteEditDirtyTracker(isDirty, added, removed, changed);

            added.OnNext(new EditorNote(Position(5), NoteTypes.Single));
            Assert.IsTrue(isDirty.Value, "追加で dirty が立ちません");

            isDirty.Value = false;
            removed.OnNext(new EditorNote(Position(6), NoteTypes.Single));
            Assert.IsTrue(isDirty.Value, "削除で dirty が立ちません");

            isDirty.Value = false;
            changed.OnNext(new EditorNote(Position(7), NoteTypes.Long));
            Assert.IsTrue(isDirty.Value, "状態変更で dirty が立ちません");

            Assert.IsNotNull(tracker);
        }
    }
}
