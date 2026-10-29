namespace Xtzkt.Api.Models;

public class AssetInfo
{
    /// <summary>Internal unique asset id.</summary>
    public int Id { get; set; }

    /// <summary>Asset name.</summary>
    public string? Name { get; set; }

    /// <summary>Asset symbol.</summary>
    public string? Symbol { get; set; }

    /// <summary>Asset logo uri.</summary>
    public string? Logo { get; set; }
}
