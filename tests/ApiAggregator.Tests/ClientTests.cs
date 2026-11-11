using ApiAggregator.Clients;
using ApiAggregator.Models;

namespace ApiAggregator.Tests;

public class ClientTests
{

    [Fact]
    public async Task Nagerholidays_fetcher_returns_expected_items()
    {
        var handler = new StubHandler(_ => StubHandler.Json("""
            {
                "holidays": [
                    {"name":"New Year's Day","date":"2026-01-01"},
                    {"name":"Independence Day","date":"2026-07-04"}
                ]
            }
        """));

        var client = new NagerholidaysClient(new HttpClient(handler));

        var items = await client.FetchAsync(new AggregationQuery { Country = "US" }, CancellationToken.None);

        Assert.Collection(items,
            item =>
            {
                Assert.Equal("New Year's Day", item.Title);
                Assert.Equal("2026-01-01", item.Description);
            },
            item =>
            {
                Assert.Equal("Independence Day", item.Title);
                Assert.Equal("2026-07-04", item.Description);
            });
    }

    [Fact]
    public async Task Nagerholidays_fetcher_returns_nothing_for_unknown_country()
    {
        var handler = new StubHandler(_ => StubHandler.Json("{}"));
        var client = new NagerholidaysClient(new HttpClient(handler));
        var items = await client.FetchAsync(new AggregationQuery { Country = "XX" }, CancellationToken.None);
        Assert.Empty(items);
    }

    [Fact]
    public async Task Nagerholidays_fetcher_returns_nothing_for_invalid_country()
    {
        var handler = new StubHandler(_ => StubHandler.Json("{}"));
        var client = new NagerholidaysClient(new HttpClient(handler));
        var items = await client.FetchAsync(new AggregationQuery { Country = "XX" }, CancellationToken.None);
        Assert.Empty(items);
    }

    [Fact]
    public async Task Frankfurter_fetcher_returns_expected_items()
    {
        var handler = new StubHandler(_ => StubHandler.Json("""
            {
                "rates": {
                    "USD": 1.0,
                    "EUR": 0.85
                }
            }
        """));
        var client = new FrankfurterClient(new HttpClient(handler));
        var items = await client.FetchAsync(new AggregationQuery { Keyword = "USD" }, CancellationToken.None);
        Assert.NotEmpty(items);
    }

    [Fact]
    public async Task Frankfurter_fetcher_returns_nothing_for_invalid_currencies()
    {
        var handler = new StubHandler(_ => StubHandler.Json("{}"));
        var client = new FrankfurterClient(new HttpClient(handler));
        var items = await client.FetchAsync(new AggregationQuery { Keyword  = "XXX" }, CancellationToken.None);
        Assert.Empty(items);
    }

    [Fact]
    public async Task OpenMeteo_geocodes_then_fetches_current_weather()
    {
        var handler = new StubHandler(request => request.RequestUri!.Host.StartsWith("geocoding")
            ? StubHandler.Json("""{"results":[{"name":"Athens","latitude":37.98,"longitude":23.72}]}""")
            : StubHandler.Json("""{"current":{"time":"2026-11-09T10:00","temperature_2m":18.5,"wind_speed_10m":12.0,"relative_humidity_2m":60}}"""));
        var client = new OpenMeteoClient(new HttpClient(handler));

        var item = Assert.Single(await client.FetchAsync(new AggregationQuery { City = "Athens" }, CancellationToken.None));

        Assert.Equal("Current weather in Athens", item.Title);
        Assert.Contains("18.5°C", item.Description);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("latitude=37.98", handler.Requests[1].Query);
    }

    [Fact]
    public async Task OpenMeteo_returns_nothing_for_unknown_city()
    {
        var handler = new StubHandler(_ => StubHandler.Json("{}"));
        var client = new OpenMeteoClient(new HttpClient(handler));

        var items = await client.FetchAsync(new AggregationQuery { City = "Nowhereville" }, CancellationToken.None);

        Assert.Empty(items);
    }

}
