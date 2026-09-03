using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto18
{
    class DoubleConsensusCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public void Apply(L1Block block, byte[] opHash, JsonElement content)
        {
            #region init
            var accusedLevel = GetAccusedLevel(content);
            var accuser = Context.Proposer;
            var offender = Cache.Addresses.GetExistingBaker(GetOffender(content));

            var operation = new DoubleConsensusOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = opHash,

                Kind = GetKind(content),

                AccusedLevel = accusedLevel,
                SlashedLevel = GetSlashingLevel(block, Context.Protocol, accusedLevel),

                AccuserId = accuser.Id,
                OffenderId = offender.Id,

                Reward = 0,
                LostStaked = 0,
                LostUnstaked = 0,
                LostExternalStaked = 0,
                LostExternalUnstaked = 0
            };
            #endregion

            #region apply operation
            Db.TryAttach(accuser);
            accuser.DoubleConsensusCount++;

            if (offender != accuser)
            {
                Db.TryAttach(offender);
                offender.DoubleConsensusCount++;
            }

            block.Operations |= L1Operations.DoubleConsensus;

            Cache.Chain.Get().DoubleConsensusOpsCount++;
            #endregion

            Db.DoubleConsensusOps.Add(operation);
            Context.DoubleConsensusOps.Add(operation);
        }

        public void Revert(DoubleConsensusOperation operation)
        {
            #region init
            var accuser = Cache.Addresses.GetBaker(operation.AccuserId);
            var offender = Cache.Addresses.GetBaker(operation.OffenderId);
            #endregion

            #region revert operation
            Db.TryAttach(accuser);
            accuser.DoubleConsensusCount--;

            if (offender != accuser)
            {
                Db.TryAttach(offender);
                offender.DoubleConsensusCount--;
            }

            Cache.Chain.Get().DoubleConsensusOpsCount--;
            #endregion

            Db.DoubleConsensusOps.Remove(operation);
            Cache.Chain.ReleaseOperationId();
        }

        protected virtual int GetAccusedLevel(JsonElement content)
        {
            return content.Required("op1").Required("operations").RequiredInt32("level");
        }

        protected virtual string GetOffender(JsonElement content)
        {
            var offender = content.Required("metadata").OptionalString("forbidden_delegate");
            
            // one-time workaround to avoid signature brute forcing
            if (offender == null && Cache.Chain.GetChainId() == "NetXdQprcVkpaWU" && Context.Block.Level == 5689908)
                offender = "tz1bZ8vsMAXmaWEV7FRnyhcuUs2fYMaQ6Hkk";

            return offender ?? throw new Exception("Failed to determine offender");
        }

        protected virtual int GetSlashingLevel(L1Block block, L1Protocol protocol, int accusedLevel)
        {
            return Cache.Protocols.GetCycleEnd(block.Cycle);
        }

        protected virtual DoubleConsensusKind GetKind(JsonElement content)
        {
            return content.RequiredString("kind") == "double_endorsement_evidence"
                ? DoubleConsensusKind.DoubleAttestation
                : DoubleConsensusKind.DoublePreattestation;
        }
    }
}
