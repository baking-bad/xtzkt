using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    class ActivationsCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, byte[] opHash, JsonElement content)
        {
            #region init
            var sender = (L1User)await Cache.Addresses.GetOrCreateAsync(content.RequiredString("pkh"), block);

            var activatedBalance = content
                .Required("metadata")
                .RequiredArray("balance_updates")
                .EnumerateArray()
                .Single(x => x.RequiredString("kind") == "contract" && x.RequiredString("contract") == sender.Hash)
                .RequiredInt64("change");

            var activation = new ActivationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = opHash,
                AddressId = sender.Id,
                Balance = activatedBalance
            };

            var btz = Blind.GetBlindedAddress(content.RequiredString("pkh"), content.RequiredString("secret"));
            var commitment = await Db.Commitments.FirstAsync(x => x.ChainId == Cache.Chain.Get().Id && x.Hash == btz);
            #endregion

            #region apply operation
            Db.TryAttach(sender);
            Receive(sender, activation.Balance);
            sender.ActivationsCount++;

            block.Operations |= L1Operations.Activation;

            commitment.AddressId = sender.Id;
            commitment.Level = block.Level;

            Cache.Chain.Get().ActivationOpsCount++;
            Cache.Statistics.Current.TotalActivated += activation.Balance;
            #endregion

            Db.ActivationOps.Add(activation);
            Context.ActivationOps.Add(activation);
        }

        public virtual async Task Revert(L1Block block, ActivationOperation activation)
        {
            #region entities
            var sender = (L1User)await Cache.Addresses.GetAsync(activation.AddressId);
            var commitment = await Db.Commitments.FirstAsync(x => x.AddressId == activation.AddressId);
            #endregion

            #region revert operation
            Db.TryAttach(sender);
            RevertReceive(sender, activation.Balance);
            sender.ActivationsCount--;

            commitment.AddressId = null;
            commitment.Level = null;

            Cache.Chain.Get().ActivationOpsCount--;
            #endregion

            Db.ActivationOps.Remove(activation);
            Cache.Chain.ReleaseOperationId();
        }
    }
}
