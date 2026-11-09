using NoteEditor.Common;
using NoteEditor.Notes;
using NoteEditor.Model;
using NoteEditor.Utility;
using System.Linq;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

namespace NoteEditor.Presenter
{
    public class EditNotesPresenter : SingletonMonoBehaviour<EditNotesPresenter>
    {
        [SerializeField]
        CanvasEvents canvasEvents = default;

        // ノーツ編集のリクエスト。
        //
        // 遅延初期化 (= null なら生成) にしてあるのは、EditMode テストが
        // `RuntimeHelpers.GetUninitializedObject` でインスタンスを作るためで、
        // フィールド初期化子 (= new Subject<Note>()) が走らない。
        // テストからも購読を追跡できるようにするため here.
        Subject<Note> requestForEditNote_;
        Subject<Note> requestForRemoveNote_;
        Subject<Note> requestForAddNote_;
        Subject<Note> requestForChangeNoteStatus_;

        public Subject<Note> RequestForEditNote { get { return requestForEditNote_ ?? (requestForEditNote_ = new Subject<Note>()); } }
        public Subject<Note> RequestForRemoveNote { get { return requestForRemoveNote_ ?? (requestForRemoveNote_ = new Subject<Note>()); } }
        public Subject<Note> RequestForAddNote { get { return requestForAddNote_ ?? (requestForAddNote_ = new Subject<Note>()); } }
        public Subject<Note> RequestForChangeNoteStatus { get { return requestForChangeNoteStatus_ ?? (requestForChangeNoteStatus_ = new Subject<Note>()); } }

        // ノーツ編集を「未保存の変更」として追跡する購読。
        NoteEditDirtyTracker dirtyTracker_;

        // Init() を 1 回だけ実行するためのフラグ。
        bool initialized_;

        void Awake()
        {
            if (canvasEvents == null)
                Debug.LogWarning("[EditNotesPresenter] CanvasEvents が未割り当てです。マウス操作の購読は行いません。");

            Init();
        }

        // 初期化。
        //
        // dirty 追跡 (Issue #225) は常に行うので、テストからも
        // 購読の開始を観測できるように public にしてある。
        // 複数回呼ばれても購読が重複しないように 1 回だけ実行する。
        public void Init()
        {
            if (initialized_)
                return;

            initialized_ = true;

            // ノーツの追加・削除・状態変更は「未保存の変更」として扱う。
            // 曲切替時の確認ダイアログ (Issue #225) はこのフラグで判断する。
            SubscribeDirtyTracking();

            // CanvasEvents が未割り当て (テスト環境など) の場合は
            // マウス操作の購読だけを省く。dirty 追跡は上面で始まっている。
            if (canvasEvents == null)
                return;

            var closestNoteAreaOnMouseDownObservable = canvasEvents.NotesRegionOnMouseDownObservable
                .Where(_ => !KeyInput.CtrlKey())
                .Where(_ => !Input.GetMouseButtonDown(1))
                .Where(_ => 0 <= NoteCanvas.ClosestNotePosition.Value.num);

            closestNoteAreaOnMouseDownObservable
                .Where(_ => EditState.NoteType.Value == NoteTypes.Single)
                .Where(_ => !KeyInput.ShiftKey())
                .Merge(closestNoteAreaOnMouseDownObservable
                    .Where(_ => EditState.NoteType.Value == NoteTypes.Long))
                .Subscribe(_ =>
                {
                    if (EditData.Notes.ContainsKey(NoteCanvas.ClosestNotePosition.Value))
                    {
                        EditData.Notes[NoteCanvas.ClosestNotePosition.Value].OnClickObservable.OnNext(Unit.Default);
                    }
                    else
                    {
                        RequestForEditNote.OnNext(
                           new Note(
                               NoteCanvas.ClosestNotePosition.Value,
                               EditState.NoteType.Value,
                               NotePosition.None,
                               EditState.LongNoteTailPosition.Value));
                    }
                });


            // Start editing of long note
            closestNoteAreaOnMouseDownObservable
                .Where(_ => EditState.NoteType.Value == NoteTypes.Single)
                .Where(_ => KeyInput.ShiftKey())
                .Do(_ => EditState.NoteType.Value = NoteTypes.Long)
                .Subscribe(_ => RequestForAddNote.OnNext(
                    new Note(
                        NoteCanvas.ClosestNotePosition.Value,
                        NoteTypes.Long,
                        NotePosition.None,
                        NotePosition.None)));


            // Finish editing long note by press-escape or right-click
            this.UpdateAsObservable()
                .Where(_ => EditState.NoteType.Value == NoteTypes.Long)
                .Where(_ => Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
                .Subscribe(_ => EditState.NoteType.Value = NoteTypes.Single);

            var finishEditLongNoteObservable = EditState.NoteType.Where(editType => editType == NoteTypes.Single);

            finishEditLongNoteObservable.Subscribe(_ => EditState.LongNoteTailPosition.Value = NotePosition.None);


            RequestForRemoveNote.Buffer(RequestForRemoveNote.ThrottleFrame(1))
                .Select(b => b.OrderBy(note => note.position.ToSamples(EditData.AudioFrequency(), EditData.BPM.Value)).ToList())
                .Subscribe(notes => EditCommandManager.Do(
                    new Command(
                        () => notes.ForEach(RemoveNote),
                        () => notes.ForEach(AddNote))));

            RequestForAddNote.Buffer(RequestForAddNote.ThrottleFrame(1))
                .Select(b => b.OrderBy(note => note.position.ToSamples(EditData.AudioFrequency(), EditData.BPM.Value)).ToList())
                .Subscribe(notes => EditCommandManager.Do(
                    new Command(
                        () => notes.ForEach(AddNote),
                        () => notes.ForEach(RemoveNote))));

            RequestForChangeNoteStatus.Select(note => new { current = note, prev = EditData.Notes[note.position].note })
                .Buffer(RequestForChangeNoteStatus.ThrottleFrame(1))
                .Select(b => b.OrderBy(note => note.current.position.ToSamples(EditData.AudioFrequency(), EditData.BPM.Value)).ToList())
                .Subscribe(notes => EditCommandManager.Do(
                    new Command(
                        () => notes.ForEach(x => ChangeNoteStates(x.current)),
                        () => notes.ForEach(x => ChangeNoteStates(x.prev)))));


            // ノーツの追加・削除・状態変更は「未保存の変更」として扱う。
            // 曲切替時の確認ダイアログ (Issue #225) はこのフラグで判断する。
            // 読み込み (Deserialize) は NoteObject を直接作るため、ここでは反応しない。

            RequestForEditNote.Subscribe(note =>
            {
                if (note.type == NoteTypes.Single)
                {
                    (EditData.Notes.ContainsKey(note.position)
                        ? RequestForRemoveNote
                        : RequestForAddNote)
                    .OnNext(note);
                }
                else if (note.type == NoteTypes.Long)
                {
                    if (!EditData.Notes.ContainsKey(note.position))
                    {
                        RequestForAddNote.OnNext(note);
                        return;
                    }

                    var noteObject = EditData.Notes[note.position];
                    (noteObject.note.type == NoteTypes.Long
                        ? RequestForRemoveNote
                        : RequestForChangeNoteStatus)
                    .OnNext(noteObject.note);
                }
            });
        }

        // ノーツ編集を「未保存の変更」フラグに接続する購読を開始する。
        //
        // Init() から自動で呼ばれるが、テストからも明示的に呼べるよう public に
        // してある (B-1: テストが IsDirty を手代入せずに dirty 検知を固定できる)。
        // 複数回呼んでも購読は 1 つのまま。
        public NoteEditDirtyTracker SubscribeDirtyTracking()
        {
            if (dirtyTracker_ == null)
            {
                dirtyTracker_ = new NoteEditDirtyTracker(
                    EditData.IsDirty,
                    RequestForAddNote,
                    RequestForRemoveNote,
                    RequestForChangeNoteStatus);
            }

            return dirtyTracker_;
        }

        public void AddNote(Note note)
        {
            if (EditData.Notes.ContainsKey(note.position))
            {
                if (!EditData.Notes[note.position].note.Equals(note))
                    RequestForChangeNoteStatus.OnNext(note);

                return;
            }

            var noteObject = new NoteObject();
            noteObject.SetState(note);
            noteObject.Init();
            EditData.Notes.Add(noteObject.note.position, noteObject);
        }

        void ChangeNoteStates(Note note)
        {
            if (!EditData.Notes.ContainsKey(note.position))
                return;

            EditData.Notes[note.position].SetState(note);
        }

        void RemoveNote(Note note)
        {
            if (!EditData.Notes.ContainsKey(note.position))
                return;

            var noteObject = EditData.Notes[note.position];
            noteObject.Dispose();
            EditData.Notes.Remove(noteObject.note.position);
        }
    }
}
