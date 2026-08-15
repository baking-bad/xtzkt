namespace Xtzkt.Api.Models;

public class ChainInfo
{
    /// <summary>
    /// Internal unique chain id.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Publicly known chain id.
    /// </summary>
    public required string ChainId { get; init; }

    /// <summary>
    /// Chain layer.
    /// </summary>
    public required string Layer { get; init; }
}
