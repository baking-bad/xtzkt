using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto21
{
    class CycleCommit : Proto20.CycleCommit
    {
        public CycleCommit(ProtocolHandler protocol) : base(protocol) { }

        public override async Task Apply(L1Block block)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleBegin))
                return;

            if (block.Cycle == Context.Protocol.FirstCycle)
            {
                var prevProto = await Cache.Protocols.GetAsync(Context.Protocol.Id - 1);
                if (prevProto.ConsensusRightsDelay != Context.Protocol.ConsensusRightsDelay)
                {
                    Cache.Chain.Get().CyclesCount--;
                    return;
                }
            }

            await base.Apply(block);
        }

        public override async Task Revert(L1Block block)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleBegin))
                return;

            if (block.Cycle == Context.Protocol.FirstCycle)
            {
                var prevProto = await Cache.Protocols.GetAsync(Context.Protocol.Id - 1);
                if (prevProto.ConsensusRightsDelay != Context.Protocol.ConsensusRightsDelay)
                {
                    Cache.Chain.Get().CyclesCount++;
                    return;
                }
            }

            await base.Revert(block);
        }
    }
}
