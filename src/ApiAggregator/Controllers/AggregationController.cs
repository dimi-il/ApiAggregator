using ApiAggregator.Models;
using ApiAggregator.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;

namespace ApiAggregator.Controllers;

[ApiController]
[Route("api/aggregate")]

public sealed class AggregationController(IAggregationService aggregation) : ControllerBase
{
    /// <summary>Returns data aggregated from all external APIs, filtered and sorted.</summary>
    /// <param name="q">Keyword for Holidays and Currency country (default "").</param>
    /// <param name="city">City for weather (default "Athens").</param>
    /// <param name="sortBy">Date (default), Relevance or Title.</param>
    /// <param name="order">Asc or Desc (default).</param>
    [HttpGet]
    [ProducesResponseType(typeof(AggregationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AggregationResult>> Get(
        //[FromQuery] string? q,
        [FromQuery] string? city,
        [FromQuery] string? symbol,
        [FromQuery] string? countrycode,
        [FromQuery] int? year,
        [FromQuery] SortField sortBy = SortField.Date,
        [FromQuery] SortDirection order = SortDirection.Desc,
        CancellationToken cancellationToken = default)
    {
        /// before year 2024 is invalid selection for Nager holiday and cause error 500 
       
   
        var query = new AggregationQuery
        {
            City =  string.IsNullOrEmpty(city) ? "Athens" : city.Trim(),
            Symbol = string.IsNullOrEmpty(symbol) ? "EUR" : symbol.Trim(),
            CountryIsoCode = string.IsNullOrEmpty(countrycode) ? "GR" : countrycode.Trim(),
            Year = year ?? DateTime.Now.Year,
            SortBy = sortBy,
            Order = order
        };

        return Ok(await aggregation.AggregateAsync(query, cancellationToken));
    }
}
