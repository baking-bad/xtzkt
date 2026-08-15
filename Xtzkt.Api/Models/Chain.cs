using System.Text.Json.Serialization;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "layer")]
[JsonDerivedType(typeof(XChain), Layers.TezosX)]
[JsonDerivedType(typeof(L1Chain), Layers.L1)]
public abstract class Chain
{
    /// <summary>
    /// Internal unique chain id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Publicly known chain id.
    /// </summary>
    public required string ChainId { get; set; }

    /// <summary>
    /// Network name.
    /// </summary>
    public required string Network { get; set; }

    /// <summary>
    /// Level of the last indexed block.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Timestamp of the last indexed block.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Hash of the last indexed block.
    /// </summary>
    public required string Hash { get; set; }

    /// <summary>
    /// Level of the last known block.
    /// </summary>
    public int KnownLevel { get; set; }

    /// <summary>
    /// Timestamp of the last synchronization with the node (ISO 8601, e.g. `2020-02-20T02:40:57Z`).
    /// </summary>
    public DateTime SyncedAt { get; set; }
}

public class XChain : Chain
{
    /// <summary>
    /// Address of the smart rollup behind this Tezos X chain.
    /// </summary>
    public required string RollupAddress { get; set; }

    /// <summary>
    /// Kernel root hash.
    /// </summary>
    public required string Kernel { get; set; }

    /// <summary>
    /// Scheduled kernel upgrade root hash.
    /// </summary>
    public string? KernelUpgrade { get; set; }

    /// <summary>
    /// Scheduled kernel upgrade time (ISO 8601, e.g. `2020-02-20T02:40:57Z`).
    /// </summary>
    public DateTime? KernelUpgradeTime { get; set; }

    /// <summary>
    /// Level of the Michelson runtime activation.
    /// </summary>
    public int? MichelsonActivationLevel { get; set; }

    /// <summary>
    /// Michelson chain id.
    /// </summary>
    public string? MichelsonChainId { get; set; }

    /// <summary>
    /// Michelson protocol hash.
    /// </summary>
    public string? MichelsonProtocol { get; set; }

    /// <summary>
    /// Michelson hash of the last indexed block.
    /// </summary>
    public string? MichelsonBlock { get; set; }
}

public class L1Chain : Chain
{
    /// <summary>
    /// Current cycle index
    /// </summary>
    public int Cycle { get; set; }

    /// <summary>
    /// Current protocol hash
    /// </summary>
    public required string Protocol { get; set; }

    /// <summary>
    /// Next block protocol hash
    /// </summary>
    public required string NextProtocol { get; set; }

    /// <summary>
    /// Current voting epoch index, starting from zero
    /// </summary>
    public int VotingEpoch { get; set; }

    /// <summary>
    /// Current voting period index, starting from zero
    /// </summary>
    public int VotingPeriod { get; set; }
}
