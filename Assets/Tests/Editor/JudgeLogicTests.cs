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
    public void ComputeScore_IsNeverNegative()
    {
        // 負の ratioScore (異常値) でも負のスコアを画面に出さない。
        Assert.GreaterOrEqual(Judge.ComputeScore(-10f, 10f), 0);
    }
}
