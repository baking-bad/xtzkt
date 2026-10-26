namespace Xtzkt.Services.Admin.Exceptions;

public class AssetNotFoundException(int id) : Exception($"Asset {id} doesn't exist")
{
    public int Id { get; } = id;
}
