using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto19
{
    class BakerCycleCommit : Proto18.BakerCycleCommit
    {
        public BakerCycleCommit(ProtocolHandler protocol) : base(protocol) { }

        protected override async Task ApplyNewCycle(
            L1Block block,
            Cycle futureCycle,
            IEnumerable<RightsGenerator.BR> futureBakingRights,
            IEnumerable<RightsGenerator.AR> futureAttestationRights,
            List<SnapshotBalance> snapshots,
            Dictionary<int, long> selectedStakes)
        {
            if (block.Cycle == Context.Protocol.FirstCycle)
            {
                var prevProto = await Cache.Protocols.GetAsync(Context.Protocol.Id - 1);
                if (prevProto.ConsensusRightsDelay != Context.Protocol.ConsensusRightsDelay)
                    return;
            }

            await base.ApplyNewCycle(block, futureCycle, futureBakingRights, futureAttestationRights, snapshots, selectedStakes);
        }

        protected override async Task RevertNewCycle(L1Block block)
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
