using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto18
{
    class DoubleBakingCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public async Task Apply(L1Block block, JsonElement op, JsonElement content)
        {
            #region init
            var accusedLevel = content.Required("bh1").RequiredInt32("level");
            var accusedRound = Hex.Parse(content.Required("bh1").RequiredArray("fitness", 5)[4].RequiredString()).ToInt32();
            var accusedBakerId = (await Db.BakingRights.AsNoTracking().FirstOrDefaultAsync(x => x.ChainId == block.ChainId && x.Level == accusedLevel && x.Round == accusedRound))?.BakerId;
            if (accusedBakerId == null)
            {
                var rpcRights = await Proto.Rpc.GetLevelBakingRightsAsync(block.Level, accusedLevel, accusedRound);
                var accusedBaker = rpcRights
                    .EnumerateArray()
                    .First(x => x.RequiredInt32("level") == accusedLevel && x.RequiredInt32("round") == accusedRound)
                    .RequiredString("delegate");
                accusedBakerId = Cache.Addresses.GetExistingBaker(accusedBaker).Id;
            }

            var accuser = Context.Proposer;
            var offender = Cache.Addresses.GetBaker(accusedBakerId.Value);

            var operation = new DoubleBakingOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = op.RequiredMichelsonOperationHashBytes("hash"),

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
            accuser.DoubleBakingCount++;

            if (offender != accuser)
            {
                Db.TryAttach(offender);
                offender.DoubleBakingCount++;
            }

            block.Operations |= L1Operations.DoubleBaking;

            Cache.Chain.Get().DoubleBakingOpsCount++;
            #endregion

            Db.DoubleBakingOps.Add(operation);
            Context.DoubleBakingOps.Add(operation);
        }

        public void Revert(DoubleBakingOperation operation)
        {
            #region init
            var accuser = Cache.Addresses.GetBaker(operation.AccuserId);
            var offender = Cache.Addresses.GetBaker(operation.OffenderId);
            #endregion

            #region revert operation
            Db.TryAttach(accuser);
            accuser.DoubleBakingCount--;

            if (offender != accuser)
            {
                Db.TryAttach(offender);
                offender.DoubleBakingCount--;
            }

            Cache.Chain.Get().DoubleBakingOpsCount--;
            #endregion

            Db.DoubleBakingOps.Remove(operation);
            Cache.Chain.ReleaseOperationId();
        }

        protected virtual int GetSlashingLevel(L1Block block, L1Protocol protocol, int accusedLevel)
        {
            return Cache.Protocols.GetCycleEnd(block.Cycle);
        }
    }
}
