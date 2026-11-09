using NoteEditor.Notes;
using UniRx;

namespace NoteEditor.Presenter
{
    // ノーツの編集操作を「未保存の変更」フラグに接続する。
    //
    // Issue #225: 曲切替の確認 (保存 / 破棄 / キャンセル) は
    // 「未保存の変更があるかどうか」で判断する。どこで変更されたかではなく
    // 「変更があった」ことだけを 1 箇所で判定するため、編集の入口と
    // dirty フラグの接続をこのクラスに集約する。
    //
    // MonoBehaviour に依存しないので EditMode テストから直接検証できる。
    // テストが `EditData.IsDirty.Value = true` を手代入せずに
    // 「編集操作が dirty を立てる」ことを固定できる (B-1)。
    public class NoteEditDirtyTracker
    {
        readonly ReactiveProperty<bool> isDirty_;

        public NoteEditDirtyTracker(
            ReactiveProperty<bool> isDirty,
            IObservable<Note> noteAdded,
            IObservable<Note> noteRemoved,
            IObservable<Note> noteStatusChanged)
        {
            isDirty_ = isDirty;

            noteAdded.Subscribe(_ => MarkDirty());
            noteRemoved.Subscribe(_ => MarkDirty());
            noteStatusChanged.Subscribe(_ => MarkDirty());
        }

        // 未保存の変更があったことを記録する。
        public void MarkDirty()
        {
            isDirty_.Value = true;
        }
    }
}
