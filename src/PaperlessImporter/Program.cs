using Microsoft.Extensions.Options;
using PaperlessImporter;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<ImporterOptions>()
    .BindConfiguration(ImporterOptions.SectionName)
    .Validate(options =>
        options.Locations.Length > 0 &&
        options.Locations.All(location =>
            !string.IsNullOrWhiteSpace(location.InputPath) &&
            !string.IsNullOrWhiteSpace(location.ProcessingPath) &&
            !string.IsNullOrWhiteSpace(location.QuarantinePath) &&
            (location.SuccessDisposition != SuccessDisposition.Move ||
                !string.IsNullOrWhiteSpace(location.OutputPath)) &&
            !PathsEqual(location.InputPath, location.ProcessingPath) &&
            !PathsEqual(location.InputPath, location.QuarantinePath) &&
            !PathsEqual(location.ProcessingPath, location.QuarantinePath) &&
            (location.SuccessDisposition != SuccessDisposition.Move ||
                (!PathsEqual(location.InputPath, location.OutputPath) &&
                 !PathsEqual(location.ProcessingPath, location.OutputPath) &&
                 !PathsEqual(location.OutputPath, location.QuarantinePath)))) &&
        !string.IsNullOrWhiteSpace(options.HealthPath) &&
        options.StablePollsRequired >= 2 &&
        options.MinimumAgeSeconds > 0 &&
        options.PollIntervalSeconds > 0 &&
        options.MaximumFileBytes > 0 &&
        options.LivenessStaleSeconds > 0 &&
        options.ReadinessStaleSeconds > 0 &&
        options.BacklogProgressStaleSeconds > 0 &&
        options.TaskClockSkewSeconds >= 0 &&
        options.AllowedExtensions.Length > 0,
        "Importer paths, extensions, and positive timing/size limits are required.")
    .ValidateOnStart();
builder.Services
    .AddOptions<PaperlessOptions>()
    .BindConfiguration(PaperlessOptions.SectionName)
    .Validate(options => options.BaseUrl is { IsAbsoluteUri: true }, "Paperless:BaseUrl must be an absolute URL.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Token) || !string.IsNullOrWhiteSpace(options.TokenFile),
        "Paperless:Token or Paperless:TokenFile is required.")
    .ValidateOnStart();

builder.Services.AddWindowsService(options => options.ServiceName = "Paperless Importer");
builder.Services.AddSystemd();
builder.Services.AddHttpClient<IPaperlessClient, PaperlessClient>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<PaperlessOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
});
builder.Services.AddSingleton(services =>
    new HealthStatus(services.GetRequiredService<IOptions<ImporterOptions>>().Value.HealthPath));
builder.Services.AddHostedService<ImporterWorker>();

using var host = builder.Build();
if (args.Contains("--health-live", StringComparer.OrdinalIgnoreCase) ||
    args.Contains("--health-ready", StringComparer.OrdinalIgnoreCase))
{
    var importer = host.Services.GetRequiredService<IOptions<ImporterOptions>>().Value;
    var readiness = args.Contains("--health-ready", StringComparer.OrdinalIgnoreCase);
    return HealthStatus.Check(importer.HealthPath, readiness, importer, DateTimeOffset.UtcNow) ? 0 : 1;
}

try
{
    await host.Services
        .GetRequiredService<IPaperlessClient>()
        .VerifyStartupPrerequisitesAsync(CancellationToken.None);
}
catch (PaperlessStartupException exception)
{
    Console.Error.WriteLine($"Startup preflight failed: {exception.Message}");
    return 78;
}
catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException)
{
    Console.Error.WriteLine("Startup preflight failed: Paperless or its mounted token is unavailable or invalid.");
    return 69;
}

await host.RunAsync();
return 0;

static bool PathsEqual(string left, string right) =>
    string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
