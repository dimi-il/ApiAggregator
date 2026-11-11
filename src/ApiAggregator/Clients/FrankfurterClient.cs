using ApiAggregator.Models;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace ApiAggregator.Clients
{
    public sealed class FrankfurterClient(HttpClient http) : IExternalApiClient
    {
        public string Name => "Frankfurter";
        public string Category => "Currency";

        public string CacheKey(AggregationQuery query) => $"openFrankfurter:{string.Join(",", query.Country)}";

        public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(AggregationQuery query, CancellationToken cancellationToken)
        {

            try
            {
                var url = $"https://api.frankfurter.dev/v2/rates?base={Uri.EscapeDataString(string.Join(",", query.Keyword))}";
                var response = await http.GetFromJsonAsync<IReadOnlyList<SearchResponse>>(url, cancellationToken);

                var data = response?? throw new Exception("No data returned from Frankfurter API.");

                var i = 0;

                return data.Select(r => new AggregatedItem(
                        Name,
                        Category,
                        r.@base,
                        $"The currency rate for {r.Quote} is {r.Rate}",
                        "https://frankfurter.dev",
                        r.Dates,
                        Relevance.FromRank(i, response.Count)))
                    .ToList();

            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching data from Frankfurter API: {ex.Message}", ex);
            }
        }

        private sealed record SearchResponse(
            [property: JsonPropertyName("date")] DateTime  Dates,
            [property: JsonPropertyName("base")] string @base,
            [property: JsonPropertyName("quote")] string? Quote,
            [property: JsonPropertyName("rate")] decimal  Rate );
    }
}
