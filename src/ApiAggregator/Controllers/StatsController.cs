using ApiAggregator.Statistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiAggregator.Controllers;

[ApiController]
[Route("api/stats")]

public sealed class StatsController(IApiStatsStore store) : ControllerBase
{
    /// <summary>Per external API: total requests, average response time and fast/average/slow buckets.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApiStatsSnapshot>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ApiStatsSnapshot>> Get() => Ok(store.GetSnapshot());
}
