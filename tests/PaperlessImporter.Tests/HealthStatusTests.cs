namespace PaperlessImporter.Tests;

public sealed class HealthStatusTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"paperless-importer-tests-{Guid.NewGuid():N}");

    [Fact]
    public void SeparatesLivenessFromReadiness()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "health.txt");
        var now = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        var status = new HealthStatus(path);
        status.Update(now, false, false, 1, "paperless-unavailable");
        var options = new ImporterOptions { HealthPath = path };

        Assert.True(HealthStatus.Check(path, false, options, now.AddSeconds(1)));
        Assert.False(HealthStatus.Check(path, true, options, now.AddSeconds(1)));
    }

    [Fact]
    public void ReadinessRequiresRecentProgressWhileBacklogged()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "health.txt");
        var now = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        var status = new HealthStatus(path);
        status.Update(now, true, true, 1, "ok");
        var options = new ImporterOptions { HealthPath = path, BacklogProgressStaleSeconds = 10 };

        Assert.True(HealthStatus.Check(path, true, options, now.AddSeconds(5)));
        Assert.False(HealthStatus.Check(path, true, options, now.AddSeconds(20)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
