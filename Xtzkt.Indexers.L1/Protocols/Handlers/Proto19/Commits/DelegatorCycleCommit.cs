using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto19
{
    class DelegatorCycleCommit(ProtocolHandler protocol) : Proto18.DelegatorCycleCommit(protocol)
    {
        public override async Task Apply(L1Block block, Cycle? futureCycle)
        {
            if (block.Cycle == Context.Protocol.FirstCycle)
            {
                var prevProto = await Cache.Protocols.GetAsync(Context.Protocol.Id - 1);
                if (prevProto.ConsensusRightsDelay != Context.Protocol.ConsensusRightsDelay)
                    return;
            }

            await base.Apply(block, futureCycle);
        }

        public override async Task Revert(L1Block block)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleBegin))
                return;

            if (block.Cycle == Context.Protocol.FirstCycle)
            {
                var prevProto = await Cache.Protocols.GetAsync(Context.Protocol.Id - 1);
                if (prevProto.ConsensusRightsDelay != Context.Protocol.ConsensusRightsDelay)
                    return;
            }

            await base.Revert(block);
        }
    }
}
