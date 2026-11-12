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

        public string CacheKey(AggregationQuery query) => $"openFrankfurter:{query.Keyword}";

        public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(AggregationQuery query, CancellationToken cancellationToken)
        {
        
            var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query.City)}&count=1";
            var geoCountry = await http.GetFromJsonAsync<CountryResponse>(geoUrl, cancellationToken);
            var country = geoCountry?.Results?.FirstOrDefault();
            if (country is null) return Array.Empty<AggregatedItem>();

            var cnUrl = $"https://countries.dev/alpha/{Uri.EscapeDataString(country.Country_code)}";
            var countrycode = await http.GetFromJsonAsync<CityResponse>(cnUrl, cancellationToken);
            var code = countrycode?.Results?.FirstOrDefault();
            if (code is null) return Array.Empty<AggregatedItem>();


            String url = $"https://api.frankfurter.dev/v2/rates?";

            if (query.From.HasValue)
            {
                url = String.Format("{0}date={1}", url, Uri.EscapeDataString(query.From.Value.ToString("yyyy-MM-dd")));
            }
            else if (query.To.HasValue)
            {
                url = String.Format("{0}date={1}", url, Uri.EscapeDataString(query.To.Value.ToString("yyyy-MM-dd")));
            }
            else if (query.Keyword is not null) {
                url = String.Format("{0}base={1}", url, Uri.EscapeDataString(code.code));
            } ;

            var response = await http.GetFromJsonAsync<IReadOnlyList<SearchResponse>>(url, cancellationToken);
            var data = response?? throw new Exception("No data returned from Frankfurter API.");

            var i = 0;

            return data.Select(r => new AggregatedItem(
                    Name,
                    Category,
                    $"{code.code} currency of {country.Country}" ,
                    $"Todays rate between {r.Quote}/{code.code}  is 1/{r.Rate}",
                    "https://api.frankfurter.dev",
                    r.Dates,
                    Relevance.FromRank(i, response.Count)))
                .ToList();
        }

        private sealed record SearchResponse(
            [property: JsonPropertyName("date")] DateTime Dates,
            [property: JsonPropertyName("base")] string @base,
            [property: JsonPropertyName("quote")] string? Quote,
            [property: JsonPropertyName("rate")] decimal  Rate );

        private sealed record CountryResponse([property: JsonPropertyName("results")] List<CountryPlace>? Results);
        private sealed record CityResponse([property: JsonPropertyName("currencies")] List<Currencies>? Results);

        private sealed record CountryPlace(
            [property: JsonPropertyName("country")] string Country,
            [property: JsonPropertyName("country_code")] string Country_code);
        private sealed record Currencies(
            [property: JsonPropertyName("code")] string code,
            [property: JsonPropertyName("name")] string name)
        ;

    }
}
