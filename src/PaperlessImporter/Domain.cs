namespace PaperlessImporter;

public readonly record struct FileSnapshot(string Path, string FileName, string Extension, long SizeBytes, long ModifiedUtcTicks);

public sealed record FileObservation(long SizeBytes, long ModifiedUtcTicks, int StablePolls);

public enum PaperlessTaskState { Pending, Success, Failure }
public readonly record struct PaperlessTaskResult(
    string TaskId,
    string? FileName,
    DateTimeOffset? CreatedAt,
    PaperlessTaskState State,
    string? ErrorCode);
