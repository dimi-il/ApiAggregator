using System.Net;
using System.Text;
using ApiAggregator.Clients;
using ApiAggregator.Models;

namespace ApiAggregator.Tests;

internal static class Items
{
    public static AggregatedItem Make(string source, string category, string title, DateTimeOffset date, double relevance = 0.5) =>
        new(source, category, title, null, null, date, relevance);
}

internal sealed class FakeClient(
    string name,
    string category,
    Func<AggregationQuery, CancellationToken, Task<IReadOnlyList<AggregatedItem>>> handler) : IExternalApiClient
{
    private int _calls;
    public int Calls => _calls;

    public string Name => name;
    public string Category => category;
    public string CacheKey(AggregationQuery query) => $"{name}:{query.Keyword}";

    public Task<IReadOnlyList<AggregatedItem>> FetchAsync(AggregationQuery query, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return handler(query, cancellationToken);
    }

    public static FakeClient Returning(string name, string category, params AggregatedItem[] items) =>
        new(name, category, (_, _) => Task.FromResult<IReadOnlyList<AggregatedItem>>(items));

    public static FakeClient Failing(string name, string category) =>
        new(name, category, (_, _) => Task.FromException<IReadOnlyList<AggregatedItem>>(new HttpRequestException("boom")));
}

internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(respond(request));
    }

    public static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
