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
    /// </summary>
    public static class Scoring
    {
        /// <summary>scoringVersion of the legacy v1 formula.</summary>
        public const int Version1 = 1;

        /// <summary>scoringVersion of the v2 formula (combo + no-miss bonus).</summary>
        public const int Version2 = 2;

        /// <summary>scoringVersion of the v3 formula (ADR-0029: milestones + full-combo bonus).</summary>
        public const int Version3 = 3;

        /// <summary>The scoring version new plays and new submissions use.</summary>
        public const int LatestVersion = Version3;

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

        /// <summary>The v2 score (0..1,000,000) of a play, via Judge.ComputeScore.</summary>
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
            if (b.MaxRatio <= 0f)
            {
                return 0;
            }

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
    }
}
