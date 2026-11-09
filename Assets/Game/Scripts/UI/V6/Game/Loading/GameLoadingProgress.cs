using UnityEngine;

namespace FlashBeat.UI.V6.Game.Loading
{
    /// <summary>
    /// GameScene の曲準備の実進捗の共有口 (Issue #224)。
    ///
    /// NotesManager (Gameplay) が譜面準備の done/total を報告し、
    /// V6GameRuntime が読んで V6LoadingView へ渡す。旧 LoadingOverlay は
    /// この値を直接受け取っていたが、v6 有効時は表示を v6 が所有するため
    /// 値だけを静的に仲介する (GameLoadingRoute と同じ分割)。
    ///
    /// 値の意味:
    ///   - 未報告 (HasNotesProgress == false) = 進捗不明 → 表示側は不定インジケータ。
    ///   - 0..1 = 実進捗。0 も「既知」(準備がまだ始まったばかり) として扱う。
    ///
    /// メディア (動画/音源) 側の準備は MusicManager が段階名だけを持ち
    /// 数値進捗を持たない (MusicManager は本担当が編集不可)。数値があるのは
    /// 譜面準備のみ — 表示側はその旨をラベルで正直に示す。
    /// </summary>
    public static class GameLoadingProgress
    {
        /// <summary>未報告を表す内部値。</summary>
        const float Unknown = -1f;

        static float notesProgress = Unknown;

        /// <summary>譜面準備の進捗 (0..1)。未報告なら負値。</summary>
        public static float NotesProgress
        {
            get { return notesProgress; }
        }

        /// <summary>譜面準備の進捗を 1 回でも報告済みか。</summary>
        public static bool HasNotesProgress
        {
            get { return notesProgress >= 0f; }
        }

        /// <summary>譜面準備の進捗を報告する (0..1 にクランプ)。</summary>
        public static void ReportNotes(float normalized)
        {
            notesProgress = Mathf.Clamp01(normalized);
        }

        /// <summary>報告を消す (進捗不明に戻す)。譜面読み込み開始時に呼ぶ。</summary>
        public static void ResetNotes()
        {
            notesProgress = Unknown;
        }
    }
}
