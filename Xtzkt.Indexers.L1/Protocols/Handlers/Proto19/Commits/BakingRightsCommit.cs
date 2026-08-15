using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto19
{
    class BakingRightsCommit : Proto16.BakingRightsCommit
    {
        public BakingRightsCommit(ProtocolHandler protocol) : base(protocol) { }

        protected override async Task ApplyNewCycle(L1Block block, Cycle futureCycle, Dictionary<int, long> selectedStakes)
        {
            if (block.Cycle == Context.Protocol.FirstCycle)
            {
                var prevProto = await Cache.Protocols.GetAsync(Context.Protocol.Id - 1);
                if (prevProto.ConsensusRightsDelay != Context.Protocol.ConsensusRightsDelay)
                    return;
            }

            await base.ApplyNewCycle(block, futureCycle, selectedStakes);
        }

        public override async Task RevertNewCycle(L1Block block)
        {
            if (block.Cycle == Context.Protocol.FirstCycle)
            {
                var prevProto = await Cache.Protocols.GetAsync(Context.Protocol.Id - 1);
                if (prevProto.ConsensusRightsDelay != Context.Protocol.ConsensusRightsDelay)
                    return;
            }

            await base.RevertNewCycle(block);
        }
    }
}
