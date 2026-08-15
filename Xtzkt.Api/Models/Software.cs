namespace Xtzkt.Api.Models;

public class Software
{
    /// <summary>
    /// Internal unique software id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Chain the software was seen on.
    /// </summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>
    /// Short commit hash of the baker software build, taken from the blocks it produced.
    /// </summary>
    public required string ShortHash { get; set; }

    /// <summary>
    /// Level of the first block produced by this software build.
    /// </summary>
    public int FirstLevel { get; set; }

    /// <summary>
    /// Level of the last block produced by this software build.
    /// </summary>
    public int LastLevel { get; set; }

    /// <summary>
    /// Number of blocks produced by this software build.
    /// </summary>
    public int BlocksCount { get; set; }
}
