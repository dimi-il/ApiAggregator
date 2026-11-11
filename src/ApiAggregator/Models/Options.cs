namespace ApiAggregator.Models;

public sealed class AggregationOptions
{
    /// <summary>How long a successful response is served without calling the external API. Zero disables the fresh cache.</summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>How long the last good response is kept as fallback when an API is down.</summary>
    public TimeSpan StaleDuration { get; set; } = TimeSpan.FromHours(1);
}

public sealed class AnomalyOptions
{
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Anomaly when window average &gt; baseline average * ratio (1.5 = "50% bigger").</summary>
    public double ThresholdRatio { get; set; } = 1.5;

    /// <summary>Minimum samples in the window before a verdict is made (avoids noise).</summary>
    public int MinSamples { get; set; } = 3;
}
