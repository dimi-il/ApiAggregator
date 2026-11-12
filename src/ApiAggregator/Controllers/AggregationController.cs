using ApiAggregator.Models;
using ApiAggregator.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiAggregator.Controllers;

[ApiController]
[Route("api/aggregate")]

public sealed class AggregationController(IAggregationService aggregation) : ControllerBase
{
    /// <summary>Returns data aggregated from all external APIs, filtered and sorted.</summary>
    /// <param name="q">Keyword for news and repositories (default "dotnet").</param>
    /// <param name="city">City for weather (default "Athens").</param>
    /// <param name="sources">Comma separated source names, e.g. "GitHub,HackerNews".</param>
    /// <param name="category">Weather, News or Repositories.</param>
    /// <param name="dateFrom">Only items on/after this date (ISO 8601).</param>
    /// <param name="dateTo">Only items on/before this date (ISO 8601).</param>
    /// <param name="sortBy">Date (default), Relevance or Title.</param>
    /// <param name="order">Asc or Desc (default).</param>
    [HttpGet]
    [ProducesResponseType(typeof(AggregationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AggregationResult>> Get(
        [FromQuery] string? q,
        [FromQuery] string? city,
        [FromQuery] string? sources,
        [FromQuery] string? category,
        [FromQuery(Name = "from")] DateTimeOffset? dateFrom,
        [FromQuery(Name = "to")] DateTimeOffset? dateTo,
        [FromQuery] SortField sortBy = SortField.Date,
        [FromQuery] SortDirection order = SortDirection.Desc,
        CancellationToken cancellationToken = default)
    {
        if (dateFrom is not null && dateTo is not null && dateFrom > dateTo)
            return BadRequest(new ProblemDetails { Title = "'from' must not be later than 'to'." });

        var query = new AggregationQuery
        {
            Keyword = string.IsNullOrWhiteSpace(q) ? "EUR" : q.Trim(),
            City = string.IsNullOrWhiteSpace(city) ? "Athens" : city.Trim(),
            Sources = (sources ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Category = category,
            From = dateFrom,
            To = dateTo,
            SortBy = sortBy,
            Order = order
        };

        return Ok(await aggregation.AggregateAsync(query, cancellationToken));
    }
}
