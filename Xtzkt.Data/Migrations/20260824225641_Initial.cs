using System;
using System.Numerics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Xtzkt.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivationOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    Balance = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivationOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Layer = table.Column<int>(type: "integer", nullable: false),
                    Runtime = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false),
                    FirstLevel = table.Column<int>(type: "integer", nullable: false),
                    FirstTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false),
                    LastTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ContractsCount = table.Column<int>(type: "integer", nullable: false),
                    ActiveTokensCount = table.Column<int>(type: "integer", nullable: false),
                    TokenBalancesCount = table.Column<int>(type: "integer", nullable: false),
                    TokenTransfersCount = table.Column<long>(type: "bigint", nullable: false),
                    ActiveTicketsCount = table.Column<int>(type: "integer", nullable: false),
                    TicketBalancesCount = table.Column<int>(type: "integer", nullable: false),
                    TicketTransfersCount = table.Column<int>(type: "integer", nullable: false),
                    TransactionsCount = table.Column<long>(type: "bigint", nullable: false),
                    OriginationsCount = table.Column<int>(type: "integer", nullable: false),
                    MigrationsCount = table.Column<int>(type: "integer", nullable: false),
                    Extras = table.Column<string>(type: "jsonb", nullable: true),
                    Balance = table.Column<long>(type: "bigint", nullable: true),
                    SmartRollupBonds = table.Column<long>(type: "bigint", nullable: true),
                    Counter = table.Column<int>(type: "integer", nullable: true),
                    BakerId = table.Column<int>(type: "integer", nullable: true),
                    DelegationLevel = table.Column<int>(type: "integer", nullable: true),
                    DelegationTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Staked = table.Column<bool>(type: "boolean", nullable: true),
                    Index = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupsCount = table.Column<int>(type: "integer", nullable: true),
                    DelegationsCount = table.Column<int>(type: "integer", nullable: true),
                    RevealsCount = table.Column<int>(type: "integer", nullable: true),
                    TransferTicketCount = table.Column<int>(type: "integer", nullable: true),
                    IncreasePaidStorageCount = table.Column<int>(type: "integer", nullable: true),
                    UpdateSecondaryKeyCount = table.Column<int>(type: "integer", nullable: true),
                    DrainDelegateCount = table.Column<int>(type: "integer", nullable: true),
                    SubsidyCount = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupAddMessagesCount = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupCementCount = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupExecuteCount = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupOriginateCount = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupPublishCount = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupRecoverBondCount = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupRefuteCount = table.Column<int>(type: "integer", nullable: true),
                    RefutationGamesCount = table.Column<int>(type: "integer", nullable: true),
                    ActiveRefutationGamesCount = table.Column<int>(type: "integer", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: true),
                    TypeHash = table.Column<int>(type: "integer", nullable: true),
                    CodeHash = table.Column<int>(type: "integer", nullable: true),
                    Tags = table.Column<int>(type: "integer", nullable: true),
                    TokensCount = table.Column<int>(type: "integer", nullable: true),
                    LogsCount = table.Column<long>(type: "bigint", nullable: true),
                    TicketsCount = table.Column<int>(type: "integer", nullable: true),
                    CreatorId = table.Column<int>(type: "integer", nullable: true),
                    PvmKind = table.Column<int>(type: "integer", nullable: true),
                    ParameterSchema = table.Column<byte[]>(type: "bytea", nullable: true),
                    GenesisCommitment = table.Column<string>(type: "text", nullable: true),
                    LastCommitment = table.Column<string>(type: "text", nullable: true),
                    InboxLevel = table.Column<int>(type: "integer", nullable: true),
                    TotalStakers = table.Column<int>(type: "integer", nullable: true),
                    ActiveStakers = table.Column<int>(type: "integer", nullable: true),
                    ExecutedCommitments = table.Column<int>(type: "integer", nullable: true),
                    CementedCommitments = table.Column<int>(type: "integer", nullable: true),
                    PendingCommitments = table.Column<int>(type: "integer", nullable: true),
                    RefutedCommitments = table.Column<int>(type: "integer", nullable: true),
                    OrphanCommitments = table.Column<int>(type: "integer", nullable: true),
                    Revealed = table.Column<bool>(type: "boolean", nullable: true),
                    PublicKey = table.Column<string>(type: "text", nullable: true),
                    StakedPseudotokens = table.Column<BigInteger>(type: "numeric", nullable: true),
                    UnstakedBalance = table.Column<long>(type: "bigint", nullable: true),
                    UnstakedBakerId = table.Column<int>(type: "integer", nullable: true),
                    StakingUpdatesCount = table.Column<int>(type: "integer", nullable: true),
                    ActivationsCount = table.Column<int>(type: "integer", nullable: true),
                    RegisterConstantsCount = table.Column<int>(type: "integer", nullable: true),
                    SetDepositsLimitsCount = table.Column<int>(type: "integer", nullable: true),
                    StakingOpsCount = table.Column<int>(type: "integer", nullable: true),
                    SetDelegateParametersOpsCount = table.Column<int>(type: "integer", nullable: true),
                    DalPublishCommitmentOpsCount = table.Column<int>(type: "integer", nullable: true),
                    ActivationLevel = table.Column<int>(type: "integer", nullable: true),
                    ActivationTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeactivationLevel = table.Column<int>(type: "integer", nullable: true),
                    ConsensusAddress = table.Column<string>(type: "text", nullable: true),
                    CompanionAddress = table.Column<string>(type: "text", nullable: true),
                    BakingPower = table.Column<long>(type: "bigint", nullable: true),
                    VotingPower = table.Column<long>(type: "bigint", nullable: true),
                    OwnDelegatedBalance = table.Column<long>(type: "bigint", nullable: true),
                    ExternalDelegatedBalance = table.Column<long>(type: "bigint", nullable: true),
                    MinTotalDelegated = table.Column<long>(type: "bigint", nullable: true),
                    MinTotalDelegatedLevel = table.Column<int>(type: "integer", nullable: true),
                    DelegatorsCount = table.Column<int>(type: "integer", nullable: true),
                    OwnStakedBalance = table.Column<long>(type: "bigint", nullable: true),
                    ExternalStakedBalance = table.Column<long>(type: "bigint", nullable: true),
                    IssuedPseudotokens = table.Column<BigInteger>(type: "numeric", nullable: true),
                    StakersCount = table.Column<int>(type: "integer", nullable: true),
                    ExternalUnstakedBalance = table.Column<long>(type: "bigint", nullable: true),
                    RoundingError = table.Column<long>(type: "bigint", nullable: true),
                    FrozenDepositLimit = table.Column<long>(type: "bigint", nullable: true),
                    LimitOfStakingOverBaking = table.Column<long>(type: "bigint", nullable: true),
                    EdgeOfBakingOverStaking = table.Column<long>(type: "bigint", nullable: true),
                    BlocksCount = table.Column<int>(type: "integer", nullable: true),
                    AttestationsCount = table.Column<int>(type: "integer", nullable: true),
                    PreattestationsCount = table.Column<int>(type: "integer", nullable: true),
                    BallotsCount = table.Column<int>(type: "integer", nullable: true),
                    ProposalsCount = table.Column<int>(type: "integer", nullable: true),
                    DalEntrapmentEvidenceOpsCount = table.Column<int>(type: "integer", nullable: true),
                    DoubleBakingCount = table.Column<int>(type: "integer", nullable: true),
                    DoubleConsensusCount = table.Column<int>(type: "integer", nullable: true),
                    NonceRevelationsCount = table.Column<int>(type: "integer", nullable: true),
                    VdfRevelationsCount = table.Column<int>(type: "integer", nullable: true),
                    RevelationPenaltiesCount = table.Column<int>(type: "integer", nullable: true),
                    AttestationRewardsCount = table.Column<int>(type: "integer", nullable: true),
                    DalAttestationRewardsCount = table.Column<int>(type: "integer", nullable: true),
                    AutostakingOpsCount = table.Column<int>(type: "integer", nullable: true),
                    SoftwareId = table.Column<int>(type: "integer", nullable: true),
                    SoftwareUpdateLevel = table.Column<int>(type: "integer", nullable: true),
                    AliasesCount = table.Column<int>(type: "integer", nullable: true),
                    DepositOpsCount = table.Column<int>(type: "integer", nullable: true),
                    Balance18 = table.Column<BigInteger>(type: "numeric", nullable: true),
                    Eip7702DelegationCount = table.Column<int>(type: "integer", nullable: true),
                    ActiveBridgeTicketsCount = table.Column<int>(type: "integer", nullable: true),
                    BridgeTicketBalancesCount = table.Column<int>(type: "integer", nullable: true),
                    BridgeTicketTransfersCount = table.Column<int>(type: "integer", nullable: true),
                    OwnerId = table.Column<int>(type: "integer", nullable: true),
                    Eip7702DelegateId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Logo = table.Column<string>(type: "text", nullable: true),
                    Tokens = table.Column<long[]>(type: "bigint[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttestationOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    Power = table.Column<long>(type: "bigint", nullable: false),
                    Reward = table.Column<long>(type: "bigint", nullable: false),
                    Deposit = table.Column<long>(type: "bigint", nullable: false),
                    ResetDeactivation = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttestationOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttestationRewardOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    Expected = table.Column<long>(type: "bigint", nullable: false),
                    RewardDelegated = table.Column<long>(type: "bigint", nullable: false),
                    RewardStakedOwn = table.Column<long>(type: "bigint", nullable: false),
                    RewardStakedEdge = table.Column<long>(type: "bigint", nullable: false),
                    RewardStakedShared = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttestationRewardOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutostakingOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    StakingUpdatesCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutostakingOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BakerCycles",
                columns: table => new
                {
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Cycle = table.Column<int>(type: "integer", nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    OwnDelegatedBalance = table.Column<long>(type: "bigint", nullable: false),
                    ExternalDelegatedBalance = table.Column<long>(type: "bigint", nullable: false),
                    DelegatorsCount = table.Column<int>(type: "integer", nullable: false),
                    OwnStakedBalance = table.Column<long>(type: "bigint", nullable: false),
                    ExternalStakedBalance = table.Column<long>(type: "bigint", nullable: false),
                    StakersCount = table.Column<int>(type: "integer", nullable: false),
                    IssuedPseudotokens = table.Column<BigInteger>(type: "numeric", nullable: true),
                    BakingPower = table.Column<long>(type: "bigint", nullable: false),
                    TotalBakingPower = table.Column<long>(type: "bigint", nullable: false),
                    FutureBlocks = table.Column<int>(type: "integer", nullable: false),
                    Blocks = table.Column<int>(type: "integer", nullable: false),
                    MissedBlocks = table.Column<int>(type: "integer", nullable: false),
                    FutureAttestations = table.Column<int>(type: "integer", nullable: false),
                    Attestations = table.Column<int>(type: "integer", nullable: false),
                    MissedAttestations = table.Column<int>(type: "integer", nullable: false),
                    FutureBlockRewards = table.Column<long>(type: "bigint", nullable: false),
                    MissedBlockRewards = table.Column<long>(type: "bigint", nullable: false),
                    BlockRewardsDelegated = table.Column<long>(type: "bigint", nullable: false),
                    BlockRewardsStakedOwn = table.Column<long>(type: "bigint", nullable: false),
                    BlockRewardsStakedEdge = table.Column<long>(type: "bigint", nullable: false),
                    BlockRewardsStakedShared = table.Column<long>(type: "bigint", nullable: false),
                    FutureAttestationRewards = table.Column<long>(type: "bigint", nullable: false),
                    MissedAttestationRewards = table.Column<long>(type: "bigint", nullable: false),
                    AttestationRewardsDelegated = table.Column<long>(type: "bigint", nullable: false),
                    AttestationRewardsStakedOwn = table.Column<long>(type: "bigint", nullable: false),
                    AttestationRewardsStakedEdge = table.Column<long>(type: "bigint", nullable: false),
                    AttestationRewardsStakedShared = table.Column<long>(type: "bigint", nullable: false),
                    FutureDalAttestationRewards = table.Column<long>(type: "bigint", nullable: false),
                    MissedDalAttestationRewards = table.Column<long>(type: "bigint", nullable: false),
                    DalAttestationRewardsDelegated = table.Column<long>(type: "bigint", nullable: false),
                    DalAttestationRewardsStakedOwn = table.Column<long>(type: "bigint", nullable: false),
                    DalAttestationRewardsStakedEdge = table.Column<long>(type: "bigint", nullable: false),
                    DalAttestationRewardsStakedShared = table.Column<long>(type: "bigint", nullable: false),
                    BlockFees = table.Column<long>(type: "bigint", nullable: false),
                    MissedBlockFees = table.Column<long>(type: "bigint", nullable: false),
                    DoubleBakingRewards = table.Column<long>(type: "bigint", nullable: false),
                    DoubleBakingLostStaked = table.Column<long>(type: "bigint", nullable: false),
                    DoubleBakingLostUnstaked = table.Column<long>(type: "bigint", nullable: false),
                    DoubleBakingLostExternalStaked = table.Column<long>(type: "bigint", nullable: false),
                    DoubleBakingLostExternalUnstaked = table.Column<long>(type: "bigint", nullable: false),
                    DoubleConsensusRewards = table.Column<long>(type: "bigint", nullable: false),
                    DoubleConsensusLostStaked = table.Column<long>(type: "bigint", nullable: false),
                    DoubleConsensusLostUnstaked = table.Column<long>(type: "bigint", nullable: false),
                    DoubleConsensusLostExternalStaked = table.Column<long>(type: "bigint", nullable: false),
                    DoubleConsensusLostExternalUnstaked = table.Column<long>(type: "bigint", nullable: false),
                    VdfRevelationRewardsDelegated = table.Column<long>(type: "bigint", nullable: false),
                    VdfRevelationRewardsStakedOwn = table.Column<long>(type: "bigint", nullable: false),
                    VdfRevelationRewardsStakedEdge = table.Column<long>(type: "bigint", nullable: false),
                    VdfRevelationRewardsStakedShared = table.Column<long>(type: "bigint", nullable: false),
                    NonceRevelationRewardsDelegated = table.Column<long>(type: "bigint", nullable: false),
                    NonceRevelationRewardsStakedOwn = table.Column<long>(type: "bigint", nullable: false),
                    NonceRevelationRewardsStakedEdge = table.Column<long>(type: "bigint", nullable: false),
                    NonceRevelationRewardsStakedShared = table.Column<long>(type: "bigint", nullable: false),
                    NonceRevelationLosses = table.Column<long>(type: "bigint", nullable: false),
                    ExpectedBlocks = table.Column<double>(type: "double precision", nullable: false),
                    ExpectedAttestations = table.Column<double>(type: "double precision", nullable: false),
                    ExpectedDalAttestations = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BakerCycles", x => new { x.ChainId, x.Cycle, x.BakerId });
                });

            migrationBuilder.CreateTable(
                name: "BakingRights",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Cycle = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: true),
                    Slots = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BakingRights", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BallotOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    Epoch = table.Column<int>(type: "integer", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    ProposalId = table.Column<int>(type: "integer", nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    VotingPower = table.Column<long>(type: "bigint", nullable: false),
                    Vote = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BallotOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BigMapKeys",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    BigMapId = table.Column<int>(type: "integer", nullable: false),
                    FirstLevel = table.Column<int>(type: "integer", nullable: false),
                    FirstTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false),
                    LastTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Updates = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    KeyHash = table.Column<string>(type: "character(54)", fixedLength: true, maxLength: 54, nullable: false),
                    RawKey = table.Column<byte[]>(type: "bytea", nullable: false),
                    JsonKey = table.Column<string>(type: "jsonb", nullable: false),
                    RawValue = table.Column<byte[]>(type: "bytea", nullable: false),
                    JsonValue = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BigMapKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BigMaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Ptr = table.Column<int>(type: "integer", nullable: false),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    StoragePath = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    KeyType = table.Column<byte[]>(type: "bytea", nullable: false),
                    ValueType = table.Column<byte[]>(type: "bytea", nullable: false),
                    FirstLevel = table.Column<int>(type: "integer", nullable: false),
                    FirstTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false),
                    LastTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalKeys = table.Column<int>(type: "integer", nullable: false),
                    ActiveKeys = table.Column<int>(type: "integer", nullable: false),
                    Updates = table.Column<int>(type: "integer", nullable: false),
                    Tags = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BigMaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BigMapUpdates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    BigMapId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    OriginationId = table.Column<long>(type: "bigint", nullable: true),
                    TransactionId = table.Column<long>(type: "bigint", nullable: true),
                    MigrationId = table.Column<long>(type: "bigint", nullable: true),
                    BigMapKeyId = table.Column<long>(type: "bigint", nullable: true),
                    RawValue = table.Column<byte[]>(type: "bytea", nullable: true),
                    JsonValue = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BigMapUpdates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Blocks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Layer = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProtocolId = table.Column<int>(type: "integer", nullable: false),
                    OpsCounter = table.Column<int>(type: "integer", nullable: true),
                    SubsCounter = table.Column<int>(type: "integer", nullable: true),
                    Extras = table.Column<string>(type: "jsonb", nullable: true),
                    Cycle = table.Column<int>(type: "integer", nullable: true),
                    SoftwareId = table.Column<int>(type: "integer", nullable: true),
                    PayloadRound = table.Column<int>(type: "integer", nullable: true),
                    BlockRound = table.Column<int>(type: "integer", nullable: true),
                    AttestationPower = table.Column<long>(type: "bigint", nullable: true),
                    AttestationCommittee = table.Column<long>(type: "bigint", nullable: true),
                    Events = table.Column<int>(type: "integer", nullable: true),
                    Operations = table.Column<long>(type: "bigint", nullable: true),
                    RewardDelegated = table.Column<long>(type: "bigint", nullable: true),
                    RewardStakedOwn = table.Column<long>(type: "bigint", nullable: true),
                    RewardStakedEdge = table.Column<long>(type: "bigint", nullable: true),
                    RewardStakedShared = table.Column<long>(type: "bigint", nullable: true),
                    BonusDelegated = table.Column<long>(type: "bigint", nullable: true),
                    BonusStakedOwn = table.Column<long>(type: "bigint", nullable: true),
                    BonusStakedEdge = table.Column<long>(type: "bigint", nullable: true),
                    BonusStakedShared = table.Column<long>(type: "bigint", nullable: true),
                    BakerFees = table.Column<long>(type: "bigint", nullable: true),
                    BurnedFees = table.Column<long>(type: "bigint", nullable: true),
                    ProposerId = table.Column<int>(type: "integer", nullable: true),
                    ProducerId = table.Column<int>(type: "integer", nullable: true),
                    RevelationId = table.Column<long>(type: "bigint", nullable: true),
                    ResetBakerDeactivation = table.Column<int>(type: "integer", nullable: true),
                    ResetProposerDeactivation = table.Column<int>(type: "integer", nullable: true),
                    LBToggle = table.Column<bool>(type: "boolean", nullable: true),
                    LBToggleEma = table.Column<int>(type: "integer", nullable: true),
                    BakerFees18 = table.Column<BigInteger>(type: "numeric", nullable: true),
                    BurnedFees18 = table.Column<BigInteger>(type: "numeric", nullable: true),
                    MichelsonHash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BridgeTicketBalances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    FirstLevel = table.Column<int>(type: "integer", nullable: false),
                    FirstTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false),
                    LastTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TransfersCount = table.Column<int>(type: "integer", nullable: false),
                    Balance = table.Column<BigInteger>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BridgeTicketBalances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BridgeTickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    WeakHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    FirstLevel = table.Column<int>(type: "integer", nullable: false),
                    FirstTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false),
                    LastTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TransfersCount = table.Column<int>(type: "integer", nullable: false),
                    BalancesCount = table.Column<int>(type: "integer", nullable: false),
                    HoldersCount = table.Column<int>(type: "integer", nullable: false),
                    TotalMinted = table.Column<BigInteger>(type: "numeric", nullable: false),
                    TotalBurned = table.Column<BigInteger>(type: "numeric", nullable: false),
                    TotalSupply = table.Column<BigInteger>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BridgeTickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BridgeTicketTransfers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<BigInteger>(type: "numeric", nullable: false),
                    FromId = table.Column<int>(type: "integer", nullable: true),
                    ToId = table.Column<int>(type: "integer", nullable: true),
                    TransactionId = table.Column<long>(type: "bigint", nullable: true),
                    DepositId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BridgeTicketTransfers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Chains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Layer = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<string>(type: "text", nullable: false),
                    Network = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false),
                    KnownLevel = table.Column<int>(type: "integer", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AddressCounter = table.Column<int>(type: "integer", nullable: false),
                    OperationCounter = table.Column<long>(type: "bigint", nullable: false),
                    BigMapCounter = table.Column<int>(type: "integer", nullable: false),
                    BigMapKeyCounter = table.Column<long>(type: "bigint", nullable: false),
                    BigMapUpdateCounter = table.Column<long>(type: "bigint", nullable: false),
                    StorageCounter = table.Column<long>(type: "bigint", nullable: false),
                    ScriptCounter = table.Column<int>(type: "integer", nullable: false),
                    LogsCounter = table.Column<long>(type: "bigint", nullable: false),
                    ProtocolsCount = table.Column<int>(type: "integer", nullable: false),
                    BlocksCount = table.Column<int>(type: "integer", nullable: false),
                    RevealOpsCount = table.Column<long>(type: "bigint", nullable: false),
                    TransactionOpsCount = table.Column<long>(type: "bigint", nullable: false),
                    OriginationOpsCount = table.Column<long>(type: "bigint", nullable: false),
                    RegisterConstantOpsCount = table.Column<long>(type: "bigint", nullable: false),
                    IncreasePaidStorageOpsCount = table.Column<long>(type: "bigint", nullable: false),
                    TransferTicketOpsCount = table.Column<long>(type: "bigint", nullable: false),
                    MigrationOpsCount = table.Column<long>(type: "bigint", nullable: false),
                    TokensCount = table.Column<int>(type: "integer", nullable: false),
                    TokenBalancesCount = table.Column<int>(type: "integer", nullable: false),
                    TokenTransfersCount = table.Column<long>(type: "bigint", nullable: false),
                    TicketsCount = table.Column<int>(type: "integer", nullable: false),
                    TicketBalancesCount = table.Column<int>(type: "integer", nullable: false),
                    TicketTransfersCount = table.Column<int>(type: "integer", nullable: false),
                    LogsCount = table.Column<long>(type: "bigint", nullable: false),
                    ConstantsCount = table.Column<int>(type: "integer", nullable: false),
                    Extras = table.Column<string>(type: "jsonb", nullable: true),
                    Cycle = table.Column<int>(type: "integer", nullable: true),
                    Protocol = table.Column<string>(type: "text", nullable: true),
                    NextProtocol = table.Column<string>(type: "text", nullable: true),
                    VotingEpoch = table.Column<int>(type: "integer", nullable: true),
                    VotingPeriod = table.Column<int>(type: "integer", nullable: true),
                    AiActivationLevel = table.Column<int>(type: "integer", nullable: true),
                    AbaActivationLevel = table.Column<int>(type: "integer", nullable: true),
                    PendingBakerParameters = table.Column<int>(type: "integer", nullable: true),
                    PendingSecondaryKeys = table.Column<int>(type: "integer", nullable: true),
                    ManagerCounter = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupCommitmentCounter = table.Column<int>(type: "integer", nullable: true),
                    RefutationGameCounter = table.Column<int>(type: "integer", nullable: true),
                    InboxMessageCounter = table.Column<int>(type: "integer", nullable: true),
                    ProposalCounter = table.Column<int>(type: "integer", nullable: true),
                    SoftwareCounter = table.Column<int>(type: "integer", nullable: true),
                    CommitmentsCount = table.Column<int>(type: "integer", nullable: true),
                    ActivationOpsCount = table.Column<int>(type: "integer", nullable: true),
                    BallotOpsCount = table.Column<int>(type: "integer", nullable: true),
                    DelegationOpsCount = table.Column<int>(type: "integer", nullable: true),
                    DalEntrapmentEvidenceOpsCount = table.Column<int>(type: "integer", nullable: true),
                    DoubleBakingOpsCount = table.Column<int>(type: "integer", nullable: true),
                    DoubleConsensusOpsCount = table.Column<int>(type: "integer", nullable: true),
                    AttestationOpsCount = table.Column<long>(type: "bigint", nullable: true),
                    PreattestationOpsCount = table.Column<int>(type: "integer", nullable: true),
                    NonceRevelationOpsCount = table.Column<int>(type: "integer", nullable: true),
                    VdfRevelationOpsCount = table.Column<int>(type: "integer", nullable: true),
                    ProposalOpsCount = table.Column<int>(type: "integer", nullable: true),
                    StakingOpsCount = table.Column<int>(type: "integer", nullable: true),
                    SetDelegateParametersOpsCount = table.Column<int>(type: "integer", nullable: true),
                    AttestationRewardOpsCount = table.Column<int>(type: "integer", nullable: true),
                    DalAttestationRewardOpsCount = table.Column<int>(type: "integer", nullable: true),
                    SetDepositsLimitOpsCount = table.Column<int>(type: "integer", nullable: true),
                    UpdateSecondaryKeyOpsCount = table.Column<int>(type: "integer", nullable: true),
                    DrainDelegateOpsCount = table.Column<int>(type: "integer", nullable: true),
                    SubsidyOpsCount = table.Column<int>(type: "integer", nullable: true),
                    RevelationPenaltyOpsCount = table.Column<int>(type: "integer", nullable: true),
                    AutostakingOpsCount = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupAddMessagesOpsCount = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupCementOpsCount = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupExecuteOpsCount = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupOriginateOpsCount = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupPublishOpsCount = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupRecoverBondOpsCount = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupRefuteOpsCount = table.Column<int>(type: "integer", nullable: true),
                    DalPublishCommitmentOpsCount = table.Column<int>(type: "integer", nullable: true),
                    CyclesCount = table.Column<int>(type: "integer", nullable: true),
                    StakingUpdatesCount = table.Column<int>(type: "integer", nullable: true),
                    UnstakeRequestsCount = table.Column<int>(type: "integer", nullable: true),
                    QuoteLevel = table.Column<int>(type: "integer", nullable: true),
                    QuoteBtc = table.Column<double>(type: "double precision", nullable: true),
                    QuoteEur = table.Column<double>(type: "double precision", nullable: true),
                    QuoteUsd = table.Column<double>(type: "double precision", nullable: true),
                    QuoteCny = table.Column<double>(type: "double precision", nullable: true),
                    QuoteJpy = table.Column<double>(type: "double precision", nullable: true),
                    QuoteKrw = table.Column<double>(type: "double precision", nullable: true),
                    QuoteEth = table.Column<double>(type: "double precision", nullable: true),
                    QuoteGbp = table.Column<double>(type: "double precision", nullable: true),
                    DomainsNameRegistry = table.Column<string>(type: "text", nullable: true),
                    DomainsLevel = table.Column<int>(type: "integer", nullable: true),
                    RollupAddress = table.Column<string>(type: "text", nullable: true),
                    Kernel = table.Column<string>(type: "text", nullable: true),
                    KernelUpgrade = table.Column<string>(type: "text", nullable: true),
                    KernelUpgradeTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MichelsonActivationLevel = table.Column<int>(type: "integer", nullable: true),
                    MichelsonChainId = table.Column<string>(type: "text", nullable: true),
                    MichelsonProtocol = table.Column<string>(type: "text", nullable: true),
                    MichelsonBlock = table.Column<string>(type: "text", nullable: true),
                    DepositOpsCount = table.Column<long>(type: "bigint", nullable: true),
                    Eip7702DelegationCount = table.Column<int>(type: "integer", nullable: true),
                    BridgeTicketsCount = table.Column<int>(type: "integer", nullable: true),
                    BridgeTicketBalancesCount = table.Column<int>(type: "integer", nullable: true),
                    BridgeTicketTransfersCount = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Commitments",
                columns: table => new
                {
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Hash = table.Column<string>(type: "character(37)", fixedLength: true, maxLength: 37, nullable: false),
                    Balance = table.Column<long>(type: "bigint", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commitments", x => new { x.ChainId, x.Hash });
                });

            migrationBuilder.CreateTable(
                name: "Cycles",
                columns: table => new
                {
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    FirstLevel = table.Column<int>(type: "integer", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false),
                    Seed = table.Column<byte[]>(type: "bytea", fixedLength: true, maxLength: 32, nullable: false),
                    SnapshotLevel = table.Column<int>(type: "integer", nullable: false),
                    TotalBakers = table.Column<int>(type: "integer", nullable: false),
                    TotalBakingPower = table.Column<long>(type: "bigint", nullable: false),
                    BlockReward = table.Column<long>(type: "bigint", nullable: false),
                    BlockBonusPerBlock = table.Column<long>(type: "bigint", nullable: false),
                    AttestationRewardPerBlock = table.Column<long>(type: "bigint", nullable: false),
                    NonceRevelationReward = table.Column<long>(type: "bigint", nullable: false),
                    VdfRevelationReward = table.Column<long>(type: "bigint", nullable: false),
                    DalAttestationRewardPerShard = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cycles", x => new { x.ChainId, x.Index });
                });

            migrationBuilder.CreateTable(
                name: "DalAttestationRewardOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    Expected = table.Column<long>(type: "bigint", nullable: false),
                    RewardDelegated = table.Column<long>(type: "bigint", nullable: false),
                    RewardStakedOwn = table.Column<long>(type: "bigint", nullable: false),
                    RewardStakedEdge = table.Column<long>(type: "bigint", nullable: false),
                    RewardStakedShared = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DalAttestationRewardOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DalEntrapmentEvidenceOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    AccuserId = table.Column<int>(type: "integer", nullable: false),
                    OffenderId = table.Column<int>(type: "integer", nullable: false),
                    TrapLevel = table.Column<int>(type: "integer", nullable: false),
                    TrapSlotIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DalEntrapmentEvidenceOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DalPublishCommitmentOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    BakerFee = table.Column<long>(type: "bigint", nullable: false),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    AllocationFee = table.Column<long>(type: "bigint", nullable: true),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    StorageLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    Commitment = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DalPublishCommitmentOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DelegationOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    BakerFee = table.Column<long>(type: "bigint", nullable: false),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    AllocationFee = table.Column<long>(type: "bigint", nullable: true),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    StorageLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    InitiatorId = table.Column<int>(type: "integer", nullable: true),
                    Nonce = table.Column<int>(type: "integer", nullable: true),
                    SenderCodeHash = table.Column<int>(type: "integer", nullable: true),
                    BakerId = table.Column<int>(type: "integer", nullable: true),
                    PrevBakerId = table.Column<int>(type: "integer", nullable: true),
                    PrevDelegationLevel = table.Column<int>(type: "integer", nullable: true),
                    PrevDelegationTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrevDeactivationLevel = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    StakingUpdatesCount = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegationOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DelegationSnapshots",
                columns: table => new
                {
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    OwnDelegatedBalance = table.Column<long>(type: "bigint", nullable: false),
                    ExternalDelegatedBalance = table.Column<long>(type: "bigint", nullable: true),
                    DelegatorsCount = table.Column<int>(type: "integer", nullable: true),
                    PrevMinTotalDelegatedLevel = table.Column<int>(type: "integer", nullable: true),
                    PrevMinTotalDelegated = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegationSnapshots", x => new { x.ChainId, x.Level, x.BakerId, x.AddressId });
                });

            migrationBuilder.CreateTable(
                name: "DelegatorCycles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Cycle = table.Column<int>(type: "integer", nullable: false),
                    DelegatorId = table.Column<int>(type: "integer", nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    DelegatedBalance = table.Column<long>(type: "bigint", nullable: false),
                    StakedPseudotokens = table.Column<BigInteger>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegatorCycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DepositOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Runtime = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    InboxLevel = table.Column<int>(type: "integer", nullable: false),
                    InboxMessageId = table.Column<int>(type: "integer", nullable: false),
                    ReceiverId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Amount18 = table.Column<BigInteger>(type: "numeric", nullable: true),
                    TicketHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    ProxyId = table.Column<int>(type: "integer", nullable: true),
                    DepositId = table.Column<BigInteger>(type: "numeric", nullable: true),
                    ClaimTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    SubsCounter = table.Column<int>(type: "integer", nullable: true),
                    LogsCount = table.Column<int>(type: "integer", nullable: true),
                    BridgeTicketTransfers = table.Column<int>(type: "integer", nullable: true),
                    GasUsed = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepositOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Domains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Owner = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Reverse = table.Column<bool>(type: "boolean", nullable: false),
                    Expiration = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Data = table.Column<JsonElement>(type: "jsonb", nullable: true),
                    FirstLevel = table.Column<int>(type: "integer", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Domains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DoubleBakingOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    AccusedLevel = table.Column<int>(type: "integer", nullable: false),
                    SlashedLevel = table.Column<int>(type: "integer", nullable: false),
                    AccuserId = table.Column<int>(type: "integer", nullable: false),
                    OffenderId = table.Column<int>(type: "integer", nullable: false),
                    Reward = table.Column<long>(type: "bigint", nullable: false),
                    LostStaked = table.Column<long>(type: "bigint", nullable: false),
                    LostUnstaked = table.Column<long>(type: "bigint", nullable: false),
                    LostExternalStaked = table.Column<long>(type: "bigint", nullable: false),
                    LostExternalUnstaked = table.Column<long>(type: "bigint", nullable: false),
                    StakingUpdatesCount = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoubleBakingOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DoubleConsensusOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    AccusedLevel = table.Column<int>(type: "integer", nullable: false),
                    SlashedLevel = table.Column<int>(type: "integer", nullable: false),
                    AccuserId = table.Column<int>(type: "integer", nullable: false),
                    OffenderId = table.Column<int>(type: "integer", nullable: false),
                    Reward = table.Column<long>(type: "bigint", nullable: false),
                    LostStaked = table.Column<long>(type: "bigint", nullable: false),
                    LostUnstaked = table.Column<long>(type: "bigint", nullable: false),
                    LostExternalStaked = table.Column<long>(type: "bigint", nullable: false),
                    LostExternalUnstaked = table.Column<long>(type: "bigint", nullable: false),
                    StakingUpdatesCount = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoubleConsensusOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrainDelegateOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Fee = table.Column<long>(type: "bigint", nullable: false),
                    AllocationFee = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrainDelegateOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Eip7702Delegations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TransactionId = table.Column<long>(type: "bigint", nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    AuthorityId = table.Column<int>(type: "integer", nullable: false),
                    Nonce = table.Column<int>(type: "integer", nullable: false),
                    PrevDelegateId = table.Column<int>(type: "integer", nullable: true),
                    DelegateId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Eip7702Delegations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    PredecessorLevel = table.Column<int>(type: "integer", nullable: true),
                    OperationId = table.Column<long>(type: "bigint", nullable: true),
                    Payload = table.Column<byte[]>(type: "bytea", nullable: true),
                    Protocol = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IncreasePaidStorageOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Layer = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    StorageLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<BigInteger>(type: "numeric", nullable: false),
                    BakerFee = table.Column<long>(type: "bigint", nullable: true),
                    DaFee = table.Column<long>(type: "bigint", nullable: true),
                    GasFee = table.Column<long>(type: "bigint", nullable: true),
                    GasRefund = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncreasePaidStorageOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Logs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Runtime = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    ContractTypeHash = table.Column<int>(type: "integer", nullable: false),
                    ContractCodeHash = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Payload = table.Column<string>(type: "jsonb", nullable: true),
                    Guessed = table.Column<bool>(type: "boolean", nullable: true),
                    TransactionId = table.Column<long>(type: "bigint", nullable: true),
                    OriginationId = table.Column<long>(type: "bigint", nullable: true),
                    DepositId = table.Column<long>(type: "bigint", nullable: true),
                    Topics = table.Column<byte[][]>(type: "bytea[]", nullable: true),
                    Data = table.Column<byte[]>(type: "bytea", nullable: true),
                    Type = table.Column<byte[]>(type: "bytea", nullable: true),
                    PayloadRaw = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MigrationOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Runtime = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    ScriptId = table.Column<int>(type: "integer", nullable: true),
                    BalanceChange18 = table.Column<BigInteger>(type: "numeric", nullable: true),
                    NonceChange = table.Column<int>(type: "integer", nullable: true),
                    BalanceChange = table.Column<long>(type: "bigint", nullable: true),
                    StorageId = table.Column<long>(type: "bigint", nullable: true),
                    BigMapUpdates = table.Column<int>(type: "integer", nullable: true),
                    TokenTransfers = table.Column<int>(type: "integer", nullable: true),
                    SubsCounter = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NonceRevelationOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    RevealedLevel = table.Column<int>(type: "integer", nullable: false),
                    RevealedCycle = table.Column<int>(type: "integer", nullable: false),
                    RewardDelegated = table.Column<long>(type: "bigint", nullable: false),
                    RewardStakedOwn = table.Column<long>(type: "bigint", nullable: false),
                    RewardStakedEdge = table.Column<long>(type: "bigint", nullable: false),
                    RewardStakedShared = table.Column<long>(type: "bigint", nullable: false),
                    Nonce = table.Column<byte[]>(type: "bytea", fixedLength: true, maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NonceRevelationOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OriginationOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Env = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    InitiatorId = table.Column<int>(type: "integer", nullable: true),
                    SenderCodeHash = table.Column<int>(type: "integer", nullable: true),
                    ContractId = table.Column<int>(type: "integer", nullable: true),
                    ContractCodeHash = table.Column<int>(type: "integer", nullable: true),
                    ScriptId = table.Column<int>(type: "integer", nullable: true),
                    TokenTransfers = table.Column<int>(type: "integer", nullable: true),
                    SubsCounter = table.Column<int>(type: "integer", nullable: true),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    AllocationFee = table.Column<long>(type: "bigint", nullable: true),
                    StorageLimit = table.Column<int>(type: "integer", nullable: true),
                    StorageUsed = table.Column<int>(type: "integer", nullable: true),
                    Nonce = table.Column<int>(type: "integer", nullable: true),
                    StorageId = table.Column<long>(type: "bigint", nullable: true),
                    BigMapUpdates = table.Column<int>(type: "integer", nullable: true),
                    Balance = table.Column<long>(type: "bigint", nullable: true),
                    BakerFee = table.Column<long>(type: "bigint", nullable: true),
                    BakerId = table.Column<int>(type: "integer", nullable: true),
                    DaFee = table.Column<long>(type: "bigint", nullable: true),
                    GasFee = table.Column<long>(type: "bigint", nullable: true),
                    GasRefund = table.Column<long>(type: "bigint", nullable: true),
                    OpType = table.Column<int>(type: "integer", nullable: true),
                    OpCode = table.Column<int>(type: "integer", nullable: true),
                    GasPrice = table.Column<BigInteger>(type: "numeric", nullable: true),
                    MaxFeePerGas = table.Column<BigInteger>(type: "numeric", nullable: true),
                    MaxPriorityFeePerGas = table.Column<BigInteger>(type: "numeric", nullable: true),
                    EffectiveGasPrice = table.Column<BigInteger>(type: "numeric", nullable: true),
                    DaFee18 = table.Column<BigInteger>(type: "numeric", nullable: true),
                    GasFee18 = table.Column<BigInteger>(type: "numeric", nullable: true),
                    Balance18 = table.Column<BigInteger>(type: "numeric", nullable: true),
                    InternalOperations = table.Column<int>(type: "integer", nullable: true),
                    LogsCount = table.Column<int>(type: "integer", nullable: true),
                    ReOriginated = table.Column<bool>(type: "boolean", nullable: true),
                    NonceConsumed = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OriginationOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PreattestationOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    Power = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreattestationOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProposalOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    Epoch = table.Column<int>(type: "integer", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    ProposalId = table.Column<int>(type: "integer", nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    VotingPower = table.Column<long>(type: "bigint", nullable: false),
                    Duplicated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Proposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    InitiatorId = table.Column<int>(type: "integer", nullable: false),
                    FirstPeriod = table.Column<int>(type: "integer", nullable: false),
                    LastPeriod = table.Column<int>(type: "integer", nullable: false),
                    Epoch = table.Column<int>(type: "integer", nullable: false),
                    Upvotes = table.Column<int>(type: "integer", nullable: false),
                    VotingPower = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Extras = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proposals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Protocols",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Layer = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    FirstLevel = table.Column<int>(type: "integer", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false),
                    Extras = table.Column<string>(type: "jsonb", nullable: true),
                    FirstCycle = table.Column<int>(type: "integer", nullable: true),
                    FirstCycleLevel = table.Column<int>(type: "integer", nullable: true),
                    RampUpCycles = table.Column<int>(type: "integer", nullable: true),
                    NoRewardCycles = table.Column<int>(type: "integer", nullable: true),
                    ConsensusRightsDelay = table.Column<int>(type: "integer", nullable: true),
                    BakerParametersActivationDelay = table.Column<int>(type: "integer", nullable: true),
                    BlocksPerCycle = table.Column<int>(type: "integer", nullable: true),
                    BlocksPerCommitment = table.Column<int>(type: "integer", nullable: true),
                    BlocksPerSnapshot = table.Column<int>(type: "integer", nullable: true),
                    BlocksPerVoting = table.Column<int>(type: "integer", nullable: true),
                    TimeBetweenBlocks = table.Column<int>(type: "integer", nullable: true),
                    AttestersPerBlock = table.Column<int>(type: "integer", nullable: true),
                    HardOperationGasLimit = table.Column<int>(type: "integer", nullable: true),
                    HardOperationStorageLimit = table.Column<int>(type: "integer", nullable: true),
                    HardBlockGasLimit = table.Column<int>(type: "integer", nullable: true),
                    MinimalStake = table.Column<long>(type: "bigint", nullable: true),
                    MinimalFrozenStake = table.Column<long>(type: "bigint", nullable: true),
                    BlockDeposit = table.Column<long>(type: "bigint", nullable: true),
                    BlockReward0 = table.Column<long>(type: "bigint", nullable: true),
                    BlockReward1 = table.Column<long>(type: "bigint", nullable: true),
                    MaxBakingReward = table.Column<long>(type: "bigint", nullable: true),
                    AttestationDeposit = table.Column<long>(type: "bigint", nullable: true),
                    AttestationReward0 = table.Column<long>(type: "bigint", nullable: true),
                    AttestationReward1 = table.Column<long>(type: "bigint", nullable: true),
                    MaxAttestationReward = table.Column<long>(type: "bigint", nullable: true),
                    OriginationSize = table.Column<int>(type: "integer", nullable: true),
                    ByteCost = table.Column<int>(type: "integer", nullable: true),
                    ProposalQuorum = table.Column<int>(type: "integer", nullable: true),
                    BallotQuorumMin = table.Column<int>(type: "integer", nullable: true),
                    BallotQuorumMax = table.Column<int>(type: "integer", nullable: true),
                    LBToggleThreshold = table.Column<int>(type: "integer", nullable: true),
                    ConsensusThreshold = table.Column<int>(type: "integer", nullable: true),
                    MinParticipationNumerator = table.Column<int>(type: "integer", nullable: true),
                    MinParticipationDenominator = table.Column<int>(type: "integer", nullable: true),
                    DenunciationPeriod = table.Column<int>(type: "integer", nullable: true),
                    SlashingDelay = table.Column<int>(type: "integer", nullable: true),
                    MaxDelegatedOverFrozenRatio = table.Column<int>(type: "integer", nullable: true),
                    MaxExternalOverOwnStakeRatio = table.Column<int>(type: "integer", nullable: true),
                    StakePowerMultiplier = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupOriginationSize = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupStakeAmount = table.Column<long>(type: "bigint", nullable: true),
                    SmartRollupChallengeWindow = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupCommitmentPeriod = table.Column<int>(type: "integer", nullable: true),
                    SmartRollupTimeoutPeriod = table.Column<int>(type: "integer", nullable: true),
                    Dictator = table.Column<string>(type: "text", nullable: true),
                    DoubleBakingSlashedPercentage = table.Column<int>(type: "integer", nullable: true),
                    DoubleConsensusSlashedPercentage = table.Column<int>(type: "integer", nullable: true),
                    NumberOfShards = table.Column<int>(type: "integer", nullable: true),
                    ToleratedInactivityPeriod = table.Column<int>(type: "integer", nullable: true),
                    MichelsonHash = table.Column<string>(type: "text", nullable: true),
                    MinBlockTimeMs = table.Column<int>(type: "integer", nullable: true),
                    MaxBlockTimeMs = table.Column<int>(type: "integer", nullable: true),
                    DaFeePerByte = table.Column<long>(type: "bigint", nullable: true),
                    DaFeePerByte18 = table.Column<BigInteger>(type: "numeric", nullable: true),
                    HardEvmBlockGasLimit = table.Column<long>(type: "bigint", nullable: true),
                    HardEvmOperationGasLimit = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Protocols", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Quotes",
                columns: table => new
                {
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Btc = table.Column<double>(type: "double precision", nullable: false),
                    Eur = table.Column<double>(type: "double precision", nullable: false),
                    Usd = table.Column<double>(type: "double precision", nullable: false),
                    Cny = table.Column<double>(type: "double precision", nullable: false),
                    Jpy = table.Column<double>(type: "double precision", nullable: false),
                    Krw = table.Column<double>(type: "double precision", nullable: false),
                    Eth = table.Column<double>(type: "double precision", nullable: false),
                    Gbp = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => new { x.ChainId, x.Level });
                });

            migrationBuilder.CreateTable(
                name: "RefutationGames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    SmartRollupId = table.Column<int>(type: "integer", nullable: false),
                    InitiatorId = table.Column<int>(type: "integer", nullable: false),
                    OpponentId = table.Column<int>(type: "integer", nullable: false),
                    InitiatorCommitmentId = table.Column<int>(type: "integer", nullable: false),
                    OpponentCommitmentId = table.Column<int>(type: "integer", nullable: false),
                    LastMoveId = table.Column<long>(type: "bigint", nullable: false),
                    FirstLevel = table.Column<int>(type: "integer", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false),
                    InitiatorReward = table.Column<long>(type: "bigint", nullable: true),
                    InitiatorLoss = table.Column<long>(type: "bigint", nullable: true),
                    OpponentReward = table.Column<long>(type: "bigint", nullable: true),
                    OpponentLoss = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefutationGames", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegisterConstantOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Layer = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    StorageLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "character varying(54)", maxLength: 54, nullable: true),
                    Value = table.Column<byte[]>(type: "bytea", nullable: true),
                    Refs = table.Column<int>(type: "integer", nullable: true),
                    Extras = table.Column<string>(type: "jsonb", nullable: true),
                    BakerFee = table.Column<long>(type: "bigint", nullable: true),
                    DaFee = table.Column<long>(type: "bigint", nullable: true),
                    GasFee = table.Column<long>(type: "bigint", nullable: true),
                    GasRefund = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisterConstantOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RevealOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Layer = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    AllocationFee = table.Column<long>(type: "bigint", nullable: true),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    StorageLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    BakerFee = table.Column<long>(type: "bigint", nullable: true),
                    DaFee = table.Column<long>(type: "bigint", nullable: true),
                    GasFee = table.Column<long>(type: "bigint", nullable: true),
                    GasRefund = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevealOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RevelationPenaltyOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    MissedLevel = table.Column<int>(type: "integer", nullable: false),
                    Loss = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevelationPenaltyOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Scripts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Runtime = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    OriginationId = table.Column<long>(type: "bigint", nullable: true),
                    MigrationId = table.Column<long>(type: "bigint", nullable: true),
                    Current = table.Column<bool>(type: "boolean", nullable: false),
                    TypeHash = table.Column<int>(type: "integer", nullable: false),
                    CodeHash = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<byte[]>(type: "bytea", nullable: true),
                    AbiJson = table.Column<string>(type: "text", nullable: true),
                    SolidityMetadataBzzr0 = table.Column<string>(type: "text", nullable: true),
                    SolidityMetadataBzzr1 = table.Column<string>(type: "text", nullable: true),
                    SolidityMetadataIpfs = table.Column<string>(type: "text", nullable: true),
                    SolidityMetadataSolc = table.Column<string>(type: "text", nullable: true),
                    SolidityMetadataExperimental = table.Column<bool>(type: "boolean", nullable: true),
                    ParameterSchema = table.Column<byte[]>(type: "bytea", nullable: true),
                    StorageSchema = table.Column<byte[]>(type: "bytea", nullable: true),
                    CodeSchema = table.Column<byte[]>(type: "bytea", nullable: true),
                    Views = table.Column<byte[][]>(type: "bytea[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scripts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SetDelegateParametersOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    BakerFee = table.Column<long>(type: "bigint", nullable: false),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    AllocationFee = table.Column<long>(type: "bigint", nullable: true),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    StorageLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    LimitOfStakingOverBaking = table.Column<long>(type: "bigint", nullable: true),
                    EdgeOfBakingOverStaking = table.Column<long>(type: "bigint", nullable: true),
                    ActivationCycle = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetDelegateParametersOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SetDepositsLimitOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    BakerFee = table.Column<long>(type: "bigint", nullable: false),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    AllocationFee = table.Column<long>(type: "bigint", nullable: true),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    StorageLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    Limit = table.Column<BigInteger>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetDepositsLimitOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmartRollupAddMessagesOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    BakerFee = table.Column<long>(type: "bigint", nullable: false),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    AllocationFee = table.Column<long>(type: "bigint", nullable: true),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    StorageLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    MessagesCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartRollupAddMessagesOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmartRollupCementOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    BakerFee = table.Column<long>(type: "bigint", nullable: false),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    AllocationFee = table.Column<long>(type: "bigint", nullable: true),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    StorageLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    SmartRollupId = table.Column<int>(type: "integer", nullable: true),
                    CommitmentId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartRollupCementOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmartRollupCommitments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    InitiatorId = table.Column<int>(type: "integer", nullable: false),
                    SmartRollupId = table.Column<int>(type: "integer", nullable: false),
                    PredecessorId = table.Column<int>(type: "integer", nullable: true),
                    InboxLevel = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false),
                    Ticks = table.Column<long>(type: "bigint", nullable: false),
                    FirstLevel = table.Column<int>(type: "integer", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false),
                    Stakers = table.Column<int>(type: "integer", nullable: false),
                    ActiveStakers = table.Column<int>(type: "integer", nullable: false),
                    Successors = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartRollupCommitments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmartRollupExecuteOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    BakerFee = table.Column<long>(type: "bigint", nullable: false),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    AllocationFee = table.Column<long>(type: "bigint", nullable: true),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    StorageLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    SmartRollupId = table.Column<int>(type: "integer", nullable: true),
                    CommitmentId = table.Column<int>(type: "integer", nullable: true),
                    TicketTransfers = table.Column<int>(type: "integer", nullable: true),
                    SubsCounter = table.Column<int>(type: "integer", nullable: true),
                    InternalOperations = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartRollupExecuteOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmartRollupOriginateOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    BakerFee = table.Column<long>(type: "bigint", nullable: false),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    AllocationFee = table.Column<long>(type: "bigint", nullable: true),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    StorageLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    PvmKind = table.Column<int>(type: "integer", nullable: false),
                    Kernel = table.Column<byte[]>(type: "bytea", nullable: false),
                    ParameterType = table.Column<byte[]>(type: "bytea", nullable: true),
                    GenesisCommitment = table.Column<string>(type: "text", nullable: true),
                    SmartRollupId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartRollupOriginateOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmartRollupPublishOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    BakerFee = table.Column<long>(type: "bigint", nullable: false),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    AllocationFee = table.Column<long>(type: "bigint", nullable: true),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    StorageLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    SmartRollupId = table.Column<int>(type: "integer", nullable: true),
                    CommitmentId = table.Column<int>(type: "integer", nullable: true),
                    Bond = table.Column<long>(type: "bigint", nullable: false),
                    BondStatus = table.Column<int>(type: "integer", nullable: true),
                    Flags = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartRollupPublishOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmartRollupRecoverBondOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    BakerFee = table.Column<long>(type: "bigint", nullable: false),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    AllocationFee = table.Column<long>(type: "bigint", nullable: true),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    StorageLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    SmartRollupId = table.Column<int>(type: "integer", nullable: true),
                    StakerId = table.Column<int>(type: "integer", nullable: true),
                    Bond = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartRollupRecoverBondOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmartRollupRefuteOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    BakerFee = table.Column<long>(type: "bigint", nullable: false),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    AllocationFee = table.Column<long>(type: "bigint", nullable: true),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    StorageLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    SmartRollupId = table.Column<int>(type: "integer", nullable: true),
                    GameId = table.Column<int>(type: "integer", nullable: true),
                    Move = table.Column<int>(type: "integer", nullable: false),
                    GameStatus = table.Column<int>(type: "integer", nullable: false),
                    DissectionStart = table.Column<long>(type: "bigint", nullable: true),
                    DissectionEnd = table.Column<long>(type: "bigint", nullable: true),
                    DissectionSteps = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartRollupRefuteOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SnapshotBalances",
                columns: table => new
                {
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    DelegatorsCount = table.Column<int>(type: "integer", nullable: true),
                    OwnDelegatedBalance = table.Column<long>(type: "bigint", nullable: false),
                    ExternalDelegatedBalance = table.Column<long>(type: "bigint", nullable: true),
                    OwnStakedBalance = table.Column<long>(type: "bigint", nullable: true),
                    ExternalStakedBalance = table.Column<long>(type: "bigint", nullable: true),
                    StakersCount = table.Column<int>(type: "integer", nullable: true),
                    Pseudotokens = table.Column<BigInteger>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnapshotBalances", x => new { x.ChainId, x.Level, x.BakerId, x.AddressId });
                });

            migrationBuilder.CreateTable(
                name: "Software",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    ShortHash = table.Column<string>(type: "character(10)", fixedLength: true, maxLength: 10, nullable: false),
                    FirstLevel = table.Column<int>(type: "integer", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false),
                    BlocksCount = table.Column<int>(type: "integer", nullable: false),
                    Extras = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Software", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StakerCycles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Cycle = table.Column<int>(type: "integer", nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    StakerId = table.Column<int>(type: "integer", nullable: false),
                    InitialStake = table.Column<long>(type: "bigint", nullable: false),
                    AvgStake = table.Column<long>(type: "bigint", nullable: false),
                    AddedStake = table.Column<long>(type: "bigint", nullable: false),
                    RemovedStake = table.Column<long>(type: "bigint", nullable: false),
                    FinalStake = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StakerCycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StakingOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    BakerFee = table.Column<long>(type: "bigint", nullable: false),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    AllocationFee = table.Column<long>(type: "bigint", nullable: true),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    StorageLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    StakerId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    RequestedAmount = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: true),
                    BakerId = table.Column<int>(type: "integer", nullable: true),
                    StakingUpdatesCount = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StakingOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StakingUpdates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Cycle = table.Column<int>(type: "integer", nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    StakerId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Pseudotokens = table.Column<BigInteger>(type: "numeric", nullable: true),
                    RoundingError = table.Column<long>(type: "bigint", nullable: true),
                    AutostakingOpId = table.Column<long>(type: "bigint", nullable: true),
                    StakingOpId = table.Column<long>(type: "bigint", nullable: true),
                    DelegationOpId = table.Column<long>(type: "bigint", nullable: true),
                    DoubleBakingOpId = table.Column<long>(type: "bigint", nullable: true),
                    DoubleConsensusOpId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StakingUpdates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Statistics",
                columns: table => new
                {
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Layer = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Cycle = table.Column<int>(type: "integer", nullable: true),
                    TotalBootstrapped = table.Column<long>(type: "bigint", nullable: true),
                    TotalCommitments = table.Column<long>(type: "bigint", nullable: true),
                    TotalActivated = table.Column<long>(type: "bigint", nullable: true),
                    TotalCreated = table.Column<long>(type: "bigint", nullable: true),
                    TotalBurned = table.Column<long>(type: "bigint", nullable: true),
                    TotalBanished = table.Column<long>(type: "bigint", nullable: true),
                    TotalLost = table.Column<long>(type: "bigint", nullable: true),
                    TotalFrozen = table.Column<long>(type: "bigint", nullable: true),
                    TotalSmartRollupBonds = table.Column<long>(type: "bigint", nullable: true),
                    TotalOwnStaked = table.Column<long>(type: "bigint", nullable: true),
                    TotalOwnDelegated = table.Column<long>(type: "bigint", nullable: true),
                    TotalExternalStaked = table.Column<long>(type: "bigint", nullable: true),
                    TotalExternalDelegated = table.Column<long>(type: "bigint", nullable: true),
                    TotalBakingPower = table.Column<long>(type: "bigint", nullable: true),
                    TotalVotingPower = table.Column<long>(type: "bigint", nullable: true),
                    TotalBakers = table.Column<int>(type: "integer", nullable: true),
                    TotalStakers = table.Column<int>(type: "integer", nullable: true),
                    TotalDelegators = table.Column<int>(type: "integer", nullable: true),
                    TotalBootstrapped18 = table.Column<BigInteger>(type: "numeric", nullable: true),
                    TotalCreated18 = table.Column<BigInteger>(type: "numeric", nullable: true),
                    TotalBurned18 = table.Column<BigInteger>(type: "numeric", nullable: true),
                    TotalBanished18 = table.Column<BigInteger>(type: "numeric", nullable: true),
                    TotalLost18 = table.Column<BigInteger>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Statistics", x => new { x.ChainId, x.Level });
                });

            migrationBuilder.CreateTable(
                name: "Storages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    OriginationId = table.Column<long>(type: "bigint", nullable: true),
                    TransactionId = table.Column<long>(type: "bigint", nullable: true),
                    MigrationId = table.Column<long>(type: "bigint", nullable: true),
                    Current = table.Column<bool>(type: "boolean", nullable: false),
                    RawValue = table.Column<byte[]>(type: "bytea", nullable: false),
                    JsonValue = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Storages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubsidyOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    StorageId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubsidyOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketBalances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    TicketerId = table.Column<int>(type: "integer", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    FirstLevel = table.Column<int>(type: "integer", nullable: false),
                    FirstTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false),
                    LastTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TransfersCount = table.Column<int>(type: "integer", nullable: false),
                    Balance = table.Column<BigInteger>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketBalances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    TicketerId = table.Column<int>(type: "integer", nullable: false),
                    WeakHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    FirstMinterId = table.Column<int>(type: "integer", nullable: false),
                    FirstLevel = table.Column<int>(type: "integer", nullable: false),
                    FirstTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false),
                    LastTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TransfersCount = table.Column<int>(type: "integer", nullable: false),
                    BalancesCount = table.Column<int>(type: "integer", nullable: false),
                    HoldersCount = table.Column<int>(type: "integer", nullable: false),
                    TotalMinted = table.Column<BigInteger>(type: "numeric", nullable: false),
                    TotalBurned = table.Column<BigInteger>(type: "numeric", nullable: false),
                    TotalSupply = table.Column<BigInteger>(type: "numeric", nullable: false),
                    RawType = table.Column<byte[]>(type: "bytea", nullable: false),
                    RawContent = table.Column<byte[]>(type: "bytea", nullable: false),
                    JsonContent = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketTransfers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    TicketerId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<BigInteger>(type: "numeric", nullable: false),
                    FromId = table.Column<int>(type: "integer", nullable: true),
                    ToId = table.Column<int>(type: "integer", nullable: true),
                    TransactionId = table.Column<long>(type: "bigint", nullable: true),
                    TransferTicketId = table.Column<long>(type: "bigint", nullable: true),
                    SmartRollupExecuteId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTransfers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TokenBalances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    TokenId = table.Column<long>(type: "bigint", nullable: false),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    Entrypoint = table.Column<byte[]>(type: "bytea", nullable: true),
                    FirstLevel = table.Column<int>(type: "integer", nullable: false),
                    FirstTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false),
                    LastTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TransfersCount = table.Column<long>(type: "bigint", nullable: false),
                    Balance = table.Column<BigInteger>(type: "numeric", nullable: false),
                    IndexedAt = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenBalances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    TokenId = table.Column<BigInteger>(type: "numeric", nullable: false),
                    Tags = table.Column<int>(type: "integer", nullable: false),
                    FirstMinterId = table.Column<int>(type: "integer", nullable: false),
                    FirstLevel = table.Column<int>(type: "integer", nullable: false),
                    FirstTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false),
                    LastTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TransfersCount = table.Column<long>(type: "bigint", nullable: false),
                    BalancesCount = table.Column<int>(type: "integer", nullable: false),
                    HoldersCount = table.Column<int>(type: "integer", nullable: false),
                    TotalMinted = table.Column<BigInteger>(type: "numeric", nullable: false),
                    TotalBurned = table.Column<BigInteger>(type: "numeric", nullable: false),
                    TotalSupply = table.Column<BigInteger>(type: "numeric", nullable: false),
                    OwnerId = table.Column<int>(type: "integer", nullable: true),
                    OwnerEntrypoint = table.Column<byte[]>(type: "bytea", nullable: true),
                    IndexedAt = table.Column<int>(type: "integer", nullable: true),
                    Decimals = table.Column<int>(type: "integer", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    MetadataLink = table.Column<string>(type: "text", nullable: true),
                    MetadataStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MetadataSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Symbol = table.Column<string>(type: "text", nullable: true),
                    Value = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TokenTransfers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    TokenId = table.Column<long>(type: "bigint", nullable: false),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<BigInteger>(type: "numeric", nullable: false),
                    FromId = table.Column<int>(type: "integer", nullable: true),
                    FromEntrypoint = table.Column<byte[]>(type: "bytea", nullable: true),
                    ToId = table.Column<int>(type: "integer", nullable: true),
                    ToEntrypoint = table.Column<byte[]>(type: "bytea", nullable: true),
                    OriginationId = table.Column<long>(type: "bigint", nullable: true),
                    TransactionId = table.Column<long>(type: "bigint", nullable: true),
                    MigrationId = table.Column<long>(type: "bigint", nullable: true),
                    IndexedAt = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenTransfers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransactionOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    InitiatorId = table.Column<int>(type: "integer", nullable: true),
                    TokenTransfers = table.Column<int>(type: "integer", nullable: true),
                    SenderCodeHash = table.Column<int>(type: "integer", nullable: true),
                    TargetId = table.Column<int>(type: "integer", nullable: false),
                    TargetCodeHash = table.Column<int>(type: "integer", nullable: true),
                    InternalOperations = table.Column<int>(type: "integer", nullable: true),
                    LogsCount = table.Column<int>(type: "integer", nullable: true),
                    Entrypoint = table.Column<string>(type: "text", nullable: true),
                    Parameters = table.Column<string>(type: "jsonb", nullable: true),
                    Guessed = table.Column<bool>(type: "boolean", nullable: true),
                    SubsCounter = table.Column<int>(type: "integer", nullable: true),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    AllocationFee = table.Column<long>(type: "bigint", nullable: true),
                    StorageLimit = table.Column<int>(type: "integer", nullable: true),
                    StorageUsed = table.Column<int>(type: "integer", nullable: true),
                    Nonce = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<long>(type: "bigint", nullable: true),
                    StorageId = table.Column<long>(type: "bigint", nullable: true),
                    BigMapUpdates = table.Column<int>(type: "integer", nullable: true),
                    TicketTransfers = table.Column<int>(type: "integer", nullable: true),
                    AddressRegistryIndex = table.Column<int>(type: "integer", nullable: true),
                    ParametersRaw = table.Column<byte[]>(type: "bytea", nullable: true),
                    BakerFee = table.Column<long>(type: "bigint", nullable: true),
                    ResetDeactivation = table.Column<int>(type: "integer", nullable: true),
                    DaFee = table.Column<long>(type: "bigint", nullable: true),
                    GasFee = table.Column<long>(type: "bigint", nullable: true),
                    GasRefund = table.Column<long>(type: "bigint", nullable: true),
                    OpType = table.Column<int>(type: "integer", nullable: true),
                    OpCode = table.Column<int>(type: "integer", nullable: true),
                    GasPrice = table.Column<BigInteger>(type: "numeric", nullable: true),
                    MaxFeePerGas = table.Column<BigInteger>(type: "numeric", nullable: true),
                    MaxPriorityFeePerGas = table.Column<BigInteger>(type: "numeric", nullable: true),
                    EffectiveGasPrice = table.Column<BigInteger>(type: "numeric", nullable: true),
                    DaFee18 = table.Column<BigInteger>(type: "numeric", nullable: true),
                    GasFee18 = table.Column<BigInteger>(type: "numeric", nullable: true),
                    Amount18 = table.Column<BigInteger>(type: "numeric", nullable: true),
                    RoundingLoss = table.Column<BigInteger>(type: "numeric", nullable: true),
                    AliasId = table.Column<int>(type: "integer", nullable: true),
                    GatewayId = table.Column<int>(type: "integer", nullable: true),
                    GatewayEntrypoint = table.Column<string>(type: "text", nullable: true),
                    GatewayParameters = table.Column<string>(type: "jsonb", nullable: true),
                    GatewayInput = table.Column<byte[]>(type: "bytea", nullable: true),
                    Eip7702DelegationCount = table.Column<int>(type: "integer", nullable: true),
                    Input = table.Column<byte[]>(type: "bytea", nullable: true),
                    Output = table.Column<byte[]>(type: "bytea", nullable: true),
                    Result = table.Column<string>(type: "jsonb", nullable: true),
                    BridgeTicketTransfers = table.Column<int>(type: "integer", nullable: true),
                    ClaimDepositId = table.Column<long>(type: "bigint", nullable: true),
                    GatewayParametersRaw = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransferTicketOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Layer = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    StorageLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    TargetId = table.Column<int>(type: "integer", nullable: false),
                    TicketerId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<BigInteger>(type: "numeric", nullable: false),
                    RawType = table.Column<byte[]>(type: "bytea", nullable: true),
                    RawContent = table.Column<byte[]>(type: "bytea", nullable: true),
                    JsonContent = table.Column<string>(type: "jsonb", nullable: true),
                    Entrypoint = table.Column<string>(type: "text", nullable: false),
                    TicketTransfers = table.Column<int>(type: "integer", nullable: true),
                    SubsCounter = table.Column<int>(type: "integer", nullable: true),
                    InternalOperations = table.Column<int>(type: "integer", nullable: true),
                    BakerFee = table.Column<long>(type: "bigint", nullable: true),
                    DaFee = table.Column<long>(type: "bigint", nullable: true),
                    GasFee = table.Column<long>(type: "bigint", nullable: true),
                    GasRefund = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferTicketOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnstakeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Cycle = table.Column<int>(type: "integer", nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    StakerId = table.Column<int>(type: "integer", nullable: true),
                    RequestedAmount = table.Column<long>(type: "bigint", nullable: false),
                    RestakedAmount = table.Column<long>(type: "bigint", nullable: false),
                    FinalizedAmount = table.Column<long>(type: "bigint", nullable: false),
                    SlashedAmount = table.Column<long>(type: "bigint", nullable: false),
                    RoundingError = table.Column<long>(type: "bigint", nullable: true),
                    UpdatesCount = table.Column<int>(type: "integer", nullable: false),
                    FirstLevel = table.Column<int>(type: "integer", nullable: false),
                    FirstTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false),
                    LastTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnstakeRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UpdateSecondaryKeyOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    BakerFee = table.Column<long>(type: "bigint", nullable: false),
                    StorageFee = table.Column<long>(type: "bigint", nullable: true),
                    AllocationFee = table.Column<long>(type: "bigint", nullable: true),
                    GasLimit = table.Column<int>(type: "integer", nullable: false),
                    GasUsed = table.Column<int>(type: "integer", nullable: false),
                    StorageLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    KeyType = table.Column<int>(type: "integer", nullable: false),
                    ActivationCycle = table.Column<int>(type: "integer", nullable: false),
                    PublicKey = table.Column<string>(type: "text", nullable: false),
                    PublicKeyHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpdateSecondaryKeyOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VdfRevelationOps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character(51)", fixedLength: true, maxLength: 51, nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    Cycle = table.Column<int>(type: "integer", nullable: false),
                    RewardDelegated = table.Column<long>(type: "bigint", nullable: false),
                    RewardStakedOwn = table.Column<long>(type: "bigint", nullable: false),
                    RewardStakedEdge = table.Column<long>(type: "bigint", nullable: false),
                    RewardStakedShared = table.Column<long>(type: "bigint", nullable: false),
                    Solution = table.Column<byte[]>(type: "bytea", nullable: false),
                    Proof = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VdfRevelationOps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VotingPeriods",
                columns: table => new
                {
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Epoch = table.Column<int>(type: "integer", nullable: false),
                    FirstLevel = table.Column<int>(type: "integer", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Dictator = table.Column<int>(type: "integer", nullable: false),
                    TotalBakers = table.Column<int>(type: "integer", nullable: false),
                    TotalVotingPower = table.Column<long>(type: "bigint", nullable: false),
                    UpvotesQuorum = table.Column<int>(type: "integer", nullable: true),
                    ProposalsCount = table.Column<int>(type: "integer", nullable: true),
                    TopUpvotes = table.Column<int>(type: "integer", nullable: true),
                    TopVotingPower = table.Column<long>(type: "bigint", nullable: true),
                    SingleWinner = table.Column<bool>(type: "boolean", nullable: true),
                    ParticipationEma = table.Column<int>(type: "integer", nullable: true),
                    BallotsQuorum = table.Column<int>(type: "integer", nullable: true),
                    Supermajority = table.Column<int>(type: "integer", nullable: true),
                    YayBallots = table.Column<int>(type: "integer", nullable: true),
                    NayBallots = table.Column<int>(type: "integer", nullable: true),
                    PassBallots = table.Column<int>(type: "integer", nullable: true),
                    YayVotingPower = table.Column<long>(type: "bigint", nullable: true),
                    NayVotingPower = table.Column<long>(type: "bigint", nullable: true),
                    PassVotingPower = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VotingPeriods", x => new { x.ChainId, x.Index });
                });

            migrationBuilder.CreateTable(
                name: "VotingSnapshots",
                columns: table => new
                {
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    BakerId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    VotingPower = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VotingSnapshots", x => new { x.ChainId, x.Period, x.BakerId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivationOps_Level",
                table: "ActivationOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_BakerId",
                table: "Addresses",
                column: "BakerId",
                filter: "\"BakerId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_BakerId_Partial",
                table: "Addresses",
                column: "BakerId",
                filter: "\"BakerId\" IS NOT NULL AND \"StakedPseudotokens\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_DeactivationLevel_Partial",
                table: "Addresses",
                column: "DeactivationLevel",
                filter: "\"Type\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_FirstLevel",
                table: "Addresses",
                column: "FirstLevel");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_Hash",
                table: "Addresses",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_Index",
                table: "Addresses",
                column: "Index",
                filter: "\"Index\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_Staked_Partial",
                table: "Addresses",
                column: "Staked",
                filter: "\"Type\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_Type",
                table: "Addresses",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_Type_Partial",
                table: "Addresses",
                column: "Type",
                filter: "\"Staked\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_Type_Partial2",
                table: "Addresses",
                column: "Type",
                filter: "\"BakerId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_Type_Partial3",
                table: "Addresses",
                column: "Type",
                filter: "\"Staked\" = true AND \"StakedPseudotokens\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_UnstakedBakerId",
                table: "Addresses",
                column: "UnstakedBakerId",
                filter: "\"UnstakedBakerId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttestationOps_Level",
                table: "AttestationOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_AttestationRewardOps_Level",
                table: "AttestationRewardOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_AutostakingOps_Level",
                table: "AutostakingOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_BakingRights_Level",
                table: "BakingRights",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_BallotOps_Level",
                table: "BallotOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_BallotOps_Period",
                table: "BallotOps",
                column: "Period");

            migrationBuilder.CreateIndex(
                name: "IX_BigMapKeys_BigMapId_KeyHash",
                table: "BigMapKeys",
                columns: new[] { "BigMapId", "KeyHash" });

            migrationBuilder.CreateIndex(
                name: "IX_BigMapKeys_ChainId_LastLevel",
                table: "BigMapKeys",
                columns: new[] { "ChainId", "LastLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_BigMaps_ChainId_LastLevel",
                table: "BigMaps",
                columns: new[] { "ChainId", "LastLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_BigMaps_ChainId_Ptr",
                table: "BigMaps",
                columns: new[] { "ChainId", "Ptr" });

            migrationBuilder.CreateIndex(
                name: "IX_BigMaps_ContractId",
                table: "BigMaps",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_BigMapUpdates_BigMapId_Id",
                table: "BigMapUpdates",
                columns: new[] { "BigMapId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_BigMapUpdates_BigMapKeyId_Id",
                table: "BigMapUpdates",
                columns: new[] { "BigMapKeyId", "Id" },
                filter: "\"BigMapKeyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BigMapUpdates_ChainId_Level",
                table: "BigMapUpdates",
                columns: new[] { "ChainId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_ChainId_Level",
                table: "Blocks",
                columns: new[] { "ChainId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_BridgeTicketBalances_AddressId_TicketId",
                table: "BridgeTicketBalances",
                columns: new[] { "AddressId", "TicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_BridgeTickets_WeakHash",
                table: "BridgeTickets",
                column: "WeakHash");

            migrationBuilder.CreateIndex(
                name: "IX_BridgeTicketTransfers_ChainId_Level",
                table: "BridgeTicketTransfers",
                columns: new[] { "ChainId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_Commitments_AddressId",
                table: "Commitments",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_DalAttestationRewardOps_Level",
                table: "DalAttestationRewardOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_DalEntrapmentEvidenceOps_Level",
                table: "DalEntrapmentEvidenceOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_DalPublishCommitmentOps_Level",
                table: "DalPublishCommitmentOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationOps_Level",
                table: "DelegationOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationOps_SenderId_Partial",
                table: "DelegationOps",
                column: "SenderId",
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationSnapshots_ChainId_Level_Partial",
                table: "DelegationSnapshots",
                columns: new[] { "ChainId", "Level" },
                filter: "\"BakerId\" = \"AddressId\"");

            migrationBuilder.CreateIndex(
                name: "IX_DelegatorCycles_Cycle",
                table: "DelegatorCycles",
                column: "Cycle");

            migrationBuilder.CreateIndex(
                name: "IX_DepositOps_DepositId_Partial",
                table: "DepositOps",
                column: "DepositId",
                filter: "\"DepositId\" is not null");

            migrationBuilder.CreateIndex(
                name: "IX_DepositOps_Level",
                table: "DepositOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_DoubleBakingOps_Hash",
                table: "DoubleBakingOps",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_DoubleBakingOps_Level",
                table: "DoubleBakingOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_DoubleBakingOps_SlashedLevel",
                table: "DoubleBakingOps",
                column: "SlashedLevel");

            migrationBuilder.CreateIndex(
                name: "IX_DoubleConsensusOps_Hash",
                table: "DoubleConsensusOps",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_DoubleConsensusOps_Level",
                table: "DoubleConsensusOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_DoubleConsensusOps_SlashedLevel",
                table: "DoubleConsensusOps",
                column: "SlashedLevel");

            migrationBuilder.CreateIndex(
                name: "IX_DrainDelegateOps_Level",
                table: "DrainDelegateOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_Eip7702Delegations_TransactionId_Id",
                table: "Eip7702Delegations",
                columns: new[] { "TransactionId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Level",
                table: "InboxMessages",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_IncreasePaidStorageOps_Level",
                table: "IncreasePaidStorageOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_ChainId_Level",
                table: "Logs",
                columns: new[] { "ChainId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationOps_AddressId",
                table: "MigrationOps",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationOps_Level",
                table: "MigrationOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_NonceRevelationOps_Level",
                table: "NonceRevelationOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_NonceRevelationOps_RevealedCycle",
                table: "NonceRevelationOps",
                column: "RevealedCycle");

            migrationBuilder.CreateIndex(
                name: "IX_OriginationOps_BakerId_Partial",
                table: "OriginationOps",
                column: "BakerId",
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OriginationOps_Level",
                table: "OriginationOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_PreattestationOps_Level",
                table: "PreattestationOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalOps_Level",
                table: "ProposalOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalOps_Period_ProposalId_SenderId",
                table: "ProposalOps",
                columns: new[] { "Period", "ProposalId", "SenderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalOps_SenderId_Id",
                table: "ProposalOps",
                columns: new[] { "SenderId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_Epoch",
                table: "Proposals",
                column: "Epoch");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_FirstPeriod",
                table: "Proposals",
                column: "FirstPeriod");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_Hash",
                table: "Proposals",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_LastPeriod",
                table: "Proposals",
                column: "LastPeriod");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_Status_Partial",
                table: "Proposals",
                column: "Status",
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Protocols_ChainId_Hash",
                table: "Protocols",
                columns: new[] { "ChainId", "Hash" });

            migrationBuilder.CreateIndex(
                name: "IX_RefutationGames_InitiatorCommitmentId",
                table: "RefutationGames",
                column: "InitiatorCommitmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RefutationGames_OpponentCommitmentId",
                table: "RefutationGames",
                column: "OpponentCommitmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RefutationGames_SmartRollupId",
                table: "RefutationGames",
                column: "SmartRollupId");

            migrationBuilder.CreateIndex(
                name: "IX_RegisterConstantOps_Address",
                table: "RegisterConstantOps",
                column: "Address",
                filter: "\"Address\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RegisterConstantOps_Level",
                table: "RegisterConstantOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_RevealOps_Level",
                table: "RevealOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_RevelationPenaltyOps_Level",
                table: "RevelationPenaltyOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_Scripts_ContractId_Id",
                table: "Scripts",
                columns: new[] { "ContractId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Scripts_ContractId_Partial",
                table: "Scripts",
                column: "ContractId",
                filter: "\"Current\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_SetDelegateParametersOps_ActivationCycle_Partial",
                table: "SetDelegateParametersOps",
                column: "ActivationCycle",
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SetDelegateParametersOps_Level",
                table: "SetDelegateParametersOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_SetDelegateParametersOps_SenderId_Id",
                table: "SetDelegateParametersOps",
                columns: new[] { "SenderId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SetDepositsLimitOps_Level",
                table: "SetDepositsLimitOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_SetDepositsLimitOps_SenderId_Id",
                table: "SetDepositsLimitOps",
                columns: new[] { "SenderId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SmartRollupAddMessagesOps_Level",
                table: "SmartRollupAddMessagesOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_SmartRollupCementOps_Level",
                table: "SmartRollupCementOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_SmartRollupCementOps_SmartRollupId_Id",
                table: "SmartRollupCementOps",
                columns: new[] { "SmartRollupId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SmartRollupCommitments_PredecessorId",
                table: "SmartRollupCommitments",
                column: "PredecessorId");

            migrationBuilder.CreateIndex(
                name: "IX_SmartRollupCommitments_SmartRollupId_Hash_Id",
                table: "SmartRollupCommitments",
                columns: new[] { "SmartRollupId", "Hash", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SmartRollupExecuteOps_CommitmentId_Id",
                table: "SmartRollupExecuteOps",
                columns: new[] { "CommitmentId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SmartRollupExecuteOps_Level",
                table: "SmartRollupExecuteOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_SmartRollupOriginateOps_Level",
                table: "SmartRollupOriginateOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_SmartRollupOriginateOps_SmartRollupId",
                table: "SmartRollupOriginateOps",
                column: "SmartRollupId");

            migrationBuilder.CreateIndex(
                name: "IX_SmartRollupPublishOps_CommitmentId",
                table: "SmartRollupPublishOps",
                column: "CommitmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SmartRollupPublishOps_Level",
                table: "SmartRollupPublishOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_SmartRollupPublishOps_SmartRollupId_BondStatus_SenderId",
                table: "SmartRollupPublishOps",
                columns: new[] { "SmartRollupId", "BondStatus", "SenderId" },
                filter: "\"BondStatus\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SmartRollupPublishOps_SmartRollupId_SenderId_Id",
                table: "SmartRollupPublishOps",
                columns: new[] { "SmartRollupId", "SenderId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SmartRollupRecoverBondOps_Level",
                table: "SmartRollupRecoverBondOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_SmartRollupRefuteOps_GameId_Id",
                table: "SmartRollupRefuteOps",
                columns: new[] { "GameId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SmartRollupRefuteOps_Level",
                table: "SmartRollupRefuteOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_SnapshotBalances_ChainId_Level_Partial",
                table: "SnapshotBalances",
                columns: new[] { "ChainId", "Level" },
                filter: "\"BakerId\" = \"AddressId\"");

            migrationBuilder.CreateIndex(
                name: "IX_Software_ChainId_ShortHash",
                table: "Software",
                columns: new[] { "ChainId", "ShortHash" });

            migrationBuilder.CreateIndex(
                name: "IX_StakerCycles_Cycle",
                table: "StakerCycles",
                column: "Cycle");

            migrationBuilder.CreateIndex(
                name: "IX_StakerCycles_StakerId_Cycle",
                table: "StakerCycles",
                columns: new[] { "StakerId", "Cycle" });

            migrationBuilder.CreateIndex(
                name: "IX_StakingOps_Level",
                table: "StakingOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_StakingUpdates_AutostakingOpId",
                table: "StakingUpdates",
                column: "AutostakingOpId",
                filter: "\"AutostakingOpId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StakingUpdates_BakerId_Cycle_Id",
                table: "StakingUpdates",
                columns: new[] { "BakerId", "Cycle", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StakingUpdates_DelegationOpId",
                table: "StakingUpdates",
                column: "DelegationOpId",
                filter: "\"DelegationOpId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StakingUpdates_DoubleBakingOpId",
                table: "StakingUpdates",
                column: "DoubleBakingOpId",
                filter: "\"DoubleBakingOpId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StakingUpdates_DoubleConsensusOpId",
                table: "StakingUpdates",
                column: "DoubleConsensusOpId",
                filter: "\"DoubleConsensusOpId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StakingUpdates_StakerId_Cycle_Id",
                table: "StakingUpdates",
                columns: new[] { "StakerId", "Cycle", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StakingUpdates_StakingOpId",
                table: "StakingUpdates",
                column: "StakingOpId",
                filter: "\"StakingOpId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Storages_ContractId_Id",
                table: "Storages",
                columns: new[] { "ContractId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Storages_ContractId_Partial",
                table: "Storages",
                column: "ContractId",
                filter: "\"Current\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_SubsidyOps_Level",
                table: "SubsidyOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_TicketBalances_AddressId_TicketId",
                table: "TicketBalances",
                columns: new[] { "AddressId", "TicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_WeakHash",
                table: "Tickets",
                column: "WeakHash");

            migrationBuilder.CreateIndex(
                name: "IX_TicketTransfers_ChainId_Level",
                table: "TicketTransfers",
                columns: new[] { "ChainId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_TokenBalances_AddressId_TokenId",
                table: "TokenBalances",
                columns: new[] { "AddressId", "TokenId" });

            migrationBuilder.CreateIndex(
                name: "IX_TokenBalances_TokenId",
                table: "TokenBalances",
                column: "TokenId");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_ContractId_TokenId",
                table: "Tokens",
                columns: new[] { "ContractId", "TokenId" });

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransfers_ChainId_Level",
                table: "TokenTransfers",
                columns: new[] { "ChainId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionOps_Level",
                table: "TransactionOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionOps_TargetId_Partial",
                table: "TransactionOps",
                column: "TargetId",
                filter: "\"Entrypoint\" = 'transfer' AND \"TokenTransfers\" IS NULL AND \"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TransferTicketOps_Level",
                table: "TransferTicketOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_UnstakeRequests_BakerId_Cycle_StakerId",
                table: "UnstakeRequests",
                columns: new[] { "BakerId", "Cycle", "StakerId" });

            migrationBuilder.CreateIndex(
                name: "IX_UpdateSecondaryKeyOps_ActivationCycle_Partial",
                table: "UpdateSecondaryKeyOps",
                column: "ActivationCycle",
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_UpdateSecondaryKeyOps_Level",
                table: "UpdateSecondaryKeyOps",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_UpdateSecondaryKeyOps_SenderId_Id",
                table: "UpdateSecondaryKeyOps",
                columns: new[] { "SenderId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_VdfRevelationOps_Cycle",
                table: "VdfRevelationOps",
                column: "Cycle");

            migrationBuilder.CreateIndex(
                name: "IX_VdfRevelationOps_Level",
                table: "VdfRevelationOps",
                column: "Level");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivationOps");

            migrationBuilder.DropTable(
                name: "Addresses");

            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "AttestationOps");

            migrationBuilder.DropTable(
                name: "AttestationRewardOps");

            migrationBuilder.DropTable(
                name: "AutostakingOps");

            migrationBuilder.DropTable(
                name: "BakerCycles");

            migrationBuilder.DropTable(
                name: "BakingRights");

            migrationBuilder.DropTable(
                name: "BallotOps");

            migrationBuilder.DropTable(
                name: "BigMapKeys");

            migrationBuilder.DropTable(
                name: "BigMaps");

            migrationBuilder.DropTable(
                name: "BigMapUpdates");

            migrationBuilder.DropTable(
                name: "Blocks");

            migrationBuilder.DropTable(
                name: "BridgeTicketBalances");

            migrationBuilder.DropTable(
                name: "BridgeTickets");

            migrationBuilder.DropTable(
                name: "BridgeTicketTransfers");

            migrationBuilder.DropTable(
                name: "Chains");

            migrationBuilder.DropTable(
                name: "Commitments");

            migrationBuilder.DropTable(
                name: "Cycles");

            migrationBuilder.DropTable(
                name: "DalAttestationRewardOps");

            migrationBuilder.DropTable(
                name: "DalEntrapmentEvidenceOps");

            migrationBuilder.DropTable(
                name: "DalPublishCommitmentOps");

            migrationBuilder.DropTable(
                name: "DelegationOps");

            migrationBuilder.DropTable(
                name: "DelegationSnapshots");

            migrationBuilder.DropTable(
                name: "DelegatorCycles");

            migrationBuilder.DropTable(
                name: "DepositOps");

            migrationBuilder.DropTable(
                name: "Domains");

            migrationBuilder.DropTable(
                name: "DoubleBakingOps");

            migrationBuilder.DropTable(
                name: "DoubleConsensusOps");

            migrationBuilder.DropTable(
                name: "DrainDelegateOps");

            migrationBuilder.DropTable(
                name: "Eip7702Delegations");

            migrationBuilder.DropTable(
                name: "InboxMessages");

            migrationBuilder.DropTable(
                name: "IncreasePaidStorageOps");

            migrationBuilder.DropTable(
                name: "Logs");

            migrationBuilder.DropTable(
                name: "MigrationOps");

            migrationBuilder.DropTable(
                name: "NonceRevelationOps");

            migrationBuilder.DropTable(
                name: "OriginationOps");

            migrationBuilder.DropTable(
                name: "PreattestationOps");

            migrationBuilder.DropTable(
                name: "ProposalOps");

            migrationBuilder.DropTable(
                name: "Proposals");

            migrationBuilder.DropTable(
                name: "Protocols");

            migrationBuilder.DropTable(
                name: "Quotes");

            migrationBuilder.DropTable(
                name: "RefutationGames");

            migrationBuilder.DropTable(
                name: "RegisterConstantOps");

            migrationBuilder.DropTable(
                name: "RevealOps");

            migrationBuilder.DropTable(
                name: "RevelationPenaltyOps");

            migrationBuilder.DropTable(
                name: "Scripts");

            migrationBuilder.DropTable(
                name: "SetDelegateParametersOps");

            migrationBuilder.DropTable(
                name: "SetDepositsLimitOps");

            migrationBuilder.DropTable(
                name: "SmartRollupAddMessagesOps");

            migrationBuilder.DropTable(
                name: "SmartRollupCementOps");

            migrationBuilder.DropTable(
                name: "SmartRollupCommitments");

            migrationBuilder.DropTable(
                name: "SmartRollupExecuteOps");

            migrationBuilder.DropTable(
                name: "SmartRollupOriginateOps");

            migrationBuilder.DropTable(
                name: "SmartRollupPublishOps");

            migrationBuilder.DropTable(
                name: "SmartRollupRecoverBondOps");

            migrationBuilder.DropTable(
                name: "SmartRollupRefuteOps");

            migrationBuilder.DropTable(
                name: "SnapshotBalances");

            migrationBuilder.DropTable(
                name: "Software");

            migrationBuilder.DropTable(
                name: "StakerCycles");

            migrationBuilder.DropTable(
                name: "StakingOps");

            migrationBuilder.DropTable(
                name: "StakingUpdates");

            migrationBuilder.DropTable(
                name: "Statistics");

            migrationBuilder.DropTable(
                name: "Storages");

            migrationBuilder.DropTable(
                name: "SubsidyOps");

            migrationBuilder.DropTable(
                name: "TicketBalances");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "TicketTransfers");

            migrationBuilder.DropTable(
                name: "TokenBalances");

            migrationBuilder.DropTable(
                name: "Tokens");

            migrationBuilder.DropTable(
                name: "TokenTransfers");

            migrationBuilder.DropTable(
                name: "TransactionOps");

            migrationBuilder.DropTable(
                name: "TransferTicketOps");

            migrationBuilder.DropTable(
                name: "UnstakeRequests");

            migrationBuilder.DropTable(
                name: "UpdateSecondaryKeyOps");

            migrationBuilder.DropTable(
                name: "VdfRevelationOps");

            migrationBuilder.DropTable(
                name: "VotingPeriods");

            migrationBuilder.DropTable(
                name: "VotingSnapshots");
        }
    }
}
