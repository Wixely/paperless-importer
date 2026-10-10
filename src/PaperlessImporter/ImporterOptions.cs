namespace PaperlessImporter;

public sealed class ImporterOptions
{
    public const string SectionName = "Importer";

    public ImportLocationOptions[] Locations { get; set; } = [];
    public string HealthPath { get; set; } = "data/health/status.txt";
    public string[] AllowedExtensions { get; set; } = [".pdf", ".png", ".jpg", ".jpeg", ".tif", ".tiff"];
    public int StablePollsRequired { get; set; } = 2;
    public int MinimumAgeSeconds { get; set; } = 30;
    public int PollIntervalSeconds { get; set; } = 10;
    public long MaximumFileBytes { get; set; } = 100L * 1024 * 1024;
    public int LivenessStaleSeconds { get; set; } = 120;
    public int ReadinessStaleSeconds { get; set; } = 180;
    public int BacklogProgressStaleSeconds { get; set; } = 900;
    public int TaskClockSkewSeconds { get; set; } = 300;
}

public sealed class ImportLocationOptions
{
    public string InputPath { get; set; } = "data/inbox";
    public string ProcessingPath { get; set; } = "data/processing";
    public string OutputPath { get; set; } = "data/archive";
    public string QuarantinePath { get; set; } = "data/quarantine";
    public SuccessDisposition SuccessDisposition { get; set; } = SuccessDisposition.Move;
}

public enum SuccessDisposition
{
    Move,
    Delete,
    Retain
}

public sealed class PaperlessOptions
{
    public const string SectionName = "Paperless";

    public Uri? BaseUrl { get; set; }
    public string Token { get; set; } = string.Empty;
    public string TokenFile { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 120;
}
