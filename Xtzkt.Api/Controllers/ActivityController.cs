using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models.Abstract;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers;

[ApiController]
[Tags("Activity")]
[Route("v1/activity")]
[Produces("application/json")]
public class ActivityController(ActivityRepository _activity, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get account activity
    /// </summary>
    /// <remarks>
    /// Returns everything the specified account took part in — operations, token, ticket and bridge ticket transfers —
    /// merged into a single stream sorted by `id`, so an account history can be rendered from one request.
    /// Each item carries an `activity` field telling you which kind it is, and which model to expect.
    ///
    /// By default the account is matched in any role (sender, target, initiator, or just mentioned)
    /// and noisy types such as attestations are left out. Use `roles` and `types` to change that.
    ///
    /// Prefer `cursor` over `offset` for paging — it stays fast no matter how deep into the history you go.
    /// </remarks>
    [HttpGet("account")]
    public async Task<ActionResult<IEnumerable<IActivity>>> GetByAccount(AccountActivityFilter filter, CursorPagination pagination)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _activity.Get(filter, pagination);

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get block activity
    /// </summary>
    /// <remarks>
    /// Returns everything that happened in the specified block — operations, token, ticket and bridge ticket transfers —
    /// merged into a single stream sorted by `id`, so a block can be rendered from one request.
    /// Each item carries an `activity` field telling you which kind it is, and which model to expect.
    ///
    /// The block is addressed by `level`. Since the same level exists on every indexed chain,
    /// add `chain` unless you really want all of them at once.
    /// </remarks>
    [HttpGet("block")]
    public async Task<ActionResult<IEnumerable<IActivity>>> GetByBlock(BlockActivityFilter filter, CursorPagination pagination)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _activity.Get(filter, pagination);

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get opg activity
    /// </summary>
    /// <remarks>
    /// Returns everything that happened under a single operation hash — all the operations of the group,
    /// their internal operations, and the token, ticket and bridge ticket transfers they caused — sorted by `id`.
    /// Each item carries an `activity` field telling you which kind it is, and which model to expect.
    ///
    /// This is what you want for an "operation details": one request gives you the whole picture,
    /// instead of querying each operation endpoint separately and stitching the results together.
    /// </remarks>
    [HttpGet("opg")]
    public async Task<ActionResult<IEnumerable<IOpgActivity>>> GetByOpg(OpgActivityFilter filter, CursorPagination pagination)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _activity.Get(filter, pagination);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
