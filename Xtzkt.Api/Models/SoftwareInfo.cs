namespace Xtzkt.Api.Models;

public class SoftwareInfo
{
    /// <summary>Internal unique software id.</summary>
    public int Id { get; init; }

    /// <summary>Short commit hash of the baker software build.</summary>
    public required string ShortHash { get; init; }
}
