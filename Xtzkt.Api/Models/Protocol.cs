using System.Numerics;
using System.Text.Json.Serialization;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "layer")]
[JsonDerivedType(typeof(XProtocol), Layers.TezosX)]
[JsonDerivedType(typeof(L1Protocol), Layers.L1)]
public abstract class Protocol
{
    /// <summary>
    /// Internal unique protocol id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Chain the protocol was activated on.
    /// </summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>
    /// Protocol hash. For Tezos X it's the kernel root hash.
    /// </summary>
    public required string Hash { get; set; }

    /// <summary>
    /// Sequential protocol version, incremented on each activation.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Level of the first block under this protocol.
    /// </summary>
    public int FirstLevel { get; set; }

    /// <summary>
    /// Level of the last block under this protocol. Points to the current head while the protocol is active.
    /// </summary>
    public int LastLevel { get; set; }
}

public class L1Protocol : Protocol
{
    /// <summary>
    /// First cycle under this protocol.
    /// </summary>
    public int FirstCycle { get; set; }

    /// <summary>
    /// Level the first full cycle under this protocol starts at. Blocks between `firstLevel`
    /// and this one belong to the previous protocol's cycle.
    /// </summary>
    public int FirstCycleLevel { get; set; }

    /// <summary>
    /// Number of cycles it takes for security deposits to reach their full size (legacy, `0` since Proto 12).
    /// </summary>
    public int RampUpCycles { get; set; }

    /// <summary>
    /// Number of initial cycles that pay no rewards (legacy, `0` since Proto 12).
    /// </summary>
    public int NoRewardCycles { get; set; }

    /// <summary>
    /// How many cycles in advance baking and attestation rights are known.
    /// </summary>
    public int ConsensusRightsDelay { get; set; }

    /// <summary>
    /// How many cycles it takes for changed baker parameters (such as the staking edge) to take effect.
    /// </summary>
    public int BakerParametersActivationDelay { get; set; }

    /// <summary>
    /// Number of blocks in a cycle.
    /// </summary>
    public int BlocksPerCycle { get; set; }

    /// <summary>
    /// How often a baker must publish a seed nonce commitment (once per that many blocks).
    /// </summary>
    public int BlocksPerCommitment { get; set; }

    /// <summary>
    /// How often baker stake snapshots are taken (once per that many blocks).
    /// </summary>
    public int BlocksPerSnapshot { get; set; }

    /// <summary>
    /// Number of blocks in a voting period.
    /// </summary>
    public int BlocksPerVoting { get; set; }

    /// <summary>
    /// Minimal time between two blocks (seconds).
    /// </summary>
    public int TimeBetweenBlocks { get; set; }

    /// <summary>
    /// Number of attestation slots per block, shared among bakers proportionally to their baking power.
    /// </summary>
    public int AttestersPerBlock { get; set; }

    /// <summary>
    /// Maximum gas a single operation may consume.
    /// </summary>
    public int HardOperationGasLimit { get; set; }

    /// <summary>
    /// Maximum bytes of storage a single operation may allocate.
    /// </summary>
    public int HardOperationStorageLimit { get; set; }

    /// <summary>
    /// Maximum gas all operations in a block may consume.
    /// </summary>
    public int HardBlockGasLimit { get; set; }

    /// <summary>
    /// Minimal staked balance required to become a baker (mutez).
    /// </summary>
    public long MinimalStake { get; set; }

    /// <summary>
    /// Minimal own frozen stake required to keep baking (mutez).
    /// </summary>
    public long MinimalFrozenStake { get; set; }

    /// <summary>
    /// Security deposit frozen for producing a block (mutez, legacy — `0` since Proto 12).
    /// </summary>
    public long BlockDeposit { get; set; }

    /// <summary>
    /// Fixed part of the baking reward, paid for every block (mutez).
    /// </summary>
    public long BlockReward0 { get; set; }

    /// <summary>
    /// Bonus added to the baking reward per each attestation slot included above the threshold (mutez).
    /// </summary>
    public long BlockReward1 { get; set; }

    /// <summary>
    /// Maximum a baker can earn for a block, if he includes all the attestations (mutez).
    /// </summary>
    public long MaxBakingReward { get; set; }

    /// <summary>
    /// Security deposit frozen for attesting (mutez, legacy — `0` since Proto 12).
    /// </summary>
    public long AttestationDeposit { get; set; }

    /// <summary>
    /// Attestation reward per slot, paid at the end of the cycle (mutez).
    /// </summary>
    public long AttestationReward0 { get; set; }

    /// <summary>
    /// Attestation reward for a block produced at a higher round (mutez, legacy — `0` since Proto 12).
    /// </summary>
    public long AttestationReward1 { get; set; }

    /// <summary>
    /// Maximum attestation reward per block, shared among all the attesters (mutez).
    /// </summary>
    public long MaxAttestationReward { get; set; }

    /// <summary>
    /// Storage size an origination is charged for on top of the actual contract size (bytes).
    /// </summary>
    public int OriginationSize { get; set; }

    /// <summary>
    /// Cost of one byte of storage (mutez).
    /// </summary>
    public int ByteCost { get; set; }

    /// <summary>
    /// Voting power needed to promote a proposal, in basis points (e.g. `500` = 5%).
    /// </summary>
    public int ProposalQuorum { get; set; }

    /// <summary>
    /// Lower bound of the adaptive ballot quorum, in basis points (e.g. `2000` = 20%).
    /// </summary>
    public int BallotQuorumMin { get; set; }

    /// <summary>
    /// Upper bound of the adaptive ballot quorum, in basis points (e.g. `7000` = 70%).
    /// </summary>
    public int BallotQuorumMax { get; set; }

    /// <summary>
    /// Liquidity baking is stopped once the toggle EMA crosses this value.
    /// </summary>
    public int LBToggleThreshold { get; set; }

    /// <summary>
    /// Attestation power a block must collect to be considered final.
    /// </summary>
    public int ConsensusThreshold { get; set; }

    /// <summary>
    /// Numerator of the minimal share of attestations a baker must send per cycle to avoid losing rewards.
    /// </summary>
    public int MinParticipationNumerator { get; set; }

    /// <summary>
    /// Denominator of the minimal share of attestations a baker must send per cycle to avoid losing rewards.
    /// </summary>
    public int MinParticipationDenominator { get; set; }

    /// <summary>
    /// How many cycles a misbehavior can still be denounced for.
    /// </summary>
    public int DenunciationPeriod { get; set; }

    /// <summary>
    /// How many cycles after the denunciation the slashing is actually applied.
    /// </summary>
    public int SlashingDelay { get; set; }

    /// <summary>
    /// How many times more a baker can have delegated than frozen, before the excess stops counting.
    /// </summary>
    public int MaxDelegatedOverFrozenRatio { get; set; }

    /// <summary>
    /// How many times more external stake a baker can accept than his own, before the excess stops counting.
    /// </summary>
    public int MaxExternalOverOwnStakeRatio { get; set; }

    /// <summary>
    /// How much more baking power staked funds have compared to delegated ones.
    /// </summary>
    public int StakePowerMultiplier { get; set; }

    /// <summary>
    /// Storage size a smart rollup origination is charged for (bytes).
    /// </summary>
    public int SmartRollupOriginationSize { get; set; }

    /// <summary>
    /// Amount that must be staked to publish a smart rollup commitment (mutez).
    /// </summary>
    public long SmartRollupStakeAmount { get; set; }

    /// <summary>
    /// How many blocks a published smart rollup commitment can be refuted within.
    /// </summary>
    public int SmartRollupChallengeWindow { get; set; }

    /// <summary>
    /// How many blocks a smart rollup commitment covers.
    /// </summary>
    public int SmartRollupCommitmentPeriod { get; set; }

    /// <summary>
    /// How many blocks a refutation game player may stay inactive before losing by timeout.
    /// </summary>
    public int SmartRollupTimeoutPeriod { get; set; }

    /// <summary>
    /// Address allowed to force protocol changes on a testnet, bypassing voting. `null` on mainnet.
    /// </summary>
    public string? Dictator { get; set; }

    /// <summary>
    /// Share of the frozen deposits slashed for double baking, in basis points (e.g. `500` = 5%).
    /// </summary>
    public int DoubleBakingSlashedPercentage { get; set; }

    /// <summary>
    /// Share of the frozen deposits slashed for double attesting, in basis points (e.g. `5000` = 50%).
    /// </summary>
    public int DoubleConsensusSlashedPercentage { get; set; }

    /// <summary>
    /// Number of DAL shards a block's data is split into.
    /// </summary>
    public int NumberOfShards { get; set; }

    /// <summary>
    /// How many cycles a baker may stay inactive before being deactivated.
    /// </summary>
    public int ToleratedInactivityPeriod { get; set; }
}

public class XProtocol : Protocol
{
    /// <summary>
    /// Hash of the L1 protocol the Michelson runtime of this kernel is based on.
    /// </summary>
    public string? MichelsonHash { get; set; }

    /// <summary>
    /// Minimal time between two blocks (milliseconds).
    /// </summary>
    public int MinBlockTimeMs { get; set; }

    /// <summary>
    /// Maximum time between two blocks — a block is produced even if there's nothing to include (milliseconds).
    /// </summary>
    public int MaxBlockTimeMs { get; set; }

    /// <summary>
    /// Storage size an origination is charged for on top of the actual contract size (bytes).
    /// </summary>
    public int OriginationSize { get; set; }

    /// <summary>
    /// Cost of one byte of storage (mutez).
    /// </summary>
    public int ByteCost { get; set; }

    /// <summary>
    /// Data availability fee charged per byte posted to L1, as seen by the Michelson runtime (mutez).
    /// </summary>
    public long DaFeePerByte { get; set; }

    /// <summary>
    /// The same data availability fee per byte, as seen by the EVM runtime (18 decimals).
    /// </summary>
    public BigInteger DaFeePerByte18 { get; set; }

    /// <summary>
    /// Maximum gas all EVM transactions in a block may consume.
    /// </summary>
    public long HardEvmBlockGasLimit { get; set; }

    /// <summary>
    /// Maximum gas a single EVM transaction may consume.
    /// </summary>
    public long HardEvmOperationGasLimit { get; set; }

    /// <summary>
    /// Maximum gas all Michelson operations in a block may consume.
    /// </summary>
    public int HardMichelsonBlockGasLimit { get; set; }

    /// <summary>
    /// Maximum gas a single Michelson operation may consume.
    /// </summary>
    public int HardMichelsonOperationGasLimit { get; set; }

    /// <summary>
    /// Maximum bytes of storage a single Michelson operation may allocate.
    /// </summary>
    public int HardMichelsonOperationStorageLimit { get; set; }
}
