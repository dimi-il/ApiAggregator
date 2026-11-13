using ApiAggregator.Models;
using System.Linq.Expressions;
using System.Numerics;
using System.Text.Json.Serialization;


namespace ApiAggregator.Clients
{
    public sealed class FrankfurterClient(HttpClient http) : IExternalApiClient
    {
        public string Name => "Frankfurter";
        public string Category => "Currencies";

        public string CacheKey(AggregationQuery query) => $"frankfurter:{query.Symbol}:{DateTime.Now.AddMinutes(15)}";

        // null = latest rates; a past year = 31 December of that year (the API falls back to the last working day).
        

        public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(AggregationQuery query, CancellationToken cancellationToken)
        {

            var url = $"https://api.frankfurter.dev/v2/rates?base={query.Symbol}";
            var response = await http.GetFromJsonAsync<IReadOnlyList<SearchResponse>>(url, cancellationToken);
            var data = response ?? throw new Exception("No data returned from Frankfurter API.");

            var i = 0;

            return data.Select(r => new AggregatedItem(
                    Name,
                    Category,
                    $"{r.BaseCurrency}" ,
                    $"Todays rate is 1 {r.Quote} = {r.Rate} {r.BaseCurrency}",
                    "https://api.frankfurter.dev",
                    r.Dates,
                    Relevance.FromRank(i, response.Count)))
                .ToList();
        }

        
        private sealed record SearchResponse(
            [property: JsonPropertyName("date")] DateTime Dates,
            [property: JsonPropertyName("base")] string BaseCurrency,
            [property: JsonPropertyName("quote")] string? Quote,
            [property: JsonPropertyName("rate")] decimal  Rate );


    }
}
