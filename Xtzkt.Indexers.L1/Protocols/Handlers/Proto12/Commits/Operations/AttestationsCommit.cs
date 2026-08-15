using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto12
{
    class AttestationsCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public Task Apply(L1Block block, JsonElement op, JsonElement content)
        {
            var metadata = content.Required("metadata");
            return Apply(block, op.RequiredString("hash"), metadata.RequiredString("delegate"), GetPower(metadata));
        }

        public async Task Apply(L1Block block, string opHash, string bakerAddress, long power)
        {
            var baker = Cache.Addresses.GetExistingBaker(bakerAddress);

            var attestation = new AttestationOperation
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
            baker.AttestationsCount++;

            #region set baker active
            var newDeactivationLevel = baker.Staked ? GracePeriod.Reset(block.Level, Context.Protocol) : GracePeriod.Init(block.Level, Context.Protocol);
            if (baker.DeactivationLevel < newDeactivationLevel)
            {
                if (baker.DeactivationLevel <= block.Level)
                    await ActivateBaker(baker);

                attestation.ResetDeactivation = baker.DeactivationLevel;
                baker.DeactivationLevel = newDeactivationLevel;
            }
            #endregion

            block.Operations |= L1Operations.Attestation;
            block.AttestationPower += attestation.Power;

            Cache.Chain.Get().AttestationOpsCount++;

            //Db.AttestationOps.Add(attestation);
            Context.AttestationOps.Add(attestation);
        }

        public async Task Revert(L1Block block, AttestationOperation attestation)
        {
            var baker = Cache.Addresses.GetBaker(attestation.BakerId);
            Db.TryAttach(baker);
            baker.AttestationsCount--;

            #region reset baker activity
            if (attestation.ResetDeactivation != null)
            {
                if (attestation.ResetDeactivation <= block.Level)
                    await DeactivateBaker(baker);

                baker.DeactivationLevel = (int)attestation.ResetDeactivation;
            }
            #endregion

            Cache.Chain.Get().AttestationOpsCount--;

            //Db.AttestationOps.Remove(attestation);
            Cache.Chain.ReleaseOperationId();
        }

        protected virtual long GetPower(JsonElement metadata) => metadata.OptionalInt64("endorsement_power") ?? metadata.RequiredInt64("consensus_power");
    }
}
