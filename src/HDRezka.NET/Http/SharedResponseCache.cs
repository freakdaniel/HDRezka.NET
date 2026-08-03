using HdRezka.Observability;

namespace HdRezka.Http;

internal sealed class SharedResponseCache(
    Func<TimeProvider> timeProvider,
    Func<int> maximumEntries)
{
    private readonly object _lock = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public Task<string> GetAsync(
        string key,
        TimeSpan retention,
        Func<CancellationToken, Task<string>> factory,
        CancellationToken lifetimeToken,
        CancellationToken cancellationToken)
    {
        Entry entry;
        string outcome;
        lock (_lock)
        {
            var now = timeProvider().GetUtcNow();
            RemoveExpired(now);
            if (_entries.TryGetValue(key, out entry!) &&
                (!entry.ExpiresAt.HasValue || entry.ExpiresAt > now))
            {
                outcome = entry.Task.IsCompletedSuccessfully ? "hit" : "shared";
            }
            else
            {
                RemoveOldestCompletedEntries();
                entry = new Entry(now);
                _entries[key] = entry;
                entry.Task = LoadAsync(key, entry, retention, factory, lifetimeToken);
                outcome = "miss";
            }
        }

        Telemetry.CacheRequest(outcome);
        return entry.Task.WaitAsync(cancellationToken);
    }

    public void Remove(string key)
    {
        lock (_lock)
        {
            _entries.Remove(key);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    private async Task<string> LoadAsync(
        string key,
        Entry entry,
        TimeSpan retention,
        Func<CancellationToken, Task<string>> factory,
        CancellationToken lifetimeToken)
    {
        try
        {
            var value = await factory(lifetimeToken).ConfigureAwait(false);
            lock (_lock)
            {
                if (_entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
                {
                    if (retention == TimeSpan.Zero)
                    {
                        _entries.Remove(key);
                    }
                    else
                    {
                        entry.ExpiresAt = timeProvider().GetUtcNow() + retention;
                    }
                }
            }

            return value;
        }
        catch
        {
            lock (_lock)
            {
                if (_entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
                {
                    _entries.Remove(key);
                }
            }

            throw;
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var key in _entries
                     .Where(pair => pair.Value.ExpiresAt <= now)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _entries.Remove(key);
        }
    }

    private void RemoveOldestCompletedEntries()
    {
        while (_entries.Count >= maximumEntries())
        {
            var candidate = _entries
                .Where(pair => pair.Value.Task.IsCompleted)
                .OrderBy(pair => pair.Value.CreatedAt)
                .Select(pair => pair.Key)
                .FirstOrDefault();
            if (candidate is null)
            {
                return;
            }

            _entries.Remove(candidate);
        }
    }

    private sealed class Entry(DateTimeOffset createdAt)
    {
        public DateTimeOffset CreatedAt { get; } = createdAt;

        public DateTimeOffset? ExpiresAt { get; set; }

        public Task<string> Task { get; set; } = null!;
    }
}
