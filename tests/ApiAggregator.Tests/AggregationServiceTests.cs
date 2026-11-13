using ApiAggregator.Clients;
using ApiAggregator.Models;
using ApiAggregator.Services;
using ApiAggregator.Statistics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ApiAggregator.Tests;

public class AggregationServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (AggregationService Service, ApiStatsStore Stats) Create(
        IEnumerable<IExternalApiClient> clients, AggregationOptions? options = null)
    {
        var stats = new ApiStatsStore(TimeProvider.System);
        var service = new AggregationService(
            clients,
            new MemoryCache(new MemoryCacheOptions()),
            stats,
            Options.Create(options ?? new AggregationOptions()),
            NullLogger<AggregationService>.Instance);
        return (service, stats);
    }

    [Fact]
    public async Task Combines_items_from_all_clients()
    {
        var (service, _) = Create(new[]
        {
            FakeClient.Returning("A", "Weather", Items.Make("A", "Weather", "a1", T0)),
            FakeClient.Returning("B", "Currency", Items.Make("B", "Currency", "b1", T0)),
            FakeClient.Returning("C", "Holiday", Items.Make("C", "Holiday", "c1", T0))
        });

        var result = await service.AggregateAsync(new AggregationQuery());

        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Sources, s => Assert.Equal(SourceState.Ok, s.State));
    }

    [Fact]
    public async Task Calls_clients_in_parallel()
    {
        // Each client waits until BOTH have started. If calls were sequential the first would
        // time out waiting for the second and its status would be Failed.
        var started = 0;
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<IReadOnlyList<AggregatedItem>> Handler(AggregationQuery q, CancellationToken ct)
        {
            if (Interlocked.Increment(ref started) == 2) bothStarted.SetResult();
            await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), ct);
            return Array.Empty<AggregatedItem>();
        }

        var (service, _) = Create(new IExternalApiClient[]
        {
            new FakeClient("A", "Weather", Handler),
            new FakeClient("B", "Currency", Handler),
            new FakeClient("C", "Holiday", Handler)
        });

        var result = await service.AggregateAsync(new AggregationQuery());

        Assert.All(result.Sources, s => Assert.Equal(SourceState.Ok, s.State));
    }

    [Fact]
    public async Task One_failing_api_does_not_fail_the_request()
    {
        var (service, _) = Create(new[]
        {
            FakeClient.Returning("Good", "News", Items.Make("Good", "News", "ok", T0)),
            FakeClient.Failing("Bad", "Weather")
        });

        var result = await service.AggregateAsync(new AggregationQuery());

        Assert.Single(result.Items);
        Assert.Equal(SourceState.Ok, result.Sources.Single(s => s.Source == "Good").State);
        var bad = result.Sources.Single(s => s.Source == "Bad");
        Assert.Equal(SourceState.Failed, bad.State);
        Assert.Equal("boom", bad.Error);
    }

    [Fact]
    public async Task Falls_back_to_last_good_data_when_api_goes_down()
    {
        var down = false;
        var client = new FakeClient("A", "News", async (_, _) =>
        {
            await Task.Yield();
            if (down) throw new HttpRequestException("down");
            return new[] { Items.Make("A", "News", "cached-item", T0) };
        });

        // CacheDuration zero => every call goes to the API, but good results are kept as fallback.
        var (service, _) = Create(new[] { client }, new AggregationOptions { CacheDuration = TimeSpan.Zero });

        var first = await service.AggregateAsync(new AggregationQuery());
        Assert.Equal(SourceState.Ok, first.Sources.Single().State);

        down = true;
        var second = await service.AggregateAsync(new AggregationQuery());

        Assert.Equal(SourceState.Stale, second.Sources.Single().State);
        Assert.Equal("cached-item", second.Items.Single().Title);
    }

    [Fact]
    public async Task Second_identical_request_is_served_from_cache()
    {
        var client = FakeClient.Returning("A", "News", Items.Make("A", "News", "x", T0));
        var (service, _) = Create(new[] { client });

        await service.AggregateAsync(new AggregationQuery());
        var second = await service.AggregateAsync(new AggregationQuery());

        Assert.Equal(1, client.Calls);
        Assert.Equal(SourceState.Cached, second.Sources.Single().State);
    }

    [Fact]
    public async Task Different_query_values_use_different_cache_entries()
    {
        var client = FakeClient.Returning("A", "News", Items.Make("A", "News", "x", T0));
        var (service, _) = Create(new[] { client });

        await service.AggregateAsync(new AggregationQuery { Keyword = "one" });
        await service.AggregateAsync(new AggregationQuery { Keyword = "two" });

        Assert.Equal(2, client.Calls);
    }

 

    [Fact]
    public async Task Filters_by_year()
    {
        var (service, _) = Create(new[]
        {
        FakeClient.Returning("A", "News",
            Items.Make("A", "News", "old", new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)),
            Items.Make("A", "News", "mid", new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)),
            Items.Make("A", "News", "new", new DateTimeOffset(2027, 6, 1, 0, 0, 0, TimeSpan.Zero)))
    });

        var result = await service.AggregateAsync(new AggregationQuery { Year = 2026 });

        Assert.Equal(new[] { "mid" }, result.Items.Select(i => i.Title));
    }


    [Theory]
    [InlineData(SortField.Date, SortDirection.Desc, new[] { "c", "b", "a" })]
    [InlineData(SortField.Date, SortDirection.Asc, new[] { "a", "b", "c" })]
    [InlineData(SortField.Relevance, SortDirection.Desc, new[] { "b", "c", "a" })]
    [InlineData(SortField.Title, SortDirection.Asc, new[] { "a", "b", "c" })]
    [InlineData(SortField.Title, SortDirection.Desc, new[] { "c", "b", "a" })]
    public async Task Sorts_by_requested_field_and_direction(SortField field, SortDirection order, string[] expected)
    {
        var (service, _) = Create(new[]
        {
            FakeClient.Returning("A", "News",
                Items.Make("A", "News", "a", T0.AddDays(1), relevance: 0.1),
                Items.Make("A", "News", "b", T0.AddDays(2), relevance: 0.9),
                Items.Make("A", "News", "c", T0.AddDays(3), relevance: 0.5))
        });

        var result = await service.AggregateAsync(new AggregationQuery { SortBy = field, Order = order });

        Assert.Equal(expected, result.Items.Select(i => i.Title));
    }

    [Fact]
    public async Task Records_statistics_for_success_and_failure()
    {
        var (service, stats) = Create(new[]
        {
            FakeClient.Returning("Good", "Weather", Items.Make("Good", "Weather", "ok", T0)),
            FakeClient.Failing("Bad", "Weather"),
            FakeClient.Failing("Good", "Holidays")
        });

        await service.AggregateAsync(new AggregationQuery());

        var snapshot = stats.GetSnapshot();
        Assert.Equal(1, snapshot.Single(s => s.Api == "Good").TotalRequests);
        Assert.Equal(0, snapshot.Single(s => s.Api == "Good").FailedRequests);
        Assert.Equal(1, snapshot.Single(s => s.Api == "Bad").FailedRequests);
    }

    [Fact]
    public async Task Cancellation_is_propagated_and_not_treated_as_api_failure()
    {
        var client = new FakeClient("A", "Weather", async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Array.Empty<AggregatedItem>();
        });
        var (service, stats) = Create(new[] { client });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.AggregateAsync(new AggregationQuery(), cts.Token));

        Assert.Empty(stats.GetSnapshot());
    }


}
