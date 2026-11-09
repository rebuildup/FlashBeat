using System;
using System.IO;

namespace NoteEditor.Presenter.FlashBeatSong
{
    // 曲切替の orchestration。UI (ダイアログ) も Unity にも依存しないため、
    // EditMode テストから直接検証できる。
    //
    // Issue #225: 未保存の変更があるまま別曲へ切り替えると、
    // 前の曲のノーツが新曲に混入したまま新曲の JSON が上書きされる。
    // ここでは「未保存変更の検知 → 確認 → 保存 / 破棄 / キャンセル」の
    // 順序を 1 箇所に集約し、切り替えの可否をここでだけ決める。
    public class SongSwitchCoordinator
    {
        // いま編集中の曲を保存する。保存できた場合 true を返す。
        public delegate bool SaveCurrentSong();

        // 指定した JSON を開く。
        public delegate void LoadSong(string jsonPath);

        readonly Func<bool> hasUnsavedChanges_;
        readonly Func<string> currentSongName_;
        readonly ISongSwitchPrompt prompt_;
        readonly SaveCurrentSong saveCurrentSong_;
        readonly LoadSong loadSong_;

        public SongSwitchCoordinator(
            Func<bool> hasUnsavedChanges,
            Func<string> currentSongName,
            ISongSwitchPrompt prompt,
            SaveCurrentSong saveCurrentSong,
            LoadSong loadSong)
        {
            hasUnsavedChanges_ = hasUnsavedChanges;
            currentSongName_ = currentSongName;
            prompt_ = prompt;
            saveCurrentSong_ = saveCurrentSong;
            loadSong_ = loadSong;
        }

        // 曲切替を要求する。
        // 未保存の変更がない場合は確認なしで切り替える。
        // 未保存の変更がある場合は確認ダイアログを出し、選択結果に従う
        // (ダイアログの選択は後から通知されるため、このメソッドは同期的に戻らない)。
        public bool RequestSwitch(string jsonPath)
        {
            if (string.IsNullOrEmpty(jsonPath))
                return false;

            if (!SongSwitchDecision.NeedsConfirmation(hasUnsavedChanges_()))
            {
                loadSong_(jsonPath);
                return true;
            }

            var message = BuildMessage(currentSongName_(), Path.GetFileNameWithoutExtension(jsonPath));
            prompt_.Show(message, choice => Complete(jsonPath, choice));
            return true;
        }

        // ダイアログで選ばれた選択を実際の動作へ翻訳する。
        void Complete(string jsonPath, SongSwitchChoice choice)
        {
            switch (SongSwitchDecision.Resolve(choice))
            {
                case SongSwitchAction.SaveThenSwitch:
                    // 保存に失敗したら切り替えを中止する (変更を失わないため)。
                    if (!saveCurrentSong_())
                        return;

                    loadSong_(jsonPath);
                    return;

                case SongSwitchAction.SwitchWithoutSaving:
                    loadSong_(jsonPath);
                    return;

                case SongSwitchAction.StayOnCurrentSong:
                    // 現在の曲も編集中の変更もそのまま残す。何もしない。
                    return;
            }
        }

        // 確認ダイアログに出す文言。
        public static string BuildMessage(string currentSongName, string targetSongName)
        {
            var current = string.IsNullOrEmpty(currentSongName) ? "(未読込)" : currentSongName;
            var target = string.IsNullOrEmpty(targetSongName) ? "(不明)" : targetSongName;
            return $"曲「{current}」に未保存の変更があります。\n"
                + $"曲「{target}」に切り替えますか?\n"
                + "「保存して切り替え」を選ぶと現在の曲を保存してから切り替えます。";
        }
    }
}
