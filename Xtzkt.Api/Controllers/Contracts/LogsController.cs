using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Contracts;

[ApiController]
[Tags("Contracts")]
[Route("v1/logs")]
[Produces("application/json")]
public class LogsController(LogRepository _logs, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get logs
    /// </summary>
    /// <remarks>
    /// Returns events emitted by contracts — EVM logs and Michelson events alike. The `runtime` field
    /// tells you which kind a log is, and therefore which extra fields it carries: `topics` and `data`
    /// for EVM, typed Micheline payload for Michelson.
    ///
    /// EVM logs are best filtered by `topic0` (the event signature hash) plus `address`.
    /// Michelson events are filtered by `name`.
    ///
    /// A decoded `name` and `payload` are provided when the event could be matched against a known ABI;
    /// `guessed=true` means the match was a guess, so treat those with care.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Log>>> Get(LogFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _logs.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _logs.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get logs count
    /// </summary>
    /// <remarks>
    /// Returns the number of logs matching the filters — the same ones accepted by `/v1/logs`.
    /// Handy for pagination controls.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(LogFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _logs.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
