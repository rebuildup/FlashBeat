using System;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// The scoring contract shared by the play session, the result screen and
    /// the ranking submission (ADR-0028, Issue #577).
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
    /// The float order mirrors the server recomputation in
    /// `web/routes/api/v1/_score-model.ts` (`computeUnityScoreV2`): every
    /// component is a single-precision value and the three components are
    /// summed in double precision and rounded once, so a legitimate client
    /// score is never rejected with 422 by one-ulp differences.
    /// </summary>
    public static class Scoring
    {
        /// <summary>scoringVersion of the legacy v1 formula.</summary>
        public const int Version1 = 1;

        /// <summary>scoringVersion of the v2 formula (combo + no-miss bonus).</summary>
        public const int Version2 = 2;

        /// <summary>The scoring version new plays and new submissions use.</summary>
        public const int LatestVersion = Version2;

        /// <summary>v2 denominator per note: judgment cap 5 + combo cap 5 + no-miss cap 5.</summary>
        public const float MaxRatioPerNoteV2 = 15f;

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
    }
}
