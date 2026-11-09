using UnityEngine;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// 判定回数とスコアを集計する純粋クラス (Issue #164 / #182)。
    ///
    /// 「1 ノーツ = 1 判定」をこのクラスで保証する:
    ///
    ///   - 先頭の押下で 1 回だけ加点する (<see cref="ApplyHead"/>)。
    ///   - ロングノーツを途中で離した (中断) ときは、先頭判定を BAD に
    ///     差し替える (<see cref="DowngradeHoldToBad"/>)。BAD を **追加で**
    ///     加算しないので、1 ノーツの配点が <c>maxScore = note数 x 5</c> を
    ///     超えることはない (旧実装の二重加点を廃止)。
    ///   - Miss はコンボを切るだけ。
    ///
    /// Judge (MonoBehaviour) がこの集計を GManager の static フィールドへ
    /// 写すので、他シーン (ResultScene など) からの参照方式は変わらない。
    /// </summary>
    public class JudgeScoreboard
    {
        public int Perfect;
        public int Great;
        public int Bad;
        public int Miss;
        public int Combo;
        public int MaxCombo;

        /// <summary>
        /// 確定したノーツ数 (Issue #182)。
        ///
        /// タップ / HOLD の先頭の押下と Miss が 1 ずつ数える。HOLD の中断
        /// (<see cref="DowngradeHoldToBad"/>) は先頭判定の差し替えなので
        /// 数えない。よって「1 ノーツ = 1 判定」であり、
        /// <c>Settled == Perfect + Great + Bad + Miss</c> が成り立つ。
        ///
        /// MusicManager はこの値を GManager.nextNotes 経由で「確定済みノーツ数」
        /// として曲終了の判定に使うので、ノーツ数と一致していなければならない。
        /// </summary>
        public int Settled;

        /// <summary>配点の合計 (1 ノーツ最大 5)。スコア表示は GManager 側で行う。</summary>
        public float RatioScore;

        /// <summary>先頭の押下 1 回分を加算する。</summary>
        public void ApplyHead(LongNote.Judgement judgement)
        {
            RatioScore += LongNote.ScoreFor(judgement);
            if (judgement == LongNote.Judgement.Perfect) Perfect++;
            else if (judgement == LongNote.Judgement.Great) Great++;
            else if (judgement == LongNote.Judgement.Bad) Bad++;

            Combo++;
            if (Combo > MaxCombo) MaxCombo = Combo;

            // 押下は 1 ノーツの確定 (HOLD の先頭も 1 ノーツ = 1 判定)。
            Settled++;
        }

        /// <summary>
        /// 保持中のロングノーツを途中で離した (中断) ときの整理。
        ///
        /// 先頭判定を BAD に差し替える: Perfect / Great だった分を取り消して
        /// BAD (1 点) にする。すでに BAD なら何も変わらない。コンボは
        /// 先頭で 1 回だけ数えたまま (中断で追加も消失もしない)。
        /// </summary>
        public void DowngradeHoldToBad(LongNote.Judgement headJudgement)
        {
            if (headJudgement == LongNote.Judgement.Bad) return;

            if (headJudgement == LongNote.Judgement.Perfect)
            {
                Perfect--;
                RatioScore -= LongNote.SCORE_PERFECT;
            }
            else if (headJudgement == LongNote.Judgement.Great)
            {
                Great--;
                RatioScore -= LongNote.SCORE_GREAT;
            }
            Bad++;
            RatioScore += LongNote.SCORE_BAD;
        }

        /// <summary>時刻切れ (Miss) 1 回分。コンボは切れる。</summary>
        public void ApplyMiss()
        {
            Miss++;
            Combo = 0;

            // Miss も 1 ノーツの確定。
            Settled++;
        }
    }
}
