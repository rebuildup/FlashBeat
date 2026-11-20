using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using FlashBeat.UI.V6.Result;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// Result 画面のグレード・数値テキストの収まり検証 (Issue #737)。
    ///
    /// design zcSHh の契約: Grade 箱は Result Block 内 (0,0) の 280×280、
    /// グレードは SS/S/A/B/C/– のいずれも箱からはみ出さず 1 行で描かれる
    /// (fs240 は単一グリフの design 値で、"SS" は実測フィットで縮む)。
    /// SCORE / 前回最高 / 判定 5 列 / ボーナス / Stat の数値は、桁が増えても
    /// (100 万点超・大きな判定数) 矩形内に収まる (TMP auto-size)。
    /// 期待値は design の矩形契約 (V6ResultLogic.GradeBox* / 各配置定数) から
    /// 導き、実装のフォントサイズ定数はコピーしない。
    /// </summary>
    public sealed class V6ResultGradeFitTests
    {
        private GameObject canvasGo;

        private int savedScore, savedPerfect, savedGreat, savedBad, savedMiss, savedMaxCombo, savedMilestones;
        private bool savedJudgmentsComplete;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6ResultGradeFitTests Canvas", typeof(Canvas));
            savedScore = GManager.score;
            savedPerfect = GManager.perfect;
            savedGreat = GManager.great;
            savedBad = GManager.bad;
            savedMiss = GManager.miss;
            savedMaxCombo = GManager.maxCombo;
            savedMilestones = GManager.comboMilestones;
            savedJudgmentsComplete = GManager.judgmentsComplete;
        }

        [TearDown]
        public void TearDown()
        {
            GManager.score = savedScore;
            GManager.perfect = savedPerfect;
            GManager.great = savedGreat;
            GManager.bad = savedBad;
            GManager.miss = savedMiss;
            GManager.maxCombo = savedMaxCombo;
            GManager.comboMilestones = savedMilestones;
            GManager.judgmentsComplete = savedJudgmentsComplete;

            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }

            canvasGo = null;
        }

        // --- ヘルパ ---

        private V6ResultView CreateView(int score, int perfect = 0, int great = 0, int bad = 0,
            int miss = 0, int maxCombo = 0)
        {
            GManager.score = score;
            GManager.perfect = perfect;
            GManager.great = great;
            GManager.bad = bad;
            GManager.miss = miss;
            GManager.maxCombo = maxCombo;
            return V6ResultView.Create((RectTransform)canvasGo.transform, null, null, null);
        }

        private static TextMeshProUGUI FindText(V6ResultView view, string path)
        {
            Transform t = view.Root.Find(path);
            Assert.IsNotNull(t, "node not found: " + path);
            var tmp = t.GetComponent<TextMeshProUGUI>();
            Assert.IsNotNull(tmp, path + " must be a TMP text");
            return tmp;
        }

        /// <summary>
        /// TMP textInfo のメッシュ頂点からインクの外接矩形 (幅・高さ) を返す。
        /// 実装側 (V6ResultStats.InkScale) と同じ頂点走査だが、こちらは
        /// 「描かれるインクが矩形に収まるか」という契約だけを見る。
        /// </summary>
        private static void InkSize(TextMeshProUGUI tmp, out float width, out float height)
        {
            tmp.ForceMeshUpdate(true, true);
            var info = tmp.textInfo;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            if (info != null && info.meshInfo != null)
            {
                int quadLimit = info.characterCount * 4;
                for (int m = 0; m < info.meshInfo.Length; m++)
                {
                    Vector3[] verts = info.meshInfo[m].vertices;
                    if (verts == null)
                    {
                        continue;
                    }

                    int limit = Mathf.Min(verts.Length, quadLimit);
                    for (int i = 0; i + 3 < limit; i += 4)
                    {
                        for (int k = 0; k < 4; k++)
                        {
                            Vector3 v = verts[i + k];
                            if (v.x < minX) { minX = v.x; }
                            if (v.x > maxX) { maxX = v.x; }
                            if (v.y < minY) { minY = v.y; }
                            if (v.y > maxY) { maxY = v.y; }
                        }
                    }
                }
            }

            width = maxX - minX;
            height = maxY - minY;
        }

        private static void AssertSingleLineFittingBox(TextMeshProUGUI tmp, float boxW, float boxH, string label)
        {
            Assert.AreEqual(TextWrappingModes.NoWrap, tmp.textWrappingMode, label + " must not wrap");
            tmp.ForceMeshUpdate(true, true);
            Assert.Greater(tmp.textInfo.characterCount, 0, label + " must render glyphs");
            Assert.LessOrEqual(tmp.textInfo.lineCount, 1, label + " must stay on a single line");

            InkSize(tmp, out float inkW, out float inkH);
            Assert.Greater(inkW, 0f, label + " ink width must be measurable");
            Assert.Greater(inkH, 0f, label + " ink height must be measurable");
            Assert.LessOrEqual(inkW, boxW, label + " ink width must fit the box");
            Assert.LessOrEqual(inkH, boxH, label + " ink height must fit the box");
        }

        // --- グレード表示 (Issue #737 の本体) ---

        /// <summary>
        /// GradeOf が返し得る全グレード ("–","C","B","A","S","SS") で、
        /// グレード文字が 280×280 の Grade 箱からはみ出さず 1 行で描かれること。
        /// スコアは scoring v4 (満点 2,000,000) の帯 (ADR-0034 / Issue #758)。
        /// </summary>
        [Test]
        public void GradeLetter_AllGrades_FitInsideGradeBox()
        {
            // (score, 期待グレード)。閾値は V6GradeLogic.GradeOfV4 と同一
            // (GradeOf の帯は V6ResultLogicTests / V6GradeLogicTests が固定済み)。
            var cases = new[]
            {
                new KeyValuePair<int, string>(0, "–"),
                new KeyValuePair<int, string>(100, "C"),
                new KeyValuePair<int, string>(1599999, "C"),
                new KeyValuePair<int, string>(1600000, "B"),
                new KeyValuePair<int, string>(1800000, "A"),
                new KeyValuePair<int, string>(1900000, "S"),
                new KeyValuePair<int, string>(1999999, "S"),
                new KeyValuePair<int, string>(2000000, "SS"),
            };

            foreach (var c in cases)
            {
                var view = CreateView(c.Key);
                try
                {
                    var letter = FindText(view, "Main/Result Block/Grade/Letter");
                    Assert.AreEqual(c.Value, letter.text, "grade text @ score " + c.Key);
                    AssertSingleLineFittingBox(letter,
                        V6ResultLogic.GradeBoxW, V6ResultLogic.GradeBoxH,
                        "grade '" + c.Value + "' @ score " + c.Key);
                    Assert.GreaterOrEqual(letter.fontSize, V6ResultLogic.GradeMinFontSize,
                        "grade font size floor @ " + c.Value);
                    Assert.LessOrEqual(letter.fontSize, V6ResultLogic.GradeMaxFontSize,
                        "grade font size ceiling @ " + c.Value);
                }
                finally
                {
                    Object.DestroyImmediate(view.Root.gameObject);
                }
            }
        }

        /// <summary>
        /// "SS" (2 グリフ) は fs240 では Grade 箱の幅を超えるため、実測フィットで
        /// 最大 fs より小さいサイズへ縮むこと。単一グリフは design どおり fs240。
        /// </summary>
        [Test]
        public void GradeLetter_SS_ShrinksToFit_WhileSingleGlyphKeepsMaxSize()
        {
            var ssView = CreateView(2000000);
            try
            {
                var ss = FindText(ssView, "Main/Result Block/Grade/Letter");
                Assert.AreEqual("SS", ss.text);
                Assert.Less(ss.fontSize, V6ResultLogic.GradeMaxFontSize,
                    "SS must shrink below the single-glyph design size to fit the box");
                Assert.GreaterOrEqual(ss.fontSize, V6ResultLogic.GradeMinFontSize);
            }
            finally
            {
                Object.DestroyImmediate(ssView.Root.gameObject);
            }

            var sView = CreateView(1900000);
            try
            {
                var s = FindText(sView, "Main/Result Block/Grade/Letter");
                Assert.AreEqual("S", s.text);
                Assert.AreEqual(V6ResultLogic.GradeMaxFontSize, s.fontSize, 0.5f,
                    "single glyph keeps the design font size");
            }
            finally
            {
                Object.DestroyImmediate(sView.Root.gameObject);
            }
        }

        /// <summary>
        /// グレード文字のインクが Grade 箱の右端 (280) を越えず、隣の SCORE 列
        /// (x336) へ届かないこと。箱の中央揃えなので、インク半幅が箱の半幅を
        /// 超えなければ SCORE 列とは水平方向で重ならない。
        /// </summary>
        [Test]
        public void GradeLetter_SS_DoesNotReachScoreColumn()
        {
            var view = CreateView(1000000);
            try
            {
                var letter = FindText(view, "Main/Result Block/Grade/Letter");
                InkSize(letter, out float inkW, out _);
                // Grade 箱はブロック内 x0..280、SCORE 列は x336 開始。
                // 中央揃えなので右端 = 140 + inkW/2。336 に届かないことを見る。
                Assert.LessOrEqual(140f + inkW * 0.5f, 336f,
                    "SS ink must not reach the SCORE column (x336)");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        // --- 数値テキストの大きな値への耐性 ---

        /// <summary>
        /// SCORE / 前回最高 / 判定 5 列 / ボーナス 4 列は auto-size が有効で、
        /// int 最大級の値でも矩形 (インク) からはみ出さないこと。
        /// </summary>
        [Test]
        public void NumericTexts_ExtremeValues_StayInsideTheirRects()
        {
            // 判定数は譜面規模まで現実的な大きな値 (コンボは 6 桁) に留める。
            // int 最大級を入れると auto-size の下限 (可読性フロア) を割り込み、
            // 「収まる」契約ではなく「読める」契約の検証になってしまう。
            var view = CreateView(int.MaxValue,
                perfect: 999999, great: 999999, bad: 999999, miss: 999999,
                maxCombo: 999999);
            try
            {
                AssertAutoFitNumeric("Main/Result Block/Score Value", view, 500f, 80f);
                AssertAutoFitNumeric("Main/Result Block/Best Value", view, 400f, 40f);

                string[] judgeNames = { "Perfect", "Great", "Bad", "Miss", "Combo" };
                for (int i = 0; i < judgeNames.Length; i++)
                {
                    AssertAutoFitNumeric("Main/Judgements/J " + judgeNames[i] + "/Value",
                        view, 160f, 70f);
                }

                // Issue #736 (scoring v4): ボーナス内訳の表示名は
                // BASE / COMBO / MILESTONE / CLEAR の 4 列
                // (V6ResultStats.BuildBonusLine の keys と同一)。
                string[] bonusKeys =
                {
                    "Base Score", "Combo Bonus", "Milestone Bonus", "Clear Bonus",
                };
                for (int i = 0; i < bonusKeys.Length; i++)
                {
                    // ボーナス列幅は (840-110)/4 = 182.5。
                    AssertAutoFitNumeric("Main/Bonus Line/" + bonusKeys[i] + " Value",
                        view, 182.5f, 22f);
                }
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }

        /// <summary>
        /// auto-size が有効 (fontSizeMin &lt; fontSizeMax = design fs) で、
        /// ForceMeshUpdate 後のインクが矩形内に収まることを確認する。
        /// </summary>
        private static void AssertAutoFitNumeric(string path, V6ResultView view, float rectW, float rectH)
        {
            var tmp = FindText(view, path);
            Assert.IsTrue(tmp.enableAutoSizing, path + " must use TMP auto-size");
            Assert.Greater(tmp.fontSizeMax, tmp.fontSizeMin, path + " fontSizeMax above min");
            Assert.GreaterOrEqual(tmp.fontSize, tmp.fontSizeMin - 0.01f,
                path + " font size stays at/above the readability floor");
            Assert.LessOrEqual(tmp.fontSize, tmp.fontSizeMax + 0.01f,
                path + " design font size is the ceiling");

            tmp.ForceMeshUpdate(true, true);
            Assert.Greater(tmp.textInfo.characterCount, 0, path + " must render glyphs");
            InkSize(tmp, out float inkW, out float inkH);
            Assert.LessOrEqual(inkW, rectW + 0.5f, path + " ink width must fit its column");
            Assert.LessOrEqual(inkH, rectH + 0.5f, path + " ink height must fit its column");
        }

        /// <summary>
        /// 登録モーダルの Score Value / Grade / Record Value も auto-size で、
        /// 100 万点超の桁でも Summary 内の矩形からはみ出さないこと。
        /// </summary>
        [Test]
        public void RegisterModal_NumericTexts_StayInsideTheirRects()
        {
            var view = CreateView(1000000);
            try
            {
                // モーダルは非表示だが構造は構築済み。RefreshModal で実値が入る。
                var score = FindText(view, "Modal 05b/Dialog/Summary/Score Group/Score Value");
                Assert.IsTrue(score.enableAutoSizing, "modal score must use TMP auto-size");
                score.ForceMeshUpdate(true, true);
                InkSize(score, out float scoreInkW, out _);
                Assert.LessOrEqual(scoreInkW, 132f + 0.5f, "modal score ink must fit 132px column");

                var grade = FindText(view, "Modal 05b/Dialog/Summary/Grade/GradeText");
                Assert.IsTrue(grade.enableAutoSizing, "modal grade must use TMP auto-size");
                grade.ForceMeshUpdate(true, true);
                InkSize(grade, out float gradeInkW, out float gradeInkH);
                Assert.LessOrEqual(gradeInkW, 64f + 0.5f, "modal grade ink must fit the chip");
                Assert.LessOrEqual(gradeInkH, 40f + 0.5f, "modal grade ink must fit the chip");
            }
            finally
            {
                Object.DestroyImmediate(view.Root.gameObject);
            }
        }
    }
}
