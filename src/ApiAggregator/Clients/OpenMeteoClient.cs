using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ApiAggregator.Models;

namespace ApiAggregator.Clients;

/// <summary>Weather from Open-Meteo (free, no API key). Two calls: geocoding, then forecast.</summary>
public sealed class OpenMeteoClient(HttpClient http) : IExternalApiClient
{
    public string Name => "OpenMeteo";
    public string Category => "Weather";

    public string CacheKey(AggregationQuery query) => $"openmeteo:{query.City.Trim().ToLowerInvariant()}";

    public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(AggregationQuery query, CancellationToken cancellationToken)
    {
        var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query.City)}&count=1";
        var geo = await http.GetFromJsonAsync<GeoResponse>(geoUrl, cancellationToken);
        var place = geo?.Results?.FirstOrDefault();
        if (place is null) return Array.Empty<AggregatedItem>();

        var forecastUrl = FormattableString.Invariant(
            $"https://api.open-meteo.com/v1/forecast?latitude={place.Latitude}&longitude={place.Longitude}&current=temperature_2m,wind_speed_10m,relative_humidity_2m");
        var forecast = await http.GetFromJsonAsync<ForecastResponse>(forecastUrl, cancellationToken);
        var current = forecast?.Current;
        if (current is null) return Array.Empty<AggregatedItem>();

        var date = DateTimeOffset.TryParse(current.Time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : DateTimeOffset.UtcNow;

        var description = FormattableString.Invariant(
            $"{current.Temperature:0.#}°C, wind {current.WindSpeed:0.#} km/h, humidity {current.Humidity:0.#}%");

        return new[]
        {
            new AggregatedItem(Name, Category, $"Current weather in {place.Name}", description,
                "https://open-meteo.com/", date, 1.0)
        };
    }

    private sealed record GeoResponse([property: JsonPropertyName("results")] List<GeoPlace>? Results);

    private sealed record GeoPlace(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude);

    private sealed record ForecastResponse([property: JsonPropertyName("current")] CurrentWeather? Current);

    private sealed record CurrentWeather(
        [property: JsonPropertyName("time")] string? Time,
        [property: JsonPropertyName("temperature_2m")] double? Temperature,
        [property: JsonPropertyName("wind_speed_10m")] double? WindSpeed,
        [property: JsonPropertyName("relative_humidity_2m")] double? Humidity);
}
