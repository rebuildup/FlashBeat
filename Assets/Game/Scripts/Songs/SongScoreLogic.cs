using System.Globalization;
using UnityEngine;

/// <summary>
/// 曲スコア / 曲長の純ロジック (Issue #740: 旧 V5 SelectViewLogic から
/// 製品で生きているメンバーだけを Songs 層へ移設)。
///
/// かつては v5 Select 画面のフィルタ / ソート / スクロール窓計算も持っていたが、
/// v6 選曲画面は <c>V6SelectDataLogic</c> / <c>V6SelectViewLogic</c> が同じ責務を
/// 持つため、v5 View の削除に合わせて撤去した。
///
/// UnityEngine への依存は最小 (Mathf のみ) で、EditMode テストから直接検証できる。
/// </summary>
public static class SongScoreLogic
{
    /// <summary>スコア満点 (デザインのスコアレール / グレード判定の基準)。</summary>
    public const int ScoreMax = 1000000;

    /// <summary>
    /// スコアからグレード文字列を返す (記録なしは null)。
    /// デザイン 02/07 の実測値 (987,410=S / 921,880=A / 842,300=B / 742,000=C / 1,000,000=SS) に整合。
    /// </summary>
    public static string GradeOf(int score)
    {
        if (score <= 0) return null;
        if (score >= ScoreMax) return "SS";
        if (score >= 950000) return "S";
        if (score >= 900000) return "A";
        if (score >= 800000) return "B";
        return "C";
    }

    /// <summary>曲長表示 (秒 → "m:ss")。</summary>
    public static string DurationText(float seconds)
    {
        if (seconds <= 0f) return null;
        int total = Mathf.RoundToInt(seconds);
        return (total / 60) + ":" + (total % 60).ToString("00", CultureInfo.InvariantCulture);
    }
}
