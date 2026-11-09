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
        //
        // 既にダイアログが表示中の場合は要求を拒否して false を返す。
        // (保留中の切替が新しい要求で上書きされて消えるのを防ぐ。
        //  独立レビュー S-3)
        bool Show(string message, Action<SongSwitchChoice> onChoice);

        // エラーメッセージを通知する (保存失敗 / 読み込み失敗をユーザーへ見せる。
        // 独立レビュー S-3)。
        void ShowError(string message);
    }
}
