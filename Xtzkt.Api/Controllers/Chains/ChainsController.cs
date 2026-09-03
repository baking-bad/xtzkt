using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Responses;

namespace Xtzkt.Api.Controllers.Chains;

[ApiController]
[Tags("Chains")]
[Route("v1/chains")]
[Produces("application/json")]
public class ChainsController(ChainRepository _chains) : ControllerBase
{
    /// <summary>
    /// Get chains
    /// </summary>
    /// <remarks>
    /// Returns the chains this instance indexes, together with their sync state — the last indexed block
    /// and the last block known to the node. Comparing `level` with `knownLevel` tells you whether
    /// the indexer is caught up.
    ///
    /// Start here to discover which `chain` values the other endpoints accept, and to find the current
    /// head level before querying anything level-based.
    /// </remarks>
    [HttpGet]
    public ActionResult<IEnumerable<Chain>> Get(ChainFilter filter, Pagination pagination, Selection selection)
    {
        return Ok(selection.Select == null
            ? _chains.Get(filter, pagination)
            : new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = _chains.Get(filter, pagination, selection)
            });
    }

    /// <summary>
    /// Get chains count
    /// </summary>
    /// <remarks>
    /// Returns the number of chains matching the filters — the same ones accepted by `/v1/chains`.
    /// </remarks>
    [HttpGet("count")]
    public ActionResult<long> GetCount(ChainFilter filter)
    {
        return _chains.Count(filter);
    }
}
