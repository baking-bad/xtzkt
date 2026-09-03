using System.Numerics;
using System.Text.Json.Serialization;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "layer")]
[JsonDerivedType(typeof(XBlock), Layers.TezosX)]
[JsonDerivedType(typeof(L1Block), Layers.L1)]
public abstract class Block
{
    /// <summary>
    /// Internal unique block id.
    /// </summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>
    /// Chain the block belongs to.
    /// </summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>
    /// Height of the block in the chain.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Hash of the block.
    /// </summary>
    public required string Hash { get; set; }

    /// <summary>
    /// When the block was created (ISO 8601, e.g. `2020-02-20T02:40:57Z`).
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Protocol the block was created under.
    /// </summary>
    public required ProtocolInfo Protocol { get; set; }
}

public class L1Block : Block
{
    /// <summary>
    /// Cycle the block belongs to.
    /// </summary>
    public int Cycle { get; set; }

    /// <summary>
    /// Baker software that produced the block, if it was possible to detect.
    /// </summary>
    public SoftwareInfo? Software { get; set; }

    /// <summary>
    /// Round in which the block payload was proposed. `0` means the first baker in the row did his job.
    /// </summary>
    public int PayloadRound { get; set; }

    /// <summary>
    /// Round in which the block was produced. Greater than `0` means someone missed his turn.
    /// </summary>
    public int BlockRound { get; set; }

    /// <summary>
    /// Total voting power of the attestations included in the block.
    /// </summary>
    public long AttestationPower { get; set; }

    /// <summary>
    /// Total voting power that could have attested the block, if everyone did his job.
    /// </summary>
    public long AttestationCommittee { get; set; }

    /// <summary>
    /// Baking reward paid to the proposer as spendable balance (mutez).
    /// </summary>
    public long RewardDelegated { get; set; }

    /// <summary>
    /// Baking reward frozen as the proposer's own stake (mutez).
    /// </summary>
    public long RewardStakedOwn { get; set; }

    /// <summary>
    /// Baking reward frozen as the proposer's edge taken from his stakers' rewards (mutez).
    /// </summary>
    public long RewardStakedEdge { get; set; }

    /// <summary>
    /// Baking reward frozen and shared among the proposer's external stakers (mutez).
    /// </summary>
    public long RewardStakedShared { get; set; }

    /// <summary>
    /// Bonus for extra attestations paid to the producer as spendable balance (mutez).
    /// </summary>
    public long BonusDelegated { get; set; }

    /// <summary>
    /// Bonus for extra attestations frozen as the producer's own stake (mutez).
    /// </summary>
    public long BonusStakedOwn { get; set; }

    /// <summary>
    /// Bonus for extra attestations frozen as the producer's edge taken from his stakers' rewards (mutez).
    /// </summary>
    public long BonusStakedEdge { get; set; }

    /// <summary>
    /// Bonus for extra attestations frozen and shared among the producer's external stakers (mutez).
    /// </summary>
    public long BonusStakedShared { get; set; }

    /// <summary>
    /// Total fees paid by the operations in the block and collected by the baker (mutez).
    /// </summary>
    public long BakerFees { get; set; }

    /// <summary>
    /// Total amount burned by the operations in the block (mutez).
    /// </summary>
    public long BurnedFees { get; set; }

    /// <summary>
    /// Gas consumed by all the operations in the block: the sum of the per-operation figures, each rounded up
    /// from milligas. Note that `hardBlockGasLimit` caps the declared gas limits, not the consumed gas.
    /// </summary>
    public int GasUsed { get; set; }


    /// <summary>
    /// Baker who proposed the block payload and got the baking reward.
    /// </summary>
    public AddressInfo? Proposer { get; set; }

    /// <summary>
    /// Baker who actually produced (signed) the block and got the bonus. Differs from the proposer
    /// when the block was re-proposed at a higher round.
    /// </summary>
    public AddressInfo? Producer { get; set; }

    /// <summary>
    /// Liquidity baking vote cast by the baker: `true` to keep the subsidy on, `false` to turn it off,
    /// `null` to pass.
    /// </summary>
    public bool? LBToggle { get; set; }

    /// <summary>
    /// Exponential moving average of the liquidity baking votes. The subsidy stops once it crosses
    /// the protocol threshold.
    /// </summary>
    public int LBToggleEma { get; set; }
}

public class XBlock : Block
{
    /// <summary>
    /// Total data availability fees paid by the transactions in the block (18 decimals).
    /// </summary>
    public BigInteger DaFees { get; set; }

    /// <summary>
    /// Total amount burned by the transactions in the block (18 decimals).
    /// </summary>
    public BigInteger BurnedFees { get; set; }

    /// <summary>
    /// Pre-refund execution gas of the block's EVM-runtime operations. The EVM side has no per-block
    /// gas cap to compare it with — the `gasLimit` the node reports for a block is a placeholder.
    /// </summary>
    public long EvmGasUsed { get; set; }

    /// <summary>
    /// Gas consumed by the block's Michelson-runtime operations. Note that `hardMichelsonBlockGasLimit`
    /// caps the declared gas limits, not the consumed gas.
    /// </summary>
    public int MichelsonGasUsed { get; set; }


    /// <summary>
    /// Sequencer pool that produced the block.
    /// </summary>
    public AddressInfo? SequencerPool { get; set; }

    /// <summary>
    /// Hash of the same block in the Michelson runtime. The `hash` field holds the EVM one,
    /// so the block can be looked up by either.
    /// </summary>
    public string? MichelsonHash { get; set; }
}
