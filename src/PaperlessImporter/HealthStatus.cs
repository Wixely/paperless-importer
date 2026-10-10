using System.Globalization;

namespace PaperlessImporter;

public sealed class HealthStatus
{
    private readonly object _gate = new();
    private readonly string _path;
    private DateTimeOffset? _lastScan;
    private DateTimeOffset? _lastProgress;
    private long _backlog;
    private string _dependency = "starting";

    public HealthStatus(string path) => _path = Path.GetFullPath(path);

    public void Update(DateTimeOffset now, bool scanSucceeded, bool madeProgress, long backlog, string dependency)
    {
        lock (_gate)
        {
            if (scanSucceeded) _lastScan = now;
            if (madeProgress) _lastProgress = now;
            _backlog = backlog;
            _dependency = dependency;
            Write(now);
        }
    }

    public void Heartbeat(DateTimeOffset now)
    {
        lock (_gate) Write(now);
    }

    private void Write(DateTimeOffset heartbeat)
    {
        var directory = Path.GetDirectoryName(_path) ?? ".";
        Directory.CreateDirectory(directory);
        var temporary = _path + ".tmp";
        var lines = new[]
        {
            $"heartbeat={heartbeat.UtcTicks.ToString(CultureInfo.InvariantCulture)}",
            $"scan={_lastScan?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? string.Empty}",
            $"progress={_lastProgress?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? string.Empty}",
            $"backlog={_backlog.ToString(CultureInfo.InvariantCulture)}",
            $"dependency={_dependency}"
        };
        File.WriteAllLines(temporary, lines);
        File.Move(temporary, _path, true);
    }

    public static bool Check(string path, bool readiness, ImporterOptions options, DateTimeOffset now)
    {
        try
        {
            var values = File.ReadAllLines(path)
                .Select(line => line.Split('=', 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
            if (!TryAge(values, "heartbeat", now, out var heartbeatAge) ||
                heartbeatAge > TimeSpan.FromSeconds(options.LivenessStaleSeconds)) return false;
            if (!readiness) return true;
            if (!TryAge(values, "scan", now, out var scanAge) ||
                scanAge > TimeSpan.FromSeconds(options.ReadinessStaleSeconds)) return false;
            if (!values.TryGetValue("dependency", out var dependency) || dependency != "ok") return false;
            var backlog = values.TryGetValue("backlog", out var rawBacklog) && long.TryParse(rawBacklog, out var count) ? count : 0;
            if (backlog < 0) return false;
            return backlog == 0 || (TryAge(values, "progress", now, out var progressAge) &&
                progressAge <= TimeSpan.FromSeconds(options.BacklogProgressStaleSeconds));
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static bool TryAge(Dictionary<string, string> values, string key, DateTimeOffset now, out TimeSpan age)
    {
        age = default;
        if (!values.TryGetValue(key, out var raw) || !long.TryParse(raw, CultureInfo.InvariantCulture, out var ticks)) return false;
        age = now - new DateTimeOffset(ticks, TimeSpan.Zero);
        return age >= TimeSpan.Zero;
    }
}
