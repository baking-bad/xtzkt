using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Chains;

[ApiController]
[Tags("Chains")]
[Route("v1/protocols")]
[Produces("application/json")]
public class ProtocolsController(ProtocolRepository _protocols, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get protocols
    /// </summary>
    /// <remarks>
    /// Returns protocols with the level range they were active for and the constants they introduced —
    /// cycle length, gas and storage limits, reward parameters, and so on. On Tezos X a "protocol"
    /// is a kernel version, identified by its root hash.
    ///
    /// Constants change from protocol to protocol, so read them from the protocol a block belongs to
    /// rather than hardcoding values or assuming the current ones held in the past.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Protocol>>> Get(ProtocolFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _protocols.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _protocols.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get protocols count
    /// </summary>
    /// <remarks>
    /// Returns the number of protocols matching the filters — the same ones accepted by `/v1/protocols`.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(ProtocolFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _protocols.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
