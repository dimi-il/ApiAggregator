using ApiAggregator.Background;
using ApiAggregator.Models;
using ApiAggregator.Statistics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ApiAggregator.Tests;

public class PerformanceAnomalyDetectorTests
{
    private static (PerformanceAnomalyDetector Detector, ApiStatsStore Store, FakeTimeProvider Time) Create()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var store = new ApiStatsStore(time);
        var detector = new PerformanceAnomalyDetector(store, time, Options.Create(new AnomalyOptions()));
        return (detector, store, time);
    }

    private static void Record(ApiStatsStore store, int count, int ms)
    {
        for (var i = 0; i < count; i++) store.Record("A", TimeSpan.FromMilliseconds(ms), true);
    }

    [Fact]
    public void Flags_api_whose_recent_average_is_more_than_50_percent_above_baseline()
    {
        var (detector, store, time) = Create();
        Record(store, 20, 100);                    // baseline: 100ms
        time.Advance(TimeSpan.FromMinutes(10));    // baseline leaves the 5 minute window
        Record(store, 5, 200);                     // recent: 200ms (+100%)

        var anomaly = Assert.Single(detector.Detect());

        Assert.Equal("A", anomaly.Api);
        Assert.Equal(200, anomaly.WindowAverageMs, 1);
        Assert.Equal(100, anomaly.BaselineAverageMs, 1);
    }

    [Fact]
    public void Does_not_flag_when_increase_is_within_threshold()
    {
        var (detector, store, time) = Create();
        Record(store, 20, 100);
        time.Advance(TimeSpan.FromMinutes(10));
        Record(store, 5, 140);                     // +40%

        Assert.Empty(detector.Detect());
    }

    [Fact]
    public void Ignores_windows_with_too_few_samples()
    {
        var (detector, store, time) = Create();
        Record(store, 20, 100);
        time.Advance(TimeSpan.FromMinutes(10));
        Record(store, 2, 1000);                    // below MinSamples (3)

        Assert.Empty(detector.Detect());
    }

    [Fact]
    public void Ignores_apis_without_a_baseline()
    {
        var (detector, store, _) = Create();
        Record(store, 5, 500);                     // everything is inside the window

        Assert.Empty(detector.Detect());
    }
}
