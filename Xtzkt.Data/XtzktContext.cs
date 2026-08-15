using Microsoft.EntityFrameworkCore;

using Xtzkt.Data.Models;

namespace Xtzkt.Data
{
    public class XtzktContext(DbContextOptions options) : DbContext(options)
    {
        #region addresses
        public DbSet<Address> Addresses { get; set; }
        #endregion

        #region baking
        public DbSet<BakerCycle> BakerCycles { get; set; }
        public DbSet<BakingRight> BakingRights { get; set; }
        public DbSet<Cycle> Cycles { get; set; }
        public DbSet<DelegationSnapshot> DelegationSnapshots { get; set; }
        public DbSet<DelegatorCycle> DelegatorCycles { get; set; }
        public DbSet<SnapshotBalance> SnapshotBalances { get; set; }
        public DbSet<StakerCycle> StakerCycles { get; set; }
        #endregion

        #region chains
        public DbSet<Block> Blocks { get; set; }
        public DbSet<L1Block> L1Blocks { get; set; }
        public DbSet<XBlock> XBlocks { get; set; }

        public DbSet<Chain> Chains { get; set; }

        public DbSet<Protocol> Protocols { get; set; }
        public DbSet<L1Protocol> L1Protocols { get; set; }
        public DbSet<XProtocol> XProtocols { get; set; }

        public DbSet<Commitment> Commitments { get; set; }
        public DbSet<Software> Software { get; set; }
        #endregion

        #region contracts
        public DbSet<BigMap> BigMaps { get; set; }
        public DbSet<BigMapKey> BigMapKeys { get; set; }
        public DbSet<BigMapUpdate> BigMapUpdates { get; set; }
        public DbSet<Eip7702Delegation> Eip7702Delegations { get; set; }
        public DbSet<Log> Logs { get; set; }
        public DbSet<Script> Scripts { get; set; }
        public DbSet<Storage> Storages { get; set; }
        #endregion

        #region operations
        public DbSet<ActivationOperation> ActivationOps { get; set; }
        public DbSet<DalEntrapmentEvidenceOperation> DalEntrapmentEvidenceOps { get; set; }
        public DbSet<DoubleBakingOperation> DoubleBakingOps { get; set; }
        public DbSet<DoubleConsensusOperation> DoubleConsensusOps { get; set; }
        public DbSet<DrainDelegateOperation> DrainDelegateOps { get; set; }
        public DbSet<NonceRevelationOperation> NonceRevelationOps { get; set; }
        public DbSet<VdfRevelationOperation> VdfRevelationOps { get; set; }

        public DbSet<AttestationOperation> AttestationOps { get; set; }
        public DbSet<PreattestationOperation> PreattestationOps { get; set; }

        public DbSet<BallotOperation> BallotOps { get; set; }
        public DbSet<ProposalOperation> ProposalOps { get; set; }

        public DbSet<AttestationRewardOperation> AttestationRewardOps { get; set; }
        public DbSet<AutostakingOperation> AutostakingOps { get; set; }
        public DbSet<DalAttestationRewardOperation> DalAttestationRewardOps { get; set; }
        public DbSet<MigrationOperation> MigrationOps { get; set; }
        public DbSet<RevelationPenaltyOperation> RevelationPenaltyOps { get; set; }
        public DbSet<SubsidyOperation> SubsidyOps { get; set; }

        public DbSet<DepositOperation> DepositOps { get; set; }
        public DbSet<TransactionOperation> TransactionOps { get; set; }
        public DbSet<DalPublishCommitmentOperation> DalPublishCommitmentOps { get; set; }
        public DbSet<DelegationOperation> DelegationOps { get; set; }
        public DbSet<IncreasePaidStorageOperation> IncreasePaidStorageOps { get; set; }
        public DbSet<OriginationOperation> OriginationOps { get; set; }
        public DbSet<RegisterConstantOperation> RegisterConstantOps { get; set; }
        public DbSet<RevealOperation> RevealOps { get; set; }
        public DbSet<SetDelegateParametersOperation> SetDelegateParametersOps { get; set; }
        public DbSet<SetDepositsLimitOperation> SetDepositsLimitOps { get; set; }
        public DbSet<SmartRollupAddMessagesOperation> SmartRollupAddMessagesOps { get; set; }
        public DbSet<SmartRollupCementOperation> SmartRollupCementOps { get; set; }
        public DbSet<SmartRollupExecuteOperation> SmartRollupExecuteOps { get; set; }
        public DbSet<SmartRollupOriginateOperation> SmartRollupOriginateOps { get; set; }
        public DbSet<SmartRollupPublishOperation> SmartRollupPublishOps { get; set; }
        public DbSet<SmartRollupRecoverBondOperation> SmartRollupRecoverBondOps { get; set; }
        public DbSet<SmartRollupRefuteOperation> SmartRollupRefuteOps { get; set; }
        public DbSet<StakingOperation> StakingOps { get; set; }
        public DbSet<TransferTicketOperation> TransferTicketOps { get; set; }
        public DbSet<UpdateSecondaryKeyOperation> UpdateSecondaryKeyOps { get; set; }
        #endregion

        #region plugins
        public DbSet<Domain> Domains { get; set; }
        public DbSet<Quote> Quotes { get; set; }
        #endregion

        #region smart rollups
        public DbSet<InboxMessage> InboxMessages { get; set; }
        public DbSet<RefutationGame> RefutationGames { get; set; }
        public DbSet<SmartRollupCommitment> SmartRollupCommitments { get; set; }
        #endregion

        #region staking
        public DbSet<StakingUpdate> StakingUpdates { get; set; }
        public DbSet<UnstakeRequest> UnstakeRequests { get; set; }
        #endregion

        #region statistics
        public DbSet<Statistics> Statistics { get; set; }
        #endregion

        #region tickets
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<TicketBalance> TicketBalances { get; set; }
        public DbSet<TicketTransfer> TicketTransfers { get; set; }
        #endregion

        #region tokens
        public DbSet<Asset> Assets { get; set; }
        public DbSet<Token> Tokens { get; set; }
        public DbSet<TokenBalance> TokenBalances { get; set; }
        public DbSet<TokenTransfer> TokenTransfers { get; set; }
        #endregion

        #region voting
        public DbSet<Proposal> Proposals { get; set; }
        public DbSet<VotingPeriod> VotingPeriods { get; set; }
        public DbSet<VotingSnapshot> VotingSnapshots { get; set; }
        #endregion

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            #region addresses
            modelBuilder.BuildAddressModel();
            #endregion

            #region baking
            modelBuilder.BuildBakerCycleModel();
            modelBuilder.BuildBakingRightModel();
            modelBuilder.BuildCycleModel();
            modelBuilder.BuildDelegationSnapshotModel();
            modelBuilder.BuildDelegatorCycleModel();
            modelBuilder.BuildSnapshotBalanceModel();
            modelBuilder.BuildStakerCycleModel();
            #endregion

            #region chains
            modelBuilder.BuildBlockModel();
            modelBuilder.BuildChainModel();
            modelBuilder.BuildProtocolModel();
            modelBuilder.BuildCommitmentModel();
            modelBuilder.BuildSoftwareModel();
            #endregion

            #region contracts
            modelBuilder.BuildBigMapModel();
            modelBuilder.BuildBigMapKeyModel();
            modelBuilder.BuildBigMapUpdateModel();
            modelBuilder.BuildEip7702DelegationModel();
            modelBuilder.BuildLogModel();
            modelBuilder.BuildScriptModel();
            modelBuilder.BuildStorageModel();
            #endregion

            #region operations
            modelBuilder.BuildActivationOperationModel();
            modelBuilder.BuildDalEntrapmentEvidenceOperationModel();
            modelBuilder.BuildDoubleBakingOperationModel();
            modelBuilder.BuildDoubleConsensusOperationModel();
            modelBuilder.BuildDrainDelegateOperationModel();
            modelBuilder.BuildNonceRevelationOperationModel();
            modelBuilder.BuildVdfRevelationOperationModel();

            modelBuilder.BuildAttestationOperationModel();
            modelBuilder.BuildPreattestationOperationModel();

            modelBuilder.BuildBallotOperationModel();
            modelBuilder.BuildProposalOperationModel();

            modelBuilder.BuildAttestationRewardOperationModel();
            modelBuilder.BuildAutostakingOperationModel();
            modelBuilder.BuildDalAttestationRewardOperationModel();
            modelBuilder.BuildMigrationOperationModel();
            modelBuilder.BuildRevelationPenaltyOperationModel();
            modelBuilder.BuildSubsidyOperationModel();

            modelBuilder.BuildDepositOperationModel();
            modelBuilder.BuildTransactionOperationModel();
            modelBuilder.BuildDalPublishCommitmentOperationModel();
            modelBuilder.BuildDelegationOperationModel();
            modelBuilder.BuildIncreasePaidStorageOperationModel();
            modelBuilder.BuildOriginationOperationModel();
            modelBuilder.BuildRegisterConstantOperationModel();
            modelBuilder.BuildRevealOperationModel();
            modelBuilder.BuildSetDelegateParametersOperationModel();
            modelBuilder.BuildSetDepositsLimitOperationModel();
            modelBuilder.BuildSmartRollupAddMessagesOperationModel();
            modelBuilder.BuildSmartRollupCementOperationModel();
            modelBuilder.BuildSmartRollupExecuteOperationModel();
            modelBuilder.BuildSmartRollupOriginateOperationModel();
            modelBuilder.BuildSmartRollupPublishOperationModel();
            modelBuilder.BuildSmartRollupRecoverBondOperationModel();
            modelBuilder.BuildSmartRollupRefuteOperationModel();
            modelBuilder.BuildStakingOperationModel();
            modelBuilder.BuildTransferTicketOperationModel();
            modelBuilder.BuildUpdateSecondaryKeyOperationModel();
            #endregion

            #region plugins
            modelBuilder.BuildDomainModel();
            modelBuilder.BuildQuoteModel();
            #endregion

            #region smart rollups
            modelBuilder.BuildInboxMessageModel();
            modelBuilder.BuildRefutationGameModel();
            modelBuilder.BuildSmartRollupCommitmentModel();
            #endregion

            #region staking
            modelBuilder.BuildStakingUpdateModel();
            modelBuilder.BuildUnstakeRequestModel();
            #endregion

            #region statistics
            modelBuilder.BuildStatisticsModel();
            #endregion

            #region tickets
            modelBuilder.BuildTicketModel();
            modelBuilder.BuildTicketBalanceModel();
            modelBuilder.BuildTicketTransferModel();
            #endregion

            #region tokens
            modelBuilder.BuildAssetModel();
            modelBuilder.BuildTokenModel();
            modelBuilder.BuildTokenBalanceModel();
            modelBuilder.BuildTokenTransferModel();
            #endregion

            #region voting
            modelBuilder.BuildProposalModel();
            modelBuilder.BuildVotingPeriodModel();
            modelBuilder.BuildVotingSnapshotModel();
            #endregion
        }
    }
}
