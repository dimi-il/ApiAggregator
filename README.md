# ApiAggregator

ApiAggregator is an ASP.NET Core 8 web API that combines multiple public data sources into a single, normalized response. It fetches data from several upstream APIs in parallel, applies optional filters and sorting, and returns a unified result model that is easy for client applications to consume.

The project currently pulls data from:

- Open-Meteo for weather information
- Nagerholidays for public holidays
- Frankfurter for currency exchange rates

It is designed to be easily extensible: new providers can be added by implementing the `IExternalApiClient` contract and registering them in `Program.cs`.

## Features

- Aggregates multiple external APIs through one endpoint
- Runs selected providers in parallel using `Task.WhenAll`
- Normalizes all responses into a shared `AggregatedItem` model
- Supports filtering by category, date range, and selected sources
- Sorts results by title, relevance, or date
- Provides source-level status reporting for partial failures
- Uses an in-memory cache and stale fallback mechanism for resilience
- Tracks latency and error statistics per external API
- Detects abnormal performance spikes via a background anomaly detector
- Includes Swagger UI for local testing and API exploration

## Architecture overview

The application follows a lightweight layered design:

- `Controllers` handle HTTP requests
- `Services` coordinate aggregation logic
- `Clients` integrate with external providers
- `Models` define shared query and response contracts
- `Statistics` track request metrics and slow/average/fast buckets
- `Background` monitors service health and performance anomalies

Core request flow:

```text
HTTP request
  -> AggregationController
  -> AggregationService
  -> matching IExternalApiClient implementations
  -> parallel upstream calls
  -> normalization, filtering, sorting
  -> AggregationResult JSON response
```

## Supported API sources

### OpenMeteo
- Resolves a city name and fetches current weather conditions
- Maps forecast data into a single weather item

### Nagerholidays
- Calls the public holiday API for the current year and country code
- Converts results into normalized holiday items

### Frankfurter
- Fetches exchange-rate data for a specified base currency
- Produces a normalized currency item from the upstream payload

## API endpoints

### GET /api/aggregate

Returns aggregated data from the configured external sources.

Query parameters:

- `q`: keyword used by some providers
- `city`: city name for weather lookup (default: `Athens`)
- `sources`: comma-separated source names, for example `OpenMeteo,Frankfurter`
- `category`: optional filter such as `Weather`, `Currency`, or `Holidays`
- `from`: lower bound for item date (ISO 8601)
- `to`: upper bound for item date (ISO 8601)
- `sortBy`: `Date`, `Relevance`, or `Title`
- `order`: `Asc` or `Desc`

Example:

```http
GET /api/aggregate?city=London&sources=OpenMeteo,Frankfurter&sortBy=Date&order=Desc
```

Example response:

```json
{
  "items": [
    {
      "source": "OpenMeteo",
      "category": "Weather",
      "title": "Current weather in London",
      "description": "12.4°C, wind 8.2 km/h, humidity 71.0%",
      "url": "https://open-meteo.com/",
      "date": "2026-11-11T11:00:00+00:00",
      "relevance": 1.0
    }
  ],
  "sources": [
    {
      "source": "OpenMeteo",
      "state": "Ok",
      "elapsedMs": 280,
      "error": null
    }
  ],
  "totalCount": 1
}
```

Possible source states:

- `Ok`: fetched live from the source
- `Cached`: served from memory cache
- `Stale`: source failed but valid fallback data was returned
- `Failed`: source failed and no fallback was available

### GET /api/stats

Returns per-source statistics including total requests, failure counts, average response time, and performance buckets.

Example response:

```json
[
  {
    "api": "OpenMeteo",
    "totalRequests": 24,
    "failedRequests": 2,
    "averageResponseMs": 148.3,
    "buckets": {
      "fast": { "count": 12, "averageResponseMs": 72.5 },
      "average": { "count": 8, "averageResponseMs": 130.4 },
      "slow": { "count": 4, "averageResponseMs": 260.1 }
    }
  }
]
```

## Getting started

### Prerequisites

- .NET 8 SDK
- A code editor such as Visual Studio or VS Code

### Restore and run

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/ApiAggregator/ApiAggregator.csproj
```

Then open the application in a browser. Swagger UI is configured to show at the site root, so you can usually access it at:

```text
http://localhost:<port>/
```

If you run the project in default local settings, Swagger should be available on a localhost URL similar to:

```text
http://localhost:5000/
```

## Configuration

Runtime settings are defined in `src/ApiAggregator/appsettings.json`.

Key settings:

- `Aggregation:CacheDuration`: how long fresh responses remain cached
- `Aggregation:StaleDuration`: how long stale responses are retained as fallback
- `Anomaly:CheckInterval`: interval for the performance monitor
- `Anomaly:Window`: evaluation window for slow-response detection
- `Anomaly:ThresholdRatio`: ratio above which a response time is considered anomalous
- `Anomaly:MinSamples`: minimum samples before anomaly detection triggers

## Project structure

```text
ApiAggregator/
├── src/
│   └── ApiAggregator/
│       ├── Background/
│       ├── Clients/
│       ├── Controllers/
│       ├── Infrastructure/
│       ├── Models/
│       ├── Services/
│       ├── Statistics/
│       ├── Program.cs
│       ├── appsettings.json
│       └── ApiAggregator.csproj
├── tests/
│   └── ApiAggregator.Tests/
├── ApiAggregator.sln
├── LICENSE
├── README.md
└── API_Aggregator_Assignment_II.docx
```

## Extending the app

To add a new provider:

1. Create a new client class implementing `IExternalApiClient`
2. Implement `Name`, `Category`, `CacheKey`, and `FetchAsync`
3. Register the client in `Program.cs` using `AddExternalApiClient<TClient>()`
4. Ensure the provider returns data shaped as `AggregatedItem`

This makes the new integration participate automatically in:

- parallel fetching
- caching
- fallback handling
- source stats collection
- sorting and filtering

## Notes

This project intentionally uses a straightforward in-memory design for simplicity and local experimentation. It is a strong fit for a small internal service or prototype. For production-scale deployments, you would typically replace the in-memory stores with shared caching, monitoring, and persistence layers.

## License

This project is provided under the MIT license. See the `LICENSE` file for details.
