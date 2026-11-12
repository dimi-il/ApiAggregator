using ApiAggregator.Models;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Numerics;
using System.Text.Json.Serialization;

namespace ApiAggregator.Clients
{
    public sealed class NagerholidaysClient(HttpClient http) : IExternalApiClient
    {
        public string Name => "Nagerholidays";
        public string Category => "Holidays";

        public string CacheKey(AggregationQuery query) => $"openNager:{query.Keyword}";

        public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(AggregationQuery query, CancellationToken cancellationToken)
        {


            var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query.City)}&count=1";
            var geo = await http.GetFromJsonAsync<CityResponse>(geoUrl, cancellationToken);
            var place = geo?.Results?.FirstOrDefault();
            if (place is null) return Array.Empty<AggregatedItem>();

            var url = $"https://nagerholidays.com/api/v4/Holidays/{place.Country_code}/{Uri.EscapeDataString(DateTime.Now.Year.ToString())}";
            var items = await http.GetFromJsonAsync<IReadOnlyList<SearchResponse>>(url, cancellationToken);
            if (items is null) return Array.Empty<AggregatedItem>();

            int i = 0;

            return items.Select((r) => new AggregatedItem(
                    Name,
                    Category,
                    $"National holidays of {r.CountryCode}",
                    $" The {r.Name} is a national Day of {place.Country} at {r.Dates.ToString("yyyy/MM/dd") }",
                    "https://nagerholidays.com",
                    r.Dates,
                    Relevance.FromRank(i++, items.Count))).ToList();
        }


        private sealed record SearchResponse(
            [property: JsonPropertyName("date")] DateTime Dates,
            [property: JsonPropertyName("name")] string Name,
            [property: JsonPropertyName("countryCode")] string CountryCode,
            [property: JsonPropertyName("nationalHoliday")] bool NationalHoliday,
            [property: JsonPropertyName("subdivisionCodes")] IReadOnlyList<string> SubdivisionCodes,
            [property: JsonPropertyName("holidayTypes")] IReadOnlyList<string> HolidayTypes);


        private sealed record CityResponse([property: JsonPropertyName("results")] List<CityPlace>? Results);
        private sealed record CityPlace(
            [property: JsonPropertyName("country")] string Country,
            [property: JsonPropertyName("country_code")] string Country_code);
    }
}
