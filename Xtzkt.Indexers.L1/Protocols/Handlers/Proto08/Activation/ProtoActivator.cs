using Newtonsoft.Json.Linq;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto08
{
    class ProtoActivator : Proto07.ProtoActivator
    {
        public ProtoActivator(ProtocolHandler proto) : base(proto) { }

        protected override void SetParameters(L1Protocol protocol, JToken parameters)
        {
            base.SetParameters(protocol, parameters);
            protocol.BlocksPerVoting = parameters["blocks_per_voting_period"]?.Value<int>() ?? 20_480;
        }

        protected override void UpgradeParameters(L1Protocol protocol, L1Protocol prev)
        {
            protocol.BlocksPerVoting = 20_480;
        }

        protected override async Task MigrateContext(L1Chain state)
        {

            var prevPeriod = await Cache.Periods.GetAsync(state.VotingPeriod - 1);
            Db.TryAttach(prevPeriod);
            prevPeriod.LastLevel -= 1;

            var newPeriod = await Cache.Periods.GetAsync(state.VotingPeriod);
            Db.TryAttach(newPeriod);
            newPeriod.FirstLevel -= 1;
            newPeriod.LastLevel = newPeriod.FirstLevel + 20_479;
        }
    }
}
