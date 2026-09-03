using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto23
{
    partial class ProtoActivator(ProtocolHandler proto) : Proto22.ProtoActivator(proto)
    {
        protected override void SetParameters(L1Protocol protocol, JToken parameters)
        {
            base.SetParameters(protocol, parameters);
        }

        protected override void UpgradeParameters(L1Protocol protocol, L1Protocol prev)
        {
            if (prev.BlocksPerCycle == 10800 && prev.BlocksPerCommitment == 240)
                protocol.BlocksPerCommitment = 84;
        }

        protected override async Task ActivateContext(L1Chain state)
        {
            await base.ActivateContext(state);
            Cache.Chain.Get().AiActivationLevel = 1;
            UpdateBakersPower();
        }

        protected override async Task MigrateContext(L1Chain state)
        {
            var prevProto = await Cache.Protocols.GetAsync(state.Protocol);
            var nextProto = await Cache.Protocols.GetAsync(state.NextProtocol);

            #region unreveal tz4
            var tz4Addresses = await Db.Addresses
                .OfType<L1User>()
                .Where(x => x.ChainId == state.Id && x.Revealed && x.Hash.StartsWith("tz4"))
                .ToListAsync();

            foreach (var address in tz4Addresses)
            {
                Cache.Addresses.Add(address);
                Db.TryAttach(address);
                address.Revealed = false;
            }
            #endregion

            #region update revelation rewards
            if (prevProto.BlocksPerCommitment != nextProto.BlocksPerCommitment)
            {
                foreach (var cycle in await Db.Cycles.Where(x => x.ChainId == state.Id && x.Index > state.Cycle).ToListAsync())
                {
                    cycle.NonceRevelationReward = cycle.NonceRevelationReward * nextProto.BlocksPerCommitment / prevProto.BlocksPerCommitment;
                    cycle.VdfRevelationReward= cycle.VdfRevelationReward * nextProto.BlocksPerCommitment / prevProto.BlocksPerCommitment;
                }
            }
            #endregion
        }

        protected override Task RevertContext(L1Chain state) => throw new NotImplementedException();
    }
}
