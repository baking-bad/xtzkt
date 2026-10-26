using System.Text.Json;
using System.Text.Json.Serialization;
using Xtzkt.Data.Models;
using Xtzkt.Services.Admin.Exceptions;
using Xtzkt.Services.Admin.Models;
using Xtzkt.Services.Admin.Services;

namespace Xtzkt.Services.Admin.Endpoints;

static class AssetEndpoints
{
    #region static
    const int DefaultLimit = 100;
    const int MaxLimit = 10_000;
    const int MaxNameLength = 256;
    const int MaxSymbolLength = 64;
    const int MaxDescriptionLength = 1024;
    const int MaxTokens = 100;

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

        var assets = await store.GetAssetsAsync(offset, limit, ct);
        return Results.Json(assets.ConvertAll(ToInfo), JsonOptions);
    }

    public static async Task<IResult> UpdateAsync(List<AssetInfo?> assets, StoreService store, CancellationToken ct)
    {
        var indexes = new Dictionary<int, int>(assets.Count);
        var tokenIndexes = new Dictionary<long, int>();
        var creates = new List<Asset>();
        var updates = new List<Asset>();
        var removes = new List<int>();

        for (int i = 0; i < assets.Count; i++)
        {
            if (assets[i] is not AssetInfo asset)
                return Results.BadRequest($"[{i}]: asset is null");

            if (asset.Name != null && string.IsNullOrWhiteSpace(asset.Name))
                return Results.BadRequest($"[{i}]: name must be either null or non-empty");

            if (asset.Name?.Length > MaxNameLength)
                return Results.BadRequest($"[{i}]: name must not be longer than {MaxNameLength} characters");

            if (asset.Symbol != null && string.IsNullOrWhiteSpace(asset.Symbol))
                return Results.BadRequest($"[{i}]: symbol must be either null or non-empty");

            if (asset.Symbol?.Length > MaxSymbolLength)
                return Results.BadRequest($"[{i}]: symbol must not be longer than {MaxSymbolLength} characters");

            if (asset.Description?.Length > MaxDescriptionLength)
                return Results.BadRequest($"[{i}]: description must not be longer than {MaxDescriptionLength} characters");

            if (asset.Id is int id && !indexes.TryAdd(id, i))
                return Results.BadRequest($"[{i}]: duplicate id {id}");

            if (asset.Tokens == null)
            {
                if (asset.Id == null)
                    return Results.BadRequest($"[{i}]: tokens are required to create an asset");

                removes.Add(asset.Id.Value);
                continue;
            }

            if (asset.Tokens.Length == 0)
                return Results.BadRequest($"[{i}]: tokens must be either null or non-empty");

            if (asset.Tokens.Length > MaxTokens)
                return Results.BadRequest($"[{i}]: an asset must not have more than {MaxTokens} tokens");

            foreach (var token in asset.Tokens)
            {
                if (tokenIndexes.TryGetValue(token, out var j))
                    return Results.BadRequest(j == i
                        ? $"[{i}]: duplicate token {token}"
                        : $"[{i}]: token {token} is already in [{j}]");

                tokenIndexes.Add(token, i);
            }

            if (asset.Id == null)
                creates.Add(ToModel(asset, 0, asset.Tokens));
            else
                updates.Add(ToModel(asset, asset.Id.Value, asset.Tokens));
        }

        try
        {
            var created = await store.UpdateAssetsAsync(creates, updates, removes, ct);

            var ids = new int[assets.Count];
            for (int i = 0, k = 0; i < assets.Count; i++)
                ids[i] = assets[i]!.Id ?? created[k++];

            return Results.Ok(ids);
        }
        catch (AssetNotFoundException ex)
        {
            return Results.BadRequest($"[{indexes[ex.Id]}]: asset {ex.Id} doesn't exist");
        }
        catch (TokenNotFoundException ex)
        {
            return Results.BadRequest($"[{tokenIndexes[ex.Id]}]: token {ex.Id} doesn't exist");
        }
        catch (AssetTokenConflictException ex)
        {
            return Results.BadRequest($"[{tokenIndexes[ex.Id]}]: token {ex.Id} already belongs to asset {ex.AssetId}");
        }
    }

    static Asset ToModel(AssetInfo asset, int id, long[] tokens) => new()
    {
        Id = id,
        Name = asset.Name,
        Symbol = asset.Symbol,
        Description = asset.Description,
        Logo = asset.Logo,
        Tokens = tokens
    };

    static AssetInfo ToInfo(Asset asset) => new()
    {
        Id = asset.Id,
        Name = asset.Name,
        Symbol = asset.Symbol,
        Description = asset.Description,
        Logo = asset.Logo,
        Tokens = asset.Tokens
    };
}
