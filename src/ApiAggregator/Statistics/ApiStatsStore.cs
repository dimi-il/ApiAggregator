using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace ApiAggregator.Statistics;

public sealed record BucketStats(long Count, double AverageResponseMs);

public sealed record PerformanceBuckets(BucketStats Fast, BucketStats Average, BucketStats Slow);

public sealed record ApiStatsSnapshot(
    string Api,
    long TotalRequests,
    long FailedRequests,
    double AverageResponseMs,
    PerformanceBuckets Buckets)
{
    /// <summary>Exact lifetime sum, used by the anomaly detector (not part of the public JSON).</summary>
    [JsonIgnore]
    public double TotalResponseTimeMs { get; init; }
}

public readonly record struct WindowStats(long Count, double TotalMs);

public interface IApiStatsStore
{
    void Record(string api, TimeSpan elapsed, bool success);
    IReadOnlyList<ApiStatsSnapshot> GetSnapshot();
    WindowStats GetWindow(string api, DateTimeOffset since);
}

/// <summary>
/// In-memory, thread-safe statistics. One lock per API (not a global lock), so recording
/// for different APIs never contends. Buckets: fast &lt; 100ms, average 100-200ms, slow &gt; 200ms.
/// </summary>
public sealed class ApiStatsStore(TimeProvider time) : IApiStatsStore
{
    public const double FastThresholdMs = 100;
    public const double SlowThresholdMs = 200;

    private static readonly TimeSpan SampleRetention = TimeSpan.FromHours(1);
    private const int MaxSamplesPerApi = 50_000;

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public void Record(string api, TimeSpan elapsed, bool success)
    {
        var entry = _entries.GetOrAdd(api, _ => new Entry());
        entry.Add(time.GetUtcNow(), elapsed.TotalMilliseconds, success);
    }

    public IReadOnlyList<ApiStatsSnapshot> GetSnapshot() =>
        _entries.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
                .Select(e => e.Value.ToSnapshot(e.Key))
                .ToList();

    public WindowStats GetWindow(string api, DateTimeOffset since) =>
        _entries.TryGetValue(api, out var entry) ? entry.GetWindow(since) : default;

    private sealed class Entry
    {
        private readonly object _gate = new();
        private readonly long[] _bucketCounts = new long[3];
        private readonly double[] _bucketSums = new double[3];
        private readonly Queue<(DateTimeOffset At, double Ms)> _samples = new();
        private long _total;
        private long _failed;
        private double _sum;

        public void Add(DateTimeOffset at, double ms, bool success)
        {
            var bucket = ms < FastThresholdMs ? 0 : ms <= SlowThresholdMs ? 1 : 2;
            lock (_gate)
            {
                _total++;
                if (!success) _failed++;
                _sum += ms;
                _bucketCounts[bucket]++;
                _bucketSums[bucket] += ms;

                _samples.Enqueue((at, ms));
                while (_samples.Count > MaxSamplesPerApi ||
                       (_samples.Count > 0 && at - _samples.Peek().At > SampleRetention))
                {
                    _samples.Dequeue();
                }
            }
        }

        public ApiStatsSnapshot ToSnapshot(string api)
        {
            lock (_gate)
            {
                static BucketStats Bucket(long count, double sum) =>
                    new(count, count == 0 ? 0 : Math.Round(sum / count, 2));

                return new ApiStatsSnapshot(
                    api,
                    _total,
                    _failed,
                    _total == 0 ? 0 : Math.Round(_sum / _total, 2),
                    new PerformanceBuckets(
                        Bucket(_bucketCounts[0], _bucketSums[0]),
                        Bucket(_bucketCounts[1], _bucketSums[1]),
                        Bucket(_bucketCounts[2], _bucketSums[2])))
                {
                    TotalResponseTimeMs = _sum
                };
            }
        }

        public WindowStats GetWindow(DateTimeOffset since)
        {
            lock (_gate)
            {
                long count = 0;
                double total = 0;
                foreach (var (at, ms) in _samples)
                {
                    if (at < since) continue;
                    count++;
                    total += ms;
                }
                return new WindowStats(count, total);
            }
        }
    }
}
