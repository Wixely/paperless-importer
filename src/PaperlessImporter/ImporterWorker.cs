using Microsoft.Extensions.Options;

namespace PaperlessImporter;

public sealed class ImporterWorker(
    IPaperlessClient paperless,
    IOptions<ImporterOptions> options,
    HealthStatus health,
    ILogger<ImporterWorker> logger) : BackgroundService
{
    private readonly ImporterOptions _options = options.Value;
    private readonly FileReadinessTracker _readiness = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            TryRecordHealth(() => health.Heartbeat(now));
            try
            {
                var result = await ScanOnceAsync(now, stoppingToken);
                var dependency = result.AllLocationsSucceeded ? "ok" : "filesystem-unavailable";
                TryRecordHealth(() => health.Update(
                    DateTimeOffset.UtcNow,
                    result.AllLocationsSucceeded,
                    result.Progress,
                    result.Backlog,
                    dependency));
                failures = result.AllLocationsSucceeded ? 0 : failures + 1;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                failures++;
                var code = ErrorCode(exception);
                logger.LogError("Import scan failed with code {ErrorCode}; polling will continue", code);
                TryRecordHealth(() => health.Update(
                    DateTimeOffset.UtcNow,
                    false,
                    false,
                    CountBacklogSafely(),
                    code));
            }

            var multiplier = Math.Min(6, Math.Max(1, failures));
            await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds * multiplier), stoppingToken);
        }
    }

    internal async Task<ScanCycleResult> ScanOnceAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tasks = (await paperless.GetTasksAsync(cancellationToken)).ToList();
        var progress = false;
        var allLocationsSucceeded = true;

        for (var index = 0; index < _options.Locations.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                progress |= await ScanLocationAsync(_options.Locations[index], tasks, now, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                allLocationsSucceeded = false;
                logger.LogError(
                    "Import location {LocationIndex} failed with code {ErrorCode}; other locations will continue",
                    index,
                    ErrorCode(exception));
            }
        }

        return new ScanCycleResult(progress, CountBacklogSafely(), allLocationsSucceeded);
    }

    private async Task<bool> ScanLocationAsync(
        ImportLocationOptions location,
        List<PaperlessTaskResult> tasks,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        EnsureLocationAvailable(location);
        var progress = false;

        foreach (var claimDirectory in Directory.EnumerateDirectories(location.ProcessingPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var files = Directory.EnumerateFiles(claimDirectory).Take(2).ToArray();
                if (files.Length != 1)
                {
                    logger.LogWarning("A processing claim has an invalid file count and was left untouched");
                    continue;
                }

                progress |= await ProcessClaimAsync(files[0], claimDirectory, location, tasks, now, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogError("A claimed document could not be processed; code {ErrorCode}", ErrorCode(exception));
            }
        }

        var snapshots = EnumerateInput(location).ToArray();
        var ready = _readiness.Observe(
            snapshots,
            now,
            _options.StablePollsRequired,
            TimeSpan.FromSeconds(_options.MinimumAgeSeconds));

        foreach (var snapshot in ready)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? claimDirectory = null;
            try
            {
                claimDirectory = Path.Combine(
                    location.ProcessingPath,
                    $"{now.UtcTicks}-{Guid.NewGuid():N}");
                Directory.CreateDirectory(claimDirectory);
                var claimedPath = Path.Combine(claimDirectory, snapshot.FileName);
                if (!TryClaim(snapshot.Path, claimedPath))
                {
                    TryDeleteEmptyDirectory(claimDirectory);
                    logger.LogDebug("A candidate is still open or unavailable and will be retried on the next poll");
                    continue;
                }

                _readiness.Forget(snapshot.Path);
                progress = true;
                progress |= await ProcessClaimAsync(claimedPath, claimDirectory, location, tasks, now, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                if (claimDirectory is not null) TryDeleteEmptyDirectory(claimDirectory);
                logger.LogError("An inbox document could not be claimed; code {ErrorCode}", ErrorCode(exception));
            }
        }

        return progress;
    }

    private async Task<bool> ProcessClaimAsync(
        string path,
        string claimDirectory,
        ImportLocationOptions location,
        List<PaperlessTaskResult> tasks,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(path);
        var modified = File.GetLastWriteTimeUtc(path);
        var earliestTask = new DateTimeOffset(modified, TimeSpan.Zero).AddSeconds(-_options.TaskClockSkewSeconds);
        var matchingTask = tasks
            .Where(task => string.Equals(task.FileName, fileName, StringComparison.OrdinalIgnoreCase))
            .Where(task => task.CreatedAt is not null && task.CreatedAt >= earliestTask)
            .OrderByDescending(task => task.CreatedAt)
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(matchingTask.TaskId))
        {
            if (matchingTask.State == PaperlessTaskState.Pending) return false;
            var destination = matchingTask.State == PaperlessTaskState.Success
                ? location.OutputPath
                : location.QuarantinePath;
            MoveToUnique(path, destination);
            Directory.Delete(claimDirectory, false);
            logger.LogInformation(
                "A Paperless task reached {TaskState}; the source was moved to its configured destination",
                matchingTask.State);
            return true;
        }

        var taskId = await paperless.UploadAsync(path, fileName, cancellationToken);
        tasks.Add(new PaperlessTaskResult(taskId, fileName, now, PaperlessTaskState.Pending, null));
        logger.LogInformation("A document was accepted by Paperless and remains claimed pending task completion");
        return true;
    }

    private IEnumerable<FileSnapshot> EnumerateInput(ImportLocationOptions location)
    {
        var extensions = new HashSet<string>(
            _options.AllowedExtensions.Select(NormalizeExtension),
            StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(location.InputPath, "*", SearchOption.TopDirectoryOnly))
        {
            FileInfo info;
            try
            {
                info = new FileInfo(path);
                info.Refresh();
                if (!info.Exists) continue;
            }
            catch (IOException)
            {
                continue;
            }

            var extension = NormalizeExtension(info.Extension);
            if (!extensions.Contains(extension) || info.Length <= 0 || info.Length > _options.MaximumFileBytes) continue;
            yield return new FileSnapshot(info.FullName, info.Name, extension, info.Length, info.LastWriteTimeUtc.Ticks);
        }
    }

    private static bool TryClaim(string source, string destination)
    {
        try
        {
            using (new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.None, 1, FileOptions.None)) { }
            File.Move(source, destination, false);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string NormalizeExtension(string extension) =>
        extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";

    private static void MoveToUnique(string source, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        var fileName = Path.GetFileName(source);
        var destination = Path.Combine(destinationDirectory, fileName);
        if (File.Exists(destination))
        {
            destination = Path.Combine(destinationDirectory,
                $"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}{Path.GetExtension(fileName)}");
        }
        File.Move(source, destination, false);
    }

    private static void EnsureLocationAvailable(ImportLocationOptions location)
    {
        if (!Directory.Exists(location.InputPath))
            throw new DirectoryNotFoundException("The configured input directory is unavailable.");

        Directory.CreateDirectory(location.ProcessingPath);
        Directory.CreateDirectory(location.OutputPath);
        Directory.CreateDirectory(location.QuarantinePath);
    }

    private long CountBacklogSafely()
    {
        long count = 0;
        foreach (var location in _options.Locations)
        {
            try
            {
                count += Directory.EnumerateFiles(location.InputPath).LongCount();
                count += Directory.EnumerateFiles(location.ProcessingPath, "*", SearchOption.AllDirectories).LongCount();
            }
            catch (IOException) { return -1; }
            catch (UnauthorizedAccessException) { return -1; }
        }
        return count;
    }

    private void TryRecordHealth(Action action)
    {
        try
        {
            action();
        }
        catch (IOException)
        {
            logger.LogWarning("The health status file is unavailable; importing will continue");
        }
        catch (UnauthorizedAccessException)
        {
            logger.LogWarning("The health status file is not writable; importing will continue");
        }
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        try { Directory.Delete(path, false); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string ErrorCode(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "access-denied",
        DirectoryNotFoundException => "directory-unavailable",
        InvalidOperationException => "configuration-error",
        InvalidDataException => "remote-contract-error",
        HttpRequestException => "paperless-unavailable",
        IOException => "filesystem-error",
        _ => "unexpected-error"
    };
}

internal readonly record struct ScanCycleResult(bool Progress, long Backlog, bool AllLocationsSucceeded);
