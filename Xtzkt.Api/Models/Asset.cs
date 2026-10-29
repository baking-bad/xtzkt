namespace Xtzkt.Api.Models;

public class Asset
{
    /// <summary>
    /// Internal unique asset id, or `null` if the token doesn't belong to any asset.
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    /// Asset name. Falls back to the token's own name, if the token doesn't belong to any asset.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Asset symbol. Falls back to the token's own symbol, if the token doesn't belong to any asset.
    /// </summary>
    public string? Symbol { get; set; }

    /// <summary>
    /// Asset description. Falls back to the token's metadata, if the token doesn't belong to any asset.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Asset logo uri. Falls back to the token's metadata, if the token doesn't belong to any asset.
    /// </summary>
    public string? Logo { get; set; }

    /// <summary>
    /// Tokens the asset consists of.
    /// </summary>
    public required List<Token> Tokens { get; set; }
}
