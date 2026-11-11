using ApiAggregator.Models;
using ApiAggregator.Statistics;
using Microsoft.Extensions.Options;

namespace ApiAggregator.Background;

public sealed record PerformanceAnomaly(string Api, double WindowAverageMs, double BaselineAverageMs, long WindowSamples);

/// <summary>
/// Compares the average response time of each API over the last window (default 5 min)
/// with its average over all earlier requests. Flags it if the window is more than
/// ThresholdRatio (default 1.5 = 50% bigger) times slower.
/// </summary>
public sealed class PerformanceAnomalyDetector(
    IApiStatsStore store,
    TimeProvider time,
    IOptions<AnomalyOptions> options)
{
    public IReadOnlyList<PerformanceAnomaly> Detect()
    {
        var o = options.Value;
        var since = time.GetUtcNow() - o.Window;
        var anomalies = new List<PerformanceAnomaly>();

        foreach (var snapshot in store.GetSnapshot())
        {
            var window = store.GetWindow(snapshot.Api, since);
            if (window.Count < o.MinSamples) continue;

            // Baseline = everything before the window, so a burst of slow calls cannot hide itself.
            var baselineCount = snapshot.TotalRequests - window.Count;
            if (baselineCount <= 0) continue;

            var baselineAverage = (snapshot.TotalResponseTimeMs - window.TotalMs) / baselineCount;
            if (baselineAverage <= 0) continue;

            var windowAverage = window.TotalMs / window.Count;
            if (windowAverage > baselineAverage * o.ThresholdRatio)
            {
                anomalies.Add(new PerformanceAnomaly(snapshot.Api, windowAverage, baselineAverage, window.Count));
            }
        }

        return anomalies;
    }
}
