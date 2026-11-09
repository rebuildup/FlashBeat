using System;
using System.IO;

namespace NoteEditor.Presenter.FlashBeatSong
{
    // 曲切替の要求の結果。
    //
    // 独立レビュー S-4: 以前の bool 戻り値は「切り替えが成立したか」を
    // 表すと読めて実際は違う (ダイアログを出しただけの場合も true) ので、
    // 状態ごとに明示的な値を返す。
    public enum SongSwitchResult
    {
        // パスが空など、要求自体が不正で処理されなかった。
        InvalidRequest,
        // 確認なしで曲に切り替わった。
        Switched,
        // 確認ダイアログを出し、ユーザーの選択を待っている。
        AwaitingChoice,
        // ダイアログ表示中のため新しい要求を受け付けなかった。
        Busy,
        // 保存に失敗したため切り替えを中止した。
        SaveFailed,
        // 読み込みに失敗したため切り替えを中止した。
        LoadFailed,
    }

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

        // 指定した JSON を開く。成功したら true を返す。
        // 失敗した場合は error に理由を返す (UI に出すため)。
        public delegate bool LoadSong(string jsonPath, out string error);

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
        public SongSwitchResult RequestSwitch(string jsonPath)
        {
            if (string.IsNullOrEmpty(jsonPath))
                return SongSwitchResult.InvalidRequest;

            if (!SongSwitchDecision.NeedsConfirmation(hasUnsavedChanges_()))
            {
                return LoadOrReportError(jsonPath)
                    ? SongSwitchResult.Switched
                    : SongSwitchResult.LoadFailed;
            }

            var message = BuildMessage(currentSongName_(), Path.GetFileNameWithoutExtension(jsonPath));
            if (!prompt_.Show(message, choice => Complete(jsonPath, choice)))
            {
                // ダイアログ表示中に別の要求が来た場合、保留中の切替を
                // 上書きしない (消えてしまうのを防ぐ)。
                return SongSwitchResult.Busy;
            }

            return SongSwitchResult.AwaitingChoice;
        }

        // ダイアログで選ばれた選択を実際の動作へ翻訳する。
        void Complete(string jsonPath, SongSwitchChoice choice)
        {
            switch (SongSwitchDecision.Resolve(choice))
            {
                case SongSwitchAction.SaveThenSwitch:
                    // 保存に失敗したら切り替えを中止する (変更を失わないため)。
                    if (!saveCurrentSong_())
                    {
                        prompt_.ShowError("現在の曲を保存できなかったため、曲の切り替えを中止しました。");
                        return;
                    }

                    LoadOrReportError(jsonPath);
                    return;

                case SongSwitchAction.SwitchWithoutSaving:
                    LoadOrReportError(jsonPath);
                    return;

                case SongSwitchAction.StayOnCurrentSong:
                    // 現在の曲も編集中の変更もそのまま残す。何もしない。
                    return;
            }
        }

        // 読み込み失敗は UI に出す (独立レビュー S-3)。
        bool LoadOrReportError(string jsonPath)
        {
            string error;
            if (loadSong_(jsonPath, out error))
                return true;

            var message = "曲を読み込めませんでした。";
            if (!string.IsNullOrEmpty(error))
                message = message + "\n" + error;

            prompt_.ShowError(message);
            return false;
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
