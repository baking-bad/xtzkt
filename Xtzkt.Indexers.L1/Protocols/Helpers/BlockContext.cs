using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Indexers.L1.Protocols
{
    public class BlockContext
    {
        public L1Block Block { get; set; } = null!;
        public L1Baker Proposer { get; set; } = null!;
        public L1Protocol Protocol { get; set; } = null!;

        #region operations
        public List<AttestationOperation> AttestationOps { get; set; } = [];
        public List<PreattestationOperation> PreattestationOps { get; set; } = [];

        public List<ProposalOperation> ProposalOps { get; set; } = [];
        public List<BallotOperation> BallotOps { get; set; } = [];

        public List<ActivationOperation> ActivationOps { get; set; } = [];
        public List<DalEntrapmentEvidenceOperation> DalEntrapmentEvidenceOps { get; set; } = [];
        public List<DoubleBakingOperation> DoubleBakingOps { get; set; } = [];
        public List<DoubleConsensusOperation> DoubleConsensusOps { get; set; } = [];
        public List<NonceRevelationOperation> NonceRevelationOps { get; set; } = [];
        public List<VdfRevelationOperation> VdfRevelationOps { get; set; } = [];
        public List<DrainDelegateOperation> DrainDelegateOps { get; set; } = [];

        public List<DelegationOperation> DelegationOps { get; set; } = [];
        public List<L1OriginationOperation> OriginationOps { get; set; } = [];
        public List<L1TransactionOperation> TransactionOps { get; set; } = [];
        public List<L1RevealOperation> RevealOps { get; set; } = [];
        public List<L1RegisterConstantOperation> RegisterConstantOps { get; set; } = [];
        public List<SetDepositsLimitOperation> SetDepositsLimitOps { get; set; } = [];
        public List<L1IncreasePaidStorageOperation> IncreasePaidStorageOps { get; set; } = [];
        public List<UpdateSecondaryKeyOperation> UpdateSecondaryKeyOps { get; set; } = [];
        public List<L1TransferTicketOperation> TransferTicketOps { get; set; } = [];
        public List<SetDelegateParametersOperation> SetDelegateParametersOps { get; set; } = [];
        public List<DalPublishCommitmentOperation> DalPublishCommitmentOps { get; set; } = [];
        public List<StakingOperation> StakingOps { get; set; } = [];

        public List<SmartRollupAddMessagesOperation> SmartRollupAddMessagesOps { get; set; } = [];
        public List<SmartRollupCementOperation> SmartRollupCementOps { get; set; } = [];
        public List<SmartRollupExecuteOperation> SmartRollupExecuteOps { get; set; } = [];
        public List<SmartRollupOriginateOperation> SmartRollupOriginateOps { get; set; } = [];
        public List<SmartRollupPublishOperation> SmartRollupPublishOps { get; set; } = [];
        public List<SmartRollupRecoverBondOperation> SmartRollupRecoverBondOps { get; set; } = [];
        public List<SmartRollupRefuteOperation> SmartRollupRefuteOps { get; set; } = [];
        #endregion

        #region fictive operations
        public List<MichelsonMigrationOperation> MigrationOps { get; set; } = [];
        public List<SubsidyOperation> SubsidyOps { get; set; } = [];
        public List<RevelationPenaltyOperation> RevelationPenaltyOps { get; set; } = [];
        public List<AttestationRewardOperation> AttestationRewardOps { get; set; } = [];
        public List<DalAttestationRewardOperation> DalAttestationRewardOps { get; set; } = [];
        public List<AutostakingOperation> AutostakingOps { get; set; } = [];
        #endregion

        public IEnumerable<IOperation> EnumerateOps()
        {
            var ops = Enumerable.Empty<IOperation>();

            if (AttestationOps.Count != 0) ops = ops.Concat(AttestationOps);
            if (PreattestationOps.Count != 0) ops = ops.Concat(PreattestationOps);

            if (BallotOps.Count != 0) ops = ops.Concat(BallotOps);
            if (ProposalOps.Count != 0) ops = ops.Concat(ProposalOps);

            if (ActivationOps.Count != 0) ops = ops.Concat(ActivationOps);
            if (DalEntrapmentEvidenceOps.Count != 0) ops = ops.Concat(DalEntrapmentEvidenceOps);
            if (DoubleBakingOps.Count != 0) ops = ops.Concat(DoubleBakingOps);
            if (DoubleConsensusOps.Count != 0) ops = ops.Concat(DoubleConsensusOps);
            if (NonceRevelationOps.Count != 0) ops = ops.Concat(NonceRevelationOps);
            if (VdfRevelationOps.Count != 0) ops = ops.Concat(VdfRevelationOps);
            if (DrainDelegateOps.Count != 0) ops = ops.Concat(DrainDelegateOps);

            if (DelegationOps.Count != 0) ops = ops.Concat(DelegationOps);
            if (OriginationOps.Count != 0) ops = ops.Concat(OriginationOps);
            if (TransactionOps.Count != 0) ops = ops.Concat(TransactionOps);
            if (RevealOps.Count != 0) ops = ops.Concat(RevealOps);
            if (RegisterConstantOps.Count != 0) ops = ops.Concat(RegisterConstantOps);
            if (SetDepositsLimitOps.Count != 0) ops = ops.Concat(SetDepositsLimitOps);
            if (IncreasePaidStorageOps.Count != 0) ops = ops.Concat(IncreasePaidStorageOps);
            if (UpdateSecondaryKeyOps.Count != 0) ops = ops.Concat(UpdateSecondaryKeyOps);
            if (TransferTicketOps.Count != 0) ops = ops.Concat(TransferTicketOps);
            if (SetDelegateParametersOps.Count != 0) ops = ops.Concat(SetDelegateParametersOps);
            if (DalPublishCommitmentOps.Count != 0) ops = ops.Concat(DalPublishCommitmentOps);
            if (StakingOps.Count != 0) ops = ops.Concat(StakingOps);

            if (SmartRollupAddMessagesOps.Count != 0) ops = ops.Concat(SmartRollupAddMessagesOps);
            if (SmartRollupCementOps.Count != 0) ops = ops.Concat(SmartRollupCementOps);
            if (SmartRollupExecuteOps.Count != 0) ops = ops.Concat(SmartRollupExecuteOps);
            if (SmartRollupOriginateOps.Count != 0) ops = ops.Concat(SmartRollupOriginateOps);
            if (SmartRollupPublishOps.Count != 0) ops = ops.Concat(SmartRollupPublishOps);
            if (SmartRollupRecoverBondOps.Count != 0) ops = ops.Concat(SmartRollupRecoverBondOps);
            if (SmartRollupRefuteOps.Count != 0) ops = ops.Concat(SmartRollupRefuteOps);

            return ops;
        }

        public void Apply(XtzktContext db)
        {
            var conn = (db.Database.GetDbConnection() as NpgsqlConnection)!;

            if (TransactionOps.Count != 0)
                L1TransactionOperation.Write(conn, TransactionOps);

            if (AttestationOps.Count != 0)
                AttestationOperation.Write(conn, AttestationOps);
        }

        public async Task Revert(XtzktContext db)
        {
            if (TransactionOps.Count != 0)
                await db.Database.ExecuteSqlRawAsync($$"""
                    DELETE FROM "{{nameof(XtzktContext.TransactionOps)}}"
                    WHERE "{{nameof(TransactionOperation.ChainId)}}" = {0}
                    AND "{{nameof(TransactionOperation.Level)}}" = {1}
                    """, Block.ChainId, Block.Level);

            if (AttestationOps.Count != 0)
                await db.Database.ExecuteSqlRawAsync($$"""
                    DELETE FROM "{{nameof(XtzktContext.AttestationOps)}}"
                    WHERE "{{nameof(AttestationOperation.ChainId)}}" = {0}
                    AND "{{nameof(AttestationOperation.Level)}}" = {1}
                    """, Block.ChainId, Block.Level);
        }
    }
}
