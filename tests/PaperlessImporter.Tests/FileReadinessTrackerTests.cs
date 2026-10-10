namespace PaperlessImporter.Tests;

public sealed class FileReadinessTrackerTests
{
    [Fact]
    public void RequiresStableObservationsAndMinimumAge()
    {
        var tracker = new FileReadinessTracker();
        var now = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        var snapshot = new FileSnapshot("scan.pdf", "scan.pdf", ".pdf", 123, now.AddMinutes(-1).UtcTicks);

        Assert.Empty(tracker.Observe([snapshot], now, 2, TimeSpan.FromSeconds(30)));
        Assert.Single(tracker.Observe([snapshot], now.AddSeconds(10), 2, TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void GrowthRestartsStabilityCount()
    {
        var tracker = new FileReadinessTracker();
        var now = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        var first = new FileSnapshot("scan.pdf", "scan.pdf", ".pdf", 123, now.AddMinutes(-1).UtcTicks);
        var grown = first with { SizeBytes = 456, ModifiedUtcTicks = now.AddSeconds(-40).UtcTicks };

        tracker.Observe([first], now, 2, TimeSpan.FromSeconds(30));
        Assert.Empty(tracker.Observe([grown], now.AddSeconds(10), 2, TimeSpan.FromSeconds(30)));
        Assert.Single(tracker.Observe([grown], now.AddSeconds(20), 2, TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void DisappearanceClearsPriorObservation()
    {
        var tracker = new FileReadinessTracker();
        var now = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        var snapshot = new FileSnapshot("scan.pdf", "scan.pdf", ".pdf", 123, now.AddMinutes(-1).UtcTicks);

        tracker.Observe([snapshot], now, 2, TimeSpan.Zero);
        tracker.Observe([], now, 2, TimeSpan.Zero);

        Assert.Empty(tracker.Observe([snapshot], now, 2, TimeSpan.Zero));
    }
}
