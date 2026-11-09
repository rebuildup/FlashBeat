using System;

namespace NoteEditor.Presenter.FlashBeatSong
{
    // 未保存変更があるときの確認 UI との境界。
    // ダイアログ (UI) はこのインターフェースだけを持ち、
    // 呼び出し側は「どの選択がされたか」だけを受け取る。
    public interface ISongSwitchPrompt
    {
        // 3 択のダイアログを表示し、選択結果を onChoice で通知する。
        // 選択は UI 側の操作後に非同期に呼ばれるためコールバック式。
        void Show(string message, Action<SongSwitchChoice> onChoice);
    }
}
