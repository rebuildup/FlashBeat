using System.Collections.Generic;
using NUnit.Framework;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.V5CanvasTests
{
    /// <summary>
    /// V6GradeLogic (Issue #758 / ADR-0034、帯の改訂は Issue #788) の純ロジック。
    /// scoringVersion ごとのスコア満点と v4 グレード帯、および実際の
    /// scoring v4 計算 (<see cref="Scoring.ScoreV4"/>) が生むスコアが
    /// 意図したグレード帯に入ることを検証する。
    ///
    /// Issue #788 の受入条件に従い、閾値の境界値だけでなく
    /// 「判定構成 × miss 数・配置」の成績分布シナリオが期待グレードに
    /// なることを独立に検証する (<see cref="BuildJudgments"/> /
    /// <see cref="PlayOf"/> が JudgeScoreboard と同じコンボ / 節目規則で
    /// 内訳を組み立てる)。
    /// </summary>
    public sealed class V6GradeLogicTests
    {
        // --- スコア満点 ---

        [Test]
        public void ScoreMaxOf_FollowsScoringVersion()
        {
            Assert.AreEqual(1000000, V6GradeLogic.ScoreMaxOf(Scoring.Version1));
            Assert.AreEqual(1000000, V6GradeLogic.ScoreMaxOf(Scoring.Version2));
            Assert.AreEqual(1000000, V6GradeLogic.ScoreMaxOf(Scoring.Version3));
            Assert.AreEqual(2000000, V6GradeLogic.ScoreMaxOf(Scoring.Version4));
            // v5 (ADR-0035) も同じ 2,000,000 満点 — ゲージ正規化は #788 の
            // グレード閾値と同じ正本 (ScoreMaxOf) から出る。
            Assert.AreEqual(2000000, V6GradeLogic.ScoreMaxOf(Scoring.Version5));
            Assert.AreEqual(2000000, V6GradeLogic.ScoreMaxOf(Scoring.LatestVersion));
        }

        // --- v4 グレード帯 (境界値。Issue #788: S 95% / A 70% / B 50%) ---

        [Test]
        public void GradeOfV4_BandBoundaries()
        {
            Assert.AreEqual("SS", V6GradeLogic.GradeOfV4(2000000));
            Assert.AreEqual("S", V6GradeLogic.GradeOfV4(1999999));
            Assert.AreEqual("S", V6GradeLogic.GradeOfV4(1900000));
            Assert.AreEqual("A", V6GradeLogic.GradeOfV4(1899999));
            Assert.AreEqual("A", V6GradeLogic.GradeOfV4(1800000));
            Assert.AreEqual("A", V6GradeLogic.GradeOfV4(1400000));
            Assert.AreEqual("B", V6GradeLogic.GradeOfV4(1399999));
            Assert.AreEqual("B", V6GradeLogic.GradeOfV4(1000000));
            Assert.AreEqual("C", V6GradeLogic.GradeOfV4(999999));
            Assert.AreEqual("C", V6GradeLogic.GradeOfV4(0));
            Assert.AreEqual("C", V6GradeLogic.GradeOfV4(-1));
        }

        [Test]
        public void GradeOf_DispatchesByScoringVersion()
        {
            // RankFor 系の帯: v4 以降は 2M 帯、v1–v3 は RankFor 閾値。
            Assert.AreEqual("SS", V6GradeLogic.GradeOf(2000000, Scoring.Version4));
            Assert.AreEqual("A", V6GradeLogic.GradeOf(1600000, Scoring.LatestVersion));
            // 同じスコアでも scoringVersion で帯が変わる: 950,000 は v4 帯では
            // C (B 境界 1,000,000 未満)、v1–v3 の RankFor では S (≥950,000)。
            // v5 (ADR-0035) は v4 と同じ帯 — 1,227,055 のようなスコアは
            // 引き続き B で、ゲージも score/2M の同じ写像に乗る。
            Assert.AreEqual("B", V6GradeLogic.GradeOf(1227055, Scoring.Version5));
            Assert.AreEqual("A", V6GradeLogic.GradeOf(1600000, Scoring.Version5));
            Assert.AreEqual("C", V6GradeLogic.GradeOf(950000, Scoring.LatestVersion));
            Assert.AreEqual("S", V6GradeLogic.GradeOf(950000, Scoring.Version3));
            Assert.AreEqual("SS", V6GradeLogic.GradeOf(1000000, Scoring.Version3));
            Assert.AreEqual("B", V6GradeLogic.GradeOf(700000, Scoring.Version1));
        }

        // --- 実際の scoring v4 スコアが帯に収まること ---

        /// <summary>判定シーケンス 1 プレイ分のスコア (Issue #788 の分布検証用)。</summary>
        private struct PlayResult
        {
            public int Score;
            public int Perfect;
            public int Great;
            public int Bad;
            public int Miss;
        }

        /// <summary>
        /// 判定列 ('P' / 'G' / 'B' / 'M') から v4 スコアを計算する。
        /// コンボは Miss で 0 に戻り (Bad はコンボを維持する)、
        /// 節目はコンボが 50 の倍数に達するたびに 1 増える
        /// (JudgeScoreboard.ApplyHead / ApplyMiss と同じ規則。切れても
        /// 回数は保持され、以降のコンボで再び加点される)。
        /// 全ノーツ判定済みなので judgmentsComplete = true。
        /// </summary>
        private static PlayResult PlayOf(string judgments)
        {
            int p = 0, g = 0, b = 0, m = 0;
            int combo = 0, maxCombo = 0, milestones = 0;
            for (int i = 0; i < judgments.Length; i++)
            {
                char j = judgments[i];
                if (j == 'M')
                {
                    m++;
                    combo = 0;
                    continue;
                }

                if (j == 'P') p++;
                else if (j == 'G') g++;
                else b++;

                combo++;
                if (combo > maxCombo) maxCombo = combo;
                if (combo % Scoring.MilestoneInterval == 0) milestones++;
            }

            int noteCount = judgments.Length;
            int score = Scoring.ScoreV4(p, g, b, m, maxCombo, milestones, noteCount, true);
            return new PlayResult { Score = score, Perfect = p, Great = g, Bad = b, Miss = m };
        }

        /// <summary>
        /// ヒット部分に判定ミックス (pFrac / gFrac / 残り Bad) を均等に
        /// 散らし、missIndices の位置を 'M' にした判定列を作る
        /// (成績分布シミュレーションと同じ決定的な配置)。
        /// </summary>
        private static string BuildJudgments(int noteCount, double pFrac, double gFrac, params int[] missIndices)
        {
            var miss = new HashSet<int>(missIndices);
            int hits = noteCount - miss.Count;
            int pCount = (int)System.Math.Round(hits * pFrac);
            int gCount = (int)System.Math.Round(hits * gFrac);
            var chars = new char[noteCount];
            int hitSeen = 0;
            for (int i = 0; i < noteCount; i++)
            {
                if (miss.Contains(i))
                {
                    chars[i] = 'M';
                    continue;
                }

                double t = (double)hitSeen / hits;
                hitSeen++;
                chars[i] = t < (double)pCount / hits ? 'P'
                    : t < (double)(pCount + gCount) / hits ? 'G' : 'B';
            }

            return new string(chars);
        }

        /// <summary>等間隔に <paramref name="count"/> 個の miss を置く。</summary>
        private static int[] SpreadMisses(int noteCount, int count)
        {
            var idx = new List<int>();
            for (int i = 0; i < count; i++)
            {
                int pos = System.Math.Min(noteCount - 1, (int)System.Math.Floor(((i + 0.5) / count) * noteCount));
                if (!idx.Contains(pos)) idx.Add(pos);
            }

            return idx.ToArray();
        }

        private static void AssertGrade(string judgments, string expected, string label)
        {
            PlayResult play = PlayOf(judgments);
            Assert.AreEqual(expected, V6GradeLogic.GradeOfV4(play.Score),
                label + " (score=" + play.Score + ")");
        }

        [Test]
        public void GradeOfV4_PerfectFullCombo_IsSS()
        {
            // 全 Perfect + フルコンボ終了: 基礎 1,000,000 + ボーナス 1,000,000。
            AssertGrade(BuildJudgments(100, 1.0, 0.0), "SS", "all-Perfect FC");
        }

        [Test]
        public void GradeOfV4_NoMissPlay_LandsOnJudgmentQualityBands()
        {
            // ノーミスのクリアはボーナスが満点 1,000,000 に張り付くため、
            // グレードは実質「基礎スコア (判定の質)」で決まる。
            // 90P/10G FC: 基礎 960,000 → 1,960,000 = S。
            AssertGrade(BuildJudgments(500, 0.9, 0.1), "S", "90P10G FC");
            // 70P/30G FC: 基礎 880,000 → 1,880,000 = A。
            AssertGrade(BuildJudgments(500, 0.7, 0.3), "A", "70P30G FC");
            // 全 Great FC: 基礎 600,000 → 1,600,000 = A。
            AssertGrade(BuildJudgments(500, 0.0, 1.0), "A", "all-Great FC");
            // 50P/30G/20B FC: 基礎 720,000 → 1,720,000 = A。
            AssertGrade(BuildJudgments(500, 0.5, 0.3), "A", "50P30G20B FC");
            // 全 Bad FC: 基礎 200,000 → 1,200,000 = B (実際にはほぼ起きない
            // 極端な内訳だが、ノーミス完走の下限として B に留まる)。
            AssertGrade(BuildJudgments(500, 0.0, 0.0), "B", "all-Bad FC");
        }

        [Test]
        public void GradeOfV4_SingleLateMiss_WithGoodJudgments_IsA()
        {
            // Issue #788 の中核ケース: 判定質の良いプレイ (85P/15G) が
            // 終盤で 1 回だけ miss しても A に届く (ノーツ数 139〜1000 で
            // 1,410,000〜1,460,000)。旧帯 (A ≥ 1,800,000) では C だった。
            foreach (int n in new[] { 139, 300, 500, 1000 })
            {
                AssertGrade(BuildJudgments(n, 0.85, 0.15, n - 1), "A", "1 late miss, n=" + n);
            }
        }

        [Test]
        public void GradeOfV4_FewSpreadMisses_WithGoodJudgments_IsB()
        {
            // 数回の miss を含む通常プレイは B。序盤〜中盤の miss は
            // コンボ継続を大きく失うため終盤 miss より低いが、B には届く。
            foreach (int n in new[] { 139, 300, 500, 1000 })
            {
                AssertGrade(BuildJudgments(n, 0.85, 0.15, n / 2), "B", "1 mid miss, n=" + n);
                AssertGrade(BuildJudgments(n, 0.85, 0.15, SpreadMisses(n, 3)), "B", "3 spread misses, n=" + n);
            }
        }

        [Test]
        public void GradeOfV4_MissRateAroundTwoPercent_OrWorse_IsC()
        {
            // ~2% 以上の miss 率、または判定質の低いプレイの複数 miss は
            // C に分ける (B ≥ 1,000,000 は「概ね追えている」プレイの下限)。
            // 小さい譜面は miss 1 回あたりの重みが大きいので、同じ割合でも
            // 絶対数の少ない側は B に残りうる (139 ノーツ 3 miss = B)。
            AssertGrade(BuildJudgments(139, 0.85, 0.15, SpreadMisses(139, 5)), "C",
                "139 notes, 5 misses (~3.6%)");
            AssertGrade(BuildJudgments(300, 0.85, 0.15, SpreadMisses(300, 6)), "C",
                "300 notes, 6 misses (~2%)");
            AssertGrade(BuildJudgments(500, 0.85, 0.15, SpreadMisses(500, 10)), "C",
                "500 notes, 10 misses (2%)");
            AssertGrade(BuildJudgments(1000, 0.85, 0.15, SpreadMisses(1000, 20)), "C",
                "1000 notes, 20 misses (2%)");
            foreach (int n in new[] { 139, 300, 500, 1000 })
            {
                AssertGrade(BuildJudgments(n, 0.5, 0.3, SpreadMisses(n, 10)), "C",
                    "casual mix + 10 misses, n=" + n);
            }
        }

        [Test]
        public void GradeOfV4_MissSplittingCombo_LowersScoreAndGrade()
        {
            // v4 のスコアは miss の「位置」ではなくコンボの分断のされ方で
            // 決まる: 終盤の 1 miss はそれまでのコンボ継続をほぼ保持するが、
            // 中盤の 1 miss はコンボを 2 つに割り、継続・節目ボーナスを失う。
            string late = BuildJudgments(500, 0.85, 0.15, 499);
            string mid = BuildJudgments(500, 0.85, 0.15, 250);
            PlayResult latePlay = PlayOf(late);
            PlayResult midPlay = PlayOf(mid);
            Assert.Greater(latePlay.Score, midPlay.Score);
            Assert.AreEqual("A", V6GradeLogic.GradeOfV4(latePlay.Score));
            Assert.AreEqual("B", V6GradeLogic.GradeOfV4(midPlay.Score));
        }

        [Test]
        public void GradeOfV4_NewBands_NeverDowngradeVersusIssue758Bands()
        {
            // Issue #788 の帯は旧帯 (S 95% / A 90% / B 80%) を緩める方向にのみ
            // 動く: 全スコアで新グレードが旧グレードを下回らないことを
            // 保存済み v4 記録の再解釈互換として検証する。
            int[] oldBands = { 1900000, 1800000, 1600000 }; // S, A, B
            for (int score = 0; score <= 2000000; score += 997)
            {
                string oldGrade = OldBandsGrade(score);
                string newGrade = V6GradeLogic.GradeOfV4(score);
                Assert.GreaterOrEqual(GradeRank(newGrade), GradeRank(oldGrade),
                    "score=" + score + " old=" + oldGrade + " new=" + newGrade);
            }

            string OldBandsGrade(int s)
            {
                if (s >= 2000000) return "SS";
                if (s >= oldBands[0]) return "S";
                if (s >= oldBands[1]) return "A";
                if (s >= oldBands[2]) return "B";
                return "C";
            }

            int GradeRank(string g)
            {
                switch (g)
                {
                    case "SS": return 5;
                    case "S": return 4;
                    case "A": return 3;
                    case "B": return 2;
                    case "C": return 1;
                    default: return 0;
                }
            }
        }
    }
}
