using System.Diagnostics;
using ApiAggregator.Clients;
using ApiAggregator.Models;
using ApiAggregator.Statistics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ApiAggregator.Services;

public interface IAggregationService
{
    Task<AggregationResult> AggregateAsync(AggregationQuery query, CancellationToken cancellationToken = default);
}

public sealed class AggregationService(IEnumerable<IExternalApiClient> clients,IMemoryCache cache,IApiStatsStore stats,IOptions<AggregationOptions> options,ILogger<AggregationService> logger) : IAggregationService
{
    private readonly IReadOnlyList<IExternalApiClient> _clients = clients.ToList();
    private readonly AggregationOptions _options = options.Value;

    public async Task<AggregationResult> AggregateAsync(AggregationQuery query, CancellationToken cancellationToken = default)
    {
        // Only call the APIs that can contribute to this request (source / category filters).
        var selected = _clients.ToList();

        // Parallelism: all selected APIs are called at the same time; FetchSourceAsync never throws
        // (except on cancellation), so one failing API cannot fail the whole request.
        var tasks = selected.Select(c => FetchSourceAsync(c, query, cancellationToken)).ToList();
        var results = await Task.WhenAll(tasks);

        IEnumerable<AggregatedItem> items = results.SelectMany(r => r.Items);
        items = ApplyFilters(items, query);
        var sorted = Sort(items, query).ToList();

        return new AggregationResult(sorted, results.Select(r => r.Status).ToList(), sorted.Count);
    }

    private async Task<(IReadOnlyList<AggregatedItem> Items, SourceStatus Status)> FetchSourceAsync(IExternalApiClient client, AggregationQuery query, CancellationToken cancellationToken)
    {
        var key = client.CacheKey(query);
        var freshKey = $"fresh:{key}";
        var staleKey = $"stale:{key}";

        if (cache.TryGetValue(freshKey, out IReadOnlyList<AggregatedItem>? cached) && cached is not null)
        {
            return (cached, new SourceStatus(client.Name, SourceState.Cached, 0, null));
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var items = await client.FetchAsync(query, cancellationToken);
            stopwatch.Stop();
            stats.Record(client.Name, stopwatch.Elapsed, success: true);

            if (_options.CacheDuration > TimeSpan.Zero)
                cache.Set(freshKey, items, _options.CacheDuration);
            cache.Set(staleKey, items, _options.StaleDuration);

            return (items, new SourceStatus(client.Name, SourceState.Ok, stopwatch.ElapsedMilliseconds, null));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // the caller went away; this is not an API failure
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            stats.Record(client.Name, stopwatch.Elapsed, success: false);
            logger.LogWarning(ex, "External API {Api} failed after {ElapsedMs}ms", client.Name, stopwatch.ElapsedMilliseconds);

            // Fallback: serve the last known good data if we have it.
            if (cache.TryGetValue(staleKey, out IReadOnlyList<AggregatedItem>? stale) && stale is not null)
            {
                return (stale, new SourceStatus(client.Name, SourceState.Stale, stopwatch.ElapsedMilliseconds, ex.Message));
            }

            return (Array.Empty<AggregatedItem>(),new SourceStatus(client.Name, SourceState.Failed, stopwatch.ElapsedMilliseconds, ex.Message));
        }
    }



    private static IEnumerable<AggregatedItem> ApplyFilters(IEnumerable<AggregatedItem> items, AggregationQuery query)
    {
    

        if (query.Year is { } year) items = items.Where(i => i.Date.Year == year);
        
        return items;
    }

    private static IEnumerable<AggregatedItem> Sort(IEnumerable<AggregatedItem> items, AggregationQuery query)
    {
        var desc = query.Order == SortDirection.Desc;
        IOrderedEnumerable<AggregatedItem> ordered = query.SortBy switch
        {
            SortField.Relevance => desc ? items.OrderByDescending(i => i.Relevance) : items.OrderBy(i => i.Relevance),
            SortField.Title => desc
                ? items.OrderByDescending(i => i.Title, StringComparer.OrdinalIgnoreCase)
                : items.OrderBy(i => i.Title, StringComparer.OrdinalIgnoreCase),
            _ => desc ? items.OrderByDescending(i => i.Date) : items.OrderBy(i => i.Date)
        };

        // Deterministic tie-breakers so results are stable between calls.
        return ordered
            .ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Source, StringComparer.OrdinalIgnoreCase);
    }
}
