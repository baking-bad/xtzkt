using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Data.Utils;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto12
{
    class PreattestationsCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public void Apply(L1Block block, byte[] opHash, JsonElement content)
        {
            var metadata = content.Required("metadata");
            Apply(block, opHash, metadata.RequiredString("delegate"), GetPower(metadata));
        }

        public void Apply(L1Block block, byte[] opHash, string bakerAddress, long power)
        {
            var baker = Cache.Addresses.GetExistingBaker(bakerAddress);

            var preattestation = new PreattestationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = opHash,
                Power = power,
                BakerId = baker.Id
            };

            Db.TryAttach(baker);
            baker.PreattestationsCount++;

            block.Operations |= L1Operations.Preattestation;

            Cache.Chain.Get().PreattestationOpsCount++;

            Db.PreattestationOps.Add(preattestation);
            Context.PreattestationOps.Add(preattestation);
        }

        public Task Revert(L1Block block, PreattestationOperation preattestation)
        {
            var baker = Cache.Addresses.GetBaker(preattestation.BakerId);
            Db.TryAttach(baker);
            baker.PreattestationsCount--;

            Cache.Chain.Get().PreattestationOpsCount--;

            Db.PreattestationOps.Remove(preattestation);
            Cache.Chain.ReleaseOperationId();

            return Task.CompletedTask;
        }

        protected virtual long GetPower(JsonElement metadata) => metadata.OptionalInt64("preendorsement_power") ?? metadata.RequiredInt64("consensus_power");
    }
}
