using NUnit.Framework;

/// <summary>
/// Contract of the frame time budget scheduler used by NotesManager (issue #113):
/// a batch never exceeds the remaining notes, the measured speed adapts the next
/// batch size, and invalid measurements are ignored.
/// </summary>
public class TimeBudgetBatcherTests
{
    [Test]
    public void NextBatchSizeReturnsAtLeastOneForRemainingWork()
    {
        var batcher = new TimeBudgetBatcher(8.0, seedItemsPerSecond: 0.001);
        Assert.AreEqual(1, batcher.NextBatchSize(100));
    }

    [Test]
    public void NextBatchSizeNeverExceedsRemaining()
    {
        var batcher = new TimeBudgetBatcher(8.0, seedItemsPerSecond: 100000.0);
        Assert.AreEqual(7, batcher.NextBatchSize(7));
    }

    [Test]
    public void NextBatchSizeIsZeroWithoutRemainingWork()
    {
        var batcher = new TimeBudgetBatcher(8.0);
        Assert.AreEqual(0, batcher.NextBatchSize(0));
        Assert.AreEqual(0, batcher.NextBatchSize(-3));
    }

    [Test]
    public void BatchSizeFitsTheTimeBudget()
    {
        // 1000 items/s * 8ms budget = 8 items per batch (seed estimate).
        var batcher = new TimeBudgetBatcher(8.0, seedItemsPerSecond: 1000.0);
        Assert.AreEqual(8, batcher.NextBatchSize(100));
    }

    [Test]
    public void ReportAdaptsTheBatchSizeToTheMeasuredSpeed()
    {
        var batcher = new TimeBudgetBatcher(8.0, seedItemsPerSecond: 1000.0);

        // 100 items in 0.5s = 200 items/s -> 200 * 0.008 = 1.6 -> 2 items.
        batcher.Report(100, 0.5);
        Assert.AreEqual(2, batcher.NextBatchSize(100));

        // 500 items in 0.1s = 5000 items/s -> 5000 * 0.008 = 40 items.
        batcher.Report(500, 0.1);
        Assert.AreEqual(40, batcher.NextBatchSize(100));
    }

    [Test]
    public void InvalidMeasurementsAreIgnored()
    {
        var batcher = new TimeBudgetBatcher(8.0, seedItemsPerSecond: 1000.0);
        batcher.Report(0, 0.5);
        batcher.Report(10, 0.0);
        batcher.Report(-5, 0.5);
        Assert.AreEqual(1000.0, batcher.EstimatedItemsPerSecond);
    }
}
