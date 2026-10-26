using System.Diagnostics.CodeAnalysis;

namespace Xtzkt.Api.Models.Enums;

public class ActivityTypes
{
    public const string Activation = "activation";
    public const string DalEntrapmentEvidence = "dal_entrapment_evidence";
    public const string DoubleBaking = "double_baking";
    public const string DoubleConsensus = "double_consensus";
    public const string DrainDelegate = "drain_delegate";
    public const string NonceRevelation = "nonce_revelation";
    public const string VdfRevelation = "vdf_revelation";

    public const string Attestation = "attestation";
    public const string Preattestation = "preattestation";

    public const string Ballot = "ballot";
    public const string Proposal = "proposal";

    public const string Migration = "migration";
    public const string AttestationReward = "attestation_reward";
    public const string Autostaking = "autostaking";
    public const string DalAttestationReward = "dal_attestation_reward";
    public const string RevelationPenalty = "revelation_penalty";
    public const string Subsidy = "subsidy";

    public const string Deposit = "deposit";
    public const string Origination = "origination";
    public const string Transaction = "transaction";
    public const string DalPublishCommitment = "dal_publish_commitment";
    public const string Delegation = "delegation";
    public const string IncreasePaidStorage = "increase_paid_storage";
    public const string RegisterConstant = "register_constant";
    public const string Reveal = "reveal";
    public const string SetDepositsLimit = "set_deposits_limit";
    public const string SetDelegateParameters = "set_delegate_parameters";
    public const string SmartRollupAddMessages = "smart_rollup_add_messages";
    public const string SmartRollupCement = "smart_rollup_cement";
    public const string SmartRollupExecute = "smart_rollup_execute";
    public const string SmartRollupOriginate = "smart_rollup_originate";
    public const string SmartRollupPublish = "smart_rollup_publish";
    public const string SmartRollupRecoverBond = "smart_rollup_recover_bond";
    public const string SmartRollupRefute = "smart_rollup_refute";
    public const string Staking = "staking";
    public const string TransferTicket = "transfer_ticket";
    public const string UpdateSecondaryKey = "update_secondary_key";

    public const string TicketTransfer = "ticket_transfer";
    public const string TokenTransfer = "token_transfer";
    public const string BridgeTicketTransfer = "bridge_ticket_transfer";
    public const string Baking = "baking";

    #region aliases
    public const string Anonymous = "anonymous";
    public const string Consensus = "consensus";
    public const string Voting = "voting";
    public const string Implicit = "implicit";
    public const string Manager = "manager";
    public const string All = "all";
    #endregion

    static readonly HashSet<string> AnonymousGroup =
    [
        Activation,
        DalEntrapmentEvidence,
        DoubleBaking,
        DoubleConsensus,
        DrainDelegate,
        NonceRevelation,
        VdfRevelation,
    ];

    static readonly HashSet<string> ConsensusGroup =
    [
        Attestation,
        Preattestation,
        Baking,
    ];

    static readonly HashSet<string> VotingGroup =
    [
        Ballot,
        Proposal,
    ];

    static readonly HashSet<string> ImplicitGroup =
    [
        Migration,
        AttestationReward,
        Autostaking,
        DalAttestationReward,
        RevelationPenalty,
        Subsidy,
    ];

    static readonly HashSet<string> ManagerGroup =
    [
        Deposit,
        Origination,
        Transaction,
        DalPublishCommitment,
        Delegation,
        IncreasePaidStorage,
        RegisterConstant,
        Reveal,
        SetDepositsLimit,
        SetDelegateParameters,
        SmartRollupAddMessages,
        SmartRollupCement,
        SmartRollupExecute,
        SmartRollupOriginate,
        SmartRollupPublish,
        SmartRollupRecoverBond,
        SmartRollupRefute,
        Staking,
        TransferTicket,
        UpdateSecondaryKey,

        TicketTransfer,
        TokenTransfer,
        BridgeTicketTransfer,
    ];

    static readonly HashSet<string> AllGroup =
    [
        .. AnonymousGroup,
        .. ConsensusGroup,
        .. VotingGroup,
        .. ImplicitGroup,
        .. ManagerGroup,
    ];

    public static bool IsValid(string value) => AllGroup.Contains(value);

    public static bool TryGetGroup(string value, [NotNullWhen(true)] out IReadOnlySet<string>? group)
    {
        group = value switch
        {
            Anonymous => AnonymousGroup,
            Consensus => ConsensusGroup,
            Voting => VotingGroup,
            Implicit => ImplicitGroup,
            Manager => ManagerGroup,
            All => AllGroup,
            _ => null
        };
        return group != null;
    }

    public static readonly HashSet<string> Default =
    [
        Activation,
        DalEntrapmentEvidence,
        DoubleBaking,
        DoubleConsensus,
        DrainDelegate,
        NonceRevelation,
        VdfRevelation,

        //Attestation,
        //Preattestation,

        Ballot,
        Proposal,

        Migration,
        AttestationReward,
        //Autostaking,
        DalAttestationReward,
        //RevelationPenalty,
        Subsidy,

        Deposit,
        Origination,
        Transaction,
        DalPublishCommitment,
        Delegation,
        IncreasePaidStorage,
        RegisterConstant,
        Reveal,
        SetDelegateParameters,
        //SetDepositsLimit,
        SmartRollupAddMessages,
        SmartRollupCement,
        SmartRollupExecute,
        SmartRollupOriginate,
        SmartRollupPublish,
        SmartRollupRecoverBond,
        SmartRollupRefute,
        Staking,
        TransferTicket,
        UpdateSecondaryKey,

        TicketTransfer,
        TokenTransfer,
        BridgeTicketTransfer,
        Baking,
    ];
}
