using System;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// The scoring contract shared by the play session, the result screen and
    /// the ranking submission (ADR-0028, Issue #577; v3 components added by
    /// ADR-0029, Issue #602).
    ///
    /// scoringVersion 1 is the legacy formula (`Judge.ComputeScore` over
    /// `judgment / (noteCount * 5)`). scoringVersion 2 adds the combo
    /// continuation bonus and the no-miss clear bonus:
    ///
    ///   judgment    = 5 * Perfect + 3 * Great + 1 * Bad
    ///   comboBonus  = maxCombo * 5                (maxCombo counts Bad-kept runs;
    ///                                            only a time-expiry Miss breaks it)
    ///   noMissBonus = miss == 0 ? (Perfect + Great + Bad) * 5 : 0
    ///   ratioScore  = judgment + comboBonus + noMissBonus
    ///   maxRatio    = noteCount * 15              (theoretical maximum)
    ///   score       = ComputeScore(ratioScore, maxRatio)  // same rounding as v1
    ///
    /// scoringVersion 3 (ADR-0029) keeps every v2 component and adds the
    /// periodic combo-milestone bonus and the full-combo end bonus:
    ///
    ///   milestoneBonus = comboMilestones * 25     (one crossing per
    ///                                            MilestoneInterval combo;
    ///                                            crossings recur after a break)
    ///   finalBonus     = judgmentsComplete && miss == 0 ? 100 : 0
    ///                    (vests once every chart note has been judged —
    ///                    the end-of-play condition the server can verify
    ///                    via perfect+great+bad+miss == noteCount; it is
    ///                    never part of the live in-progress projection)
    ///   maxRatio       = noteCount * 15 + floor(noteCount / 50) * 25 + 100
    ///
    /// The v1/v2 float order mirrors the server recomputation in
    /// `web/routes/api/v1/_score-model.ts` (`computeUnityScoreV2`): every
    /// component is a single-precision value and the components are summed
    /// in double precision and rounded once.
    ///
    /// The v3 score itself is platform-independent (Issue #631): every v3
    /// component is an integer, so <see cref="ScoreV3(V3Breakdown)"/> uses
    /// exact integer floor division instead of single-precision division.
    /// Mono JIT, IL2CPP and the Web worker therefore produce the identical
    /// score on every platform, and a legitimate submission can never be
    /// rejected with 422 by a last-ulp float difference.
    ///
    /// scoringVersion 4 (ADR-0033, Issue #736) splits the score into a
    /// judgment-derived base and an additive bonus instead of normalizing
    /// everything into one 1,000,000 ceiling:
    ///
    ///   baseScore  = floor(judgment * 1_000_000 / (5 * noteCount))   ≤ 1,000,000
    ///   bonusScore = floor(bonusRatio * 1_000_000 / maxBonusRatio)   ≤ 1,000,000
    ///   score      = baseScore + bonusScore                          ≤ 2,000,000
    ///
    /// where bonusRatio / maxBonusRatio reuse the v3 bonus components
    /// (comboBonus + noMissBonus + milestoneBonus + finalBonus). A perfect
    /// full-combo run scores exactly 2,000,000. Like v3, every step is
    /// exact integer arithmetic.
    ///
    /// scoringVersion 5 (ADR-0035, Issue #797) keeps the v4 two-half
    /// structure but removes the continuous combo bonus: combo rewards
    /// are paid only when a 50-combo milestone is crossed.
    ///
    ///   milestoneBonus = comboMilestones * MilestoneBonusPointsV5 (250)
    ///   bonusRatio     = milestoneBonus + noMissBonus + finalBonus
    ///   maxBonusRatio  = floor(noteCount / 50) * 250 + noteCount * 5 + 100
    ///
    /// A completed no-miss play keeps its v4 total (bonusRatio saturates
    /// the denominator); only plays containing a miss score differently.
    /// </summary>
    public static class Scoring
    {
        /// <summary>scoringVersion of the legacy v1 formula.</summary>
        public const int Version1 = 1;

        /// <summary>scoringVersion of the v2 formula (combo + no-miss bonus).</summary>
        public const int Version2 = 2;

        /// <summary>scoringVersion of the v3 formula (ADR-0029: milestones + full-combo bonus).</summary>
        public const int Version3 = 3;

        /// <summary>
        /// scoringVersion of the v4 formula (ADR-0033): the judgment-derived
        /// base score keeps the 1,000,000 ceiling and the combo/milestone
        /// bonuses are added on top, so a full-combo run exceeds 1,000,000.
        /// </summary>
        public const int Version4 = 4;

        /// <summary>
        /// scoringVersion of the v5 formula (ADR-0035, Issue #797): the
        /// continuous combo bonus (maxCombo * 5) is removed and the combo
        /// reward vests only at milestone crossings, so reaching a milestone
        /// is the single score event.
        /// </summary>
        public const int Version5 = 5;

        /// <summary>The scoring version new plays and new submissions use.</summary>
        public const int LatestVersion = Version5;

        /// <summary>v2 denominator per note: judgment cap 5 + combo cap 5 + no-miss cap 5.</summary>
        public const float MaxRatioPerNoteV2 = 15f;

        // v3 constants (ADR-0029). These are contract values shared with the
        // server recompute — do not tune them per chart or for convenience.
        /// <summary>Combo count step between milestone bonuses.</summary>
        public const int MilestoneInterval = 50;
        /// <summary>Ratio points awarded per milestone crossing.</summary>
        public const int MilestoneBonusPoints = 25;
        /// <summary>Ratio points of the full-combo (miss == 0) end-of-song bonus.</summary>
        public const int FullComboBonusPoints = 100;

        /// <summary>The v3 denominator of a chart with <paramref name="noteCount"/> notes.</summary>
        public static float MaxRatioV3(int noteCount)
        {
            // 空の譜面にはスコアを与えない (サーバも noteCount<=0 を拒否する)。
            if (noteCount <= 0)
            {
                return 0f;
            }

            // floor(noteCount / MilestoneInterval) is exact in single
            // precision for every chart size the game accepts.
            return noteCount * MaxRatioPerNoteV2
                + (noteCount / MilestoneInterval) * MilestoneBonusPoints
                + FullComboBonusPoints;
        }

        /// <summary>The v2 components of one play, in ratio points.</summary>
        public struct V2Breakdown
        {
            /// <summary>5P + 3G + B (the v1 judgment total).</summary>
            public float Judgment;
            /// <summary>maxCombo * 5. Keeps its value after a later Miss.</summary>
            public int ComboBonus;
            /// <summary>(P + G + B) * 5 while miss == 0, else 0.</summary>
            public int NoMissBonus;
            /// <summary>judgment + comboBonus + noMissBonus (single-precision).</summary>
            public float RatioScore;
            /// <summary>noteCount * 15 (single-precision).</summary>
            public float MaxRatio;
        }

        /// <summary>
        /// The v2 components of a play. `noteCount` is the chart's note count
        /// (the denominator only; it does not enter the ratio itself).
        ///
        /// <para>
        /// TEST / PARITY ONLY (Issue #641): no production caller remains —
        /// plays score through <see cref="BreakdownV3"/>/<see cref="ScoreV3"/>.
        /// Kept because the v2 parity fixtures (<c>ParityFixtureTests</c>,
        /// <c>ScoringV2Tests</c>) pin the legacy formula against the server
        /// recomputation (<c>computeUnityScoreV2</c>).
        /// </para>
        /// </summary>
        public static V2Breakdown BreakdownV2(int perfect, int great, int bad, int miss,
            int maxCombo, int noteCount)
        {
            float judgment = perfect * 5f + great * 3f + bad;
            int comboBonus = maxCombo * 5;
            int noMissBonus = miss == 0 ? (perfect + great + bad) * 5 : 0;
            // Sum in double and round once: identical to the server's
            // Math.fround(judgment + comboBonus + noMissBonus).
            float ratio = (float)((double)judgment + comboBonus + noMissBonus);
            return new V2Breakdown
            {
                Judgment = judgment,
                ComboBonus = comboBonus,
                NoMissBonus = noMissBonus,
                RatioScore = ratio,
                MaxRatio = noteCount * MaxRatioPerNoteV2,
            };
        }

        /// <summary>
        /// The v2 score (0..1,000,000) of a play, via Judge.ComputeScore.
        ///
        /// TEST / PARITY ONLY (Issue #641): production scoring is
        /// <see cref="ScoreV3"/>; this stays for the v2 parity fixtures.
        /// </summary>
        public static int ScoreV2(int perfect, int great, int bad, int miss,
            int maxCombo, int noteCount)
        {
            V2Breakdown b = BreakdownV2(perfect, great, bad, miss, maxCombo, noteCount);
            return Judge.ComputeScore(b.RatioScore, b.MaxRatio);
        }

        /// <summary>
        /// The score-point contribution of one v2 component, for the result
        /// breakdown display. The components do not have to add up to the
        /// total exactly (the total is floored once); this is a display
        /// estimate of each component's share, not a submission field.
        ///
        /// TEST / PARITY ONLY (Issue #641): kept for the v2 fixtures; the
        /// result screen displays v3 components via
        /// <see cref="ComponentScoreV3"/>.
        /// </summary>
        public static int ComponentScoreV2(float componentRatio, int noteCount)
        {
            float maxRatio = noteCount * MaxRatioPerNoteV2;
            if (maxRatio <= 0f)
            {
                return 0;
            }

            return (int)Math.Round(1000000.0 * componentRatio / maxRatio);
        }

        /// <summary>The v3 components of one play, in ratio points.</summary>
        public struct V3Breakdown
        {
            /// <summary>5P + 3G + B (the v1 judgment total).</summary>
            public float Judgment;
            /// <summary>maxCombo * 5. Keeps its value after a later Miss.</summary>
            public int ComboBonus;
            /// <summary>(P + G + B) * 5 while miss == 0, else 0.</summary>
            public int NoMissBonus;
            /// <summary>comboMilestones * 25. Crossings recur after a combo break.</summary>
            public int MilestoneBonus;
            /// <summary>
            /// 100 while judgmentsComplete && miss == 0, else 0. Vests only
            /// once every chart note has been judged (Issue #618).
            /// </summary>
            public int FinalBonus;
            /// <summary>judgment + all bonuses (single-precision).</summary>
            public float RatioScore;
            /// <summary>MaxRatioV3(noteCount) (single-precision).</summary>
            public float MaxRatio;
        }

        /// <summary>
        /// The v3 components of a play. `noteCount` is the chart's note
        /// count; `comboMilestones` is the number of milestone crossings the
        /// session recorded (see JudgeScoreboard.MilestoneCrossings);
        /// `judgmentsComplete` tells whether every chart note has been
        /// judged (perfect + great + bad + miss == noteCount) — the end-of-
        /// play condition the full-combo bonus vests on (Issue #618).
        /// </summary>
        public static V3Breakdown BreakdownV3(int perfect, int great, int bad, int miss,
            int maxCombo, int comboMilestones, int noteCount, bool judgmentsComplete)
        {
            float judgment = perfect * 5f + great * 3f + bad;
            int comboBonus = maxCombo * 5;
            int hits = perfect + great + bad;
            int noMissBonus = miss == 0 ? hits * 5 : 0;
            int milestoneBonus = comboMilestones * MilestoneBonusPoints;
            int finalBonus = judgmentsComplete && miss == 0 ? FullComboBonusPoints : 0;
            // Sum in double and round once: identical to the server's
            // Math.fround(judgment + comboBonus + noMissBonus + milestoneBonus + finalBonus).
            float ratio = (float)((double)judgment + comboBonus + noMissBonus
                + milestoneBonus + finalBonus);
            return new V3Breakdown
            {
                Judgment = judgment,
                ComboBonus = comboBonus,
                NoMissBonus = noMissBonus,
                MilestoneBonus = milestoneBonus,
                FinalBonus = finalBonus,
                RatioScore = ratio,
                MaxRatio = MaxRatioV3(noteCount),
            };
        }

        /// <summary>
        /// The v3 score (0..1,000,000) of a play, via the deterministic
        /// integer computation of <see cref="ScoreV3(V3Breakdown)"/>.
        /// `judgmentsComplete` has the same meaning as in
        /// <see cref="BreakdownV3"/>.
        /// </summary>
        public static int ScoreV3(int perfect, int great, int bad, int miss,
            int maxCombo, int comboMilestones, int noteCount, bool judgmentsComplete)
        {
            return ScoreV3(BreakdownV3(perfect, great, bad, miss, maxCombo, comboMilestones, noteCount, judgmentsComplete));
        }

        /// <summary>
        /// The v3 score of a computed breakdown (Issue #631).
        ///
        /// Every v3 component is an integer (the judgment total, the bonus
        /// amounts and the MaxRatioV3 denominator are all whole ratio
        /// points), so the score is the exact floor of
        /// `ratio * 1_000_000 / maxRatio` computed in 64-bit integers. This
        /// is deliberately NOT the legacy `Judge.ComputeScore` float path:
        /// single-precision division rounds differently on the Editor Mono
        /// JIT, on IL2CPP and under the Web `Math.fround` emulation, which
        /// could reject a legitimate score with 422 over a one-ulp
        /// difference. Integer arithmetic has no such platform dependence.
        /// </summary>
        public static int ScoreV3(V3Breakdown b)
        {
            // The breakdown fields are integer-valued floats by construction
            // (every component is a whole number of ratio points and the
            // sums stay far below 2^24), so the long casts are exact.
            long ratio = (long)b.Judgment + b.ComboBonus + b.NoMissBonus
                + b.MilestoneBonus + b.FinalBonus;
            long max = (long)b.MaxRatio;
            if (max <= 0)
            {
                return 0;
            }

            return (int)(ratio * 1000000L / max);
        }

        /// <summary>
        /// The score-point contribution of one v3 component, for the result
        /// breakdown and in-play bonus display. The components do not have
        /// to add up to the total exactly (the total is floored once); this
        /// is a display estimate of each component's share, not a
        /// submission field.
        /// </summary>
        public static int ComponentScoreV3(float componentRatio, int noteCount)
        {
            return ComponentScoreV3(componentRatio, MaxRatioV3(noteCount));
        }

        /// <summary>
        /// ComponentScoreV3 against an already-computed v3 denominator
        /// (GManager.maxScore holds MaxRatioV3(noteCount) during play).
        /// </summary>
        public static int ComponentScoreV3(float componentRatio, float maxRatio)
        {
            if (maxRatio <= 0f)
            {
                return 0;
            }

            return (int)Math.Round(1000000.0 * componentRatio / maxRatio);
        }

        // --- scoring v4 (ADR-0033, Issue #736) -----------------------------

        /// <summary>
        /// The v4 ceiling of one half of the score. The base (judgment) half
        /// and the bonus half are each normalized to their own theoretical
        /// maximum of 1,000,000, so the total can reach 2,000,000.
        /// </summary>
        public const int HalfScoreMaxV4 = 1000000;

        /// <summary>The largest score a v4 play can reach (base + bonus).</summary>
        public const int ScoreMaxV4 = 2 * HalfScoreMaxV4;

        /// <summary>HalfScoreMaxV4 as a long for the integer divisions below.</summary>
        private const long HalfScoreMaxV4L = HalfScoreMaxV4;

        /// <summary>
        /// The v4 bonus denominator of a chart with <paramref name="noteCount"/>
        /// notes: the sum of the simultaneously achievable bonus caps —
        /// comboBonus ≤ 5N, noMissBonus ≤ 5N, milestoneBonus ≤
        /// floor(N/50)*25, finalBonus ≤ 100.
        /// </summary>
        public static float MaxBonusRatioV4(int noteCount)
        {
            // 空の譜面にはスコアを与えない (サーバも noteCount<=0 を拒否する)。
            if (noteCount <= 0)
            {
                return 0f;
            }

            // 全て整数で、実用上の譜面サイズでは float でも厳密。
            return noteCount * 10f
                + (noteCount / MilestoneInterval) * MilestoneBonusPoints
                + FullComboBonusPoints;
        }

        /// <summary>The v4 components of one play.</summary>
        public struct V4Breakdown
        {
            /// <summary>5P + 3G + B (the v1 judgment total).</summary>
            public float Judgment;
            /// <summary>maxCombo * 5. Keeps its value after a later Miss.</summary>
            public int ComboBonus;
            /// <summary>(P + G + B) * 5 while miss == 0, else 0.</summary>
            public int NoMissBonus;
            /// <summary>comboMilestones * 25. Crossings recur after a combo break.</summary>
            public int MilestoneBonus;
            /// <summary>
            /// 100 while judgmentsComplete && miss == 0, else 0. Vests only
            /// once every chart note has been judged (Issue #618).
            /// </summary>
            public int FinalBonus;
            /// <summary>
            /// Judgment-derived base score: floor(judgment * 1e6 / (5N)).
            /// Always ≤ 1,000,000; exactly 1,000,000 for an all-Perfect play.
            /// </summary>
            public int BaseScore;
            /// <summary>
            /// Additive bonus score: floor(bonusRatio * 1e6 / maxBonusRatio).
            /// Always ≤ 1,000,000 and ≥ 0.
            /// </summary>
            public int BonusScore;
            /// <summary>BaseScore + BonusScore (≤ 2,000,000).</summary>
            public int Score;
            /// <summary>judgment + all bonus ratio points (informational).</summary>
            public float RatioScore;
            /// <summary>MaxBonusRatioV4(noteCount) (single-precision).</summary>
            public float MaxBonusRatio;
        }

        /// <summary>
        /// The v4 components of a play. The arguments have the same meaning
        /// as <see cref="BreakdownV3"/>; only the score mapping changes
        /// (ADR-0033): the judgment part is normalized to its own
        /// 1,000,000 ceiling and the v3 bonus components are converted to
        /// score points against <see cref="MaxBonusRatioV4"/> and added on
        /// top.
        /// </summary>
        public static V4Breakdown BreakdownV4(int perfect, int great, int bad, int miss,
            int maxCombo, int comboMilestones, int noteCount, bool judgmentsComplete)
        {
            float judgment = perfect * 5f + great * 3f + bad;
            int comboBonus = maxCombo * 5;
            int hits = perfect + great + bad;
            int noMissBonus = miss == 0 ? hits * 5 : 0;
            int milestoneBonus = comboMilestones * MilestoneBonusPoints;
            int finalBonus = judgmentsComplete && miss == 0 ? FullComboBonusPoints : 0;
            int bonusRatio = comboBonus + noMissBonus + milestoneBonus + finalBonus;
            float maxBonusRatio = MaxBonusRatioV4(noteCount);

            // Base: the judgment-derived score with the v1 ceiling.
            // judgment is an integer-valued float; compute in long like
            // ScoreV3 so the result is platform-independent (Issue #631).
            int baseScore = 0;
            if (noteCount > 0)
            {
                long judgmentLong = perfect * 5L + great * 3L + bad;
                baseScore = (int)(judgmentLong * HalfScoreMaxV4 / (5L * noteCount));
            }

            int bonusScore = 0;
            if (maxBonusRatio > 0f)
            {
                bonusScore = (int)(bonusRatio * HalfScoreMaxV4L / (long)maxBonusRatio);
            }

            return new V4Breakdown
            {
                Judgment = judgment,
                ComboBonus = comboBonus,
                NoMissBonus = noMissBonus,
                MilestoneBonus = milestoneBonus,
                FinalBonus = finalBonus,
                BaseScore = baseScore,
                BonusScore = bonusScore,
                Score = baseScore + bonusScore,
                RatioScore = (float)((double)judgment + bonusRatio),
                MaxBonusRatio = maxBonusRatio,
            };
        }

        /// <summary>
        /// The v4 score (0..2,000,000) of a play, via the deterministic
        /// integer computation of <see cref="ScoreV4(V4Breakdown)"/>.
        /// `judgmentsComplete` has the same meaning as in
        /// <see cref="BreakdownV3"/>.
        /// </summary>
        public static int ScoreV4(int perfect, int great, int bad, int miss,
            int maxCombo, int comboMilestones, int noteCount, bool judgmentsComplete)
        {
            return ScoreV4(BreakdownV4(perfect, great, bad, miss, maxCombo,
                comboMilestones, noteCount, judgmentsComplete));
        }

        /// <summary>
        /// The v4 score of a computed breakdown (ADR-0033).
        ///
        /// <see cref="BreakdownV4"/> already produced the two floored halves
        /// in exact integer arithmetic; this returns their sum. The ceiling
        /// is 2,000,000 (1,000,000 base + 1,000,000 bonus).
        /// </summary>
        public static int ScoreV4(V4Breakdown b)
        {
            return b.Score;
        }

        /// <summary>
        /// The score-point contribution of one v4 bonus component, for the
        /// result breakdown and in-play bonus display. Uses the same floor
        /// division as <see cref="BreakdownV4"/>'s bonus half; the
        /// components do not have to add up to <see cref="V4Breakdown.BonusScore"/>
        /// exactly (the total is floored once), so this is a display
        /// estimate, not a submission field.
        /// </summary>
        public static int ComponentScoreV4(int bonusRatioPoints, int noteCount)
        {
            long maxBonusRatio = (long)MaxBonusRatioV4(noteCount);
            if (maxBonusRatio <= 0)
            {
                return 0;
            }

            return (int)(bonusRatioPoints * HalfScoreMaxV4L / maxBonusRatio);
        }

        /// <summary>
        /// ComponentScoreV4 against an already-computed v4 bonus denominator
        /// (GManager.maxBonusRatio holds MaxBonusRatioV4(noteCount) during
        /// play).
        /// </summary>
        public static int ComponentScoreV4(int bonusRatioPoints, float maxBonusRatio)
        {
            if (maxBonusRatio <= 0f)
            {
                return 0;
            }

            return (int)(bonusRatioPoints * HalfScoreMaxV4L / (long)maxBonusRatio);
        }

        // --- scoring v5 (ADR-0035, Issue #797) -----------------------------

        /// <summary>
        /// Ratio points awarded per milestone crossing in v5. The removed
        /// v4 combo bonus (maxCombo * 5) had a cap of 5N ratio points; 250
        /// per crossing makes the milestone total cap floor(N/50) * 250 ≈ 5N,
        /// i.e. the milestone mechanism inherits the combo bonus budget.
        /// </summary>
        public const int MilestoneBonusPointsV5 = 250;

        /// <summary>The largest score a v5 play can reach (same 2,000,000 ceiling as v4).</summary>
        public const int ScoreMaxV5 = ScoreMaxV4;

        /// <summary>
        /// The v5 bonus denominator of a chart with
        /// <paramref name="noteCount"/> notes: the simultaneously achievable
        /// bonus caps — milestoneBonus ≤ floor(N/50) * 250, noMissBonus ≤ 5N,
        /// finalBonus ≤ 100. There is no continuous combo term.
        /// </summary>
        public static float MaxBonusRatioV5(int noteCount)
        {
            // 空の譜面にはスコアを与えない (サーバも noteCount<=0 を拒否する)。
            if (noteCount <= 0)
            {
                return 0f;
            }

            // 全て整数で、実用上の譜面サイズでは float でも厳密。
            return (noteCount / MilestoneInterval) * (float)MilestoneBonusPointsV5
                + noteCount * 5f
                + FullComboBonusPoints;
        }

        /// <summary>The v5 components of one play.</summary>
        public struct V5Breakdown
        {
            /// <summary>5P + 3G + B (the v1 judgment total).</summary>
            public float Judgment;
            /// <summary>
            /// comboMilestones * 250. Crossings accumulate across combo
            /// breaks; each crossing is one vesting event (ADR-0035).
            /// </summary>
            public int MilestoneBonus;
            /// <summary>(P + G + B) * 5 while miss == 0, else 0.</summary>
            public int NoMissBonus;
            /// <summary>
            /// 100 while judgmentsComplete && miss == 0, else 0. Vests only
            /// once every chart note has been judged (Issue #618).
            /// </summary>
            public int FinalBonus;
            /// <summary>
            /// Judgment-derived base score: floor(judgment * 1e6 / (5N)).
            /// Always ≤ 1,000,000; exactly 1,000,000 for an all-Perfect play.
            /// </summary>
            public int BaseScore;
            /// <summary>
            /// Additive bonus score: floor(bonusRatio * 1e6 / maxBonusRatio).
            /// Always ≤ 1,000,000 and ≥ 0.
            /// </summary>
            public int BonusScore;
            /// <summary>BaseScore + BonusScore (≤ 2,000,000).</summary>
            public int Score;
            /// <summary>judgment + all bonus ratio points (informational).</summary>
            public float RatioScore;
            /// <summary>MaxBonusRatioV5(noteCount) (single-precision).</summary>
            public float MaxBonusRatio;
        }

        /// <summary>
        /// The v5 components of a play. Same arguments as
        /// <see cref="BreakdownV4"/>; the only difference is the bonus
        /// composition (ADR-0035): the continuous combo bonus is gone and
        /// the milestone term carries the combo reward at 250 ratio points
        /// per crossing.
        /// </summary>
        public static V5Breakdown BreakdownV5(int perfect, int great, int bad, int miss,
            int maxCombo, int comboMilestones, int noteCount, bool judgmentsComplete)
        {
            float judgment = perfect * 5f + great * 3f + bad;
            int hits = perfect + great + bad;
            int noMissBonus = miss == 0 ? hits * 5 : 0;
            int milestoneBonus = comboMilestones * MilestoneBonusPointsV5;
            int finalBonus = judgmentsComplete && miss == 0 ? FullComboBonusPoints : 0;
            int bonusRatio = milestoneBonus + noMissBonus + finalBonus;
            float maxBonusRatio = MaxBonusRatioV5(noteCount);

            // Base: the judgment-derived score with the v1 ceiling.
            int baseScore = 0;
            if (noteCount > 0)
            {
                long judgmentLong = perfect * 5L + great * 3L + bad;
                baseScore = (int)(judgmentLong * HalfScoreMaxV4 / (5L * noteCount));
            }

            int bonusScore = 0;
            if (maxBonusRatio > 0f)
            {
                bonusScore = (int)(bonusRatio * HalfScoreMaxV4L / (long)maxBonusRatio);
            }

            return new V5Breakdown
            {
                Judgment = judgment,
                MilestoneBonus = milestoneBonus,
                NoMissBonus = noMissBonus,
                FinalBonus = finalBonus,
                BaseScore = baseScore,
                BonusScore = bonusScore,
                Score = baseScore + bonusScore,
                RatioScore = (float)((double)judgment + bonusRatio),
                MaxBonusRatio = maxBonusRatio,
            };
        }

        /// <summary>
        /// The v5 score (0..2,000,000) of a play, via the deterministic
        /// integer computation of <see cref="ScoreV5(V5Breakdown)"/>.
        /// </summary>
        public static int ScoreV5(int perfect, int great, int bad, int miss,
            int maxCombo, int comboMilestones, int noteCount, bool judgmentsComplete)
        {
            return ScoreV5(BreakdownV5(perfect, great, bad, miss, maxCombo,
                comboMilestones, noteCount, judgmentsComplete));
        }

        /// <summary>The v5 score of a computed breakdown (ADR-0035).</summary>
        public static int ScoreV5(V5Breakdown b)
        {
            return b.Score;
        }

        /// <summary>
        /// The score-point contribution of one v5 bonus component, for the
        /// result breakdown and in-play bonus display. Same floor division
        /// as <see cref="BreakdownV5"/>'s bonus half; a display estimate,
        /// not a submission field.
        /// </summary>
        public static int ComponentScoreV5(int bonusRatioPoints, int noteCount)
        {
            long maxBonusRatio = (long)MaxBonusRatioV5(noteCount);
            if (maxBonusRatio <= 0)
            {
                return 0;
            }

            return (int)(bonusRatioPoints * HalfScoreMaxV4L / maxBonusRatio);
        }

        /// <summary>
        /// ComponentScoreV5 against an already-computed v5 bonus denominator
        /// (GManager.maxBonusRatio holds MaxBonusRatioV5(noteCount) during
        /// play).
        /// </summary>
        public static int ComponentScoreV5(int bonusRatioPoints, float maxBonusRatio)
        {
            if (maxBonusRatio <= 0f)
            {
                return 0;
            }

            return (int)(bonusRatioPoints * HalfScoreMaxV4L / (long)maxBonusRatio);
        }
    }
}
