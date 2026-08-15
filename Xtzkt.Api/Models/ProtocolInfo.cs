namespace Xtzkt.Api.Models;

public class ProtocolInfo
{
    /// <summary>
    /// Internal unique protocol id.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Protocol hash.
    /// </summary>
    public required string Hash { get; set; }

    /// <summary>
    /// Internal version of the protocol.
    /// </summary>
    public int Version { get; set; }
}
