# API Aggregator

This project is an ASP.NET Core API that combines several public data sources into one unified response. Instead of calling each provider separately from different clients, the application exposes a single endpoint that fetches data in parallel, merges the results, filters them, and returns a clean sorted list.

The current implementation aggregates:

- Open-Meteo: current weather for a city
- Nager.date / Nagerholidays: public holidays for a country in the current year
- Frankfurter: exchange-rate information for a base currency

The service is built to be easy to extend: new APIs can be added by implementing the `IExternalApiClient` contract and registering the client in `Program.cs`.

## What the API does

The main purpose of the API is to collect useful information from multiple external APIs and present it in one place.

Example use cases:

- Get weather and public holidays for a selected country in a single request
- Query multiple data sources with one URL and a single response model
- Filter by category, date range, and sorting preference
- Handle partial failures without breaking the whole request

## How it works

The flow is straightforward:

1. A client sends a request to `/api/aggregate`.
2. The controller validates basic query parameters and creates an `AggregationQuery`.
3. `AggregationService` selects only the clients that match the requested sources and category.
4. Each selected client runs in parallel using `Task.WhenAll`.
5. Every client calls its own upstream API using `HttpClient`.
6. Each response is normalized into the same `AggregatedItem` shape.
7. Results are filtered by date/category and sorted by title, relevance, or date.
8. The final response includes both the merged items and a per-source status summary.

## Architecture

The project is organized in a few focused layers:

- Controllers: receive HTTP requests
- Services: orchestrate the aggregation logic
- Clients: connect to external APIs
- Models: shared request/response records
- Statistics: track API response time and failure counts
- Background jobs: detect slow or abnormal response patterns

### Core flow

```text
HTTP request
   -> AggregationController
   -> AggregationService
      -> IExternalApiClient implementations
         -> OpenMeteo / Nagerholidays / Frankfurter
      -> filter + sort + merge results
   -> JSON response
```

### Important behaviors

- Parallel execution: all selected sources are fetched at the same time to minimize latency.
- Cache support: fresh responses are kept in memory for a short period, and stale good data is kept as a fallback.
- Fallback handling: if one API fails, the request still succeeds using the remaining sources; the failed source is marked as `Failed` or `Stale`.
- Response telemetry: each API call records timing and success/failure counters.
- Continuous monitoring: a background service checks whether a source has suddenly become much slower than its normal baseline.

## API endpoints

### GET /api/aggregate

Returns aggregated data from the active sources.

Query parameters:

- `q`: keyword used by some data sources (default: `dotnet`)
- `city`: city name for weather lookup (default: `Athens`)
- `country`: country code for holiday lookup (default: `GR`)
- `sources`: comma-separated source names, for example `OpenMeteo,Frankfurter`
- `category`: optional filter such as `Weather`, `Currency`, or `Holidays`
- `from`: lower bound for item date
- `to`: upper bound for item date
- `sortBy`: `Date`, `Relevance`, or `Title`
- `order`: `Asc` or `Desc`

Example request:

```http
GET /api/aggregate?city=London&country=GB&sortBy=Date&order=Desc
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

Source status values:

- `Ok`: data was fetched live
- `Cached`: data came from the in-memory cache
- `Stale`: the API failed but the last known-good response was returned
- `Failed`: the API failed and no cached fallback existed

### GET /api/stats

Returns response-time and failure statistics for each external API.

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

## External API clients

The app registers integrations through `AddExternalApiClient<TClient>()` in `Program.cs`.

### OpenMeteo client

- Uses geocoding to resolve a city name
- Calls Open-Meteo forecast API for the current temperature, wind, and humidity
- Maps the result to a single weather item

### Nagerholidays client

- Calls the Nagerholidays API using the country code and current year
- Maps holiday records to normalized items with title, date, and description

### Frankfurter client

- Calls the Frankfurter API for exchange-rate data
- Uses the supplied base currency value to fetch a rate
- Creates a normalized item for the returned rate data

## How the code is structured

Key files:

- `src/ApiAggregator/Program.cs`: registers services and starts the app
- `src/ApiAggregator/Controllers/AggregationController.cs`: exposes the aggregate endpoint
- `src/ApiAggregator/Controllers/StatsController.cs`: exposes the stats endpoint
- `src/ApiAggregator/Services/AggregationService.cs`: orchestrates the call flow
- `src/ApiAggregator/Clients/*`: each external API integration
- `src/ApiAggregator/Models/AggregationModels.cs`: shared request and response models
- `src/ApiAggregator/Statistics/ApiStatsStore.cs`: in-memory statistics storage
- `src/ApiAggregator/Background/PerformanceAnomalyService.cs`: checks for slow API trends

## Configuration

The application uses `src/ApiAggregator/appsettings.json` for runtime settings such as cache lifetime and anomaly detection thresholds.

Important settings:

- `Aggregation:CacheDuration`: how long successful responses stay in the fresh cache
- `Aggregation:StaleDuration`: how long the last successful response is kept as fallback
- `Anomaly:CheckInterval`: how often the background monitor runs
- `Anomaly:Window`: time window used for anomaly comparison
- `Anomaly:ThresholdRatio`: threshold for identifying unusually slow behavior
- `Anomaly:MinSamples`: minimum samples required before flagging an anomaly

## Running the project

```bash
dotnet restore
dotnet test
dotnet run --project src/ApiAggregator
```

Then open Swagger in the browser, usually at:

```text
http://localhost:5000/swagger
```

The exact port may vary depending on your local environment. Swagger will show the available endpoints and request examples.

## Notes

This project is intentionally designed around a simple in-memory architecture. It is ideal for a local API aggregator or a small service where low operational complexity matters more than distributed scale. For production use at larger scale, you would typically move caching and metrics to a shared external store such as Redis or a monitoring backend.
