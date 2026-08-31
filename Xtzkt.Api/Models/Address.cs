using System.Numerics;
using System.Text.Json.Serialization;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(L1Baker),            AddressTypes.L1Baker)]
[JsonDerivedType(typeof(L1User),             AddressTypes.L1User)]
[JsonDerivedType(typeof(L1Contract),         AddressTypes.L1Contract)]
[JsonDerivedType(typeof(L1SmartRollup),      AddressTypes.L1SmartRollup)]
[JsonDerivedType(typeof(L1Ghost),            AddressTypes.L1Ghost)]
[JsonDerivedType(typeof(XEvmUser),           AddressTypes.XEvmUser)]
[JsonDerivedType(typeof(XEvmAlias),          AddressTypes.XEvmAlias)]
[JsonDerivedType(typeof(XEvmContract),       AddressTypes.XEvmContract)]
[JsonDerivedType(typeof(XMichelsonUser),     AddressTypes.XMichelsonUser)]
[JsonDerivedType(typeof(XMichelsonAlias),    AddressTypes.XMichelsonAlias)]
[JsonDerivedType(typeof(XMichelsonContract), AddressTypes.XMichelsonContract)]
[JsonDerivedType(typeof(XMichelsonGhost),    AddressTypes.XMichelsonGhost)]
public abstract class Address
{
    /// <summary>Internal unique address id.</summary>
    public int Id { get; set; }

    /// <summary>Chain the address exists on.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>Address hash (`tz`, `KT`, `sr` or `0x`).</summary>
    public required string Hash { get; set; }

    /// <summary>Layer the address belongs to (`l1` or `x`).</summary>
    public required string Layer { get; set; }

    /// <summary>Runtime the address belongs to (`michelson` or `evm`).</summary>
    public required string Runtime { get; set; }

    /// <summary>Level of the block where the address first appeared.</summary>
    public int FirstLevel { get; set; }

    /// <summary>Timestamp of the block where the address first appeared.</summary>
    public DateTime FirstTimestamp { get; set; }

    /// <summary>Level of the block where the address was last seen.</summary>
    public int LastLevel { get; set; }

    /// <summary>Timestamp of the block where the address was last seen.</summary>
    public DateTime LastTimestamp { get; set; }

    /// <summary>Number of contracts deployed by the address.</summary>
    public int ContractsCount { get; set; }

    /// <summary>Number of tokens the address currently holds (non-zero balances).</summary>
    public int ActiveTokensCount { get; set; }

    /// <summary>Number of tokens the address ever held, including the ones spent to zero.</summary>
    public int TokenBalancesCount { get; set; }

    /// <summary>Number of token transfers the address took part in.</summary>
    public long TokenTransfersCount { get; set; }

    /// <summary>Number of tickets the address currently holds (non-zero balances).</summary>
    public int ActiveTicketsCount { get; set; }

    /// <summary>Number of tickets the address ever held, including the ones spent to zero.</summary>
    public int TicketBalancesCount { get; set; }

    /// <summary>Number of ticket transfers the address took part in.</summary>
    public int TicketTransfersCount { get; set; }

    /// <summary>Number of transactions the address took part in.</summary>
    public long TransactionsCount { get; set; }

    /// <summary>Number of originations the address took part in.</summary>
    public int OriginationsCount { get; set; }

    /// <summary>Number of protocol migrations that affected the address.</summary>
    public int MigrationsCount { get; set; }
}

public abstract class L1AddressBase : Address
{
    /// <summary>Spendable balance (mutez).</summary>
    public long Balance { get; set; }

    /// <summary>Amount frozen as smart rollup commitment bonds (mutez).</summary>
    public long SmartRollupBonds { get; set; }

    /// <summary>Address's operation counter, ensuring operations apply in order and only once.</summary>
    public int Counter { get; set; }

    /// <summary>Baker the address delegates to, if any.</summary>
    public AddressInfo? Baker { get; set; }

    /// <summary>Level of the block where the current delegation was set.</summary>
    public int? DelegationLevel { get; set; }

    /// <summary>Timestamp of the block where the current delegation was set.</summary>
    public DateTime? DelegationTimestamp { get; set; }

    /// <summary>Whether the address has any funds staked with its baker.</summary>
    public bool Staked { get; set; }

    /// <summary>Address index in the on-chain address registry, if it was registered there.</summary>
    public int? Index { get; set; }

    /// <summary>Number of smart rollups deployed by the address.</summary>
    public int SmartRollupsCount { get; set; }

    /// <summary>Number of delegation operations sent by the address.</summary>
    public int DelegationsCount { get; set; }

    /// <summary>Number of reveal operations sent by the address.</summary>
    public int RevealsCount { get; set; }

    /// <summary>Number of transfer ticket operations the address took part in.</summary>
    public int TransferTicketCount { get; set; }

    /// <summary>Number of increase paid storage operations the address took part in.</summary>
    public int IncreasePaidStorageCount { get; set; }

    /// <summary>Number of update secondary key operations sent by the address.</summary>
    public int UpdateSecondaryKeyCount { get; set; }

    /// <summary>Number of times the address was drained by a drain delegate operation.</summary>
    public int DrainDelegateCount { get; set; }

    /// <summary>Number of liquidity baking subsidies received by the address.</summary>
    public int SubsidyCount { get; set; }

    /// <summary>Number of smart rollup add messages operations sent by the address.</summary>
    public int SmartRollupAddMessagesCount { get; set; }

    /// <summary>Number of smart rollup cement operations the address took part in.</summary>
    public int SmartRollupCementCount { get; set; }

    /// <summary>Number of smart rollup execute operations the address took part in.</summary>
    public int SmartRollupExecuteCount { get; set; }

    /// <summary>Number of smart rollup originate operations sent by the address.</summary>
    public int SmartRollupOriginateCount { get; set; }

    /// <summary>Number of smart rollup publish operations the address took part in.</summary>
    public int SmartRollupPublishCount { get; set; }

    /// <summary>Number of smart rollup recover bond operations the address took part in.</summary>
    public int SmartRollupRecoverBondCount { get; set; }

    /// <summary>Number of smart rollup refute operations the address took part in.</summary>
    public int SmartRollupRefuteCount { get; set; }

    /// <summary>Number of refutation games the address took part in.</summary>
    public int RefutationGamesCount { get; set; }

    /// <summary>Number of refutation games the address is currently playing.</summary>
    public int ActiveRefutationGamesCount { get; set; }
}

public class L1User : L1AddressBase
{
    /// <summary>Whether the address has revealed its public key, which it must do before sending anything.</summary>
    public bool Revealed { get; set; }

    /// <summary>Public key of the address, if it was revealed.</summary>
    public string? PublicKey { get; set; }

    /// <summary>Shares of the baker's staked pool the address owns, used to compute its staked balance.</summary>
    public BigInteger? StakedPseudotokens { get; set; }

    /// <summary>Amount requested to unstake and waiting for the freeze period to pass (mutez).</summary>
    public long UnstakedBalance { get; set; }

    /// <summary>Baker the unstaked balance is still held by.</summary>
    public AddressInfo? UnstakedBaker { get; set; }

    /// <summary>Number of times the address's stake changed.</summary>
    public int? StakingUpdatesCount { get; set; }

    /// <summary>Number of activation operations sent by the address.</summary>
    public int ActivationsCount { get; set; }

    /// <summary>Number of global constants registered by the address.</summary>
    public int RegisterConstantsCount { get; set; }

    /// <summary>Number of set deposits limit operations sent by the address.</summary>
    public int SetDepositsLimitsCount { get; set; }

    /// <summary>Number of staking operations sent by the address.</summary>
    public int StakingOpsCount { get; set; }

    /// <summary>Number of set delegate parameters operations sent by the address.</summary>
    public int SetDelegateParametersOpsCount { get; set; }

    /// <summary>Number of DAL publish commitment operations sent by the address.</summary>
    public int DalPublishCommitmentOpsCount { get; set; }
}

public class L1Baker : L1User
{
    /// <summary>Level of the block where the address registered as a baker.</summary>
    public int ActivationLevel { get; set; }

    /// <summary>Timestamp of the block where the address registered as a baker.</summary>
    public DateTime ActivationTimestamp { get; set; }

    /// <summary>Level the baker will be deactivated at, unless it stays active.</summary>
    public int DeactivationLevel { get; set; }

    /// <summary>Separate key the baker signs consensus operations with, if it set one.</summary>
    public string? ConsensusAddress { get; set; }

    /// <summary>Separate key the baker signs DAL operations with, if it set one.</summary>
    public string? CompanionAddress { get; set; }

    /// <summary>Weight the baker's rights are distributed by (mutez).</summary>
    public long BakingPower { get; set; }

    /// <summary>Weight the baker's votes count with (mutez).</summary>
    public long VotingPower { get; set; }

    /// <summary>Baker's own balance that is delegated, not staked (mutez).</summary>
    public long OwnDelegatedBalance { get; set; }

    /// <summary>Balance delegated to the baker by others (mutez).</summary>
    public long ExternalDelegatedBalance { get; set; }

    /// <summary>Smallest total delegated balance the baker had during the current cycle (mutez).</summary>
    public long MinTotalDelegated { get; set; }

    /// <summary>Level where the smallest total delegated balance was recorded.</summary>
    public int MinTotalDelegatedLevel { get; set; }

    /// <summary>Number of addresses delegating to the baker.</summary>
    public int DelegatorsCount { get; set; }

    /// <summary>Baker's own balance that is staked (mutez).</summary>
    public long OwnStakedBalance { get; set; }

    /// <summary>Balance staked with the baker by others (mutez).</summary>
    public long ExternalStakedBalance { get; set; }

    /// <summary>Total shares of the baker's staked pool issued to external stakers.</summary>
    public BigInteger? IssuedPseudotokens { get; set; }

    /// <summary>Number of addresses staking with the baker.</summary>
    public int StakersCount { get; set; }

    /// <summary>Amount external stakers requested to unstake, still held by the baker (mutez).</summary>
    public long ExternalUnstakedBalance { get; set; }

    /// <summary>Remainder left by the staked pool share arithmetic, kept so the totals add up (mutez).</summary>
    public long RoundingError { get; set; }

    /// <summary>Self-imposed cap on the baker's frozen deposits (mutez, legacy).</summary>
    public long? FrozenDepositLimit { get; set; }

    /// <summary>How much external stake the baker accepts, relative to its own (per cent).</summary>
    public long? LimitOfStakingOverBaking { get; set; }

    /// <summary>Baker's cut of its external stakers' rewards (per cent).</summary>
    public long? EdgeOfBakingOverStaking { get; set; }

    /// <summary>Number of blocks produced by the baker.</summary>
    public int BlocksCount { get; set; }

    /// <summary>Number of attestations sent by the baker.</summary>
    public int AttestationsCount { get; set; }

    /// <summary>Number of preattestations sent by the baker.</summary>
    public int PreattestationsCount { get; set; }

    /// <summary>Number of ballots cast by the baker.</summary>
    public int BallotsCount { get; set; }

    /// <summary>Number of protocol proposals submitted by the baker.</summary>
    public int ProposalsCount { get; set; }

    /// <summary>Number of DAL entrapment evidence operations the baker took part in.</summary>
    public int DalEntrapmentEvidenceOpsCount { get; set; }

    /// <summary>Number of double baking accusations the baker took part in.</summary>
    public int DoubleBakingCount { get; set; }

    /// <summary>Number of double consensus accusations the baker took part in.</summary>
    public int DoubleConsensusCount { get; set; }

    /// <summary>Number of seed nonce revelations the baker took part in.</summary>
    public int NonceRevelationsCount { get; set; }

    /// <summary>Number of VDF revelations sent by the baker.</summary>
    public int VdfRevelationsCount { get; set; }

    /// <summary>Number of times the baker was penalized for not revealing a nonce.</summary>
    public int RevelationPenaltiesCount { get; set; }

    /// <summary>Number of times the baker was paid attestation rewards.</summary>
    public int AttestationRewardsCount { get; set; }

    /// <summary>Number of times the baker was paid DAL attestation rewards.</summary>
    public int DalAttestationRewardsCount { get; set; }

    /// <summary>Number of times the baker's stake was adjusted automatically by the protocol.</summary>
    public int AutostakingOpsCount { get; set; }

    /// <summary>Baker software the last block was produced with, if it was possible to detect.</summary>
    public SoftwareInfo? Software { get; set; }

    /// <summary>Level of the block where the baker last changed its software.</summary>
    public int? SoftwareUpdateLevel { get; set; }
}

public class L1Contract : L1AddressBase
{
    /// <summary>Contract kind (`smart_contract`, `delegator_contract` or `asset`).</summary>
    public required string Kind { get; set; }

    /// <summary>32-bit hash of the contract parameter and storage types (helps to find similar contracts).</summary>
    public int TypeHash { get; set; }

    /// <summary>32-bit hash of the contract code (helps to find identical contracts).</summary>
    public int CodeHash { get; set; }

    /// <summary>Interfaces the contract was recognized as implementing (`fa2`, `fa12`, `nft`, ...).</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Number of tokens issued by the contract.</summary>
    public int TokensCount { get; set; }

    /// <summary>Number of logs (events) emitted by the contract.</summary>
    public long LogsCount { get; set; }

    /// <summary>Number of tickets issued by the contract.</summary>
    public int TicketsCount { get; set; }

    /// <summary>Address that deployed the contract.</summary>
    public required AddressInfo Creator { get; set; }
}

public class L1SmartRollup : L1AddressBase
{
    /// <summary>Address that deployed the rollup.</summary>
    public required AddressInfo Creator { get; set; }

    /// <summary>Virtual machine the rollup runs on (`wasm` or `arith`).</summary>
    public required string PvmKind { get; set; }

    /// <summary>Michelson type of the messages the rollup accepts.</summary>
    public required byte[] ParameterSchema { get; set; }

    /// <summary>Hash of the rollup's initial state commitment.</summary>
    public required string GenesisCommitment { get; set; }

    /// <summary>Hash of the most recent cemented commitment.</summary>
    public required string LastCommitment { get; set; }

    /// <summary>Level up to which the rollup has processed its inbox.</summary>
    public required int InboxLevel { get; set; }

    /// <summary>Number of addresses that ever staked on the rollup's commitments.</summary>
    public int TotalStakers { get; set; }

    /// <summary>Number of addresses currently staking on the rollup's commitments.</summary>
    public int ActiveStakers { get; set; }

    /// <summary>Number of commitments whose outbox messages were executed.</summary>
    public int ExecutedCommitments { get; set; }

    /// <summary>Number of commitments that passed the challenge window and became final.</summary>
    public int CementedCommitments { get; set; }

    /// <summary>Number of commitments still within the challenge window.</summary>
    public int PendingCommitments { get; set; }

    /// <summary>Number of commitments rejected by a refutation game.</summary>
    public int RefutedCommitments { get; set; }

    /// <summary>Number of commitments left hanging off a rejected one.</summary>
    public int OrphanCommitments { get; set; }
}

public class L1Ghost : L1AddressBase { }

public abstract class XAddressBase : Address
{
    /// <summary>Number of aliases the address has in other runtimes.</summary>
    public int AliasesCount { get; set; }

    /// <summary>Number of deposits credited to the address from L1.</summary>
    public int DepositOpsCount { get; set; }
}

public abstract class XEvmAddressBase : XAddressBase
{
    /// <summary>Address's transaction nonce, ensuring transactions apply in order and only once.</summary>
    public int Counter { get; set; }

    /// <summary>Balance (18 decimals).</summary>
    public BigInteger Balance { get; set; }

    /// <summary>Number of blocks produced by the address as a sequencer pool.</summary>
    public int BlocksCount { get; set; }

    /// <summary>Number of EIP-7702 delegations the address authorized.</summary>
    public int Eip7702DelegationCount { get; set; }

    /// <summary>
    /// Number of logs (events), emitted by the address. Non-contract addresses can emit logs
    /// as well, if they have an EIP-7702 delegation.
    /// </summary>
    public long LogsCount { get; set; }

    /// <summary>Number of bridge tickets the address currently holds (non-zero balances).</summary>
    public int ActiveBridgeTicketsCount { get; set; }

    /// <summary>Number of bridge tickets the address ever held, including the ones spent to zero.</summary>
    public int BridgeTicketBalancesCount { get; set; }

    /// <summary>Number of bridge ticket transfers the address took part in.</summary>
    public int BridgeTicketTransfersCount { get; set; }
}

public class XEvmUser : XEvmAddressBase
{
    /// <summary>Contract the address runs the code of, if it set an EIP-7702 delegation.</summary>
    public AddressInfo? Eip7702Delegate { get; set; }
}

public class XEvmAlias : XEvmAddressBase
{
    /// <summary>Michelson address this alias belongs to.</summary>
    public required AddressInfo Owner { get; set; }

    /// <summary>Contract the address runs the code of, if it set an EIP-7702 delegation.</summary>
    public AddressInfo? Eip7702Delegate { get; set; }
}

public class XEvmContract : XEvmAddressBase
{
    /// <summary>Contract kind (`smart_contract` or `asset`).</summary>
    public required string Kind { get; set; }

    /// <summary>32-bit hash of the contract interface (helps to find similar contracts).</summary>
    public int TypeHash { get; set; }

    /// <summary>32-bit hash of the contract code (helps to find identical contracts).</summary>
    public int CodeHash { get; set; }

    /// <summary>Interfaces the contract was recognized as implementing (`erc20`, `erc721`, ...).</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Address that deployed the contract.</summary>
    public required AddressInfo Creator { get; set; }

    /// <summary>Number of tokens issued by the contract.</summary>
    public int TokensCount { get; set; }
}

public abstract class XMichelsonAddressBase : XAddressBase
{
    /// <summary>Balance (mutez).</summary>
    public long Balance { get; set; }

    /// <summary>Address index in the on-chain address registry, if it was registered there.</summary>
    public int? Index { get; set; }

    /// <summary>Number of transfer ticket operations the address took part in.</summary>
    public int TransferTicketCount { get; set; }

    /// <summary>Number of increase paid storage operations the address took part in.</summary>
    public int IncreasePaidStorageCount { get; set; }
}

public class XMichelsonUser : XMichelsonAddressBase
{
    /// <summary>Address's operation counter, ensuring operations apply in order and only once.</summary>
    public int Counter { get; set; }

    /// <summary>Whether the address has revealed its public key, which it must do before sending anything.</summary>
    public bool Revealed { get; set; }

    /// <summary>Public key of the address, if it was revealed.</summary>
    public string? PublicKey { get; set; }

    /// <summary>Number of reveal operations sent by the address.</summary>
    public int RevealsCount { get; set; }

    /// <summary>Number of global constants registered by the address.</summary>
    public int RegisterConstantsCount { get; set; }
}

public class XMichelsonAlias : XMichelsonAddressBase
{
    /// <summary>EVM address this alias belongs to.</summary>
    public required AddressInfo Owner { get; set; }
}

public class XMichelsonContract : XMichelsonAddressBase
{
    /// <summary>Contract kind (`smart_contract`, `delegator_contract` or `asset`).</summary>
    public required string Kind { get; set; }

    /// <summary>32-bit hash of the contract parameter and storage types (helps to find similar contracts).</summary>
    public int TypeHash { get; set; }

    /// <summary>32-bit hash of the contract code (helps to find identical contracts).</summary>
    public int CodeHash { get; set; }

    /// <summary>Interfaces the contract was recognized as implementing (`fa2`, `fa12`, `nft`, ...).</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Number of tokens issued by the contract.</summary>
    public int TokensCount { get; set; }

    /// <summary>Number of logs (events) emitted by the contract.</summary>
    public long LogsCount { get; set; }

    /// <summary>Number of tickets issued by the contract.</summary>
    public int TicketsCount { get; set; }

    /// <summary>Address that deployed the contract.</summary>
    public required AddressInfo Creator { get; set; }
}

public class XMichelsonGhost : XMichelsonAddressBase { }
