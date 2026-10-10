namespace PaperlessImporter;

public sealed class ImporterOptions
{
    public const string SectionName = "Importer";

    public ImportLocationOptions[] Locations { get; init; } = [new()];
    public string HealthPath { get; init; } = "data/health/status.txt";
    public string[] AllowedExtensions { get; init; } = [".pdf", ".png", ".jpg", ".jpeg", ".tif", ".tiff"];
    public int StablePollsRequired { get; init; } = 2;
    public int MinimumAgeSeconds { get; init; } = 30;
    public int PollIntervalSeconds { get; init; } = 10;
    public long MaximumFileBytes { get; init; } = 100L * 1024 * 1024;
    public int LivenessStaleSeconds { get; init; } = 120;
    public int ReadinessStaleSeconds { get; init; } = 180;
    public int BacklogProgressStaleSeconds { get; init; } = 900;
    public int TaskClockSkewSeconds { get; init; } = 300;
}

public sealed class ImportLocationOptions
{
    public string InputPath { get; init; } = "data/inbox";
    public string ProcessingPath { get; init; } = "data/processing";
    public string OutputPath { get; init; } = "data/archive";
    public string QuarantinePath { get; init; } = "data/quarantine";
}

public sealed class PaperlessOptions
{
    public const string SectionName = "Paperless";

    public Uri? BaseUrl { get; init; }
    public string Token { get; init; } = string.Empty;
    public string TokenFile { get; init; } = string.Empty;
    public int RequestTimeoutSeconds { get; init; } = 120;
}
