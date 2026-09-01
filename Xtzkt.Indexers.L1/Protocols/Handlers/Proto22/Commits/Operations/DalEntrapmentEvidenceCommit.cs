using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.L1.Protocols.Proto22
{
    class DalEntrapmentEvidenceCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public async Task Apply(L1Block block, JsonElement op, JsonElement content)
        {
            #region init
            var trapLevel = GetTrapLevel(content);
            var trapSlotIndex = content.RequiredInt32("slot_index");

            var accuser = Context.Proposer;
            var offender = Cache.Addresses.GetBaker(await GetAttester(trapLevel, GetConsensusSlot(content)));

            var operation = new DalEntrapmentEvidenceOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = op.RequiredMichelsonOperationHashBytes("hash"),

                AccuserId = accuser.Id,
                OffenderId = offender.Id,

                TrapLevel = trapLevel,
                TrapSlotIndex = trapSlotIndex,
            };
            #endregion

            #region apply operation
            Db.TryAttach(accuser);
            accuser.DalEntrapmentEvidenceOpsCount++;

            if (offender.Id != accuser.Id)
            {
                Db.TryAttach(offender);
                offender.DalEntrapmentEvidenceOpsCount++;
            }

            block.Operations |= L1Operations.DalEntrapmentEvidence;

            Cache.Chain.Get().DalEntrapmentEvidenceOpsCount++;
            #endregion

            Db.DalEntrapmentEvidenceOps.Add(operation);
            Context.DalEntrapmentEvidenceOps.Add(operation);
        }

        public void Revert(DalEntrapmentEvidenceOperation operation)
        {
            #region init
            var accuser = Cache.Addresses.GetBaker(operation.AccuserId);
            var offender = Cache.Addresses.GetBaker(operation.OffenderId);
            #endregion

            #region revert operation
            Db.TryAttach(accuser);
            accuser.DalEntrapmentEvidenceOpsCount--;

            if (offender.Id != accuser.Id)
            {
                Db.TryAttach(offender);
                offender.DalEntrapmentEvidenceOpsCount--;
            }

            Cache.Chain.Get().DalEntrapmentEvidenceOpsCount--;
            #endregion

            Db.DalEntrapmentEvidenceOps.Remove(operation);
            Cache.Chain.ReleaseOperationId();
        }

        protected virtual int GetTrapLevel(JsonElement content)
        {
            return content.Required("attestation").Required("operations").RequiredInt32("level");
        }

        protected virtual int GetConsensusSlot(JsonElement content)
        {
            return content.Required("attestation").Required("operations").RequiredInt32("slot");
        }

        async Task<int> GetAttester(int level, int slot)
        {
            var chain = Cache.Chain.Get();
            var cycleIndex = Context.Protocol.GetCycle(level);
            var cycle = await Db.Cycles.SingleAsync(x => x.ChainId == chain.Id && x.Index == cycleIndex);

            var bakerCycles = await Cache.BakerCycles.GetAsync(cycle.Index);
            var sampler = GetSampler(bakerCycles.Values
                .Where(x => x.BakingPower > 0)
                .Select(x => (x.BakerId, x.BakingPower))
                .ToList());

            return RightsGenerator.GetAttester(sampler, cycle, level, slot);
        }

        Sampler GetSampler(IEnumerable<(int id, long stake)> selection)
        {
            var sorted = selection.OrderByDescending(x =>
            {
                var baker = Cache.Addresses.GetBaker(x.id);
                return new byte[] { (byte)baker.PublicKey![0] }.Concat(Base58.Parse(baker.Hash));
            }, BytesComparer.Instance);

            return new Sampler([..sorted.Select(x => x.id)], [..sorted.Select(x => x.stake)]);
        }
    }
}
