namespace PaperlessImporter;

public sealed class FileReadinessTracker
{
    private readonly Dictionary<string, FileObservation> _observations = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<FileSnapshot> Observe(
        IEnumerable<FileSnapshot> snapshots,
        DateTimeOffset now,
        int stablePollsRequired,
        TimeSpan minimumAge)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ready = new List<FileSnapshot>();

        foreach (var snapshot in snapshots)
        {
            seen.Add(snapshot.Path);
            var stablePolls = 1;
            if (_observations.TryGetValue(snapshot.Path, out var prior) &&
                prior.SizeBytes == snapshot.SizeBytes &&
                prior.ModifiedUtcTicks == snapshot.ModifiedUtcTicks)
            {
                stablePolls = prior.StablePolls + 1;
            }

            _observations[snapshot.Path] = new FileObservation(snapshot.SizeBytes, snapshot.ModifiedUtcTicks, stablePolls);
            var age = now - new DateTimeOffset(snapshot.ModifiedUtcTicks, TimeSpan.Zero);
            if (stablePolls >= stablePollsRequired && age >= minimumAge)
            {
                ready.Add(snapshot);
            }
        }

        foreach (var missing in _observations.Keys.Where(path => !seen.Contains(path)).ToArray())
        {
            _observations.Remove(missing);
        }

        return ready;
    }

    public void Forget(string path) => _observations.Remove(path);
}
