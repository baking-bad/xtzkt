namespace Xtzkt.Services.Admin.Exceptions;

public class AssetTokenConflictException(long id, int assetId) : Exception($"Token {id} already belongs to asset {assetId}")
{
    public long Id { get; } = id;
    public int AssetId { get; } = assetId;
}
