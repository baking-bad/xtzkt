using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    class AttestationsCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, byte[] opHash, JsonElement content)
        {
            #region init
            var metadata = content.Required("metadata");
            var reward = metadata
                    .RequiredArray("balance_updates")
                    .EnumerateArray()
                    .FirstOrDefault(x => x.RequiredString("kind")[0] == 'f' && x.RequiredString("category")[0] == 'r');
            var deposit = metadata
                    .RequiredArray("balance_updates")
                    .EnumerateArray()
                    .FirstOrDefault(x => x.RequiredString("kind")[0] == 'f' && x.RequiredString("category")[0] == 'd');

            var sender = Cache.Addresses.GetExistingBaker(metadata.RequiredString("delegate"));

            var attestation = new AttestationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = opHash,
                Power = metadata.RequiredArray("slots").Count(),
                BakerId = sender.Id,
                Reward = reward.ValueKind != JsonValueKind.Undefined ? reward.RequiredInt64("change") : 0,
                Deposit = deposit.ValueKind != JsonValueKind.Undefined ? deposit.RequiredInt64("change") : 0
            };
            #endregion

            #region entities
            Db.TryAttach(sender);
            #endregion

            #region apply operation
            ReceiveLockedRewards(sender, attestation.Reward.Value);

            sender.AttestationsCount++;

            block.Operations |= L1Operations.Attestation;
            block.AttestationPower += attestation.Power;

            var newDeactivationLevel = sender.Staked ? GracePeriod.Reset(attestation.Level, Context.Protocol) : GracePeriod.Init(attestation.Level, Context.Protocol);
            if (sender.DeactivationLevel < newDeactivationLevel)
            {
                if (sender.DeactivationLevel <= attestation.Level)
                    await ActivateBaker(sender);

                attestation.ResetDeactivation = sender.DeactivationLevel;
                sender.DeactivationLevel = newDeactivationLevel;
            }

            Cache.Chain.Get().AttestationOpsCount++;
            Cache.Statistics.Current.TotalCreated += attestation.Reward.Value;
            Cache.Statistics.Current.TotalFrozen += attestation.Reward.Value + attestation.Deposit.Value;
            #endregion

            //Db.AttestationOps.Add(attestation);
            Context.AttestationOps.Add(attestation);
        }

        public virtual async Task Revert(L1Block block, AttestationOperation attestation)
        {
            #region entities
            var sender = Cache.Addresses.GetBaker(attestation.BakerId);
            Db.TryAttach(sender);
            #endregion

            #region revert operation
            RevertReceiveLockedRewards(sender, attestation.Reward!.Value);

            sender.AttestationsCount--;

            if (attestation.ResetDeactivation != null)
            {
                if (attestation.ResetDeactivation <= attestation.Level)
                    await DeactivateBaker(sender);

                sender.DeactivationLevel = (int)attestation.ResetDeactivation;
            }

            Cache.Chain.Get().AttestationOpsCount--;
            #endregion

            //Db.AttestationOps.Remove(attestation);
            Cache.Chain.ReleaseOperationId();
        }
    }
}
