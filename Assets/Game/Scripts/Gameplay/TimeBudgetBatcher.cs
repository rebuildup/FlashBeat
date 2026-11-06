using System;

/// <summary>
/// 1 フレームあたりの時間予算 (ミリ秒) に収まるようにバッチサイズを調整する
/// 純粋なスケジューラ。Unity API に依存しないので NUnit テストから直接検証できる。
///
/// 使い方 (NotesManager のノーツ生成):
///   1. NextBatchSize(remaining) で今回処理する件数を得る
///   2. Stopwatch で実測しながらその件数を処理する
///   3. Report(count, elapsedSeconds) で実測を反映する
/// 実測が予算を超えればバッチは小さく、余れば大きくなる (適応制御)。
/// </summary>
public class TimeBudgetBatcher
{
    readonly double budgetSeconds;
    double itemsPerSecond;

    /// <param name="budgetMilliseconds">1 バッチの目標時間上限 (ミリ秒、例: 8)。</param>
    /// <param name="seedItemsPerSecond">初期の処理速度の見積り (実測が入るまでの種)。</param>
    public TimeBudgetBatcher(double budgetMilliseconds, double seedItemsPerSecond = 1000.0)
    {
        if (budgetMilliseconds <= 0.0) throw new ArgumentOutOfRangeException(nameof(budgetMilliseconds));
        budgetSeconds = budgetMilliseconds / 1000.0;
        itemsPerSecond = seedItemsPerSecond > 0.0 ? seedItemsPerSecond : 1000.0;
    }

    /// <summary>次のバッチの件数 (1..remaining、remaining &lt;= 0 なら 0)。</summary>
    public int NextBatchSize(int remaining)
    {
        if (remaining <= 0) return 0;
        double size = Math.Ceiling(itemsPerSecond * budgetSeconds);
        int batchSize = size >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)size);
        return batchSize > remaining ? remaining : batchSize;
    }

    /// <summary>直前バッチの実測から処理速度を見直す。無効な実測は無視する。</summary>
    public void Report(int processedCount, double elapsedSeconds)
    {
        if (processedCount <= 0 || elapsedSeconds <= 0.0) return;
        itemsPerSecond = processedCount / elapsedSeconds;
    }

    /// <summary>現在の見積り速度 (件/秒)。テストと診断用。</summary>
    public double EstimatedItemsPerSecond => itemsPerSecond;
}
