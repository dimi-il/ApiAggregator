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

        public string CacheKey(AggregationQuery query) => $"nager:{query.CountryIsoCode}:{query.Year}";

        public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(AggregationQuery query, CancellationToken cancellationToken)
        {
            var url = $"https://nagerholidays.com/api/v4/Holidays/{query.CountryIsoCode}/{query.Year}";
           
            var items = await http.GetFromJsonAsync<List<SearchResponse>>(url, cancellationToken);
            if (items is null) return Array.Empty<AggregatedItem>();

            int i = 0;

            return items.Select((r) => new AggregatedItem(
                        Name,
                        Category,
                        $"National holidays of {r.CountryCode}",
                        $" The {r.Name} is a national Day of {query.CountryIsoCode} at {r.Dates.ToString("yyyy/MM/dd")}",
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

    }
}
