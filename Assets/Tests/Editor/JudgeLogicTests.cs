using NUnit.Framework;

/// <summary>
/// 判定の純ロジック (スコア計算) の EditMode テスト。
///
/// Judge は MonoBehaviour で UpdateScore が private なので、
/// 採点計算を <see cref="Judge.ComputeScore"/> として切り出して検証する。
/// </summary>
public class JudgeLogicTests
{
    [Test]
    public void ComputeScore_ZeroMaxScore_ReturnsZeroInsteadOfNaN()
    {
        // 譜面が無いセッション: maxScore が 0 のまま。0 で割ると NaN になり、
        // (int) キャストすると不定の値 (int.MinValue) になっていた。
        Assert.AreEqual(0, Judge.ComputeScore(0f, 0f));
        Assert.AreEqual(0, Judge.ComputeScore(120f, 0f));
        Assert.AreEqual(0, Judge.ComputeScore(0f, -1f));
    }

    [Test]
    public void ComputeScore_FullRatio_IsOneMillion()
    {
        Assert.AreEqual(1000000, Judge.ComputeScore(500f, 500f));
    }

    [Test]
    public void ComputeScore_HalfRatio_IsHalfOfOneMillion()
    {
        Assert.AreEqual(500000, Judge.ComputeScore(5f, 10f));
    }

    [Test]
    public void ComputeScore_StaysWithinZeroToOneMillion()
    {
        // 正常系のスコアは 0〜1,000,000 の範囲に収まる。
        foreach (float ratio in new float[] { 0f, 1f, 17f, 250f, 499f, 500f })
        {
            int score = Judge.ComputeScore(ratio, 500f);
            Assert.GreaterOrEqual(score, 0, $"ratio={ratio}");
            Assert.LessOrEqual(score, 1000000, $"ratio={ratio}");
        }
    }
}
