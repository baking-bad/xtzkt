using System.Text.Json;
using System.Text.Json.Serialization;
using Xtzkt.Data.Models;
using Xtzkt.Services.Admin.Exceptions;
using Xtzkt.Services.Admin.Models;
using Xtzkt.Services.Admin.Services;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Services.Admin.Endpoints;

static class TicketBindingEndpoints
{
    #region static
    const int DefaultLimit = 100;
    const int MaxLimit = 10_000;
    const int TicketHashLength = 2 + 32 * 2;

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    #endregion

    public static async Task<IResult> GetAsync(StoreService store, CancellationToken ct, int offset = 0, int limit = DefaultLimit)
    {
        if (offset < 0)
            return Results.BadRequest("offset must be non-negative");

        if (limit < 1 || limit > MaxLimit)
            return Results.BadRequest($"limit must be between 1 and {MaxLimit}");

        var ticketBindings = await store.GetTicketBindingsAsync(offset, limit, ct);
        return Results.Json(ticketBindings.ConvertAll(ToBindingInfo), JsonOptions);
    }

    public static async Task<IResult> UpdateAsync(List<TicketBindingInfo?> ticketBindings, StoreService store, CancellationToken ct)
    {
        var hashes = new HashSet<string>(ticketBindings.Count);
        var tokenIndexes = new Dictionary<long, int>();
        var upserts = new List<TicketBinding>(ticketBindings.Count);
        var removes = new List<byte[]>();

        for (int i = 0; i < ticketBindings.Count; i++)
        {
            if (ticketBindings[i] is not TicketBindingInfo ticketBinding)
                return Results.BadRequest($"[{i}]: ticket binding is null");

            if (ticketBinding.TicketHash is not { Length: TicketHashLength } value ||
                !value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                !Hex.TryGetBytes(value, out var hash))
                return Results.BadRequest($"[{i}]: ticketHash must be a 0x-prefixed 32-byte hex string");

            var normalized = Hex.GetString(hash);
            if (!hashes.Add(normalized))
                return Results.BadRequest($"[{i}]: duplicate ticketHash {normalized}");

            if (ticketBinding.TokenId is not long tokenId)
            {
                removes.Add(hash);
                continue;
            }

            tokenIndexes.TryAdd(tokenId, i);
            upserts.Add(new TicketBinding { TicketHash = hash, TokenId = tokenId });
        }

        try
        {
            await store.UpdateTicketBindingsAsync(upserts, removes, ct);
            return Results.NoContent();
        }
        catch (TokenNotFoundException ex)
        {
            return Results.BadRequest($"[{tokenIndexes[ex.Id]}]: token {ex.Id} doesn't exist");
        }
    }

    static TicketBindingInfo ToBindingInfo(TicketBinding ticketBinding) => new()
    {
        TicketHash = Hex.GetString(ticketBinding.TicketHash),
        TokenId = ticketBinding.TokenId
    };
}
