using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto17
{
    partial class ProtoActivator : Proto15.ProtoActivator
    {
        public ProtoActivator(ProtocolHandler proto) : base(proto) { }

        protected override async Task ActivateContext(L1Chain state)
        {
            await base.ActivateContext(state);
            new InboxCommit(Proto).Init(Cache.Blocks.Current());
        }

        protected override async Task DeactivateContext(L1Chain state)
        {
            await base.DeactivateContext(state);
            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "InboxMessages"
                WHERE "ChainId" = {0}
                """, state.Id);
            Cache.Chain.Get().InboxMessageCounter = 0;
        }

        protected override void SetParameters(L1Protocol protocol, JToken parameters)
        {
            base.SetParameters(protocol, parameters);
            protocol.SmartRollupOriginationSize = parameters["smart_rollup_origination_size"]?.Value<int>() ?? 6_314;
            protocol.SmartRollupStakeAmount = long.Parse(parameters["smart_rollup_stake_amount"]?.Value<string>() ?? "10000000000");
            protocol.SmartRollupChallengeWindow = parameters["smart_rollup_challenge_window_in_blocks"]?.Value<int>() ?? 80_640;
            protocol.SmartRollupCommitmentPeriod = parameters["smart_rollup_commitment_period_in_blocks"]?.Value<int>() ?? 60;
            protocol.SmartRollupTimeoutPeriod = parameters["smart_rollup_timeout_period_in_blocks"]?.Value<int>() ?? 40_320;
        }

        protected override void UpgradeParameters(L1Protocol protocol, L1Protocol prev)
        {
            // nothing to upgrade
        }

        protected override Task MigrateContext(L1Chain state)
        {
            // nothing to migrate
            return Task.CompletedTask;
        }

        protected override Task RevertContext(L1Chain state)
        {
            // nothing to revert
            return Task.CompletedTask;
        }
    }
}
