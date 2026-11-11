using ApiAggregator.Models;

namespace ApiAggregator.Clients;

/// <summary>
/// Contract every external API integration implements. To add a new API:
/// 1) create a class implementing this interface, 2) register it with AddExternalApiClient&lt;T&gt;() in Program.cs.
/// Nothing else changes: aggregation, caching, fallback, parallelism and statistics are applied automatically.
/// </summary>
public interface IExternalApiClient
{
    string Name { get; }
    string Category { get; }

    /// <summary>Cache key built only from the query values this API actually uses.</summary>
    string CacheKey(AggregationQuery query);

    Task<IReadOnlyList<AggregatedItem>> FetchAsync(AggregationQuery query, CancellationToken cancellationToken);
}

internal static class Relevance
{
    /// <summary>Converts a result rank (0 = best) into a 0..1 relevance score.</summary>
    public static double FromRank(int index, int count)
    { 
        return count <= 1 ? 1.0 : Math.Round(1.0 - (double)index / count, 4); 
    }
}
