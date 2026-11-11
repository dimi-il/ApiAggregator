using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ApiAggregator.Models;

namespace ApiAggregator.Clients
{
    public sealed class NagerholidaysClient(HttpClient http) : IExternalApiClient
    {
        public string Name => "Nagerholidays";
        public string Category => "Holidays";

        public string CacheKey(AggregationQuery query) => $"openNager:{query.Country.Trim().ToLowerInvariant()}";

        public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(AggregationQuery query, CancellationToken cancellationToken)
        {
            var url = $"https://nagerholidays.com/api/v4/Holidays/{query.Country.Trim()}/{Uri.EscapeDataString(DateTime.Now.Year.ToString())}";
            var response = await http.GetFromJsonAsync<IReadOnlyList<SearchResponse>>(url, cancellationToken);
            var i = 0;
            return response.Select(r => new AggregatedItem(
                    Name,
                    Category,
                    r.CountryCode,
                    $" The {r.Name} is for country with code {r.CountryCode} a day of on {r.Dates}",
                    url,
                    r.Dates,
                    Relevance.FromRank( i++, response.Count))).ToList();
        }

        private sealed record SearchResponse(
            [property: JsonPropertyName("date")] DateTime Dates,
            [property: JsonPropertyName("name")] string Name,
            [property: JsonPropertyName("countryCode")] string CountryCode,
            [property: JsonPropertyName("nationalHoliday")] bool NationalHoliday,
            [property: JsonPropertyName("subdivisionCodes")] string? SubdivisionCodes,
            [property: JsonPropertyName("holidayTypes")] IReadOnlyList<string> HolidayTypes);
        
   
    }
}
