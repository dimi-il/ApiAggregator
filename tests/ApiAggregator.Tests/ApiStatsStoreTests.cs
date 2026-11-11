using ApiAggregator.Statistics;
using Microsoft.Extensions.Time.Testing;

namespace ApiAggregator.Tests;

public class ApiStatsStoreTests
{
    [Fact]
    public void Groups_requests_into_performance_buckets()
    {
        var store = new ApiStatsStore(TimeProvider.System);
        store.Record("A", TimeSpan.FromMilliseconds(50), true);    // fast
        store.Record("A", TimeSpan.FromMilliseconds(99), true);    // fast
        store.Record("A", TimeSpan.FromMilliseconds(100), true);   // average (100-200 inclusive)
        store.Record("A", TimeSpan.FromMilliseconds(200), true);   // average
        store.Record("A", TimeSpan.FromMilliseconds(250), false);  // slow

        var s = store.GetSnapshot().Single();

        Assert.Equal(5, s.TotalRequests);
        Assert.Equal(1, s.FailedRequests);
        Assert.Equal(2, s.Buckets.Fast.Count);
        Assert.Equal(2, s.Buckets.Average.Count);
        Assert.Equal(1, s.Buckets.Slow.Count);
        Assert.Equal(74.5, s.Buckets.Fast.AverageResponseMs);
        Assert.Equal(150, s.Buckets.Average.AverageResponseMs);
        Assert.Equal(250, s.Buckets.Slow.AverageResponseMs);
        Assert.Equal(139.8, s.AverageResponseMs);
    }

    [Fact]
    public void Keeps_apis_separate_and_sorted_by_name()
    {
        var store = new ApiStatsStore(TimeProvider.System);
        store.Record("Zeta", TimeSpan.FromMilliseconds(10), true);
        store.Record("Alpha", TimeSpan.FromMilliseconds(10), true);

        Assert.Equal(new[] { "Alpha", "Zeta" }, store.GetSnapshot().Select(s => s.Api));
    }

    [Fact]
    public void Is_thread_safe_under_concurrent_writes()
    {
        var store = new ApiStatsStore(TimeProvider.System);

        Parallel.For(0, 20_000, i => store.Record(i % 2 == 0 ? "A" : "B", TimeSpan.FromMilliseconds(i % 300), true));

        var snapshot = store.GetSnapshot();
        Assert.Equal(10_000, snapshot.Single(s => s.Api == "A").TotalRequests);
        Assert.Equal(10_000, snapshot.Single(s => s.Api == "B").TotalRequests);
        foreach (var s in snapshot)
        {
            Assert.Equal(s.TotalRequests, s.Buckets.Fast.Count + s.Buckets.Average.Count + s.Buckets.Slow.Count);
        }
    }

    [Fact]
    public void Window_only_counts_recent_samples()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var store = new ApiStatsStore(time);

        store.Record("A", TimeSpan.FromMilliseconds(100), true);
        time.Advance(TimeSpan.FromMinutes(10));
        store.Record("A", TimeSpan.FromMilliseconds(300), true);

        var window = store.GetWindow("A", time.GetUtcNow() - TimeSpan.FromMinutes(5));

        Assert.Equal(1, window.Count);
        Assert.Equal(300, window.TotalMs);
        Assert.Equal(default, store.GetWindow("unknown", DateTimeOffset.MinValue));
    }
}
