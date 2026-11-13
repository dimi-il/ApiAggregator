using ApiAggregator.Models;
using Microsoft.Extensions.Options;


namespace ApiAggregator.Background;

/// <summary>Runs the anomaly detector periodically and logs a warning per anomaly.</summary>
public sealed class PerformanceAnomalyService(PerformanceAnomalyDetector detector,IOptions<AnomalyOptions> options,ILogger<PerformanceAnomalyService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.CheckInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    foreach (var a in detector.Detect())
                    {
                        logger.LogWarning(
                            "Performance anomaly: {Api} averaged {WindowAvg:F0}ms over the last window ({Samples} calls) vs {Baseline:F0}ms before ({Ratio:P0} of baseline)",
                            a.Api, a.WindowAverageMs, a.WindowSamples, a.BaselineAverageMs, a.WindowAverageMs / a.BaselineAverageMs);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Anomaly check failed; will retry on next tick");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
    }
}
