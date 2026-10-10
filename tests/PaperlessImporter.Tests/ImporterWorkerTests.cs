using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace PaperlessImporter.Tests;

public sealed class ImporterWorkerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"paperless-importer-worker-{Guid.NewGuid():N}");

    [Fact]
    public async Task MatchingSuccessfulTaskPreventsUploadAndArchivesClaim()
    {
        var options = CreateOptions();
        var location = options.Locations[0];
        var claimDirectory = Path.Combine(location.ProcessingPath, "claim");
        Directory.CreateDirectory(claimDirectory);
        var claimedPath = Path.Combine(claimDirectory, "synthetic.pdf");
        await File.WriteAllTextAsync(claimedPath, "synthetic fixture", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(claimedPath, DateTime.UtcNow.AddMinutes(-1));
        var client = new FakePaperlessClient([
            new PaperlessTaskResult("task-1", "synthetic.pdf", DateTimeOffset.UtcNow, PaperlessTaskState.Success, null)
        ]);
        var worker = CreateWorker(options, client);

        await worker.ScanOnceAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(0, client.UploadCount);
        Assert.True(File.Exists(Path.Combine(location.OutputPath, "synthetic.pdf")));
        Assert.False(Directory.Exists(claimDirectory));
    }

    [Fact]
    public async Task MissingTaskUploadsOnceAndLeavesClaimForReconciliation()
    {
        var options = CreateOptions();
        var location = options.Locations[0];
        var claimDirectory = Path.Combine(location.ProcessingPath, "claim");
        Directory.CreateDirectory(claimDirectory);
        var claimedPath = Path.Combine(claimDirectory, "synthetic.pdf");
        await File.WriteAllTextAsync(claimedPath, "synthetic fixture", TestContext.Current.CancellationToken);
        var client = new FakePaperlessClient([]);
        var worker = CreateWorker(options, client);

        await worker.ScanOnceAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(1, client.UploadCount);
        Assert.True(File.Exists(claimedPath));
    }

    [Fact]
    public async Task UnavailableLocationDoesNotBlockAnotherLocation()
    {
        var available = CreateLocation("available");
        var unavailableRoot = Path.Combine(_root, "unavailable");
        var unavailable = new ImportLocationOptions
        {
            InputPath = Path.Combine(_root, "missing-input"),
            ProcessingPath = Path.Combine(unavailableRoot, "processing"),
            OutputPath = Path.Combine(unavailableRoot, "output"),
            QuarantinePath = Path.Combine(unavailableRoot, "quarantine")
        };
        var options = new ImporterOptions
        {
            Locations = [unavailable, available],
            HealthPath = Path.Combine(_root, "health", "status.txt"),
            MinimumAgeSeconds = 1
        };
        var claimDirectory = Path.Combine(available.ProcessingPath, "claim");
        Directory.CreateDirectory(claimDirectory);
        var claimedPath = Path.Combine(claimDirectory, "synthetic.pdf");
        await File.WriteAllTextAsync(claimedPath, "synthetic fixture", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(claimedPath, DateTime.UtcNow.AddMinutes(-1));
        var client = new FakePaperlessClient([
            new PaperlessTaskResult("task-1", "synthetic.pdf", DateTimeOffset.UtcNow, PaperlessTaskState.Success, null)
        ]);

        var result = await CreateWorker(options, client)
            .ScanOnceAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.False(result.AllLocationsSucceeded);
        Assert.True(File.Exists(Path.Combine(available.OutputPath, "synthetic.pdf")));
    }

    [Fact]
    public async Task OpenFileIsLeftForTheNextPoll()
    {
        if (!OperatingSystem.IsWindows()) return;

        var options = CreateOptions();
        var location = options.Locations[0];
        var inputPath = Path.Combine(location.InputPath, "synthetic.pdf");
        await File.WriteAllTextAsync(inputPath, "synthetic fixture", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(inputPath, DateTime.UtcNow.AddMinutes(-1));
        var client = new FakePaperlessClient([]);
        var worker = CreateWorker(options, client);
        var now = DateTimeOffset.UtcNow;
        await worker.ScanOnceAsync(now, TestContext.Current.CancellationToken);

        await using (var locked = new FileStream(inputPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await worker.ScanOnceAsync(now.AddSeconds(10), TestContext.Current.CancellationToken);
            Assert.True(File.Exists(inputPath));
            Assert.Equal(0, client.UploadCount);
        }

        await worker.ScanOnceAsync(now.AddSeconds(20), TestContext.Current.CancellationToken);
        Assert.False(File.Exists(inputPath));
        Assert.Equal(1, client.UploadCount);
    }

    private ImporterOptions CreateOptions()
    {
        var location = CreateLocation("default");
        return new ImporterOptions
        {
            Locations = [location],
            HealthPath = Path.Combine(_root, "health", "status.txt"),
            MinimumAgeSeconds = 1
        };
    }

    private ImportLocationOptions CreateLocation(string name)
    {
        var location = new ImportLocationOptions
        {
            InputPath = Path.Combine(_root, name, "inbox"),
            ProcessingPath = Path.Combine(_root, name, "processing"),
            OutputPath = Path.Combine(_root, name, "output"),
            QuarantinePath = Path.Combine(_root, name, "quarantine")
        };
        Directory.CreateDirectory(location.InputPath);
        return location;
    }

    private static ImporterWorker CreateWorker(ImporterOptions options, IPaperlessClient client) =>
        new(client, Options.Create(options), new HealthStatus(options.HealthPath), NullLogger<ImporterWorker>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class FakePaperlessClient(IReadOnlyList<PaperlessTaskResult> tasks) : IPaperlessClient
    {
        public int UploadCount { get; private set; }

        public Task<IReadOnlyList<PaperlessTaskResult>> GetTasksAsync(CancellationToken cancellationToken) =>
            Task.FromResult(tasks);

        public Task<string> UploadAsync(string path, string fileName, CancellationToken cancellationToken)
        {
            UploadCount++;
            return Task.FromResult("new-task");
        }
    }
}
