using UnityEngine;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// ロングノーツ (HOLD) の判定ロジック (Issue #164)。
    ///
    /// 仕様は <c>specs/164-long-note/spec.md</c> に凍結されている。要点は次の 3 つ:
    ///
    ///   1. **先頭 1 判定 + 終端での離し** (2 判定方式)。保持中 (開始〜終端) は
    ///      連続評価しない。押下はタップと同じ幅で判定し、終端で離した时被が
    ///      成功になる。
    ///   2. **保持中に離すと即 BAD**。押し直しても救済しない (1 note = 1 判定)。
    ///      救済ありにすると「離して BAD → 押し直して Perfect」で 1 note に
    ///      2 回配点|Carriage することになり、<c>maxScore = note数 x 5</c> の
    ///      定義と矛盾する。
    ///   3. **配点はタップと同一** (Perfect 5 / Great 3 / Bad 1)。既存の
    ///      ランキング記録の解釈が変わらない。
    ///
    /// このクラスは MonoBehaviour に依存しない純粋関数だけを持つ。Editor の
    /// EditMode テスト (Assets/Tests/Editor/LongNoteTests.cs) から直接呼べる。
    /// </summary>
    public static class LongNote
    {
        /// <summary>タップのノーツ種別 (Tap)。</summary>
        public const int TYPE_TAP = 1;

        /// <summary>ロングノーツのノーツ種別 (Hold)。</summary>
        public const int TYPE_HOLD = 2;

        /// <summary>Perfect の判定幅 (秒)。</summary>
        public const float PERFECT_WINDOW = 0.10f;

        /// <summary>Great の判定幅 (秒)。</summary>
        public const float GREAT_WINDOW = 0.15f;

        /// <summary>Bad の判定幅 (秒)。</summary>
        public const float BAD_WINDOW = 0.20f;

        /// <summary>Perfect のスコア。</summary>
        public const int SCORE_PERFECT = 5;

        /// <summary>Great のスコア。</summary>
        public const int SCORE_GREAT = 3;

        /// <summary>Bad のスコア。</summary>
        public const int SCORE_BAD = 1;

        /// <summary>判定結果。タップの判定表示と共有する。</summary>
        public enum Judgement
        {
            /// <summary>判定なし (時間外)。</summary>
            None = -1,

            /// <summary>Perfect。</summary>
            Perfect = 0,

            /// <summary>Great。</summary>
            Great = 1,

            /// <summary>Bad。</summary>
            Bad = 2,

            /// <summary>Miss。</summary>
            Miss = 3,
        }

        /// <summary>そのノーツがロングノーツか。</summary>
        public static bool IsHold(Note note)
        {
            return note != null && note.type == TYPE_HOLD;
        }

        /// <summary>
        /// ロングノーツの開始時刻 (秒)。
        ///
        /// <see cref="NotesManager.NoteTimeSeconds"/> と同じ式を使う:
        /// <c>60 / BPM * num / LPB</c>。
        /// </summary>
        public static float StartSeconds(int bpm, Note note)
        {
            return NotesManager.NoteTimeSeconds(bpm, note);
        }

        /// <summary>
        /// ロングノーツの終端時刻 (秒)。
        ///
        /// 終端は <c>endNum</c> (行番号) で表され、開始と同じ解像度で計算する:
        /// <c>60 / BPM * endNum / LPB</c>。
        /// </summary>
        public static float EndSeconds(int bpm, Note note)
        {
            if (note == null || note.LPB <= 0) return 0f;
            float kankaku = 60 / (bpm * (float)note.LPB);
            return kankaku * (float)note.endNum;
        }

        /// <summary>ロングノーツの長さ (秒)。</summary>
        public static float LengthSeconds(int bpm, Note note)
        {
            return EndSeconds(bpm, note) - StartSeconds(bpm, note);
        }

        /// <summary>
        /// 開始時の押下を判定する。
        ///
        /// タップと同じ幅を使う (Perfect &lt;= 0.10s / Great &lt;= 0.15s /
        /// Bad &lt;= 0.20s)。幅の外は何も起きない (==
        /// タップと同じで、单押しは問答無用)。
        /// </summary>
        public static Judgement JudgeHead(float timeLag)
        {
            if (timeLag <= PERFECT_WINDOW) return Judgement.Perfect;
            if (timeLag <= GREAT_WINDOW) return Judgement.Great;
            if (timeLag <= BAD_WINDOW) return Judgement.Bad;
            return Judgement.None;
        }

        /// <summary>
        /// 保持中かどうか (開始と終端の間にあるか)。
        ///
        /// 開始ちょうどと終端ちょうどは「保持中」とみなす。開始は押下の判定が
        /// 担当し、終端は離しの判定が担当するため。
        /// </summary>
        public static bool IsHolding(float mediaTime, float startSeconds, float endSeconds)
        {
            return mediaTime >= startSeconds && mediaTime <= endSeconds;
        }

        /// <summary>
        /// 終端での離しを判定する。
        ///
        /// 離した時刻が終端の GREAT_WINDOW 以内なら成功。それより前 (早すぎる
        /// 離し) でも後 (押しすぎ) でも失敗。
        ///
        /// 戻り値は成功したかのみ。スコアは開始時の判定で確定済みなので、ここでは
        /// 加点しない (加点すると <c>maxScore = note数 x 5</c> と矛盾する)。
        /// </summary>
        public static bool JudgeRelease(float releaseTime, float endSeconds)
        {
            return JudgeRelease(releaseTime, endSeconds, GREAT_WINDOW);
        }

        /// <summary>
        /// 終端での離しを判定する (判定幅を明示する版)。
        ///
        /// 実機経路 (JudgeQueue.UpdateHold) はこの版を使う。
        /// </summary>
        public static bool JudgeRelease(float releaseTime, float endSeconds, float greatWindow)
        {
            return Mathf.Abs(releaseTime - endSeconds) <= greatWindow;
        }

        /// <summary>
        /// 保持中の離しでInterrupt(中断)したか。
        ///
        /// 終端の許容幅の外でキーを離したら中断であり、BAD として確定する。
        /// 許容幅より前 (早すぎる離し) も後 (押しすぎ) も中断。
        /// </summary>
        public static bool IsBroken(float releaseTime, float endSeconds)
        {
            return IsBroken(releaseTime, endSeconds, GREAT_WINDOW);
        }

        /// <summary>
        /// 保持中の離しでInterrupt(中断)したか (判定幅を明示する版)。
        ///
        /// 実機経路 (JudgeQueue.UpdateHold) はこの版を使う。
        /// </summary>
        public static bool IsBroken(float releaseTime, float endSeconds, float greatWindow)
        {
            return !JudgeRelease(releaseTime, endSeconds, greatWindow);
        }

        /// <summary>
        /// 押しっぱなしで終端の許容幅をすぎたか (押し続けていた = 成功扱い)。
        ///
        /// 実機経路 (JudgeQueue.UpdateHold) はこの版を使う。
        /// </summary>
        public static bool IsHoldExpired(float mediaTime, float endSeconds, float greatWindow)
        {
            return mediaTime > endSeconds + greatWindow;
        }

        /// <summary>判定に対応するスコア。</summary>
        public static int ScoreFor(Judgement judgement)
        {
            switch (judgement)
            {
                case Judgement.Perfect: return SCORE_PERFECT;
                case Judgement.Great: return SCORE_GREAT;
                case Judgement.Bad: return SCORE_BAD;
                default: return 0;
            }
        }

        /// <summary>
        /// ロングノーツが最後まで成功したか。
        ///
        /// 開始で押して、保持して、終端で離した状態が成功。途中で離した
        /// (中断) 状態は成功しない。押し直しで戻すことはできない。
        /// </summary>
        public static bool Succeeded(bool headJudged, bool broken, bool releasedInTime)
        {
            return headJudged && !broken && releasedInTime;
        }
    }
}
