# ApiAggregator

ApiAggregator is an ASP.NET Core 8 Web API that combines several public data sources into one unified response. The application fetches weather, public holiday, and exchange-rate data in parallel, normalizes the results, and returns a single aggregated JSON payload that is easier for client applications to consume.

## What this project does

The service currently integrates with:

- Open-Meteo: weather data for a city
- Nager.Date / Nagerholidays: public holidays for a country and year
- Frankfurter: currency exchange-rate data for a base symbol

It exposes a single aggregation endpoint and records per-source health and performance metrics.

## Key features

- Aggregates multiple APIs into a single response model
- Executes provider calls in parallel
- Normalizes responses into a shared `AggregatedItem` structure
- Supports sort options by date, title, or relevance
- Uses in-memory caching and stale fallback handling
- Tracks request statistics per API
- Detects abnormal slowdowns with a background anomaly detector
- Exposes Swagger UI for manual API exploration

## Architecture

The project is organized into a few focused layers:

- `Controllers`: HTTP endpoints
- `Services`: orchestration and aggregation logic
- `Clients`: external provider integrations
- `Models`: request and response payloads
- `Statistics`: metrics and per-source performance tracking
- `Background`: monitoring for performance anomalies

## API endpoints

### GET /api/aggregate

Returns a combined result from the configured external APIs.

Query parameters:

- `city`: weather lookup city (default: `Athens`)
- `symbol`: base currency symbol (default: `EUR`)
- `countrycode`: holiday country ISO code (default: `GR`)
- `year`: target year for holiday data (default: current year)
- `sortBy`: `Date`, `Relevance`, or `Title`
- `order`: `Asc` or `Desc`

Example:

```http
GET /api/aggregate?city=London&symbol=USD&countrycode=GB&year=2025&sortBy=Date&order=Desc
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

Possible source states are:

- `Ok`
- `Cached`
- `Stale`
- `Failed`

### GET /api/stats

Returns aggregated runtime statistics and performance buckets for each API source.

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
- Visual Studio 2022, VS Code, or another C# editor

### Restore, build, and run

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/ApiAggregator/ApiAggregator.csproj
```

Once the app is running, open Swagger in the browser at:

```text
http://localhost:<port>/
```

The project configures Swagger at the root path, so the API can usually be explored directly from the local startup URL.

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
├── .gitignore
└── API_Aggregator_Assignment_II.docx
```

## Configuration

Application settings are stored in `src/ApiAggregator/appsettings.json`.

The main configuration sections include:

- `Aggregation`: cache and fallback durations
- `Anomaly`: detection thresholds and window settings

## Extending the project

To add a new provider:

1. Create a client class implementing the API contract used by the project
2. Map the provider data to the common `AggregatedItem` model
3. Register the client in `Program.cs`
4. Ensure the new provider participates in the existing aggregation flow

This design makes it easy to add additional external sources without changing the API contract for consumers.

## License

This project is licensed under the MIT License. See the `LICENSE` file for details.
