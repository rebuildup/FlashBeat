namespace NoteEditor.Presenter.FlashBeatSong
{
    // 曲切替時のダイアログで提示する選択肢。
    public enum SongSwitchChoice
    {
        // 保存して切り替え
        SaveAndSwitch,
        // 保存せずに切り替え (編集を破棄)
        DiscardAndSwitch,
        // キャンセル (現在の曲を保持し、編集も残す)
        Cancel,
    }

    // 選択を元に実際に何をするか。
    public enum SongSwitchAction
    {
        // 保存せずにそのまま切り替える
        SwitchWithoutSaving,
        // 保存してから切り替える
        SaveThenSwitch,
        // 切り替えずに現在の曲を保持する
        StayOnCurrentSong,
    }

    // 曲切替の判断ロジック (UI を持たない純粋な部分)。
    // ダイアログは UI だけを担当し、このクラスで「何をするか」を決める。
    // テストはここだけを直接検証し、UI は実機操作でしか検証できない。
    public static class SongSwitchDecision
    {
        // 未保存の変更があるときだけ確認ダイアログを出す必要があるか。
        public static bool NeedsConfirmation(bool hasUnsavedChanges)
        {
            return hasUnsavedChanges;
        }

        // ユーザーの選択を実際の動作に翻訳する。
        public static SongSwitchAction Resolve(SongSwitchChoice choice)
        {
            switch (choice)
            {
                case SongSwitchChoice.SaveAndSwitch:
                    return SongSwitchAction.SaveThenSwitch;
                case SongSwitchChoice.DiscardAndSwitch:
                    return SongSwitchAction.SwitchWithoutSaving;
                default:
                    return SongSwitchAction.StayOnCurrentSong;
            }
        }
    }
}
